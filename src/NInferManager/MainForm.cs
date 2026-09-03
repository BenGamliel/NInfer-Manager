using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Json;

namespace NInferManager;

internal sealed class MainForm : Form
{
    private readonly AppPaths _paths;
    private readonly AppSettings _settings;
    private readonly SettingsStore _settingsStore;
    private readonly ModelCatalogService _catalog;
    private readonly ModelDownloadService _downloads;
    private readonly UpdateService _updates;
    private readonly EngineController _engine;
    private readonly ApiProxy _proxy;
    private readonly AppLogger _logger;
    private readonly PortStartupResult _portStartup;
    private readonly Icon _icon;
    private readonly NotifyIcon _tray;
    private readonly Panel _pageHost = new() { Dock = DockStyle.Fill };
    private readonly List<Panel> _pages = [];
    private int _selectedPage;
    private readonly List<Button> _navigationButtons = [];
    private readonly Label _headerState = new() { AutoSize = true, TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label _headerEndpoint = new() { AutoSize = true, TextAlign = ContentAlignment.MiddleRight };
    private readonly Button _themeToggle = new ThemedButton { AutoSize = true, MinimumSize = new Size(98, 36) };
    private readonly Panel _updateBanner = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Label _updateBannerText = new() { AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _updateBannerButton = Button("Install update", true, true);
    private readonly ProgressBar _updateProgress = new ThemedProgressBar { Dock = DockStyle.Top, Height = 8, Visible = false };
    private readonly Label _updateProgressText = UiTheme.Role(new Label { AutoSize = true, Visible = false }, ThemeRole.MutedText);
    private readonly Label _stateValue = ValueLabel();
    private readonly Label _modelValue = ValueLabel();
    private readonly Label _apiValue = ValueLabel();
    private readonly Label _profileValue = ValueLabel();
    private readonly Label _gpuValue = ValueLabel();
    private readonly MetricRing _vramRing = new() { TitleText = "VRAM", Dock = DockStyle.Fill };
    private readonly MetricRing _contextRing = new() { TitleText = "Context", Dock = DockStyle.Fill };
    private readonly MetricRing _gpuRing = new() { TitleText = "GPU", Dock = DockStyle.Fill };
    private readonly FlowLayoutPanel _profileChips = new() { Dock = DockStyle.Fill, AutoScroll = false, WrapContents = true };
    private readonly Label _activityEngine = new() { AutoSize = true };
    private readonly Label _activityApi = new() { AutoSize = true };
    private readonly Label _activityLifecycle = new() { AutoSize = true };
    private readonly Label _footerStatus = new() { AutoSize = true };
    private readonly DataGridView _modelsGrid = new();
    private readonly TextBox _modelSearch = new() { Width = 280, PlaceholderText = "Search models" };
    private readonly ComboBox _modelFilter = new ThemedComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ProgressBar _modelProgress = new ThemedProgressBar { Dock = DockStyle.Fill, Height = 12 };
    private readonly Label _modelProgressText = UiTheme.Role(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true }, ThemeRole.MutedText);
    private readonly Button _installButton = Button("Install / Resume");
    private readonly Button _cancelDownloadButton = Button("Pause", false);
    private readonly Button _setActiveButton = Button("Set active");
    private readonly Button _verifyButton = Button("Verify");
    private readonly Button _deleteButton = Button("Delete", danger: true);
    private readonly Button _openModelCardButton = Button("Open model card");
    private readonly Button _refreshCatalogButton = Button("Check for new models");
    private readonly RoundedPanel _modelActionsCard = UiTheme.Card();
    private readonly PropertyGrid _appPropertyGrid = new() { Dock = DockStyle.Fill, PropertySort = PropertySort.Categorized, HelpVisible = true, ToolbarVisible = true };
    private readonly PropertyGrid _profilePropertyGrid = new() { Dock = DockStyle.Fill, PropertySort = PropertySort.Categorized, HelpVisible = true, ToolbarVisible = true };
    private readonly ComboBox _profileModel = new ThemedComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly CheckBox _startWithWindows = new() { Text = "Start with Windows", AutoSize = true };
    private readonly CheckBox _basicVision = new() { Text = "Enable image and video input", AutoSize = true };
    private readonly ThemedNumericField _basicContext = new() { Minimum = 1024, Maximum = 262144, Increment = 1024, ThousandsSeparator = true, Width = 150 };
    private readonly ComboBox _basicKv = new ThemedComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly CheckBox _basicAutoUnload = new() { Text = "Unload the model automatically when idle", AutoSize = true };
    private readonly ThemedNumericField _basicIdle = new() { Minimum = 0.1m, Maximum = 10080, DecimalPlaces = 1, Increment = 0.5m, Width = 150 };
    private readonly ThemedNumericField _basicPublicPort = new() { Minimum = 1, Maximum = 65535, Width = 150 };
    private readonly CheckBox _basicLockPort = new() { Text = "Lock this port", AutoSize = true };
    private readonly TextBox _basicApiKey = new() { Width = 260, UseSystemPasswordChar = true, PlaceholderText = "Optional — local access is open when empty" };
    private readonly CheckBox _basicAutoUpdates = new() { Text = "Automatically check for NInfer Manager updates", AutoSize = true };
    private readonly Button _essentialsTabButton = Button("Essentials");
    private readonly Button _advancedTabButton = Button("Advanced");
    private readonly Panel _settingsSectionHost = new() { Dock = DockStyle.Fill };
    private Panel? _essentialsSection;
    private Panel? _advancedSection;
    private bool _advancedSettingsVisible;
    private readonly TextBox _logBox = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = new Font("Consolas", 9f) };
    private readonly TextBox _testPrompt = new() { Text = "Reply with exactly: NInfer Manager OK", Dock = DockStyle.Fill };
    private readonly TextBox _testOutput = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly Button _loadButton = Button("Load model");
    private readonly Button _unloadButton = Button("Unload model", true, true);
    private readonly Button _restartButton = Button("Restart NInfer");
    private readonly Button _sendTestButton = Button("Send test", true, true);
    private readonly RoundedPanel _noModelCard = UiTheme.Card();
    private readonly ContextMenuStrip _trayMenu = new();
    private readonly System.Windows.Forms.Timer _visibleTimer = new() { Interval = 1000 };
    private CancellationTokenSource? _downloadCancellation;
    private string? _downloadingModelFile;
    private CancellationTokenSource? _updateCancellation;
    private UpdateInfo? _availableUpdate;
    private bool _realExit;
    private int _savedPublicPort;
    private int _savedBackendPort;
    private string _lastProfileSignature = string.Empty;
    private int _gpuRefreshTicks;
    private bool _gpuRefreshRunning;

    private bool _startHidden;

    public MainForm(AppPaths paths, AppSettings settings, SettingsStore settingsStore, ModelCatalogService catalog,
        ModelDownloadService downloads, UpdateService updates, EngineController engine, ApiProxy proxy, AppLogger logger, PortStartupResult portStartup)
    {
        _paths = paths; _settings = settings; _settingsStore = settingsStore; _catalog = catalog;
        _downloads = downloads; _updates = updates; _engine = engine; _proxy = proxy; _logger = logger; _portStartup = portStartup;
        _savedPublicPort = settings.PublicPort; _savedBackendPort = settings.BackendPort;
        _basicKv.DataSource = Enum.GetValues<KvPrecision>();
        _modelFilter.Items.AddRange(["All models", "Installed", "Vision", "Available"]); _modelFilter.SelectedIndex = 0;
        _icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application;
        Text = "NInfer Manager";
        Icon = _icon;
        MinimumSize = new Size(1040, 700);
        Size = new Size(1240, 840);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI Variable Text", 9.25f);

        _pages.Add(BuildDashboard());
        _pages.Add(BuildModelsPage());
        _pages.Add(BuildSettingsPage());
        _pages.Add(BuildLogsPage());
        _pages.Add(BuildAboutPage());
        foreach (var page in _pages) { page.Dock = DockStyle.Fill; page.Visible = false; _pageHost.Controls.Add(page); }
        Controls.Add(BuildShell());
        UiTheme.ApplyWindow(this);
        UiTheme.ApplyTree(this);
        SelectPage(0);

        _trayMenu.Items.Add("Open NInfer Manager", null, (_, _) => RestoreFromTray());
        _trayMenu.Items.Add("Open Web UI", null, (_, _) => OpenUrl(_proxy.WebUiUrl));
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("Load model", null, async (_, _) => await RunEngineActionAsync(() => _engine.EnsureLoadedAsync()));
        _trayMenu.Items.Add("Unload from VRAM", null, async (_, _) => await RunEngineActionAsync(() => _engine.UnloadAsync("notification area")));
        _trayMenu.Items.Add("Restart NInfer", null, async (_, _) => await RunEngineActionAsync(_engine.RestartAsync));
        var idleMenu = new ToolStripMenuItem("Automatic VRAM unload");
        idleMenu.DropDownItems.Add("Off", null, (_, _) => SetIdleFromTray(false, 3));
        idleMenu.DropDownItems.Add("After 3 minutes", null, (_, _) => SetIdleFromTray(true, 3));
        idleMenu.DropDownItems.Add("After 10 minutes", null, (_, _) => SetIdleFromTray(true, 10));
        idleMenu.DropDownItems.Add("After 30 minutes", null, (_, _) => SetIdleFromTray(true, 30));
        _trayMenu.Items.Add(idleMenu);
        _trayMenu.Items.Add("Check for updates", null, async (_, _) => await CheckForUpdatesAsync(true));
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("Exit", null, (_, _) => ExitCompletely());
        UiTheme.StyleMenu(_trayMenu);
        _tray = new NotifyIcon { Icon = _icon, Text = "NInfer Manager - Unloaded", Visible = true, ContextMenuStrip = _trayMenu };
        _tray.DoubleClick += (_, _) => RestoreFromTray();

        FormClosing += OnFormClosing;
        Shown += OnShown;
        VisibleChanged += (_, _) => { _visibleTimer.Enabled = Visible; if (Visible) RefreshUi(); };
        _visibleTimer.Tick += async (_, _) =>
        {
            RefreshUi();
            if (++_gpuRefreshTicks % 5 != 0 || _gpuRefreshRunning) return;
            _gpuRefreshRunning = true;
            try { await RefreshGpuAsync(); }
            finally { _gpuRefreshRunning = false; }
        };
        _engine.StateChanged += state => PostToUi(() =>
        {
            RefreshUi();
            if (state == EngineState.Unloaded && !Visible) _ = WorkingSetTrimmer.TrimAfterIdleAsync();
        });
        _catalog.CatalogChanged += () => PostToUi(RefreshModels);
        _logger.LineWritten += _ => { if (Visible && _selectedPage == 3) PostToUi(RefreshLogs); };
        _appPropertyGrid.SelectedObject = _settings;
        PopulateProfileSelector();
        _startWithWindows.Checked = StartupIntegration.IsEnabled();
        RefreshModels();
        RefreshUi();
    }

    public void HideToTray() { ShowInTaskbar = false; Hide(); _ = WorkingSetTrimmer.TrimAfterIdleAsync(); }
    internal void ConfigureStartHidden(bool enabled) => _startHidden = enabled;

    private Control BuildShell()
    {
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = UiTheme.Background };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 228));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var sidebar = UiTheme.Role(new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 22, 16, 16), RowCount = 3 }, ThemeRole.Sidebar);
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var brand = UiTheme.Role(new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, Margin = new Padding(4, 0, 0, 28) }, ThemeRole.Sidebar);
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46)); brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        brand.Controls.Add(new PictureBox { Image = _icon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(34, 34), Margin = new Padding(0, 1, 10, 0) }, 0, 0);
        brand.Controls.Add(UiTheme.Role(new Label { Text = "NInfer\nManager", AutoSize = true, Font = new Font("Segoe UI Variable Display Semibold", 12f), Margin = new Padding(0) }, ThemeRole.SidebarText), 1, 0);
        sidebar.Controls.Add(brand, 0, 0);
        var navigation = UiTheme.Role(new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = false }, ThemeRole.Sidebar);
        string[] names = ["Dashboard", "Models", "Settings", "Logs", "About"];
        string[] icons = ["⌂", "◇", "⚙", "▤", "ⓘ"];
        for (var index = 0; index < names.Length; index++)
        {
            var pageIndex = index;
            var button = new ThemedButton
            {
                Text = $"{icons[index]}     {names[index]}",
                Width = 196,
                AccessibleName = $"Open {names[index]} page",
                Margin = new Padding(0, 0, 0, 7),
            };
            UiTheme.StyleButton(button, ButtonKind.Navigation);
            button.Click += (_, _) => SelectPage(pageIndex);
            _navigationButtons.Add(button); navigation.Controls.Add(button);
        }
        sidebar.Controls.Add(navigation, 0, 1);
        sidebar.Controls.Add(UiTheme.Role(new Label { Text = $"Version {UpdateService.CurrentVersion}\n{(_paths.IsPortable ? "Portable" : "Installed")}", AutoSize = true, Padding = new Padding(12, 8, 0, 0) }, ThemeRole.MutedText), 0, 2);
        shell.Controls.Add(sidebar, 0, 0);

        var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = UiTheme.Background, RowCount = 3, ColumnCount = 1 };
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        workspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(26, 13, 26, 9), BackColor = UiTheme.Background };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(new Label { Text = "Local AI control center", AutoSize = true, Font = new Font("Segoe UI Variable Display Semibold", 13f), Padding = new Padding(0, 7, 0, 0) }, 0, 0);
        var headerActions = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
        var statusCard = UiTheme.Card(ThemeRole.SuccessSoft); statusCard.Size = new Size(102, 36); statusCard.Radius = 16; statusCard.Padding = new Padding(1); statusCard.Margin = new Padding(0, 1, 12, 0);
        _headerState.AutoSize = false; _headerState.Dock = DockStyle.Fill; _headerState.Text = "●  API Online"; _headerState.Font = new Font("Segoe UI Variable Text Semibold", 9f); UiTheme.Role(_headerState, ThemeRole.SuccessText); statusCard.Controls.Add(_headerState);
        _headerEndpoint.Padding = new Padding(0, 9, 8, 0); UiTheme.Role(_headerEndpoint, ThemeRole.MutedText);
        UiTheme.StyleButton(_themeToggle, ButtonKind.Ghost); _themeToggle.Height = 36; _themeToggle.Click += (_, _) => ToggleTheme();
        headerActions.Controls.Add(statusCard); headerActions.Controls.Add(_headerEndpoint); headerActions.Controls.Add(_themeToggle);
        header.Controls.Add(headerActions, 1, 0);
        workspace.Controls.Add(header, 0, 0);

        UiTheme.Role(_updateBanner, ThemeRole.AccentSoft);
        _updateBanner.Padding = new Padding(22, 8, 18, 8);
        var updateLayout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        updateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); updateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _updateBannerText.ForeColor = UiTheme.Text; _updateBannerText.Padding = new Padding(0, 9, 0, 0);
        _updateBannerButton.Click += async (_, _) => await InstallAvailableUpdateAsync();
        updateLayout.Controls.Add(_updateBannerText, 0, 0); updateLayout.Controls.Add(_updateBannerButton, 1, 0); _updateBanner.Controls.Add(updateLayout);
        workspace.Controls.Add(_updateBanner, 0, 1);
        workspace.Controls.Add(_pageHost, 0, 2);
        shell.Controls.Add(workspace, 1, 0);
        SelectPage(0);
        return shell;
    }

    private void SelectPage(int index)
    {
        _selectedPage = Math.Clamp(index, 0, _pages.Count - 1);
        for (var pageIndex = 0; pageIndex < _pages.Count; pageIndex++) _pages[pageIndex].Visible = pageIndex == _selectedPage;
        _pages[_selectedPage].BringToFront();
        for (var i = 0; i < _navigationButtons.Count; i++)
        {
            var selected = i == index;
            _navigationButtons[i].BackColor = selected ? UiTheme.AccentSoft : UiTheme.Sidebar;
            _navigationButtons[i].ForeColor = selected ? UiTheme.Text : UiTheme.SidebarText;
            _navigationButtons[i].Font = new Font("Segoe UI Variable Text", 9.25f, selected ? FontStyle.Bold : FontStyle.Regular);
        }
        if (_selectedPage == 3) RefreshLogs();
    }

    private Panel BuildDashboard()
    {
        var page = Page("Dashboard");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(26, 18, 26, 18), ColumnCount = 1, RowCount = 7, BackColor = UiTheme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 238));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(PageTitle("Dashboard", "Your local AI engine at a glance."), 0, 0);
        _noModelCard.Dock = DockStyle.Top; _noModelCard.Height = 104; _noModelCard.ThemeRole = ThemeRole.AccentSoft; _noModelCard.RefreshTheme();
        var empty = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8) }; empty.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); empty.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var emptyCopy = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        emptyCopy.Controls.Add(new Label { Text = "Choose your first model", AutoSize = true, Font = new Font("Segoe UI Variable Display Semibold", 14f) }, 0, 0);
        emptyCopy.Controls.Add(UiTheme.Role(new Label { Text = "Install a verified NInfer model to begin. Nothing is bundled with the app.", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, ThemeRole.MutedText), 0, 1);
        var choose = Button("Choose a model", true, true); choose.Click += (_, _) => SelectPage(1); empty.Controls.Add(emptyCopy, 0, 0); empty.Controls.Add(choose, 1, 0); _noModelCard.Controls.Add(empty); root.Controls.Add(_noModelCard, 0, 1);

        var summary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(0, 4, 0, 0) };
        summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28)); summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24)); summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24)); summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        var modelCard = UiTheme.Card(); modelCard.Dock = DockStyle.Fill;
        var modelLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, Padding = new Padding(4) };
        modelLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); modelLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); modelLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); modelLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); modelLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); modelLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        modelLayout.Controls.Add(UiTheme.Role(new Label { Text = "Active model", AutoSize = true, Font = new Font("Segoe UI Variable Text Semibold", 9.5f) }, ThemeRole.MutedText), 0, 0);
        _modelValue.Font = new Font("Segoe UI Variable Display Semibold", 15f); _modelValue.MaximumSize = new Size(245, 0); _modelValue.Margin = new Padding(0, 8, 0, 8); modelLayout.Controls.Add(_modelValue, 0, 1);
        _stateValue.Font = new Font("Segoe UI Variable Text Semibold", 9f); UiTheme.Role(_stateValue, ThemeRole.SuccessSoft); modelLayout.Controls.Add(_stateValue, 0, 2);
        _loadButton.Dock = DockStyle.Top; UiTheme.StyleButton(_loadButton, ButtonKind.Primary); _loadButton.Click += async (_, _) => await RunEngineActionAsync(() => _engine.EnsureLoadedAsync()); modelLayout.Controls.Add(_loadButton, 0, 4);
        _unloadButton.Dock = DockStyle.Top; _unloadButton.Click += async (_, _) => await RunEngineActionAsync(() => _engine.UnloadAsync("dashboard")); modelLayout.Controls.Add(_unloadButton, 0, 4);
        _restartButton.Dock = DockStyle.Top; _restartButton.Margin = new Padding(0, 7, 0, 0); _restartButton.Click += async (_, _) => await RunEngineActionAsync(_engine.RestartAsync); modelLayout.Controls.Add(_restartButton, 0, 5);
        modelCard.Controls.Add(modelLayout); summary.Controls.Add(modelCard, 0, 0);
        summary.Controls.Add(MetricCard(_vramRing), 1, 0); summary.Controls.Add(MetricCard(_contextRing), 2, 0); summary.Controls.Add(MetricCard(_gpuRing), 3, 0);
        root.Controls.Add(summary, 0, 2);

        var details = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52)); details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        var (profileCard, profileBody) = SectionCard("Active profile", "Profile settings currently used when the model loads.");
        _profileChips.Padding = new Padding(0, 8, 0, 0); profileBody.Controls.Add(_profileChips); details.Controls.Add(profileCard, 0, 0);
        var (quickCard, quickBody) = SectionCard("Quick actions", "Common tools without opening another page.");
        var quick = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(0, 8, 0, 0) };
        quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f)); quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f)); quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        var web = ActionTile("◎\nOpen Web UI"); web.Click += (_, _) => OpenUrl(_proxy.WebUiUrl);
        var copy = ActionTile("▣\nCopy API address"); copy.Click += (_, _) => Clipboard.SetText(_proxy.ApiBaseUrl);
        _sendTestButton.Text = "⚗\nRun API test"; UiTheme.StyleButton(_sendTestButton, ButtonKind.ActionTile);
        quick.Controls.Add(web, 0, 0); quick.Controls.Add(copy, 1, 0); quick.Controls.Add(_sendTestButton, 2, 0); quickBody.Controls.Add(quick); details.Controls.Add(quickCard, 1, 0);
        root.Controls.Add(details, 0, 3);

        var (activityCard, activityBody) = SectionCard("Recent activity", "Live service summary. Detailed output remains available in Logs.");
        var activity = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(2, 7, 2, 0) };
        activity.Controls.Add(_activityEngine, 0, 0); activity.Controls.Add(_activityApi, 0, 1); activity.Controls.Add(_activityLifecycle, 0, 2); activityBody.Controls.Add(activity); root.Controls.Add(activityCard, 0, 4);

        var (testCard, testBody) = SectionCard("API test", "Send a small OpenAI-compatible request and inspect the response."); testCard.Visible = false; testCard.Height = 176;
        var testLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(0, 8, 0, 0) };
        testLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); testLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); testLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); testLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        testLayout.Controls.Add(_testPrompt, 0, 0); var send = Button("Send", true, true); send.Click += async (_, _) => await SendApiTestAsync(send); testLayout.Controls.Add(send, 1, 0); testLayout.Controls.Add(_testOutput, 0, 1); testLayout.SetColumnSpan(_testOutput, 2); testBody.Controls.Add(testLayout); root.Controls.Add(testCard, 0, 5);
        _sendTestButton.Click += async (_, _) => { testCard.Visible = true; await SendApiTestAsync(_sendTestButton); };

        UiTheme.Role(_footerStatus, ThemeRole.MutedText); _footerStatus.Padding = new Padding(4, 5, 0, 2); root.Controls.Add(_footerStatus, 0, 6);
        page.Controls.Add(root); return page;
    }

    private Panel BuildModelsPage()
    {
        var page = Page("Models");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26, 18, 26, 22), RowCount = 6, ColumnCount = 1, BackColor = UiTheme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(PageTitle("Model Manager", "Download, verify and activate official NInfer model artifacts."), 0, 0);
        var filterCard = UiTheme.Card(ThemeRole.SurfaceAlt); filterCard.Dock = DockStyle.Top; filterCard.Height = 62; filterCard.Padding = new Padding(14, 8, 14, 8);
        var filters = Flow();
        _modelSearch.TextChanged += (_, _) => RefreshModels(); _modelFilter.SelectedIndexChanged += (_, _) => RefreshModels();
        _refreshCatalogButton.Click += async (_, _) => await RefreshCatalogAsync();
        filters.Controls.Add(_modelSearch); filters.Controls.Add(_modelFilter); filters.Controls.Add(_refreshCatalogButton); filterCard.Controls.Add(filters); root.Controls.Add(filterCard, 0, 1);
        _modelsGrid.Dock = DockStyle.Fill; _modelsGrid.ReadOnly = true; _modelsGrid.AllowUserToAddRows = false; _modelsGrid.AllowUserToDeleteRows = false;
        _modelsGrid.AutoGenerateColumns = false; _modelsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _modelsGrid.MultiSelect = false;
        _modelsGrid.RowHeadersVisible = false; _modelsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        UiTheme.StyleGrid(_modelsGrid);
        _modelsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "Model", FillWeight = 180 });
        _modelsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Weights", HeaderText = "Weights", FillWeight = 80 });
        _modelsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Size", HeaderText = "Size", FillWeight = 70 });
        _modelsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Vision", HeaderText = "Vision", FillWeight = 55 });
        _modelsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "Status", FillWeight = 105 });
        _modelsGrid.SelectionChanged += (_, _) => RefreshModelButtons();
        var gridCard = UiTheme.Card(); gridCard.Dock = DockStyle.Fill; gridCard.Padding = new Padding(1); gridCard.Controls.Add(_modelsGrid); root.Controls.Add(gridCard, 0, 2);
        _modelActionsCard.Dock = DockStyle.Top; _modelActionsCard.Height = 72; _modelActionsCard.Padding = new Padding(12, 9, 12, 9);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 2, 0, 0) };
        _installButton.Click += async (_, _) => await StartDownloadAsync(); actions.Controls.Add(_installButton);
        _cancelDownloadButton.Click += (_, _) => _downloadCancellation?.Cancel(); actions.Controls.Add(_cancelDownloadButton);
        _setActiveButton.Click += async (_, _) => await RunButtonActionAsync(_setActiveButton, SetSelectedActiveAsync); actions.Controls.Add(_setActiveButton);
        _verifyButton.Click += async (_, _) => await RunButtonActionAsync(_verifyButton, VerifySelectedAsync); actions.Controls.Add(_verifyButton);
        _deleteButton.Click += async (_, _) => await RunButtonActionAsync(_deleteButton, DeleteSelectedAsync); actions.Controls.Add(_deleteButton);
        _openModelCardButton.Click += (_, _) => { var entry = SelectedEntry(); if (entry is not null) OpenUrl(entry.ModelCardUrl); }; actions.Controls.Add(_openModelCardButton);
        _modelActionsCard.Controls.Add(actions); root.Controls.Add(_modelActionsCard, 0, 3);
        var progress = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        progress.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45)); progress.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        progress.Controls.Add(_modelProgress, 0, 0); progress.Controls.Add(_modelProgressText, 1, 0); root.Controls.Add(progress, 0, 4);
        root.Controls.Add(UiTheme.Role(new Label { Text = "Downloads can be paused and resumed. Models are activated only after size and SHA-256 verification.", AutoSize = true, Padding = new Padding(4, 5, 0, 0) }, ThemeRole.MutedText), 0, 5);
        page.Controls.Add(root); return page;
    }

    private Panel BuildSettingsPage()
    {
        var page = Page("Settings");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26, 18, 26, 22), RowCount = 4, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(PageTitle("Settings", "Start with the essentials. Every NInfer option remains available under Advanced."), 0, 0);
        var topCard = UiTheme.Card(ThemeRole.SurfaceAlt); topCard.Dock = DockStyle.Top; topCard.Height = 62; topCard.Padding = new Padding(14, 7, 14, 7);
        var top = Flow(); top.Controls.Add(new Label { Text = "Model profile", AutoSize = true, Font = new Font("Segoe UI Variable Text Semibold", 9.25f), Padding = new Padding(0, 8, 5, 0) });
        _profileModel.SelectedIndexChanged += (_, _) => SelectProfile(); top.Controls.Add(_profileModel); top.Controls.Add(_startWithWindows);
        topCard.Controls.Add(top); root.Controls.Add(topCard, 0, 1);

        var sections = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        sections.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); sections.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var selector = UiTheme.Card(ThemeRole.SurfaceAlt); selector.Dock = DockStyle.Fill; selector.Padding = new Padding(10, 6, 10, 6); selector.Margin = new Padding(0, 0, 0, 8);
        var selectorFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _essentialsTabButton.Width = 130; _advancedTabButton.Width = 130;
        _essentialsTabButton.Click += (_, _) => ShowSettingsSection(false); _advancedTabButton.Click += (_, _) => ShowSettingsSection(true);
        selectorFlow.Controls.Add(_essentialsTabButton); selectorFlow.Controls.Add(_advancedTabButton); selector.Controls.Add(selectorFlow); sections.Controls.Add(selector, 0, 0);
        _essentialsSection = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Background, Padding = new Padding(0) };
        var essentialsCard = UiTheme.Card(); essentialsCard.Dock = DockStyle.Fill;
        var essentialsScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Padding = new Padding(4) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddSetting(fields, "Vision and video", _basicVision, "Accept images and video frames with compatible models.");
        AddSetting(fields, "Maximum context", _basicContext, "Larger values use more KV cache memory.");
        AddSetting(fields, "KV cache precision", _basicKv, "INT8 is the recommended balance for the default 150K profile.");
        AddSetting(fields, "Automatic VRAM unload", _basicAutoUnload, "Keeps the API online while releasing model VRAM.");
        AddSetting(fields, "Idle time (minutes)", _basicIdle, "The timer resets after every inference request.");
        AddSetting(fields, "Public API port", _basicPublicPort, "Loopback-only address used by applications and the Web UI.");
        AddSetting(fields, "Port behavior", _basicLockPort, "Automatic can move to a free port. Locked stops and asks when the selected port is busy.");
        var portActions = Flow();
        AddAction(portActions, "Use for this session", async () => await RestartApiOnPortAsync(false));
        AddAction(portActions, "Save and restart API", async () => await RestartApiOnPortAsync(true), true);
        fields.Controls.Add(portActions, 1, fields.RowCount); fields.SetColumnSpan(portActions, 2); fields.RowCount++;
        AddSetting(fields, "API key", _basicApiKey, "Optional bearer token for clients connecting to the local API.");
        AddSetting(fields, "Application updates", _basicAutoUpdates, "Checks GitHub Releases in the background without downloading anything.");
        var updateActions = Flow();
        AddAction(updateActions, "Check for updates", async () => await CheckForUpdatesAsync(true), true);
        updateActions.Controls.Add(_updateProgressText); fields.Controls.Add(updateActions, 1, fields.RowCount); fields.SetColumnSpan(updateActions, 2); fields.RowCount++;
        fields.Controls.Add(_updateProgress, 0, fields.RowCount); fields.SetColumnSpan(_updateProgress, 3); fields.RowCount++;
        essentialsScroll.Controls.Add(fields); essentialsCard.Controls.Add(essentialsScroll); _essentialsSection.Controls.Add(essentialsCard);

        _advancedSection = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Background, Padding = new Padding(0) };
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 360, BackColor = UiTheme.Background };
        var appGroup = UiTheme.Card(); appGroup.Dock = DockStyle.Fill; appGroup.Padding = new Padding(8); appGroup.Controls.Add(_appPropertyGrid);
        var modelGroup = UiTheme.Card(); modelGroup.Dock = DockStyle.Fill; modelGroup.Padding = new Padding(8); modelGroup.Controls.Add(_profilePropertyGrid);
        split.Panel1.Controls.Add(appGroup); split.Panel2.Controls.Add(modelGroup); _advancedSection.Controls.Add(split);
        _settingsSectionHost.Controls.Add(_advancedSection); _settingsSectionHost.Controls.Add(_essentialsSection); sections.Controls.Add(_settingsSectionHost, 0, 1); root.Controls.Add(sections, 0, 2);
        ShowSettingsSection(false);
        var actions = Flow();
        AddAction(actions, "Save settings", SaveSettingsAsync, true);
        AddAction(actions, "Restore recommended model defaults", RestoreProfileAsync);
        AddAction(actions, "Copy generated command", () => { Clipboard.SetText(_engine.BuildCommandPreview()); return Task.CompletedTask; });
        AddAction(actions, "Open Setup Wizard", ShowSetupWizardAsync);
        root.Controls.Add(actions, 0, 3); page.Controls.Add(root); return page;
    }

    private Panel BuildLogsPage()
    {
        var page = Page("Logs");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26, 18, 26, 22), RowCount = 3, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(PageTitle("Logs and diagnostics", "Inspect recent activity or create a redacted support package."), 0, 0);
        var logCard = UiTheme.Card(); logCard.Dock = DockStyle.Fill; logCard.Padding = new Padding(10); logCard.Controls.Add(_logBox); root.Controls.Add(logCard, 0, 1);
        var actions = Flow(); AddAction(actions, "Refresh", () => { RefreshLogs(); return Task.CompletedTask; });
        AddAction(actions, "Open log file", () => { Process.Start(new ProcessStartInfo(_logger.FilePath) { UseShellExecute = true }); return Task.CompletedTask; });
        AddAction(actions, "Create diagnostics package", CreateDiagnosticsAsync); root.Controls.Add(actions, 0, 2);
        page.Controls.Add(root); return page;
    }

    private Panel BuildAboutPage()
    {
        var page = Page("About");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26, 18, 26, 22), RowCount = 3, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(PageTitle("About NInfer Manager", "Version, update and local storage information."), 0, 0);
        var card = UiTheme.Card(); card.Dock = DockStyle.Fill;
        var text = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            ForeColor = UiTheme.Text,
            Font = new Font("Segoe UI Variable Text", 10f),
            Text = $"NInfer Manager {UpdateService.CurrentVersion}\nUnofficial lightweight GUI, updater and model manager for NInfer.\n\n" +
                   "This independent community application is not affiliated with or endorsed by the NInfer project. Third-party components retain their own licenses.\n\n" +
                   $"Mode: {(_paths.IsPortable ? "Portable — updates preserve Data and Models" : "Installed — updates use the Windows installer")}\n" +
                   $"Engine: {_paths.EngineDirectory}\nModels: {_paths.ModelsDirectory}\nData: {_paths.DataDirectory}\n\n" +
                   "Update packages must come from the official BenGamliel/NInfer-Manager GitHub Releases page and pass the SHA-256 digest published by GitHub."
        };
        card.Controls.Add(text); root.Controls.Add(card, 0, 1);
        var actions = Flow(); AddAction(actions, "Check for updates", async () => await CheckForUpdatesAsync(true), true);
        AddAction(actions, "Open project page", () => { OpenUrl("https://github.com/BenGamliel/NInfer-Manager"); return Task.CompletedTask; });
        root.Controls.Add(actions, 0, 2); page.Controls.Add(root); return page;
    }

    private async void OnShown(object? sender, EventArgs e)
    {
        if (_startHidden) BeginInvoke(HideToTray);
        if (_portStartup.ChangedAutomatically)
            MessageBox.Show(this, $"Port {_portStartup.RequestedPort} was already in use.\n\nNInfer Manager selected the free local port {_proxy.Port} for this session. You can change or lock the port in Settings.", "API port changed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        if (!_settings.FirstRunCompleted && !_startHidden) await ShowSetupWizardAsync();
        await RefreshGpuAsync();
        if (_catalog.ShouldCheckAutomatically())
        {
            try { await _catalog.RefreshOnlineAsync(); }
            catch (Exception exception) { _logger.Write("Automatic catalog check failed; cached catalog remains available", exception); }
        }
        if (_updates.ShouldCheckAutomatically()) await CheckForUpdatesAsync(false);
    }

    private void RefreshUi()
    {
        var active = _engine.ActiveEntryOrNull;
        var loaded = _engine.IsLoaded;
        _stateValue.Text = loaded ? "●  Ready" : _engine.State == EngineState.Loading ? "●  Loading" : "○  Unloaded";
        UiTheme.Role(_stateValue, loaded ? ThemeRole.SuccessSoft : ThemeRole.WarningSoft);
        _modelValue.Text = active?.DisplayName ?? "No model selected";
        _apiValue.Text = _proxy.ApiBaseUrl;
        _profileValue.Text = active is null ? "Install and activate a model to configure its profile" : $"{_engine.ActiveProfile.MaxContext:N0} context | {_engine.ActiveProfile.KvPrecision} KV | Vision {(_engine.ActiveProfile.VisionEnabled ? "ON" : "OFF")} | Auto-unload {(_settings.AutoUnloadEnabled ? _settings.IdleMinutes + " min" : "OFF")}";
        _headerState.Text = "●  API Online"; _headerEndpoint.Text = _proxy.ApiBaseUrl.Replace("http://", string.Empty).Replace("/v1", string.Empty);
        _themeToggle.Text = UiTheme.Dark ? "☀  Light" : "☾  Dark";
        _noModelCard.Visible = active is null;
        _loadButton.Visible = active is not null && !loaded; _loadButton.Enabled = active is not null;
        _unloadButton.Visible = active is not null && loaded; _unloadButton.Enabled = loaded;
        _restartButton.Enabled = active is not null; _sendTestButton.Enabled = active is not null;
        var profile = active is null ? null : _engine.ActiveProfile;
        _contextRing.Percentage = profile is null ? 0 : (int)Math.Round(Math.Clamp(profile.MaxContext / 262144d, 0, 1) * 100);
        _contextRing.ValueText = profile is null ? "—" : profile.MaxContext >= 1000 ? $"{profile.MaxContext / 1000d:0.#}K" : profile.MaxContext.ToString("N0");
        _contextRing.DetailText = profile is null ? "No active profile" : "configured capacity";
        var signature = active is null ? string.Empty : $"{active.FileName}|{profile!.KvPrecision}|{profile.MaxContext}|{profile.VisionEnabled}|{profile.SpeculativeMode}|{profile.DraftTokens}|{profile.ThinkingEnabled}|{UiTheme.Preference}";
        if (signature != _lastProfileSignature)
        {
            _profileChips.Controls.Clear();
            if (active is null) _profileChips.Controls.Add(Chip("Choose a model to create a profile"));
            else
            {
                _profileChips.Controls.Add(Chip(active.Weights.ToUpperInvariant()));
                _profileChips.Controls.Add(Chip($"{profile!.KvPrecision.ToString().ToUpperInvariant()} KV"));
                _profileChips.Controls.Add(Chip($"{profile.MaxContext / 1000d:0.#}K Context"));
                _profileChips.Controls.Add(Chip($"Vision {(profile.VisionEnabled ? "ON" : "OFF")}"));
                _profileChips.Controls.Add(Chip(profile.SpeculativeMode == SpeculativeMode.Disabled ? "Speculation OFF" : $"{profile.SpeculativeMode.ToString().ToUpperInvariant()}{profile.DraftTokens}"));
                _profileChips.Controls.Add(Chip($"Thinking {(profile.ThinkingEnabled ? "ON" : "OFF")}"));
            }
            _lastProfileSignature = signature;
        }
        _activityEngine.Text = loaded ? $"●   {active?.DisplayName} is loaded and ready." : $"○   Engine is {_engine.State.ToString().ToLowerInvariant()}.";
        _activityEngine.ForeColor = loaded ? UiTheme.Success : UiTheme.Muted;
        _activityApi.Text = $"●   OpenAI-compatible API is listening on {_proxy.ApiBaseUrl}."; _activityApi.ForeColor = UiTheme.Teal;
        _activityLifecycle.Text = _settings.AutoUnloadEnabled ? $"○   Automatic VRAM release is set to {_settings.IdleMinutes:0.#} minute(s)." : "○   Automatic VRAM release is disabled."; _activityLifecycle.ForeColor = UiTheme.Muted;
        _footerStatus.Text = _settings.AutoUnloadEnabled ? $"◷  Auto-unload after {_settings.IdleMinutes:0.#} idle minute(s)" : "◷  Auto-unload is disabled";
        _tray.Text = ("NInfer Manager - " + _engine.State)[..Math.Min(63, ("NInfer Manager - " + _engine.State).Length)];
    }

    private async Task CheckForUpdatesAsync(bool interactive)
    {
        try
        {
            _updateProgressText.Visible = interactive;
            _updateProgressText.Text = "Checking GitHub Releases...";
            var update = await _updates.CheckAsync();
            if (update is null)
            {
                _availableUpdate = null; _updateBanner.Visible = false;
                _updateProgressText.Text = $"You are up to date — version {UpdateService.CurrentVersion}.";
                if (interactive) MessageBox.Show($"NInfer Manager {UpdateService.CurrentVersion} is the latest version.", "Check for updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _availableUpdate = update;
            _updateBannerText.Text = $"NInfer Manager {update.Version} is available. Your current version is {UpdateService.CurrentVersion}.";
            _updateBanner.Visible = true;
            _updateProgressText.Text = $"Version {update.Version} is ready to download.";
            _tray.ShowBalloonTip(6000, "NInfer Manager update available", $"Version {update.Version} is available. Open NInfer Manager to install it.", ToolTipIcon.Info);
            if (interactive && MessageBox.Show($"NInfer Manager {update.Version} is available.\n\nDownload and install it now?", "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                await InstallAvailableUpdateAsync();
        }
        catch (Exception exception)
        {
            _logger.Write("Application update check failed", exception);
            _updateProgressText.Visible = interactive; _updateProgressText.Text = "Update check failed — the application can continue normally.";
            if (interactive) MessageBox.Show("The update service could not be reached.\n\n" + exception.Message, "Check for updates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task InstallAvailableUpdateAsync()
    {
        if (_availableUpdate is null) { await CheckForUpdatesAsync(true); return; }
        _updateCancellation?.Dispose(); _updateCancellation = new CancellationTokenSource();
        _updateBannerButton.Enabled = false; _updateProgress.Visible = true; _updateProgressText.Visible = true;
        var progress = new Progress<UpdateProgress>(value => { _updateProgress.Value = value.Percent; _updateProgressText.Text = value.Description; });
        try
        {
            var package = await _updates.DownloadAsync(_availableUpdate, progress, _updateCancellation.Token);
            if (MessageBox.Show($"Version {_availableUpdate.Version} was downloaded and verified.\n\nNInfer Manager will close and install the update now.", "Install update", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
            await _engine.UnloadAsync("application update");
            _updates.LaunchInstaller(package);
            ExitCompletely();
        }
        catch (OperationCanceledException) { _updateProgressText.Text = "Update download cancelled."; }
        catch (Exception exception)
        {
            _logger.Write("Application update failed", exception);
            MessageBox.Show(exception.Message, "Update failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { if (!IsDisposed) _updateBannerButton.Enabled = true; }
    }

    private void SetIdleFromTray(bool enabled, decimal minutes)
    {
        _settings.AutoUnloadEnabled = enabled; _settings.IdleMinutes = minutes; _settingsStore.Save(_settings); _proxy.ApplyLifecycleSettings(); RefreshUi();
        _tray.ShowBalloonTip(3000, "Automatic VRAM unload", enabled ? $"The model will unload after {minutes} idle minutes." : "Automatic unloading is disabled.", ToolTipIcon.Info);
    }

    private async Task RefreshGpuAsync()
    {
        _gpuValue.Text = "Checking...";
        var gpu = await GpuInfo.QueryAsync();
        if (IsDisposed) return;
        _gpuValue.Text = gpu?.Summary ?? "NVIDIA GPU information unavailable";
        _vramRing.Percentage = gpu is null || gpu.MemoryTotalMiB <= 0 ? 0 : (int)Math.Round(gpu.MemoryUsedMiB * 100d / gpu.MemoryTotalMiB);
        _vramRing.ValueText = gpu is null ? "—" : $"{_vramRing.Percentage}%";
        _vramRing.DetailText = gpu is null ? "Unavailable" : $"{gpu.MemoryUsedMiB / 1024d:0.0} / {gpu.MemoryTotalMiB / 1024d:0.#} GB";
        _gpuRing.Percentage = gpu?.UtilizationPercent ?? 0;
        _gpuRing.ValueText = gpu is null ? "—" : $"{gpu.UtilizationPercent}%";
        _gpuRing.DetailText = gpu is null ? "Unavailable" : $"{gpu.TemperatureC}°C";
    }

    private void RefreshModels()
    {
        var selected = SelectedEntry()?.FileName;
        var query = _modelSearch.Text.Trim();
        var filter = _modelFilter.SelectedItem?.ToString() ?? "All models";
        var rows = _catalog.Entries
            .Where(e => query.Length == 0 || e.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) || e.Weights.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Where(e => filter switch { "Installed" => _downloads.IsInstalled(e), "Vision" => e.Vision, "Available" => !_downloads.IsInstalled(e), _ => true })
            .Select(e => new ModelRow(e, GetModelStatus(e))).ToList();
        _modelsGrid.DataSource = new BindingList<ModelRow>(rows);
        if (selected is not null)
            foreach (DataGridViewRow row in _modelsGrid.Rows) if ((row.DataBoundItem as ModelRow)?.Entry.FileName == selected) { row.Selected = true; break; }
        PopulateProfileSelector(); RefreshModelButtons();
    }

    private string GetModelStatus(ModelCatalogEntry entry)
    {
        if (_settings.ActiveModelFile.Equals(entry.FileName, StringComparison.OrdinalIgnoreCase))
            return _downloads.IsInstalled(entry) ? "Active / installed" : "Active / missing";
        if (_downloads.IsInstalled(entry)) return "Installed";
        if (File.Exists(_downloads.GetModelPath(entry) + ".part")) return "Paused / partial";
        return entry.DiscoveredOnline ? "Available / new" : "Available";
    }

    private ModelCatalogEntry? SelectedEntry() => (_modelsGrid.CurrentRow?.DataBoundItem as ModelRow)?.Entry;
    private void RefreshModelButtons()
    {
        var entry = SelectedEntry();
        _modelActionsCard.Visible = entry is not null;
        if (entry is null) return;
        var installed = _downloads.IsInstalled(entry);
        var active = _settings.ActiveModelFile.Equals(entry.FileName, StringComparison.OrdinalIgnoreCase);
        var downloading = _downloadCancellation is not null && string.Equals(_downloadingModelFile, entry.FileName, StringComparison.OrdinalIgnoreCase);
        var partial = File.Exists(_downloads.GetModelPath(entry) + ".part");

        _installButton.Text = partial ? "Resume download" : "Install model";
        _installButton.AccessibleName = _installButton.Text;
        _installButton.Visible = !installed && !downloading;
        _installButton.Enabled = _downloadCancellation is null;
        _cancelDownloadButton.Visible = downloading;
        _cancelDownloadButton.Enabled = downloading;
        _setActiveButton.Visible = installed && !active;
        _verifyButton.Visible = installed;
        _deleteButton.Visible = installed;
        _openModelCardButton.Visible = installed;
    }

    private async Task StartDownloadAsync()
    {
        var entry = SelectedEntry(); if (entry is null) return;
        if (_downloads.IsInstalled(entry)) { MessageBox.Show("This model is already installed and has the expected size.", Text); return; }
        if (entry.DiscoveredOnline && MessageBox.Show(
                "This model was discovered in the official upstream catalog after this Manager release. It may require a newer NInfer engine. Continue with the download?",
                "New upstream model", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
        _downloadCancellation = new CancellationTokenSource();
        _downloadingModelFile = entry.FileName; RefreshModelButtons();
        var progress = new Progress<DownloadProgress>(p => { _modelProgress.Value = p.Percent; _modelProgressText.Text = p.Description + (p.BytesPerSecond > 0 ? $" — {p.BytesPerSecond / 1024d / 1024d:0.0} MiB/s" : ""); });
        try { await _downloads.DownloadAsync(entry, progress, _downloadCancellation.Token); MessageBox.Show("The model was downloaded and verified successfully.", Text); }
        catch (OperationCanceledException) { _modelProgressText.Text = "Download paused. Select Install / Resume to continue."; }
        catch (Exception exception) { _logger.Write("Model download failed", exception); MessageBox.Show(exception.Message, "Download failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { _downloadCancellation.Dispose(); _downloadCancellation = null; _downloadingModelFile = null; RefreshModels(); }
    }

    private async Task SetSelectedActiveAsync()
    {
        var entry = SelectedEntry(); if (entry is null) return;
        if (!_downloads.IsInstalled(entry)) { MessageBox.Show("Install and verify this model first.", Text); return; }
        if (_engine.IsLoaded) await _engine.UnloadAsync("model switch");
        _settings.ActiveModelFile = entry.FileName; _settings.GetProfile(entry); _settingsStore.Save(_settings);
        SelectProfileByFile(entry.FileName); RefreshModels(); RefreshUi();
    }

    private async Task VerifySelectedAsync()
    {
        var entry = SelectedEntry(); if (entry is null) return;
        var progress = new Progress<DownloadProgress>(p => { _modelProgress.Value = p.Percent; _modelProgressText.Text = p.Description; });
        try { MessageBox.Show(await _downloads.VerifyAsync(entry, progress, CancellationToken.None) ? "Verification passed." : "Verification failed.", Text); }
        catch (Exception e) { MessageBox.Show(e.Message, "Verification failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task ImportSelectedAsync()
    {
        var entry = SelectedEntry(); if (entry is null) return;
        using var dialog = new OpenFileDialog { Filter = "NInfer artifacts (*.ninfer)|*.ninfer", Title = $"Import {entry.DisplayName}" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var progress = new Progress<DownloadProgress>(p => { _modelProgress.Value = p.Percent; _modelProgressText.Text = p.Description; });
        try { await _downloads.ImportAsync(dialog.FileName, entry, progress, CancellationToken.None); MessageBox.Show("The model was imported and verified.", Text); RefreshModels(); }
        catch (Exception e) { MessageBox.Show(e.Message, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task DeleteSelectedAsync()
    {
        var entry = SelectedEntry(); if (entry is null) return;
        if (MessageBox.Show($"Move {entry.DisplayName} to the Recycle Bin?", "Delete model", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        if (_engine.IsLoaded && _settings.ActiveModelFile.Equals(entry.FileName, StringComparison.OrdinalIgnoreCase)) await _engine.UnloadAsync("active model deletion");
        _downloads.Delete(entry);
        if (_settings.ActiveModelFile.Equals(entry.FileName, StringComparison.OrdinalIgnoreCase)) { _settings.ActiveModelFile = string.Empty; _settingsStore.Save(_settings); }
        RefreshModels(); RefreshUi();
    }

    private async Task RefreshCatalogAsync()
    {
        try { var count = await _catalog.RefreshOnlineAsync(); MessageBox.Show(count == 0 ? "The model catalog is up to date." : $"{count} new model(s) were found.", Text); }
        catch (Exception e) { MessageBox.Show("The official catalog could not be reached. The cached catalog is still available.\n\n" + e.Message, "Catalog check", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void PopulateProfileSelector()
    {
        var selectedFile = (_profileModel.SelectedItem as ModelChoice)?.Entry.FileName ?? _settings.ActiveModelFile;
        _profileModel.DataSource = _catalog.Entries.Select(x => new ModelChoice(x)).ToList();
        _profileModel.DisplayMember = nameof(ModelChoice.Name);
        SelectProfileByFile(selectedFile);
    }

    private void SelectProfileByFile(string fileName)
    {
        for (var i = 0; i < _profileModel.Items.Count; i++) if ((_profileModel.Items[i] as ModelChoice)?.Entry.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase) == true) { _profileModel.SelectedIndex = i; return; }
    }

    private void SelectProfile()
    {
        if (_profileModel.SelectedItem is ModelChoice choice)
        {
            _profilePropertyGrid.SelectedObject = _settings.GetProfile(choice.Entry);
            SyncBasicSettings();
        }
    }

    private void SyncBasicSettings()
    {
        if (_profileModel.SelectedItem is not ModelChoice choice) return;
        var profile = _settings.GetProfile(choice.Entry);
        _basicVision.Checked = profile.VisionEnabled;
        _basicContext.Value = Math.Clamp(profile.MaxContext, (int)_basicContext.Minimum, (int)_basicContext.Maximum);
        _basicKv.SelectedItem = profile.KvPrecision;
        _basicAutoUnload.Checked = _settings.AutoUnloadEnabled;
        _basicIdle.Value = Math.Clamp(_settings.IdleMinutes, _basicIdle.Minimum, _basicIdle.Maximum);
        _basicPublicPort.Value = Math.Clamp(_settings.PublicPort, (int)_basicPublicPort.Minimum, (int)_basicPublicPort.Maximum);
        _basicLockPort.Checked = _settings.LockPublicPort;
        _basicApiKey.Text = _settings.ApiKey;
        _basicAutoUpdates.Checked = _settings.AutoCheckUpdates;
    }

    private void ApplyBasicSettings()
    {
        if (_profileModel.SelectedItem is not ModelChoice choice) return;
        var profile = _settings.GetProfile(choice.Entry);
        profile.VisionEnabled = _basicVision.Checked;
        profile.MaxContext = (int)_basicContext.Value;
        profile.KvPrecision = _basicKv.SelectedItem is KvPrecision precision ? precision : KvPrecision.Int8;
        if (profile.KvCapacityMode == KvCapacityMode.MatchContext) profile.CustomKvCapacity = profile.MaxContext;
        _settings.AutoUnloadEnabled = _basicAutoUnload.Checked;
        _settings.IdleMinutes = _basicIdle.Value;
        _settings.PublicPort = (int)_basicPublicPort.Value;
        _settings.LockPublicPort = _basicLockPort.Checked;
        _settings.ApiKey = _basicApiKey.Text.Trim();
        _settings.AutoCheckUpdates = _basicAutoUpdates.Checked;
        _appPropertyGrid.Refresh(); _profilePropertyGrid.Refresh();
    }

    private Task SaveSettingsAsync()
    {
        try
        {
            ApplyBasicSettings(); ValidateSettings(); _settingsStore.Save(_settings); StartupIntegration.SetEnabled(_startWithWindows.Checked); _proxy.ApplyLifecycleSettings(); RefreshUi();
            if (_settings.PublicPort != _savedPublicPort || _settings.BackendPort != _savedBackendPort)
                MessageBox.Show("Settings were saved. Restart NInfer Manager to apply the new ports.", Text);
            else if (_engine.IsLoaded && MessageBox.Show("Settings were saved. Restart NInfer now to apply the active model profile?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                _ = RunEngineActionAsync(_engine.RestartAsync);
            else MessageBox.Show("Settings saved.", Text);
        }
        catch (Exception e) { MessageBox.Show(e.Message, "Invalid settings", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        return Task.CompletedTask;
    }

    private Task RestoreProfileAsync()
    {
        if (_profileModel.SelectedItem is not ModelChoice choice) return Task.CompletedTask;
        if (MessageBox.Show($"Restore recommended defaults for {choice.Entry.DisplayName}?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return Task.CompletedTask;
        _settings.Profiles[choice.Entry.FileName] = ModelProfile.CreateRecommended(choice.Entry); SelectProfile(); return Task.CompletedTask;
    }

    private void ValidateSettings()
    {
        if (_settings.PublicPort is < 1 or > 65535 || _settings.BackendPort is < 1 or > 65535 || _settings.PublicPort == _settings.BackendPort) throw new InvalidOperationException("Public and backend ports must be different values between 1 and 65535.");
        if (_settings.IdleMinutes is < 0.1m or > 10080m) throw new InvalidOperationException("Idle minutes must be between 0.1 and 10080.");
        if (_settings.CatalogCheckHours is < 1 or > 8760) throw new InvalidOperationException("Catalog check interval must be between 1 and 8760 hours.");
        if (_settings.UpdateCheckHours is < 1 or > 8760) throw new InvalidOperationException("Update check interval must be between 1 and 8760 hours.");
        foreach (var (name, p) in _settings.Profiles)
        {
            if (p.MaxContext is < 1024 or > 262144 || p.DefaultMaxTokens is < 1 or > 262144) throw new InvalidOperationException($"Invalid context or output limit in {name}.");
            if (p.DraftTokens is < 1 or > 5 || p.MaxConcurrency is < 1 or > 8) throw new InvalidOperationException($"Draft tokens must be 1-5 and concurrency must be 1-8 in {name}.");
        }
    }

    private async Task SendApiTestAsync(Button button)
    {
        if (!_engine.HasActiveModel) { MessageBox.Show(this, "Choose and install a model before sending an inference request.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); SelectPage(1); return; }
        button.Enabled = false; _testOutput.Text = "Loading the model and sending a request...";
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            if (!string.IsNullOrWhiteSpace(_settings.ApiKey)) client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            var body = new { model = _engine.ActiveEntry.ModelId, messages = new[] { new { role = "user", content = _testPrompt.Text } }, max_tokens = 128, temperature = 0 };
            using var response = await client.PostAsJsonAsync(_proxy.ApiBaseUrl + "/chat/completions", body);
            _testOutput.Text = await response.Content.ReadAsStringAsync();
        }
        catch (Exception e) { _testOutput.Text = e.Message; }
        finally { button.Enabled = true; }
    }

    private async Task RestartApiOnPortAsync(bool save)
    {
        try
        {
            var requested = (int)_basicPublicPort.Value;
            if (requested == _settings.BackendPort) throw new InvalidOperationException("The public and internal NInfer ports must be different.");
            await _proxy.RestartAsync(requested);
            if (save)
            {
                _settings.PublicPort = requested; _settings.LockPublicPort = _basicLockPort.Checked; _settingsStore.Save(_settings); _savedPublicPort = requested;
            }
            RefreshUi();
            MessageBox.Show(this, $"The API is now available at {_proxy.ApiBaseUrl}.\n\n" + (save ? "This port and behavior were saved for future launches." : "This change applies only to the current session."), "API restarted", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "Port unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private Task ShowSetupWizardAsync()
    {
        using var wizard = new FirstRunWizard(_settings, _settingsStore, _catalog, _downloads, _proxy, _icon);
        wizard.ShowDialog(this); SyncBasicSettings(); RefreshModels(); RefreshUi(); return Task.CompletedTask;
    }

    private void RefreshLogs() => _logBox.Text = _logger.ReadTail();
    private async Task CreateDiagnosticsAsync()
    {
        try { var path = await DiagnosticsPackage.CreateAsync(_paths, _settings, _logger); MessageBox.Show("Diagnostics package created without API keys or model data:\n\n" + path, Text); Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }); }
        catch (Exception e) { MessageBox.Show(e.Message, "Diagnostics failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task RunEngineActionAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception e) { _logger.Write("Engine action failed", e); MessageBox.Show(e.Message, "NInfer Manager", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        RefreshUi();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_realExit || !_settings.CloseToTray) return;
        e.Cancel = true; HideToTray();
    }

    private void RestoreFromTray() { Show(); ShowInTaskbar = true; WindowState = FormWindowState.Normal; Activate(); }
    private void ExitCompletely() { _realExit = true; _tray.Visible = false; Close(); }
    private static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private void ToggleTheme()
    {
        try
        {
            _settings.Theme = UiTheme.Dark ? ThemePreference.Light : ThemePreference.Dark;
            _settingsStore.Save(_settings);
            UiTheme.SetTheme(_settings.Theme, this);
            UiTheme.StyleMenu(_trayMenu);
            _lastProfileSignature = string.Empty;
            RefreshUi();
            SelectPage(_selectedPage);
            ShowSettingsSection(_advancedSettingsVisible);
            _appPropertyGrid.Refresh();
            _profilePropertyGrid.Refresh();
        }
        catch (Exception exception)
        {
            _logger.Write("Theme could not be changed", exception);
            MessageBox.Show(this, exception.Message, "Theme could not be changed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ShowSettingsSection(bool advanced)
    {
        if (_essentialsSection is null || _advancedSection is null) return;
        _advancedSettingsVisible = advanced;
        _essentialsSection.Visible = !advanced; _advancedSection.Visible = advanced;
        (advanced ? _advancedSection : _essentialsSection).BringToFront();
        UiTheme.StyleButton(_essentialsTabButton, ButtonKind.Secondary); UiTheme.StyleButton(_advancedTabButton, ButtonKind.Secondary);
        var selected = advanced ? _advancedTabButton : _essentialsTabButton;
        selected.BackColor = UiTheme.AccentSoft; selected.ForeColor = UiTheme.Text; selected.FlatAppearance.BorderColor = UiTheme.Accent;
    }

    private void PostToUi(Action action)
    {
        if (IsDisposed || Disposing || !IsHandleCreated) return;
        try { BeginInvoke(action); }
        catch (InvalidOperationException) when (IsDisposed || Disposing || !IsHandleCreated) { }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _visibleTimer.Dispose(); _tray.Dispose(); _trayMenu.Dispose(); _icon.Dispose(); _downloadCancellation?.Cancel(); _downloadCancellation?.Dispose(); _updateCancellation?.Cancel(); _updateCancellation?.Dispose(); }
        base.Dispose(disposing);
    }

    private static RoundedPanel MetricCard(MetricRing ring)
    {
        var card = UiTheme.Card(); card.Dock = DockStyle.Fill; card.Padding = new Padding(8); card.Controls.Add(ring); return card;
    }

    private static (RoundedPanel Card, Panel Content) SectionCard(string title, string subtitle)
    {
        var card = UiTheme.Card(); card.Dock = DockStyle.Fill; card.Padding = new Padding(16);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        heading.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI Variable Display Semibold", 11f) }, 0, 0);
        heading.Controls.Add(UiTheme.Role(new Label { Text = subtitle, AutoSize = true, Font = new Font("Segoe UI Variable Text", 8.75f) }, ThemeRole.MutedText), 0, 1);
        var content = new Panel { Dock = DockStyle.Fill };
        layout.Controls.Add(heading, 0, 0); layout.Controls.Add(content, 0, 1); card.Controls.Add(layout); return (card, content);
    }

    private static Button ActionTile(string text)
    {
        var button = new ThemedButton { Text = text, Dock = DockStyle.Fill, Margin = new Padding(4), AccessibleName = text.Replace("\n", " ") };
        UiTheme.StyleButton(button, ButtonKind.ActionTile); return button;
    }

    private static Control Chip(string text)
    {
        var chip = UiTheme.Card(ThemeRole.AccentSoft); chip.Size = new Size(125, 36); chip.Padding = new Padding(7, 7, 7, 5); chip.Margin = new Padding(0, 0, 7, 8);
        chip.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true, Font = new Font("Segoe UI Variable Text Semibold", 8.75f) });
        return chip;
    }

    private static Panel Page(string text) => new() { Name = text, AccessibleName = text, BackColor = UiTheme.Background, ForeColor = UiTheme.Text };
    private static Control PageTitle(string title, string subtitle)
    {
        var panel = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, RowCount = 2, Margin = new Padding(0, 0, 0, 14) };
        panel.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = UiTheme.Text, Font = new Font("Segoe UI Variable Display Semibold", 20f) }, 0, 0);
        panel.Controls.Add(UiTheme.Role(new Label { Text = subtitle, AutoSize = true, Font = new Font("Segoe UI Variable Text", 9.5f), Padding = new Padding(0, 2, 0, 0) }, ThemeRole.MutedText), 0, 1);
        return panel;
    }
    private static Label Heading(string text) => new() { Text = text, AutoSize = true, ForeColor = UiTheme.Text, Font = new Font("Segoe UI Variable Display Semibold", 17f), Padding = new Padding(0, 0, 0, 6) };
    private static Label ValueLabel() => new() { AutoSize = true, ForeColor = UiTheme.Text, Font = new Font("Segoe UI Variable Text", 9.5f), Padding = new Padding(0, 3, 0, 3) };
    private static Button Button(string text, bool enabled = true, bool primary = false, bool danger = false)
    {
        var button = new ThemedButton { Text = text, AutoSize = true, MinimumSize = new Size(112, 38), Enabled = enabled, AccessibleName = text };
        UiTheme.StyleButton(button, primary, danger); return button;
    }
    private static FlowLayoutPanel Flow() => new() { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(0, 6, 0, 6) };
    private static void AddInfo(TableLayoutPanel table, string name, Control value) { var row = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.AutoSize)); table.Controls.Add(UiTheme.Role(new Label { Text = name, AutoSize = true, Font = new Font("Segoe UI Variable Text Semibold", 9f), Padding = new Padding(0, 4, 0, 3) }, ThemeRole.MutedText), 0, row); table.Controls.Add(value, 1, row); }
    private static void AddSetting(TableLayoutPanel table, string name, Control control, string description)
    {
        var row = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = name, AutoSize = true, ForeColor = UiTheme.Text, Font = new Font("Segoe UI Variable Text Semibold", 9.25f), Padding = new Padding(0, 9, 8, 9) }, 0, row);
        control.Margin = new Padding(0, 5, 12, 5); control.AccessibleDescription = description; table.Controls.Add(control, 1, row);
        table.Controls.Add(UiTheme.Role(new Label { Text = description, AutoSize = true, MaximumSize = new Size(420, 0), Padding = new Padding(0, 9, 0, 9) }, ThemeRole.MutedText), 2, row);
    }
    private static void AddAction(FlowLayoutPanel flow, string text, Func<Task> action, bool primary = false, bool danger = false) { var button = Button(text, true, primary, danger); button.Click += async (_, _) => { button.Enabled = false; try { await action(); } finally { if (!button.IsDisposed) button.Enabled = true; } }; flow.Controls.Add(button); }
    private static async Task RunButtonActionAsync(Button button, Func<Task> action) { button.Enabled = false; try { await action(); } finally { if (!button.IsDisposed) button.Enabled = true; } }

    private sealed record ModelRow(ModelCatalogEntry Entry, string Status)
    {
        public string Name => Entry.DisplayName; public string Weights => Entry.Weights; public string Size => Entry.SizeText; public string Vision => Entry.Vision ? "Yes" : "No";
    }
    private sealed record ModelChoice(ModelCatalogEntry Entry) { public string Name => Entry.DisplayName; public override string ToString() => Name; }
}
