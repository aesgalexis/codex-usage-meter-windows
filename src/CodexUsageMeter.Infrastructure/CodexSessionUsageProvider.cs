using System.Text;
using CodexUsageMeter.Core;

namespace CodexUsageMeter.Infrastructure;

public sealed class CodexSessionUsageProvider : IUsageProvider
{
    private const int TailBytes = 1024 * 1024;
    private const int RecentFileCount = 10;
    private const int TrackedFileCount = 64;
    private readonly string _sessionsDirectory;
    private readonly object _fileTrackingLock = new();
    private readonly Dictionary<string, long> _knownFileLengths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _trackedFileActivity = new(StringComparer.OrdinalIgnoreCase);
    private long _fileActivitySequence;

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
            var observedFiles = new List<(FileInfo File, long Length)>();
            foreach (var file in new DirectoryInfo(_sessionsDirectory)
                         .EnumerateFiles("*.jsonl", SearchOption.AllDirectories))
            {
                try
                {
                    observedFiles.Add((file, file.Length));
                }
                catch (IOException)
                {
                    // A session can disappear during enumeration. Ignore it for this pass.
                }
                catch (UnauthorizedAccessException)
                {
                    // Other readable sessions can still provide a current snapshot.
                }
            }

            files = SelectCandidateFiles(observedFiles);
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

    private FileInfo[] SelectCandidateFiles(IReadOnlyList<(FileInfo File, long Length)> observedFiles)
    {
        lock (_fileTrackingLock)
        {
            var firstPass = _knownFileLengths.Count == 0;
            var currentPaths = observedFiles
                .Select(item => item.File.FullName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (file, length) in observedFiles)
            {
                if (!firstPass &&
                    (!_knownFileLengths.TryGetValue(file.FullName, out var previousLength) || previousLength != length))
                {
                    _trackedFileActivity[file.FullName] = ++_fileActivitySequence;
                }

                _knownFileLengths[file.FullName] = length;
            }

            foreach (var missingPath in _knownFileLengths.Keys.Where(path => !currentPaths.Contains(path)).ToArray())
            {
                _knownFileLengths.Remove(missingPath);
                _trackedFileActivity.Remove(missingPath);
            }

            var recent = observedFiles
                .OrderByDescending(item => item.File.LastWriteTimeUtc)
                .Take(RecentFileCount)
                .ToArray();
            foreach (var (file, _) in recent)
            {
                if (!_trackedFileActivity.ContainsKey(file.FullName))
                {
                    _trackedFileActivity[file.FullName] = ++_fileActivitySequence;
                }
            }

            foreach (var path in _trackedFileActivity
                         .OrderByDescending(item => item.Value)
                         .Skip(TrackedFileCount)
                         .Select(item => item.Key)
                         .ToArray())
            {
                _trackedFileActivity.Remove(path);
            }

            var byPath = observedFiles.ToDictionary(item => item.File.FullName, StringComparer.OrdinalIgnoreCase);
            return recent
                .Concat(_trackedFileActivity.Keys
                    .Where(byPath.ContainsKey)
                    .Select(path => byPath[path]))
                .Select(item => item.File)
                .DistinctBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
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
