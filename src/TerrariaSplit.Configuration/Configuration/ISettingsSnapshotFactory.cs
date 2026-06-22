namespace TerrariaSplit.Configuration;

public interface ISettingsSnapshotFactory
{
    AppSettings CreateSnapshot(AppSettings settings);
}
