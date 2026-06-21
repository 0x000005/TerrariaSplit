using System.Globalization;

namespace TerrariaSplit.Domain.Formatting;

internal static class TimeText
{
    public static bool TryParse(string? text, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string trimmed = text.Trim();
        string[] parts = trimmed.Split(':');
        if (parts.Length is < 2 or > 3)
        {
            return false;
        }

        if (!double.TryParse(parts[^1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double seconds))
        {
            return false;
        }

        if (!int.TryParse(parts[^2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes))
        {
            return false;
        }

        int hours = 0;
        if (parts.Length == 3 &&
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out hours))
        {
            return false;
        }

        if (minutes < 0 || minutes > 59 || seconds < 0 || seconds >= 60 || hours < 0)
        {
            return false;
        }

        time = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
        return true;
    }

    public static string FormatSplit(TimeSpan time)
    {
        int hours = (int)time.TotalHours;
        return hours > 0
            ? $"{hours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";
    }

    public static string FormatRecord(TimeSpan time)
    {
        int hours = (int)time.TotalHours;
        string ms = (time.Milliseconds / 10).ToString("00");
        return hours > 0
            ? $"{hours}:{time.Minutes:00}:{time.Seconds:00}.{ms}"
            : $"{time.Minutes}:{time.Seconds:00}.{ms}";
    }

    public static string FormatDelta(TimeSpan delta)
    {
        string sign = delta < TimeSpan.Zero ? "\u2212" : "+";
        TimeSpan value = delta.Duration();
        return sign + FormatDynamicDeltaBody(value);
    }

    public static string FormatDelta(TimeSpan delta, bool dynamicUnits)
    {
        string sign = delta < TimeSpan.Zero ? "\u2212" : "+";
        TimeSpan value = delta.Duration();
        return sign + (dynamicUnits
            ? FormatDynamicDeltaBody(value)
            : FormatWholeSecondDeltaBody(value));
    }

    public static bool IsDeltaDisplayedAsZero(TimeSpan delta, bool dynamicUnits)
    {
        TimeSpan value = delta.Duration();
        if (!dynamicUnits)
        {
            return RoundToNearest(value, 1d) == TimeSpan.Zero;
        }

        TimeSpan roundedToSecond = RoundToNearest(value, 1d);
        if (roundedToSecond.TotalMinutes >= 1d)
        {
            return roundedToSecond == TimeSpan.Zero;
        }

        TimeSpan roundedToTenth = RoundToNearest(value, 0.1d);
        if (roundedToTenth.TotalSeconds >= 10d)
        {
            return roundedToTenth == TimeSpan.Zero;
        }

        return RoundToNearest(value, 0.01d) == TimeSpan.Zero;
    }

    private static string FormatDynamicDeltaBody(TimeSpan value)
    {
        TimeSpan roundedToSecond = RoundToNearest(value, 1d);
        int roundedHours = (int)roundedToSecond.TotalHours;
        if (roundedHours > 0)
        {
            return $"{roundedHours}:{roundedToSecond.Minutes:00}:{roundedToSecond.Seconds:00}";
        }

        int roundedMinutes = (int)roundedToSecond.TotalMinutes;
        if (roundedMinutes > 0)
        {
            return $"{roundedMinutes}:{roundedToSecond.Seconds:00}";
        }

        TimeSpan roundedToTenth = RoundToNearest(value, 0.1d);
        if (roundedToTenth.TotalSeconds >= 10d)
        {
            return roundedToTenth.TotalSeconds.ToString("00.0", CultureInfo.InvariantCulture);
        }

        TimeSpan roundedToHundredth = RoundToNearest(value, 0.01d);
        return roundedToHundredth.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string FormatWholeSecondDeltaBody(TimeSpan value)
    {
        TimeSpan roundedToSecond = RoundToNearest(value, 1d);
        int hours = (int)roundedToSecond.TotalHours;
        if (hours > 0)
        {
            return $"{hours}:{roundedToSecond.Minutes:00}:{roundedToSecond.Seconds:00}";
        }

        int minutes = (int)roundedToSecond.TotalMinutes;
        return $"{minutes}:{roundedToSecond.Seconds:00}";
    }

    private static TimeSpan RoundToNearest(TimeSpan value, double secondsStep)
    {
        double roundedSeconds = Math.Round(
            value.TotalSeconds / secondsStep,
            MidpointRounding.AwayFromZero) * secondsStep;
        return TimeSpan.FromSeconds(roundedSeconds);
    }
}
