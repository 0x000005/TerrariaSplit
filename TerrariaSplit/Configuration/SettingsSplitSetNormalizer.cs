namespace TerrariaSplit.Configuration;

internal static class SettingsSplitSetNormalizer
{
    public static void NormalizeReferenceSets(AppSettings settings)
    {
        if (settings.ReferenceSplitSets.Count == 0)
        {
            settings.ReferenceSplitSets.Add(AppSettings.CreateReferenceSet(
                "WR",
                keys: SplitConditionDataRows.Build(settings).Select(row => row.Key)));
        }

        HashSet<string> conditionRowKeys = SplitConditionDataRows.Build(settings)
            .Select(row => row.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (ReferenceSplitSet set in settings.ReferenceSplitSets)
        {
            set.Name = string.IsNullOrWhiteSpace(set.Name) ? "Reference" : set.Name.Trim();
            set.Splits ??= new Dictionary<string, string>();
            SettingsNormalizationHelpers.RemoveKeysExcept(set.Splits, conditionRowKeys);

            foreach (string key in conditionRowKeys)
            {
                set.Splits.TryAdd(key, string.Empty);
            }
        }

        if (string.IsNullOrWhiteSpace(settings.ActiveReferenceSplitSet) ||
            !settings.ReferenceSplitSets.Any(set => string.Equals(
                set.Name,
                settings.ActiveReferenceSplitSet,
                StringComparison.OrdinalIgnoreCase)))
        {
            settings.ActiveReferenceSplitSet = settings.ReferenceSplitSets[0].Name;
        }
    }

    public static void NormalizePersonalBestTimeSets(AppSettings settings)
    {
        NormalizePersonalSets(
            settings.PersonalBestTimeSets,
            "Personal",
            validKeys: SplitConditionDataRows.Build(settings).Select(row => row.Key),
            activeName: settings.ActivePersonalBestTimeSet,
            setActiveName: value => settings.ActivePersonalBestTimeSet = value);
    }

    public static void NormalizePersonalBestSegmentSets(AppSettings settings)
    {
        NormalizePersonalSets(
            settings.PersonalBestSegmentSets,
            "Personal",
            validKeys: SplitRouteGroups.Build(settings).Select(group => group.Key),
            activeName: settings.ActivePersonalBestSegmentSet,
            setActiveName: value => settings.ActivePersonalBestSegmentSet = value);
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
