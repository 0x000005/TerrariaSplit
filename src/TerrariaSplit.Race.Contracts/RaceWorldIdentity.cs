namespace TerrariaSplit.Race.Contracts;

public sealed record RaceWorldIdentity(
    string Name,
    int WorldId,
    Guid UniqueId);
