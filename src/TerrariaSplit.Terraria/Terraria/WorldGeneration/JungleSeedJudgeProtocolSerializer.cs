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

    public static JungleSeedJudgeResult DeserializeResponse(string responseJson, string expectedRequestId)
    {
        JungleSeedJudgeResult result;
        try
        {
            result = JsonSerializer.Deserialize<JungleSeedJudgeResult>(responseJson, JsonOptions)
                ?? throw new InvalidDataException("World filter returned an empty JSON value.");
        }
        catch (JsonException ex) { throw new InvalidDataException("World filter returned invalid protocol JSON.", ex); }
        if (result.ProtocolVersion != JungleSeedJudgeProtocol.Version || result.CompatibilityId != JungleSeedJudgeProtocol.CompatibilityId)
            throw new InvalidDataException("World Filter protocol/compatibility mismatch.");
        if (result.RequestId != expectedRequestId) throw new InvalidDataException("World Filter requestId mismatch.");
        if (result.Status == JungleSeedJudgeStatus.Complete && !ValidDecision(result))
            throw new InvalidDataException("Complete World Filter response has invalid decision data.");
        return result;
    }

    private static bool ValidDecision(JungleSeedJudgeResult r)
    {
        if (!r.Complete || r.Decision is not (JungleSeedJudgeDecision.Accepted or JungleSeedJudgeDecision.Rejected) || string.IsNullOrEmpty(r.SeedText) || string.IsNullOrEmpty(r.Reason) ||
            r.RequestedThreads is < 0 or > 4 || r.AvailableThreads < 1 ||
            r.Threads != Math.Min(r.RequestedThreads == 0 ? 4 : r.RequestedThreads, r.AvailableThreads) ||
            r.PlannedEndPass is not (29 or 40 or 42 or 53 or 59 or 69 or 97) ||
            !double.IsFinite(r.DurationMs) || r.DurationMs < 0 || !double.IsFinite(r.GenerationMs) || r.GenerationMs < 0)
            return false;
        bool fast = r.ExecutionPath == "PyramidFast";
        if (!fast && r.ExecutionPath != "FullPrefix") return false;
        if (fast && r.PlannedEndPass != 40) return false;
        if (r.EarlyRejected != (r.Decision == JungleSeedJudgeDecision.Rejected && r.CheckpointPassIndex < r.PlannedEndPass)) return false;
        if (r.CheckpointPassIndex > r.PlannedEndPass) return false;
        if (r.Decision != JungleSeedJudgeDecision.Rejected) return r.CheckpointPassIndex == r.PlannedEndPass;
        return fast ? r.CheckpointPassIndex is 2 or 32 or 40 : r.CheckpointPassIndex is 2 or 29 or 32 or 40 or 42 or 53 or 59 or 69 or 97;
    }
}
