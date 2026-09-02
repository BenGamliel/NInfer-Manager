using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace NInferManager;

internal static class UiTheme
{
    public static bool Dark { get; } = ReadDarkMode();
    public static Color Background => Dark ? Color.FromArgb(18, 23, 31) : Color.FromArgb(245, 247, 250);
    public static Color Surface => Dark ? Color.FromArgb(28, 35, 46) : Color.White;
    public static Color SurfaceAlt => Dark ? Color.FromArgb(35, 43, 56) : Color.FromArgb(237, 242, 247);
    public static Color Text => Dark ? Color.FromArgb(238, 243, 248) : Color.FromArgb(25, 35, 48);
    public static Color Muted => Dark ? Color.FromArgb(166, 179, 195) : Color.FromArgb(91, 105, 122);
    public static Color Accent => Color.FromArgb(15, 154, 171);
    public static Color AccentHover => Color.FromArgb(9, 126, 143);
    public static Color AccentDark => Color.FromArgb(9, 112, 128);
    public static Color AccentSoft => Dark ? Color.FromArgb(25, 54, 64) : Color.FromArgb(229, 247, 249);
    public static Color Success => Color.FromArgb(31, 157, 105);
    public static Color Warning => Color.FromArgb(226, 153, 44);
    public static Color Danger => Color.FromArgb(205, 65, 78);
    public static Color Border => Dark ? Color.FromArgb(55, 66, 82) : Color.FromArgb(216, 223, 232);
    public static Color Sidebar => Dark ? Color.FromArgb(12, 17, 24) : Color.FromArgb(23, 31, 43);

    public static void ApplyWindow(Form form)
    {
        form.BackColor = Background;
        form.ForeColor = Text;
        if (Dark)
        {
            var enabled = 1;
            _ = DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int));
        }
    }

    public static void StyleButton(Button button, bool primary = false, bool danger = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.BorderColor = danger ? Danger : Border;
        button.BackColor = primary ? Accent : danger ? (Dark ? Color.FromArgb(80, 35, 42) : Color.FromArgb(255, 238, 240)) : SurfaceAlt;
        button.ForeColor = primary ? Color.White : danger ? Danger : Text;
        button.Cursor = Cursors.Hand;
        button.Padding = new Padding(12, 0, 12, 0);
        button.FlatAppearance.MouseOverBackColor = primary ? AccentHover : danger ? Color.FromArgb(255, 226, 229) : (Dark ? Color.FromArgb(48, 58, 72) : Color.FromArgb(225, 232, 240));
        button.Height = 40;
        button.Font = new Font("Segoe UI Variable Text Semibold", 9.25f);
        void Round(object? _, EventArgs __) => ApplyRoundedRegion(button, 9);
        button.Resize += Round; button.HandleCreated += Round;
    }

    public static Panel Card() => new RoundedPanel { BackColor = Surface, BorderColor = Border, Radius = 14, Padding = new Padding(20), Margin = new Padding(0, 0, 14, 16) };

    public static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = Border;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = SurfaceAlt, ForeColor = Text, Font = new Font("Segoe UI Semibold", 9f), Padding = new Padding(6) };
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Surface, ForeColor = Text, SelectionBackColor = Color.FromArgb(0, 115, 128), SelectionForeColor = Color.White, Padding = new Padding(6) };
        grid.RowTemplate.Height = 38;
        grid.ColumnHeadersHeight = 42;
    }

    private static bool ReadDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch { return false; }
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
        var diameter = radius * 2; var path = new GraphicsPath();
        var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90); arc.X = bounds.Right - diameter; path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter; path.AddArc(arc, 0, 90); arc.X = bounds.Left; path.AddArc(arc, 90, 90);
        path.CloseFigure(); return path;
    }
}

internal sealed class RoundedPanel : Panel
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = 14;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = UiTheme.Border;
    public RoundedPanel() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = UiTheme.RoundedPath(new Rectangle(1, 1, Width - 3, Height - 3), Radius);
        using var fill = new SolidBrush(BackColor); using var border = new Pen(BorderColor);
        e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(border, path);
    }
}
