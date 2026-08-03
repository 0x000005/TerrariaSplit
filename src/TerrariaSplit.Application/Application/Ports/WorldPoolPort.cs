namespace TerrariaSplit.Application.Ports;

public interface IWorldPoolStore
{
    int Count(string signature);

    void EnsureSignature(string signature);

    bool TryAdd(
        string signature,
        string sourceWorldPath,
        TerrariaWorldSeedMetadata metadata,
        out WorldPoolItem item);

    bool TryPeekFirst(string signature, out WorldPoolItem item);

    void RemoveFirst(string signature, WorldPoolItem item);

    bool TryInstallWorld(
        WorldPoolItem item,
        string worldsPath,
        out string installedPath,
        out string message);

    bool TryGetWorldPath(WorldPoolItem item, out string worldPath);
}

public readonly record struct WorldPoolItem(
    string WorldFileName,
    TerrariaWorldSeedMetadata Metadata);
