using System.Drawing;

namespace TerrariaSplit.Domain;

public static class SegmentBestDeltaHighlightStyles
{
    public const string None = "None";
    public const string Rainbow = "Rainbow";
    public const string Aurora = "Aurora";

    private static readonly IReadOnlyList<string> ids = new[]
    {
        None,
        Rainbow,
        Aurora
    };

    public static IReadOnlyList<string> Ids => ids;

    public static string Normalize(string? id)
    {
        if (string.Equals(id, "Breathe", StringComparison.OrdinalIgnoreCase))
        {
            return Aurora;
        }

        return ids.Any(candidate => string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase))
            ? ids.First(candidate => string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase))
            : None;
    }

    public static string GetDisplayName(string id)
    {
        return Normalize(id) switch
        {
            None => "None",
            Rainbow => "Neon",
            Aurora => "Breathe",
            _ => "None"
        };
    }

    public static Color Apply(Color baseColor, string id, double seconds)
    {
        string style = Normalize(id);
        if (style == None)
        {
            return baseColor;
        }

        return style switch
        {
            Rainbow => Blend(baseColor, FromHsv((float)(seconds * 150.0), 0.78f, 1f), 0.36f),
            Aurora => Blend(baseColor, FromHsv(229f + MathF.Sin((float)(seconds * 1.45)) * 63f, 0.58f, 1f), 0.34f),
            _ => baseColor
        };
    }

    private static Color Blend(Color a, Color b, float amount)
    {
        float t = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            (int)MathF.Round(a.R + (b.R - a.R) * t),
            (int)MathF.Round(a.G + (b.G - a.G) * t),
            (int)MathF.Round(a.B + (b.B - a.B) * t));
    }

    private static Color FromHsv(float hue, float saturation, float value)
    {
        float h = ((hue % 360f) + 360f) % 360f;
        float c = value * saturation;
        float x = c * (1f - Math.Abs((h / 60f) % 2f - 1f));
        float m = value - c;

        (float r, float g, float b) = h switch
        {
            < 60f => (c, x, 0f),
            < 120f => (x, c, 0f),
            < 180f => (0f, c, x),
            < 240f => (0f, x, c),
            < 300f => (x, 0f, c),
            _ => (c, 0f, x)
        };

        return Color.FromArgb(
            (int)MathF.Round((r + m) * 255),
            (int)MathF.Round((g + m) * 255),
            (int)MathF.Round((b + m) * 255));
    }
}
