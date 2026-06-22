namespace TerrariaSplit.Configuration;

public static class RefreshRateSettings
{
    public static readonly int[] ReadyWatcherPollHzOptions = [120, 240, 480, 960];
    public static readonly int[] StandardRefreshHzOptions = [60, 90, 120, 180, 240];

    public const int MinReadyWatcherPollHz = 120;
    public const int MaxReadyWatcherPollHz = 960;
    public const int MinReadyUiControlHz = 60;
    public const int MaxReadyUiControlHz = 240;
    public const int MinRunningStatusPaintHz = 60;
    public const int MaxRunningStatusPaintHz = 240;
    public const int MinTimerOverlayRefreshHz = 60;
    public const int MaxTimerOverlayRefreshHz = 240;

    public static int NormalizeReadyWatcherPollHz(int value)
    {
        return NormalizeOption(value, ReadyWatcherPollHzOptions);
    }

    public static int NormalizeReadyUiControlHz(int value)
    {
        return NormalizeOption(value, StandardRefreshHzOptions);
    }

    public static int NormalizeRunningStatusPaintHz(int value)
    {
        return NormalizeOption(value, StandardRefreshHzOptions);
    }

    public static int NormalizeTimerOverlayRefreshHz(int value)
    {
        return NormalizeOption(value, StandardRefreshHzOptions);
    }

    public static TimeSpan ToInterval(int hz)
    {
        return TimeSpan.FromMilliseconds(1000d / Math.Max(1, hz));
    }

    private static int NormalizeOption(int value, IReadOnlyList<int> options)
    {
        if (options.Count == 0)
        {
            return Math.Max(1, value);
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
