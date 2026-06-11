namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

internal sealed class WorldGenState
{
    private readonly List<PyramidChest> pyramidChests = [];
    private readonly List<PyramidCandidate> pyramidCandidates = [];

    public WorldGenState(WorldOptions options)
    {
        Options = options;
        Tiles = new DenseTileGrid(options.Dimensions);
    }

    public WorldOptions Options { get; }

    public DenseTileGrid Tiles { get; }

    public bool ResetApplied { get; set; }

    public int ResetProbeRandNext { get; set; }

    public int WorldId { get; set; }

    public bool Crimson { get; set; }

    public bool GeneratingRandomEvil { get; set; }

    public bool CrimsonLeft { get; set; }

    public int Copper { get; set; } = 7;

    public int Iron { get; set; } = 6;

    public int Silver { get; set; } = 9;

    public int Gold { get; set; } = 8;

    public int CopperBar { get; set; } = 20;

    public int IronBar { get; set; } = 22;

    public int SilverBar { get; set; } = 21;

    public int GoldBar { get; set; } = 19;

    public int[] HellChestItems { get; set; } = [];

    public int SlimeRainTime { get; set; }

    public int CloudBgActive { get; set; }

    public int NumClouds { get; set; }

    public float WindSpeedCurrent { get; set; }

    public ushort JungleHut { get; set; }

    public int[] TreeX { get; } = new int[3];

    public int[] TreeStyle { get; } = new int[4];

    public int[] CaveBackX { get; } = new int[3];

    public int[] CaveBackStyle { get; } = new int[4];

    public int IceBackStyle { get; set; }

    public int HellBackStyle { get; set; }

    public int JungleBackStyle { get; set; }

    public int TreeBackground1 { get; set; }

    public int TreeBackground2 { get; set; }

    public int TreeBackground3 { get; set; }

    public int TreeBackground4 { get; set; }

    public int CorruptBackground { get; set; }

    public int JungleBackground { get; set; }

    public int SnowBackground { get; set; }

    public int HallowBackground { get; set; }

    public int CrimsonBackground { get; set; }

    public int DesertBackground { get; set; }

    public int OceanBackground { get; set; }

    public int MushroomBackground { get; set; }

    public int UnderworldBackground { get; set; }

    public int MoonType { get; set; }

    public bool TerrainApplied { get; set; }

    public int TerrainProbeRandNext { get; set; }

    public bool DunesApplied { get; set; }

    public int DunesProbeRandNext { get; set; }

    public bool OceanSandApplied { get; set; }

    public int OceanSandProbeRandNext { get; set; }

    public bool SandPatchesApplied { get; set; }

    public int SandPatchesProbeRandNext { get; set; }

    public double MainWorldSurface { get; set; }

    public double MainRockLayer { get; set; }

    public double WorldSurface { get; set; }

    public double WorldSurfaceLow { get; set; }

    public double WorldSurfaceHigh { get; set; }

    public double RockLayer { get; set; }

    public double RockLayerLow { get; set; }

    public double RockLayerHigh { get; set; }

    public int WaterLine { get; set; }

    public int LavaLine { get; set; }

    public int UnderworldLayer => Options.Dimensions.Height - 200;

    public int[] TerrainSurfaceHeights { get; set; } = [];

    public double[] TerrainRockLayerHeights { get; set; } = [];

    public int RemixMushroomLayerLow { get; set; }

    public int RemixMushroomLayerHigh { get; set; }

    public int RemixSurfaceLayerLow { get; set; }

    public int RemixSurfaceLayerHigh { get; set; }

    public int DesertHiveHigh { get; set; }

    public int DesertHiveLow { get; set; }

    public int DesertHiveLeft { get; set; }

    public int DesertHiveRight { get; set; }

    public bool SkipDesertTileCheck { get; set; }

    public WorldRect UndergroundDesertLocation { get; set; } = WorldRect.Empty;

    public WorldRect UndergroundDesertHiveLocation { get; set; } = WorldRect.Empty;

    public int SkyLakes { get; set; }

    public int BeachBordersWidth { get; set; }

    public int BeachSandRandomCenter { get; set; }

    public int BeachSandRandomWidthRange { get; set; }

    public int BeachSandDungeonExtraWidth { get; set; }

    public int BeachSandJungleExtraWidth { get; set; }

    public int OceanWaterStartRandomMin { get; set; }

    public int OceanWaterStartRandomMax { get; set; }

    public int OceanWaterForcedJungleLength { get; set; }

    public int EvilBiomeBeachAvoidance { get; set; }

    public int EvilBiomeAvoidanceMidFixer { get; set; }

    public int LakesBeachAvoidance { get; set; }

    public int SmallHolesBeachAvoidance { get; set; }

    public int SurfaceCavesBeachAvoidance { get; set; }

    public int SurfaceCavesBeachAvoidance2 { get; set; }

    public int JungleOriginX { get; set; }

    public int JungleMinX { get; set; } = -1;

    public int JungleMaxX { get; set; } = -1;

    public int SnowOriginLeft { get; set; }

    public int SnowOriginRight { get; set; }

    public int[] SnowMinX { get; set; } = [];

    public int[] SnowMaxX { get; set; } = [];

    public int SnowTop { get; set; }

    public int SnowBottom { get; set; }

    public int LeftBeachEnd { get; set; }

    public int RightBeachStart { get; set; }

    public int DungeonSide { get; set; }

    public int DungeonLocation { get; set; }

    public int DungeonX { get; set; }

    public int DungeonY { get; set; }

    public int ExtraBastStatueCount { get; set; }

    public int ExtraBastStatueCountMax { get; set; } = 2;

    public int LogX { get; set; } = -1;

    public int LogY { get; set; } = -1;

    public bool MudWall { get; set; }

    public int JungleX { get; set; }

    public int NumMushroomBiomes { get; set; }

    public (int X, int Y)[] MushroomBiomesPosition { get; } = new (int X, int Y)[50];

    public int NumTunnels { get; set; }

    public int[] TunnelX { get; } = new int[50];

    public int NumMountainCaves { get; set; }

    public int[] MountainCaveX { get; } = new int[30];

    public int[] MountainCaveY { get; } = new int[30];

    public IReadOnlyList<PyramidCandidate> PyramidCandidates => pyramidCandidates;

    public void ClearWorld()
    {
        Tiles.Clear();
        pyramidChests.Clear();
        pyramidCandidates.Clear();
        ResetApplied = false;
        ResetProbeRandNext = 0;
        WorldId = 0;
        Crimson = false;
        GeneratingRandomEvil = false;
        CrimsonLeft = false;
        Copper = 7;
        Iron = 6;
        Silver = 9;
        Gold = 8;
        CopperBar = 20;
        IronBar = 22;
        SilverBar = 21;
        GoldBar = 19;
        HellChestItems = [];
        SlimeRainTime = 0;
        CloudBgActive = 0;
        NumClouds = 0;
        WindSpeedCurrent = 0f;
        JungleHut = 0;
        Array.Clear(TreeX);
        Array.Clear(TreeStyle);
        Array.Clear(CaveBackX);
        Array.Clear(CaveBackStyle);
        IceBackStyle = 0;
        HellBackStyle = 0;
        JungleBackStyle = 0;
        TreeBackground1 = 0;
        TreeBackground2 = 0;
        TreeBackground3 = 0;
        TreeBackground4 = 0;
        CorruptBackground = 0;
        JungleBackground = 0;
        SnowBackground = 0;
        HallowBackground = 0;
        CrimsonBackground = 0;
        DesertBackground = 0;
        OceanBackground = 0;
        MushroomBackground = 0;
        UnderworldBackground = 0;
        MoonType = 0;
        TerrainApplied = false;
        TerrainProbeRandNext = 0;
        DunesApplied = false;
        DunesProbeRandNext = 0;
        OceanSandApplied = false;
        OceanSandProbeRandNext = 0;
        SandPatchesApplied = false;
        SandPatchesProbeRandNext = 0;
        MainWorldSurface = 0d;
        MainRockLayer = 0d;
        WorldSurface = 0d;
        WorldSurfaceLow = 0d;
        WorldSurfaceHigh = 0d;
        RockLayer = 0d;
        RockLayerLow = 0d;
        RockLayerHigh = 0d;
        WaterLine = 0;
        LavaLine = 0;
        TerrainSurfaceHeights = [];
        TerrainRockLayerHeights = [];
        RemixMushroomLayerLow = 0;
        RemixMushroomLayerHigh = 0;
        RemixSurfaceLayerLow = 0;
        RemixSurfaceLayerHigh = 0;
        DesertHiveHigh = Options.Dimensions.Height;
        DesertHiveLow = 0;
        DesertHiveLeft = Options.Dimensions.Width;
        DesertHiveRight = 0;
        SkipDesertTileCheck = false;
        UndergroundDesertLocation = WorldRect.Empty;
        UndergroundDesertHiveLocation = WorldRect.Empty;
        SkyLakes = 0;
        BeachBordersWidth = 0;
        BeachSandRandomCenter = 0;
        BeachSandRandomWidthRange = 0;
        BeachSandDungeonExtraWidth = 0;
        BeachSandJungleExtraWidth = 0;
        OceanWaterStartRandomMin = 0;
        OceanWaterStartRandomMax = 0;
        OceanWaterForcedJungleLength = 0;
        EvilBiomeBeachAvoidance = 0;
        EvilBiomeAvoidanceMidFixer = 0;
        LakesBeachAvoidance = 0;
        SmallHolesBeachAvoidance = 0;
        SurfaceCavesBeachAvoidance = 0;
        SurfaceCavesBeachAvoidance2 = 0;
        JungleOriginX = 0;
        JungleMinX = -1;
        JungleMaxX = -1;
        SnowOriginLeft = 0;
        SnowOriginRight = 0;
        SnowMinX = [];
        SnowMaxX = [];
        SnowTop = 0;
        SnowBottom = 0;
        LeftBeachEnd = 0;
        RightBeachStart = 0;
        DungeonSide = 0;
        DungeonLocation = 0;
        DungeonX = 0;
        DungeonY = 0;
        ExtraBastStatueCount = 0;
        ExtraBastStatueCountMax = 2;
        LogX = -1;
        LogY = -1;
        MudWall = false;
        JungleX = 0;
        NumMushroomBiomes = 0;
        Array.Clear(MushroomBiomesPosition);
        NumTunnels = 0;
        Array.Clear(TunnelX);
        NumMountainCaves = 0;
        Array.Clear(MountainCaveX);
        Array.Clear(MountainCaveY);
    }

    public void AddPyramidCandidate(int x, int y, int sourceIndex)
    {
        pyramidCandidates.Add(new PyramidCandidate(x, y, sourceIndex));
    }

    public void IncludeJungleMudColumns(int leftInclusive, int rightExclusive)
    {
        int left = Math.Clamp(leftInclusive, 0, Options.Dimensions.Width);
        int right = Math.Clamp(rightExclusive, left, Options.Dimensions.Width);
        if (right <= left)
        {
            return;
        }

        JungleMinX = JungleMinX < 0 ? left : Math.Min(JungleMinX, left);
        JungleMaxX = JungleMaxX < 0 ? right : Math.Max(JungleMaxX, right);
    }

    public void AddPyramidChest(int x, int y, IReadOnlyList<PyramidChestItem> items)
    {
        pyramidChests.Add(new PyramidChest(x, y, items));
    }

    public PyramidChestSet ScanTargetPyramidChests()
    {
        Bounds bounds = Bounds.ForSpeedrunCorridor(Options.Dimensions.SizeCode());
        // Later simulated pyramid candidates can be false positives when earlier pyramid terrain blocks them in the real world.
        for (int i = 0; i < pyramidChests.Count; i++)
        {
            PyramidChest chest = pyramidChests[i];
            if (bounds.Intersects(chest.X, chest.Y, width: 2, height: 2) &&
                chest.Items.Any(PyramidChestItemNames.IsKnownPyramidItem))
            {
                return new PyramidChestSet([chest]);
            }
        }

        return PyramidChestSet.Empty;
    }

}

internal static class WorldDimensionsExtensions
{
    public static int SizeCode(this WorldDimensions dimensions)
    {
        return (dimensions.Width, dimensions.Height) switch
        {
            (4200, 1200) => 1,
            (8400, 2400) => 3,
            _ => 2
        };
    }
}

internal static class TileIds
{
    public const int Dirt = 0;

    public const int Stone = 1;

    public const int Grass = 2;

    public const int Sand = 53;

    public const int Mud = 59;

    public const int Silt = 123;

    public const int SnowBlock = 147;

    public const int SandstoneBrick = 151;

    public const int IceBlock = 161;

    public const int Stalactite = 165;

    public const int CrimsonGrass = 199;

    public const int FleshIce = 200;

    public const int Crimstone = 203;

    public const int Crimtane = 204;

    public const int Slush = 224;

    public const int Crimsand = 234;

    public const int Marble = 367;

    public const int Granite = 368;

    public const int SandStoneSlab = 274;

    public const int CrimsonHardenedSand = 399;

    public const int CrimsonSandstone = 401;

    public const int Sandstone = 396;

    public const int HardenedSand = 397;

    public const int DesertFossil = 404;

    public const int Clay = 40;

    public const int CrimsonJungleGrass = 662;
}

internal readonly record struct PyramidCandidate(int X, int Y, int SourceIndex);
