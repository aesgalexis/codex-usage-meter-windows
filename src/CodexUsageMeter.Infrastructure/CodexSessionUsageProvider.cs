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
            return UsageResult.Failure("Codex sessions directory was not found.", UsageFailureKind.SessionsMissing);
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
            return UsageResult.Failure($"Could not enumerate Codex sessions: {ex.Message}", UsageFailureKind.ReadError);
        }
        catch (UnauthorizedAccessException ex)
        {
            return UsageResult.Failure($"Could not access Codex sessions: {ex.Message}", UsageFailureKind.AccessDenied);
        }

        UsageSnapshot? latest = null;
        foreach (var file in files)
        {
            var snapshot = await ReadLatestFromFileAsync(file.FullName, cancellationToken);
            if (snapshot is not null && (latest is null || snapshot.ObservedAt > latest.ObservedAt))
            {
                latest = snapshot;
            }
        }

        if (latest is not null) return UsageResult.Success(latest);

        return UsageResult.Failure("No usage snapshots were found in Codex sessions.", UsageFailureKind.NoSnapshots);
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

            UsageSnapshot? latest = null;
            string? latestModel = null;
            var latestModelAt = DateTimeOffset.MinValue;
            for (var index = lines.Length - 1; index >= 0; index--)
            {
                var model = CodexRateLimitParser.ParseModel(lines[index].TrimEnd('\r'));
                if (model is { } modelObservation && (latestModel is null || modelObservation.ObservedAt > latestModelAt))
                {
                    latestModel = modelObservation.Model;
                    latestModelAt = modelObservation.ObservedAt;
                }
                if (!lines[index].Contains("\"rate_limits\"", StringComparison.Ordinal))
                {
                    continue;
                }

                var snapshot = CodexRateLimitParser.Parse(lines[index].TrimEnd('\r'));
                if (snapshot is not null && (latest is null || snapshot.ObservedAt > latest.ObservedAt))
                {
                    latest = snapshot;
                }
            }
            return latest is null ? null : latest with { ActiveModel = latestModel };
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
