using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using Forms = System.Windows.Forms;

namespace CodexUsageMeter.App;

public static class UsageBarVisibilityPolicy
{
    public static bool ShouldShow(
        bool taskbarVisible,
        bool foregroundWindowFullScreen,
        bool foregroundWindowIsDesktop = false) =>
        taskbarVisible && (!foregroundWindowFullScreen || foregroundWindowIsDesktop);

    public static bool IsFullScreen(Rectangle windowBounds, Rectangle screenBounds) =>
        windowBounds.Left <= screenBounds.Left &&
        windowBounds.Top <= screenBounds.Top &&
        windowBounds.Right >= screenBounds.Right &&
        windowBounds.Bottom >= screenBounds.Bottom;
}

internal static class NativeTaskbarState
{
    private const int MinimumVisibleTaskbarThickness = 3;

    public static bool ShouldShowUsageBar(Forms.Screen screen) =>
        UsageBarVisibilityPolicy.ShouldShow(IsTaskbarVisible(screen), IsForegroundWindowFullScreenOn(screen));

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

    private static bool IsForegroundWindowFullScreenOn(Forms.Screen screen)
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || !IsWindowVisible(foreground)) return false;
        if (IsDesktopWindow(foreground)) return false;

        GetWindowThreadProcessId(foreground, out var processId);
        if (processId == (uint)Environment.ProcessId) return false;
        if (!Forms.Screen.FromHandle(foreground).DeviceName.Equals(screen.DeviceName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!TryGetVisibleWindowRect(foreground, out var windowBounds)) return false;

        return UsageBarVisibilityPolicy.IsFullScreen(windowBounds, screen.Bounds);
    }

    private static bool TryGetVisibleWindowRect(IntPtr window, out Rectangle bounds)
    {
        // GetWindowRect includes the invisible resize border on maximized windows. The DWM
        // frame bounds do not, allowing a normal maximized window (which stops above the
        // taskbar) to be distinguished from a genuine full-screen window.
        if (DwmGetWindowAttribute(
                window,
                DwmWindowAttribute.ExtendedFrameBounds,
                out var nativeRect,
                Marshal.SizeOf<NativeRect>()) != 0 &&
            !GetWindowRect(window, out nativeRect))
        {
            bounds = Rectangle.Empty;
            return false;
        }

        bounds = Rectangle.FromLTRB(nativeRect.Left, nativeRect.Top, nativeRect.Right, nativeRect.Bottom);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static bool IsTaskbarWindow(IntPtr window)
    {
        return GetWindowClassName(window) is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
    }

    private static bool IsDesktopWindow(IntPtr window) =>
        GetWindowClassName(window) is "Progman" or "WorkerW";

    private static string GetWindowClassName(IntPtr window)
    {
        var className = new StringBuilder(64);
        _ = GetClassName(window, className, className.Capacity);
        return className.ToString();
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

    private enum DwmWindowAttribute
    {
        ExtendedFrameBounds = 9
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
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr window,
        DwmWindowAttribute attribute,
        out NativeRect value,
        int valueSize);
}
