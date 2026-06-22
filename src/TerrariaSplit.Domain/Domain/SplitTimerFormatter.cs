namespace TerrariaSplit.Domain;

public static class SplitTimerFormatter
{
    // The timer overlay formats elapsed time every frame; centiseconds only take
    // 100 distinct values and the main text only changes once per second, so both
    // are served from caches. The last-value cache is read and written from the
    // timer overlay and UI threads; the benign race only costs a recompute.
    private static readonly string[] CentisecondsTexts = BuildCentisecondsTexts();
    private static (long Seconds, string Text)? lastWithoutMilliseconds;

    public static string Format(TimeSpan elapsed)
    {
        return $"{FormatWithoutMilliseconds(elapsed)}{FormatMilliseconds(elapsed)}";
    }

    public static string FormatWithoutMilliseconds(TimeSpan elapsed)
    {
        long totalSeconds = (long)elapsed.TotalSeconds;
        (long Seconds, string Text)? cached = lastWithoutMilliseconds;
        if (cached is { } value && value.Seconds == totalSeconds)
        {
            return value.Text;
        }

        int hours = (int)elapsed.TotalHours;
        string text = hours > 0
            ? $"{hours}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
            : $"{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        lastWithoutMilliseconds = (totalSeconds, text);
        return text;
    }

    public static string FormatMilliseconds(TimeSpan elapsed)
    {
        return CentisecondsTexts[Math.Clamp(elapsed.Milliseconds / 10, 0, 99)];
    }

    private static string[] BuildCentisecondsTexts()
    {
        var texts = new string[100];
        for (int i = 0; i < texts.Length; i++)
        {
            texts[i] = $".{i:00}";
        }

        return texts;
    }
}
