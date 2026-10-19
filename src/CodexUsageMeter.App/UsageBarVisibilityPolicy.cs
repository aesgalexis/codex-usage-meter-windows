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

    public static bool IsFullScreen(
        Rectangle windowBounds,
        Rectangle screenBounds,
        bool isMaximized = false,
        bool hasWindowFrame = false) =>
        // A maximized browser can fill the monitor when the taskbar auto-hides.
        // Full-screen browsers remove their caption and resize frame.
        !(isMaximized && hasWindowFrame) &&
        windowBounds.Width > 0 && windowBounds.Height > 0 &&
        screenBounds.Width > 0 && screenBounds.Height > 0 &&
        windowBounds.Left <= screenBounds.Left &&
        windowBounds.Top <= screenBounds.Top &&
        windowBounds.Right >= screenBounds.Right &&
        windowBounds.Bottom >= screenBounds.Bottom;
}

internal static class NativeTaskbarState
{
    private const int MinimumVisibleTaskbarThickness = 3;
    private const int WindowStyleIndex = -16;
    private const int ExtendedWindowStyleIndex = -20;
    private const int CaptionStyle = 0x00C00000;
    private const int ThickFrameStyle = 0x00040000;
    private const int ToolWindowStyle = 0x00000080;
    private const int NoActivateStyle = 0x08000000;

    public static bool ShouldShowUsageBar(Forms.Screen screen) =>
        UsageBarVisibilityPolicy.ShouldShow(IsTaskbarVisible(screen), IsTopWindowFullScreenOn(screen));

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

    private static bool IsTopWindowFullScreenOn(Forms.Screen screen)
    {
        var fullScreen = false;
        // Inspect the top application on this display, even when keyboard focus is
        // on another monitor or temporarily belongs to our tray menu/widget.
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window) || IsIconic(window) || IsDesktopWindow(window) ||
                IsTaskbarWindow(window) || IsWindowCloaked(window)) return true;

            GetWindowThreadProcessId(window, out var processId);
            if (processId == (uint)Environment.ProcessId) return true;
            var extendedStyle = GetWindowLong(window, ExtendedWindowStyleIndex);
            if ((extendedStyle & (ToolWindowStyle | NoActivateStyle)) != 0) return true;

            var style = GetWindowLong(window, WindowStyleIndex);
            var hasWindowFrame = (style & (CaptionStyle | ThickFrameStyle)) != 0;
            if (!TryGetVisibleWindowRect(window, hasWindowFrame, out var bounds)) return true;
            if (!bounds.IntersectsWith(screen.Bounds)) return true;

            fullScreen = UsageBarVisibilityPolicy.IsFullScreen(bounds, screen.Bounds, IsZoomed(window), hasWindowFrame);
            // A full-screen window may span monitors. A normal window on another
            // display must not mask this display's full-screen application.
            return !fullScreen && !Forms.Screen.FromHandle(window).DeviceName.Equals(
                screen.DeviceName, StringComparison.OrdinalIgnoreCase);
        }, IntPtr.Zero);
        return fullScreen;
    }

    private static bool TryGetVisibleWindowRect(IntPtr window, bool hasWindowFrame, out Rectangle bounds)
    {
        // GetWindowRect includes the invisible resize border on maximized windows. The DWM
        // frame bounds do not, allowing a normal maximized window (which stops above the
        // taskbar) to be distinguished from a genuine full-screen window.
        // Borderless video windows use their actual window rectangle; there is
        // no invisible resize frame to exclude from the full-screen comparison.
        NativeRect nativeRect;
        if ((!hasWindowFrame || DwmGetWindowAttribute(
                window,
                DwmWindowAttribute.ExtendedFrameBounds,
                out nativeRect,
                Marshal.SizeOf<NativeRect>()) != 0) &&
            !GetWindowRect(window, out nativeRect))
        {
            bounds = Rectangle.Empty;
            return false;
        }

        bounds = Rectangle.FromLTRB(nativeRect.Left, nativeRect.Top, nativeRect.Right, nativeRect.Bottom);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static bool IsWindowCloaked(IntPtr window) =>
        DwmGetWindowAttribute(window, DwmWindowAttribute.Cloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

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
        ExtendedFrameBounds = 9,
        Cloaked = 14
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr window);

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

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr window,
        DwmWindowAttribute attribute,
        out int value,
        int valueSize);
}
