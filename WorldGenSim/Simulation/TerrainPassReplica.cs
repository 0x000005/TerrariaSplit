namespace WorldGenSim.Simulation;

internal static class TerrainPassReplica
{
    private const int FlatBeachPadding = 5;

    private enum TerrainFeatureType
    {
        Plateau,
        Hill,
        Dale,
        Mountain,
        Valley
    }

    public static void Apply(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Terrain replica requires a WorldGenState.");
        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        int height = state.Options.Dimensions.Height;

        state.TerrainSurfaceHeights = new int[width];
        state.TerrainRockLayerHeights = new double[width];

        TerrainFeatureType featureType = TerrainFeatureType.Plateau;
        int featureLength = 0;
        double surface = height * 0.3;
        surface *= random.Next(90, 110) * 0.005;
        double rock = surface + height * 0.2;
        rock *= random.Next(90, 110) * 0.01;

        double surfaceLow = surface;
        double surfaceHigh = surface;
        double rockLow = rock;
        double rockHigh = rock;
        double beachSurfaceLimit = height * 0.23;
        var history = new SurfaceHistory(500);
        featureLength = state.LeftBeachEnd + FlatBeachPadding;

        for (int x = 0; x < width; x++)
        {
            progress.Set((double)x / width);
            surfaceLow = Math.Min(surface, surfaceLow);
            surfaceHigh = Math.Max(surface, surfaceHigh);
            rockLow = Math.Min(rock, rockLow);
            rockHigh = Math.Max(rock, rockHigh);

            if (featureLength <= 0)
            {
                featureType = (TerrainFeatureType)random.Next(0, 5);
                featureLength = random.Next(5, 40);
                if (featureType == TerrainFeatureType.Plateau)
                {
                    featureLength *= (int)(random.Next(5, 30) * 0.2);
                }
            }

            featureLength--;
            if (x > width * 0.45 && x < width * 0.55 &&
                (featureType == TerrainFeatureType.Mountain || featureType == TerrainFeatureType.Valley))
            {
                featureType = (TerrainFeatureType)random.Next(3);
            }

            if (x > width * 0.48 && x < width * 0.52)
            {
                featureType = TerrainFeatureType.Plateau;
            }

            surface += GenerateWorldSurfaceOffset(random, featureType);

            double surfaceMinRatio = 0.17;
            const double surfaceMaxRatio = 0.26;
            if (state.Options.Dimensions.SizeCode() == 1)
            {
                surfaceMinRatio += 0.02;
            }

            if (x < state.LeftBeachEnd + FlatBeachPadding || x > state.RightBeachStart - FlatBeachPadding)
            {
                surface = Math.Clamp(surface, height * surfaceMinRatio, beachSurfaceLimit);
            }
            else if (surface < height * surfaceMinRatio)
            {
                surface = height * surfaceMinRatio;
                featureLength = 0;
            }
            else if (surface > height * surfaceMaxRatio)
            {
                surface = height * surfaceMaxRatio;
                featureLength = 0;
            }

            while (random.Next(0, 3) == 0)
            {
                rock += random.Next(-2, 3);
            }

            if (rock < surface + height * 0.06)
            {
                rock += 1.0;
            }

            if (rock > surface + height * 0.35)
            {
                rock -= 1.0;
            }

            history.Record(surface);
            FillColumn(state, x, surface, rock);

            if (x == state.RightBeachStart - FlatBeachPadding)
            {
                if (surface > beachSurfaceLimit)
                {
                    RetargetSurfaceHistory(state, history, x, beachSurfaceLimit);
                }

                featureType = TerrainFeatureType.Plateau;
                featureLength = width - x;
            }
        }

        state.MainWorldSurface = (int)(surfaceHigh + 25.0);
        state.MainRockLayer = rockHigh;
        double layerDelta = (int)((state.MainRockLayer - state.MainWorldSurface) / 6.0) * 6;
        state.MainRockLayer = (int)(state.MainWorldSurface + layerDelta);

        int waterLine = (int)(state.MainRockLayer + height) / 2 + random.Next(-100, 20);
        int lavaLine = waterLine + random.Next(50, 80);

        const int minimumSurfaceRockGap = 20;
        if (rockLow < surfaceHigh + minimumSurfaceRockGap)
        {
            double midpoint = (rockLow + surfaceHigh) / 2.0;
            double spread = Math.Abs(rockLow - surfaceHigh);
            if (spread < minimumSurfaceRockGap)
            {
                spread = minimumSurfaceRockGap;
            }

            rockLow = midpoint + spread / 2.0;
            surfaceHigh = midpoint - spread / 2.0;
        }

        state.RockLayer = rock;
        state.RockLayerHigh = rockHigh;
        state.RockLayerLow = rockLow;
        state.WorldSurface = surface;
        state.WorldSurfaceHigh = surfaceHigh;
        state.WorldSurfaceLow = surfaceLow;
        state.WaterLine = waterLine;
        state.LavaLine = lavaLine;
        state.RemixMushroomLayerLow = height - 350;
        state.RemixMushroomLayerHigh = state.UnderworldLayer;
        state.RemixSurfaceLayerLow = (int)state.RockLayerLow;
        state.RemixSurfaceLayerHigh = state.RemixMushroomLayerLow;
        state.TerrainApplied = true;
    }

    private static void FillColumn(WorldGenState state, int x, double worldSurface, double rockLayer)
    {
        for (int y = 0; y < worldSurface; y++)
        {
            state.Tiles[x, y].Clear();
        }

        for (int y = (int)worldSurface; y < state.Options.Dimensions.Height; y++)
        {
            state.Tiles[x, y].SetType(y < rockLayer ? TileIds.Dirt : TileIds.Stone);
        }

        state.TerrainSurfaceHeights[x] = (int)worldSurface;
        state.TerrainRockLayerHeights[x] = rockLayer;
    }

    private static void RetargetColumn(WorldGenState state, int x, double worldSurface)
    {
        if ((uint)x >= (uint)state.Options.Dimensions.Width)
        {
            return;
        }

        for (int y = 0; y < worldSurface; y++)
        {
            state.Tiles[x, y].Clear();
        }

        for (int y = (int)worldSurface; y < state.Options.Dimensions.Height; y++)
        {
            ref TileData tile = ref state.Tiles[x, y];
            if (!tile.IsActiveType(TileIds.Stone))
            {
                tile.SetType(TileIds.Dirt);
            }
        }

        state.TerrainSurfaceHeights[x] = (int)worldSurface;
    }

    private static double GenerateWorldSurfaceOffset(UnifiedRandom random, TerrainFeatureType featureType)
    {
        double offset = 0.0;
        switch (featureType)
        {
            case TerrainFeatureType.Plateau:
                while (random.Next(0, 7) == 0)
                {
                    offset += random.Next(-1, 2);
                }

                break;
            case TerrainFeatureType.Hill:
                while (random.Next(0, 4) == 0)
                {
                    offset -= 1.0;
                }

                while (random.Next(0, 10) == 0)
                {
                    offset += 1.0;
                }

                break;
            case TerrainFeatureType.Dale:
                while (random.Next(0, 4) == 0)
                {
                    offset += 1.0;
                }

                while (random.Next(0, 10) == 0)
                {
                    offset -= 1.0;
                }

                break;
            case TerrainFeatureType.Mountain:
                while (random.Next(0, 2) == 0)
                {
                    offset -= 1.0;
                }

                while (random.Next(0, 6) == 0)
                {
                    offset += 1.0;
                }

                break;
            case TerrainFeatureType.Valley:
                while (random.Next(0, 2) == 0)
                {
                    offset += 1.0;
                }

                while (random.Next(0, 5) == 0)
                {
                    offset -= 1.0;
                }

                break;
        }

        return offset;
    }

    private static void RetargetSurfaceHistory(
        WorldGenState state,
        SurfaceHistory history,
        int targetX,
        double targetHeight)
    {
        for (int i = 0; i < history.Length / 2; i++)
        {
            if (history[history.Length - 1] <= targetHeight)
            {
                break;
            }

            for (int j = 0; j < history.Length - i * 2; j++)
            {
                double height = history[history.Length - j - 1] - 1.0;
                history[history.Length - j - 1] = height;
                if (height <= targetHeight)
                {
                    break;
                }
            }
        }

        for (int k = 0; k < history.Length; k++)
        {
            double worldSurface = history[history.Length - k - 1];
            RetargetColumn(state, targetX - k, worldSurface);
        }
    }

    private sealed class SurfaceHistory
    {
        private readonly double[] heights;
        private int index;

        public SurfaceHistory(int size)
        {
            heights = new double[size];
        }

        public int Length => heights.Length;

        public double this[int offset]
        {
            get => heights[(offset + index) % heights.Length];
            set => heights[(offset + index) % heights.Length] = value;
        }

        public void Record(double height)
        {
            heights[index] = height;
            index = (index + 1) % heights.Length;
        }
    }
}
