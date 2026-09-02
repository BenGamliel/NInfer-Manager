using System.Diagnostics;
using System.Globalization;

namespace NInferManager;

internal enum EngineState { Unloaded, Loading, Ready, Unloading, Error }

internal sealed class EngineController : IAsyncDisposable
{
    public const string Host = "127.0.0.1";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly AppPaths _paths;
    private readonly AppSettings _settings;
    private readonly ModelCatalogService _catalog;
    private readonly AppLogger _logger;
    private readonly ProcessJob _job;
    private readonly HttpClient _readyClient = new(new SocketsHttpHandler { UseProxy = false }) { Timeout = TimeSpan.FromMilliseconds(750) };
    private Process? _process;
    private volatile EngineState _state;

    public EngineController(AppPaths paths, AppSettings settings, ModelCatalogService catalog, AppLogger logger, ProcessJob job)
    {
        _paths = paths; _settings = settings; _catalog = catalog; _logger = logger; _job = job;
    }

    public int Port => ReadPort("NINFER_MANAGER_BACKEND_PORT", _settings.BackendPort);
    public EngineState State => _state;
    public bool IsLoaded => _state == EngineState.Ready && _process is { HasExited: false };
    public int? ProcessId => _process is { HasExited: false } ? _process.Id : null;
    public event Action<EngineState>? StateChanged;

    public ModelCatalogEntry? ActiveEntryOrNull => string.IsNullOrWhiteSpace(_settings.ActiveModelFile) ? null : _catalog.Find(_settings.ActiveModelFile);
    public bool HasActiveModel => ActiveEntryOrNull is not null;
    public ModelCatalogEntry ActiveEntry => ActiveEntryOrNull
        ?? throw new InvalidOperationException("No model is selected. Open Model Manager, install a model, and select Set active.");
    public string ActiveModelPath => Path.Combine(_paths.ModelsDirectory, ActiveEntry.FileName);
    public ModelProfile ActiveProfile => _settings.GetProfile(ActiveEntry);

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (IsLoaded) return;
            ValidateFiles();
            SetState(EngineState.Loading);
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(_paths.EngineDirectory, "ninfer-serve.exe"),
                WorkingDirectory = _paths.EngineDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var argument in BuildArguments()) startInfo.ArgumentList.Add(argument);
            _logger.Write("Starting NInfer: " + BuildCommandPreview());
            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) _logger.Write("NInfer: " + e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) _logger.Write("NInfer: " + e.Data); };
            process.Exited += (_, _) =>
            {
                _logger.Write($"NInfer exited with code {SafeExitCode(process)}");
                if (_state is EngineState.Ready or EngineState.Loading) SetState(EngineState.Unloaded);
            };
            if (!process.Start()) throw new InvalidOperationException("Windows did not start ninfer-serve.exe.");
            _job.Assign(process);
            _process = process;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (process.HasExited) throw new InvalidOperationException($"NInfer exited during startup with code {process.ExitCode}. Open Logs for details.");
                if (await IsReadyAsync(cancellationToken))
                {
                    SetState(EngineState.Ready);
                    _logger.Write("NInfer is ready");
                    return;
                }
                await Task.Delay(100, cancellationToken);
            }
            throw new TimeoutException("NInfer did not become ready within 90 seconds.");
        }
        catch
        {
            SetState(EngineState.Error);
            KillProcess();
            throw;
        }
        finally { _gate.Release(); }
    }

    public async Task UnloadAsync(string reason)
    {
        await _gate.WaitAsync();
        try
        {
            if (_process is null || _process.HasExited) { KillProcess(); SetState(EngineState.Unloaded); return; }
            SetState(EngineState.Unloading);
            _logger.Write("Unloading NInfer: " + reason);
            KillProcess();
            SetState(EngineState.Unloaded);
        }
        finally { _gate.Release(); }
    }

    public async Task RestartAsync() { await UnloadAsync("restart"); await EnsureLoadedAsync(); }

    public string BuildCommandPreview()
    {
        if (!HasActiveModel) return "Choose and install a model to generate the NInfer command.";
        static string Quote(string value) => value.Contains(' ') ? $"\"{value}\"" : value;
        return "ninfer-serve.exe " + string.Join(" ", BuildArguments().Select(Quote));
    }

    private IEnumerable<string> BuildArguments()
    {
        var p = ActiveProfile;
        var args = new List<string>
        {
            ActiveModelPath,
            "--model-id", ActiveEntry.ModelId,
            "--max-context", p.MaxContext.ToString(CultureInfo.InvariantCulture),
            "--default-max-tokens", p.DefaultMaxTokens.ToString(CultureInfo.InvariantCulture),
            "--kv-dtype", p.KvPrecision.ToString().ToLowerInvariant(),
            "--device", p.Device.ToString(CultureInfo.InvariantCulture),
            "--max-concurrency", p.MaxConcurrency.ToString(CultureInfo.InvariantCulture),
            "--max-pending-requests", p.MaxPendingRequests.ToString(CultureInfo.InvariantCulture),
            "--pending-timeout-ms", p.PendingTimeoutMs.ToString(CultureInfo.InvariantCulture),
            "--log-stats-interval-ms", p.LogStatsIntervalMs.ToString(CultureInfo.InvariantCulture),
            "--max-request-mib", p.MaxRequestMiB.ToString(CultureInfo.InvariantCulture),
            "--media-cache-mib", p.MediaCacheMiB.ToString(CultureInfo.InvariantCulture),
            "--media-live-mib", p.MediaLiveMiB.ToString(CultureInfo.InvariantCulture),
            "--media-preprocess-threads", p.MediaPreprocessThreads.ToString(CultureInfo.InvariantCulture),
            "--host-state-slots", p.HostStateSlots.ToString(CultureInfo.InvariantCulture),
            "--host-kv-mib", p.HostKvMiB.ToString(CultureInfo.InvariantCulture),
            "--response-store-max-records", p.ResponseStoreMaxRecords.ToString(CultureInfo.InvariantCulture),
            "--response-store-max-mib", p.ResponseStoreMaxMiB.ToString(CultureInfo.InvariantCulture),
            "--host", Host,
            "--port", Port.ToString(CultureInfo.InvariantCulture),
            "--webui-dir", _paths.WebUiDirectory,
        };
        if (p.KvCapacityMode == KvCapacityMode.Auto) Add(args, "--kv-capacity", "auto");
        if (p.KvCapacityMode == KvCapacityMode.Custom) Add(args, "--kv-capacity", p.CustomKvCapacity);
        if (p.SpeculativeMode != SpeculativeMode.Disabled) { Add(args, "--spec", p.SpeculativeMode.ToString().ToLowerInvariant()); Add(args, "--draft-tokens", p.DraftTokens); }
        if (p.LmHeadDraft) args.Add("--lm-head-draft");
        if (p.VisionEnabled) args.Add("--vision");
        if (!p.CudaGraphEnabled) args.Add("--no-cuda-graph");
        if (!p.PrefixReuseEnabled) args.Add("--no-prefix-reuse");
        if (!p.ThinkingEnabled) args.Add("--no-thinking");
        if (p.PreserveThinking) args.Add("--preserve-thinking");
        if (_settings.CorsEnabled) args.Add("--cors");
        if (p.Greedy) args.Add("--greedy");
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey)) Add(args, "--api-key", _settings.ApiKey);
        if (p.PrefillChunk > 0) Add(args, "--prefill-chunk", p.PrefillChunk);
        if (p.DeviceStateSlots >= 0) Add(args, "--device-state-slots", p.DeviceStateSlots);
        if (p.MaxPrivateContinuations >= 0) Add(args, "--max-private-continuations", p.MaxPrivateContinuations);
        if (p.MaxSharedPrefixes >= 0) Add(args, "--max-shared-prefixes", p.MaxSharedPrefixes);
        if (p.MaxLongAnchorsPerContinuation >= 0) Add(args, "--max-long-anchors-per-continuation", p.MaxLongAnchorsPerContinuation);
        if (p.MaxCacheMarkersPerRequest >= 0) Add(args, "--max-cache-markers-per-request", p.MaxCacheMarkersPerRequest);
        if (p.DefaultThinkingBudget is not null) Add(args, "--default-thinking-budget", p.DefaultThinkingBudget.Value);
        if (p.Temperature is not null) Add(args, "--temperature", p.Temperature.Value);
        if (p.TopP is not null) Add(args, "--top-p", p.TopP.Value);
        if (p.TopK is not null) Add(args, "--top-k", p.TopK.Value);
        if (p.MinP is not null) Add(args, "--min-p", p.MinP.Value);
        if (p.PresencePenalty is not null) Add(args, "--presence-penalty", p.PresencePenalty.Value);
        if (p.FrequencyPenalty is not null) Add(args, "--frequency-penalty", p.FrequencyPenalty.Value);
        if (p.Seed is not null) Add(args, "--seed", p.Seed.Value);
        if (!string.IsNullOrWhiteSpace(p.ContextCostPresetsFile)) Add(args, "--context-cost-presets", p.ContextCostPresetsFile);
        if (!string.IsNullOrWhiteSpace(p.RequestLogJsonlFile)) Add(args, "--request-log-jsonl", p.RequestLogJsonlFile);
        return args;
    }

    private static void Add(List<string> args, string name, object value) { args.Add(name); args.Add(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty); }

    private void ValidateFiles()
    {
        if (!HasActiveModel) throw new InvalidOperationException("No model is selected. Open Model Manager, install a model, and select Set active.");
        var executable = Path.Combine(_paths.EngineDirectory, "ninfer-serve.exe");
        if (!File.Exists(executable)) throw new FileNotFoundException("NInfer Engine is missing. Reinstall the application or select a valid Engine folder.", executable);
        if (!File.Exists(ActiveModelPath)) throw new FileNotFoundException("The selected model is not installed. Install it from the Models page first.", ActiveModelPath);
        if (!Directory.Exists(_paths.WebUiDirectory)) throw new DirectoryNotFoundException("The bundled Web UI is missing: " + _paths.WebUiDirectory);
    }

    private async Task<bool> IsReadyAsync(CancellationToken token)
    {
        try { using var response = await _readyClient.GetAsync($"http://{Host}:{Port}/v1/models", token); return response.IsSuccessStatusCode; }
        catch { return false; }
    }

    private void KillProcess()
    {
        var process = _process; _process = null;
        if (process is null) return;
        try { if (!process.HasExited) { process.Kill(true); process.WaitForExit(10000); } }
        catch (Exception exception) { _logger.Write("NInfer could not be stopped cleanly", exception); }
        finally { process.Dispose(); }
    }

    private void SetState(EngineState state) { _state = state; StateChanged?.Invoke(state); }
    private static int SafeExitCode(Process process) { try { return process.ExitCode; } catch { return -1; } }
    private static int ReadPort(string name, int fallback) => int.TryParse(Environment.GetEnvironmentVariable(name), out var port) && port is > 0 and <= 65535 ? port : fallback;

    public async ValueTask DisposeAsync() { await UnloadAsync("manager exit"); _readyClient.Dispose(); _gate.Dispose(); }
}
