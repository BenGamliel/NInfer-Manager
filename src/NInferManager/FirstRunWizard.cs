namespace NInferManager;

internal sealed class FirstRunWizard : Form
{
    private readonly AppSettings _settings;
    private readonly SettingsStore _settingsStore;
    private readonly ModelCatalogService _catalog;
    private readonly ModelDownloadService _downloads;
    private readonly ApiProxy _proxy;
    private readonly TabControl _pages = new() { Dock = DockStyle.Fill, Appearance = TabAppearance.FlatButtons, ItemSize = new Size(0, 1), SizeMode = TabSizeMode.Fixed };
    private readonly ComboBox _portMode = new ThemedComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 310 };
    private readonly ThemedNumericField _port = new() { Minimum = 1024, Maximum = 65535, Width = 150 };
    private readonly ComboBox _models = new ThemedComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 430 };
    private readonly Label _modelDetails = new() { AutoSize = true, MaximumSize = new Size(570, 0), ForeColor = UiTheme.Muted };
    private readonly ProgressBar _progress = new ThemedProgressBar { Width = 570, Height = 10 };
    private readonly Label _progressText = new() { AutoSize = true, ForeColor = UiTheme.Muted };
    private readonly Button _back = ActionButton("Back");
    private readonly Button _next = ActionButton("Continue", true);
    private readonly Button _skip = ActionButton("Skip setup");
    private CancellationTokenSource? _downloadCancellation;

    public FirstRunWizard(AppSettings settings, SettingsStore settingsStore, ModelCatalogService catalog,
        ModelDownloadService downloads, ApiProxy proxy, Icon icon)
    {
        _settings = settings; _settingsStore = settingsStore; _catalog = catalog; _downloads = downloads; _proxy = proxy;
        Text = "Welcome to NInfer Manager"; Icon = icon; StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 580); Size = new Size(780, 620); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
        BackColor = UiTheme.Background; ForeColor = UiTheme.Text; Font = new Font("Segoe UI Variable Text", 9.5f);
        _pages.TabPages.Add(WelcomePage()); _pages.TabPages.Add(PortPage()); _pages.TabPages.Add(ModelPage());
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72)); root.Controls.Add(_pages, 0, 0);
        var footer = UiTheme.Role(new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(26, 12, 26, 14) }, ThemeRole.Surface);
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        _skip.Click += (_, _) => CompleteWithoutModel(); footer.Controls.Add(_skip, 0, 0);
        var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        _back.Click += (_, _) => MovePage(-1); _next.Click += async (_, _) => await ContinueAsync(); navigation.Controls.Add(_next); navigation.Controls.Add(_back); footer.Controls.Add(navigation, 1, 0);
        root.Controls.Add(footer, 0, 1); Controls.Add(root); UiTheme.ApplyWindow(this);
        UiTheme.Role(_modelDetails, ThemeRole.MutedText); UiTheme.Role(_progressText, ThemeRole.MutedText); UiTheme.ApplyTree(this);
        _portMode.Items.AddRange(["Automatic — choose a free port when needed", "Locked — always require this exact port"]); _portMode.SelectedIndex = settings.LockPublicPort ? 1 : 0;
        _port.Value = proxy.Port; _models.DataSource = catalog.Entries.Select(x => new WizardModel(x)).ToList(); _models.DisplayMember = nameof(WizardModel.Name);
        _models.SelectedIndexChanged += (_, _) => RefreshModelDetails(); RefreshModelDetails(); RefreshNavigation();
        FormClosing += (_, _) => _downloadCancellation?.Cancel();
    }

    private TabPage WelcomePage()
    {
        var page = Page(); var content = Content();
        content.Controls.Add(Title("Your local AI, without the command line"));
        content.Controls.Add(Copy("NInfer Manager keeps the API available, loads a model only when it is needed, and can release VRAM automatically when you are done."));
        var card = UiTheme.Card(); card.Width = 610; card.Height = 185;
        card.ThemeRole = ThemeRole.SurfaceAlt; card.RefreshTheme();
        card.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "This quick setup will help you:\n\n  1. Confirm how the local API port is selected\n  2. Choose and install your first model\n  3. Start with safe recommended settings\n\nYou can skip now and reopen Setup Wizard from Settings.", Font = new Font("Segoe UI Variable Text", 10f), Padding = new Padding(8) });
        content.Controls.Add(card); page.Controls.Add(content); return page;
    }

    private TabPage PortPage()
    {
        var page = Page(); var content = Content();
        content.Controls.Add(Title("Connect applications to your local API"));
        content.Controls.Add(Copy("Automatic mode is recommended. If the usual port is busy, NInfer Manager safely selects another local port and tells you."));
        var card = UiTheme.Card(); card.Width = 610; card.Height = 190;
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(12) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.Controls.Add(FieldLabel("Port behavior"), 0, 0); fields.Controls.Add(_portMode, 1, 0);
        fields.Controls.Add(FieldLabel("API port"), 0, 1); fields.Controls.Add(_port, 1, 1);
        fields.Controls.Add(UiTheme.Role(new Label { Text = "Locked mode shows an error instead of silently changing ports when the selected port is unavailable.", AutoSize = true, MaximumSize = new Size(390, 0), Padding = new Padding(0, 8, 0, 0) }, ThemeRole.MutedText), 1, 2);
        card.Controls.Add(fields); content.Controls.Add(card); page.Controls.Add(content); return page;
    }

    private TabPage ModelPage()
    {
        var page = Page(); var content = Content();
        content.Controls.Add(Title("Choose your first model"));
        content.Controls.Add(Copy("Models are downloaded from their official source and verified before they can be activated. No model is bundled with NInfer Manager."));
        content.Controls.Add(new Label { Text = "Model", AutoSize = true, ForeColor = UiTheme.Text, Font = new Font("Segoe UI Variable Text Semibold", 9.5f), Padding = new Padding(0, 4, 0, 4) });
        content.Controls.Add(_models); content.Controls.Add(_modelDetails); content.Controls.Add(_progress); content.Controls.Add(_progressText);
        page.Controls.Add(content); return page;
    }

    private async Task ContinueAsync()
    {
        if (_pages.SelectedIndex < 2) { MovePage(1); return; }
        if (_models.SelectedItem is not WizardModel choice) return;
        try
        {
            SetBusy(true);
            await ApplyPortAsync();
            if (!_downloads.IsInstalled(choice.Entry))
            {
                if (MessageBox.Show(this, $"Download {choice.Entry.DisplayName}?\n\nDownload size: {choice.Entry.SizeBytes / 1024d / 1024d / 1024d:0.00} GiB", "Install model", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) { SetBusy(false); return; }
                _downloadCancellation = new CancellationTokenSource();
                var progress = new Progress<DownloadProgress>(value => { _progress.Value = value.Percent; _progressText.Text = value.Description + (value.BytesPerSecond > 0 ? $" — {value.BytesPerSecond / 1024d / 1024d:0.0} MiB/s" : string.Empty); });
                await _downloads.DownloadAsync(choice.Entry, progress, _downloadCancellation.Token);
            }
            _settings.ActiveModelFile = choice.Entry.FileName; _settings.GetProfile(choice.Entry); _settings.FirstRunCompleted = true; _settingsStore.Save(_settings);
            DialogResult = DialogResult.OK; Close();
        }
        catch (OperationCanceledException) { _progressText.Text = "Download paused. You can resume it from Model Manager."; SetBusy(false); }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "Setup could not finish", MessageBoxButtons.OK, MessageBoxIcon.Warning); SetBusy(false); }
        finally { _downloadCancellation?.Dispose(); _downloadCancellation = null; }
    }

    private async Task ApplyPortAsync()
    {
        var requested = (int)_port.Value;
        if (requested != _proxy.Port)
        {
            if (!PortManagement.IsAvailable(requested)) throw new InvalidOperationException($"Port {requested} is already in use. Choose another port.");
            await _proxy.RestartAsync(requested);
        }
        _settings.PublicPort = requested; _settings.LockPublicPort = _portMode.SelectedIndex == 1; _settingsStore.Save(_settings);
    }

    private void CompleteWithoutModel()
    {
        _settings.FirstRunCompleted = true; _settingsStore.Save(_settings); DialogResult = DialogResult.Ignore; Close();
    }

    private void MovePage(int direction) { _pages.SelectedIndex = Math.Clamp(_pages.SelectedIndex + direction, 0, _pages.TabCount - 1); RefreshNavigation(); }
    private void RefreshNavigation() { _back.Enabled = _pages.SelectedIndex > 0; _next.Text = _pages.SelectedIndex == 2 ? "Install and finish" : "Continue"; }
    private void SetBusy(bool busy) { _back.Enabled = !busy && _pages.SelectedIndex > 0; _next.Enabled = !busy; _skip.Enabled = !busy; _models.Enabled = !busy; }
    private void RefreshModelDetails()
    {
        if (_models.SelectedItem is not WizardModel choice) return;
        _modelDetails.Text = $"{choice.Entry.Weights} • {choice.Entry.SizeBytes / 1024d / 1024d / 1024d:0.00} GiB • Vision {(choice.Entry.Vision ? "supported" : "not supported")} • Recommended context {choice.Entry.RecommendedContext:N0}";
    }

    private static TabPage Page() => new() { BackColor = UiTheme.Background };
    private static FlowLayoutPanel Content() => new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(54, 45, 54, 30), AutoScroll = true };
    private static Label Title(string text) => new() { Text = text, AutoSize = true, MaximumSize = new Size(620, 0), ForeColor = UiTheme.Text, Font = new Font("Segoe UI Variable Display Semibold", 22f), Margin = new Padding(0, 0, 0, 8) };
    private static Label Copy(string text) => UiTheme.Role(new Label { Text = text, AutoSize = true, MaximumSize = new Size(610, 0), Font = new Font("Segoe UI Variable Text", 10.5f), Margin = new Padding(0, 0, 0, 24) }, ThemeRole.MutedText);
    private static Label FieldLabel(string text) => new() { Text = text, AutoSize = true, ForeColor = UiTheme.Text, Font = new Font("Segoe UI Variable Text Semibold", 9.5f), Padding = new Padding(0, 8, 8, 8) };
    private static Button ActionButton(string text, bool primary = false) { var button = new ThemedButton { Text = text, AutoSize = true, MinimumSize = new Size(110, 40) }; UiTheme.StyleButton(button, primary); return button; }
    private sealed record WizardModel(ModelCatalogEntry Entry) { public string Name => Entry.DisplayName + (Entry.Vision ? "  •  Vision" : string.Empty); }
}
