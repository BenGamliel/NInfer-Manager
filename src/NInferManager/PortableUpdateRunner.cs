using System.Diagnostics;
using System.IO.Compression;

namespace NInferManager;

internal static class PortableUpdateRunner
{
    public static bool TryRun(string[] args)
    {
        if (!args.Contains("--apply-portable-update", StringComparer.OrdinalIgnoreCase)) return false;
        try
        {
            var waitPid = int.Parse(Value(args, "--wait-pid"));
            var zipPath = Path.GetFullPath(Value(args, "--zip"));
            var target = Path.GetFullPath(Value(args, "--target")).TrimEnd(Path.DirectorySeparatorChar);
            var executable = Value(args, "--exe");
            if (!File.Exists(zipPath) || !File.Exists(Path.Combine(target, "portable.mode")))
                throw new InvalidOperationException("Portable update paths could not be validated.");
            try { Process.GetProcessById(waitPid).WaitForExit(60000); } catch (ArgumentException) { }
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var files = archive.Entries.Where(x => !string.IsNullOrEmpty(x.Name)).ToList();
                var rootPrefix = files.Select(x => x.FullName.Replace('\\', '/').Split('/')[0]).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1
                    ? files[0].FullName.Replace('\\', '/').Split('/')[0] + "/" : string.Empty;
                foreach (var entry in files)
                {
                    var relative = entry.FullName.Replace('\\', '/');
                    if (rootPrefix.Length > 0 && relative.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)) relative = relative[rootPrefix.Length..];
                    if (string.IsNullOrWhiteSpace(relative) || relative.StartsWith("Data/", StringComparison.OrdinalIgnoreCase) || relative.StartsWith("Models/", StringComparison.OrdinalIgnoreCase)) continue;
                    var destination = Path.GetFullPath(Path.Combine(target, relative.Replace('/', Path.DirectorySeparatorChar)));
                    if (!destination.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe path in update package.");
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    var temp = destination + ".updating";
                    entry.ExtractToFile(temp, true);
                    File.Move(temp, destination, true);
                }
            }
            File.Delete(zipPath);
            Process.Start(new ProcessStartInfo(Path.Combine(target, executable), "--minimized") { UseShellExecute = true, WorkingDirectory = target });
        }
        catch (Exception exception)
        {
            MessageBox.Show("The portable update could not be applied.\n\n" + exception.Message, "NInfer Manager Updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        return true;
    }

    private static string Value(string[] args, string name)
    {
        var index = Array.FindIndex(args, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length) throw new ArgumentException($"Missing updater argument {name}.");
        return args[index + 1];
    }
}
