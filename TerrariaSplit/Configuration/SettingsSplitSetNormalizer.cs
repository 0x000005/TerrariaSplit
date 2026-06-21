namespace TerrariaSplit.Configuration;

internal static class SettingsSplitSetNormalizer
{
    public static void NormalizeReferenceSets(AppSettings settings)
    {
        if (settings.Comparison.ReferenceSplitSets.Count == 0)
        {
            settings.Comparison.ReferenceSplitSets.Add(AppSettings.CreateReferenceSet(
                "WR",
                keys: SplitConditionDataRows.Build(settings).Select(row => row.Key)));
        }

        HashSet<string> conditionRowKeys = SplitConditionDataRows.Build(settings)
            .Select(row => row.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (ReferenceSplitSet set in settings.Comparison.ReferenceSplitSets)
        {
            set.Name = string.IsNullOrWhiteSpace(set.Name) ? "Reference" : set.Name.Trim();
            set.Splits ??= new Dictionary<string, string>();
            SettingsNormalizationHelpers.RemoveKeysExcept(set.Splits, conditionRowKeys);

            foreach (string key in conditionRowKeys)
            {
                set.Splits.TryAdd(key, string.Empty);
            }
        }

        if (string.IsNullOrWhiteSpace(settings.Comparison.ActiveReferenceSplitSet) ||
            !settings.Comparison.ReferenceSplitSets.Any(set => string.Equals(
                set.Name,
                settings.Comparison.ActiveReferenceSplitSet,
                StringComparison.OrdinalIgnoreCase)))
        {
            settings.Comparison.ActiveReferenceSplitSet = settings.Comparison.ReferenceSplitSets[0].Name;
        }
    }

    public static void NormalizePersonalBestTimeSets(AppSettings settings)
    {
        NormalizePersonalSets(
            settings.Comparison.PersonalBestTimeSets,
            "Personal",
            validKeys: SplitConditionDataRows.Build(settings).Select(row => row.Key),
            activeName: settings.Comparison.ActivePersonalBestTimeSet,
            setActiveName: value => settings.Comparison.ActivePersonalBestTimeSet = value);
    }

    public static void NormalizePersonalBestSegmentSets(AppSettings settings)
    {
        NormalizePersonalSets(
            settings.Comparison.PersonalBestSegmentSets,
            "Personal",
            validKeys: SplitRouteGroups.Build(settings).Select(group => group.Key),
            activeName: settings.Comparison.ActivePersonalBestSegmentSet,
            setActiveName: value => settings.Comparison.ActivePersonalBestSegmentSet = value);
    }

    private static void NormalizePersonalSets(
        List<ReferenceSplitSet> sets,
        string fallbackName,
        IEnumerable<string> validKeys,
        string activeName,
        Action<string> setActiveName)
    {
        HashSet<string> validKeySet = validKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (sets.Count == 0)
        {
            sets.Add(CreateEmptyPersonalSet(fallbackName, validKeySet));
        }

        foreach (ReferenceSplitSet set in sets)
        {
            set.Name = string.IsNullOrWhiteSpace(set.Name) ? fallbackName : set.Name.Trim();
            set.Splits ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            SettingsNormalizationHelpers.RemoveKeysExcept(set.Splits, validKeySet);
            foreach (string key in validKeySet)
            {
                set.Splits.TryAdd(key, string.Empty);
            }
        }

        if (string.IsNullOrWhiteSpace(activeName) ||
            !sets.Any(set => string.Equals(set.Name, activeName, StringComparison.OrdinalIgnoreCase)))
        {
            setActiveName(sets[0].Name);
        }
    }

    private static ReferenceSplitSet CreateEmptyPersonalSet(string name, IEnumerable<string> keys)
    {
        var set = new ReferenceSplitSet
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Personal" : name.Trim(),
            Splits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (string key in keys)
        {
            set.Splits[key] = string.Empty;
        }

        return set;
    }
}
