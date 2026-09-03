using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.VisualBasic.FileIO;

namespace NInferManager;

internal sealed record DownloadProgress(string Stage, long Completed, long Total, double BytesPerSecond)
{
    public int Percent => Total <= 0 ? 0 : (int)Math.Clamp(Completed * 100 / Total, 0, 100);
    public string Description => Total <= 0
        ? Stage
        : $"{Stage} — {Percent}% — {Completed / 1024d / 1024d / 1024d:0.00}/{Total / 1024d / 1024d / 1024d:0.00} GiB";
}

internal sealed class ModelDownloadService : IDisposable
{
    private readonly AppPaths _paths;
    private readonly AppLogger _logger;
    private readonly HttpClient _client;

    public ModelDownloadService(AppPaths paths, AppLogger logger)
    {
        _paths = paths;
        _logger = logger;
        _client = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.None })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("NInfer-Manager/1.0");
    }

    public string GetModelPath(ModelCatalogEntry entry) => Path.Combine(_paths.ModelsDirectory, entry.FileName);
    public bool IsInstalled(ModelCatalogEntry entry) => File.Exists(GetModelPath(entry)) && new FileInfo(GetModelPath(entry)).Length == entry.SizeBytes;

    public async Task DownloadAsync(ModelCatalogEntry entry, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
    {
        var finalPath = GetModelPath(entry);
        var partPath = finalPath + ".part";
        Directory.CreateDirectory(_paths.ModelsDirectory);
        if (File.Exists(finalPath))
        {
            if (await VerifyAsync(entry, progress, cancellationToken)) return;
            throw new InvalidOperationException("An existing model file failed verification. Delete or move it before downloading again.");
        }

        var offset = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;
        if (offset > entry.SizeBytes) { File.Delete(partPath); offset = 0; }
        using var request = new HttpRequestMessage(HttpMethod.Get, entry.DownloadUrl);
        if (offset > 0) request.Headers.Range = new RangeHeaderValue(offset, null);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (offset > 0 && response.StatusCode == HttpStatusCode.OK)
        {
            offset = 0;
            File.Delete(partPath);
        }
        response.EnsureSuccessStatusCode();
        var mode = offset > 0 ? FileMode.Append : FileMode.Create;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(partPath, mode, FileAccess.Write, FileShare.Read, 1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);
        var completed = offset;
        var lastReport = DateTime.UtcNow;
        var lastBytes = completed;
        try
        {
            while (true)
            {
                var read = await input.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                completed += read;
                var now = DateTime.UtcNow;
                if ((now - lastReport).TotalMilliseconds >= 500)
                {
                    var speed = (completed - lastBytes) / Math.Max(0.001, (now - lastReport).TotalSeconds);
                    progress.Report(new DownloadProgress("Downloading", completed, entry.SizeBytes, speed));
                    lastReport = now;
                    lastBytes = completed;
                }
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
        await output.FlushAsync(cancellationToken);
        if (completed != entry.SizeBytes) throw new InvalidDataException($"Downloaded size is {completed:N0} bytes; expected {entry.SizeBytes:N0}.");
        if (!await VerifyFileAsync(partPath, entry, progress, cancellationToken)) throw new InvalidDataException("SHA-256 verification failed. The partial file was kept for diagnosis.");
        File.Move(partPath, finalPath, true);
        progress.Report(new DownloadProgress("Installed and verified", entry.SizeBytes, entry.SizeBytes, 0));
        _logger.Write($"Model installed: {entry.FileName}");
    }

    public Task<bool> VerifyAsync(ModelCatalogEntry entry, IProgress<DownloadProgress> progress, CancellationToken cancellationToken) =>
        VerifyFileAsync(GetModelPath(entry), entry, progress, cancellationToken);

    private static async Task<bool> VerifyFileAsync(string path, ModelCatalogEntry entry, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != entry.SizeBytes) return false;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4 * 1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(4 * 1024 * 1024);
        long completed = 0;
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;
                hash.AppendData(buffer, 0, read);
                completed += read;
                progress.Report(new DownloadProgress("Verifying SHA-256", completed, entry.SizeBytes, 0));
            }
            return Convert.ToHexString(hash.GetHashAndReset()).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase);
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    public async Task ImportAsync(string sourcePath, ModelCatalogEntry entry, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
    {
        if (!await VerifyFileAsync(sourcePath, entry, progress, cancellationToken))
            throw new InvalidDataException("The selected file does not match the official size and SHA-256 for this model.");
        var target = GetModelPath(entry);
        var temp = target + ".importing";
        File.Copy(sourcePath, temp, true);
        File.Move(temp, target, true);
        var part = target + ".part";
        if (File.Exists(part)) File.Delete(part);
        _logger.Write($"Model imported: {entry.FileName}");
    }

    public void Delete(ModelCatalogEntry entry)
    {
        var path = GetModelPath(entry);
        if (File.Exists(path)) FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        var part = path + ".part";
        if (File.Exists(part)) File.Delete(part);
        _logger.Write($"Model moved to Recycle Bin: {entry.FileName}");
    }

    public void Dispose() => _client.Dispose();
}
