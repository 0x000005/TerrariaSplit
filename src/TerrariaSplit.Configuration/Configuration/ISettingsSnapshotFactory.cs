namespace TerrariaSplit.Configuration;

public interface ISettingsSnapshotFactory
{
    AppSettings CreateSnapshot(AppSettings settings);
}

public sealed class SettingsSnapshotFactory : ISettingsSnapshotFactory
{
    public AppSettings CreateSnapshot(AppSettings settings)
    {
        AppSettings snapshot = AppSettingsCloner.Clone(settings);
        SettingsNormalizer.Normalize(snapshot);
        return snapshot;
    }
}
