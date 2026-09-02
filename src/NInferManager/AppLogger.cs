using System.Text;

namespace NInferManager;

internal sealed class AppLogger : IDisposable
{
    private const long MaxLogBytes = 5 * 1024 * 1024;
    private readonly object _sync = new();
    private readonly string _path;

    public AppLogger(string path)
    {
        _path = path;
        RotateIfNeeded();
    }

    public string FilePath => _path;
    public event Action<string>? LineWritten;

    public void Write(string message, Exception? exception = null)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
        if (exception is not null) line += $" | {exception.GetType().Name}: {exception.Message}";
        lock (_sync)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.AppendAllText(_path, line + Environment.NewLine, Encoding.UTF8);
        }
        LineWritten?.Invoke(line);
    }

    public string ReadTail(int maxLines = 250)
    {
        lock (_sync)
        {
            if (!File.Exists(_path)) return string.Empty;
            return string.Join(Environment.NewLine, File.ReadLines(_path).TakeLast(maxLines));
        }
    }

    private void RotateIfNeeded()
    {
        try
        {
            if (!File.Exists(_path) || new FileInfo(_path).Length <= MaxLogBytes) return;
            var previous = _path + ".previous";
            File.Move(_path, previous, true);
        }
        catch { }
    }

    public void Dispose() { }
}
