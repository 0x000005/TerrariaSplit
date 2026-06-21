namespace TerrariaSplit.Storage;

internal static class AppSettingsPersistenceProjection
{
    public static AppSettings Create(AppSettings settings)
    {
        AppSettings projection = SettingsSerializer.Clone(settings);
        projection.ReferenceSplitSets = new List<ReferenceSplitSet>();
        projection.PersonalBestTimeSets = new List<ReferenceSplitSet>();
        projection.PersonalBestSegmentSets = new List<ReferenceSplitSet>();
        return projection;
    }
}
