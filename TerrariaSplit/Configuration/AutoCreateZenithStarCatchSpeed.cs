using System.Globalization;

namespace TerrariaSplit;

internal static class AutoCreateZenithStarCatchSpeed
{
    public const int MinimumSliderValue = 0;
    public const int MaximumSliderValue = 1000;
    public static readonly int DefaultSliderValue = FindNearestSliderValue(5d);

    private const double MinimumMultiplier = 1d;
    private const double MaximumMultiplier = 50d;

    public static int NormalizeSliderValue(int value)
    {
        return Math.Clamp(value, MinimumSliderValue, MaximumSliderValue);
    }

    public static double GetMultiplier(int sliderValue)
    {
        int normalized = NormalizeSliderValue(sliderValue);
        double t = (double)(normalized - MinimumSliderValue) / (MaximumSliderValue - MinimumSliderValue);
        double logValue = Math.Log(MinimumMultiplier) + (Math.Log(MaximumMultiplier) - Math.Log(MinimumMultiplier)) * t;
        return QuantizeMultiplier(Math.Exp(logValue));
    }

    public static string FormatMultiplier(int sliderValue)
    {
        return GetMultiplier(sliderValue).ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static int FindNearestSliderValue(double multiplier)
    {
        double target = Math.Clamp(multiplier, MinimumMultiplier, MaximumMultiplier);
        int bestSliderValue = MinimumSliderValue;
        double bestDisplayedDistance = double.MaxValue;
        double bestRawDistance = double.MaxValue;

        for (int sliderValue = MinimumSliderValue; sliderValue <= MaximumSliderValue; sliderValue++)
        {
            double displayedDistance = Math.Abs(GetMultiplier(sliderValue) - target);
            double rawDistance = Math.Abs(GetRawMultiplier(sliderValue) - target);
            if (displayedDistance < bestDisplayedDistance ||
                (Math.Abs(displayedDistance - bestDisplayedDistance) < 0.0000001d && rawDistance < bestRawDistance))
            {
                bestSliderValue = sliderValue;
                bestDisplayedDistance = displayedDistance;
                bestRawDistance = rawDistance;
            }
        }

        return bestSliderValue;
    }

    private static double QuantizeMultiplier(double multiplier)
    {
        multiplier = Math.Clamp(multiplier, MinimumMultiplier, MaximumMultiplier);
        double step = multiplier switch
        {
            <= 5d => 0.1d,
            <= 10d => 0.2d,
            <= 20d => 0.5d,
            _ => 1d
        };

        return Math.Round(multiplier / step, MidpointRounding.AwayFromZero) * step;
    }

    private static double GetRawMultiplier(int sliderValue)
    {
        int normalized = NormalizeSliderValue(sliderValue);
        double t = (double)(normalized - MinimumSliderValue) / (MaximumSliderValue - MinimumSliderValue);
        double logValue = Math.Log(MinimumMultiplier) + (Math.Log(MaximumMultiplier) - Math.Log(MinimumMultiplier)) * t;
        return Math.Exp(logValue);
    }
}
