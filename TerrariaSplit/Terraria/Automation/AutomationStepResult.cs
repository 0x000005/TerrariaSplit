using System.Drawing;

namespace TerrariaSplit.Terraria.Automation;

internal readonly record struct AutomationStepResult(
    string Step,
    bool Success,
    Point? ClientPoint = null,
    Size? ClientSize = null,
    string? Detail = null)
{
    public string ToLogMessage()
    {
        List<string> parts = new()
        {
            $"Terraria automation step '{Step}' {(Success ? "succeeded" : "failed")}"
        };

        if (ClientPoint is Point point)
        {
            parts.Add($"client ({point.X}, {point.Y})");
        }

        if (ClientSize is Size size && !size.IsEmpty)
        {
            parts.Add($"client size {size.Width}x{size.Height}");
        }

        if (!string.IsNullOrWhiteSpace(Detail))
        {
            parts.Add(Detail);
        }

        return string.Join("; ", parts) + ".";
    }
}
