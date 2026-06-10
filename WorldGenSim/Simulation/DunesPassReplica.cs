namespace WorldGenSim.Simulation;

internal static class DunesPassReplica
{
    private const double ChanceOfPyramid = 0.8;
    private const double HeightScale = 1.0;

    public static void Apply(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Dunes replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        UnifiedRandom random = context.Random;
        SetupDungeonGenVarVariablesForTarget(random);

        int width = state.Options.Dimensions.Width;
        int duneCount = random.Next(1, 3);
        for (int i = 0; i < duneCount; i++)
        {
            progress.Set((double)i / duneCount);
            Point2 origin = ChooseDuneOrigin(random, state);
            PlaceDunes(random, state, origin.X);

            if (random.NextDouble() <= ChanceOfPyramid)
            {
                int candidateX = random.Next(origin.X - 200, origin.X + 200);
                for (int y = 0; y < state.Options.Dimensions.Height; y++)
                {
                    if (state.Tiles[candidateX, y].Active)
                    {
                        state.AddPyramidCandidate(candidateX, y + 20, i);
                        break;
                    }
                }
            }
        }

        state.DunesApplied = true;
    }

    private static void SetupDungeonGenVarVariablesForTarget(UnifiedRandom random)
    {
        // DungeonCrawler.SetupDungeonGenVarVariables consumes dungeon color, two
        // entrance-style probes, then a RandomSeed-style value in the entrance
        // settings path used by normal non-secret worlds.
        _ = random.Next(3);
        _ = random.Next(3);
        _ = random.Next(3);
        _ = random.Next();
    }

    private static Point2 ChooseDuneOrigin(UnifiedRandom random, WorldGenState state)
    {
        int width = state.Options.Dimensions.Width;
        int height = state.Options.Dimensions.Height;
        double scale = width / 4200.0;
        Point2 origin = default;
        bool accepted = false;
        int attempts = 0;
        while (!accepted)
        {
            origin = new Point2(
                random.Next(500, width - 500),
                random.Next(0, height));
            bool nearJungle = Math.Abs(origin.X - state.JungleOriginX) < (int)(600.0 * scale);
            bool nearSpawn = Math.Abs(origin.X - width / 2) < 300;
            bool nearSnow = origin.X > state.SnowOriginLeft - 300 &&
                origin.X < state.SnowOriginRight + 300;
            attempts++;
            if (attempts >= width)
            {
                nearJungle = false;
            }

            if (attempts >= width * 2)
            {
                nearSnow = false;
            }

            accepted = !(nearJungle || nearSpawn || nearSnow);
        }

        return origin;
    }

    private static void PlaceDunes(UnifiedRandom random, WorldGenState state, int originX)
    {
        int height1 = (int)(random.Next(60, 100) * HeightScale);
        int height2 = (int)(random.Next(60, 100) * HeightScale);
        int width1 = random.Next(150, 251);
        int width2 = random.Next(150, 251);

        DuneDescription first = DuneDescription.Create(state, random, originX - width1 / 2 + 30, width1, height1);
        DuneDescription second = DuneDescription.Create(state, random, originX + width2 / 2 - 30, width2, height2);
        PlaceSingle(random, state, first);
        PlaceSingle(random, state, second);
    }

    private static void PlaceSingle(UnifiedRandom random, WorldGenState state, DuneDescription description)
    {
        int hillCount = random.Next(3) + 8;
        for (int i = 0; i < hillCount - 1; i++)
        {
            int width = (int)(2.0 / hillCount * description.Width);
            int centerX = (int)((double)i / hillCount * description.Width + description.Left) + width * 2 / 5;
            centerX += random.Next(-5, 6);
            double progress = (double)i / (hillCount - 2);
            double scale = 1.0 - Math.Abs(progress - 0.5) * 2.0;
            PlaceHill(
                random,
                state,
                centerX - width / 2,
                centerX + width / 2,
                (scale * 0.3 + 0.2) * HeightScale,
                description);
        }

        int centralHillCount = random.Next(2) + 1;
        for (int i = 0; i < centralHillCount; i++)
        {
            int width = description.Width / 2;
            int centerX = description.CenterX + random.Next(-10, 11);
            PlaceHill(
                random,
                state,
                centerX - width / 2,
                centerX + width / 2,
                0.8 * HeightScale,
                description);
        }
    }

    private static void PlaceHill(
        UnifiedRandom random,
        WorldGenState state,
        int startX,
        int endX,
        double scale,
        DuneDescription description)
    {
        int startY = description.SurfaceAt(startX);
        int endY = description.SurfaceAt(endX);
        int middleX = (startX + endX) / 2;
        int middleY = (startY + endY) / 2 - (int)(35.0 * scale);
        int maxOffset = (endX - middleX) / 4;
        int minOffset = (endX - middleX) / 16;
        if (description.WindRight)
        {
            middleX += random.Next(minOffset, maxOffset + 1);
        }
        else
        {
            middleX -= random.Next(minOffset, maxOffset + 1);
        }

        int positiveAnchorY = (int)(scale * 12.0);
        int negativeAnchorY = positiveAnchorY / -2;
        if (description.WindRight)
        {
            PlaceCurvedLine(state, startX, startY, middleX, middleY, 0, negativeAnchorY, description);
            PlaceCurvedLine(state, middleX, middleY, endX, endY, 0, positiveAnchorY, description);
        }
        else
        {
            PlaceCurvedLine(state, startX, startY, middleX, middleY, 0, positiveAnchorY, description);
            PlaceCurvedLine(state, middleX, middleY, endX, endY, 0, negativeAnchorY, description);
        }
    }

    private static void PlaceCurvedLine(
        WorldGenState state,
        int startX,
        int startY,
        int endX,
        int endY,
        int anchorOffsetX,
        int anchorOffsetY,
        DuneDescription description)
    {
        double anchorX = (startX + endX) / 2.0 + anchorOffsetX;
        double anchorY = (startY + endY) / 2.0 + anchorOffsetY;
        double step = 0.5 / (endX - startX);
        int lastX = -1;
        int lastY = -1;

        for (double t = 0.0; t <= 1.0; t += step)
        {
            double firstX = startX + (anchorX - startX) * t;
            double firstY = startY + (anchorY - startY) * t;
            double secondX = anchorX + (endX - anchorX) * t;
            double secondY = anchorY + (endY - anchorY) * t;
            int x = (int)(firstX + (secondX - firstX) * t);
            int y = (int)(firstY + (secondY - firstY) * t);

            if (x == lastX && y == lastY)
            {
                continue;
            }

            lastX = x;
            lastY = y;
            int widthFromCenter = description.Width / 2 - Math.Abs(x - description.CenterX);
            int bottom = description.SurfaceAt(x) + (int)(Math.Sqrt(widthFromCenter) * 3.0);
            for (int clearY = y - 10; clearY < y; clearY++)
            {
                if (IsInWorld(state, x, clearY) &&
                    state.Tiles[x, clearY].Active &&
                    state.Tiles[x, clearY].Type != TileIds.Sand)
                {
                    state.Tiles[x, clearY].Clear();
                }
            }

            for (int sandY = y; sandY < bottom; sandY++)
            {
                if (!IsInWorld(state, x, sandY))
                {
                    continue;
                }

                ref TileData tile = ref state.Tiles[x, sandY];
                tile.Clear();
                tile.SetType(TileIds.Sand);
            }
        }
    }

    private static bool IsInWorld(WorldGenState state, int x, int y)
    {
        return (uint)x < (uint)state.Options.Dimensions.Width &&
            (uint)y < (uint)state.Options.Dimensions.Height;
    }

    private readonly record struct Point2(int X, int Y);

    private sealed class DuneDescription
    {
        private DuneDescription(
            int left,
            int width,
            int centerX,
            int surfaceX,
            short[] surface,
            bool windRight)
        {
            Left = left;
            Width = width;
            CenterX = centerX;
            SurfaceX = surfaceX;
            Surface = surface;
            WindRight = windRight;
        }

        public int Left { get; }

        public int Width { get; }

        public int CenterX { get; }

        public int SurfaceX { get; }

        public short[] Surface { get; }

        public bool WindRight { get; }

        public static DuneDescription Create(
            WorldGenState state,
            UnifiedRandom random,
            int centerX,
            int width,
            int height)
        {
            _ = height;
            int left = centerX - width / 2;
            int surfaceX = left - 20;
            return new DuneDescription(
                left,
                width,
                left + width / 2,
                surfaceX,
                SurfaceMapFromArea(state, surfaceX, width + 40),
                random.Next(2) != 0);
        }

        public int SurfaceAt(int absoluteX)
        {
            return Surface[absoluteX - SurfaceX];
        }

        private static short[] SurfaceMapFromArea(WorldGenState state, int startX, int width)
        {
            int halfHeight = state.Options.Dimensions.Height / 2;
            var surface = new short[width];
            for (int x = startX; x < startX + width; x++)
            {
                bool found = false;
                int height = 0;
                for (int y = 50; y < 50 + halfHeight; y++)
                {
                    if (state.Tiles[x, y].Active)
                    {
                        if (!found)
                        {
                            height = y;
                            found = true;
                        }
                    }

                    if (!found)
                    {
                        height = halfHeight + 50;
                    }
                }

                surface[x - startX] = (short)height;
            }

            return surface;
        }
    }
}
