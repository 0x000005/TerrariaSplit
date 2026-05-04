namespace TerrariaSplit;

internal static class DeltaGradientCurves
{
    public const string Linear = "Linear";
    public const string Smooth = "Smooth";
    public const string HardStep = "HardStep";
    public const string SoftStep = "SoftStep";

    private static readonly IReadOnlyList<string> ids = new[]
    {
        Linear,
        Smooth,
        HardStep,
        SoftStep
    };

    public static IReadOnlyList<string> Ids => ids;

    public static string Normalize(string? id)
    {
        if (string.Equals(id, "Sine", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(id, "Smoothstep", StringComparison.OrdinalIgnoreCase))
        {
            return Smooth;
        }

        if (string.Equals(id, "Exponential", StringComparison.OrdinalIgnoreCase))
        {
            return Linear;
        }

        return ids.Any(candidate => string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase))
            ? ids.First(candidate => string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase))
            : SoftStep;
    }

    public static string GetDisplayName(string id)
    {
        return Normalize(id) switch
        {
            Linear => "Linear",
            Smooth => "Smooth",
            HardStep => "Hard step",
            SoftStep => "Soft step",
            _ => "Soft step"
        };
    }

    public static float Evaluate(string? id, float value)
    {
        float t = Math.Clamp(value, 0f, 1f);
        return Normalize(id) switch
        {
            Smooth => MathF.Sin(t * MathF.PI * 0.5f),
            HardStep => 0.4f + 0.6f * t,
            SoftStep => 0.2f + 0.8f * t,
            _ => t
        };
    }
}
