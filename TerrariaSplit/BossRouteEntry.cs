namespace TerrariaSplit;

internal sealed class BossRouteEntry
{
    public string BossId { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Segment { get; set; } = 1;
}
