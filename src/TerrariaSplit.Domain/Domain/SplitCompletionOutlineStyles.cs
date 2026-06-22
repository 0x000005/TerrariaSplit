using System.Drawing;

namespace TerrariaSplit.Domain;

public static class SplitCompletionOutlineStyles
{
    public const string None = "None";
    public const string Rainbow = "Rainbow";
    public const string Aurora = "Aurora";
    public const string Gold = "Gold";

    private static readonly IReadOnlyList<string> ids = new[]
    {
        None,
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
            Rainbow => "Rainbow",
            Aurora => "Aurora",
            Gold => "Gold",
            _ => "None"
        };
    }

    public static Color[] GetColors(string id, double seconds)
    {
        string style = Normalize(id);
        float shift = (float)(seconds * GetSpeed(style));
        return style switch
        {
            Rainbow => Hues(shift, 0.84f, 1f, 0f, 95f, 190f, 285f),
            Aurora => AuroraColors(shift),
            Gold => GoldColors(shift),
            _ => new[] { Color.Transparent, Color.Transparent, Color.Transparent, Color.Transparent }
        };
    }

    private static double GetSpeed(string style)
    {
        return style switch
        {
            Rainbow => 150.0,
            Aurora => 142.5,
            Gold => 165.0,
            _ => 0.0
        };
    }

    private static Color[] GoldColors(float shift)
    {
        float phase = ((shift / 360f) % 1f + 1f) % 1f;
        float[] stops = { 0f, 0.12f, 0.24f, 0.38f, 0.50f, 0.62f, 0.76f, 0.88f, 1f };
        var colors = new Color[stops.Length];
        for (int i = 0; i < stops.Length; i++)
        {
            float distance = MathF.Abs(stops[i] - phase);
            distance = MathF.Min(distance, 1f - distance);
            float highlight = MathF.Pow(MathF.Max(0f, 1f - distance / 0.18f), 1.45f);
            float secondary = MathF.Pow(MathF.Max(0f, 1f - MathF.Abs(distance - 0.28f) / 0.16f), 2.0f);
            float sparkle = (MathF.Sin((shift * 4.2f + stops[i] * 720f) * MathF.PI / 180f) + 1f) * 0.5f;
            float value = Math.Clamp(0.86f + highlight * 0.14f + sparkle * 0.08f - secondary * 0.10f, 0f, 1f);
            float saturation = Math.Clamp(0.58f - highlight * 0.34f + secondary * 0.18f, 0f, 1f);
            colors[i] = FromHsv(48f, saturation, value);
        }

        return colors;
    }

    private static Color[] AuroraColors(float shift)
    {
        float phase = ((shift / 360f) % 1f + 1f) % 1f;
        Color[] palette =
        {
            FromHsv(166f, 0.64f, 0.98f),
            FromHsv(196f, 0.66f, 1.00f),
            FromHsv(226f, 0.62f, 0.98f),
            FromHsv(262f, 0.54f, 0.96f),
            FromHsv(292f, 0.42f, 0.98f)
        };

        float[] stops = { 0f, 0.25f, 0.5f, 0.75f, 1f };
        var colors = new Color[stops.Length];
        for (int i = 0; i < stops.Length; i++)
        {
            colors[i] = SampleLoop(palette, stops[i] + phase);
        }

        return colors;
    }

    private static Color SampleLoop(Color[] palette, float position)
    {
        float wrapped = ((position % 1f) + 1f) % 1f;
        float scaled = wrapped * palette.Length;
        int index = (int)MathF.Floor(scaled) % palette.Length;
        int next = (index + 1) % palette.Length;
        float amount = scaled - MathF.Floor(scaled);
        return Lerp(palette[index], palette[next], amount);
    }

    private static Color Lerp(Color a, Color b, float amount)
    {
        return Color.FromArgb(
            (int)MathF.Round(a.R + (b.R - a.R) * amount),
            (int)MathF.Round(a.G + (b.G - a.G) * amount),
            (int)MathF.Round(a.B + (b.B - a.B) * amount));
    }

    private static Color[] Hues(float baseHue, float saturation, float value, params float[] offsets)
    {
        return offsets.Select(offset => FromHsv(baseHue + offset, saturation, value)).ToArray();
    }

    private static Color[] Colors(params string[] hex)
    {
        return hex.Select(ColorTranslator.FromHtml).ToArray();
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
            (int)Math.Round((r + m) * 255),
            (int)Math.Round((g + m) * 255),
            (int)Math.Round((b + m) * 255));
    }
}
