namespace TerrariaSplit;

internal static class RefreshRateSettings
{
    public static readonly int[] ReadyWatcherPollHzOptions = [100, 200, 400, 800];
    public static readonly int[] StandardRefreshHzOptions = [50, 100, 150, 200];

    public const int MinReadyWatcherPollHz = 100;
    public const int MaxReadyWatcherPollHz = 800;
    public const int MinReadyUiControlHz = 50;
    public const int MaxReadyUiControlHz = 200;
    public const int MinRunningStatusPaintHz = 50;
    public const int MaxRunningStatusPaintHz = 200;
    public const int MinTimerOverlayRefreshHz = 50;
    public const int MaxTimerOverlayRefreshHz = 200;

    public static int NormalizeReadyWatcherPollHz(int value)
    {
        return NormalizeOption(value, ReadyWatcherPollHzOptions, AdvancedSettings.DefaultReadyWatcherPollHz);
    }

    public static int NormalizeReadyUiControlHz(int value)
    {
        return NormalizeOption(value, StandardRefreshHzOptions, AdvancedSettings.DefaultReadyUiControlHz);
    }

    public static int NormalizeRunningStatusPaintHz(int value)
    {
        return NormalizeOption(value, StandardRefreshHzOptions, AdvancedSettings.DefaultRunningStatusPaintHz);
    }

    public static int NormalizeTimerOverlayRefreshHz(int value)
    {
        return NormalizeOption(value, StandardRefreshHzOptions, AdvancedSettings.DefaultTimerOverlayRefreshHz);
    }

    public static TimeSpan ToInterval(int hz)
    {
        return TimeSpan.FromMilliseconds(1000d / Math.Max(1, hz));
    }

    private static int NormalizeOption(int value, IReadOnlyList<int> options, int fallback)
    {
        if (options.Count == 0)
        {
            return fallback;
        }

        int normalized = options[0];
        int bestDistance = Math.Abs(value - normalized);
        for (int i = 1; i < options.Count; i++)
        {
            int candidate = options[i];
            int distance = Math.Abs(value - candidate);
            if (distance < bestDistance || (distance == bestDistance && candidate > normalized))
            {
                normalized = candidate;
                bestDistance = distance;
            }
        }

        return normalized;
    }
}
