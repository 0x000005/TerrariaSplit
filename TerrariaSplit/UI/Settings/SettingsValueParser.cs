using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit;

internal static class SettingsValueParser
{
    public static int ParseIntBox(TextBox textBox, int fallback, int minimum, int maximum)
    {
        return int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;
    }

    public static int ParseTimeBox(TextBox textBox, int fallbackSeconds, int minimumSeconds, int maximumSeconds)
    {
        return TimeText.TryParse(textBox.Text, out TimeSpan value)
            ? Math.Clamp((int)Math.Round(value.TotalSeconds), minimumSeconds, maximumSeconds)
            : fallbackSeconds;
    }

    public static float ParseFloatBox(TextBox textBox, float fallback, float minimum, float maximum)
    {
        return float.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;
    }

    public static decimal ParseRouteGroup(string? text)
    {
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
            ? Math.Clamp(value, 1m, 99m)
            : 1m;
    }
}
