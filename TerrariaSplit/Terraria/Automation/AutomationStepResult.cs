using System.Drawing;

namespace TerrariaSplit;

internal readonly record struct AutomationStepResult(
    string Step,
    bool Success,
    Point? ClientPoint = null,
    Size? ClientSize = null,
    string? ExpectedMenuModes = null,
    int? LastMenuMode = null,
    string? Detail = null)
{
    public string ToLogMessage()
    {
        List<string> parts = new()
        {
            $"Create world automation step '{Step}' {(Success ? "succeeded" : "failed")}"
        };

        if (ClientPoint is Point point)
        {
            parts.Add($"client ({point.X}, {point.Y})");
        }

        if (ClientSize is Size size && !size.IsEmpty)
        {
            parts.Add($"client size {size.Width}x{size.Height}");
        }

        if (!string.IsNullOrWhiteSpace(ExpectedMenuModes))
        {
            parts.Add($"expected menuMode [{ExpectedMenuModes}]");
        }

        if (LastMenuMode.HasValue)
        {
            parts.Add($"last menuMode {LastMenuMode.Value}");
        }
        else if (!Success && ExpectedMenuModes is not null)
        {
            parts.Add("last menuMode unavailable");
        }

        if (!string.IsNullOrWhiteSpace(Detail))
        {
            parts.Add(Detail);
        }

        return string.Join("; ", parts) + ".";
    }
}
