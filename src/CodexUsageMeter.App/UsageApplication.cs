using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
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
    private const string AppRegistryPath = @"Software\CodexUsageMeter";
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);
    private readonly IUsageProvider _provider = new CodexSessionUsageProvider();
    private readonly AppSettingsStore _settingsStore = new();
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly DispatcherTimer _fileChangeTimer = new() { Interval = TimeSpan.FromMilliseconds(750) };
    private readonly DispatcherTimer _activityStateTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private readonly DispatcherTimer _shineTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly DispatcherTimer _flyoutCloseTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly StableNotifyIcon _notifyIcon = new();
    private readonly Forms.ToolStripMenuItem _statusItem = new() { Enabled = false };
    private readonly Forms.ToolStripMenuItem _resetItem = new() { Enabled = false };
    private Forms.ToolStripMenuItem _startupItem = null!;
    private Forms.ToolStripMenuItem _widgetItem = null!;
    private Forms.ToolStripMenuItem _disabledWidgetItem = null!;
    private Forms.ToolStripMenuItem _normalWidgetItem = null!;
    private Forms.ToolStripMenuItem _compactWidgetItem = null!;
    private Forms.ToolStripMenuItem _usageBarItem = null!;
    private UsageFlyoutWindow? _flyout;
    private UsageFlyoutWindow? _usageBar;
    private Icon? _currentIcon;
    private UsageSnapshot? _latest;
    private AppSettings _settings = new();
    private FileSystemWatcher? _sessionWatcher;
    private readonly HashSet<string> _pendingActivityPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _activeTaskFiles = new(StringComparer.OrdinalIgnoreCase);
    private bool _isRefreshing;
    private bool _latestIsStale;
    private UsageFailureKind _latestFailureKind;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _settings = _settingsStore.Load();
        var language = _settings.Language ?? ReadInstallerLanguage() ?? AppText.DetectLanguage();
        AppText.SetLanguage(language);
        _settings.Language = AppText.CurrentLanguage;
        _settingsStore.Save(_settings);
        if (!_settings.WidgetEnabled) _settings.WidgetPinned = false;

        _notifyIcon.Text = AppText.Get("Searching");
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
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        _fileChangeTimer.Tick += async (_, _) =>
        {
            _fileChangeTimer.Stop();
            await RefreshAsync();
        };
        _activityStateTimer.Tick += async (_, _) =>
        {
            _activityStateTimer.Stop();
            await ProcessPendingActivityStatesAsync();
        };
        _shineTimer.Tick += async (_, _) =>
        {
            await ReconcileActivityStatesAsync();
            if (_shineTimer.IsEnabled)
            {
                _flyout?.PlayShine(force: true);
                _usageBar?.PlayShine(force: true);
            }
        };
        EnsureSessionWatcher();
        _refreshTimer.Start();
        _ = RefreshAsync();
        if (_settings.WidgetPinned) Dispatcher.BeginInvoke(() => ShowFlyout(true));
        if (_settings.UsageBarEnabled) Dispatcher.BeginInvoke(ShowUsageBar);
    }

    private Forms.ContextMenuStrip BuildMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        var title = new Forms.ToolStripMenuItem("Codex Usage Meter") { Enabled = false };
        var refresh = new Forms.ToolStripMenuItem(AppText.Get("Refresh"));
        refresh.Click += async (_, _) => await RefreshAsync();
        var keepVisible = new Forms.ToolStripMenuItem(AppText.Get("KeepTray"));
        keepVisible.Click += (_, _) => OpenTrayVisibilitySettings();
        var notifications = BuildNotificationsMenu();
        var widgetSize = BuildWidgetSizeMenu();
        _usageBarItem = new Forms.ToolStripMenuItem(AppText.Get("UsageBar")) { CheckOnClick = true, Checked = _settings.UsageBarEnabled };
        _usageBarItem.CheckedChanged += (_, _) => SetUsageBarEnabled(_usageBarItem.Checked);
        var language = BuildLanguageMenu();
        _widgetItem = new Forms.ToolStripMenuItem(AppText.Get("ShowPinned")) { CheckOnClick = true, Checked = _settings.WidgetPinned };
        _widgetItem.CheckedChanged += (_, _) => SetWidgetPinned(_widgetItem.Checked);
        _startupItem = new Forms.ToolStripMenuItem(AppText.Get("StartWindows")) { CheckOnClick = true, Checked = IsStartupEnabled() };
        _startupItem.CheckedChanged += (_, _) => SetStartupEnabled(_startupItem.Checked);
        var exit = new Forms.ToolStripMenuItem(AppText.Get("Exit"));
        exit.Click += (_, _) => Shutdown();

        menu.Items.AddRange([
            title,
            new Forms.ToolStripSeparator(),
            _statusItem,
            _resetItem,
            new Forms.ToolStripSeparator(),
            refresh,
            keepVisible,
            notifications,
            _widgetItem,
            widgetSize,
            _usageBarItem,
            language,
            _startupItem,
            new Forms.ToolStripSeparator(),
            exit
        ]);
        return menu;
    }

    private Forms.ToolStripMenuItem BuildWidgetSizeMenu()
    {
        var menu = new Forms.ToolStripMenuItem(AppText.Get("Widget"));
        _disabledWidgetItem = new Forms.ToolStripMenuItem(AppText.Get("Disabled")) { CheckOnClick = true };
        _normalWidgetItem = new Forms.ToolStripMenuItem(AppText.Get("Normal")) { CheckOnClick = true };
        _compactWidgetItem = new Forms.ToolStripMenuItem(AppText.Get("Compact")) { CheckOnClick = true };
        _disabledWidgetItem.Checked = !_settings.WidgetEnabled;
        _normalWidgetItem.Checked = _settings.WidgetEnabled && !_settings.WidgetCompact;
        _compactWidgetItem.Checked = _settings.WidgetEnabled && _settings.WidgetCompact;
        _normalWidgetItem.Click += (_, _) => SetWidgetMode(true, compact: false);
        _compactWidgetItem.Click += (_, _) => SetWidgetMode(true, compact: true);
        _disabledWidgetItem.Click += (_, _) => SetWidgetMode(false, compact: false);
        menu.DropDownItems.AddRange([_disabledWidgetItem, _normalWidgetItem, _compactWidgetItem]);
        return menu;
    }

    private Forms.ToolStripMenuItem BuildLanguageMenu()
    {
        var menu = new Forms.ToolStripMenuItem(AppText.Get("Language"));
        var english = new Forms.ToolStripMenuItem("English") { Checked = AppText.CurrentLanguage == AppText.English };
        var spanish = new Forms.ToolStripMenuItem("Español") { Checked = AppText.CurrentLanguage == AppText.Spanish };
        english.Click += (_, _) => ChangeLanguage(AppText.English);
        spanish.Click += (_, _) => ChangeLanguage(AppText.Spanish);
        menu.DropDownItems.AddRange([english, spanish]);
        return menu;
    }

    private void ChangeLanguage(string language)
    {
        AppText.SetLanguage(language);
        _settings.Language = AppText.CurrentLanguage;
        _settingsStore.Save(_settings);
        var previous = _notifyIcon.ContextMenuStrip;
        _notifyIcon.ContextMenuStrip = BuildMenu();
        previous?.Dispose();
        _flyout?.SetPinned(_flyout.IsPinned);
        _flyout?.UpdateUsage(_latest, LocalizeFailure(_latestFailureKind), _latestIsStale);
        _usageBar?.UpdateUsage(_latest, LocalizeFailure(_latestFailureKind), _latestIsStale);
        _ = RefreshAsync();
    }

    private Forms.ToolStripMenuItem BuildNotificationsMenu()
    {
        var menu = new Forms.ToolStripMenuItem(AppText.Get("Notifications"));
        menu.DropDownItems.Add(CreateNotificationOption(
            AppText.Get("NotifyChange"),
            _settings.NotifyOnPercentChange,
            value => _settings.NotifyOnPercentChange = value));
        menu.DropDownItems.Add(new Forms.ToolStripSeparator());
        menu.DropDownItems.Add(CreateNotificationOption(
            AppText.Get("Notify50"),
            _settings.NotifyAt50Percent,
            value => _settings.NotifyAt50Percent = value));
        menu.DropDownItems.Add(CreateNotificationOption(
            AppText.Get("Notify75"),
            _settings.NotifyAt75Percent,
            value => _settings.NotifyAt75Percent = value));
        menu.DropDownItems.Add(CreateNotificationOption(
            AppText.Get("Notify90"),
            _settings.NotifyAt90Percent,
            value => _settings.NotifyAt90Percent = value));
        menu.DropDownItems.Add(new Forms.ToolStripSeparator());
        menu.DropDownItems.Add(CreateNotificationOption(
            AppText.Get("NotifyReset"),
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
            var previous = _latest;
            var state = UsageStatePolicy.Resolve(_latest, result, DateTimeOffset.Now, StaleAfter);
            _latest = state.Snapshot;
            _latestIsStale = state.IsStale;
            _latestFailureKind = state.FailureKind;

            if (_latest is { } snapshot)
            {
                var reason = state.FailureKind == UsageFailureKind.None ? null : LocalizeFailure(state.FailureKind);
                UpdateUsageDisplay(snapshot, state.IsStale, reason);
                if (result.Snapshot == snapshot)
                {
                    var notification = UsageNotificationEvaluator.Evaluate(previous, snapshot, _settings.ToNotificationOptions());
                    ShowUsageNotification(notification, snapshot);
                }
            }
            else
            {
                var failure = LocalizeFailure(state.FailureKind);
                _statusItem.Text = failure;
                _resetItem.Text = AppText.Get("RunTask");
                _notifyIcon.Text = TruncateTooltip($"Codex Usage Meter: {AppText.Get("NoData")}");
                _notifyIcon.Icon = ReplaceIcon(TrayIconFactory.Create(null));
                _flyout?.UpdateUsage(null, failure);
                _usageBar?.UpdateUsage(null, failure);
            }
        }
        finally
        {
            EnsureSessionWatcher();
            _isRefreshing = false;
        }
    }

    private void UpdateUsageDisplay(UsageSnapshot snapshot, bool stale, string? staleReason = null)
    {
        var available = (int)Math.Round(snapshot.AvailablePercent);
        _statusItem.Text = AppText.Get("Available", available, snapshot.UsedPercent.ToString("0.#", AppText.Culture));
        if (stale) _statusItem.Text += $" · {staleReason ?? AppText.Get("Stale")}";
        _resetItem.Text = snapshot.ResetsAt is { } reset
            ? AppText.Get("ResetAt", reset.ToLocalTime().ToString("g", AppText.Culture))
            : AppText.Get("NoReset");
        _notifyIcon.Text = TruncateTooltip($"Codex: {AppText.Get("AvailableText", available)}{(stale ? $" · {AppText.Get("Stale")}" : string.Empty)}");
        _notifyIcon.Icon = ReplaceIcon(TrayIconFactory.Create(snapshot.AvailablePercent));
        _flyout?.UpdateUsage(snapshot, staleReason, stale);
        _usageBar?.UpdateUsage(snapshot, staleReason, stale);
    }

    private static string LocalizeFailure(UsageFailureKind kind) => kind switch
    {
        UsageFailureKind.SessionsMissing => AppText.Get("SessionsMissing"),
        UsageFailureKind.NoSnapshots => AppText.Get("NoSnapshots"),
        UsageFailureKind.AccessDenied => AppText.Get("AccessDenied"),
        UsageFailureKind.ReadError => AppText.Get("ReadError"),
        _ => AppText.Get("Stale")
    };

    private void ShowUsageNotification(UsageNotification? notification, UsageSnapshot snapshot)
    {
        if (notification is null)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = notification.Kind switch
        {
            UsageNotificationKind.ThresholdReached => AppText.Get("Threshold", notification.Threshold),
            UsageNotificationKind.LimitReset => AppText.Get("LimitReset"),
            _ => AppText.Get("UsageChanged")
        };
        _notifyIcon.BalloonTipText = AppText.Get("AvailableUsed", snapshot.AvailablePercent.ToString("0", AppText.Culture), snapshot.UsedPercent.ToString("0.#", AppText.Culture));
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
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
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
            _pendingActivityPaths.Add(e.FullPath);
            _activityStateTimer.Stop();
            _activityStateTimer.Start();
            _fileChangeTimer.Stop();
            _fileChangeTimer.Start();
        });
    }

    private async Task ProcessPendingActivityStatesAsync()
    {
        var paths = _pendingActivityPaths.ToArray();
        _pendingActivityPaths.Clear();

        foreach (var path in paths)
        {
            await UpdateActivityStateAsync(path, updateTimer: false);
        }

        UpdateShineTimerState();
    }

    private async Task ReconcileActivityStatesAsync()
    {
        foreach (var path in _activeTaskFiles.Keys.ToArray())
        {
            if (!File.Exists(path))
            {
                _activeTaskFiles.Remove(path);
                continue;
            }

            await UpdateActivityStateAsync(path, updateTimer: false);
        }

        UpdateShineTimerState();
    }

    private async Task UpdateActivityStateAsync(string path, bool updateTimer = true)
    {
        var isActive = await ReadLatestTaskStateAsync(path);
        if (isActive is null) return;

        if (isActive.Value) _activeTaskFiles[path] = true;
        else _activeTaskFiles.Remove(path);

        if (updateTimer) UpdateShineTimerState();
    }

    private void UpdateShineTimerState()
    {
        if (_activeTaskFiles.Values.Any(active => active))
        {
            if (!_shineTimer.IsEnabled)
            {
                _flyout?.PlayShine(force: true);
                _usageBar?.PlayShine(force: true);
            }
            _shineTimer.Start();
        }
        else
        {
            _shineTimer.Stop();
        }
    }

    private static async Task<bool?> ReadLatestTaskStateAsync(string path)
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
            var bytesToRead = (int)Math.Min(stream.Length, 128 * 1024);
            stream.Seek(-bytesToRead, SeekOrigin.End);
            var buffer = new byte[bytesToRead];
            var read = await stream.ReadAsync(buffer);
            var lines = Encoding.UTF8.GetString(buffer, 0, read)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);

            for (var index = lines.Length - 1; index >= 0; index--)
            {
                try
                {
                    using var document = JsonDocument.Parse(lines[index].TrimEnd('\r'));
                    var root = document.RootElement;
                    if (!root.TryGetProperty("type", out var type) || type.GetString() != "event_msg" ||
                        !root.TryGetProperty("payload", out var payload) ||
                        !payload.TryGetProperty("type", out var payloadType)) continue;

                    var eventType = payloadType.GetString();
                    if (eventType == "task_started") return true;
                    if (eventType == "task_complete") return false;
                }
                catch (JsonException)
                {
                    // The first or last line may be incomplete while Codex is writing it.
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return null;
    }

    private void OnSessionWatcherError(object sender, ErrorEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _activeTaskFiles.Clear();
            _shineTimer.Stop();
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
            _notifyIcon.BalloonTipTitle = AppText.Get("BalloonUsage");
            _notifyIcon.BalloonTipText = AppText.Get("AvailableUsed", snapshot.AvailablePercent.ToString("0", AppText.Culture), snapshot.UsedPercent.ToString("0.#", AppText.Culture));
        }
        else
        {
            _notifyIcon.BalloonTipTitle = AppText.Get("BalloonUnavailable");
            _notifyIcon.BalloonTipText = AppText.Get("BalloonNoData");
        }
        _notifyIcon.ShowBalloonTip(4000);
    }

    private void ShowFlyout(bool pinned, bool activate = false)
    {
        if (!_settings.WidgetEnabled) return;
        _flyoutCloseTimer.Stop();
        EnsureFlyout();
        _flyout!.SetPinned(pinned || _settings.WidgetPinned);
        _flyout.SetMode(_settings.WidgetCompact, line: false);
        _flyout.UpdateUsage(_latest, LocalizeFailure(_latestFailureKind), _latestIsStale);

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
        if (_flyout.IsCompact) _flyout.PlayShine(force: true);
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
        if (enabled && _flyout is { IsVisible: true })
        {
            _flyout.SetMode(compact, line: false);
        }
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

    private void SetUsageBarEnabled(bool enabled)
    {
        _settings.UsageBarEnabled = enabled;
        _settingsStore.Save(_settings);
        if (enabled) ShowUsageBar();
        else _usageBar?.Hide();
    }

    private void ShowUsageBar()
    {
        if (!_settings.UsageBarEnabled) return;
        _usageBar ??= new UsageFlyoutWindow();
        _usageBar.SetPinned(true);
        _usageBar.SetMode(compact: false, line: true);
        _usageBar.UpdateUsage(_latest, LocalizeFailure(_latestFailureKind), _latestIsStale);
        PositionLineAboveTaskbar();
        _usageBar.Show();
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

    private void PositionLineAboveTaskbar()
    {
        if (_usageBar is null) return;
        var iconBounds = _notifyIcon.TryGetBounds(out var bounds)
            ? bounds
            : new Rectangle(Forms.Cursor.Position, new System.Drawing.Size(1, 1));
        var screen = Forms.Screen.FromRectangle(iconBounds);
        var source = PresentationSource.FromVisual(_usageBar);
        var scaleX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1d;
        var scaleY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1d;
        var work = screen.WorkingArea;
        _usageBar.Width = work.Width * scaleX;
        _usageBar.Left = work.Left * scaleX;
        _usageBar.Top = work.Bottom * scaleY - _usageBar.Height;
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

    private static string? ReadInstallerLanguage()
    {
        using var key = Registry.CurrentUser.OpenSubKey(AppRegistryPath);
        return key?.GetValue("InstallLanguage")?.ToString()?.ToLowerInvariant() switch
        {
            "spanish" or "es-es" => AppText.Spanish,
            "english" or "en-us" => AppText.English,
            _ => null
        };
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
        _activityStateTimer.Stop();
        _shineTimer.Stop();
        _flyoutCloseTimer.Stop();
        _sessionWatcher?.Dispose();
        _flyout?.Close();
        _usageBar?.Close();
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
