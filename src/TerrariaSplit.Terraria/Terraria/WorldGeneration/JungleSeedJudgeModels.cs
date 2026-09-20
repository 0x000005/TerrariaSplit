using System.Text.Json.Serialization;

namespace TerrariaSplit.Terraria.WorldGeneration;

internal static class JungleSeedJudgeProtocol
{
    public const int Version = 4;
    public const string CompatibilityId =
        "terraria-1.4.5.8-resource-judge-analysis-v4";
}

internal static class ResourceJudgeAnalysis
{
    public const int PyramidItems = 1, PyramidGold = 2, PyramidDepth = 4, Crimson = 8,
        JungleRoute = 16, JungleItems = 32, LifeCrystals = 64, SpelunkerPotions = 128, FeatherfallPotions = 256;
    public const int Pyramids = PyramidItems | PyramidGold | PyramidDepth;
    public const int Resources = JungleItems | LifeCrystals | SpelunkerPotions | FeatherfallPotions;
    public const int Jungle = JungleRoute | Resources;
    public const int All = Pyramids | Crimson | Jungle;
    public static int EndPass(int mask) => (mask & Resources) != 0 ? 62 : (mask & JungleRoute) != 0 ? 59 : (mask & PyramidDepth) != 0 ? 53 : 40;
}

[JsonConverter(typeof(JsonStringEnumConverter<JungleSeedJudgeGameMode>))]
internal enum JungleSeedJudgeGameMode
{
    Classic,
    Expert,
    Master,
    Journey
}

[JsonConverter(typeof(JsonStringEnumConverter<JungleSeedJudgeStatus>))]
internal enum JungleSeedJudgeStatus
{
    Complete,
    InvalidRequest,
    InvalidSeed,
    SpecialSeedUnsupported,
    GenerationFailed
}

[JsonConverter(typeof(JsonStringEnumConverter<JungleSeedAnalysisStatus>))]
internal enum JungleSeedAnalysisStatus
{
    Complete,
    Uncertain
}

[JsonConverter(typeof(JsonStringEnumConverter<JungleRouteStatus>))]
internal enum JungleRouteStatus
{
    Complete,
    Partial,
    Unavailable
}

[JsonConverter(typeof(JsonStringEnumConverter<JungleResourceSource>))]
internal enum JungleResourceSource
{
    Tile,
    Chest
}

internal sealed record JungleSeedJudgeResult(
    int ProtocolVersion,
    string RequestId,
    string CompatibilityId,
    JungleSeedJudgeStatus Status,
    string? SeedText,
    int CheckpointPassIndex,
    double DurationMs,
    double GenerationMs,
    JungleSeedAnalysis? Jungle,
    IReadOnlyList<CrimsonCorridorVertex>? CrimsonVertices,
    string Detail)
{
    public string? ResourceScope { get; init; }
    public int Threads { get; init; }
    public int RequestedThreads { get; init; }
    public int AvailableThreads { get; init; }
    public ResourceJudgePyramidRegion? PyramidRegion { get; init; }
    public IReadOnlyList<ResourceJudgePyramid>? Pyramids { get; init; }
    public ResourceJudgeMetrics? Metrics { get; init; }
    public int AnalysisMask { get; init; }
    public int PlannedEndPass { get; init; }
    public string? ExecutionPath { get; init; }
    public bool EarlyRejected { get; init; }
    public int? PyramidDepthPassIndex { get; init; }
    public int? PyramidGoldPassIndex { get; init; }

    public bool Complete =>
        Status == JungleSeedJudgeStatus.Complete &&
        Threads is >= 1 and <= 4 && AnalysisMask is > 0 and <= ResourceJudgeAnalysis.All && Metrics is not null &&
        ((AnalysisMask & ResourceJudgeAnalysis.Pyramids) == 0 || (PyramidRegion is not null && Pyramids is not null)) &&
        ((AnalysisMask & ResourceJudgeAnalysis.Jungle) == 0 || Jungle is not null) &&
        ((AnalysisMask & ResourceJudgeAnalysis.Crimson) == 0 || CrimsonVertices is { Count: 2 });
}

internal sealed record JungleSeedAnalysis(
    JungleSeedAnalysisStatus AnalysisStatus,
    string Side,
    int OriginX,
    int MinX,
    int MaxX,
    JungleRouteSummary Route,
    double ResourceAnalysisMs,
    int VisitedCostTiles,
    IReadOnlyList<JungleResourceLocation> Resources)
{
    public int GeneratedDeepestY { get; init; }
}

internal sealed record JungleRouteSummary(
    JungleRouteStatus Status,
    double DurationMs,
    int BandRadiusTiles,
    int CellCount,
    int DeepestX,
    [property: JsonPropertyName("reachableDeepestY")] int DeepestY);

internal sealed record ResourceJudgePyramidRegion(int MinimumX, int MaximumX, string Coordinates);
internal sealed record ResourceJudgePoint(int X, int Y);
internal sealed record ResourceJudgePyramid(
    ResourceJudgePoint Entrance,
    int? MainItemType,
    [property: JsonRequired] int? ItemMask,
    [property: JsonRequired] int? GoldCoinPileCount,
    [property: JsonRequired] int? TunnelSurfaceDistance);

// Measurements only. Selection masks, thresholds and AND/OR policy live in the host.
internal sealed record ResourceJudgeMetrics(
    [property: JsonRequired] int? PyramidItemMask,
    [property: JsonRequired] int? DungeonSide,
    [property: JsonRequired] int? NearestDungeonSideCrimsonDistance,
    [property: JsonRequired] int? JungleRouteDeepestY,
    [property: JsonRequired] int? JungleItemMask,
    [property: JsonRequired] int? LifeCrystalCount,
    [property: JsonRequired] int? SpelunkerPotionCount,
    [property: JsonRequired] int? FeatherfallPotionCount);

internal sealed record JungleResourceLocation(
    string Category,
    JungleResourceSource Source,
    int X,
    int Y,
    int? TileId,
    int? ItemId,
    int? ChestX,
    int? ChestY,
    int? ChestStyle,
    int? Slot,
    int Stack,
    int Units,
    double Cost,
    int CostLimit,
    int TravelSteps,
    int DugTiles,
    int? NearestRouteX,
    int? NearestRouteY);

internal sealed record CrimsonCorridorVertex(
    int Index,
    int X,
    int Y);
