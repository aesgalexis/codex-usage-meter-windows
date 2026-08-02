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
    private readonly AppSettingsStore _settingsStore = new();
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly DispatcherTimer _fileChangeTimer = new() { Interval = TimeSpan.FromMilliseconds(750) };
    private readonly DispatcherTimer _flyoutCloseTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly StableNotifyIcon _notifyIcon = new();
    private readonly Forms.ToolStripMenuItem _statusItem = new() { Enabled = false };
    private readonly Forms.ToolStripMenuItem _resetItem = new() { Enabled = false };
    private readonly Forms.ToolStripMenuItem _startupItem = new("Iniciar con Windows") { CheckOnClick = true };
    private readonly Forms.ToolStripMenuItem _widgetItem = new("Mostrar widget fijo") { CheckOnClick = true };
    private readonly Forms.ToolStripMenuItem _disabledWidgetItem = new("Desactivado") { CheckOnClick = true };
    private readonly Forms.ToolStripMenuItem _normalWidgetItem = new("Normal") { CheckOnClick = true };
    private readonly Forms.ToolStripMenuItem _compactWidgetItem = new("Compacto") { CheckOnClick = true };
    private UsageFlyoutWindow? _flyout;
    private Icon? _currentIcon;
    private UsageSnapshot? _latest;
    private AppSettings _settings = new();
    private FileSystemWatcher? _sessionWatcher;
    private bool _isRefreshing;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _settings = _settingsStore.Load();
        if (!_settings.WidgetEnabled) _settings.WidgetPinned = false;

        _notifyIcon.Text = "Codex Usage Meter: buscando datos…";
        _notifyIcon.Icon = ReplaceIcon(TrayIconFactory.Create(null));
        _notifyIcon.ContextMenuStrip = BuildMenu();
        _notifyIcon.MouseClick += OnTrayMouseClick;
        _notifyIcon.HoverOpened += (_, _) => ShowFlyout(false, false);
        _notifyIcon.HoverClosed += (_, _) => ScheduleFlyoutClose();
        _notifyIcon.Visible = true;

        _flyoutCloseTimer.Tick += (_, _) =>
        {
            _flyoutCloseTimer.Stop();
            if (_flyout is { IsPinned: false, IsMouseOver: false }) _flyout.Hide();
        };
        _widgetItem.Checked = _settings.WidgetPinned;
        _widgetItem.CheckedChanged += (_, _) => SetWidgetPinned(_widgetItem.Checked);

        _startupItem.Checked = IsStartupEnabled();
        _startupItem.CheckedChanged += (_, _) => SetStartupEnabled(_startupItem.Checked);

        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        _fileChangeTimer.Tick += async (_, _) =>
        {
            _fileChangeTimer.Stop();
            await RefreshAsync();
        };
        EnsureSessionWatcher();
        _refreshTimer.Start();
        _ = RefreshAsync();
        if (_settings.WidgetPinned) Dispatcher.BeginInvoke(() => ShowFlyout(true));
    }

    private Forms.ContextMenuStrip BuildMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        var title = new Forms.ToolStripMenuItem("Codex Usage Meter") { Enabled = false };
        var refresh = new Forms.ToolStripMenuItem("Actualizar ahora");
        refresh.Click += async (_, _) => await RefreshAsync();
        var openSessions = new Forms.ToolStripMenuItem("Abrir sesiones de Codex");
        openSessions.Click += (_, _) => OpenSessionsFolder();
        var keepVisible = new Forms.ToolStripMenuItem("Mostrar siempre en la bandeja…");
        keepVisible.Click += (_, _) => OpenTrayVisibilitySettings();
        var notifications = BuildNotificationsMenu();
        var widgetSize = BuildWidgetSizeMenu();
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
            keepVisible,
            notifications,
            _widgetItem,
            widgetSize,
            _startupItem,
            new Forms.ToolStripSeparator(),
            exit
        ]);
        return menu;
    }

    private Forms.ToolStripMenuItem BuildWidgetSizeMenu()
    {
        var menu = new Forms.ToolStripMenuItem("Widget");
        _disabledWidgetItem.Checked = !_settings.WidgetEnabled;
        _normalWidgetItem.Checked = _settings.WidgetEnabled && !_settings.WidgetCompact;
        _compactWidgetItem.Checked = _settings.WidgetEnabled && _settings.WidgetCompact;
        _normalWidgetItem.Click += (_, _) => SetWidgetMode(true, false);
        _compactWidgetItem.Click += (_, _) => SetWidgetMode(true, true);
        _disabledWidgetItem.Click += (_, _) => SetWidgetMode(false, false);
        menu.DropDownItems.AddRange([_disabledWidgetItem, _normalWidgetItem, _compactWidgetItem]);
        return menu;
    }

    private Forms.ToolStripMenuItem BuildNotificationsMenu()
    {
        var menu = new Forms.ToolStripMenuItem("Notificaciones");
        menu.DropDownItems.Add(CreateNotificationOption(
            "Al cambiar el porcentaje",
            _settings.NotifyOnPercentChange,
            value => _settings.NotifyOnPercentChange = value));
        menu.DropDownItems.Add(new Forms.ToolStripSeparator());
        menu.DropDownItems.Add(CreateNotificationOption(
            "Al alcanzar 50 % usado",
            _settings.NotifyAt50Percent,
            value => _settings.NotifyAt50Percent = value));
        menu.DropDownItems.Add(CreateNotificationOption(
            "Al alcanzar 75 % usado",
            _settings.NotifyAt75Percent,
            value => _settings.NotifyAt75Percent = value));
        menu.DropDownItems.Add(CreateNotificationOption(
            "Al alcanzar 90 % usado",
            _settings.NotifyAt90Percent,
            value => _settings.NotifyAt90Percent = value));
        menu.DropDownItems.Add(new Forms.ToolStripSeparator());
        menu.DropDownItems.Add(CreateNotificationOption(
            "Al restablecerse el límite",
            _settings.NotifyOnReset,
            value => _settings.NotifyOnReset = value));
        return menu;
    }

    private Forms.ToolStripMenuItem CreateNotificationOption(
        string text,
        bool initialValue,
        Action<bool> update)
    {
        var item = new Forms.ToolStripMenuItem(text)
        {
            Checked = initialValue,
            CheckOnClick = true
        };
        item.CheckedChanged += (_, _) =>
        {
            update(item.Checked);
            _settingsStore.Save(_settings);
        };
        return item;
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
                var notification = UsageNotificationEvaluator.Evaluate(
                    _latest,
                    snapshot,
                    _settings.ToNotificationOptions());
                _latest = snapshot;
                var available = (int)Math.Round(snapshot.AvailablePercent);
                _statusItem.Text = $"Disponible: {available}%  ·  Usado: {snapshot.UsedPercent:0.#}%";
                _resetItem.Text = snapshot.ResetsAt is { } reset
                    ? $"Se reinicia: {reset.ToLocalTime():g}"
                    : "Reinicio: sin datos";
                _notifyIcon.Text = TruncateTooltip($"Codex: {available}% disponible");
                _notifyIcon.Icon = ReplaceIcon(TrayIconFactory.Create(snapshot.AvailablePercent));
                _flyout?.UpdateUsage(snapshot);
                ShowUsageNotification(notification, snapshot);
            }
            else
            {
                _latest = null;
                _statusItem.Text = result.Error ?? "No hay datos de uso";
                _resetItem.Text = "Abre Codex y ejecuta al menos una tarea";
                _notifyIcon.Text = TruncateTooltip("Codex Usage Meter: sin datos");
                _notifyIcon.Icon = ReplaceIcon(TrayIconFactory.Create(null));
                _flyout?.UpdateUsage(null, result.Error);
            }
        }
        finally
        {
            EnsureSessionWatcher();
            _isRefreshing = false;
        }
    }

    private void ShowUsageNotification(UsageNotification? notification, UsageSnapshot snapshot)
    {
        if (notification is null)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = notification.Kind switch
        {
            UsageNotificationKind.ThresholdReached => $"Codex ha alcanzado {notification.Threshold}% de uso",
            UsageNotificationKind.LimitReset => "El límite de Codex se ha restablecido",
            _ => "El uso de Codex ha cambiado"
        };
        _notifyIcon.BalloonTipText = $"{snapshot.AvailablePercent:0}% disponible ({snapshot.UsedPercent:0.#}% usado).";
        _notifyIcon.ShowBalloonTip(5000);
    }

    private void EnsureSessionWatcher()
    {
        if (_sessionWatcher is not null)
        {
            if (Directory.Exists(_sessionWatcher.Path)) return;
            _sessionWatcher.Dispose();
            _sessionWatcher = null;
        }

        var sessionsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex",
            "sessions");
        if (!Directory.Exists(sessionsPath))
        {
            return;
        }

        try
        {
            _sessionWatcher = new FileSystemWatcher(sessionsPath, "*.jsonl")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };
            _sessionWatcher.Changed += OnSessionFileChanged;
            _sessionWatcher.Created += OnSessionFileChanged;
            _sessionWatcher.Renamed += OnSessionFileChanged;
            _sessionWatcher.Error += OnSessionWatcherError;
        }
        catch (IOException)
        {
            _sessionWatcher?.Dispose();
            _sessionWatcher = null;
        }
        catch (UnauthorizedAccessException)
        {
            _sessionWatcher?.Dispose();
            _sessionWatcher = null;
        }
    }

    private void OnSessionFileChanged(object sender, FileSystemEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _fileChangeTimer.Stop();
            _fileChangeTimer.Start();
        });
    }

    private void OnSessionWatcherError(object sender, ErrorEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _sessionWatcher?.Dispose();
            _sessionWatcher = null;
        });
    }

    private void OnTrayMouseClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button != Forms.MouseButtons.Left)
        {
            return;
        }

        if (!_settings.WidgetEnabled)
        {
            ShowUsageBalloon();
        }
        else if (_flyout?.IsVisible == true && !_flyout.IsPinned) _flyout.Hide();
        else ShowFlyout(false, true);
    }

    private void ShowUsageBalloon()
    {
        if (_latest is { } snapshot)
        {
            _notifyIcon.BalloonTipTitle = "Uso de Codex";
            _notifyIcon.BalloonTipText = $"{snapshot.AvailablePercent:0}% disponible ({snapshot.UsedPercent:0.#}% usado).";
        }
        else
        {
            _notifyIcon.BalloonTipTitle = "Uso de Codex no disponible";
            _notifyIcon.BalloonTipText = "Ejecuta una tarea en Codex y vuelve a actualizar.";
        }
        _notifyIcon.ShowBalloonTip(4000);
    }

    private void ShowFlyout(bool pinned, bool activate = false)
    {
        if (!_settings.WidgetEnabled) return;
        _flyoutCloseTimer.Stop();
        EnsureFlyout();
        _flyout!.SetPinned(pinned || _settings.WidgetPinned);
        _flyout.SetCompact(_settings.WidgetCompact);
        _flyout.UpdateUsage(_latest);

        var hasSavedPosition = _settings.WidgetLeft is not null && _settings.WidgetTop is not null;
        if (_flyout.IsPinned && _settings.WidgetLeft is { } left && _settings.WidgetTop is { } top)
        {
            _flyout.Left = left;
            _flyout.Top = top;
        }
        else
        {
            PositionFlyoutAtTray();
        }

        _flyout.Show();
        if (_flyout.IsPinned && !hasSavedPosition) SaveWidgetPosition();
        if (activate) _flyout.Activate();
    }

    private void EnsureFlyout()
    {
        if (_flyout is not null) return;
        _flyout = new UsageFlyoutWindow();
        _flyout.MouseEnter += (_, _) => _flyoutCloseTimer.Stop();
        _flyout.MouseLeave += (_, _) => ScheduleFlyoutClose();
        _flyout.PinChanged += (_, pinned) =>
        {
            _settings.WidgetPinned = pinned;
            _widgetItem.Checked = pinned;
            SaveWidgetPosition();
            _settingsStore.Save(_settings);
        };
        _flyout.PositionChanged += (_, _) => SaveWidgetPosition();
    }

    private void SetWidgetMode(bool enabled, bool compact)
    {
        _settings.WidgetEnabled = enabled;
        _settings.WidgetCompact = compact;
        _disabledWidgetItem.Checked = !enabled;
        _normalWidgetItem.Checked = enabled && !compact;
        _compactWidgetItem.Checked = enabled && compact;
        if (!enabled)
        {
            _settings.WidgetPinned = false;
            _widgetItem.Checked = false;
            _flyout?.Hide();
        }
        _settingsStore.Save(_settings);
        if (enabled && _flyout is { IsVisible: true }) _flyout.SetCompact(compact);
    }

    private void SetWidgetPinned(bool pinned)
    {
        if (pinned && !_settings.WidgetEnabled)
        {
            _settings.WidgetEnabled = true;
            _settings.WidgetCompact = false;
            _disabledWidgetItem.Checked = false;
            _normalWidgetItem.Checked = true;
            _compactWidgetItem.Checked = false;
        }
        _settings.WidgetPinned = pinned;
        _settingsStore.Save(_settings);
        if (pinned) ShowFlyout(true);
        else if (_flyout is not null) { _flyout.SetPinned(false); _flyout.Hide(); }
    }

    private void ScheduleFlyoutClose()
    {
        if (_flyout?.IsPinned != false) return;
        _flyoutCloseTimer.Stop();
        _flyoutCloseTimer.Start();
    }

    private void PositionFlyoutAtTray()
    {
        if (_flyout is null) return;
        var iconBounds = _notifyIcon.TryGetBounds(out var bounds)
            ? bounds
            : new Rectangle(Forms.Cursor.Position, new System.Drawing.Size(1, 1));
        var screen = Forms.Screen.FromRectangle(iconBounds);
        var source = PresentationSource.FromVisual(_flyout);
        var scaleX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1d;
        var scaleY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1d;
        var work = screen.WorkingArea;
        var left = (iconBounds.Left + iconBounds.Width / 2d) * scaleX - _flyout.Width / 2d;
        var top = iconBounds.Top * scaleY - _flyout.Height - 8;
        _flyout.Left = Math.Clamp(left, work.Left * scaleX + 8, work.Right * scaleX - _flyout.Width - 8);
        _flyout.Top = Math.Clamp(top, work.Top * scaleY + 8, work.Bottom * scaleY - _flyout.Height - 8);
    }

    private void SaveWidgetPosition()
    {
        if (_flyout is not { IsPinned: true }) return;
        _settings.WidgetLeft = _flyout.Left;
        _settings.WidgetTop = _flyout.Top;
        _settingsStore.Save(_settings);
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

    private static void OpenTrayVisibilitySettings()
    {
        Process.Start(new ProcessStartInfo("ms-settings:taskbar") { UseShellExecute = true });
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupKeyPath);
        if (key?.GetValue(StartupValueName) is not string configuredPath ||
            string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            return false;
        }

        try
        {
            var normalizedConfiguredPath = Path.GetFullPath(configuredPath.Trim().Trim('"'));
            var normalizedProcessPath = Path.GetFullPath(Environment.ProcessPath);
            return string.Equals(normalizedConfiguredPath, normalizedProcessPath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }
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
        _fileChangeTimer.Stop();
        _flyoutCloseTimer.Stop();
        _sessionWatcher?.Dispose();
        _flyout?.Close();
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

        var roundedAvailable = availablePercent is { } available
            ? (int)Math.Round(available)
            : (int?)null;
        var color = roundedAvailable switch
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
