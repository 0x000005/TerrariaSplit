namespace WorldGenSim.Simulation;

internal static class MudCavesToGrassPassReplica
{
    private const int JungleGrass = 60;
    private const int TileCounterMax = 20;

    public static void Apply(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Mud caves to grass replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        int height = state.Options.Dimensions.Height;
        (int jungleLeft, int jungleRight) = JungleMudScanRange(state);
        int jungleWidth = Math.Max(1, jungleRight - jungleLeft);
        for (int x = jungleLeft; x < jungleRight; x++)
        {
            for (int y = 0; y < height; y++)
            {
                ref TileData tile = ref state.Tiles.GetUnchecked(x, y);
                if (tile.Active && tile.Type == TileIds.Mud)
                {
                    SpreadGrass(state, x, y, recursionDepth: 0);
                }
            }

            progress.Set(0.2 * ((x - jungleLeft + 1.0) / jungleWidth));
        }

        List<int> cleanupColumns = CleanupScanColumns(state, jungleLeft, jungleRight);
        for (int i = 0; i < cleanupColumns.Count; i++)
        {
            int x = cleanupColumns[i];
            ScanTileColumnAndRemoveClumps(state, x);
            progress.Set(0.2 + ((i + 1.0) / cleanupColumns.Count) * 0.8);
        }
    }

    private static (int LeftInclusive, int RightExclusive) JungleMudScanRange(WorldGenState state)
    {
        int width = state.Options.Dimensions.Width;
        if (state.JungleMinX < 0 || state.JungleMaxX <= state.JungleMinX)
        {
            return (0, width);
        }

        int left = Math.Max(0, state.JungleMinX - TileCounterMax);
        int right = Math.Min(width, state.JungleMaxX + TileCounterMax);
        return (left, right);
    }

    private static List<int> CleanupScanColumns(WorldGenState state, int jungleLeft, int jungleRight)
    {
        int width = state.Options.Dimensions.Width;
        bool[] include = new bool[width];
        int left = Math.Max(10, jungleLeft - TileCounterMax);
        int right = Math.Min(width - 10, jungleRight + TileCounterMax);
        for (int x = left; x < right; x++)
        {
            include[x] = true;
        }

        foreach (PyramidCandidate candidate in state.PyramidCandidates)
        {
            left = Math.Max(10, candidate.X - TileCounterMax);
            right = Math.Min(width - 10, candidate.X + TileCounterMax + 1);
            for (int x = left; x < right; x++)
            {
                include[x] = true;
            }
        }

        var columns = new List<int>();
        for (int x = 10; x < width - 10; x++)
        {
            if (include[x])
            {
                columns.Add(x);
            }
        }

        return columns;
    }

    private static void SpreadGrass(WorldGenState state, int x, int y, int recursionDepth)
    {
        if (!InWorld(state, x, y, 10))
        {
            return;
        }

        ref TileData current = ref state.Tiles.GetUnchecked(x, y);
        if (!current.Active || current.Type != TileIds.Mud)
        {
            return;
        }

        int left = Math.Max(0, x - 1);
        int right = Math.Min(state.Options.Dimensions.Width, x + 2);
        int top = Math.Max(0, y - 1);
        int bottom = Math.Min(state.Options.Dimensions.Height, y + 2);
        bool fullySurroundedBySolid = true;
        for (int tileX = left; tileX < right; tileX++)
        {
            for (int tileY = top; tileY < bottom; tileY++)
            {
                ref TileData tile = ref state.Tiles.GetUnchecked(tileX, tileY);
                if (!tile.Active || !IsSolid(tile.Type))
                {
                    fullySurroundedBySolid = false;
                }

                if (tile.Liquid > 0 && tile.LiquidType == 1)
                {
                    fullySurroundedBySolid = true;
                    break;
                }
            }
        }

        if (fullySurroundedBySolid || !CanBeClearedDuringGeneration(current.Type))
        {
            return;
        }

        current.Type = JungleGrass;
        for (int tileX = left; tileX < right; tileX++)
        {
            for (int tileY = top; tileY < bottom; tileY++)
            {
                ref TileData tile = ref state.Tiles.GetUnchecked(tileX, tileY);
                if (tile.Active &&
                    tile.Type == TileIds.Mud &&
                    recursionDepth < 1000)
                {
                    SpreadGrass(state, tileX, tileY, recursionDepth + 1);
                }
            }
        }
    }

    private static void ScanTileColumnAndRemoveClumps(WorldGenState state, int x)
    {
        int consecutive = 0;
        int startY = 0;
        for (int y = 10; y < state.Options.Dimensions.Height - 10; y++)
        {
            if (IsClearableSolid(state.Tiles.GetUnchecked(x, y)))
            {
                if (consecutive == 0)
                {
                    startY = y;
                }

                consecutive++;
                continue;
            }

            if (consecutive > 0 && consecutive < TileCounterMax &&
                CountClearableConnectedTiles(state, x, startY, out List<(int X, int Y)> connected) < TileCounterMax)
            {
                foreach ((int tileX, int tileY) in connected)
                {
                    state.Tiles.GetUnchecked(tileX, tileY).Active = false;
                }
            }

            consecutive = 0;
        }
    }

    private static int CountClearableConnectedTiles(WorldGenState state, int startX, int startY, out List<(int X, int Y)> connected)
    {
        connected = [];
        Stack<(int X, int Y)> pending = [];
        pending.Push((startX, startY));
        while (pending.Count > 0 && connected.Count < TileCounterMax)
        {
            (int x, int y) = pending.Pop();
            if (x < 5 ||
                x > state.Options.Dimensions.Width - 5 ||
                y < 5 ||
                y > state.Options.Dimensions.Height - 5 ||
                !IsClearableSolid(state.Tiles.GetUnchecked(x, y)) ||
                connected.Contains((x, y)))
            {
                continue;
            }

            connected.Add((x, y));
            pending.Push((x - 1, y));
            pending.Push((x + 1, y));
            pending.Push((x, y - 1));
            pending.Push((x, y + 1));
        }

        return connected.Count;
    }

    private static bool InWorld(WorldGenState state, int x, int y, int fluff)
    {
        return x >= fluff &&
            y >= fluff &&
            x < state.Options.Dimensions.Width - fluff &&
            y < state.Options.Dimensions.Height - fluff;
    }

    private static bool IsClearableSolid(TileData tile)
    {
        return tile.Active && IsSolid(tile.Type) && CanBeClearedDuringGeneration(tile.Type);
    }

    private static bool IsSolid(int tileType)
    {
        return tileType is
            TileIds.Dirt or
            TileIds.Stone or
            TileIds.Grass or
            TileIds.Clay or
            TileIds.Sand or
            TileIds.Mud or
            JungleGrass or
            TileIds.SnowBlock or
            TileIds.IceBlock or
            TileIds.HardenedSand or
            TileIds.SandstoneBrick;
    }

    private static bool CanBeClearedDuringGeneration(int tileType)
    {
        return tileType is
            TileIds.Dirt or
            TileIds.Stone or
            TileIds.Grass or
            TileIds.Clay or
            TileIds.Sand or
            TileIds.Mud or
            JungleGrass or
            TileIds.SnowBlock or
            TileIds.IceBlock or
            TileIds.HardenedSand;
    }
}
