using System.Drawing;
using TerrariaSplit.Configuration;
using TerrariaSplit.UI.Rendering;

namespace TerrariaSplit.UI;

internal static class RaceLeaderboardColorMath
{
    public static Color GetRankFillColor(
        int rank,
        int rowCount,
        RaceLeaderboardRankGradientColorSettings? gradient)
    {
        RaceLeaderboardRankGradientColorSettings defaults = new();
        gradient ??= defaults;
        Color start = ColorText.Parse(gradient.Start, ColorText.Parse(defaults.Start, Color.Gold));
        Color middle = ColorText.Parse(gradient.Middle, ColorText.Parse(defaults.Middle, Color.White));
        Color end = ColorText.Parse(gradient.End, ColorText.Parse(defaults.End, Color.Red));
        return GetRankFillColor(rank, rowCount, start, middle, end);
    }

    public static Color GetRankFillColor(
        int rank,
        int rowCount,
        Color start,
        Color middle,
        Color end)
    {
        if (rowCount <= 1)
        {
            return start;
        }

        float progress = Math.Clamp((rank - 1) / (float)(rowCount - 1), 0f, 1f);
        return progress <= 0.5f
            ? LerpColor(start, middle, progress * 2f)
            : LerpColor(middle, end, (progress - 0.5f) * 2f);
    }

    private static Color LerpColor(Color start, Color end, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            LerpByte(start.A, end.A, amount),
            LerpByte(start.R, end.R, amount),
            LerpByte(start.G, end.G, amount),
            LerpByte(start.B, end.B, amount));
    }

    private static int LerpByte(byte start, byte end, float amount)
    {
        return (int)MathF.Round(start + (end - start) * amount);
    }
}
