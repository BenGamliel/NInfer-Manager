using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace NInferManager;

internal enum ThemePreference { Light, Dark }
internal enum ThemeRole { Background, Surface, SurfaceAlt, Sidebar, SidebarText, MutedText, AccentSoft, SuccessSoft, WarningSoft }
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

    public static void Initialize(ThemePreference preference) => _preference = preference;

    public static void SetTheme(ThemePreference preference, Form form)
    {
        _preference = preference;
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
        if (control.Tag is ThemeRole role) ApplyRole(control, role);
        else if (control.Tag is ButtonKind kind && control is Button button) ApplyButton(button, kind);
        else switch (control)
        {
            case Form form: ApplyWindow(form); break;
            case RoundedPanel panel: panel.RefreshTheme(); break;
            case MetricRing ring: ring.BackColor = Surface; ring.ForeColor = Text; break;
            case TabPage:
            case TableLayoutPanel:
            case FlowLayoutPanel:
            case SplitContainer:
                control.BackColor = Background; control.ForeColor = Text; break;
            case TabControl tabs: tabs.BackColor = Background; tabs.ForeColor = Text; tabs.Invalidate(); break;
            case PropertyGrid grid:
                grid.BackColor = Surface; grid.ViewBackColor = Surface; grid.ViewForeColor = Text; grid.CategoryForeColor = Text;
                grid.HelpBackColor = SurfaceAlt; grid.HelpForeColor = Text; grid.CommandsBackColor = Surface; grid.CommandsForeColor = Text; grid.LineColor = Border; break;
            case DataGridView grid: StyleGrid(grid); break;
            case TextBoxBase textBox: textBox.BackColor = Surface; textBox.ForeColor = Text; textBox.BorderStyle = BorderStyle.FixedSingle; break;
            case ComboBox combo: combo.BackColor = Surface; combo.ForeColor = Text; break;
            case NumericUpDown numeric: numeric.BackColor = Surface; numeric.ForeColor = Text; break;
            case CheckBox checkBox: checkBox.BackColor = Background; checkBox.ForeColor = Text; break;
            case GroupBox group: group.BackColor = Background; group.ForeColor = Text; break;
            case Label label: label.BackColor = Color.Transparent; label.ForeColor = Text; break;
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
        void Round(object? _, EventArgs __) => ApplyRoundedRegion(button, kind == ButtonKind.ActionTile ? 12 : 9);
        button.Resize += Round;
        button.HandleCreated += Round;
    }

    private static void ApplyButton(Button button, ButtonKind kind)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = kind is ButtonKind.Secondary or ButtonKind.Ghost or ButtonKind.ActionTile ? 1 : 0;
        button.FlatAppearance.BorderColor = kind == ButtonKind.Danger ? Danger : Border;
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
