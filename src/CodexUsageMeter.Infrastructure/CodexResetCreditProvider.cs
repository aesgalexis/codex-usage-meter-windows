using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using CodexUsageMeter.Core;

namespace CodexUsageMeter.Infrastructure;

public sealed class CodexResetCreditProvider(string? executablePath = null) : IResetCreditProvider
{
    public async Task<int?> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var process = new Process();
        try
        {
            if (timeout.IsCancellationRequested) return null;
            var executable = executablePath ?? FindExecutable();
            if (executable is null) return null;
            process.StartInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[] { "app-server", "--stdio", "-c", "analytics.enabled=false" })
                process.StartInfo.ArgumentList.Add(argument);
            process.StartInfo.Environment["CODEX_HOME"] = CodexHomeResolver.Resolve();
            if (!process.Start()) return null;
            // Drain diagnostics without displaying or persisting account information.
            var diagnostics = process.StandardError.BaseStream.CopyToAsync(Stream.Null, timeout.Token);
            try
            {
                return await ReadProtocolAsync(process.StandardOutput, process.StandardInput, timeout.Token);
            }
            finally
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                try { await diagnostics; }
                catch (OperationCanceledException) { }
                catch (IOException) { }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                         Win32Exception or InvalidOperationException or OperationCanceledException)
        {
            return null;
        }
    }

    internal static async Task<int?> ReadProtocolAsync(TextReader output, TextWriter input, CancellationToken cancellationToken)
    {
        var initialize = JsonSerializer.Serialize(new
        {
            id = 1,
            method = "initialize",
            @params = new { clientInfo = new { name = "codex_usage_meter", title = "Codex Usage Meter",
                version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0" } }
        });
        await input.WriteLineAsync(initialize.AsMemory(), cancellationToken);
        await input.FlushAsync(cancellationToken);
        if (await ReadResponseAsync(output, 1, cancellationToken) is null) return null;
        await input.WriteLineAsync("{\"method\":\"initialized\"}".AsMemory(), cancellationToken);
        // Read-only: never call account/rateLimitResetCredit/consume.
        await input.WriteLineAsync("{\"id\":2,\"method\":\"account/rateLimits/read\"}".AsMemory(), cancellationToken);
        await input.FlushAsync(cancellationToken);
        var result = await ReadResponseAsync(output, 2, cancellationToken);
        if (result is not { ValueKind: JsonValueKind.Object } response ||
            !response.TryGetProperty("rateLimitResetCredits", out var credits) ||
            credits.ValueKind != JsonValueKind.Object ||
            !credits.TryGetProperty("availableCount", out var count) || count.ValueKind != JsonValueKind.Number ||
            !count.TryGetInt32(out var available) || available < 0) return null;
        return available;
    }

    private static async Task<JsonElement?> ReadResponseAsync(TextReader output, int expectedId, CancellationToken cancellationToken)
    {
        for (var index = 0; index < 64; index++)
        {
            var line = await output.ReadLineAsync(cancellationToken);
            if (line is null) return null;
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("id", out var id) ||
                    id.ValueKind != JsonValueKind.Number || !id.TryGetInt32(out var value) || value != expectedId) continue;
                return !root.TryGetProperty("error", out _) && root.TryGetProperty("result", out var result) &&
                       result.ValueKind == JsonValueKind.Object ? result.Clone() : null;
            }
            catch (JsonException) { }
        }
        return null;
    }

    private static string? FindExecutable()
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var path = Path.Combine(directory.Trim('"'), "codex.exe");
            if (File.Exists(path)) return path;
        }
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        foreach (var root in new[] { Path.Combine(local, "Programs", "OpenAI", "Codex", "bin"), Path.Combine(local, "OpenAI", "Codex", "bin") })
        {
            var path = Path.Combine(root, "codex.exe");
            if (File.Exists(path)) return path;
            if (!Directory.Exists(root)) continue;
            foreach (var directory in new DirectoryInfo(root).EnumerateDirectories().OrderByDescending(item => item.LastWriteTimeUtc))
            {
                path = Path.Combine(directory.FullName, "codex.exe");
                if (File.Exists(path)) return path;
            }
        }
        return null;
    }
}
