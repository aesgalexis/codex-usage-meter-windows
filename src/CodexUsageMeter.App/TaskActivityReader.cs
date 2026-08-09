using System.IO;
using System.Text;
using System.Text.Json;

namespace CodexUsageMeter.App;

public static class TaskActivityReader
{
    public static async Task<bool?> ReadLatestAsync(string path, DateTimeOffset now, TimeSpan inactiveAfter)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, bufferSize: 4096, useAsync: true);
            var lastWrite = File.GetLastWriteTimeUtc(path);
            var bytesToRead = (int)Math.Min(stream.Length, 128 * 1024);
            stream.Seek(-bytesToRead, SeekOrigin.End);
            var buffer = new byte[bytesToRead];
            var read = 0;
            while (read < buffer.Length)
            {
                var count = await stream.ReadAsync(buffer.AsMemory(read));
                if (count == 0) break;
                read += count;
            }

            var lines = Encoding.UTF8.GetString(buffer, 0, read).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (var index = lines.Length - 1; index >= 0; index--)
            {
                try
                {
                    using var document = JsonDocument.Parse(lines[index].TrimEnd('\r'));
                    var root = document.RootElement;
                    if (!root.TryGetProperty("type", out var type) || type.GetString() != "event_msg" ||
                        !root.TryGetProperty("payload", out var payload) ||
                        !payload.TryGetProperty("type", out var payloadType)) continue;

                    switch (payloadType.GetString())
                    {
                        case "task_started":
                            return now - lastWrite <= inactiveAfter;
                        case "task_complete":
                        case "task_canceled":
                        case "task_cancelled":
                        case "task_failed":
                        case "turn_aborted":
                            return false;
                    }
                }
                catch (JsonException)
                {
                    // A boundary line may be incomplete while Codex is writing it.
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return null;
    }
}
