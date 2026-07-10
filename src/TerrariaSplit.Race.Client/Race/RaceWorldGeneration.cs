using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.Race.Client;

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
