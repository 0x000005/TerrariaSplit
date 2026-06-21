using static TerrariaSplit.Terraria.WorldGeneration.Simulation.WorldGenBounds;

namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

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
        DenseTileGrid tiles = state.Tiles;
        const int boundaryPadding = 3;
        (int targetLeft, int targetRight) = WorldInterestArea.TargetPyramidXRange(state.Options.Dimensions);
        int interestLeft = Math.Max(5, targetLeft - boundaryPadding);
        int interestRight = Math.Min(width - 5, targetRight + boundaryPadding);
        if (interestLeft >= interestRight)
        {
            progress.Set(1.0);
            return;
        }

        for (int pass = 0; pass < 2; pass++)
        {
            int direction = 1;
            int start = interestLeft;
            int end = interestRight;
            if (pass == 1)
            {
                direction = -1;
                start = interestRight - 1;
                end = interestLeft - 1;
            }

            for (int x = start; x != end; x += direction)
            {
                if (x > state.UndergroundDesertLocation.Left && x < state.UndergroundDesertLocation.Right)
                {
                    continue;
                }

                for (int y = 10; y < height - 10; y++)
                {
                    ref TileData tile = ref tiles.GetUnchecked(x, y);
                    ref TileData lowerTile = ref tiles.GetUnchecked(x, y + 1);
                    if (!tile.Active ||
                        !lowerTile.Active ||
                        !IsSand(tile.Type) ||
                        !IsSand(lowerTile.Type))
                    {
                        continue;
                    }

                    int targetX = x + direction;
                    int targetY = y + 1;
                    if (tiles.GetUnchecked(targetX, y).Active || tiles.GetUnchecked(targetX, targetY).Active)
                    {
                        continue;
                    }

                    while (InWorld(state, targetX, targetY, 10) && !tiles.GetUnchecked(targetX, targetY).Active)
                    {
                        targetY++;
                    }

                    targetY--;
                    ushort fallingType = tile.Type;
                    tile.Active = false;
                    tiles.GetUnchecked(targetX, targetY).SetType(fallingType);
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

        int height = state.Options.Dimensions.Height;
        List<int> columns = TargetPyramidCandidateColumns(state);
        if (columns.Count == 0)
        {
            progress.Set(1.0);
            return;
        }

        for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            int x = columns[columnIndex];
            progress.Set(columnIndex / (double)columns.Count);
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

        progress.Set(1.0);
    }

    private static List<int> TargetPyramidCandidateColumns(WorldGenState state)
    {
        var columns = new List<int>(state.PyramidCandidates.Count);
        foreach (PyramidCandidate candidate in state.PyramidCandidates)
        {
            if (!WorldInterestArea.IsInTargetPyramidXRange(state.Options.Dimensions, candidate.X))
            {
                continue;
            }

            bool alreadyAdded = false;
            for (int i = 0; i < columns.Count; i++)
            {
                if (columns[i] == candidate.X)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                columns.Add(candidate.X);
            }
        }

        return columns;
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

}
