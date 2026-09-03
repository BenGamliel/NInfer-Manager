using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NInferManager;

internal enum KvPrecision { Bf16, Int8, Fp8 }
internal enum KvCapacityMode { MatchContext, Auto, Custom }
internal enum SpeculativeMode { Disabled, Mtp, Dflash }

internal sealed class AppSettings
{
    [Category("Appearance"), DisplayName("Color theme")]
    public ThemePreference Theme { get; set; } = ThemePreference.Light;

    [Category("Application"), DisplayName("Start minimized")]
    public bool StartMinimized { get; set; }

    [Category("Application"), DisplayName("Close to notification area")]
    public bool CloseToTray { get; set; } = true;

    [Category("Application"), DisplayName("Check model catalog automatically")]
    public bool AutoCheckCatalog { get; set; } = true;

    [Category("Application"), DisplayName("Catalog check interval (hours)")]
    public int CatalogCheckHours { get; set; } = 24;

    [Category("Updates"), DisplayName("Check for application updates automatically")]
    public bool AutoCheckUpdates { get; set; } = true;

    [Category("Updates"), DisplayName("Update check interval (hours)")]
    public int UpdateCheckHours { get; set; } = 24;

    [Category("Serving"), DisplayName("Public API port")]
    public int PublicPort { get; set; } = 48173;

    [Category("Serving"), DisplayName("Lock public API port")]
    [Description("When enabled, startup stops and asks for a new port instead of switching automatically if this port is busy.")]
    public bool LockPublicPort { get; set; }

    [Category("Serving"), DisplayName("Internal NInfer port")]
    public int BackendPort { get; set; } = 48174;

    [Category("Serving"), DisplayName("API key"), PasswordPropertyText(true)]
    [Description("Optional bearer token. Leave empty for local-only unauthenticated use.")]
    public string ApiKey { get; set; } = string.Empty;

    [Category("Serving"), DisplayName("Allow CORS")]
    public bool CorsEnabled { get; set; } = true;

    [Category("Model lifecycle"), DisplayName("Automatic VRAM unload")]
    public bool AutoUnloadEnabled { get; set; } = true;

    [Category("Model lifecycle"), DisplayName("Idle minutes before unload")]
    public decimal IdleMinutes { get; set; } = 3;

    [Browsable(false)] public bool FirstRunCompleted { get; set; }
    [Browsable(false)] public string ActiveModelFile { get; set; } = string.Empty;
    [Browsable(false)] public DateTime? LastCatalogCheckUtc { get; set; }
    [Browsable(false)] public DateTime? LastUpdateCheckUtc { get; set; }
    [Browsable(false)] public Dictionary<string, ModelProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public ModelProfile GetProfile(ModelCatalogEntry entry)
    {
        if (!Profiles.TryGetValue(entry.FileName, out var profile))
        {
            profile = ModelProfile.CreateRecommended(entry);
            Profiles[entry.FileName] = profile;
        }
        return profile;
    }
}

internal sealed class ModelProfile
{
    [Category("Model and context"), DisplayName("Vision and video")]
    public bool VisionEnabled { get; set; } = true;

    [Category("Model and context"), DisplayName("Maximum context tokens")]
    public int MaxContext { get; set; } = 150000;

    [Category("Model and context"), DisplayName("Default maximum output tokens")]
    public int DefaultMaxTokens { get; set; } = 150000;

    [Category("Model and context"), DisplayName("KV cache precision (K and V)")]
    [Description("NInfer exposes one shared precision for both K and V.")]
    public KvPrecision KvPrecision { get; set; } = KvPrecision.Int8;

    [Category("Model and context"), DisplayName("KV capacity mode")]
    public KvCapacityMode KvCapacityMode { get; set; } = KvCapacityMode.MatchContext;

    [Category("Model and context"), DisplayName("Custom KV capacity")]
    public int CustomKvCapacity { get; set; } = 150000;

    [Category("Model and context"), DisplayName("CUDA device")]
    public int Device { get; set; }

    [Category("Speculative decoding"), DisplayName("Speculative mode")]
    public SpeculativeMode SpeculativeMode { get; set; } = SpeculativeMode.Mtp;

    [Category("Speculative decoding"), DisplayName("Draft tokens")]
    public int DraftTokens { get; set; } = 3;

    [Category("Speculative decoding"), DisplayName("Optimized LM-head draft")]
    public bool LmHeadDraft { get; set; } = true;

    [Category("Performance"), DisplayName("CUDA graphs")]
    public bool CudaGraphEnabled { get; set; } = true;

    [Category("Performance"), DisplayName("Prefix reuse")]
    public bool PrefixReuseEnabled { get; set; } = true;

    [Category("Performance"), DisplayName("Prefill chunk tokens")]
    [Description("0 uses the engine default.")]
    public int PrefillChunk { get; set; }

    [Category("Performance"), DisplayName("Maximum concurrency")]
    public int MaxConcurrency { get; set; } = 1;

    [Category("Vision media"), DisplayName("Retained media cache MiB")]
    public int MediaCacheMiB { get; set; } = 1024;

    [Category("Vision media"), DisplayName("Live media payload MiB")]
    public int MediaLiveMiB { get; set; } = 2048;

    [Category("Vision media"), DisplayName("Media preprocessing threads")]
    [Description("0 selects the automatic worker count.")]
    public int MediaPreprocessThreads { get; set; }

    [Category("Queue"), DisplayName("Maximum pending requests")]
    public int MaxPendingRequests { get; set; } = 50;

    [Category("Queue"), DisplayName("Pending timeout milliseconds")]
    public int PendingTimeoutMs { get; set; } = 3000000;

    [Category("Queue"), DisplayName("Maximum request MiB")]
    public int MaxRequestMiB { get; set; } = 384;

    [Category("Diagnostics"), DisplayName("Stats interval milliseconds")]
    public int LogStatsIntervalMs { get; set; } = 5000;

    [Category("Context cache"), DisplayName("Device state slots")]
    [Description("-1 uses NInfer's adaptive default.")]
    public int DeviceStateSlots { get; set; } = -1;

    [Category("Context cache"), DisplayName("Host state slots")]
    public int HostStateSlots { get; set; } = 8;

    [Category("Context cache"), DisplayName("Host KV cache MiB")]
    public int HostKvMiB { get; set; } = 8192;

    [Category("Context cache"), DisplayName("Maximum private continuations")]
    public int MaxPrivateContinuations { get; set; } = -1;

    [Category("Context cache"), DisplayName("Maximum shared prefixes")]
    public int MaxSharedPrefixes { get; set; } = -1;

    [Category("Context cache"), DisplayName("Maximum long anchors")]
    public int MaxLongAnchorsPerContinuation { get; set; } = -1;

    [Category("Context cache"), DisplayName("Maximum cache markers")]
    public int MaxCacheMarkersPerRequest { get; set; } = -1;

    [Category("Response storage"), DisplayName("Maximum response records")]
    public int ResponseStoreMaxRecords { get; set; } = 1024;

    [Category("Response storage"), DisplayName("Maximum response store MiB")]
    public int ResponseStoreMaxMiB { get; set; } = 256;

    [Category("Thinking"), DisplayName("Thinking enabled")]
    public bool ThinkingEnabled { get; set; } = true;

    [Category("Thinking"), DisplayName("Preserve thinking")]
    public bool PreserveThinking { get; set; } = true;

    [Category("Thinking"), DisplayName("Default thinking budget")]
    public int? DefaultThinkingBudget { get; set; }

    [Category("Sampling overrides"), DisplayName("Greedy decoding")]
    public bool Greedy { get; set; }

    [Category("Sampling overrides")] public double? Temperature { get; set; }
    [Category("Sampling overrides"), DisplayName("Top P")] public double? TopP { get; set; }
    [Category("Sampling overrides"), DisplayName("Top K")] public int? TopK { get; set; }
    [Category("Sampling overrides"), DisplayName("Minimum P")] public double? MinP { get; set; }
    [Category("Sampling overrides"), DisplayName("Presence penalty")] public double? PresencePenalty { get; set; }
    [Category("Sampling overrides"), DisplayName("Frequency penalty")] public double? FrequencyPenalty { get; set; }
    [Category("Sampling overrides")] public int? Seed { get; set; }

    [Category("Files and diagnostics"), DisplayName("Context cost presets file")]
    public string ContextCostPresetsFile { get; set; } = string.Empty;

    [Category("Files and diagnostics"), DisplayName("Request log JSONL file")]
    public string RequestLogJsonlFile { get; set; } = string.Empty;

    public static ModelProfile CreateRecommended(ModelCatalogEntry entry) => new()
    {
        VisionEnabled = entry.Vision,
        MaxContext = entry.RecommendedContext,
        DefaultMaxTokens = entry.RecommendedContext,
        CustomKvCapacity = entry.RecommendedContext,
    };
}

internal sealed class SettingsStore
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly AppLogger _logger;

    public SettingsStore(string path, AppLogger logger) { _path = path; _logger = logger; }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions) ?? new AppSettings();
        }
        catch (Exception exception)
        {
            _logger.Write("Settings could not be read; defaults will be used", exception);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var temp = _path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temp, _path, true);
    }
}
