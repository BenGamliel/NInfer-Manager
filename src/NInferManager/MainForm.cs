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
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly List<Button> _navigationButtons = [];
    private readonly Label _headerState = new() { AutoSize = true, TextAlign = ContentAlignment.MiddleRight };
    private readonly Panel _updateBanner = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Label _updateBannerText = new() { AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _updateBannerButton = Button("Install update", true, true);
    private readonly ProgressBar _updateProgress = new() { Dock = DockStyle.Top, Height = 8, Visible = false };
    private readonly Label _updateProgressText = new() { AutoSize = true, ForeColor = UiTheme.Muted, Visible = false };
    private readonly Label _stateValue = ValueLabel();
    private readonly Label _modelValue = ValueLabel();
    private readonly Label _apiValue = ValueLabel();
    private readonly Label _profileValue = ValueLabel();
    private readonly Label _gpuValue = ValueLabel();
    private readonly DataGridView _modelsGrid = new();
    private readonly TextBox _modelSearch = new() { Width = 280, PlaceholderText = "Search models" };
    private readonly ComboBox _modelFilter = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ProgressBar _modelProgress = new() { Dock = DockStyle.Fill, Height = 20 };
    private readonly Label _modelProgressText = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly Button _installButton = Button("Install / Resume");
    private readonly Button _cancelDownloadButton = Button("Pause", false);
    private readonly PropertyGrid _appPropertyGrid = new() { Dock = DockStyle.Fill, PropertySort = PropertySort.Categorized, HelpVisible = true, ToolbarVisible = true };
    private readonly PropertyGrid _profilePropertyGrid = new() { Dock = DockStyle.Fill, PropertySort = PropertySort.Categorized, HelpVisible = true, ToolbarVisible = true };
    private readonly ComboBox _profileModel = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly CheckBox _startWithWindows = new() { Text = "Start with Windows", AutoSize = true };
    private readonly CheckBox _basicVision = new() { Text = "Enable image and video input", AutoSize = true };
    private readonly NumericUpDown _basicContext = new() { Minimum = 1024, Maximum = 262144, Increment = 1024, ThousandsSeparator = true, Width = 150 };
    private readonly ComboBox _basicKv = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly CheckBox _basicAutoUnload = new() { Text = "Unload the model automatically when idle", AutoSize = true };
    private readonly NumericUpDown _basicIdle = new() { Minimum = 0.1m, Maximum = 10080, DecimalPlaces = 1, Increment = 0.5m, Width = 150 };
    private readonly NumericUpDown _basicPublicPort = new() { Minimum = 1, Maximum = 65535, Width = 150 };
    private readonly CheckBox _basicLockPort = new() { Text = "Lock this port", AutoSize = true };
    private readonly TextBox _basicApiKey = new() { Width = 260, UseSystemPasswordChar = true, PlaceholderText = "Optional — local access is open when empty" };
    private readonly CheckBox _basicAutoUpdates = new() { Text = "Automatically check for NInfer Manager updates", AutoSize = true };
    private readonly TextBox _logBox = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = new Font("Consolas", 9f) };
    private readonly TextBox _testPrompt = new() { Text = "Reply with exactly: NInfer Manager OK", Dock = DockStyle.Fill };
    private readonly TextBox _testOutput = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly Button _loadButton = Button("Load model");
    private readonly Button _restartButton = Button("Restart NInfer");
    private readonly Button _sendTestButton = Button("Send test", true, true);
    private readonly Panel _noModelCard = UiTheme.Card();
    private readonly System.Windows.Forms.Timer _visibleTimer = new() { Interval = 1000 };
    private CancellationTokenSource? _downloadCancellation;
    private CancellationTokenSource? _updateCancellation;
    private UpdateInfo? _availableUpdate;
    private bool _realExit;
    private int _savedPublicPort;
    private int _savedBackendPort;

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

        _tabs.TabPages.Add(BuildDashboard());
        _tabs.TabPages.Add(BuildModelsPage());
        _tabs.TabPages.Add(BuildSettingsPage());
        _tabs.TabPages.Add(BuildLogsPage());
        _tabs.TabPages.Add(BuildAboutPage());
        Controls.Add(BuildShell());
        UiTheme.ApplyWindow(this);

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Open NInfer Manager", null, (_, _) => RestoreFromTray());
        trayMenu.Items.Add("Open Web UI", null, (_, _) => OpenUrl(_proxy.WebUiUrl));
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Load model", null, async (_, _) => await RunEngineActionAsync(() => _engine.EnsureLoadedAsync()));
        trayMenu.Items.Add("Unload from VRAM", null, async (_, _) => await RunEngineActionAsync(() => _engine.UnloadAsync("notification area")));
        trayMenu.Items.Add("Restart NInfer", null, async (_, _) => await RunEngineActionAsync(_engine.RestartAsync));
        var idleMenu = new ToolStripMenuItem("Automatic VRAM unload");
        idleMenu.DropDownItems.Add("Off", null, (_, _) => SetIdleFromTray(false, 3));
        idleMenu.DropDownItems.Add("After 3 minutes", null, (_, _) => SetIdleFromTray(true, 3));
        idleMenu.DropDownItems.Add("After 10 minutes", null, (_, _) => SetIdleFromTray(true, 10));
        idleMenu.DropDownItems.Add("After 30 minutes", null, (_, _) => SetIdleFromTray(true, 30));
        trayMenu.Items.Add(idleMenu);
        trayMenu.Items.Add("Check for updates", null, async (_, _) => await CheckForUpdatesAsync(true));
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Exit", null, (_, _) => ExitCompletely());
        _tray = new NotifyIcon { Icon = _icon, Text = "NInfer Manager - Unloaded", Visible = true, ContextMenuStrip = trayMenu };
        _tray.DoubleClick += (_, _) => RestoreFromTray();

        FormClosing += OnFormClosing;
        Shown += OnShown;
        VisibleChanged += (_, _) => { _visibleTimer.Enabled = Visible; if (Visible) RefreshUi(); };
        _visibleTimer.Tick += (_, _) => RefreshUi();
        _engine.StateChanged += state => PostToUi(() =>
        {
            RefreshUi();
            if (state == EngineState.Unloaded && !Visible) _ = WorkingSetTrimmer.TrimAfterIdleAsync();
        });
        _catalog.CatalogChanged += () => PostToUi(RefreshModels);
        _logger.LineWritten += _ => { if (Visible && _tabs.SelectedTab?.Text == "Logs") PostToUi(RefreshLogs); };
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
        _tabs.Appearance = TabAppearance.FlatButtons;
        _tabs.ItemSize = new Size(0, 1);
        _tabs.SizeMode = TabSizeMode.Fixed;
        _tabs.Multiline = true;

        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = UiTheme.Background };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var sidebar = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = UiTheme.Sidebar, Padding = new Padding(14, 18, 14, 14), RowCount = 3 };
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var brand = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, Margin = new Padding(4, 0, 0, 24) };
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42)); brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        brand.Controls.Add(new PictureBox { Image = _icon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(32, 32), Margin = new Padding(0, 0, 10, 0) }, 0, 0);
        brand.Controls.Add(new Label { Text = "NInfer\nManager", AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI Variable Display Semibold", 11.5f), Margin = new Padding(0) }, 1, 0);
        sidebar.Controls.Add(brand, 0, 0);
        var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = false };
        string[] names = ["Dashboard", "Models", "Settings", "Logs", "About"];
        for (var index = 0; index < names.Length; index++)
        {
            var pageIndex = index;
            var button = new Button
            {
                Text = names[index],
                Width = 192,
                Height = 46,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                ForeColor = Color.FromArgb(210, 220, 231),
                BackColor = UiTheme.Sidebar,
                Cursor = Cursors.Hand,
                AccessibleName = $"Open {names[index]} page",
                Margin = new Padding(0, 0, 0, 4),
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(37, 52, 70);
            button.Click += (_, _) => SelectPage(pageIndex);
            _navigationButtons.Add(button); navigation.Controls.Add(button);
        }
        sidebar.Controls.Add(navigation, 0, 1);
        sidebar.Controls.Add(new Label { Text = $"Version {UpdateService.CurrentVersion}\n{(_paths.IsPortable ? "Portable" : "Installed")}", AutoSize = true, ForeColor = Color.FromArgb(145, 163, 181), Padding = new Padding(12, 4, 0, 0) }, 0, 2);
        shell.Controls.Add(sidebar, 0, 0);

        var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = UiTheme.Background, RowCount = 3, ColumnCount = 1 };
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        workspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(24, 14, 24, 8), BackColor = UiTheme.Background };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(new Label { Text = "Local AI control center", AutoSize = true, Font = new Font("Segoe UI Variable Display Semibold", 13f), ForeColor = UiTheme.Text }, 0, 0);
        _headerState.ForeColor = UiTheme.Muted; _headerState.Padding = new Padding(0, 5, 0, 0); header.Controls.Add(_headerState, 1, 0);
        workspace.Controls.Add(header, 0, 0);

        _updateBanner.BackColor = UiTheme.Dark ? Color.FromArgb(24, 68, 75) : Color.FromArgb(226, 248, 250);
        _updateBanner.Padding = new Padding(22, 8, 18, 8);
        var updateLayout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        updateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); updateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _updateBannerText.ForeColor = UiTheme.Text; _updateBannerText.Padding = new Padding(0, 9, 0, 0);
        _updateBannerButton.Click += async (_, _) => await InstallAvailableUpdateAsync();
        updateLayout.Controls.Add(_updateBannerText, 0, 0); updateLayout.Controls.Add(_updateBannerButton, 1, 0); _updateBanner.Controls.Add(updateLayout);
        workspace.Controls.Add(_updateBanner, 0, 1);
        workspace.Controls.Add(_tabs, 0, 2);
        shell.Controls.Add(workspace, 1, 0);
        SelectPage(0);
        return shell;
    }

    private void SelectPage(int index)
    {
        _tabs.SelectedIndex = index;
        for (var i = 0; i < _navigationButtons.Count; i++)
        {
            var selected = i == index;
            _navigationButtons[i].BackColor = selected ? UiTheme.AccentDark : UiTheme.Sidebar;
            _navigationButtons[i].ForeColor = selected ? Color.White : Color.FromArgb(210, 220, 231);
            _navigationButtons[i].Font = new Font("Segoe UI Variable Text", 9.25f, selected ? FontStyle.Bold : FontStyle.Regular);
        }
        if (_tabs.SelectedTab?.Text == "Logs") RefreshLogs();
    }

    private TabPage BuildDashboard()
    {
        var page = Page("Dashboard");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28, 22, 28, 24), ColumnCount = 1, RowCount = 6, BackColor = UiTheme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(PageTitle("Dashboard", "Run local inference without keeping the model in VRAM between sessions."), 0, 0);
        _noModelCard.Dock = DockStyle.Top; _noModelCard.Height = 126; _noModelCard.BackColor = UiTheme.AccentSoft;
        var empty = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8) }; empty.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); empty.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var emptyCopy = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        emptyCopy.Controls.Add(new Label { Text = "Choose your first model", AutoSize = true, ForeColor = UiTheme.Text, Font = new Font("Segoe UI Variable Display Semibold", 15f) }, 0, 0);
        emptyCopy.Controls.Add(new Label { Text = "Nothing is bundled or selected yet. Open Model Manager to install a verified NInfer model.", AutoSize = true, ForeColor = UiTheme.Muted, Padding = new Padding(0, 5, 0, 0) }, 0, 1);
        var choose = Button("Choose a model", true, true); choose.Click += (_, _) => SelectPage(1); empty.Controls.Add(emptyCopy, 0, 0); empty.Controls.Add(choose, 1, 0); _noModelCard.Controls.Add(empty); root.Controls.Add(_noModelCard, 0, 1);
        var infoCard = UiTheme.Card(); infoCard.Dock = DockStyle.Top; infoCard.AutoSize = true;
        var info = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, Padding = new Padding(2, 4, 2, 4) };
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155)); info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddInfo(info, "Engine status", _stateValue); AddInfo(info, "Active model", _modelValue); AddInfo(info, "API address", _apiValue);
        AddInfo(info, "Active profile", _profileValue); AddInfo(info, "GPU", _gpuValue);
        infoCard.Controls.Add(info); root.Controls.Add(infoCard, 0, 2);
        var actions = Flow();
        _loadButton.Click += async (_, _) => await RunEngineActionAsync(() => _engine.EnsureLoadedAsync()); actions.Controls.Add(_loadButton);
        AddAction(actions, "Unload from VRAM", async () => await RunEngineActionAsync(() => _engine.UnloadAsync("dashboard")));
        _restartButton.Click += async (_, _) => await RunEngineActionAsync(_engine.RestartAsync); actions.Controls.Add(_restartButton);
        AddAction(actions, "Open Web UI", () => { OpenUrl(_proxy.WebUiUrl); return Task.CompletedTask; });
        AddAction(actions, "Copy API address", () => { Clipboard.SetText(_proxy.ApiBaseUrl); return Task.CompletedTask; });
        AddAction(actions, "Refresh GPU", async () => await RefreshGpuAsync());
        root.Controls.Add(actions, 0, 3);

        var commandGroup = new GroupBox { Text = "Advanced: generated NInfer command", Dock = DockStyle.Top, Height = 145, Padding = new Padding(10), ForeColor = UiTheme.Text, Visible = false };
        var command = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Text = _engine.BuildCommandPreview() };
        commandGroup.Controls.Add(command);
        _engine.StateChanged += _ => PostToUi(() => command.Text = _engine.BuildCommandPreview());
        var toggleCommand = Button("Show advanced command");
        toggleCommand.Click += (_, _) => { commandGroup.Visible = !commandGroup.Visible; toggleCommand.Text = commandGroup.Visible ? "Hide advanced command" : "Show advanced command"; };
        actions.Controls.Add(toggleCommand);
        root.Controls.Add(commandGroup, 0, 4);

        var test = new GroupBox { Text = "Test the OpenAI-compatible API", Dock = DockStyle.Fill, Padding = new Padding(10), ForeColor = UiTheme.Text };
        var testLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        testLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); testLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        testLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); testLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        testLayout.Controls.Add(_testPrompt, 0, 0);
        _sendTestButton.Click += async (_, _) => await SendApiTestAsync(_sendTestButton);
        testLayout.Controls.Add(_sendTestButton, 1, 0); testLayout.Controls.Add(_testOutput, 0, 1); testLayout.SetColumnSpan(_testOutput, 2);
        test.Controls.Add(testLayout); root.Controls.Add(test, 0, 5);
        page.Controls.Add(root); return page;
    }

    private TabPage BuildModelsPage()
    {
        var page = Page("Models");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), RowCount = 6, ColumnCount = 1, BackColor = UiTheme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(PageTitle("Model Manager", "Download, verify and activate official NInfer model artifacts."), 0, 0);
        var filters = Flow();
        _modelSearch.TextChanged += (_, _) => RefreshModels(); _modelFilter.SelectedIndexChanged += (_, _) => RefreshModels();
        filters.Controls.Add(_modelSearch); filters.Controls.Add(_modelFilter); root.Controls.Add(filters, 0, 1);
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
        root.Controls.Add(_modelsGrid, 0, 2);
        var actions = Flow();
        _installButton.Click += async (_, _) => await StartDownloadAsync(); actions.Controls.Add(_installButton);
        _cancelDownloadButton.Click += (_, _) => _downloadCancellation?.Cancel(); actions.Controls.Add(_cancelDownloadButton);
        AddAction(actions, "Set active", SetSelectedActiveAsync); AddAction(actions, "Verify", VerifySelectedAsync);
        AddAction(actions, "Import file", ImportSelectedAsync); AddAction(actions, "Delete", DeleteSelectedAsync);
        AddAction(actions, "Open model card", () => { var e = SelectedEntry(); if (e is not null) OpenUrl(e.ModelCardUrl); return Task.CompletedTask; });
        AddAction(actions, "Check for new models", RefreshCatalogAsync);
        root.Controls.Add(actions, 0, 3);
        var progress = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        progress.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45)); progress.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        progress.Controls.Add(_modelProgress, 0, 0); progress.Controls.Add(_modelProgressText, 1, 0); root.Controls.Add(progress, 0, 4);
        root.Controls.Add(new Label { Text = "Downloads can be paused and resumed. A model becomes usable only after its official size and SHA-256 are verified.", AutoSize = true, ForeColor = UiTheme.Muted, Padding = new Padding(0, 6, 0, 0) }, 0, 5);
        page.Controls.Add(root); return page;
    }

    private TabPage BuildSettingsPage()
    {
        var page = Page("Settings");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), RowCount = 4, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(PageTitle("Settings", "Start with the essentials. Every NInfer option remains available under Advanced."), 0, 0);
        var top = Flow(); top.Controls.Add(new Label { Text = "Model profile:", AutoSize = true, Padding = new Padding(0, 8, 4, 0) });
        _profileModel.SelectedIndexChanged += (_, _) => SelectProfile(); top.Controls.Add(_profileModel); top.Controls.Add(_startWithWindows);
        root.Controls.Add(top, 0, 1);

        var sections = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 7) };
        var essentials = new TabPage("Essentials") { BackColor = UiTheme.Background, Padding = new Padding(16) };
        var essentialsCard = UiTheme.Card(); essentialsCard.Dock = DockStyle.Fill; essentialsCard.AutoScroll = true;
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
        essentialsCard.Controls.Add(fields); essentials.Controls.Add(essentialsCard);

        var advanced = new TabPage("Advanced") { BackColor = UiTheme.Background, Padding = new Padding(8) };
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 360, BackColor = UiTheme.Background };
        var appGroup = new GroupBox { Text = "Application and API", Dock = DockStyle.Fill, Padding = new Padding(6) }; appGroup.Controls.Add(_appPropertyGrid);
        var modelGroup = new GroupBox { Text = "Complete NInfer model profile", Dock = DockStyle.Fill, Padding = new Padding(6) }; modelGroup.Controls.Add(_profilePropertyGrid);
        split.Panel1.Controls.Add(appGroup); split.Panel2.Controls.Add(modelGroup); advanced.Controls.Add(split);
        sections.TabPages.Add(essentials); sections.TabPages.Add(advanced); root.Controls.Add(sections, 0, 2);
        var actions = Flow();
        AddAction(actions, "Save settings", SaveSettingsAsync, true);
        AddAction(actions, "Restore recommended model defaults", RestoreProfileAsync);
        AddAction(actions, "Copy generated command", () => { Clipboard.SetText(_engine.BuildCommandPreview()); return Task.CompletedTask; });
        AddAction(actions, "Open Setup Wizard", ShowSetupWizardAsync);
        root.Controls.Add(actions, 0, 3); page.Controls.Add(root); return page;
    }

    private TabPage BuildLogsPage()
    {
        var page = Page("Logs");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), RowCount = 3, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(PageTitle("Logs and diagnostics", "Inspect recent activity or create a redacted support package."), 0, 0);
        root.Controls.Add(_logBox, 0, 1);
        var actions = Flow(); AddAction(actions, "Refresh", () => { RefreshLogs(); return Task.CompletedTask; });
        AddAction(actions, "Open log file", () => { Process.Start(new ProcessStartInfo(_logger.FilePath) { UseShellExecute = true }); return Task.CompletedTask; });
        AddAction(actions, "Create diagnostics package", CreateDiagnosticsAsync); root.Controls.Add(actions, 0, 2);
        page.Controls.Add(root); return page;
    }

    private TabPage BuildAboutPage()
    {
        var page = Page("About");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), RowCount = 3, ColumnCount = 1 };
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
        _stateValue.Text = _engine.State.ToString();
        var active = _engine.ActiveEntryOrNull;
        _modelValue.Text = active?.DisplayName ?? "No model selected";
        _apiValue.Text = _proxy.ApiBaseUrl;
        _profileValue.Text = active is null ? "Install and activate a model to configure its profile" : $"{_engine.ActiveProfile.MaxContext:N0} context | {_engine.ActiveProfile.KvPrecision} KV | Vision {(_engine.ActiveProfile.VisionEnabled ? "ON" : "OFF")} | Auto-unload {(_settings.AutoUnloadEnabled ? _settings.IdleMinutes + " min" : "OFF")}";
        _headerState.Text = $"{_engine.State}  •  {active?.DisplayName ?? "No model"}  •  {_proxy.ApiBaseUrl}";
        _noModelCard.Visible = active is null; _loadButton.Enabled = active is not null; _restartButton.Enabled = active is not null; _sendTestButton.Enabled = active is not null;
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
        if (!IsDisposed) _gpuValue.Text = gpu?.Summary ?? "NVIDIA GPU information unavailable";
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
    private void RefreshModelButtons() { _installButton.Enabled = SelectedEntry() is not null && _downloadCancellation is null; }

    private async Task StartDownloadAsync()
    {
        var entry = SelectedEntry(); if (entry is null) return;
        if (_downloads.IsInstalled(entry)) { MessageBox.Show("This model is already installed and has the expected size.", Text); return; }
        if (entry.DiscoveredOnline && MessageBox.Show(
                "This model was discovered in the official upstream catalog after this Manager release. It may require a newer NInfer engine. Continue with the download?",
                "New upstream model", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
        _downloadCancellation = new CancellationTokenSource();
        _cancelDownloadButton.Enabled = true; _installButton.Enabled = false;
        var progress = new Progress<DownloadProgress>(p => { _modelProgress.Value = p.Percent; _modelProgressText.Text = p.Description + (p.BytesPerSecond > 0 ? $" — {p.BytesPerSecond / 1024d / 1024d:0.0} MiB/s" : ""); });
        try { await _downloads.DownloadAsync(entry, progress, _downloadCancellation.Token); MessageBox.Show("The model was downloaded and verified successfully.", Text); }
        catch (OperationCanceledException) { _modelProgressText.Text = "Download paused. Select Install / Resume to continue."; }
        catch (Exception exception) { _logger.Write("Model download failed", exception); MessageBox.Show(exception.Message, "Download failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { _downloadCancellation.Dispose(); _downloadCancellation = null; _cancelDownloadButton.Enabled = false; RefreshModels(); }
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
        _downloads.Delete(entry); RefreshModels();
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

    private void PostToUi(Action action)
    {
        if (IsDisposed || Disposing || !IsHandleCreated) return;
        try { BeginInvoke(action); }
        catch (InvalidOperationException) when (IsDisposed || Disposing || !IsHandleCreated) { }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _visibleTimer.Dispose(); _tray.Dispose(); _icon.Dispose(); _downloadCancellation?.Cancel(); _downloadCancellation?.Dispose(); _updateCancellation?.Cancel(); _updateCancellation?.Dispose(); }
        base.Dispose(disposing);
    }

    private static TabPage Page(string text) => new(text) { BackColor = UiTheme.Background, ForeColor = UiTheme.Text };
    private static Control PageTitle(string title, string subtitle)
    {
        var panel = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, RowCount = 2, Margin = new Padding(0, 0, 0, 14) };
        panel.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = UiTheme.Text, Font = new Font("Segoe UI Variable Display Semibold", 20f) }, 0, 0);
        panel.Controls.Add(new Label { Text = subtitle, AutoSize = true, ForeColor = UiTheme.Muted, Font = new Font("Segoe UI Variable Text", 9.5f), Padding = new Padding(0, 2, 0, 0) }, 0, 1);
        return panel;
    }
    private static Label Heading(string text) => new() { Text = text, AutoSize = true, ForeColor = UiTheme.Text, Font = new Font("Segoe UI Variable Display Semibold", 17f), Padding = new Padding(0, 0, 0, 6) };
    private static Label ValueLabel() => new() { AutoSize = true, ForeColor = UiTheme.Text, Font = new Font("Segoe UI Variable Text", 9.5f), Padding = new Padding(0, 3, 0, 3) };
    private static Button Button(string text, bool enabled = true, bool primary = false, bool danger = false)
    {
        var button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(112, 38), Enabled = enabled, AccessibleName = text };
        UiTheme.StyleButton(button, primary, danger); return button;
    }
    private static FlowLayoutPanel Flow() => new() { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(0, 6, 0, 6) };
    private static void AddInfo(TableLayoutPanel table, string name, Control value) { var row = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.AutoSize)); table.Controls.Add(new Label { Text = name, AutoSize = true, ForeColor = UiTheme.Muted, Font = new Font("Segoe UI Variable Text Semibold", 9f), Padding = new Padding(0, 4, 0, 3) }, 0, row); table.Controls.Add(value, 1, row); }
    private static void AddSetting(TableLayoutPanel table, string name, Control control, string description)
    {
        var row = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = name, AutoSize = true, ForeColor = UiTheme.Text, Font = new Font("Segoe UI Variable Text Semibold", 9.25f), Padding = new Padding(0, 9, 8, 9) }, 0, row);
        control.Margin = new Padding(0, 5, 12, 5); control.AccessibleDescription = description; table.Controls.Add(control, 1, row);
        table.Controls.Add(new Label { Text = description, AutoSize = true, MaximumSize = new Size(420, 0), ForeColor = UiTheme.Muted, Padding = new Padding(0, 9, 0, 9) }, 2, row);
    }
    private static void AddAction(FlowLayoutPanel flow, string text, Func<Task> action, bool primary = false, bool danger = false) { var button = Button(text, true, primary, danger); button.Click += async (_, _) => { button.Enabled = false; try { await action(); } finally { if (!button.IsDisposed) button.Enabled = true; } }; flow.Controls.Add(button); }

    private sealed record ModelRow(ModelCatalogEntry Entry, string Status)
    {
        public string Name => Entry.DisplayName; public string Weights => Entry.Weights; public string Size => Entry.SizeText; public string Vision => Entry.Vision ? "Yes" : "No";
    }
    private sealed record ModelChoice(ModelCatalogEntry Entry) { public string Name => Entry.DisplayName; public override string ToString() => Name; }
}
