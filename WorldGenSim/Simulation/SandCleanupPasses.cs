namespace WorldGenSim.Simulation;

internal static class SandCleanupPasses
{
    public static void ApplyGemsSandSettling(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Gems sand-settling replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        int width = state.Options.Dimensions.Width;
        int height = state.Options.Dimensions.Height;
        (int interestLeft, int interestRight) = WorldInterestArea.CenterSixtyXRange(state.Options.Dimensions);
        for (int pass = 0; pass < 2; pass++)
        {
            int direction = 1;
            int start = Math.Max(5, interestLeft);
            int end = Math.Min(width - 5, interestRight);
            if (pass == 1)
            {
                direction = -1;
                start = Math.Min(width - 5, interestRight - 1);
                end = Math.Max(5, interestLeft - 1);
            }

            for (int x = start; x != end; x += direction)
            {
                if (x > state.UndergroundDesertLocation.Left && x < state.UndergroundDesertLocation.Right)
                {
                    continue;
                }

                progress.Set(pass == 0
                    ? x / (double)width * 0.5
                    : 0.5 + (width - x) / (double)width * 0.5);

                for (int y = 10; y < height - 10; y++)
                {
                    if (!state.Tiles[x, y].Active ||
                        !state.Tiles[x, y + 1].Active ||
                        !IsSand(state.Tiles[x, y].Type) ||
                        !IsSand(state.Tiles[x, y + 1].Type))
                    {
                        continue;
                    }

                    int targetX = x + direction;
                    int targetY = y + 1;
                    if (!WorldInterestArea.IsInCenterSixty(state.Options.Dimensions, targetX))
                    {
                        continue;
                    }

                    if (state.Tiles[targetX, y].Active || state.Tiles[targetX, targetY].Active)
                    {
                        continue;
                    }

                    while (!state.Tiles[targetX, targetY].Active && InWorld(state, targetX, targetY, 10))
                    {
                        targetY++;
                    }

                    targetY--;
                    state.Tiles[x, y].Active = false;
                    state.Tiles[targetX, targetY].SetType(state.Tiles[x, y].Type);
                }
            }
        }
    }

    public static void ApplyGravitatingSand(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Gravitating Sand replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        int width = state.Options.Dimensions.Width;
        int height = state.Options.Dimensions.Height;
        (int interestLeft, int interestRight) = WorldInterestArea.CenterSixtyXRange(state.Options.Dimensions);
        for (int x = interestLeft; x < interestRight; x++)
        {
            progress.Set((x - interestLeft) / (double)(interestRight - interestLeft));
            bool foundSolidBelow = false;
            int lowerSolidY = 0;
            for (int y = height - 1; y > 0; y--)
            {
                if (!IsSolidTile(state.Tiles[x, y]))
                {
                    continue;
                }

                ushort type = state.Tiles[x, y].Type;
                if (foundSolidBelow &&
                    y < (int)state.MainWorldSurface &&
                    y != lowerSolidY - 1 &&
                    IsFallingTile(type))
                {
                    for (int fillY = y; fillY < lowerSolidY; fillY++)
                    {
                        ResetToType(ref state.Tiles[x, fillY], type);
                    }
                }

                foundSolidBelow = true;
                lowerSolidY = y;
            }
        }
    }

    private static void ResetToType(ref TileData tile, int type)
    {
        tile.Liquid = 0;
        tile.LiquidType = 0;
        tile.Active = true;
        tile.Type = checked((ushort)type);
    }

    private static bool IsSand(int tileType)
    {
        return tileType is TileIds.Sand or 112 or 116 or 234;
    }

    private static bool IsFallingTile(int tileType)
    {
        return tileType is TileIds.Sand or TileIds.Silt or 112 or 116 or 224 or 234;
    }

    private static bool IsSolidTile(TileData tile)
    {
        return tile.Active && tile.Type is
            TileIds.Dirt or
            TileIds.Stone or
            TileIds.Grass or
            TileIds.Clay or
            TileIds.Sand or
            TileIds.Mud or
            TileIds.Silt or
            TileIds.SnowBlock or
            TileIds.IceBlock or
            TileIds.SandstoneBrick or
            TileIds.Marble or
            TileIds.Granite or
            TileIds.Sandstone or
            TileIds.HardenedSand or
            TileIds.DesertFossil or
            112 or
            116 or
            224 or
            234;
    }

    private static bool InWorld(WorldGenState state, int x, int y, int fluff)
    {
        return x >= fluff &&
            y >= fluff &&
            x < state.Options.Dimensions.Width - fluff &&
            y < state.Options.Dimensions.Height - fluff;
    }
}
