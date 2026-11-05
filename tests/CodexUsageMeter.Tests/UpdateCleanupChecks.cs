using System.IO;
using CodexUsageMeter.App;
using CodexUsageMeter.Infrastructure;

internal static class UpdateCleanupChecks
{
    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "CodexUsageMeter-cleanup-tests-" + Guid.NewGuid().ToString("N"));
        var now = DateTimeOffset.UtcNow;
        Directory.CreateDirectory(root);
        try
        {
            var old = CreateDownload(root, now.AddDays(-2));
            var recent = CreateDownload(root, now.AddHours(-1));
            var locked = CreateDownload(root, now.AddDays(-2));
            var mixed = CreateDownload(root, now.AddDays(-2));
            File.WriteAllText(Path.Combine(mixed, "keep.txt"), "unrelated content");
            Directory.SetLastWriteTimeUtc(mixed, now.AddDays(-2).UtcDateTime);
            var unexpected = Path.Combine(root, "unrelated-directory");
            Directory.CreateDirectory(unexpected);
            File.WriteAllText(Path.Combine(unexpected, AppUpdateService.InstallerName), "unrelated installer");
            File.SetLastWriteTimeUtc(Path.Combine(unexpected, AppUpdateService.InstallerName), now.AddDays(-2).UtcDateTime);
            Directory.SetLastWriteTimeUtc(unexpected, now.AddDays(-2).UtcDateTime);
            var nested = Path.Combine(root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(nested, "keep"));
            Directory.SetLastWriteTimeUtc(nested, now.AddDays(-2).UtcDateTime);
            var empty = Path.Combine(root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(empty);
            Directory.SetLastWriteTimeUtc(empty, now.AddDays(-2).UtcDateTime);

            using (var file = new FileStream(Path.Combine(locked, AppUpdateService.InstallerName),
                       FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var removed = UpdateDownloadCleanup.RemoveStaleDownloads(root, now);
                Require(removed == 2 && !Directory.Exists(old) && !Directory.Exists(empty),
                    "Debe limpiar las descargas antiguas y sus carpetas vacías.");
                Require(Directory.Exists(recent), "Debe conservar las descargas recientes.");
                Require(Directory.Exists(locked), "Debe conservar un instalador bloqueado o en ejecución.");
                Require(File.Exists(Path.Combine(mixed, AppUpdateService.InstallerName)) &&
                        File.Exists(Path.Combine(mixed, "keep.txt")), "No debe tocar carpetas con archivos ajenos.");
                Require(Directory.Exists(unexpected) && Directory.Exists(Path.Combine(nested, "keep")),
                    "No debe borrar carpetas ajenas ni recorrer subdirectorios.");
            }

            Require(UpdateDownloadCleanup.RemoveStaleDownloads(root, now) == 1 && !Directory.Exists(locked),
                "Debe poder limpiar una descarga antigua cuando deja de estar bloqueada.");
            Require(UpdateDownloadCleanup.RemoveStaleDownloads(Path.Combine(root, "missing"), now) == 0,
                "Una carpeta de descargas ausente no debe impedir el arranque.");
            CheckReparsePoint(root, now);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void CheckReparsePoint(string root, DateTimeOffset now)
    {
        var targetRoot = Path.Combine(root, "outside");
        Directory.CreateDirectory(targetRoot);
        var target = CreateDownload(targetRoot, now.AddDays(-2));
        var linkRoot = Path.Combine(root, "links");
        Directory.CreateDirectory(linkRoot);
        var link = Path.Combine(linkRoot, Guid.NewGuid().ToString("N"));
        var rootLink = Path.Combine(root, "root-link");
        try
        {
            Directory.CreateSymbolicLink(link, target);
            Directory.CreateSymbolicLink(rootLink, targetRoot);
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine("Comprobación de enlace simbólico omitida: el usuario no tiene permiso para crearlo.");
            return;
        }
        catch (IOException exception) when ((exception.HResult & 0xffff) == 1314)
        {
            Console.WriteLine("Comprobación de enlace simbólico omitida: Windows requiere privilegios para crearlo.");
            return;
        }

        try
        {
            // Make even the newly created link old enough to qualify without the reparse guard.
            var future = now.AddDays(3);
            Require(UpdateDownloadCleanup.RemoveStaleDownloads(linkRoot, future) == 0 &&
                    UpdateDownloadCleanup.RemoveStaleDownloads(rootLink, future) == 0 &&
                    File.Exists(Path.Combine(target, AppUpdateService.InstallerName)),
                "No debe seguir enlaces simbólicos ni borrar el instalador al que apuntan.");
        }
        finally
        {
            Directory.Delete(link);
            Directory.Delete(rootLink);
        }
    }

    private static string CreateDownload(string root, DateTimeOffset writtenAt)
    {
        var directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var installer = Path.Combine(directory, AppUpdateService.InstallerName);
        File.WriteAllText(installer, "synthetic installer");
        File.SetLastWriteTimeUtc(installer, writtenAt.UtcDateTime);
        Directory.SetLastWriteTimeUtc(directory, writtenAt.UtcDateTime);
        return directory;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
