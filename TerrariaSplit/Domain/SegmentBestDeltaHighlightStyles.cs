using System.Drawing;

namespace TerrariaSplit;

internal static class SegmentBestDeltaHighlightStyles
{
    public const string None = "None";
    public const string Breathe = "Breathe";
    public const string Rainbow = "Rainbow";
    public const string Aurora = "Aurora";
    public const string Gold = "Gold";

    private static readonly IReadOnlyList<string> ids = new[]
    {
        None,
        Breathe,
        Rainbow,
        Aurora,
        Gold
    };

    public static IReadOnlyList<string> Ids => ids;

    public static string Normalize(string? id)
    {
        return ids.Any(candidate => string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase))
            ? ids.First(candidate => string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase))
            : None;
    }

    public static string GetDisplayName(string id)
    {
        return Normalize(id) switch
        {
            None => "None",
            Breathe => "Breathe",
            Rainbow => "Rainbow",
            Aurora => "Aurora",
            Gold => "Gold",
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

        float wave = (MathF.Sin((float)(seconds * Math.Tau)) + 1f) * 0.5f;
        return style switch
        {
            Breathe => Blend(baseColor, Color.White, 0.20f + wave * 0.34f),
            Rainbow => Blend(baseColor, FromHsv((float)(seconds * 150.0), 0.78f, 1f), 0.36f),
            Aurora => Blend(baseColor, FromHsv(166f + (float)((seconds * 95.0) % 126.0), 0.58f, 1f), 0.34f),
            Gold => Blend(baseColor, FromHsv(48f, 0.42f, 1f), 0.38f + wave * 0.18f),
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
