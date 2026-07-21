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
        if (result.Status == JungleSeedJudgeStatus.Complete &&
            (result.CheckpointPassIndex != 62 ||
             result.Jungle is null ||
             result.CrimsonVertices is not { Count: 2 }))
        {
            throw new InvalidDataException(
                "Complete World Filter response is missing required analysis data.");
        }

        return result;
    }
}
