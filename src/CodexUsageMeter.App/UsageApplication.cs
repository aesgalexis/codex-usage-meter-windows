using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using CodexUsageMeter.Core;
using CodexUsageMeter.Infrastructure;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace CodexUsageMeter.App;

public sealed class UsageApplication : System.Windows.Application
{
    private const string StartupKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "CodexUsageMeter";
    private readonly IUsageProvider _provider = new CodexSessionUsageProvider();
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly Forms.NotifyIcon _notifyIcon = new() { Visible = true };
    private readonly Forms.ToolStripMenuItem _statusItem = new() { Enabled = false };
    private readonly Forms.ToolStripMenuItem _resetItem = new() { Enabled = false };
    private readonly Forms.ToolStripMenuItem _startupItem = new("Iniciar con Windows") { CheckOnClick = true };
    private Icon? _currentIcon;
    private UsageSnapshot? _latest;
    private bool _isRefreshing;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _notifyIcon.Text = "Codex Usage Meter: buscando datos…";
        _notifyIcon.Icon = ReplaceIcon(TrayIconFactory.Create(null));
        _notifyIcon.ContextMenuStrip = BuildMenu();
        _notifyIcon.MouseClick += OnTrayMouseClick;

        _startupItem.Checked = IsStartupEnabled();
        _startupItem.CheckedChanged += (_, _) => SetStartupEnabled(_startupItem.Checked);

        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        _refreshTimer.Start();
        _ = RefreshAsync();
    }

    private Forms.ContextMenuStrip BuildMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        var title = new Forms.ToolStripMenuItem("Codex Usage Meter") { Enabled = false };
        var refresh = new Forms.ToolStripMenuItem("Actualizar ahora");
        refresh.Click += async (_, _) => await RefreshAsync();
        var openSessions = new Forms.ToolStripMenuItem("Abrir sesiones de Codex");
        openSessions.Click += (_, _) => OpenSessionsFolder();
        var exit = new Forms.ToolStripMenuItem("Salir");
        exit.Click += (_, _) => Shutdown();

        menu.Items.AddRange([
            title,
            new Forms.ToolStripSeparator(),
            _statusItem,
            _resetItem,
            new Forms.ToolStripSeparator(),
            refresh,
            openSessions,
            _startupItem,
            new Forms.ToolStripSeparator(),
            exit
        ]);
        return menu;
    }

    private async Task RefreshAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            var result = await _provider.GetLatestAsync();
            if (result.Snapshot is { } snapshot)
            {
                _latest = snapshot;
                var available = (int)Math.Round(snapshot.AvailablePercent);
                _statusItem.Text = $"Disponible: {available}%  ·  Usado: {snapshot.UsedPercent:0.#}%";
                _resetItem.Text = snapshot.ResetsAt is { } reset
                    ? $"Se reinicia: {reset.ToLocalTime():g}"
                    : "Reinicio: sin datos";
                _notifyIcon.Text = TruncateTooltip($"Codex: {available}% disponible");
                _notifyIcon.Icon = ReplaceIcon(TrayIconFactory.Create(snapshot.AvailablePercent));
            }
            else
            {
                _latest = null;
                _statusItem.Text = result.Error ?? "No hay datos de uso";
                _resetItem.Text = "Abre Codex y ejecuta al menos una tarea";
                _notifyIcon.Text = TruncateTooltip("Codex Usage Meter: sin datos");
                _notifyIcon.Icon = ReplaceIcon(TrayIconFactory.Create(null));
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void OnTrayMouseClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button != Forms.MouseButtons.Left)
        {
            return;
        }

        if (_latest is { } snapshot)
        {
            var reset = snapshot.ResetsAt is { } resetsAt
                ? $"\nReinicio: {resetsAt.ToLocalTime():g}"
                : string.Empty;
            _notifyIcon.BalloonTipTitle = "Uso de Codex";
            _notifyIcon.BalloonTipText = $"{snapshot.AvailablePercent:0}% disponible ({snapshot.UsedPercent:0.#}% usado){reset}";
        }
        else
        {
            _notifyIcon.BalloonTipTitle = "Uso de Codex no disponible";
            _notifyIcon.BalloonTipText = "Ejecuta una tarea en Codex y vuelve a actualizar.";
        }

        _notifyIcon.ShowBalloonTip(4000);
    }

    private Icon ReplaceIcon(Icon next)
    {
        var previous = _currentIcon;
        _currentIcon = next;
        previous?.Dispose();
        return next;
    }

    private static string TruncateTooltip(string value) => value[..Math.Min(value.Length, 63)];

    private static void OpenSessionsFolder()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "sessions");
        if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupKeyPath);
        return key?.GetValue(StartupValueName) is string;
    }

    private static void SetStartupEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(StartupKeyPath);
        if (enabled)
        {
            key.SetValue(StartupValueName, $"\"{Environment.ProcessPath}\"");
        }
        else
        {
            key.DeleteValue(StartupValueName, false);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _refreshTimer.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _currentIcon?.Dispose();
        base.OnExit(e);
    }
}

internal static class TrayIconFactory
{
    public static Icon Create(double? availablePercent)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var color = availablePercent switch
        {
            null => Color.FromArgb(120, 130, 140),
            >= 50 => Color.FromArgb(32, 180, 110),
            >= 20 => Color.FromArgb(240, 170, 35),
            _ => Color.FromArgb(220, 65, 70)
        };

        using var background = new SolidBrush(Color.FromArgb(35, 38, 45));
        using var ring = new Pen(Color.FromArgb(75, 80, 90), 4f);
        using var progress = new Pen(color, 4f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        graphics.FillEllipse(background, 2, 2, 28, 28);
        graphics.DrawEllipse(ring, 5, 5, 22, 22);
        if (availablePercent is { } value)
        {
            graphics.DrawArc(progress, 5, 5, 22, 22, -90f, (float)(360d * value / 100d));
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
