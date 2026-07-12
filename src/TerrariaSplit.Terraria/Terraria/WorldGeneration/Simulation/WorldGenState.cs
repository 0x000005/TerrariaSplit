namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

internal sealed class WorldGenState
{
    private readonly List<PyramidChest> pyramidChests = [];
    private readonly List<PyramidCandidate> pyramidCandidates = [];
    private readonly List<PyramidCandidateRisk> pyramidCandidateRisks = [];
    private readonly List<CrimsonBiomeRange> crimsonBiomeRanges = [];
    private readonly List<CrimsonRangeAttemptDiagnostic> crimsonRangeAttemptDiagnostics = [];
    private readonly List<FullDesertCandidateDiagnostic> fullDesertCandidateDiagnostics = [];
    private readonly List<JungleTunnelStep> jungleTunnelSteps = [];

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

    public IReadOnlyList<PyramidChest> PyramidChestsForDiagnostics => pyramidChests;

    public IReadOnlyList<CrimsonBiomeRange> CrimsonBiomeRangesForDiagnostics => crimsonBiomeRanges;

    public bool EnableCrimsonDiagnostics { get; set; }

    public IReadOnlyList<CrimsonRangeAttemptDiagnostic> CrimsonRangeAttemptDiagnostics => crimsonRangeAttemptDiagnostics;

    public bool EnableFullDesertDiagnostics { get; set; }

    public IReadOnlyList<FullDesertCandidateDiagnostic> FullDesertCandidateDiagnostics => fullDesertCandidateDiagnostics;

    public IReadOnlyList<JungleTunnelStep> JungleTunnelSteps => jungleTunnelSteps;

    public void AddJungleTunnelStep(double centerX, double centerY, double strength, int left, int top, int rightExclusive, int bottomExclusive)
    {
        jungleTunnelSteps.Add(new JungleTunnelStep(
            jungleTunnelSteps.Count,
            centerX,
            centerY,
            strength,
            left,
            top,
            rightExclusive,
            bottomExclusive));
    }

    public void ClearWorld()
    {
        Tiles.Clear();
        pyramidChests.Clear();
        pyramidCandidates.Clear();
        pyramidCandidateRisks.Clear();
        crimsonBiomeRanges.Clear();
        crimsonRangeAttemptDiagnostics.Clear();
        fullDesertCandidateDiagnostics.Clear();
        jungleTunnelSteps.Clear();
        EnableCrimsonDiagnostics = false;
        EnableFullDesertDiagnostics = false;
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
        PyramidCandidateRisk risk = WorldInterestArea.IsInSkippedDungeonBoundaryUncertaintyBand(this, x)
            ? PyramidCandidateRisk.SkippedDungeonBoundaryUncertain
            : PyramidCandidateRisk.None;
        pyramidCandidateRisks.Add(risk);
    }

    public void AddCrimsonBiomeRange(int center, int left, int right)
    {
        crimsonBiomeRanges.Add(new CrimsonBiomeRange(center, left, right));
    }

    public void AddCrimsonRangeAttempt(
        int biomeIndex,
        int attemptIndex,
        int center,
        int left,
        int right,
        int jungleLeft,
        int jungleRight,
        int snowLeft,
        int snowRight,
        CrimsonRangeRejectReason rejectReason)
    {
        if (!EnableCrimsonDiagnostics)
        {
            return;
        }

        crimsonRangeAttemptDiagnostics.Add(new CrimsonRangeAttemptDiagnostic(
            biomeIndex,
            attemptIndex,
            center,
            left,
            right,
            jungleLeft,
            jungleRight,
            snowLeft,
            snowRight,
            rejectReason));
    }

    public void AddFullDesertDiagnosticStep(string step, int entranceKind = -1)
    {
        if (!EnableFullDesertDiagnostics)
        {
            return;
        }

        for (int i = 0; i < pyramidCandidates.Count; i++)
        {
            PyramidCandidate candidate = pyramidCandidates[i];
            bool found = TryGetPyramidCandidateScanTile(i, out int scanY, out ushort tileType);
            fullDesertCandidateDiagnostics.Add(new FullDesertCandidateDiagnostic(
                step,
                entranceKind,
                i,
                candidate.X,
                candidate.Y,
                found,
                found ? scanY : -1,
                found ? tileType : (ushort)0));
        }
    }

    public void AddPyramidCandidateRisk(int candidateIndex, PyramidCandidateRisk risk)
    {
        if ((uint)candidateIndex >= (uint)pyramidCandidateRisks.Count)
        {
            return;
        }

        pyramidCandidateRisks[candidateIndex] |= risk;
    }

    public bool TryGetPyramidCandidateScanTile(int candidateIndex, out int scanY, out ushort tileType)
    {
        scanY = 0;
        tileType = 0;
        if ((uint)candidateIndex >= (uint)pyramidCandidates.Count)
        {
            return false;
        }

        PyramidCandidate candidate = pyramidCandidates[candidateIndex];
        int y = Math.Max(0, candidate.Y);
        int limit = Math.Clamp(
            (int)Math.Ceiling(MainWorldSurface),
            0,
            Options.Dimensions.Height);
        while (y < limit && !Tiles[candidate.X, y].Active)
        {
            y++;
        }

        if (y >= limit || !Tiles[candidate.X, y].Active)
        {
            return false;
        }

        scanY = y;
        tileType = Tiles[candidate.X, y].Type;
        return true;
    }

    public void AddRiskToCandidatesWhoseScanStopsAt(int x, int y, PyramidCandidateRisk risk)
    {
        for (int i = 0; i < pyramidCandidates.Count; i++)
        {
            PyramidCandidate candidate = pyramidCandidates[i];
            if (candidate.X != x ||
                y < candidate.Y ||
                y >= MainWorldSurface ||
                !IsFirstActiveScanTile(candidate.X, candidate.Y, y))
            {
                continue;
            }

            AddPyramidCandidateRisk(i, risk);
        }
    }

    public PyramidCandidateRisk GetPyramidCandidateRisk(int candidateIndex)
    {
        return (uint)candidateIndex < (uint)pyramidCandidateRisks.Count
            ? pyramidCandidateRisks[candidateIndex]
            : PyramidCandidateRisk.None;
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

    public void AddPyramidChest(
        int x,
        int y,
        IReadOnlyList<PyramidChestItem> items,
        int candidateIndex,
        int candidateSourceIndex,
        int candidateScanY,
        int candidateSandDepth,
        int candidateSandSpan,
        int candidateActiveDepth)
    {
        pyramidChests.Add(new PyramidChest(
            x,
            y,
            items,
            candidateIndex,
            candidateSourceIndex,
            candidateScanY,
            candidateSandDepth,
            candidateSandSpan,
            candidateActiveDepth));
    }

    public PyramidChestSet ScanTargetPyramidChests()
    {
        Bounds bounds = Bounds.ForSpeedrunCorridor(Options.Dimensions.SizeCode());
        for (int i = 0; i < pyramidChests.Count; i++)
        {
            PyramidChest chest = pyramidChests[i];
            if (bounds.Intersects(chest.X, chest.Y, width: 2, height: 2) &&
                chest.Items.Any(PyramidChestItemNames.IsKnownPyramidItem))
            {
                if (IsRejectedByCandidateRisk(chest))
                {
                    return PyramidChestSet.Empty;
                }

                return new PyramidChestSet([chest]);
            }
        }

        return PyramidChestSet.Empty;
    }

    private bool IsRejectedByCandidateRisk(PyramidChest chest)
    {
        PyramidCandidateRisk risk = GetPyramidCandidateRisk(chest.CandidateIndex);
        if ((risk & PyramidCandidateRisk.HardRejectMask) != 0)
        {
            return true;
        }

        return false;
    }

    private bool IsFirstActiveScanTile(int x, int candidateY, int y)
    {
        if (!WorldGenBounds.InWorld(this, x, y) || !Tiles[x, y].Active)
        {
            return false;
        }

        int startY = Math.Max(0, candidateY);
        for (int scanY = startY; scanY < y; scanY++)
        {
            if (Tiles[x, scanY].Active)
            {
                return false;
            }
        }

        return true;
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

    public const int BlueDungeonBrick = 41;

    public const int GreenDungeonBrick = 43;

    public const int PinkDungeonBrick = 44;

    public const int GoldBrick = 45;

    public const int AncientBlueBrick = 677;

    public const int AncientGreenBrick = 678;

    public const int AncientPinkBrick = 679;

    public const int Sand = 53;

    public const int Mud = 59;

    public const int JungleGrass = 60;

    public const int Silt = 123;

    public const int SnowBlock = 147;

    public const int SandstoneBrick = 151;

    public const int IceBlock = 161;

    public const int Stalactite = 165;

    public const int Cloud = 189;

    public const int MushroomBlock = 190;

    public const int RainCloud = 196;

    public const int CrimsonGrass = 199;

    public const int FleshIce = 200;

    public const int Crimstone = 203;

    public const int Crimtane = 204;

    public const int Slush = 224;

    public const int LihzahrdBrick = 226;

    public const int Crimsand = 234;

    public const int LihzahrdAltar = 237;

    public const int Marble = 367;

    public const int Granite = 368;

    public const int SandStoneSlab = 274;

    public const int CrimsonHardenedSand = 399;

    public const int CorruptSandstone = 400;

    public const int CrimsonSandstone = 401;

    public const int Sandstone = 396;

    public const int HardenedSand = 397;

    public const int CorruptHardenedSand = 398;

    public const int DesertFossil = 404;

    public const int CrackedBlueDungeonBrick = 481;

    public const int CrackedGreenDungeonBrick = 482;

    public const int CrackedPinkDungeonBrick = 483;

    public const int SnowCloud = 460;

    public const int LavaCloud = 717;

    public const int StarCloud = 718;

    public const int RainbowCloud = 719;

    public const int Clay = 40;

    public const int CrimsonJungleGrass = 662;
}

internal readonly record struct PyramidCandidate(int X, int Y, int SourceIndex);

internal readonly record struct CrimsonBiomeRange(int Center, int LeftInclusive, int RightExclusive);

internal readonly record struct CrimsonRangeAttemptDiagnostic(
    int BiomeIndex,
    int AttemptIndex,
    int Center,
    int LeftInclusive,
    int RightExclusive,
    int JungleLeft,
    int JungleRight,
    int SnowLeft,
    int SnowRight,
    CrimsonRangeRejectReason RejectReason);

[Flags]
internal enum CrimsonRangeRejectReason
{
    None = 0,
    Dungeon = 1 << 0,
    WorldCenter = 1 << 1,
    UndergroundDesert = 1 << 2,
    Snow = 1 << 3,
    Jungle = 1 << 4
}

internal readonly record struct FullDesertCandidateDiagnostic(
    string Step,
    int EntranceKind,
    int CandidateIndex,
    int X,
    int StartY,
    bool Found,
    int ScanY,
    ushort TileType);

[Flags]
internal enum PyramidCandidateRisk
{
    None = 0,
    CrimsonConvertedScanSand = 1 << 0,
    FullDesertBoundaryUncertain = 1 << 1,
    SkippedDungeonBoundaryUncertain = 1 << 2,
    JungleMudCoverageUncertain = 1 << 3,
    FullDesertSurfaceUncertain = 1 << 4,

    HardRejectMask =
        CrimsonConvertedScanSand |
        FullDesertBoundaryUncertain |
        SkippedDungeonBoundaryUncertain |
        JungleMudCoverageUncertain |
        FullDesertSurfaceUncertain
}
