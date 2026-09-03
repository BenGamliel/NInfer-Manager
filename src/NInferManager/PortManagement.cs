using System.Net;
using System.Net.Sockets;

namespace NInferManager;

internal sealed record PortStartupResult(int Port, bool ChangedAutomatically, int RequestedPort);

internal static class PortManagement
{
    public const int AutomaticRangeStart = 49152;
    public const int AutomaticRangeEnd = 65535;

    public static bool IsAvailable(int port)
    {
        if (port is < 1 or > 65535) return false;
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch (SocketException) { return false; }
        finally { listener?.Stop(); }
    }

    public static int FindAvailable(params int[] excluded)
    {
        var blocked = excluded.ToHashSet();
        var start = Random.Shared.Next(AutomaticRangeStart, AutomaticRangeEnd + 1);
        for (var offset = 0; offset <= AutomaticRangeEnd - AutomaticRangeStart; offset++)
        {
            var port = AutomaticRangeStart + ((start - AutomaticRangeStart + offset) % (AutomaticRangeEnd - AutomaticRangeStart + 1));
            if (!blocked.Contains(port) && IsAvailable(port)) return port;
        }
        throw new InvalidOperationException("No free local API port was found in the Windows dynamic port range.");
    }

    public static PortStartupResult Resolve(AppSettings settings)
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("NINFER_MANAGER_PUBLIC_PORT"), out var environmentPort) && environmentPort is > 0 and <= 65535)
        {
            if (!IsAvailable(environmentPort)) throw new InvalidOperationException($"The test API port {environmentPort} is already in use.");
            return new PortStartupResult(environmentPort, false, environmentPort);
        }
        if (IsAvailable(settings.PublicPort)) return new PortStartupResult(settings.PublicPort, false, settings.PublicPort);
        if (settings.LockPublicPort) return new PortStartupResult(0, false, settings.PublicPort);
        return new PortStartupResult(FindAvailable(settings.BackendPort), true, settings.PublicPort);
    }
}

internal sealed class PortConflictDialog : Form
{
    private readonly ThemedNumericField _port = new() { Minimum = 1024, Maximum = 65535, Width = 150 };
    public int SelectedPort => (int)_port.Value;

    public PortConflictDialog(int requestedPort, Icon icon)
    {
        Text = "API port unavailable"; Icon = icon; StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(510, 250); BackColor = UiTheme.Background; ForeColor = UiTheme.Text;
        var suggested = PortManagement.FindAvailable(requestedPort);
        _port.Value = suggested;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), RowCount = 5, ColumnCount = 1 };
        root.Controls.Add(new Label { Text = $"Port {requestedPort} is already in use", AutoSize = true, Font = new Font("Segoe UI Variable Display Semibold", 17f), ForeColor = UiTheme.Text }, 0, 0);
        root.Controls.Add(new Label { Text = "NInfer Manager is configured to keep this exact port, so it did not switch silently. Choose another free port to continue.", AutoSize = true, MaximumSize = new Size(445, 0), ForeColor = UiTheme.Muted, Padding = new Padding(0, 6, 0, 12) }, 0, 1);
        var input = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        input.Controls.Add(new Label { Text = "New port", AutoSize = true, Padding = new Padding(0, 8, 10, 0), Font = new Font("Segoe UI Variable Text Semibold", 9.5f) }); input.Controls.Add(_port);
        root.Controls.Add(input, 0, 2);
        var note = new Label { Text = $"Suggested from the Windows dynamic range: {suggested}", AutoSize = true, ForeColor = UiTheme.Muted, Padding = new Padding(0, 4, 0, 12) }; root.Controls.Add(note, 0, 3);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var use = new ThemedButton { Text = "Save port and continue", AutoSize = true, DialogResult = DialogResult.OK }; UiTheme.StyleButton(use, true);
        var exit = new ThemedButton { Text = "Exit", AutoSize = true, DialogResult = DialogResult.Cancel }; UiTheme.StyleButton(exit);
        actions.Controls.Add(use); actions.Controls.Add(exit); root.Controls.Add(actions, 0, 4);
        AcceptButton = use; CancelButton = exit; Controls.Add(root); UiTheme.ApplyWindow(this); UiTheme.ApplyTree(this);
        use.Click += (_, _) => { if (!PortManagement.IsAvailable(SelectedPort)) { MessageBox.Show(this, $"Port {SelectedPort} is also in use. Choose another port.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); DialogResult = DialogResult.None; } };
    }
}
