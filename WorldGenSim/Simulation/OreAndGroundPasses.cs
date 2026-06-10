namespace WorldGenSim.Simulation;

internal static class OreAndGroundPasses
{
    public static void ApplyDirtToMud(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Dirt To Mud replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        int height = state.Options.Dimensions.Height;
        double count = width * height * 0.001;
        for (int i = 0; i < count; i++)
        {
            progress.Set(i / count);
            WorldGenTileRunner.Run(
                state,
                random,
                random.Next(0, width),
                random.Next((int)state.RockLayerLow, height),
                random.Next(2, 6),
                random.Next(2, 40),
                TileIds.Mud,
                ignoreTileType: TileIds.Sand);
        }
    }

    public static void ApplySilt(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Silt replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        int height = state.Options.Dimensions.Height;
        progress.Set(0.0);

        int largePatchCount = (int)(width * height * 0.0001f);
        for (int i = 0; i < largePatchCount; i++)
        {
            progress.Set(i / (float)largePatchCount * 0.5f);
            int x = random.Next(0, width);
            int y = random.Next((int)state.RockLayerHigh, height);
            if (state.Tiles[x, y].Wall is 187 or 216)
            {
                continue;
            }

            WorldGenTileRunner.Run(
                state,
                random,
                x,
                y,
                random.Next(5, 12),
                random.Next(15, 50),
                TileIds.Silt);
        }

        int smallPatchCount = (int)(width * height * 0.0005f);
        for (int i = 0; i < smallPatchCount; i++)
        {
            progress.Set(0.5f + i / (float)smallPatchCount * 0.5f);
            int x = random.Next(0, width);
            int y = random.Next((int)state.RockLayerHigh, height);
            if (state.Tiles[x, y].Wall is 187 or 216)
            {
                continue;
            }

            WorldGenTileRunner.Run(
                state,
                random,
                x,
                y,
                random.Next(2, 5),
                random.Next(2, 5),
                TileIds.Silt);
        }

        progress.Set(1.0);
    }

    public static void ApplyShinies(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Shinies replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        UnifiedRandom random = context.Random;
        int area = state.Options.Dimensions.Width * state.Options.Dimensions.Height;
        progress.Set(0.0);
        const float progressStep = 1f / 12f;

        RunOrePatches(state, random, Count(area, 6E-05), state.WorldSurfaceLow, state.WorldSurfaceHigh, 3, 6, 2, 6, state.Copper);
        progress.Set(progressStep);
        RunOrePatches(state, random, Count(area, 8E-05), state.WorldSurfaceHigh, state.RockLayerHigh, 3, 7, 3, 7, state.Copper);
        progress.Set(progressStep * 2f);
        RunOrePatches(state, random, Count(area, 0.0002), state.RockLayerLow, state.Options.Dimensions.Height, 4, 9, 4, 8, state.Copper);
        progress.Set(progressStep * 3f);

        RunOrePatches(state, random, Count(area, 3E-05), state.WorldSurfaceLow, state.WorldSurfaceHigh, 3, 7, 2, 5, state.Iron);
        progress.Set(progressStep * 4f);
        RunOrePatches(state, random, Count(area, 8E-05), state.WorldSurfaceHigh, state.RockLayerHigh, 3, 6, 3, 6, state.Iron);
        progress.Set(progressStep * 5f);
        RunOrePatches(state, random, Count(area, 0.0002), state.RockLayerLow, state.Options.Dimensions.Height, 4, 9, 4, 8, state.Iron);
        progress.Set(progressStep * 6f);

        RunOrePatches(state, random, Count(area, 2.6E-05), state.WorldSurfaceHigh, state.RockLayerHigh, 3, 6, 3, 6, state.Silver);
        progress.Set(progressStep * 7f);
        RunOrePatches(state, random, Count(area, 0.00015), state.RockLayerLow, state.Options.Dimensions.Height, 4, 9, 4, 8, state.Silver);
        progress.Set(progressStep * 8f);

        RunOrePatches(state, random, Count(area, 0.00012), state.RockLayerLow, state.Options.Dimensions.Height, 4, 8, 4, 8, state.Gold);
        progress.Set(progressStep * 9f);

        RunOrePatches(state, random, Count(area, 0.00017), 0.0, state.WorldSurfaceLow, 4, 9, 4, 8, state.Silver);
        RunOrePatches(state, random, Count(area, 0.00012), 0.0, state.WorldSurfaceLow - 20.0, 4, 8, 4, 8, state.Gold);
        progress.Set(progressStep * 10f);

        progress.Set(progressStep * 11f);
        RunOrePatches(state, random, Count(area, 2.25E-05), state.MainRockLayer, state.Options.Dimensions.Height, 3, 6, 4, 8, TileIds.Crimtane);
        progress.Set(1.0);
    }

    private static void RunOrePatches(
        WorldGenState state,
        UnifiedRandom random,
        int count,
        double minY,
        double maxY,
        int strengthMin,
        int strengthMax,
        int stepsMin,
        int stepsMax,
        int type)
    {
        int width = state.Options.Dimensions.Width;
        int yMin = Math.Clamp((int)minY, 0, state.Options.Dimensions.Height - 1);
        int yMax = Math.Clamp((int)maxY, yMin + 1, state.Options.Dimensions.Height);
        for (int i = 0; i < count; i++)
        {
            WorldGenTileRunner.Run(
                state,
                random,
                random.Next(0, width),
                random.Next(yMin, yMax),
                random.Next(strengthMin, strengthMax),
                random.Next(stepsMin, stepsMax),
                type);
        }
    }

    private static int Count(int area, double factor)
    {
        return (int)(area * factor);
    }
}
