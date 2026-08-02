using System.Drawing;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace CodexUsageMeter.App;

public sealed class StableNotifyIcon : IDisposable
{
    private static readonly Guid TrayIconGuid = new("B74147F2-B9A7-45B8-A59A-B6FAE11B0D43");
    private const uint CallbackMessage = 0x8001;
    private readonly TrayMessageWindow _window;
    private Icon? _icon;
    private string _text = string.Empty;
    private bool _visible;
    private bool _disposed;

    public StableNotifyIcon()
    {
        _window = new TrayMessageWindow(HandleTrayMessage, AddToShell);
    }

    public event Forms.MouseEventHandler? MouseClick;

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
        if (!_visible)
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

    private void AddToShell()
    {
        if (!_visible || _icon is null || _disposed)
        {
            return;
        }

        var data = CreateData(
            NotifyIconFlags.Message |
            NotifyIconFlags.Icon |
            NotifyIconFlags.Tip |
            NotifyIconFlags.Guid);
        if (Shell_NotifyIcon(NotifyIconMessage.Add, ref data))
        {
            data.TimeoutOrVersion = 4;
            Shell_NotifyIcon(NotifyIconMessage.SetVersion, ref data);
        }
    }

    private void ModifyIcon()
    {
        if (!_visible || _icon is null || _disposed)
        {
            return;
        }

        var data = CreateData(NotifyIconFlags.Icon | NotifyIconFlags.Tip | NotifyIconFlags.Guid);
        Shell_NotifyIcon(NotifyIconMessage.Modify, ref data);
    }

    private void DeleteFromShell()
    {
        if (_window.Handle == IntPtr.Zero)
        {
            return;
        }

        var data = CreateData(NotifyIconFlags.Guid);
        Shell_NotifyIcon(NotifyIconMessage.Delete, ref data);
    }

    private NotifyIconData CreateData(NotifyIconFlags flags) => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(),
        WindowHandle = _window.Handle,
        Id = 1,
        Flags = flags,
        CallbackMessage = CallbackMessage,
        IconHandle = _icon?.Handle ?? IntPtr.Zero,
        Tip = _text[..Math.Min(_text.Length, 127)],
        Info = string.Empty,
        InfoTitle = string.Empty,
        GuidItem = TrayIconGuid
    };

    private void HandleTrayMessage(int message)
    {
        var position = Forms.Cursor.Position;
        switch (message)
        {
            case 0x0202: // WM_LBUTTONUP
                MouseClick?.Invoke(this, new Forms.MouseEventArgs(Forms.MouseButtons.Left, 1, position.X, position.Y, 0));
                break;
            case 0x0205: // WM_RBUTTONUP
                if (ContextMenuStrip is not null)
                {
                    SetForegroundWindow(_window.Handle);
                    ContextMenuStrip.Show(position);
                    PostMessage(_window.Handle, 0, IntPtr.Zero, IntPtr.Zero);
                }
                break;
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
        private readonly Action<int> _messageHandler;
        private readonly Action _taskbarCreatedHandler;
        private readonly uint _taskbarCreatedMessage;

        public TrayMessageWindow(Action<int> messageHandler, Action taskbarCreatedHandler)
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
                _messageHandler((int)(message.LParam.ToInt64() & 0xFFFF));
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

    [Flags]
    private enum NotifyIconFlags : uint
    {
        Message = 0x00000001,
        Icon = 0x00000002,
        Tip = 0x00000004,
        Info = 0x00000010,
        Guid = 0x00000020
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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);
}
