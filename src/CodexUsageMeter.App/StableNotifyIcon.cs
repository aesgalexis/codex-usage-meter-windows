using System.Drawing;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace CodexUsageMeter.App;

public sealed class StableNotifyIcon : IDisposable
{
    private static readonly Guid TrayIconGuid = new("B74147F2-B9A7-45B8-A59A-B6FAE11B0D43");
    private const uint CallbackMessage = 0x8001;
    private readonly TrayMessageWindow _window;
    private readonly Guid _trayIconGuid;
    private Icon? _icon;
    private string _text = string.Empty;
    private bool _visible;
    private bool _registered;
    private bool _useGuid = true;
    private bool _usesVersion4;
    private bool _disposed;

    public StableNotifyIcon() : this(TrayIconGuid)
    {
    }

    internal StableNotifyIcon(Guid trayIconGuid)
    {
        _trayIconGuid = trayIconGuid;
        _window = new TrayMessageWindow(HandleTrayMessage, () =>
        {
            _registered = false;
            AddToShell();
        });
    }

    public event Forms.MouseEventHandler? MouseClick;
    public event EventHandler? HoverOpened;
    public event EventHandler? HoverClosed;

    internal bool IsRegistered => _registered;
    internal bool UsesGuid => _useGuid;
    internal bool UsesVersion4 => _usesVersion4;

    public Forms.ContextMenuStrip? ContextMenuStrip { get; set; }

    public string BalloonTipTitle { get; set; } = string.Empty;

    public string BalloonTipText { get; set; } = string.Empty;

    public string Text
    {
        get => _text;
        set
        {
            _text = value ?? string.Empty;
            ModifyIcon();
        }
    }

    public Icon? Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            ModifyIcon();
        }
    }

    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value)
            {
                return;
            }

            _visible = value;
            if (value)
            {
                AddToShell();
            }
            else
            {
                DeleteFromShell();
            }
        }
    }

    public void ShowBalloonTip(int timeout)
    {
        if (!_visible || !_registered)
        {
            return;
        }

        var data = CreateData(NotifyIconFlags.Info | NotifyIconFlags.Guid);
        data.InfoTitle = BalloonTipTitle;
        data.Info = BalloonTipText;
        data.TimeoutOrVersion = (uint)Math.Max(timeout, 0);
        data.InfoFlags = 1;
        Shell_NotifyIcon(NotifyIconMessage.Modify, ref data);
    }

    public bool TryGetBounds(out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (!_registered) return false;
        var identifier = new NotifyIconIdentifier
        {
            Size = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
            WindowHandle = _window.Handle,
            Id = 1,
            GuidItem = _useGuid ? _trayIconGuid : Guid.Empty
        };
        if (Shell_NotifyIconGetRect(ref identifier, out var nativeBounds) != 0) return false;
        var rectangle = nativeBounds.ToRectangle();
        if (rectangle.Width <= 0 || rectangle.Height <= 0) return false;
        bounds = rectangle;
        return true;
    }

    public Point GetPhysicalCursorPosition()
    {
        return GetPhysicalCursorPos(out var position) ? position : Forms.Cursor.Position;
    }

    internal bool IsLeftClickInProgress() =>
        (GetAsyncKeyState(0x01) & 0x8000) != 0 && // VK_LBUTTON
        TryGetBounds(out var bounds) && bounds.Contains(GetPhysicalCursorPosition());

    private void AddToShell()
    {
        if (!_visible || _icon is null || _disposed || _registered)
        {
            return;
        }

        var data = CreateData(
            NotifyIconFlags.Message |
            NotifyIconFlags.Icon |
            NotifyIconFlags.Tip |
            NotifyIconFlags.Guid);
        _registered = Shell_NotifyIcon(NotifyIconMessage.Add, ref data);
        if (!_registered && _useGuid)
        {
            // Windows binds an unsigned icon GUID to its executable path. A portable
            // or development copy may need the HWND/ID identity instead.
            _useGuid = false;
            data = CreateData(NotifyIconFlags.Message | NotifyIconFlags.Icon | NotifyIconFlags.Tip);
            _registered = Shell_NotifyIcon(NotifyIconMessage.Add, ref data);
            if (!_registered) _useGuid = true;
        }
        if (_registered)
        {
            data.TimeoutOrVersion = 4;
            _usesVersion4 = Shell_NotifyIcon(NotifyIconMessage.SetVersion, ref data);
        }
    }

    private void ModifyIcon()
    {
        if (!_visible || _icon is null || _disposed)
        {
            return;
        }

        if (!_registered)
        {
            AddToShell();
            return;
        }
        var data = CreateData(NotifyIconFlags.Icon | NotifyIconFlags.Tip | NotifyIconFlags.Guid);
        if (!Shell_NotifyIcon(NotifyIconMessage.Modify, ref data))
        {
            _registered = false;
            AddToShell();
        }
    }

    private void DeleteFromShell()
    {
        if (!_registered || _window.Handle == IntPtr.Zero)
        {
            return;
        }

        var data = CreateData(NotifyIconFlags.Guid);
        Shell_NotifyIcon(NotifyIconMessage.Delete, ref data);
        _registered = false;
    }

    private NotifyIconData CreateData(NotifyIconFlags flags) => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(),
        WindowHandle = _window.Handle,
        Id = 1,
        // Version 4 suppresses the standard tooltip unless SHOWTIP is requested.
        // Preserve it for both registration paths and all modifications, including balloons.
        Flags = (_useGuid ? flags : flags & ~NotifyIconFlags.Guid) | NotifyIconFlags.ShowTip,
        CallbackMessage = CallbackMessage,
        IconHandle = _icon?.Handle ?? IntPtr.Zero,
        Tip = _text[..Math.Min(_text.Length, 127)],
        Info = string.Empty,
        InfoTitle = string.Empty,
        GuidItem = _useGuid ? _trayIconGuid : Guid.Empty
    };

    private void HandleTrayMessage(int message, IntPtr callbackPosition)
    {
        switch (message)
        {
            case 0x0406: // NIN_POPUPOPEN
                HoverOpened?.Invoke(this, EventArgs.Empty);
                break;
            case 0x0407: // NIN_POPUPCLOSE
                HoverClosed?.Invoke(this, EventArgs.Empty);
                break;
            case 0x0202 when !_usesVersion4: // Legacy WM_LBUTTONUP
            case 0x0400 when _usesVersion4: // NIN_SELECT
            case 0x0401 when _usesVersion4: // NIN_KEYSELECT (Enter/Space)
                // Version 4 can deliver both mouse-up and NIN_SELECT for one click.
                // Use the semantic event only, and use its icon anchor for keyboard input.
                var position = _usesVersion4 ? UnpackPosition(callbackPosition) : GetPhysicalCursorPosition();
                MouseClick?.Invoke(this, new Forms.MouseEventArgs(Forms.MouseButtons.Left, 1, position.X, position.Y, 0));
                break;
            case 0x0205 when !_usesVersion4: // Legacy WM_RBUTTONUP
            case 0x007B when _usesVersion4: // WM_CONTEXTMENU (mouse or keyboard)
                var cursor = GetPhysicalCursorPosition();
                // The version-4 callback does not define wParam coordinates for
                // WM_CONTEXTMENU. Keep mouse placement and anchor keyboard requests
                // at the icon when the pointer is elsewhere.
                var menuPosition = _usesVersion4 && TryGetBounds(out var bounds)
                    ? ResolveContextMenuPosition(cursor, bounds)
                    : cursor;
                ShowContextMenu(menuPosition);
                break;
        }
    }

    internal static Point UnpackPosition(IntPtr packedPosition)
    {
        var value = packedPosition.ToInt64();
        return new Point(unchecked((short)value), unchecked((short)(value >> 16)));
    }

    internal static Point ResolveContextMenuPosition(Point cursor, Rectangle iconBounds) =>
        iconBounds.Contains(cursor) ? cursor : new Point(iconBounds.Left, iconBounds.Bottom);

    private void ShowContextMenu(Point position)
    {
        if (ContextMenuStrip is null) return;
        SetForegroundWindow(_window.Handle);
        var previousDpiContext = SetThreadDpiAwarenessContext(new IntPtr(-4));
        try
        {
            ContextMenuStrip.Show(position);
            PostMessage(_window.Handle, 0, IntPtr.Zero, IntPtr.Zero);
        }
        finally
        {
            if (previousDpiContext != IntPtr.Zero) SetThreadDpiAwarenessContext(previousDpiContext);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DeleteFromShell();
        _visible = false;
        _disposed = true;
        ContextMenuStrip?.Dispose();
        _window.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class TrayMessageWindow : Forms.NativeWindow, IDisposable
    {
        private readonly Action<int, IntPtr> _messageHandler;
        private readonly Action _taskbarCreatedHandler;
        private readonly uint _taskbarCreatedMessage;

        public TrayMessageWindow(Action<int, IntPtr> messageHandler, Action taskbarCreatedHandler)
        {
            _messageHandler = messageHandler;
            _taskbarCreatedHandler = taskbarCreatedHandler;
            _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
            CreateHandle(new Forms.CreateParams { Caption = "CodexUsageMeter.TrayWindow" });
        }

        protected override void WndProc(ref Forms.Message message)
        {
            if ((uint)message.Msg == _taskbarCreatedMessage)
            {
                _taskbarCreatedHandler();
            }
            else if ((uint)message.Msg == CallbackMessage)
            {
                _messageHandler((int)(message.LParam.ToInt64() & 0xFFFF), message.WParam);
            }

            base.WndProc(ref message);
        }

        public void Dispose()
        {
            DestroyHandle();
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public IntPtr WindowHandle;
        public uint Id;
        public NotifyIconFlags Flags;
        public uint CallbackMessage;
        public IntPtr IconHandle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint TimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid GuidItem;
        public IntPtr BalloonIconHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyIconIdentifier
    {
        public uint Size;
        public IntPtr WindowHandle;
        public uint Id;
        public Guid GuidItem;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeIconRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [Flags]
    private enum NotifyIconFlags : uint
    {
        Message = 0x00000001,
        Icon = 0x00000002,
        Tip = 0x00000004,
        Info = 0x00000010,
        Guid = 0x00000020,
        ShowTip = 0x00000080
    }

    private enum NotifyIconMessage : uint
    {
        Add = 0x00000000,
        Modify = 0x00000001,
        Delete = 0x00000002,
        SetVersion = 0x00000004
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIcon(NotifyIconMessage message, ref NotifyIconData data);

    [DllImport("shell32.dll")]
    private static extern int Shell_NotifyIconGetRect(ref NotifyIconIdentifier identifier, out NativeIconRect iconLocation);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicalCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);
}
