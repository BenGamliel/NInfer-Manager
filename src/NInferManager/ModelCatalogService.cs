using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NInferManager;

internal sealed class ModelCatalogEntry
{
    public string DisplayName { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string Weights { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public bool Vision { get; set; }
    public int RecommendedContext { get; set; } = 150000;
    public bool DiscoveredOnline { get; set; }

    public string DownloadUrl => $"https://huggingface.co/{Repository}/resolve/main/{Uri.EscapeDataString(FileName)}?download=true";
    public string ModelCardUrl => $"https://huggingface.co/{Repository}";
    public string SizeText => SizeBytes > 0 ? $"{SizeBytes / 1024d / 1024d / 1024d:0.00} GiB" : "Unknown";
}

internal sealed class ModelCatalogService : IDisposable
{
    private const string GitHubContentsUrl = "https://api.github.com/repos/Neroued/ninfer/contents/model-cards";
    private readonly AppPaths _paths;
    private readonly AppSettings _settings;
    private readonly SettingsStore _settingsStore;
    private readonly AppLogger _logger;
    private readonly HttpClient _client;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private List<ModelCatalogEntry> _entries;

    public ModelCatalogService(AppPaths paths, AppSettings settings, SettingsStore settingsStore, AppLogger logger)
    {
        _paths = paths;
        _settings = settings;
        _settingsStore = settingsStore;
        _logger = logger;
        _client = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("NInfer-Manager/1.0 (+https://github.com/) ");
        _entries = LoadEmbedded();
        MergeCache();
    }

    public IReadOnlyList<ModelCatalogEntry> Entries => _entries;
    public event Action? CatalogChanged;

    public ModelCatalogEntry? Find(string fileName) => _entries.FirstOrDefault(x => x.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));

    public bool ShouldCheckAutomatically()
    {
        if (!_settings.AutoCheckCatalog) return false;
        if (_settings.LastCatalogCheckUtc is null) return true;
        return DateTime.UtcNow - _settings.LastCatalogCheckUtc >= TimeSpan.FromHours(Math.Max(1, _settings.CatalogCheckHours));
    }

    public async Task<int> RefreshOnlineAsync(CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            _logger.Write("Checking the official NInfer model catalog");
            using var response = await _client.GetAsync(GitHubContentsUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var discovered = new List<ModelCatalogEntry>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("type", out var type) || type.GetString() != "dir") continue;
                var name = item.GetProperty("name").GetString();
                if (string.IsNullOrWhiteSpace(name)) continue;
                var rawUrl = $"https://raw.githubusercontent.com/Neroued/ninfer/master/model-cards/{Uri.EscapeDataString(name)}/README.md";
                try
                {
                    var markdown = await _client.GetStringAsync(rawUrl, cancellationToken);
                    var entry = ParseModelCard(markdown, name);
                    if (entry is not null) discovered.Add(entry);
                }
                catch (Exception exception) { _logger.Write($"Could not read model card {name}", exception); }
            }

            var before = _entries.Select(x => x.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in discovered) MergeEntry(entry);
            _entries = _entries.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
            File.WriteAllText(_paths.CatalogCacheFile, JsonSerializer.Serialize(_entries, SettingsStore.JsonOptions));
            _settings.LastCatalogCheckUtc = DateTime.UtcNow;
            _settingsStore.Save(_settings);
            var newCount = _entries.Count(x => !before.Contains(x.FileName));
            _logger.Write($"Model catalog check complete; {newCount} new model(s)");
            CatalogChanged?.Invoke();
            return newCount;
        }
        finally { _refreshGate.Release(); }
    }

    private List<ModelCatalogEntry> LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NInferManager.Assets.catalog.json")
            ?? throw new InvalidOperationException("The embedded model catalog is missing.");
        return JsonSerializer.Deserialize<List<ModelCatalogEntry>>(stream, SettingsStore.JsonOptions) ?? new List<ModelCatalogEntry>();
    }

    private void MergeCache()
    {
        try
        {
            if (!File.Exists(_paths.CatalogCacheFile)) return;
            var cached = JsonSerializer.Deserialize<List<ModelCatalogEntry>>(File.ReadAllText(_paths.CatalogCacheFile), SettingsStore.JsonOptions);
            if (cached is null) return;
            foreach (var entry in cached.Where(IsValid)) MergeEntry(entry);
        }
        catch (Exception exception) { _logger.Write("Cached model catalog could not be read", exception); }
    }

    private void MergeEntry(ModelCatalogEntry entry)
    {
        var index = _entries.FindIndex(x => x.FileName.Equals(entry.FileName, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            if (IsValid(entry))
            {
                entry.DiscoveredOnline = _entries[index].DiscoveredOnline;
                _entries[index] = entry;
            }
        }
        else if (IsValid(entry)) _entries.Add(entry);
    }

    private static bool IsValid(ModelCatalogEntry entry) =>
        entry.FileName.EndsWith(".ninfer", StringComparison.OrdinalIgnoreCase) &&
        entry.SizeBytes > 0 && Regex.IsMatch(entry.Sha256, "^[a-fA-F0-9]{64}$") &&
        entry.Repository.StartsWith("neroued/", StringComparison.OrdinalIgnoreCase);

    private static ModelCatalogEntry? ParseModelCard(string markdown, string directoryName)
    {
        static string Capture(string text, string pattern) => Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline).Groups[1].Value.Trim();
        var file = Capture(markdown, @"^Filename\s*\|\s*`([^`]+)`");
        var sha = Capture(markdown, @"^SHA-256\s*\|\s*`([a-fA-F0-9]{64})`");
        var sizeRaw = Capture(markdown, @"^Size\s*\|\s*([\d,]+)\s+bytes").Replace(",", "");
        if (!long.TryParse(sizeRaw, out var size) || string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(sha)) return null;
        var repository = Capture(markdown, @"This model card is (?:the version-controlled source for\s+)?(?:`|\[)?(neroued/[A-Za-z0-9._-]+)");
        if (string.IsNullOrWhiteSpace(repository)) repository = "neroued/" + directoryName;
        var modelId = Capture(markdown, @"^NInfer model ID\s*\|\s*`([^`]+)`");
        var weights = Capture(markdown, @"^NInfer weights ID\s*\|\s*`([^`]+)`");
        var title = Capture(markdown, @"^#\s+(.+?)(?:\s+for NInfer)?$");
        if (string.IsNullOrWhiteSpace(title)) title = directoryName.Replace("-NInfer", "", StringComparison.OrdinalIgnoreCase);
        if (weights.Equals("nvfp4", StringComparison.OrdinalIgnoreCase) && !title.Contains("NVFP4", StringComparison.OrdinalIgnoreCase)) title += " NVFP4";
        return new ModelCatalogEntry
        {
            DisplayName = title,
            Repository = repository,
            FileName = file,
            ModelId = weights.Equals("nvfp4", StringComparison.OrdinalIgnoreCase) ? modelId + "-nvfp4" : modelId,
            Weights = weights,
            SizeBytes = size,
            Sha256 = sha.ToLowerInvariant(),
            Vision = markdown.Contains("Vision", StringComparison.OrdinalIgnoreCase),
            RecommendedContext = 150000,
            DiscoveredOnline = true,
        };
    }

    public void Dispose() { _client.Dispose(); _refreshGate.Dispose(); }
}
