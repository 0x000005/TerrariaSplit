using System.Text.Json.Serialization;
using TerrariaSplit.Compatibility;

namespace TerrariaSplit.Terraria.WorldGeneration;

internal static class JungleSeedJudgeProtocol
{
    public const int Version = 7;
}

internal sealed record ResourceJudgeRequirements(
    int PyramidItemMask = 0, int PyramidGoldMinimum = 0, int PyramidMaximumDepth = 0,
    int CrimsonMaximumDistance = 0, int JungleMinimumY = 0, int JungleItemMask = 0,
    int LifeCrystalMinimum = 0, int SpelunkerPotionMinimum = 0, int FeatherfallPotionMinimum = 0,
    int StarfuryMaximumDistance = 0, int FinchStaffMaximumDistance = 0,
    int WorldSize = 1, int Evil = 2, TerrariaResourceJudgeVersion TerrariaVersion = TerrariaResourceJudgeVersion.Version1461)
{
    public TerrariaResourceJudgeBuild TargetBuild => TerrariaResourceJudgeBuilds.ForVersion(TerrariaVersion);
    public bool HasResources => JungleItemMask != 0 || LifeCrystalMinimum != 0 || SpelunkerPotionMinimum != 0 || FeatherfallPotionMinimum != 0;
    public int EndPass => HasResources ? 97 : StarfuryMaximumDistance != 0 ? 69 : JungleMinimumY != 0 ? 59
        : PyramidMaximumDepth != 0 ? 53 : FinchStaffMaximumDistance != 0 ? 42 : (PyramidItemMask != 0 || PyramidGoldMinimum != 0) ? 40 : 29;
    public bool PyramidFastEligible => (PyramidItemMask != 0 || PyramidGoldMinimum != 0) && PyramidMaximumDepth == 0
        && CrimsonMaximumDistance == 0 && JungleMinimumY == 0 && !HasResources
        && StarfuryMaximumDistance == 0 && FinchStaffMaximumDistance == 0;
    public void Validate()
    {
        _ = TargetBuild;
        int[] values = [PyramidItemMask, PyramidGoldMinimum, PyramidMaximumDepth, CrimsonMaximumDistance, JungleMinimumY,
            JungleItemMask, LifeCrystalMinimum, SpelunkerPotionMinimum, FeatherfallPotionMinimum, StarfuryMaximumDistance, FinchStaffMaximumDistance];
        int width = WorldSize switch { 1 => 4200, 2 => 6400, 3 => 8400, _ => 0 };
        int height = WorldSize switch { 1 => 1200, 2 => 1800, 3 => 2400, _ => 0 };
        if (width == 0 || Evil is not (1 or 2) || (Evil == 1 && CrimsonMaximumDistance != 0) ||
            values.Any(v => v < 0) || !values.Any(v => v > 0) || (PyramidItemMask & ~7) != 0 || (JungleItemMask & ~11) != 0 ||
            PyramidMaximumDepth > height || JungleMinimumY >= height || CrimsonMaximumDistance > width / 2 ||
            StarfuryMaximumDistance > width / 2 || FinchStaffMaximumDistance > width / 2)
            throw new ArgumentOutOfRangeException(nameof(ResourceJudgeRequirements), "Invalid resource filter requirements.");
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<JungleSeedJudgeGameMode>))]
internal enum JungleSeedJudgeGameMode { Classic, Expert, Master, Journey }
[JsonConverter(typeof(JsonStringEnumConverter<JungleSeedJudgeStatus>))]
internal enum JungleSeedJudgeStatus { Complete, InvalidRequest, InvalidSeed, SpecialSeedUnsupported, GenerationFailed }
[JsonConverter(typeof(JsonStringEnumConverter<JungleSeedJudgeDecision>))]
internal enum JungleSeedJudgeDecision { Accepted, Rejected }

internal sealed record JungleSeedJudgeResult(
    int ProtocolVersion, string RequestId, string CompatibilityId, JungleSeedJudgeStatus Status,
    string? SeedText, JungleSeedJudgeDecision? Decision, string? Reason, int PlannedEndPass,
    int CheckpointPassIndex, string? ExecutionPath, bool EarlyRejected, int RequestedThreads,
    int AvailableThreads, int Threads, double DurationMs, double GenerationMs, string? Detail = null,
    string? TerrariaVersion = null, string? GenerationCompatibilityId = null)
{
    public bool Complete => Status == JungleSeedJudgeStatus.Complete && Decision is not null;
}
