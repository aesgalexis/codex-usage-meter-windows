namespace CodexUsageMeter.Infrastructure;

public static class CodexHomeResolver
{
    public static string Resolve(string? codexHome = null)
    {
        if (!string.IsNullOrWhiteSpace(codexHome)) return codexHome;

        var environmentHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        return !string.IsNullOrWhiteSpace(environmentHome)
            ? environmentHome
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
    }
}
