namespace TerrariaSplit.Storage;

internal static class SplitSetLoader
{
    public static void LoadInto(AppSettings settings)
    {
        SplitTimeSetStore.EnsureDirectories();
        settings.Comparison.ReferenceSplitSets = SplitTimeSetStore.LoadReferenceSets();
        LoadPersonalBestTimeSets(settings);
        LoadPersonalBestSegmentSets(settings);
        SettingsNormalizer.Normalize(settings);
        settings.SyncPersonalBestTimesFromActiveSet();
        settings.SyncPersonalBestSegmentsFromActiveSet();
    }

    private static void LoadPersonalBestTimeSets(AppSettings settings)
    {
        List<ReferenceSplitSet> personalBestTimeSets = SplitTimeSetStore.LoadPersonalBestTimeSets();
        if (personalBestTimeSets.Count > 0)
        {
            settings.Comparison.PersonalBestTimeSets = personalBestTimeSets;
            return;
        }

        settings.Comparison.PersonalBestTimeSets = new List<ReferenceSplitSet>
        {
            CreateSet("Personal", SplitConditionDataRows.Build(settings).Select(row => row.Key))
        };
        SplitTimeSetStore.SavePersonalBestTimeSets(settings.Comparison.PersonalBestTimeSets);
    }

    private static void LoadPersonalBestSegmentSets(AppSettings settings)
    {
        List<ReferenceSplitSet> personalBestSegmentSets = SplitTimeSetStore.LoadPersonalBestSegmentSets();
        if (personalBestSegmentSets.Count > 0)
        {
            settings.Comparison.PersonalBestSegmentSets = personalBestSegmentSets;
            return;
        }

        settings.Comparison.PersonalBestSegmentSets = new List<ReferenceSplitSet>
        {
            CreateSet("Personal", SplitRouteGroups.Build(settings).Select(group => group.Key))
        };
        SplitTimeSetStore.SavePersonalBestSegmentSets(settings.Comparison.PersonalBestSegmentSets);
    }

    private static ReferenceSplitSet CreateSet(string name, IEnumerable<string> keys)
    {
        var set = new ReferenceSplitSet
        {
            Name = name,
            Splits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (string key in keys)
        {
            set.Splits[key] = string.Empty;
        }

        return set;
    }
}
