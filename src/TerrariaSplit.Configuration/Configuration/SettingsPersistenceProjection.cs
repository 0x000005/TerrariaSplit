namespace TerrariaSplit.Configuration;

public static class SettingsPersistenceProjection
{
    public static void RemoveExternalSplitSets(AppSettings settings)
    {
        settings.Comparison.ReferenceSplitSets = new List<ReferenceSplitSet>();
        settings.Comparison.PersonalBestTimeSets = new List<ReferenceSplitSet>();
        settings.Comparison.PersonalBestSegmentSets = new List<ReferenceSplitSet>();
    }
}
