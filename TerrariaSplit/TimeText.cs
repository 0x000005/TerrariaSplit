using System.Globalization;

namespace TerrariaSplit;

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
        int hours = (int)value.TotalHours;

        string body = hours > 0
            ? $"{hours}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes}:{value.Seconds:00}";

        return sign + body;
    }
}
