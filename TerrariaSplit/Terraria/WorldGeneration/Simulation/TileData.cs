namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

internal struct TileData
{
    private ushort type;
    private ushort wall;
    private byte liquid;
    private byte liquidType;
    private bool active;

    public ushort Type
    {
        readonly get => type;
        set => type = value;
    }

    public ushort Wall
    {
        readonly get => wall;
        set => wall = value;
    }

    public byte Liquid
    {
        readonly get => liquid;
        set => liquid = value;
    }

    public byte LiquidType
    {
        readonly get => liquidType;
        set => liquidType = value;
    }

    public bool Active
    {
        readonly get => active;
        set => active = value;
    }

    public readonly bool IsActiveType(int tileType)
    {
        return active && type == tileType;
    }

    public static TileData CreateActive(int tileType)
    {
        var tile = new TileData();
        tile.SetType(tileType);
        return tile;
    }

    public void Clear()
    {
        type = 0;
        wall = 0;
        liquid = 0;
        liquidType = 0;
        active = false;
    }

    public void SetType(int tileType)
    {
        active = true;
        type = checked((ushort)tileType);
    }
}
