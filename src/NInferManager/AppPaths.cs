namespace NInferManager;

internal sealed class AppPaths
{
    public AppPaths()
    {
        AppDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        IsPortable = File.Exists(Path.Combine(AppDirectory, "portable.mode"));
        var overrideRoot = Environment.GetEnvironmentVariable("NINFER_MANAGER_DATA_ROOT");
        DataDirectory = !string.IsNullOrWhiteSpace(overrideRoot)
            ? Path.GetFullPath(overrideRoot)
            : IsPortable
                ? Path.Combine(AppDirectory, "Data")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NInfer Manager", "Data");
        EngineDirectory = ReadOverride("NINFER_MANAGER_ENGINE_ROOT", Path.Combine(AppDirectory, "Engine"));
        ModelsDirectory = ReadOverride("NINFER_MANAGER_MODELS_ROOT", IsPortable
            ? Path.Combine(AppDirectory, "Models")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NInfer Manager", "Models"));
        WebUiDirectory = Path.Combine(EngineDirectory, "webui");
        SettingsFile = Path.Combine(DataDirectory, "settings.json");
        CatalogCacheFile = Path.Combine(DataDirectory, "catalog.cache.json");
        LogFile = Path.Combine(DataDirectory, "Logs", "manager.log");
        UpdatesDirectory = Path.Combine(DataDirectory, "Updates");
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(ModelsDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
        Directory.CreateDirectory(UpdatesDirectory);
    }

    public string AppDirectory { get; }
    public bool IsPortable { get; }
    public string DataDirectory { get; }
    public string EngineDirectory { get; }
    public string ModelsDirectory { get; }
    public string WebUiDirectory { get; }
    public string SettingsFile { get; }
    public string CatalogCacheFile { get; }
    public string LogFile { get; }
    public string UpdatesDirectory { get; }

    private static string ReadOverride(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return Path.GetFullPath(string.IsNullOrWhiteSpace(value) ? fallback : value);
    }
}
