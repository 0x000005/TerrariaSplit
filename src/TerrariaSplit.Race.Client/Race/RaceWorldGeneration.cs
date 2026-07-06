using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.Race.Client;

public sealed record RaceWorldGenerationRequest(
    RaceWorldSettings WorldSettings,
    RaceSeedAssignment Seed,
    string WorldName);

public sealed record RaceWorldGenerationResult(
    bool Succeeded,
    string WorldPath,
    string Message)
{
    public static RaceWorldGenerationResult Success(string worldPath)
    {
        return new RaceWorldGenerationResult(true, worldPath, string.Empty);
    }

    public static RaceWorldGenerationResult Failure(string message)
    {
        return new RaceWorldGenerationResult(false, string.Empty, message);
    }
}

public interface IRaceWorldGenerator
{
    Task<RaceWorldGenerationResult> GenerateAndInstallAsync(
        RaceWorldGenerationRequest request,
        CancellationToken cancellationToken);
}

public sealed record RaceWorldFileTransferResult(
    bool Succeeded,
    string WorldPath,
    string Message,
    RaceWorldFileInfo? WorldFile)
{
    public static RaceWorldFileTransferResult Success(string worldPath, RaceWorldFileInfo worldFile)
    {
        return new RaceWorldFileTransferResult(true, worldPath, string.Empty, worldFile);
    }

    public static RaceWorldFileTransferResult Failure(string message)
    {
        return new RaceWorldFileTransferResult(false, string.Empty, message, null);
    }
}
