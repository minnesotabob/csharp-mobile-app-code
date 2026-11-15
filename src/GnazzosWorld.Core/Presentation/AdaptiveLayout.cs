namespace GnazzosWorld.Core.Presentation;

public static class AdaptiveLayout
{
    // Available width in device-independent units, not a device-family test.
    public const double WideBreakpoint = 900;
    public static bool IsWide(double width)
        => double.IsFinite(width) && width >= WideBreakpoint;
}
