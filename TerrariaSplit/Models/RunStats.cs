namespace TerrariaSplit;

internal sealed class RunStats
{
    public Dictionary<string, string> LastRunSplits { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
