namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

internal static class StageOneReset
{
    private const int DungeonSideLeft = -1;
    private const int DungeonSideRight = 1;
    private const int DungeonBeachPadding = 50;

    public static ResetSimulationResult Apply(WorldGenState state)
    {
        WorldOptions options = state.Options;
        if (!options.IsTargetScope)
        {
            return ResetSimulationResult.Unsupported(options.TargetScopeDetail());
        }

        var random = new UnifiedRandom(options.Seed);
        int width = options.Dimensions.Width;
        int height = options.Dimensions.Height;

        InitializeGenVars(state, width, height);

        state.JungleHut = (ushort)random.Next(5);
        state.CrimsonLeft = random.Next(2) != 0;

        RandomizeWeather(random, state);
        ShuffleHellChestItems(random, state);

        state.SlimeRainTime = -random.Next(86400 * 2, 86400 * 3);
        state.CloudBgActive = -random.Next(8640, 86400);

        RollOreTiers(random, state);

        state.Crimson = random.Next(2) == 0;
        state.GeneratingRandomEvil = true;
        state.Crimson = options.HasCrimson;
        state.GeneratingRandomEvil = false;

        state.JungleHut = MapJungleHut(state.JungleHut);
        state.WorldId = random.Next(int.MaxValue);

        RandomizeTreeStyle(random, state, width);
        RandomizeCaveBackgrounds(random, state, width);
        RandomizeBackgrounds(random, state);
        state.MoonType = random.Next(9);

        RollDungeonAndBiomeOrigins(random, state, width);
        RollWorldSizeDependentCounts(state, width);

        state.ResetProbeRandNext = random.Next();
        state.ResetApplied = true;
        return ResetSimulationResult.Applied("WorldGen.Reset non-special seed replica applied.");
    }

    private static void InitializeGenVars(WorldGenState state, int width, int height)
    {
        state.DesertHiveHigh = height;
        state.DesertHiveLow = 0;
        state.DesertHiveLeft = width;
        state.DesertHiveRight = 0;
        state.SkipDesertTileCheck = false;
        state.UndergroundDesertLocation = WorldRect.Empty;
        state.UndergroundDesertHiveLocation = WorldRect.Empty;

        state.WorldSurfaceLow = 0d;
        state.WorldSurface = 0d;
        state.WorldSurfaceHigh = 0d;
        state.RockLayerLow = 0d;
        state.RockLayer = 0d;
        state.RockLayerHigh = 0d;
        state.RemixMushroomLayerLow = 0;
        state.RemixMushroomLayerHigh = 0;
        state.RemixSurfaceLayerLow = 0;
        state.RemixSurfaceLayerHigh = 0;

        state.Copper = 7;
        state.Iron = 6;
        state.Silver = 9;
        state.Gold = 8;
        state.CopperBar = 20;
        state.IronBar = 22;
        state.SilverBar = 21;
        state.GoldBar = 19;

        state.SnowMinX = new int[height];
        state.SnowMaxX = new int[height];
        state.SnowTop = 0;
        state.SnowBottom = 0;

        state.SkyLakes = 1;
        if (width > 8000)
        {
            state.SkyLakes++;
        }

        if (width > 6000)
        {
            state.SkyLakes++;
        }

        state.BeachBordersWidth = 275;
        state.BeachSandRandomCenter = state.BeachBordersWidth + 5 + 40;
        state.BeachSandRandomWidthRange = 20;
        state.BeachSandDungeonExtraWidth = 40;
        state.BeachSandJungleExtraWidth = 20;
        state.OceanWaterStartRandomMin = 220;
        state.OceanWaterStartRandomMax = state.OceanWaterStartRandomMin + 40;
        state.OceanWaterForcedJungleLength = 275;
        state.LeftBeachEnd = 0;
        state.RightBeachStart = 0;
        state.EvilBiomeBeachAvoidance = state.BeachSandRandomCenter + 60;
        state.EvilBiomeAvoidanceMidFixer = 50;
        state.LakesBeachAvoidance = state.BeachSandRandomCenter + 20;
        state.SmallHolesBeachAvoidance = state.BeachSandRandomCenter + 20;
        state.SurfaceCavesBeachAvoidance = state.BeachSandRandomCenter + 20;
        state.SurfaceCavesBeachAvoidance2 = state.BeachSandRandomCenter + 20;
        state.JungleOriginX = 0;
        state.SnowOriginLeft = 0;
        state.SnowOriginRight = 0;
        state.JungleMinX = -1;
        state.JungleMaxX = -1;
        state.LogX = -1;
        state.LogY = -1;
        state.NumMushroomBiomes = 0;
        Array.Clear(state.MushroomBiomesPosition);
        state.ExtraBastStatueCount = 0;
        state.ExtraBastStatueCountMax = 2;
    }

    private static void RandomizeWeather(UnifiedRandom random, WorldGenState state)
    {
        state.NumClouds = random.Next(10, 200);
        state.WindSpeedCurrent = 0f;
        while (state.WindSpeedCurrent == 0f)
        {
            state.WindSpeedCurrent = random.NextFloat() * 0.35f * (float)(random.Next(2) * 2 - 1);
        }
    }

    private static void ShuffleHellChestItems(UnifiedRandom random, WorldGenState state)
    {
        var source = new List<int> { 274, 220, 112, 218, 3019 };
        var shuffled = new List<int>(source.Count);
        while (source.Count > 0)
        {
            int index = random.Next(source.Count);
            shuffled.Add(source[index]);
            source.RemoveAt(index);
        }

        state.HellChestItems = [.. shuffled];
    }

    private static void RollOreTiers(UnifiedRandom random, WorldGenState state)
    {
        if (random.Next(2) == 0)
        {
            state.Copper = 166;
            state.CopperBar = 703;
        }

        if (random.Next(2) == 0)
        {
            state.Iron = 167;
            state.IronBar = 704;
        }

        if (random.Next(2) == 0)
        {
            state.Silver = 168;
            state.SilverBar = 705;
        }

        if (random.Next(2) == 0)
        {
            state.Gold = 169;
            state.GoldBar = 706;
        }
    }

    private static ushort MapJungleHut(ushort jungleHut)
    {
        return jungleHut switch
        {
            0 => 119,
            1 => 120,
            2 => 158,
            3 => 175,
            _ => 45
        };
    }

    private static void RandomizeTreeStyle(UnifiedRandom random, WorldGenState state, int width)
    {
        if (width == 4200)
        {
            state.TreeX[0] = random.Next((int)(width * 0.5 - width * 0.25), (int)(width * 0.5 + width * 0.25));
            state.TreeStyle[0] = random.Next(6);
            state.TreeStyle[1] = random.Next(6);
            while (state.TreeStyle[1] == state.TreeStyle[0])
            {
                state.TreeStyle[1] = random.Next(6);
            }

            state.TreeX[1] = width;
            state.TreeX[2] = width;
            for (int i = 0; i < 2; i++)
            {
                if (state.TreeStyle[i] == 0 && random.Next(3) != 0)
                {
                    state.TreeStyle[i] = 4;
                }
            }

            return;
        }

        if (width == 6400)
        {
            state.TreeX[0] = random.Next((int)(width * 0.334 - width * 0.2), (int)(width * 0.334 + width * 0.2));
            state.TreeX[1] = random.Next((int)(width * 0.667 - width * 0.2), (int)(width * 0.667 + width * 0.2));
            state.TreeStyle[0] = random.Next(6);
            state.TreeStyle[1] = random.Next(6);
            state.TreeStyle[2] = random.Next(6);
            while (state.TreeStyle[1] == state.TreeStyle[0])
            {
                state.TreeStyle[1] = random.Next(6);
            }

            while (state.TreeStyle[2] == state.TreeStyle[0] || state.TreeStyle[2] == state.TreeStyle[1])
            {
                state.TreeStyle[2] = random.Next(6);
            }

            state.TreeX[2] = width;
            for (int i = 0; i < 3; i++)
            {
                if (state.TreeStyle[i] == 0 && random.Next(3) != 0)
                {
                    state.TreeStyle[i] = 4;
                }
            }

            return;
        }

        state.TreeX[0] = random.Next((int)(width * 0.25 - width * 0.15), (int)(width * 0.25 + width * 0.15));
        state.TreeX[1] = random.Next((int)(width * 0.5 - width * 0.15), (int)(width * 0.5 + width * 0.15));
        state.TreeX[2] = random.Next((int)(width * 0.75 - width * 0.15), (int)(width * 0.75 + width * 0.15));
        state.TreeStyle[0] = random.Next(6);
        state.TreeStyle[1] = random.Next(6);
        state.TreeStyle[2] = random.Next(6);
        state.TreeStyle[3] = random.Next(6);
        while (state.TreeStyle[1] == state.TreeStyle[0])
        {
            state.TreeStyle[1] = random.Next(6);
        }

        while (state.TreeStyle[2] == state.TreeStyle[0] || state.TreeStyle[2] == state.TreeStyle[1])
        {
            state.TreeStyle[2] = random.Next(6);
        }

        while (state.TreeStyle[3] == state.TreeStyle[0] ||
               state.TreeStyle[3] == state.TreeStyle[1] ||
               state.TreeStyle[3] == state.TreeStyle[2])
        {
            state.TreeStyle[3] = random.Next(6);
        }

        for (int i = 0; i < 4; i++)
        {
            if (state.TreeStyle[i] == 0 && random.Next(3) != 0)
            {
                state.TreeStyle[i] = 4;
            }
        }
    }

    private static void RandomizeCaveBackgrounds(UnifiedRandom random, WorldGenState state, int width)
    {
        const int maxValue = 8;
        if (width == 4200)
        {
            state.CaveBackX[0] = random.Next((int)(width * 0.5 - width * 0.25), (int)(width * 0.5 + width * 0.25));
            state.CaveBackX[1] = width;
            state.CaveBackX[2] = width;
            state.CaveBackStyle[0] = random.Next(maxValue);
            state.CaveBackStyle[1] = random.Next(maxValue);
            while (state.CaveBackStyle[1] == state.CaveBackStyle[0])
            {
                state.CaveBackStyle[1] = random.Next(maxValue);
            }
        }
        else if (width == 6400)
        {
            state.CaveBackX[0] = random.Next((int)(width * 0.334 - width * 0.2), (int)(width * 0.334 + width * 0.2));
            state.CaveBackX[1] = random.Next((int)(width * 0.667 - width * 0.2), (int)(width * 0.667 + width * 0.2));
            state.CaveBackX[2] = width;
            state.CaveBackStyle[0] = random.Next(maxValue);
            state.CaveBackStyle[1] = random.Next(maxValue);
            state.CaveBackStyle[2] = random.Next(maxValue);
            while (state.CaveBackStyle[1] == state.CaveBackStyle[0])
            {
                state.CaveBackStyle[1] = random.Next(maxValue);
            }

            while (state.CaveBackStyle[2] == state.CaveBackStyle[0] ||
                   state.CaveBackStyle[2] == state.CaveBackStyle[1])
            {
                state.CaveBackStyle[2] = random.Next(maxValue);
            }
        }
        else
        {
            state.CaveBackX[0] = random.Next((int)(width * 0.25 - width * 0.15), (int)(width * 0.25 + width * 0.15));
            state.CaveBackX[1] = random.Next((int)(width * 0.5 - width * 0.15), (int)(width * 0.5 + width * 0.15));
            state.CaveBackX[2] = random.Next((int)(width * 0.75 - width * 0.15), (int)(width * 0.75 + width * 0.15));
            state.CaveBackStyle[0] = random.Next(maxValue);
            state.CaveBackStyle[1] = random.Next(maxValue);
            state.CaveBackStyle[2] = random.Next(maxValue);
            state.CaveBackStyle[3] = random.Next(maxValue);
            while (state.CaveBackStyle[1] == state.CaveBackStyle[0])
            {
                state.CaveBackStyle[1] = random.Next(maxValue);
            }

            while (state.CaveBackStyle[2] == state.CaveBackStyle[0] ||
                   state.CaveBackStyle[2] == state.CaveBackStyle[1])
            {
                state.CaveBackStyle[2] = random.Next(maxValue);
            }

            while (state.CaveBackStyle[3] == state.CaveBackStyle[0] ||
                   state.CaveBackStyle[3] == state.CaveBackStyle[1] ||
                   state.CaveBackStyle[3] == state.CaveBackStyle[2])
            {
                state.CaveBackStyle[3] = random.Next(maxValue);
            }
        }

        state.IceBackStyle = random.Next(4);
        state.HellBackStyle = random.Next(3);
        state.JungleBackStyle = random.Next(2);
    }

    private static void RandomizeBackgrounds(UnifiedRandom random, WorldGenState state)
    {
        state.TreeBackground1 = RollRandomForestBackgroundStyle(random);
        do
        {
            state.TreeBackground2 = RollRandomForestBackgroundStyle(random);
        }
        while (state.TreeBackground2 == state.TreeBackground1);

        state.TreeBackground3 = RollRandomForestBackgroundStyle(random);
        while (state.TreeBackground3 == state.TreeBackground1 ||
               state.TreeBackground3 == state.TreeBackground2)
        {
            state.TreeBackground3 = RollRandomForestBackgroundStyle(random);
        }

        state.TreeBackground4 = RollRandomForestBackgroundStyle(random);
        while (state.TreeBackground4 == state.TreeBackground1 ||
               state.TreeBackground4 == state.TreeBackground2 ||
               state.TreeBackground4 == state.TreeBackground3)
        {
            state.TreeBackground4 = RollRandomForestBackgroundStyle(random);
        }

        state.CorruptBackground = RandomizeCorruptionBackground(random);
        state.JungleBackground = random.Next(7);
        state.SnowBackground = RandomizeSnowBackground(random);
        state.HallowBackground = random.Next(6);
        state.CrimsonBackground = random.Next(7);
        state.DesertBackground = RandomizeDesertBackground(random);
        state.OceanBackground = random.Next(8);
        state.MushroomBackground = random.Next(5);
        state.UnderworldBackground = random.Next(3);
    }

    private static int RollRandomForestBackgroundStyle(UnifiedRandom random)
    {
        const int maxValue = 14;
        int value = random.Next(maxValue);
        if ((value == 1 || value == 2) && random.Next(2) == 0)
        {
            value = random.Next(maxValue);
        }

        if (value == 0)
        {
            value = random.Next(maxValue);
        }

        if (value == 3 && random.Next(3) == 0)
        {
            value = 31;
        }

        if (value == 5 && random.Next(2) == 0)
        {
            value = 51;
        }

        if (value == 7 && random.Next(4) == 0)
        {
            value = random.Next(71, 74);
        }

        return value;
    }

    private static int RandomizeCorruptionBackground(UnifiedRandom random)
    {
        int value = random.Next(6);
        if (value == 5)
        {
            value = random.Next(2) == 0 ? 51 : 52;
        }

        return value;
    }

    private static int RandomizeSnowBackground(UnifiedRandom random)
    {
        int value = random.Next(9);
        if (value == 2 && random.Next(2) == 0)
        {
            value = random.Next(2) == 0 ? 21 : 22;
        }

        if (value == 3 && random.Next(2) == 0)
        {
            value = random.Next(2) == 0 ? 31 : 32;
        }

        if (value == 4 && random.Next(2) == 0)
        {
            value = random.Next(2) == 0 ? 41 : 42;
        }

        return value;
    }

    private static int RandomizeDesertBackground(UnifiedRandom random)
    {
        int value = random.Next(6);
        if (value == 5)
        {
            int variant = random.Next(5);
            value = 51 + variant / 2;
        }

        return value;
    }

    private static void RollDungeonAndBiomeOrigins(UnifiedRandom random, WorldGenState state, int width)
    {
        int side = random.Next(2) == 0 ? DungeonSideLeft : DungeonSideRight;
        state.DungeonSide = side;

        int jungleMinPercent = 15;
        int jungleMaxPercent = 30;
        if (side <= DungeonSideLeft)
        {
            double normalized = 1.0 - random.Next(jungleMinPercent, jungleMaxPercent) * 0.01;
            state.JungleOriginX = (int)(width * normalized);
        }
        else
        {
            double normalized = random.Next(jungleMinPercent, jungleMaxPercent) * 0.01;
            state.JungleOriginX = (int)(width * normalized);
        }

        int snowCenter = random.Next(width);
        if (side == DungeonSideRight)
        {
            while (snowCenter < width * 0.6 || snowCenter > width * 0.75)
            {
                snowCenter = random.Next(width);
            }
        }
        else
        {
            while (snowCenter < width * 0.25 || snowCenter > width * 0.4)
            {
                snowCenter = random.Next(width);
            }
        }

        double widthScale = width / 4200.0;
        int snowHalfWidth = random.Next(50, 90);
        snowHalfWidth += (int)(random.Next(20, 40) * widthScale);
        snowHalfWidth += (int)(random.Next(20, 40) * widthScale);
        int snowLeft = snowCenter - snowHalfWidth;

        snowHalfWidth = random.Next(50, 90);
        snowHalfWidth += (int)(random.Next(20, 40) * widthScale);
        snowHalfWidth += (int)(random.Next(20, 40) * widthScale);
        int snowRight = snowCenter + snowHalfWidth;

        if (snowLeft < 0)
        {
            snowLeft = 0;
        }

        if (snowRight > width)
        {
            snowRight = width;
        }

        state.SnowOriginLeft = snowLeft;
        state.SnowOriginRight = snowRight;

        state.LeftBeachEnd = random.Next(
            state.BeachSandRandomCenter - state.BeachSandRandomWidthRange,
            state.BeachSandRandomCenter + state.BeachSandRandomWidthRange);
        state.LeftBeachEnd += side == DungeonSideRight
            ? state.BeachSandDungeonExtraWidth
            : state.BeachSandJungleExtraWidth;

        state.RightBeachStart = width - random.Next(
            state.BeachSandRandomCenter - state.BeachSandRandomWidthRange,
            state.BeachSandRandomCenter + state.BeachSandRandomWidthRange);
        state.RightBeachStart -= side == DungeonSideLeft
            ? state.BeachSandDungeonExtraWidth
            : state.BeachSandJungleExtraWidth;

        state.DungeonLocation = side <= DungeonSideLeft
            ? random.Next(state.LeftBeachEnd + DungeonBeachPadding, (int)(width * 0.2))
            : random.Next((int)(width * 0.8), state.RightBeachStart - DungeonBeachPadding);
    }

    private static void RollWorldSizeDependentCounts(WorldGenState state, int width)
    {
        int extraBastStatues = 0;
        if (width >= 8400)
        {
            extraBastStatues = 2;
        }
        else if (width >= 6400)
        {
            extraBastStatues = 1;
        }

        state.ExtraBastStatueCountMax = 2 + extraBastStatues;
    }
}

internal readonly record struct ResetSimulationResult(bool IsSupported, string Detail)
{
    public static ResetSimulationResult Applied(string detail)
    {
        return new ResetSimulationResult(true, detail);
    }

    public static ResetSimulationResult Unsupported(string detail)
    {
        return new ResetSimulationResult(false, detail);
    }
}
