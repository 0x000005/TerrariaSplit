using System.Globalization;

namespace TerrariaSplit.Application;

public readonly record struct TerrariaWorldGenerationState(
    string? CurrentPassName,
    string? ProgressMessage,
    double? CurrentProgress,
    double? TotalProgress)
{
    public static TerrariaWorldGenerationState Unknown => new(
        null,
        null,
        null,
        null);

    public bool HasAnyData =>
        !string.IsNullOrWhiteSpace(CurrentPassName) ||
        !string.IsNullOrWhiteSpace(ProgressMessage) ||
        CurrentProgress.HasValue ||
        TotalProgress.HasValue;

    public string FormatProgressSummary()
    {
        if (!CurrentProgress.HasValue && !TotalProgress.HasValue)
        {
            return "Unknown";
        }

        string current = CurrentProgress.HasValue
            ? CurrentProgress.Value.ToString("P1", CultureInfo.InvariantCulture)
            : "?";
        string total = TotalProgress.HasValue
            ? TotalProgress.Value.ToString("P1", CultureInfo.InvariantCulture)
            : "?";
        return $"current {current}, total {total}";
    }
}
