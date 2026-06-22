namespace TerrariaSplit.Storage;

public sealed class StoredSettingsSnapshotFactory : ISettingsSnapshotFactory
{
    public AppSettings CreateSnapshot(AppSettings settings)
    {
        return AppSettingsStore.Clone(settings);
    }
}
