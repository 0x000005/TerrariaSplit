namespace TerrariaSplit;

internal static class SplitTimerFormatter
{
    public static string Format(TimeSpan elapsed)
    {
        return $"{FormatWithoutMilliseconds(elapsed)}{FormatMilliseconds(elapsed)}";
    }

    public static string FormatWithoutMilliseconds(TimeSpan elapsed)
    {
        int hours = (int)elapsed.TotalHours;
        return hours > 0
            ? $"{hours}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
            : $"{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    public static string FormatMilliseconds(TimeSpan elapsed)
    {
        return $".{elapsed.Milliseconds / 10:00}";
    }
}
