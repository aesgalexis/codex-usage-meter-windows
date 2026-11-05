using System.Drawing;
using CodexUsageMeter.App;

internal static class DisplayRegressionChecks
{
    internal static void Run()
    {
        Rectangle[] screens = [new(0, 0, 1920, 1040), new(-1920, -200, 1920, 1080)];
        Require(NativeWindowPositioning.IsWidgetReachable(new(100, 100, 420, 240), screens),
            "A widget on the primary display must retain its position.");
        Require(NativeWindowPositioning.IsWidgetReachable(new(-1800, -100, 420, 240), screens),
            "Negative coordinates on a connected display are valid.");
        Require(!NativeWindowPositioning.IsWidgetReachable(new(-1800, -100, 420, 240), screens.Take(1)),
            "A saved position on a disconnected display needs recovery.");
        Require(!NativeWindowPositioning.IsWidgetReachable(new(100, -200, 420, 240), screens),
            "A visible lower edge is insufficient if the drag area is inaccessible.");
        Require(!NativeWindowPositioning.IsWidgetReachable(new(1900, 100, 420, 240), screens),
            "A tiny visible sliver must not leave the widget stranded.");
        Require(!NativeWindowPositioning.IsWidgetReachable(Rectangle.Empty, screens),
            "An empty window cannot be reachable.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
