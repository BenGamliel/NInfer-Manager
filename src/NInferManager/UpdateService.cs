using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace NInferManager;

internal sealed record UpdateAsset(string Name, string DownloadUrl, string Digest, long Size);
internal sealed record UpdateInfo(Version Version, string Tag, string PageUrl, string Notes, UpdateAsset Asset);
internal sealed record UpdateProgress(int Percent, string Description);

internal sealed class UpdateService : IDisposable
{
    private const string Repository = "BenGamliel/NInfer-Manager";
    private readonly AppPaths _paths;
    private readonly AppSettings _settings;
    private readonly SettingsStore _settingsStore;
    private readonly AppLogger _logger;
    private readonly HttpClient _client;

    public UpdateService(AppPaths paths, AppSettings settings, SettingsStore settingsStore, AppLogger logger)
    {
        _paths = paths; _settings = settings; _settingsStore = settingsStore; _logger = logger;
        _client = new HttpClient(new SocketsHttpHandler { UseProxy = true }) { Timeout = TimeSpan.FromMinutes(30) };
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NInfer-Manager", CurrentVersion.ToString()));
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        CleanupOldUpdaterCopies();
    }

    public static Version CurrentVersion => typeof(UpdateService).Assembly.GetName().Version is { } value
        ? new Version(value.Major, value.Minor, Math.Max(0, value.Build)) : new Version(1, 0, 0);

    public bool ShouldCheckAutomatically() => _settings.AutoCheckUpdates &&
        (!_settings.LastUpdateCheckUtc.HasValue || DateTime.UtcNow - _settings.LastUpdateCheckUtc.Value >= TimeSpan.FromHours(Math.Clamp(_settings.UpdateCheckHours, 1, 8760)));

    public async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = $"https://api.github.com/repos/{Repository}/releases/latest";
#if DEBUG
        endpoint = Environment.GetEnvironmentVariable("NINFER_MANAGER_UPDATE_API_URL") ?? endpoint;
#endif
        using var response = await _client.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();
        var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("GitHub returned an empty release response.");
        _settings.LastUpdateCheckUtc = DateTime.UtcNow;
        _settingsStore.Save(_settings);
        var version = ParseVersion(release.TagName);
        if (version <= CurrentVersion) return null;
        var expectedName = _paths.IsPortable ? $"NInfer-Manager-Portable-{version}.zip" : $"NInfer-Manager-Setup-{version}.exe";
        var asset = release.Assets.FirstOrDefault(x => x.Name.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Release {release.TagName} does not contain {expectedName}.");
        if (string.IsNullOrWhiteSpace(asset.Digest) || !asset.Digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The update has no GitHub SHA-256 digest and was rejected for safety.");
        return new UpdateInfo(version, release.TagName, release.HtmlUrl, release.Body ?? string.Empty,
            new UpdateAsset(asset.Name, asset.DownloadUrl, asset.Digest[7..], asset.Size));
    }

    public async Task<string> DownloadAsync(UpdateInfo update, IProgress<UpdateProgress>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.UpdatesDirectory);
        var destination = Path.Combine(_paths.UpdatesDirectory, update.Asset.Name);
        var partial = destination + ".part";
        using var response = await _client.GetAsync(update.Asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? update.Asset.Size;
        await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true))
        {
            var buffer = new byte[1024 * 1024];
            long received = 0;
            while (true)
            {
                var count = await input.ReadAsync(buffer, cancellationToken);
                if (count == 0) break;
                await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                received += count;
                var percent = total > 0 ? (int)Math.Clamp(received * 100L / total, 0, 100) : 0;
                progress?.Report(new UpdateProgress(percent, $"Downloading update — {received / 1024d / 1024d:0.0} / {total / 1024d / 1024d:0.0} MiB"));
            }
        }
        progress?.Report(new UpdateProgress(100, "Verifying SHA-256..."));
        string actual;
        await using (var verification = File.OpenRead(partial))
            actual = Convert.ToHexString(await SHA256.HashDataAsync(verification, cancellationToken));
        if (!actual.Equals(update.Asset.Digest, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(partial);
            throw new InvalidDataException("The downloaded update failed SHA-256 verification and was deleted.");
        }
        File.Move(partial, destination, true);
        return destination;
    }

    public void LaunchInstaller(string packagePath)
    {
        if (_paths.IsPortable)
        {
            var updater = Path.Combine(_paths.UpdatesDirectory, $"NInfer-Manager-Updater-{Guid.NewGuid():N}.exe");
            File.Copy(Environment.ProcessPath!, updater, true);
            Process.Start(new ProcessStartInfo
            {
                FileName = updater,
                Arguments = $"--apply-portable-update --wait-pid {Environment.ProcessId} --zip \"{packagePath}\" --target \"{_paths.AppDirectory}\" --exe \"NInfer Manager.exe\"",
                UseShellExecute = true,
                WorkingDirectory = _paths.UpdatesDirectory,
            });
        }
        else
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = packagePath,
                Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART",
                UseShellExecute = true,
            });
        }
    }

    private static Version ParseVersion(string tag)
    {
        var clean = tag.Trim().TrimStart('v', 'V').Split('-', 2)[0];
        return Version.TryParse(clean, out var version) ? version : throw new InvalidOperationException($"Invalid release version: {tag}");
    }

    private void CleanupOldUpdaterCopies()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_paths.UpdatesDirectory, "NInfer-Manager-Updater-*.exe"))
                try { File.Delete(file); } catch { }
        }
        catch { }
    }

    public void Dispose() => _client.Dispose();

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = string.Empty;
        [JsonPropertyName("html_url")] public string HtmlUrl { get; set; } = string.Empty;
        [JsonPropertyName("body")] public string? Body { get; set; }
        [JsonPropertyName("assets")] public List<GitHubAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("browser_download_url")] public string DownloadUrl { get; set; } = string.Empty;
        [JsonPropertyName("digest")] public string Digest { get; set; } = string.Empty;
        [JsonPropertyName("size")] public long Size { get; set; }
    }
}
