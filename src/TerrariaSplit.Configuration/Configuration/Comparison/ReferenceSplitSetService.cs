namespace TerrariaSplit.Configuration;

public static class ReferenceSplitSetService
{
    public const string PersonalBestReferenceSetName = "PB";

    public static bool TryGetReferenceSplit(AppSettings settings, SplitDefinition definition, out TimeSpan split)
    {
        return SplitConditionDataRows.TryGetSplitTime(settings, GetActiveReferenceSet(settings).Splits, definition, out split);
    }

    public static string GetReferenceText(AppSettings settings, string name)
    {
        return GetActiveReferenceSet(settings).Splits.TryGetValue(name, out string? value) ? value : string.Empty;
    }

    public static void SetReferenceText(AppSettings settings, string name, string value)
    {
        if (settings.Comparison.UsePersonalBestAsReferenceTime)
        {
            return;
        }

        GetActiveReferenceSet(settings).Splits[name] = value;
    }

    public static ReferenceSplitSet GetActiveReferenceSet(AppSettings settings)
    {
        if (settings.Comparison.UsePersonalBestAsReferenceTime)
        {
            return CreatePersonalBestReferenceSet(settings);
        }

        ReferenceSplitSet? activeSet = settings.Comparison.ReferenceSplitSets.FirstOrDefault(
            set => string.Equals(set.Name, settings.Comparison.ActiveReferenceSplitSet, StringComparison.OrdinalIgnoreCase));
        if (activeSet is not null)
        {
            return activeSet;
        }

        if (settings.Comparison.ReferenceSplitSets.Count == 0)
        {
            settings.Comparison.ReferenceSplitSets.Add(CreateReferenceSet(
                "WR",
                keys: SplitConditionDataRows.BuildKeys(settings)));
        }

        settings.Comparison.ActiveReferenceSplitSet = settings.Comparison.ReferenceSplitSets[0].Name;
        return settings.Comparison.ReferenceSplitSets[0];
    }

    public static ReferenceSplitSet CreatePersonalBestReferenceSet(AppSettings settings)
    {
        return CreateReferenceSet(
            PersonalBestReferenceSetName,
            settings.Comparison.PersonalBestTimes,
            SplitConditionDataRows.BuildKeys(settings));
    }

    public static ReferenceSplitSet CreateReferenceSet(
        string name,
        Dictionary<string, string>? values = null,
        IEnumerable<string>? keys = null)
    {
        var set = new ReferenceSplitSet
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Reference" : name.Trim()
        };

        IEnumerable<string> splitKeys = keys ?? SplitConditionDataRows.BuildKeys(SplitCatalog.CreateDefaultRoute());
        foreach (string key in splitKeys.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string value = values is not null && values.TryGetValue(key, out string? existingValue)
                ? existingValue
                : string.Empty;
            set.Splits[key] = value;
        }

        return set;
    }
}
