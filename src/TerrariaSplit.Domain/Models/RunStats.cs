namespace TerrariaSplit.Models;

internal sealed class RunStats
{
    public Dictionary<string, string> LastRunSplits { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
