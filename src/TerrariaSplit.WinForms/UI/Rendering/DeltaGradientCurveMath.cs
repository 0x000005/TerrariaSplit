namespace TerrariaSplit.UI.Rendering;

internal static class DeltaGradientCurveMath
{
    public static float Evaluate(string? id, float value)
    {
        float t = Math.Clamp(value, 0f, 1f);
        return DeltaGradientCurves.Normalize(id) switch
        {
            DeltaGradientCurves.Smooth => MathF.Sin(t * MathF.PI * 0.5f),
            DeltaGradientCurves.HardStep => 0.4f + 0.6f * t,
            DeltaGradientCurves.SoftStep => 0.2f + 0.8f * t,
            _ => t
        };
    }
}
