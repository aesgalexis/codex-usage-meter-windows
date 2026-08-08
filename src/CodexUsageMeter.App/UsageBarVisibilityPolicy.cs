using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using Forms = System.Windows.Forms;

namespace CodexUsageMeter.App;

public static class UsageBarVisibilityPolicy
{
    public static bool ShouldShow(bool taskbarVisible, bool foregroundWindowMaximized) =>
        taskbarVisible && !foregroundWindowMaximized;
}

internal static class NativeTaskbarState
{
    private const int MinimumVisibleTaskbarThickness = 3;

    public static bool ShouldShowUsageBar(Forms.Screen screen) =>
        UsageBarVisibilityPolicy.ShouldShow(
            IsTaskbarVisible(screen),
            IsForegroundWindowMaximizedOn(screen));

    private static bool IsTaskbarVisible(Forms.Screen screen)
    {
        var foundVisibleTaskbar = false;
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window) || !IsTaskbarWindow(window) || !GetWindowRect(window, out var nativeRect))
                return true;

            var taskbar = Rectangle.FromLTRB(nativeRect.Left, nativeRect.Top, nativeRect.Right, nativeRect.Bottom);
            var intersection = Rectangle.Intersect(taskbar, screen.Bounds);
            if (intersection.Width >= MinimumVisibleTaskbarThickness &&
                intersection.Height >= MinimumVisibleTaskbarThickness &&
                Forms.Screen.FromHandle(window).DeviceName.Equals(screen.DeviceName, StringComparison.OrdinalIgnoreCase))
            {
                foundVisibleTaskbar = true;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return foundVisibleTaskbar;
    }

    private static bool IsForegroundWindowMaximizedOn(Forms.Screen screen)
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || !IsWindowVisible(foreground)) return false;

        GetWindowThreadProcessId(foreground, out var processId);
        if (processId == (uint)Environment.ProcessId) return false;
        if (!Forms.Screen.FromHandle(foreground).DeviceName.Equals(screen.DeviceName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (IsZoomed(foreground)) return true;
        if (!GetWindowRect(foreground, out var nativeRect)) return false;

        var bounds = screen.Bounds;
        return nativeRect.Left <= bounds.Left &&
               nativeRect.Top <= bounds.Top &&
               nativeRect.Right >= bounds.Right &&
               nativeRect.Bottom >= bounds.Bottom;
    }

    private static bool IsTaskbarWindow(IntPtr window)
    {
        var className = new StringBuilder(64);
        _ = GetClassName(window, className, className.Capacity);
        return className.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
    }

    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern int GetClassName(IntPtr window, StringBuilder className, int maximumCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
