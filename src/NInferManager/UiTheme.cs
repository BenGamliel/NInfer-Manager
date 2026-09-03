using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;

namespace NInferManager;

internal enum ThemePreference { Light, Dark }
internal enum ThemeRole { Background, Surface, SurfaceAlt, Sidebar, SidebarText, MutedText, AccentSoft, SuccessSoft, SuccessText, WarningSoft }
internal enum ButtonKind { Secondary, Primary, Danger, Ghost, Navigation, ActionTile }

internal static class UiTheme
{
    private static ThemePreference _preference = ThemePreference.Light;
    public static ThemePreference Preference => _preference;
    public static bool Dark => _preference == ThemePreference.Dark;
    public static Color Background => Dark ? Color.FromArgb(23, 24, 22) : Color.FromArgb(255, 253, 247);
    public static Color Surface => Dark ? Color.FromArgb(34, 35, 31) : Color.FromArgb(255, 255, 252);
    public static Color SurfaceAlt => Dark ? Color.FromArgb(43, 44, 38) : Color.FromArgb(255, 249, 223);
    public static Color Text => Dark ? Color.FromArgb(248, 241, 219) : Color.FromArgb(39, 39, 34);
    public static Color Muted => Dark ? Color.FromArgb(190, 181, 157) : Color.FromArgb(104, 101, 90);
    public static Color Accent => Dark ? Color.FromArgb(225, 184, 75) : Color.FromArgb(232, 185, 49);
    public static Color AccentHover => Dark ? Color.FromArgb(241, 202, 91) : Color.FromArgb(207, 157, 20);
    public static Color AccentDark => Dark ? Color.FromArgb(97, 79, 31) : Color.FromArgb(151, 105, 12);
    public static Color AccentSoft => Dark ? Color.FromArgb(55, 50, 31) : Color.FromArgb(255, 248, 218);
    public static Color Teal => Color.FromArgb(12, 151, 165);
    public static Color Success => Dark ? Color.FromArgb(82, 190, 103) : Color.FromArgb(37, 153, 72);
    public static Color SuccessSoft => Dark ? Color.FromArgb(35, 58, 36) : Color.FromArgb(232, 247, 232);
    public static Color Warning => Color.FromArgb(213, 145, 33);
    public static Color Danger => Dark ? Color.FromArgb(238, 112, 116) : Color.FromArgb(194, 55, 65);
    public static Color DangerSoft => Dark ? Color.FromArgb(70, 36, 37) : Color.FromArgb(255, 237, 237);
    public static Color Border => Dark ? Color.FromArgb(77, 73, 58) : Color.FromArgb(229, 224, 207);
    public static Color Sidebar => Dark ? Color.FromArgb(27, 28, 26) : Color.FromArgb(255, 254, 249);
    public static Color SidebarText => Dark ? Color.FromArgb(226, 217, 192) : Color.FromArgb(61, 59, 51);
    public static Color RingTrack => Dark ? Color.FromArgb(58, 57, 48) : Color.FromArgb(239, 236, 226);

    public static void Initialize(ThemePreference preference) { _preference = preference; ApplyApplicationMode(); }

    public static void SetTheme(ThemePreference preference, Form form)
    {
        _preference = preference;
        ApplyApplicationMode();
        ApplyWindow(form);
        ApplyTree(form);
        form.Invalidate(true);
    }

    public static T Role<T>(T control, ThemeRole role) where T : Control
    {
        control.Tag = role;
        ApplyRole(control, role);
        return control;
    }

    public static void ApplyWindow(Form form)
    {
        form.BackColor = Background;
        form.ForeColor = Text;
        var enabled = Dark ? 1 : 0;
        _ = DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int));
    }

    public static void ApplyTree(Control root)
    {
        ApplyControl(root);
        foreach (Control child in root.Controls) ApplyTree(child);
        if (root is SplitContainer split)
        {
            ApplyControl(split.Panel1);
            ApplyControl(split.Panel2);
        }
    }

    private static void ApplyControl(Control control)
    {
        ApplyNativeTheme(control);
        if (control.Tag is ThemeRole role) ApplyRole(control, role);
        else if (control.Tag is ButtonKind kind && control is Button button) ApplyButton(button, kind);
        else switch (control)
        {
            case Form form: ApplyWindow(form); break;
            case RoundedPanel panel: panel.RefreshTheme(); ApplyNativeTheme(panel); break;
            case ThemedNumericField numericField: numericField.RefreshTheme(); break;
            case MetricRing ring: ring.BackColor = Surface; ring.ForeColor = Text; break;
            case TabPage:
            case TableLayoutPanel:
            case FlowLayoutPanel:
            case SplitContainer:
            case Panel:
                control.BackColor = control.Parent?.BackColor ?? Background; control.ForeColor = Text; break;
            case TabControl tabs: tabs.BackColor = Background; tabs.ForeColor = Text; tabs.Invalidate(); break;
            case PropertyGrid grid:
                grid.BackColor = Surface; grid.ViewBackColor = Surface; grid.ViewForeColor = Text; grid.CategoryForeColor = Text;
                grid.HelpBackColor = SurfaceAlt; grid.HelpForeColor = Text; grid.CommandsBackColor = Surface; grid.CommandsForeColor = Text; grid.LineColor = Border; break;
            case DataGridView grid: StyleGrid(grid); break;
            case TextBoxBase textBox: textBox.BackColor = Surface; textBox.ForeColor = Text; textBox.BorderStyle = BorderStyle.FixedSingle; ApplyNativeTheme(textBox); break;
            case ComboBox combo: StyleComboBox(combo); break;
            case NumericUpDown numeric: numeric.BackColor = Surface; numeric.ForeColor = Text; numeric.BorderStyle = BorderStyle.FixedSingle; ApplyNativeTheme(numeric); break;
            case CheckBox checkBox: checkBox.BackColor = checkBox.Parent?.BackColor ?? Background; checkBox.ForeColor = Text; ApplyNativeTheme(checkBox); break;
            case GroupBox group: group.BackColor = group.Parent?.BackColor ?? Background; group.ForeColor = Text; break;
            case Label label: label.BackColor = Color.Transparent; label.ForeColor = Text; break;
            case ToolStrip strip: StyleToolStrip(strip); break;
            case ProgressBar progress: progress.BackColor = RingTrack; progress.ForeColor = Accent; ApplyNativeTheme(progress); break;
        }
    }

    private static void ApplyRole(Control control, ThemeRole role)
    {
        (control.BackColor, control.ForeColor) = role switch
        {
            ThemeRole.Surface => (Surface, Text),
            ThemeRole.SurfaceAlt => (SurfaceAlt, Text),
            ThemeRole.Sidebar => (Sidebar, SidebarText),
            ThemeRole.SidebarText => (Color.Transparent, SidebarText),
            ThemeRole.MutedText => (Color.Transparent, Muted),
            ThemeRole.AccentSoft => (AccentSoft, Text),
            ThemeRole.SuccessSoft => (SuccessSoft, Success),
            ThemeRole.SuccessText => (Color.Transparent, Success),
            ThemeRole.WarningSoft => (AccentSoft, Dark ? Accent : AccentDark),
            _ => (Background, Text),
        };
    }

    public static void StyleButton(Button button, bool primary = false, bool danger = false)
        => StyleButton(button, danger ? ButtonKind.Danger : primary ? ButtonKind.Primary : ButtonKind.Secondary);

    public static void StyleButton(Button button, ButtonKind kind)
    {
        button.Tag = kind;
        ApplyButton(button, kind);
        button.Cursor = Cursors.Hand;
        button.Padding = kind == ButtonKind.Navigation ? new Padding(15, 0, 8, 0) : new Padding(12, 0, 12, 0);
        button.Height = kind == ButtonKind.Navigation ? 48 : kind == ButtonKind.ActionTile ? 72 : 40;
        button.Font = new Font("Segoe UI Variable Text Semibold", kind == ButtonKind.ActionTile ? 9.5f : 9.25f);
        button.UseVisualStyleBackColor = false;
        button.Region?.Dispose();
        button.Region = null;
    }

    private static void ApplyButton(Button button, ButtonKind kind)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = kind switch { ButtonKind.Primary => AccentDark, ButtonKind.Danger => Danger, ButtonKind.Navigation => Sidebar, _ => Border };
        button.TextAlign = kind == ButtonKind.Navigation ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleCenter;
        button.BackColor = kind switch
        {
            ButtonKind.Primary => Accent,
            ButtonKind.Danger => DangerSoft,
            ButtonKind.Ghost => Background,
            ButtonKind.Navigation => Sidebar,
            _ => Surface,
        };
        button.ForeColor = kind switch
        {
            ButtonKind.Primary => Color.FromArgb(38, 34, 22),
            ButtonKind.Danger => Danger,
            ButtonKind.Navigation => SidebarText,
            _ => Text,
        };
        button.FlatAppearance.MouseOverBackColor = kind switch
        {
            ButtonKind.Primary => AccentHover,
            ButtonKind.Danger => Dark ? Color.FromArgb(84, 42, 43) : Color.FromArgb(255, 226, 226),
            ButtonKind.Navigation => AccentSoft,
            _ => SurfaceAlt,
        };
    }

    public static RoundedPanel Card(ThemeRole role = ThemeRole.Surface) => new() { ThemeRole = role, Radius = 13, Padding = new Padding(18), Margin = new Padding(0, 0, 14, 14) };

    public static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Surface; grid.BorderStyle = BorderStyle.None; grid.GridColor = Border; grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None; grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = SurfaceAlt, ForeColor = Text, SelectionBackColor = SurfaceAlt, SelectionForeColor = Text, Font = new Font("Segoe UI Variable Text Semibold", 9f), Padding = new Padding(8) };
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Surface, ForeColor = Text, SelectionBackColor = AccentSoft, SelectionForeColor = Text, Padding = new Padding(8) };
        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Dark ? Color.FromArgb(38, 39, 35) : Color.FromArgb(255, 254, 249), ForeColor = Text, SelectionBackColor = AccentSoft, SelectionForeColor = Text };
        grid.RowTemplate.Height = 42; grid.ColumnHeadersHeight = 44;
        ApplyNativeTheme(grid);
    }

    public static void StyleMenu(ContextMenuStrip menu)
    {
        menu.RenderMode = ToolStripRenderMode.Professional;
        menu.Renderer = new ToolStripProfessionalRenderer(new WarmColorTable());
        menu.BackColor = Surface; menu.ForeColor = Text; menu.Font = new Font("Segoe UI Variable Text", 9.25f);
        StyleMenuItems(menu.Items);
    }

    private static void StyleMenuItems(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            item.BackColor = Surface; item.ForeColor = Text;
            if (item is ToolStripMenuItem menuItem)
            {
                menuItem.DropDown.BackColor = Surface; menuItem.DropDown.ForeColor = Text;
                menuItem.DropDown.Renderer = new ToolStripProfessionalRenderer(new WarmColorTable());
                StyleMenuItems(menuItem.DropDownItems);
            }
        }
    }

    private static void StyleToolStrip(ToolStrip strip)
    {
        strip.BackColor = Surface; strip.ForeColor = Text;
        strip.RenderMode = ToolStripRenderMode.Professional;
        strip.Renderer = new ToolStripProfessionalRenderer(new WarmColorTable());
        foreach (ToolStripItem item in strip.Items) { item.BackColor = Surface; item.ForeColor = Text; }
        ApplyNativeTheme(strip);
    }

    private static void StyleComboBox(ComboBox combo)
    {
        combo.BackColor = Surface; combo.ForeColor = Text; combo.FlatStyle = FlatStyle.Flat;
        combo.DrawMode = DrawMode.OwnerDrawFixed; combo.ItemHeight = 24;
        combo.DrawItem -= DrawComboItem; combo.DrawItem += DrawComboItem;
        ApplyNativeTheme(combo);
    }

    private static void DrawComboItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo || e.Index < 0) return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var background = new SolidBrush(selected ? AccentSoft : Surface);
        using var foreground = new SolidBrush(Text);
        e.Graphics.FillRectangle(background, e.Bounds);
        var text = combo.GetItemText(combo.Items[e.Index]);
        TextRenderer.DrawText(e.Graphics, text, combo.Font, e.Bounds, foreground.Color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        e.DrawFocusRectangle();
    }

    public static void StyleTabs(TabControl tabs)
    {
        tabs.DrawMode = TabDrawMode.OwnerDrawFixed; tabs.SizeMode = TabSizeMode.Fixed; tabs.ItemSize = new Size(130, 36);
        tabs.DrawItem += (_, e) =>
        {
            var selected = e.Index == tabs.SelectedIndex;
            using var background = new SolidBrush(selected ? AccentSoft : Background);
            using var foreground = new SolidBrush(selected ? Text : Muted);
            e.Graphics.FillRectangle(background, e.Bounds);
            using var font = new Font("Segoe UI Variable Text Semibold", 9.25f);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(tabs.TabPages[e.Index].Text, font, foreground, e.Bounds, format);
        };
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);

    [DllImport("uxtheme.dll", EntryPoint = "#135")]
    private static extern int SetPreferredAppMode(int preferredAppMode);

    [DllImport("uxtheme.dll", EntryPoint = "#104")]
    private static extern void RefreshImmersiveColorPolicyState();

    private static void ApplyApplicationMode()
    {
        try { _ = SetPreferredAppMode(Dark ? 2 : 3); RefreshImmersiveColorPolicyState(); }
        catch (EntryPointNotFoundException) { }
    }

    internal static void ApplyNativeTheme(Control control)
    {
        if (!control.IsHandleCreated) control.CreateControl();
        _ = SetWindowTheme(control.Handle, Dark ? "DarkMode_Explorer" : "Explorer", null);
    }

    private static void ApplyRoundedRegion(Control control, int radius)
    {
        if (control.Width <= 0 || control.Height <= 0) return;
        using var path = RoundedPath(new Rectangle(0, 0, control.Width, control.Height), radius);
        control.Region?.Dispose(); control.Region = new Region(path);
    }

    internal static GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var diameter = Math.Max(2, radius * 2); var path = new GraphicsPath(); var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90); arc.X = bounds.Right - diameter; path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter; path.AddArc(arc, 0, 90); arc.X = bounds.Left; path.AddArc(arc, 90, 90); path.CloseFigure(); return path;
    }
}

internal sealed class ThemedButton : Button
{
    private bool _hovered;
    private bool _pressed;

    public ThemedButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var kind = Tag is ButtonKind value ? value : ButtonKind.Secondary;
        var background = Enabled ? BackColor : (UiTheme.Dark ? Color.FromArgb(48, 48, 43) : Color.FromArgb(242, 239, 229));
        if (Enabled && _hovered) background = FlatAppearance.MouseOverBackColor;
        if (Enabled && _pressed) background = ControlPaint.Dark(background, .06f);
        var bounds = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
        using var path = UiTheme.RoundedPath(bounds, kind == ButtonKind.ActionTile ? 11 : 8);
        using var fill = new SolidBrush(background);
        using var border = new Pen(Enabled ? FlatAppearance.BorderColor : UiTheme.Border, 1f);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
        var textBounds = Rectangle.Inflate(bounds, -Math.Max(7, Padding.Left / 2), -3);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        flags |= kind == ButtonKind.Navigation ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter;
        if (Text.Contains('\n')) flags |= TextFormatFlags.WordBreak;
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, Enabled ? ForeColor : UiTheme.Muted, flags);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(bounds, -4, -4), ForeColor, background);
    }
}

internal sealed class ThemedProgressBar : ProgressBar
{
    public ThemedProgressBar() => SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using var trackPath = UiTheme.RoundedPath(bounds, Math.Max(2, Math.Min(5, Height / 2)));
        using var track = new SolidBrush(UiTheme.RingTrack); e.Graphics.FillPath(track, trackPath);
        if (Maximum <= Minimum || Value <= Minimum) return;
        var ratio = (Value - Minimum) / (double)(Maximum - Minimum);
        var fillBounds = new Rectangle(0, 0, Math.Max(2, (int)Math.Round(bounds.Width * ratio)), bounds.Height);
        using var fillPath = UiTheme.RoundedPath(fillBounds, Math.Max(2, Math.Min(5, Height / 2)));
        using var fill = new SolidBrush(UiTheme.Accent); e.Graphics.FillPath(fill, fillPath);
    }
}

internal sealed class ThemedComboBox : ComboBox
{
    private const int WmPaint = 0x000F;
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg != WmPaint || Width < 24 || !IsHandleCreated) return;
        using var graphics = Graphics.FromHwnd(Handle);
        var button = new Rectangle(Width - 23, 1, 22, Math.Max(1, Height - 2));
        using var fill = new SolidBrush(UiTheme.Surface); graphics.FillRectangle(fill, button);
        using var divider = new Pen(UiTheme.Border); graphics.DrawLine(divider, button.Left, button.Top, button.Left, button.Bottom);
        using var arrow = new Pen(UiTheme.Muted, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var centerX = button.Left + button.Width / 2; var centerY = button.Top + button.Height / 2;
        graphics.DrawLine(arrow, centerX - 3, centerY - 1, centerX, centerY + 2);
        graphics.DrawLine(arrow, centerX, centerY + 2, centerX + 3, centerY - 1);
    }
}

internal sealed class ThemedNumericField : UserControl
{
    private readonly TextBox _editor = new() { BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Left };
    private readonly SpinButtons _spin = new() { Dock = DockStyle.Right, Width = 20 };
    private decimal _minimum;
    private decimal _maximum = decimal.MaxValue;
    private decimal _increment = 1;
    private decimal _value;
    private int _decimalPlaces;
    private bool _thousandsSeparator;

    public ThemedNumericField()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 25; MinimumSize = new Size(70, 25); Padding = new Padding(5, 5, 1, 2);
        Controls.Add(_editor); Controls.Add(_spin);
        _spin.StepRequested += direction => Value += direction * Increment;
        _editor.Leave += (_, _) => CommitText();
        _editor.KeyDown += (_, e) => { if (e.KeyCode == Keys.Up) { Value += Increment; e.SuppressKeyPress = true; } else if (e.KeyCode == Keys.Down) { Value -= Increment; e.SuppressKeyPress = true; } else if (e.KeyCode == Keys.Enter) { CommitText(); e.SuppressKeyPress = true; } };
        MouseWheel += (_, e) => Value += Math.Sign(e.Delta) * Increment;
        RefreshTheme(); UpdateText();
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] public decimal Minimum { get => _minimum; set { _minimum = value; Value = _value; } }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] public decimal Maximum { get => _maximum; set { _maximum = value; Value = _value; } }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] public decimal Increment { get => _increment; set => _increment = value <= 0 ? 1 : value; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] public decimal Value { get => _value; set { _value = Math.Clamp(value, Minimum, Maximum); UpdateText(); } }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] public int DecimalPlaces { get => _decimalPlaces; set { _decimalPlaces = Math.Clamp(value, 0, 8); UpdateText(); } }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] public bool ThousandsSeparator { get => _thousandsSeparator; set { _thousandsSeparator = value; UpdateText(); } }

    public void RefreshTheme()
    {
        BackColor = UiTheme.Surface; ForeColor = UiTheme.Text;
        _editor.BackColor = UiTheme.Surface; _editor.ForeColor = UiTheme.Text; _spin.RefreshTheme(); Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); using var border = new Pen(UiTheme.Border); e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }

    private void CommitText()
    {
        if (decimal.TryParse(_editor.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed)) Value = parsed;
        else UpdateText();
    }

    private void UpdateText()
    {
        if (_editor.IsDisposed) return;
        var format = ThousandsSeparator ? $"N{DecimalPlaces}" : $"F{DecimalPlaces}";
        _editor.Text = Value.ToString(format, CultureInfo.CurrentCulture);
    }

    private sealed class SpinButtons : Control
    {
        public event Action<int>? StepRequested;
        public SpinButtons() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); Cursor = Cursors.Hand; }
        public void RefreshTheme() { BackColor = UiTheme.Surface; ForeColor = UiTheme.Muted; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); StepRequested?.Invoke(e.Y < Height / 2 ? 1 : -1); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(UiTheme.Surface); using var divider = new Pen(UiTheme.Border); e.Graphics.DrawLine(divider, 0, 0, 0, Height);
            using var arrow = new Pen(UiTheme.Muted, 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            var centerX = Width / 2; var upperY = Height / 4; var lowerY = Height * 3 / 4;
            e.Graphics.DrawLine(arrow, centerX - 3, upperY + 1, centerX, upperY - 2); e.Graphics.DrawLine(arrow, centerX, upperY - 2, centerX + 3, upperY + 1);
            e.Graphics.DrawLine(arrow, centerX - 3, lowerY - 1, centerX, lowerY + 2); e.Graphics.DrawLine(arrow, centerX, lowerY + 2, centerX + 3, lowerY - 1);
        }
    }
}

internal sealed class WarmColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => UiTheme.Surface;
    public override Color ImageMarginGradientBegin => UiTheme.Surface;
    public override Color ImageMarginGradientMiddle => UiTheme.Surface;
    public override Color ImageMarginGradientEnd => UiTheme.Surface;
    public override Color MenuItemSelected => UiTheme.AccentSoft;
    public override Color MenuItemBorder => UiTheme.Border;
    public override Color MenuBorder => UiTheme.Border;
    public override Color SeparatorDark => UiTheme.Border;
    public override Color SeparatorLight => UiTheme.Border;
}

internal sealed class RoundedPanel : Panel
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] public int Radius { get; set; } = 13;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] public ThemeRole ThemeRole { get; set; } = ThemeRole.Surface;
    public RoundedPanel() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true); RefreshTheme(); }
    public void RefreshTheme()
    {
        BackColor = ThemeRole switch { ThemeRole.AccentSoft => UiTheme.AccentSoft, ThemeRole.SurfaceAlt => UiTheme.SurfaceAlt, ThemeRole.SuccessSoft => UiTheme.SuccessSoft, _ => UiTheme.Surface };
        ForeColor = UiTheme.Text; Invalidate();
    }
    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Background);
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
        using var path = UiTheme.RoundedPath(bounds, Radius); using var fill = new SolidBrush(BackColor); using var border = new Pen(UiTheme.Border);
        e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(border, path); base.OnPaint(e);
    }
}

internal sealed class MetricRing : Control
{
    private int _percentage;
    private string _valueText = "—", _detailText = string.Empty, _title = string.Empty;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Percentage { get => _percentage; set { _percentage = Math.Clamp(value, 0, 100); Invalidate(); } }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string ValueText { get => _valueText; set { _valueText = value; Invalidate(); } }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string DetailText { get => _detailText; set { _detailText = value; Invalidate(); } }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string TitleText { get => _title; set { _title = value; AccessibleName = value; Invalidate(); } }
    public MetricRing() { DoubleBuffered = true; MinimumSize = new Size(160, 190); BackColor = UiTheme.Surface; ForeColor = UiTheme.Text; AccessibleRole = AccessibleRole.Graphic; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var titleFont = new Font("Segoe UI Variable Text Semibold", 10f); using var valueFont = new Font("Segoe UI Variable Display Semibold", 22f); using var detailFont = new Font("Segoe UI Variable Text", 9.25f);
        using var titleBrush = new SolidBrush(UiTheme.Text); using var mutedBrush = new SolidBrush(UiTheme.Muted);
        using var track = new Pen(UiTheme.RingTrack, 13) { StartCap = LineCap.Round, EndCap = LineCap.Round }; using var progress = new Pen(UiTheme.Accent, 13) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawString(TitleText, titleFont, titleBrush, new RectangleF(8, 2, Width - 16, 28), CenterFormat());
        var size = Math.Min(Width - 42, Height - 72); var ring = new Rectangle((Width - size) / 2, 34, size, size);
        e.Graphics.DrawArc(track, ring, -90, 359.9f); if (Percentage > 0) e.Graphics.DrawArc(progress, ring, -90, Math.Max(2, Percentage * 3.6f));
        e.Graphics.DrawString(ValueText, valueFont, titleBrush, new RectangleF(ring.X, ring.Y + ring.Height / 2f - 30, ring.Width, 45), CenterFormat());
        e.Graphics.DrawString(DetailText, detailFont, mutedBrush, new RectangleF(ring.X, ring.Y + ring.Height / 2f + 13, ring.Width, 28), CenterFormat());
    }
    private static StringFormat CenterFormat() => new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
}
