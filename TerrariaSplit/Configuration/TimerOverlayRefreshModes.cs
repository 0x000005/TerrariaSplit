namespace TerrariaSplit;

internal static class TimerOverlayRefreshModes
{
    public const string Auto = "Auto";
    public const string Fixed = "Fixed";

    public static readonly string[] All = [Auto, Fixed];

    public static string Normalize(string? value)
    {
        return All.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)) ?? Auto;
    }

    public static double ResolveTargetHz(AdvancedSettings? settings, int detectedDisplayHz)
    {
        string mode = Normalize(settings?.TimerOverlayRefreshMode);
        if (string.Equals(mode, Fixed, StringComparison.OrdinalIgnoreCase))
        {
            return RefreshRateSettings.NormalizeTimerOverlayRefreshHz(
                settings?.TimerOverlayRefreshHz ?? AdvancedSettings.DefaultTimerOverlayRefreshHz);
        }

        return Math.Clamp(detectedDisplayHz <= 0 ? 60 : detectedDisplayHz, 30, 240);
    }

    public static TimeSpan ResolveInterval(AdvancedSettings? settings, int detectedDisplayHz)
    {
        double targetHz = ResolveTargetHz(settings, detectedDisplayHz);
        double milliseconds = 1000d / Math.Max(1d, targetHz);
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
