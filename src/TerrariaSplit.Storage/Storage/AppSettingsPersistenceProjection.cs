namespace TerrariaSplit.Storage;

internal static class AppSettingsPersistenceProjection
{
    public static AppSettings Create(AppSettings settings)
    {
        AppSettings projection = SettingsSerializer.Clone(settings);
        SettingsPersistenceProjection.RemoveExternalSplitSets(projection);
        return projection;
    }
}
