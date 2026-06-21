namespace TerrariaSplit.Configuration;

internal interface ISettingsSnapshotFactory
{
    AppSettings CreateSnapshot(AppSettings settings);
}
