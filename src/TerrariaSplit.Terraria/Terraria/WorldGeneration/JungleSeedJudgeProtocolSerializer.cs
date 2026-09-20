using System.Text.Json;
using System.Text.Json.Serialization;

namespace TerrariaSplit.Terraria.WorldGeneration;

internal static class JungleSeedJudgeProtocolSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    public static JungleSeedJudgeResult DeserializeResponse(
        string responseJson,
        string expectedRequestId)
    {
        JungleSeedJudgeResult result;
        try
        {
            result = JsonSerializer.Deserialize<JungleSeedJudgeResult>(
                responseJson,
                JsonOptions) ?? throw new InvalidDataException(
                    "World filter returned an empty JSON value.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "World filter returned invalid protocol JSON.",
                ex);
        }

        if (result.ProtocolVersion != JungleSeedJudgeProtocol.Version)
        {
            throw new InvalidDataException(
                $"Unsupported World Filter protocolVersion " +
                $"{result.ProtocolVersion}.");
        }
        if (!string.Equals(
                result.CompatibilityId,
                JungleSeedJudgeProtocol.CompatibilityId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "World Filter compatibilityId does not match TerrariaSplit.");
        }
        if (!string.Equals(
                result.RequestId,
                expectedRequestId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "World Filter response requestId does not match the request.");
        }
        if (result.Status == JungleSeedJudgeStatus.Complete && !ValidAnalysis(result))
        {
            throw new InvalidDataException(
                "Complete World Filter response is missing required analysis data.");
        }

        return result;
    }

    private static bool ValidAnalysis(JungleSeedJudgeResult r)
    {
        if (!r.Complete || r.Metrics is not { } m) return false;
        int mask = r.AnalysisMask;
        if (r.RequestedThreads is < 0 or > 4 || r.AvailableThreads < 1 ||
            r.Threads != Math.Min(r.RequestedThreads == 0 ? 4 : r.RequestedThreads, r.AvailableThreads)) return false;
        bool Needs(int bit) => (mask & bit) != 0;
        bool Number(int bit, int? value) => Needs(bit) ? value is >= 0 : value is null;
        int end = ResourceJudgeAnalysis.EndPass(mask);
        bool fast = mask == ResourceJudgeAnalysis.PyramidItems;
        if (r.PlannedEndPass != end || r.ExecutionPath != (fast ? "PyramidFast" : "FullPrefix") ||
            (r.EarlyRejected ? !fast || r.CheckpointPassIndex is not (2 or 32) || r.Pyramids is not { Count: 0 }
                : r.CheckpointPassIndex != end)) return false;
        if (r.ResourceScope != (Needs(ResourceJudgeAnalysis.Resources) ? "Pass62" : null) ||
            r.PyramidDepthPassIndex != (Needs(ResourceJudgeAnalysis.PyramidDepth) ? 53 : (int?)null) ||
            r.PyramidGoldPassIndex != (Needs(ResourceJudgeAnalysis.PyramidGold) ? 40 : (int?)null)) return false;
        if (Needs(ResourceJudgeAnalysis.Pyramids)
            ? r.PyramidRegion is not { MinimumX: 1260, MaximumX: 2940, Coordinates: "anchor" }
            : r.Pyramids is not null || r.PyramidRegion is not null) return false;
        if (!Needs(ResourceJudgeAnalysis.Jungle) && r.Jungle is not null) return false;
        if (!Needs(ResourceJudgeAnalysis.Crimson) && (r.CrimsonVertices is not null || m.DungeonSide is not null || m.NearestDungeonSideCrimsonDistance is not null)) return false;
        if (Needs(ResourceJudgeAnalysis.Crimson) && (m.DungeonSide is not (-1 or 1) || m.NearestDungeonSideCrimsonDistance is < 0 or > 2100)) return false;
        if (!Needs(ResourceJudgeAnalysis.JungleRoute) && m.JungleRouteDeepestY is not null) return false;
        if (!Number(ResourceJudgeAnalysis.PyramidItems, m.PyramidItemMask) ||
            !Number(ResourceJudgeAnalysis.JungleItems, m.JungleItemMask) ||
            !Number(ResourceJudgeAnalysis.LifeCrystals, m.LifeCrystalCount) ||
            !Number(ResourceJudgeAnalysis.SpelunkerPotions, m.SpelunkerPotionCount) ||
            !Number(ResourceJudgeAnalysis.FeatherfallPotions, m.FeatherfallPotionCount)) return false;
        return r.Pyramids is null || r.Pyramids.All(p =>
            Number(ResourceJudgeAnalysis.PyramidItems, p.ItemMask) &&
            Number(ResourceJudgeAnalysis.PyramidGold, p.GoldCoinPileCount) &&
            Number(ResourceJudgeAnalysis.PyramidDepth, p.TunnelSurfaceDistance));
    }
}
