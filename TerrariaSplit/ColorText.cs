using System.Drawing;

namespace TerrariaSplit;

internal static class ColorText
{
    public static Color Parse(string? value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            return ColorTranslator.FromHtml(value);
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    public static string Format(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
