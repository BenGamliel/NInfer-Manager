using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NInferManager;

internal sealed class ApiProxy : IAsyncDisposable
{
    public const string Host = "127.0.0.1";
    private static readonly HashSet<string> HopHeaders = new(StringComparer.OrdinalIgnoreCase)
    { "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization", "TE", "Trailer", "Transfer-Encoding", "Upgrade" };

    private readonly EngineController _engine;
    private readonly AppPaths _paths;
    private readonly AppSettings _settings;
    private readonly AppLogger _logger;
    private readonly HttpClient _client;
    private readonly SemaphoreSlim _idleGate = new(1, 1);
    private WebApplication? _app;
    private System.Threading.Timer? _idleTimer;
    private long _lastInferenceTicks = DateTime.UtcNow.Ticks;
    private int _activeRequests;
    private int _port;

    public ApiProxy(EngineController engine, AppPaths paths, AppSettings settings, AppLogger logger, int initialPort)
    {
        _engine = engine; _paths = paths; _settings = settings; _logger = logger;
        _port = initialPort;
        _client = new HttpClient(new SocketsHttpHandler { UseProxy = false, MaxConnectionsPerServer = 100, PooledConnectionLifetime = TimeSpan.FromMinutes(10) })
        { Timeout = Timeout.InfiniteTimeSpan };
        _engine.StateChanged += state => { if (state == EngineState.Ready) MarkInferenceActivity(); };
    }

    public int Port => _port;
    public string WebUiUrl => $"http://{Host}:{Port}";
    public string ApiBaseUrl => WebUiUrl + "/v1";
    public int ActiveRequests => Volatile.Read(ref _activeRequests);

    public async Task StartAsync()
    {
        if (_app is not null) return;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = Array.Empty<string>(),
            ApplicationName = typeof(ApiProxy).Assembly.FullName,
            ContentRootPath = _paths.AppDirectory,
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel(k => k.Listen(IPAddress.Loopback, Port));
        var app = builder.Build();
        if (Directory.Exists(_paths.WebUiDirectory))
        {
            var provider = new PhysicalFileProvider(_paths.WebUiDirectory);
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = provider });
        }
        app.Run(HandleAsync);
        try { await app.StartAsync(); }
        catch { await app.DisposeAsync(); throw; }
        _app = app;
        _idleTimer = new System.Threading.Timer(_ => _ = CheckIdleAsync(), null, Timeout.Infinite, Timeout.Infinite);
        ScheduleIdleCheck();
        _logger.Write($"OpenAI-compatible API listening at {ApiBaseUrl}");
    }

    public async Task RestartAsync(int newPort)
    {
        if (newPort == Port) return;
        if (!PortManagement.IsAvailable(newPort)) throw new InvalidOperationException($"Port {newPort} is already in use.");
        var oldPort = _port;
        var old = _app;
        _app = null;
        _idleTimer?.Dispose(); _idleTimer = null;
        if (old is not null) { await old.StopAsync(); await old.DisposeAsync(); }
        _port = newPort;
        try { await StartAsync(); }
        catch
        {
            _logger.Write($"API could not restart on port {newPort}");
            _port = oldPort;
            if (PortManagement.IsAvailable(oldPort))
                try { await StartAsync(); } catch (Exception rollback) { _logger.Write("API rollback also failed", rollback); }
            throw;
        }
    }

    public void MarkInferenceActivity()
    {
        Interlocked.Exchange(ref _lastInferenceTicks, DateTime.UtcNow.Ticks);
        ScheduleIdleCheck();
    }

    public void ApplyLifecycleSettings() => ScheduleIdleCheck();

    private async Task HandleAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        AddCors(context);
        if (HttpMethods.IsOptions(context.Request.Method)) { context.Response.StatusCode = StatusCodes.Status204NoContent; return; }

        if (path.Equals("/manager/health", StringComparison.OrdinalIgnoreCase))
        {
            await context.Response.WriteAsJsonAsync(new { status = "ok", engine = _engine.State.ToString(), model = _engine.ActiveEntryOrNull?.ModelId, active_requests = ActiveRequests });
            return;
        }

        if (path.StartsWith("/v1", StringComparison.OrdinalIgnoreCase) && !Authorized(context))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = new { message = "Invalid or missing API key.", type = "authentication_error" } });
            return;
        }

        if (HttpMethods.IsGet(context.Request.Method) && path.Equals("/v1/models", StringComparison.OrdinalIgnoreCase) && !_engine.IsLoaded)
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                @object = "list",
                data = _engine.ActiveEntryOrNull is { } active
                    ? new[] { new { id = active.ModelId, @object = "model", owned_by = "local", meta = new { n_ctx = _engine.ActiveProfile.MaxContext } } }
                    : Array.Empty<object>(),
            });
            return;
        }

        if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)) && !path.StartsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            var index = Path.Combine(_paths.WebUiDirectory, "index.html");
            if (!File.Exists(index)) { context.Response.StatusCode = 404; await context.Response.WriteAsync("Web UI is not installed."); return; }
            context.Response.ContentType = "text/html; charset=utf-8";
            if (!HttpMethods.IsHead(context.Request.Method)) await context.Response.SendFileAsync(index);
            return;
        }

        await ForwardAsync(context);
    }

    private async Task ForwardAsync(HttpContext context)
    {
        var inference = HttpMethods.IsPost(context.Request.Method);
        if (inference) MarkInferenceActivity();
        Interlocked.Increment(ref _activeRequests);
        try
        {
            await _engine.EnsureLoadedAsync(context.RequestAborted);
            var target = $"http://{EngineController.Host}:{_engine.Port}{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
            using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), target);
            if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding") || HttpMethods.IsPost(context.Request.Method))
                request.Content = new StreamContent(context.Request.Body);
            foreach (var header in context.Request.Headers)
            {
                if (HopHeaders.Contains(header.Key) || header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)) continue;
                if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && request.Content is not null)
                    request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            context.Response.StatusCode = (int)response.StatusCode;
            foreach (var header in response.Headers) if (!HopHeaders.Contains(header.Key)) context.Response.Headers[header.Key] = header.Value.ToArray();
            foreach (var header in response.Content.Headers) if (!HopHeaders.Contains(header.Key)) context.Response.Headers[header.Key] = header.Value.ToArray();
            context.Response.Headers.Remove("transfer-encoding");
            await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.Write("API request failed", exception);
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsJsonAsync(new { error = new { message = exception.Message, type = "ninfer_manager_error" } });
            }
        }
        finally
        {
            Interlocked.Decrement(ref _activeRequests);
            if (inference) MarkInferenceActivity();
        }
    }

    private bool Authorized(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey)) return true;
        var supplied = context.Request.Headers.Authorization.ToString();
        if (supplied.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) supplied = supplied[7..];
        var expectedBytes = Encoding.UTF8.GetBytes(_settings.ApiKey);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }

    private void AddCors(HttpContext context)
    {
        if (!_settings.CorsEnabled) return;
        context.Response.Headers.AccessControlAllowOrigin = "*";
        context.Response.Headers.AccessControlAllowMethods = "GET, HEAD, POST, PUT, DELETE, OPTIONS";
        context.Response.Headers.AccessControlAllowHeaders = "*";
    }

    private void ScheduleIdleCheck()
    {
        if (_idleTimer is null) return;
        if (!_settings.AutoUnloadEnabled) { _idleTimer.Change(Timeout.Infinite, Timeout.Infinite); return; }
        var delay = TimeSpan.FromMinutes((double)Math.Max(0.1m, _settings.IdleMinutes));
        _idleTimer.Change(delay, Timeout.InfiniteTimeSpan);
    }

    private async Task CheckIdleAsync()
    {
        if (!_settings.AutoUnloadEnabled || !_engine.IsLoaded || ActiveRequests != 0) { ScheduleIdleCheck(); return; }
        if (!await _idleGate.WaitAsync(0)) return;
        try
        {
            var idle = DateTime.UtcNow - new DateTime(Interlocked.Read(ref _lastInferenceTicks), DateTimeKind.Utc);
            var threshold = TimeSpan.FromMinutes((double)_settings.IdleMinutes);
            if (idle >= threshold && ActiveRequests == 0) await _engine.UnloadAsync($"idle for {_settings.IdleMinutes} minute(s)");
            else ScheduleIdleCheck();
        }
        catch (Exception exception) { _logger.Write("Automatic unload failed", exception); ScheduleIdleCheck(); }
        finally { _idleGate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        _idleTimer?.Dispose(); _idleTimer = null;
        if (_app is not null)
        {
            try { await _app.StopAsync(TimeSpan.FromSeconds(5)); } catch (Exception e) { _logger.Write("API shutdown error", e); }
            await _app.DisposeAsync(); _app = null;
        }
        _client.Dispose(); _idleGate.Dispose();
    }
}
