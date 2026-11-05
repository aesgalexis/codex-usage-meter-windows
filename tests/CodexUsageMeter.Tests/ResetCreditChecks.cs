using System.IO;
using System.Text.Json;
using CodexUsageMeter.Infrastructure;

internal static class ResetCreditChecks
{
    public static async Task RunAsync()
    {
        foreach (var available in new[] { 0, 1, 3 })
        {
            var output = new StringReader("{\"id\":1,\"result\":{}}\n" +
                "{\"method\":\"account/updated\",\"params\":{}}\n" +
                "{\"id\":2,\"result\":{\"rateLimitResetCredits\":{\"availableCount\":" + available +
                ",\"credits\":[{\"id\":\"synthetic-credit\"}]}}}\n");
            var input = new StringWriter();
            Require(await CodexResetCreditProvider.ReadProtocolAsync(output, input, CancellationToken.None) == available,
                "The availableCount field is authoritative, including zero; detail-row counts are not reset availability.");
            var requests = input.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => JsonDocument.Parse(line)).ToArray();
            try
            {
                Require(requests.Select(document => document.RootElement.GetProperty("method").GetString())
                    .SequenceEqual(new[] { "initialize", "initialized", "account/rateLimits/read" }),
                    "The reader must initialize and read limits only, never redeem a reset or request a login.");
            }
            finally { foreach (var document in requests) document.Dispose(); }
        }

        foreach (var result in new[]
        {
            "{}", "{\"rateLimitResetCredits\":null}", "{\"rateLimitResetCredits\":[]}",
            "{\"rateLimitResetCredits\":{\"availableCount\":-1}}",
            "{\"rateLimitResetCredits\":{\"availableCount\":\"3\"}}",
            "{\"rateLimitResetCredits\":{\"availableCount\":2.5}}",
            "{\"rateLimitResetCredits\":{\"availableCount\":2147483648}}",
            "{\"rateLimitResetCredits\":{\"credits\":[{}]}}",
            "{\"rateLimits\":{\"credits\":{\"balance\":\"12.5\"}}}"
        })
        {
            var output = new StringReader("{\"id\":1,\"result\":{}}\n{\"id\":2,\"result\":" + result + "}\n");
            Require(await CodexResetCreditProvider.ReadProtocolAsync(output, new StringWriter(), CancellationToken.None) is null,
                "Missing or invalid reset counts must remain unknown, without inference from currency credits.");
        }
        var rejected = new StringReader("{\"id\":1,\"error\":{\"code\":-1}}\n");
        var rejectedInput = new StringWriter();
        Require(await CodexResetCreditProvider.ReadProtocolAsync(rejected, rejectedInput, CancellationToken.None) is null &&
                !rejectedInput.ToString().Contains("account/rateLimits/read", StringComparison.Ordinal),
            "Rejected initialization must stop without making other requests.");
        Require(await CodexResetCreditProvider.ReadProtocolAsync(new StringReader(""), new StringWriter(), CancellationToken.None) is null,
            "An unavailable app-server must remain unknown.");
        var malformed = new StringReader("not-json\n[]\n{\"id\":1,\"result\":{}}\n{\"id\":2,\"error\":{\"code\":-1}}\n");
        Require(await CodexResetCreditProvider.ReadProtocolAsync(malformed, new StringWriter(), CancellationToken.None) is null,
            "Malformed diagnostics and a rejected rate-limit request must not become a reset count.");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Require(await new CodexResetCreditProvider().GetAvailableAsync(canceled.Token) is null,
            "Shutdown cancellation must not launch Codex.");
        Require(await new CodexResetCreditProvider(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.exe"))
            .GetAvailableAsync() is null, "A missing Codex executable must not interrupt the meter.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
