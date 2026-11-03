using System.Diagnostics;
using System.IO;

namespace CodexUsageMeter.App;

internal static class AppUpdateInstaller
{
    internal static ProcessStartInfo CreateStartInfo(string installer, string appDirectory, bool isInstalled, string language)
    {
        var startInfo = new ProcessStartInfo(installer) { UseShellExecute = true };
        foreach (var argument in new[]
        {
            "/VERYSILENT", "/SUPPRESSMSGBOXES", "/SP-", "/NORESTART",
            "/CLOSEAPPLICATIONS", "/FORCECLOSEAPPLICATIONS", "/NORESTARTAPPLICATIONS", "/AUTOMATICUPDATE=1"
        }) startInfo.ArgumentList.Add(argument);
        startInfo.ArgumentList.Add("/LANG=" + (language == AppText.Spanish ? "spanish" : "english"));
        startInfo.ArgumentList.Add("/RESTARTAPP=" + Path.Combine(appDirectory, "CodexUsageMeter.exe"));
        if (isInstalled) startInfo.ArgumentList.Add("/DIR=" + Path.TrimEndingDirectorySeparator(appDirectory));
        return startInfo;
    }
}
