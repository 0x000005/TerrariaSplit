using System.Drawing;

namespace TerrariaSplit.UI.Rendering;

internal static class ColorText
{
    public const string Transparent = "Transparent";

    public static bool IsTransparent(string? value)
    {
        return string.Equals(value?.Trim(), Transparent, StringComparison.OrdinalIgnoreCase);
    }

    public static Color Parse(string? value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        string text = value.Trim();
        if (IsTransparent(text))
        {
            return Color.Transparent;
        }

        if (TryParseArgbHex(text, out Color argbColor))
        {
            return argbColor;
        }

        try
        {
            return ColorTranslator.FromHtml(text);
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    public static string Format(Color color)
    {
        if (color.A == 0)
        {
            return Transparent;
        }

        if (color.A < 255)
        {
            return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static bool TryParseArgbHex(string value, out Color color)
    {
        color = Color.Empty;
        if (value.Length != 9 || value[0] != '#')
        {
            return false;
        }

        if (!int.TryParse(value.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out int alpha) ||
            !int.TryParse(value.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out int red) ||
            !int.TryParse(value.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out int green) ||
            !int.TryParse(value.AsSpan(7, 2), System.Globalization.NumberStyles.HexNumber, null, out int blue))
        {
            return false;
        }

        color = Color.FromArgb(alpha, red, green, blue);
        return true;
    }
}
