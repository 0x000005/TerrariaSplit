namespace WorldGenSim.Simulation;

internal static class SnowConversionPasses
{
    public static void ApplySlush(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Slush replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        (int interestLeft, int interestRight) = WorldInterestArea.CenterSixtyXRange(state.Options.Dimensions);
        int top = Math.Clamp(state.SnowTop, 0, state.Options.Dimensions.Height);
        int bottom = Math.Clamp(state.SnowBottom, top, state.Options.Dimensions.Height);
        for (int y = top; y < bottom; y++)
        {
            progress.Set(bottom == top ? 1.0 : (y - top) / (double)(bottom - top));
            if ((uint)y >= (uint)state.SnowMinX.Length ||
                (uint)y >= (uint)state.SnowMaxX.Length)
            {
                continue;
            }

            int left = Math.Max(state.SnowMinX[y], interestLeft);
            int right = Math.Min(state.SnowMaxX[y], interestRight);
            for (int x = left; x < right; x++)
            {
                ref TileData tile = ref state.Tiles[x, y];
                switch (tile.Type)
                {
                    case TileIds.Silt:
                        tile.Type = TileIds.Slush;
                        break;
                    case TileIds.Mud:
                        if (CanConvertMudToSlush(state, x, y))
                        {
                            tile.Type = TileIds.Slush;
                        }

                        break;
                    case TileIds.Stone:
                        tile.Type = TileIds.IceBlock;
                        break;
                }
            }
        }
    }

    private static bool CanConvertMudToSlush(WorldGenState state, int x, int y)
    {
        const int radius = 3;
        for (int tileX = x - radius; tileX <= x + radius; tileX++)
        {
            for (int tileY = y - radius; tileY <= y + radius; tileY++)
            {
                if (!InWorld(state, tileX, tileY, 0))
                {
                    continue;
                }

                if (state.Tiles[tileX, tileY].Active &&
                    state.Tiles[tileX, tileY].Type is 60 or 70 or 71 or 72)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool InWorld(WorldGenState state, int x, int y, int fluff)
    {
        return x >= fluff &&
            y >= fluff &&
            x < state.Options.Dimensions.Width - fluff &&
            y < state.Options.Dimensions.Height - fluff;
    }
}
