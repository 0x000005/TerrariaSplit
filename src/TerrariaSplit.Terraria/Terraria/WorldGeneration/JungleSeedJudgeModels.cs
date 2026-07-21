using System.Text.Json.Serialization;

namespace TerrariaSplit.Terraria.WorldGeneration;

internal static class JungleSeedJudgeProtocol
{
    public const int Version = 1;
    public const string CompatibilityId =
        "terraria-1.4.5.6-world-filter-pass62-v3";
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

internal sealed record JungleSeedJudgeRequest(
    int ProtocolVersion,
    string RequestId,
    string SeedText,
    JungleSeedJudgeGameMode GameMode);

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
    public bool Complete =>
        Status == JungleSeedJudgeStatus.Complete &&
        Jungle is not null &&
        CrimsonVertices is { Count: 2 };
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
    IReadOnlyList<JungleResourceLocation> Resources);

internal sealed record JungleRouteSummary(
    JungleRouteStatus Status,
    double DurationMs,
    int BandRadiusTiles,
    int CellCount,
    int DeepestX,
    int DeepestY);

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
