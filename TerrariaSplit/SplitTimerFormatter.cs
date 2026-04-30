namespace TerrariaSplit;

internal static class SplitTimerFormatter
{
    public static string Format(TimeSpan elapsed)
    {
        int hours = (int)elapsed.TotalHours;
        return $"{hours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds:000}";
    }
}
