using System.Threading;

namespace CodexUsageMeter.App;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
        using var mutex = new Mutex(true, "CodexUsageMeter.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            return;
        }

        var application = new UsageApplication();
        application.Run();
    }
}
