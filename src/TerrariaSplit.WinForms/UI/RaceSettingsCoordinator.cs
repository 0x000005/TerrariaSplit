namespace TerrariaSplit.UI;

internal sealed class RaceSettingsCoordinator
{
    private readonly Func<RaceSettings> getCurrentSettings;
    private readonly Action<RaceSettings> updateSettings;
    private readonly IAppLogger logger;

    public RaceSettingsCoordinator(
        Func<RaceSettings> getCurrentSettings,
        Action<RaceSettings> updateSettings,
        IAppLogger logger)
    {
        this.getCurrentSettings = getCurrentSettings;
        this.updateSettings = updateSettings;
        this.logger = logger;
    }

    public RaceSettings CreateSnapshot()
    {
        return AppSettingsCloner.CloneRaceSettings(getCurrentSettings());
    }

    public bool Update(string operationName, Action<RaceSettings> apply)
    {
        try
        {
            RaceSettings next = CreateSnapshot();
            apply(next);
            updateSettings(next);
            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, operationName + " failed.");
            return false;
        }
    }
}
