using System.Text;
using CodexUsageMeter.Core;

namespace CodexUsageMeter.Infrastructure;

public sealed class CodexSessionUsageProvider : IUsageProvider
{
    private const int TailBytes = 1024 * 1024;
    private readonly string _sessionsDirectory;

    public CodexSessionUsageProvider(string? codexHome = null)
    {
        var home = codexHome ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        _sessionsDirectory = Path.Combine(home, "sessions");
    }

    public async Task<UsageResult> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_sessionsDirectory))
        {
            return UsageResult.Failure("No se encontró la carpeta de sesiones de Codex.");
        }

        FileInfo[] files;
        try
        {
            files = new DirectoryInfo(_sessionsDirectory)
                .EnumerateFiles("*.jsonl", SearchOption.AllDirectories)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(10)
                .ToArray();
        }
        catch (IOException ex)
        {
            return UsageResult.Failure($"No se pudieron enumerar las sesiones: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return UsageResult.Failure($"No se pudo acceder a las sesiones: {ex.Message}");
        }

        foreach (var file in files)
        {
            var snapshot = await ReadLatestFromFileAsync(file.FullName, cancellationToken);
            if (snapshot is not null)
            {
                return UsageResult.Success(snapshot);
            }
        }

        return UsageResult.Failure("Todavía no hay información de uso en las sesiones de Codex.");
    }

    private static async Task<UsageSnapshot?> ReadLatestFromFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                useAsync: true);

            var bytesToRead = (int)Math.Min(stream.Length, TailBytes);
            stream.Seek(-bytesToRead, SeekOrigin.End);
            var buffer = new byte[bytesToRead];
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            var text = Encoding.UTF8.GetString(buffer, 0, read);
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            for (var index = lines.Length - 1; index >= 0; index--)
            {
                if (!lines[index].Contains("\"rate_limits\"", StringComparison.Ordinal))
                {
                    continue;
                }

                var snapshot = CodexRateLimitParser.Parse(lines[index].TrimEnd('\r'));
                if (snapshot is not null)
                {
                    return snapshot;
                }
            }
        }
        catch (IOException)
        {
            // A session can be rotated while it is being read. Try the next one.
        }
        catch (UnauthorizedAccessException)
        {
            // Try another recent session before reporting that no data was found.
        }

        return null;
    }
}
