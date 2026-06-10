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

        int width = state.Options.Dimensions.Width;
        int height = state.Options.Dimensions.Height;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                ref TileData tile = ref state.Tiles[x, y];
                if (tile.Active && tile.Type == TileIds.Mud)
                {
                    SpreadGrass(state, x, y, recursionDepth: 0);
                }
            }

            progress.Set(0.2 * ((x + 1.0) / width));
        }

        double scanWidth = width - 20;
        for (int x = 10; x < width - 10; x++)
        {
            ScanTileColumnAndRemoveClumps(state, x);
            progress.Set(0.2 + ((x - 10) / scanWidth) * 0.8);
        }
    }

    private static void SpreadGrass(WorldGenState state, int x, int y, int recursionDepth)
    {
        if (!InWorld(state, x, y, 10) ||
            !state.Tiles[x, y].Active ||
            state.Tiles[x, y].Type != TileIds.Mud)
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
                ref TileData tile = ref state.Tiles[tileX, tileY];
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

        if (fullySurroundedBySolid || !CanBeClearedDuringGeneration(state.Tiles[x, y].Type))
        {
            return;
        }

        state.Tiles[x, y].Type = JungleGrass;
        for (int tileX = left; tileX < right; tileX++)
        {
            for (int tileY = top; tileY < bottom; tileY++)
            {
                if (state.Tiles[tileX, tileY].Active &&
                    state.Tiles[tileX, tileY].Type == TileIds.Mud &&
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
            if (IsClearableSolid(state.Tiles[x, y]))
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
                    state.Tiles[tileX, tileY].Active = false;
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
                !IsClearableSolid(state.Tiles[x, y]) ||
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
