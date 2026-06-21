namespace TerrariaSplit.Storage;

internal sealed class StoredSettingsSnapshotFactory : ISettingsSnapshotFactory
{
    public AppSettings CreateSnapshot(AppSettings settings)
    {
        return AppSettingsStore.Clone(settings);
    }
}
