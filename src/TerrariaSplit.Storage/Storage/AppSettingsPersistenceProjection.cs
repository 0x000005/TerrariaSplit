namespace TerrariaSplit.Storage;

internal static class AppSettingsPersistenceProjection
{
    public static AppSettings Create(AppSettings settings)
    {
        AppSettings projection = AppSettingsCloner.Clone(settings);
        SettingsPersistenceProjection.RemoveExternalSplitSets(projection);
        return projection;
    }
}
