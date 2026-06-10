namespace WorldGenSim.Simulation;

internal static class IceBiomePassReplica
{
    private const int DirtWall = 2;
    private const int SnowWall = 40;

    public static void Apply(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Ice biome replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        int height = state.Options.Dimensions.Height;
        state.SnowTop = (int)state.MainWorldSurface;

        int iceCoreTop = state.LavaLine - random.Next(160, 200);
        int snowBottomLimit = state.LavaLine;
        int left = state.SnowOriginLeft;
        int right = state.SnowOriginRight;
        int lowerSnowThickness = 10;
        int lastRow = Math.Min(snowBottomLimit - 140, height - 2);

        for (int row = 0; row <= lastRow; row++)
        {
            progress.Set(lastRow <= 0 ? 1.0 : (double)row / lastRow);

            left += random.Next(-4, 4);
            right += random.Next(-3, 5);
            if (row > 0)
            {
                left = (left + state.SnowMinX[row - 1]) / 2;
                right = (right + state.SnowMaxX[row - 1]) / 2;
            }

            if (state.DungeonSide >= 1)
            {
                if (random.Next(4) == 0)
                {
                    left++;
                    right++;
                }
            }
            else if (random.Next(4) == 0)
            {
                left--;
                right--;
            }

            left = Math.Clamp(left, 1, width - 2);
            right = Math.Clamp(right, left + 1, width - 1);
            state.SnowMinX[row] = left;
            state.SnowMaxX[row] = right;

            for (int x = left; x < right; x++)
            {
                if (row < iceCoreTop)
                {
                    ConvertSnowTile(state, x, row);
                    continue;
                }

                lowerSnowThickness += random.Next(-3, 4);
                if (random.Next(3) == 0)
                {
                    lowerSnowThickness += random.Next(-4, 5);
                    if (random.Next(3) == 0)
                    {
                        lowerSnowThickness += random.Next(-6, 7);
                    }
                }

                if (lowerSnowThickness < 0)
                {
                    lowerSnowThickness = random.Next(3);
                }
                else if (lowerSnowThickness > 50)
                {
                    lowerSnowThickness = 50 - random.Next(3);
                }

                int bottom = Math.Min(row + lowerSnowThickness, height - 2);
                for (int y = row; y < bottom; y++)
                {
                    ConvertSnowTile(state, x, y);
                }
            }

            if (state.SnowBottom < row)
            {
                state.SnowBottom = row;
            }
        }
    }

    private static void ConvertSnowTile(WorldGenState state, int x, int y)
    {
        ref TileData tile = ref state.Tiles[x, y];
        if (tile.Wall == DirtWall)
        {
            tile.Wall = SnowWall;
        }

        tile.Type = tile.Type switch
        {
            TileIds.Dirt or 2 or 23 or TileIds.Clay or TileIds.Sand => TileIds.SnowBlock,
            TileIds.Stone => TileIds.IceBlock,
            _ => tile.Type
        };
    }
}
