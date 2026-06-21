namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

internal static class GrassPassReplica
{
    public static void Apply(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Grass replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        int height = state.Options.Dimensions.Height;
        double iterations = width * height * 0.002d;

        for (int i = 0; i < iterations; i++)
        {
            progress.Set(i / iterations);
            TryPlaceGrassInDirtPocket(
                state,
                random.Next(1, width - 1),
                ClampY(random.Next((int)state.WorldSurfaceLow, (int)state.WorldSurfaceHigh), height));

            TryPlaceGrassInDirtPocket(
                state,
                random.Next(1, width - 1),
                ClampY(random.Next(5, (int)state.WorldSurfaceLow), height));
        }
    }

    private static int ClampY(int y, int height)
    {
        return y >= height ? height - 2 : Math.Max(1, y);
    }

    private static void TryPlaceGrassInDirtPocket(WorldGenState state, int x, int y)
    {
        if (state.Tiles[x - 1, y].IsActiveType(TileIds.Dirt) &&
            state.Tiles[x + 1, y].IsActiveType(TileIds.Dirt) &&
            state.Tiles[x, y - 1].IsActiveType(TileIds.Dirt) &&
            state.Tiles[x, y + 1].IsActiveType(TileIds.Dirt))
        {
            state.Tiles[x, y].SetType(TileIds.Grass);
        }
    }
}
