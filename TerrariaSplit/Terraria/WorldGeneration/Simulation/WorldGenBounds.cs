namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

internal static class WorldGenBounds
{
    public static bool InWorld(WorldGenState state, int x, int y, int fluff = 0)
    {
        return InWorld(state.Options.Dimensions, x, y, fluff);
    }

    public static bool IsInWorld(WorldGenState state, int x, int y)
    {
        return InWorld(state.Options.Dimensions, x, y, fluff: 0);
    }

    public static bool InWorld(WorldDimensions dimensions, int x, int y, int fluff = 0)
    {
        return x >= fluff &&
            y >= fluff &&
            x < dimensions.Width - fluff &&
            y < dimensions.Height - fluff;
    }
}
