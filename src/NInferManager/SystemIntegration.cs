using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace NInferManager;

internal static class WorkingSetTrimmer
{
    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr process);

    public static async Task TrimAfterIdleAsync()
    {
        await Task.Delay(2000);
        try
        {
            GC.Collect(2, GCCollectionMode.Optimized, false, true);
            EmptyWorkingSet(Process.GetCurrentProcess().Handle);
        }
        catch { }
    }
}

internal static class StartupIntegration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NInfer Manager";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (enabled) key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --minimized");
        else key.DeleteValue(ValueName, false);
    }
}

internal sealed record GpuInfo(string Name, int MemoryUsedMiB, int MemoryTotalMiB, int TemperatureC, int UtilizationPercent)
{
    public string Summary => $"{Name} — {MemoryUsedMiB:N0}/{MemoryTotalMiB:N0} MiB VRAM — {TemperatureC}°C — {UtilizationPercent}% GPU";

    public static async Task<GpuInfo?> QueryAsync()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "nvidia-smi.exe",
                    Arguments = "--query-gpu=name,memory.used,memory.total,temperature.gpu,utilization.gpu --format=csv,noheader,nounits",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };
            process.Start();
            var line = await process.StandardOutput.ReadLineAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(line)) return null;
            var fields = line.Split(',').Select(x => x.Trim()).ToArray();
            return fields.Length >= 5 && int.TryParse(fields[1], out var used) && int.TryParse(fields[2], out var total) &&
                int.TryParse(fields[3], out var temp) && int.TryParse(fields[4], out var utilization)
                ? new GpuInfo(fields[0], used, total, temp, utilization) : null;
        }
        catch { return null; }
    }
}

internal static class DiagnosticsPackage
{
    public static async Task<string> CreateAsync(AppPaths paths, AppSettings settings, AppLogger logger)
    {
        var output = Path.Combine(paths.DataDirectory, $"NInfer-Manager-Diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        var temp = Path.Combine(paths.DataDirectory, "diagnostics-building");
        if (Directory.Exists(temp)) Directory.Delete(temp, true);
        Directory.CreateDirectory(temp);
        var redacted = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings, SettingsStore.JsonOptions), SettingsStore.JsonOptions) ?? new AppSettings();
        redacted.ApiKey = string.IsNullOrEmpty(redacted.ApiKey) ? string.Empty : "[REDACTED]";
        await File.WriteAllTextAsync(Path.Combine(temp, "settings-redacted.json"), JsonSerializer.Serialize(redacted, SettingsStore.JsonOptions));
        if (File.Exists(logger.FilePath)) File.Copy(logger.FilePath, Path.Combine(temp, "manager.log"), true);
        var gpu = await GpuInfo.QueryAsync();
        var info = new StringBuilder()
            .AppendLine("NInfer Manager diagnostics")
            .AppendLine($"Created: {DateTimeOffset.Now:O}")
            .AppendLine($"Version: {Application.ProductVersion}")
            .AppendLine($"Windows: {Environment.OSVersion}")
            .AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}")
            .AppendLine($"Portable: {paths.IsPortable}")
            .AppendLine($"Engine present: {File.Exists(Path.Combine(paths.EngineDirectory, "ninfer-serve.exe"))}")
            .AppendLine($"GPU: {gpu?.Summary ?? "Unavailable"}");
        await File.WriteAllTextAsync(Path.Combine(temp, "system.txt"), info.ToString());
        ZipFile.CreateFromDirectory(temp, output, CompressionLevel.Fastest, false);
        Directory.Delete(temp, true);
        return output;
    }
}
