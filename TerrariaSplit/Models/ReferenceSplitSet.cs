namespace TerrariaSplit.Models;

internal sealed class ReferenceSplitSet
{
    public string Name { get; set; } = "WR";
    public Dictionary<string, string> Splits { get; set; } = new();
}
