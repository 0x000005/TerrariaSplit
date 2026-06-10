namespace WorldGenSim.Simulation;

internal static class GranitePassReplica
{
    private const int BeachDistance = 380;
    private const int MapSize = 200;
    private const int MaxMagmaIterations = 300;
    private const ushort GraniteWall = 180;

    private static readonly Vec2[] NormalizedVectors =
    [
        Vec2.Normalize(new Vec2(-1.0, -1.0)),
        Vec2.Normalize(new Vec2(-1.0, 0.0)),
        Vec2.Normalize(new Vec2(-1.0, 1.0)),
        Vec2.Normalize(new Vec2(0.0, -1.0)),
        new Vec2(0.0, 0.0),
        Vec2.Normalize(new Vec2(0.0, 1.0)),
        Vec2.Normalize(new Vec2(1.0, -1.0)),
        Vec2.Normalize(new Vec2(1.0, 0.0)),
        Vec2.Normalize(new Vec2(1.0, 1.0))
    ];

    public static void Apply(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Granite replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        int height = state.Options.Dimensions.Height;
        int count = random.Next(4, 9);
        double slotWidth = (width - 200) / (double)count;
        List<(int X, int Y)> origins = new(count);
        int attempts = 0;

        while (origins.Count < count)
        {
            float slot = origins.Count / (float)count;
            progress.Set(slot * 0.2f);
            int originX = random.Next((int)(slot * (width - 200)) + 100, (int)(slot * (width - 200)) + 100 + (int)slotWidth);
            int originY = random.Next((int)state.RockLayer + 20, height - 220);
            while (originX > width * 0.45 && originX < width * 0.55)
            {
                originX = random.Next(BeachDistance, width - BeachDistance);
            }

            attempts++;
            if (CanPlace(state, originX, originY))
            {
                origins.Add((originX, originY));
            }
            else if (attempts > width * 10)
            {
                count = origins.Count;
                origins.Add((originX, originY));
                attempts = 0;
            }
        }

        for (int i = 0; i < count; i++)
        {
            progress.Set(0.2f + i / (float)count * 0.8f);
            PlaceBiome(state, origins[i].X, origins[i].Y);
        }
    }

    private static bool CanPlace(WorldGenState state, int originX, int originY)
    {
        return !BiomeTileCheck(state, originX, originY) && !state.Tiles[originX, originY].Active;
    }

    private static bool PlaceBiome(WorldGenState state, int originX, int originY)
    {
        if (state.Tiles[originX, originY].Active)
        {
            return false;
        }

        int tileOriginX = originX - MapSize / 2;
        int tileOriginY = originY - MapSize / 2;
        BuildMagmaMap(state, tileOriginX, tileOriginY, out Magma[,] source, out Magma[,] target);
        WorldRect effectedMapArea = SimulatePressure(ref source, ref target);
        PlaceGranite(state, tileOriginX, tileOriginY, effectedMapArea, source);
        CleanupTiles(state, tileOriginX, tileOriginY, effectedMapArea, source);
        PlaceDecorations(state, tileOriginX, tileOriginY, effectedMapArea, source);
        return true;
    }

    private static void BuildMagmaMap(
        WorldGenState state,
        int tileOriginX,
        int tileOriginY,
        out Magma[,] source,
        out Magma[,] target)
    {
        source = new Magma[MapSize, MapSize];
        target = new Magma[MapSize, MapSize];
        for (int x = 0; x < MapSize; x++)
        {
            for (int y = 0; y < MapSize; y++)
            {
                Magma magma = Magma.CreateEmpty(IsSolidTile(state, tileOriginX + x, tileOriginY + y) ? 4.0 : 1.0);
                source[x, y] = magma;
                target[x, y] = magma;
            }
        }
    }

    private static WorldRect SimulatePressure(ref Magma[,] source, ref Magma[,] target)
    {
        int centerX = MapSize / 2;
        int centerY = MapSize / 2;
        int left = centerX;
        int right = left;
        int top = centerY;
        int bottom = top;

        for (int iteration = 0; iteration < MaxMagmaIterations; iteration++)
        {
            for (int x = left; x <= right; x++)
            {
                for (int y = top; y <= bottom; y++)
                {
                    Magma magma = source[x, y];
                    if (!magma.IsActive)
                    {
                        continue;
                    }

                    double neighborPressure = 0.0;
                    Vec2 weightedDirection = Vec2.Zero;
                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        for (int offsetY = -1; offsetY <= 1; offsetY++)
                        {
                            if (offsetX == 0 && offsetY == 0)
                            {
                                continue;
                            }

                            Vec2 direction = NormalizedVectors[(offsetX + 1) * 3 + (offsetY + 1)];
                            Magma neighbor = source[x + offsetX, y + offsetY];
                            if (magma.Pressure > 0.01 && !neighbor.IsActive)
                            {
                                if (offsetX == -1)
                                {
                                    left = Math.Clamp(x + offsetX, 1, left);
                                }
                                else
                                {
                                    right = Math.Clamp(x + offsetX, right, MapSize - 2);
                                }

                                if (offsetY == -1)
                                {
                                    top = Math.Clamp(y + offsetY, 1, top);
                                }
                                else
                                {
                                    bottom = Math.Clamp(y + offsetY, bottom, MapSize - 2);
                                }

                                target[x + offsetX, y + offsetY] = neighbor.ToFlow();
                            }

                            double pressure = neighbor.Pressure;
                            neighborPressure += pressure;
                            weightedDirection += direction * pressure;
                        }
                    }

                    neighborPressure /= 8.0;
                    if (neighborPressure > magma.Resistance)
                    {
                        double directionalPressure = weightedDirection.Length() / 8.0;
                        double pressure = Math.Max(neighborPressure - directionalPressure - magma.Pressure, 0.0) +
                            directionalPressure +
                            magma.Pressure * 0.875 -
                            magma.Resistance;
                        pressure = Math.Max(0.0, pressure);
                        target[x, y] = Magma.CreateFlow(pressure, Math.Max(0.0, magma.Resistance - pressure * 0.02));
                    }
                }
            }

            if (iteration < 2)
            {
                target[centerX, centerY] = Magma.CreateFlow(25.0);
            }

            (source, target) = (target, source);
        }

        return new WorldRect(left, top, right - left + 1, bottom - top + 1);
    }

    private static bool ShouldUseLava(WorldGenState state, int tileOriginX, int tileOriginY)
    {
        int centerX = MapSize / 2;
        int centerY = MapSize / 2;
        if (tileOriginY + centerY <= state.LavaLine - 30)
        {
            return false;
        }

        for (int x = -50; x < 50; x++)
        {
            for (int y = -50; y < 50; y++)
            {
                int tileX = tileOriginX + centerX + x;
                int tileY = tileOriginY + centerY + y;
                if (!InWorld(state, tileX, tileY, 10) || !state.Tiles[tileX, tileY].Active)
                {
                    continue;
                }

                ushort type = state.Tiles[tileX, tileY].Type;
                if (type is TileIds.SnowBlock or TileIds.IceBlock or 162 or 163 or 200)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void PlaceGranite(
        WorldGenState state,
        int tileOriginX,
        int tileOriginY,
        WorldRect magmaMapArea,
        Magma[,] source)
    {
        bool useLava = ShouldUseLava(state, tileOriginX, tileOriginY);
        for (int mapX = magmaMapArea.Left; mapX < magmaMapArea.Right; mapX++)
        {
            for (int mapY = magmaMapArea.Top; mapY < magmaMapArea.Bottom; mapY++)
            {
                int x = tileOriginX + mapX;
                int y = tileOriginY + mapY;
                if (!InWorld(state, x, y, 10))
                {
                    continue;
                }

                Magma magma = source[mapX, mapY];
                if (!magma.IsActive)
                {
                    continue;
                }

                ref TileData tile = ref state.Tiles[x, y];
                double wave = Math.Sin(y * 0.4) * 0.7 + 1.2;
                double pressureFactor = 0.2 + 0.5 / Math.Sqrt(Math.Max(0.0, magma.Pressure - magma.Resistance));
                double threshold = Math.Max(1.0 - Math.Max(0.0, wave * pressureFactor), magma.Pressure / 15.0);
                if (threshold > 0.35 + (IsSolidTile(tile) ? 0.0 : 0.5))
                {
                    ResetToType(ref tile, TileIds.Granite);
                    tile.Wall = GraniteWall;
                }
                else if (magma.Resistance < 0.01)
                {
                    tile.Active = false;
                    tile.Wall = GraniteWall;
                }

                if (tile.Liquid > 0 && useLava)
                {
                    tile.LiquidType = 1;
                }
            }
        }
    }

    private static void CleanupTiles(
        WorldGenState state,
        int tileOriginX,
        int tileOriginY,
        WorldRect magmaMapArea,
        Magma[,] source)
    {
        List<(int X, int Y)> toClear = [];
        for (int mapX = magmaMapArea.Left; mapX < magmaMapArea.Right; mapX++)
        {
            for (int mapY = magmaMapArea.Top; mapY < magmaMapArea.Bottom; mapY++)
            {
                if (!source[mapX, mapY].IsActive)
                {
                    continue;
                }

                int x = tileOriginX + mapX;
                int y = tileOriginY + mapY;
                if (!InWorld(state, x, y, 10) || !IsSolidTile(state, x, y))
                {
                    continue;
                }

                int solidNeighbors = 0;
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    for (int offsetY = -1; offsetY <= 1; offsetY++)
                    {
                        if (IsSolidTile(state, x + offsetX, y + offsetY))
                        {
                            solidNeighbors++;
                        }
                    }
                }

                if (solidNeighbors < 3)
                {
                    toClear.Add((x, y));
                }
            }
        }

        foreach ((int x, int y) in toClear)
        {
            state.Tiles[x, y].Active = false;
            state.Tiles[x, y].Wall = GraniteWall;
        }
    }

    private static void PlaceDecorations(
        WorldGenState state,
        int tileOriginX,
        int tileOriginY,
        WorldRect magmaMapArea,
        Magma[,] source)
    {
        FastRandomReplica fastRandom = new((ulong)state.Options.Seed);
        fastRandom = fastRandom.WithModifier(65440UL);
        for (int mapX = magmaMapArea.Left; mapX < magmaMapArea.Right; mapX++)
        {
            for (int mapY = magmaMapArea.Top; mapY < magmaMapArea.Bottom; mapY++)
            {
                Magma magma = source[mapX, mapY];
                int x = tileOriginX + mapX;
                int y = tileOriginY + mapY;
                if (!InWorld(state, x, y, 10) || !magma.IsActive)
                {
                    continue;
                }

                FastRandomReplica localRandom = fastRandom.WithModifier(x, y);
                if (localRandom.Next(8) == 0 && state.Tiles[x, y].Active)
                {
                    if (!state.Tiles[x, y + 1].Active)
                    {
                        PlaceUncheckedStalactite(state, x, y + 1, localRandom.Next(2) == 0, localRandom.Next(3));
                    }

                    if (!state.Tiles[x, y - 1].Active)
                    {
                        PlaceUncheckedStalactite(state, x, y - 1, localRandom.Next(2) == 0, localRandom.Next(3));
                    }
                }

                _ = localRandom.Next(2);
            }
        }
    }

    private static void PlaceUncheckedStalactite(
        WorldGenState state,
        int x,
        int y,
        bool preferSmall,
        int variation)
    {
        _ = variation;
        if (!InWorld(state, x, y, 2) ||
            !IsSolidTile(state, x, y - 1) ||
            state.Tiles[x, y].Active ||
            state.Tiles[x, y + 1].Active)
        {
            return;
        }

        ushort anchorType = state.Tiles[x, y - 1].Type;
        if (anchorType is not (
            TileIds.SnowBlock or
            TileIds.IceBlock or
            163 or
            164 or
            200 or
            TileIds.Stone or
            117 or
            25 or
            203 or
            225 or
            TileIds.Sandstone or
            TileIds.HardenedSand or
            TileIds.Granite or
            TileIds.Marble))
        {
            return;
        }

        state.Tiles[x, y].SetType(TileIds.Stalactite);
        if (!preferSmall)
        {
            state.Tiles[x, y + 1].SetType(TileIds.Stalactite);
        }
    }

    private static bool BiomeTileCheck(WorldGenState state, int x, int y)
    {
        for (int scanX = x - 50; scanX < x + 50; scanX++)
        {
            for (int scanY = y - 50; scanY < y + 50; scanY++)
            {
                if (!InWorld(state, scanX, scanY, 0))
                {
                    continue;
                }

                TileData tile = state.Tiles[scanX, scanY];
                if (tile.Active && tile.Type is
                    TileIds.Granite or
                    TileIds.Marble or
                    TileIds.SnowBlock or
                    TileIds.IceBlock or
                    162 or
                    70 or
                    72 or
                    TileIds.Sandstone or
                    TileIds.HardenedSand)
                {
                    return true;
                }

                if (tile.Wall is 187 or 216)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void ResetToType(ref TileData tile, int type)
    {
        tile.Liquid = 0;
        tile.LiquidType = 0;
        tile.Active = true;
        tile.Type = checked((ushort)type);
    }

    private static bool IsSolidTile(WorldGenState state, int x, int y)
    {
        return InWorld(state, x, y, 0) && IsSolidTile(state.Tiles[x, y]);
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
            60 or
            70 or
            TileIds.SnowBlock or
            TileIds.IceBlock or
            162 or
            163 or
            164 or
            200 or
            TileIds.SandstoneBrick or
            TileIds.Marble or
            TileIds.Granite or
            TileIds.Sandstone or
            TileIds.HardenedSand or
            TileIds.DesertFossil;
    }

    private static bool InWorld(WorldGenState state, int x, int y, int fluff)
    {
        return x >= fluff &&
            y >= fluff &&
            x < state.Options.Dimensions.Width - fluff &&
            y < state.Options.Dimensions.Height - fluff;
    }

    private readonly record struct Magma(double Pressure, double Resistance, bool IsActive)
    {
        public Magma ToFlow()
        {
            return new Magma(Pressure, Resistance, IsActive: true);
        }

        public static Magma CreateFlow(double pressure, double resistance = 0.0)
        {
            return new Magma(pressure, resistance, IsActive: true);
        }

        public static Magma CreateEmpty(double resistance = 0.0)
        {
            return new Magma(0.0, resistance, IsActive: false);
        }
    }

    private readonly record struct Vec2(double X, double Y)
    {
        public static Vec2 Zero => new(0.0, 0.0);

        public static Vec2 operator +(Vec2 left, Vec2 right) => new(left.X + right.X, left.Y + right.Y);

        public static Vec2 operator *(Vec2 left, double value) => new(left.X * value, left.Y * value);

        public double Length()
        {
            return Math.Sqrt(X * X + Y * Y);
        }

        public static Vec2 Normalize(Vec2 value)
        {
            double length = value.Length();
            return length == 0.0 ? Zero : new Vec2(value.X / length, value.Y / length);
        }
    }
}
