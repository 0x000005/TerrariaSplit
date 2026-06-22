namespace TerrariaSplit.Storage;

public sealed class StoredSettingsSnapshotFactory : ISettingsSnapshotFactory
{
    private readonly ISettingsRepository settingsRepository;

    public StoredSettingsSnapshotFactory()
        : this(new AppSettingsRepository())
    {
    }

    public StoredSettingsSnapshotFactory(ISettingsRepository settingsRepository)
    {
        this.settingsRepository = settingsRepository;
    }

    public AppSettings CreateSnapshot(AppSettings settings)
    {
        return settingsRepository.Clone(settings);
    }
}
