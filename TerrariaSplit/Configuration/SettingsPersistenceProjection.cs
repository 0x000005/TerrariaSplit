namespace TerrariaSplit.Configuration;

internal static class SettingsPersistenceProjection
{
    public static void RemoveExternalSplitSets(AppSettings settings)
    {
        settings.ReferenceSplitSets = new List<ReferenceSplitSet>();
        settings.PersonalBestTimeSets = new List<ReferenceSplitSet>();
        settings.PersonalBestSegmentSets = new List<ReferenceSplitSet>();
    }
}
