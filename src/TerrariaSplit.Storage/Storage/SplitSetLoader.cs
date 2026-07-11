namespace TerrariaSplit.Storage;

internal sealed class SplitSetLoader
{
    private readonly SplitTimeSetRepository splitTimeSets;

    public SplitSetLoader(SplitTimeSetRepository splitTimeSets)
    {
        this.splitTimeSets = splitTimeSets;
    }

    public void LoadInto(AppSettings settings)
    {
        splitTimeSets.EnsureDirectories();
        settings.Comparison.ReferenceSplitSets = splitTimeSets.LoadReferenceSets();
        LoadPersonalBestTimeSets(settings);
        LoadPersonalBestSegmentSets(settings);
        SettingsNormalizer.Normalize(settings);
        PersonalBestSetService.SyncPersonalBestTimesFromActiveSet(settings);
        PersonalBestSetService.SyncPersonalBestSegmentsFromActiveSet(settings);
    }

    private void LoadPersonalBestTimeSets(AppSettings settings)
    {
        List<ReferenceSplitSet> personalBestTimeSets = splitTimeSets.LoadPersonalBestTimeSets();
        if (personalBestTimeSets.Count > 0)
        {
            settings.Comparison.PersonalBestTimeSets = personalBestTimeSets;
            return;
        }

        settings.Comparison.PersonalBestTimeSets = new List<ReferenceSplitSet>
        {
            CreateSet("Personal", SplitConditionDataRows.BuildKeys(settings))
        };
        splitTimeSets.SavePersonalBestTimeSets(settings.Comparison.PersonalBestTimeSets);
    }

    private void LoadPersonalBestSegmentSets(AppSettings settings)
    {
        List<ReferenceSplitSet> personalBestSegmentSets = splitTimeSets.LoadPersonalBestSegmentSets();
        if (personalBestSegmentSets.Count > 0)
        {
            settings.Comparison.PersonalBestSegmentSets = personalBestSegmentSets;
            return;
        }

        settings.Comparison.PersonalBestSegmentSets = new List<ReferenceSplitSet>
        {
            CreateSet("Personal", SplitRouteGroups.Build(settings).Select(group => group.Key))
        };
        splitTimeSets.SavePersonalBestSegmentSets(settings.Comparison.PersonalBestSegmentSets);
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
