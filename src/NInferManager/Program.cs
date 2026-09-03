using System.Threading;

namespace NInferManager;

internal static class Program
{
    private const string MutexName = "Local\\NInferManager-0B8D3D45-3E5D-4AC1-B548-36A065FEF718";

    [STAThread]
    private static void Main(string[] args)
    {
        if (PortableUpdateRunner.TryRun(args)) return;

        using var mutex = new Mutex(true, MutexName, out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("NInfer Manager is already running in the notification area.", "NInfer Manager",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        var paths = new AppPaths();
        using var logger = new AppLogger(paths.LogFile);
        var settingsStore = new SettingsStore(paths.SettingsFile, logger);
        var settings = settingsStore.Load();
        UiTheme.Initialize(settings.Theme);
        using var icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? (Icon)SystemIcons.Application.Clone();
        var portResult = PortManagement.Resolve(settings);
        if (portResult.Port == 0)
        {
            using var conflict = new PortConflictDialog(portResult.RequestedPort, icon);
            if (conflict.ShowDialog() != DialogResult.OK) return;
            settings.PublicPort = conflict.SelectedPort;
            settings.LockPublicPort = true;
            settingsStore.Save(settings);
            portResult = new PortStartupResult(conflict.SelectedPort, false, portResult.RequestedPort);
        }
        if (settings.BackendPort == portResult.Port || !PortManagement.IsAvailable(settings.BackendPort))
            settings.BackendPort = PortManagement.FindAvailable(portResult.Port);
        using var job = new ProcessJob(logger);
        using var catalog = new ModelCatalogService(paths, settings, settingsStore, logger);
        using var downloads = new ModelDownloadService(paths, logger);
        using var updates = new UpdateService(paths, settings, settingsStore, logger);
        var engine = new EngineController(paths, settings, catalog, logger, job);
        var proxy = new ApiProxy(engine, paths, settings, logger, portResult.Port);
        using var form = new MainForm(paths, settings, settingsStore, catalog, downloads, updates, engine, proxy, logger, portResult);
        form.ConfigureStartHidden(settings.StartMinimized || args.Contains("--minimized", StringComparer.OrdinalIgnoreCase));

        try
        {
            proxy.StartAsync().GetAwaiter().GetResult();
            Application.Run(form);
        }
        catch (Exception exception)
        {
            logger.Write("Fatal startup error", exception);
            MessageBox.Show($"NInfer Manager could not start.\n\n{exception.Message}", "NInfer Manager",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            proxy.DisposeAsync().AsTask().GetAwaiter().GetResult();
            engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
