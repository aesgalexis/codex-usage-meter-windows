using System.IO;
using CodexUsageMeter.Infrastructure;

namespace CodexUsageMeter.App;

internal static class UpdateDownloadCleanup
{
    // Leave a newly launched installer alone, even if the app restarts immediately.
    private static readonly TimeSpan MinimumAge = TimeSpan.FromDays(1);

    internal static int RemoveStaleDownloads(string updateDirectory, DateTimeOffset now)
    {
        try
        {
            var root = new DirectoryInfo(Path.GetFullPath(updateDirectory));
            if (!root.Exists || (root.Attributes & FileAttributes.ReparsePoint) != 0) return 0;

            var cutoff = (now - MinimumAge).UtcDateTime;
            var removed = 0;
            foreach (var directory in root.EnumerateDirectories())
            {
                try
                {
                    // Only the app's own direct GUID directories are eligible. Never recurse.
                    if (!Guid.TryParseExact(directory.Name, "N", out _) ||
                        !string.Equals(directory.Parent?.FullName, root.FullName, StringComparison.OrdinalIgnoreCase) ||
                        (directory.Attributes & FileAttributes.ReparsePoint) != 0 ||
                        directory.LastWriteTimeUtc > cutoff) continue;

                    var entries = directory.EnumerateFileSystemInfos().Take(2).ToArray();
                    if (entries.Length > 1) continue;
                    if (entries.Length == 1)
                    {
                        var installer = entries[0];
                        if (installer is not FileInfo ||
                            installer.Name != AppUpdateService.InstallerName ||
                            (installer.Attributes & FileAttributes.ReparsePoint) != 0 ||
                            installer.LastWriteTimeUtc > cutoff) continue;

                        // A running or otherwise locked installer cannot be opened exclusively.
                        // DeleteOnClose keeps the eligibility check and deletion on the same file.
                        using var file = new FileStream(installer.FullName, FileMode.Open,
                            FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
                    }

                    Directory.Delete(directory.FullName);
                    removed++;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            return removed;
        }
        catch (IOException) { return 0; }
        catch (UnauthorizedAccessException) { return 0; }
    }
}
