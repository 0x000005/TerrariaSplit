namespace TerrariaSplit.Configuration;

internal static class PersonalBestSetService
{
    public static string GetPersonalBestTimeText(AppSettings settings, string name)
    {
        return settings.Comparison.PersonalBestTimes.TryGetValue(name, out string? value) ? value : string.Empty;
    }

    public static string GetPersonalBestSegmentText(AppSettings settings, string name)
    {
        return settings.Comparison.PersonalBestSegmentTimes.TryGetValue(name, out string? value) ? value : string.Empty;
    }

    public static void SetPersonalBestTimeText(AppSettings settings, string name, string value)
    {
        settings.Comparison.PersonalBestTimes[name] = value;
    }

    public static void SetPersonalBestSegmentText(AppSettings settings, string name, string value)
    {
        settings.Comparison.PersonalBestSegmentTimes[name] = value;
    }

    public static ReferenceSplitSet GetActivePersonalBestTimeSet(AppSettings settings)
    {
        ReferenceSplitSet set = GetActivePersonalSet(
            settings.Comparison.PersonalBestTimeSets,
            settings.Comparison.ActivePersonalBestTimeSet,
            "Personal",
            settings.Comparison.PersonalBestTimes,
            out string activeName);
        settings.Comparison.ActivePersonalBestTimeSet = activeName;
        return set;
    }

    public static ReferenceSplitSet GetActivePersonalBestSegmentSet(AppSettings settings)
    {
        ReferenceSplitSet set = GetActivePersonalSet(
            settings.Comparison.PersonalBestSegmentSets,
            settings.Comparison.ActivePersonalBestSegmentSet,
            "Personal",
            settings.Comparison.PersonalBestSegmentTimes,
            out string activeName);
        settings.Comparison.ActivePersonalBestSegmentSet = activeName;
        return set;
    }

    public static void SyncPersonalBestTimesFromActiveSet(AppSettings settings)
    {
        settings.Comparison.PersonalBestTimes = new Dictionary<string, string>(
            GetActivePersonalBestTimeSet(settings).Splits,
            StringComparer.OrdinalIgnoreCase);
    }

    public static void SyncPersonalBestSegmentsFromActiveSet(AppSettings settings)
    {
        settings.Comparison.PersonalBestSegmentTimes = new Dictionary<string, string>(
            GetActivePersonalBestSegmentSet(settings).Splits,
            StringComparer.OrdinalIgnoreCase);
    }

    public static void SyncActivePersonalBestTimeSetFromDictionary(AppSettings settings)
    {
        GetActivePersonalBestTimeSet(settings).Splits = new Dictionary<string, string>(
            settings.Comparison.PersonalBestTimes,
            StringComparer.OrdinalIgnoreCase);
    }

    public static void SyncActivePersonalBestSegmentSetFromDictionary(AppSettings settings)
    {
        GetActivePersonalBestSegmentSet(settings).Splits = new Dictionary<string, string>(
            settings.Comparison.PersonalBestSegmentTimes,
            StringComparer.OrdinalIgnoreCase);
    }

    private static ReferenceSplitSet GetActivePersonalSet(
        List<ReferenceSplitSet> sets,
        string activeName,
        string fallbackName,
        Dictionary<string, string> fallbackValues,
        out string normalizedActiveName)
    {
        ReferenceSplitSet? activeSet = sets.FirstOrDefault(
            set => string.Equals(set.Name, activeName, StringComparison.OrdinalIgnoreCase));
        if (activeSet is not null)
        {
            normalizedActiveName = activeSet.Name;
            return activeSet;
        }

        if (sets.Count == 0)
        {
            sets.Add(new ReferenceSplitSet
            {
                Name = fallbackName,
                Splits = new Dictionary<string, string>(fallbackValues, StringComparer.OrdinalIgnoreCase)
            });
        }

        normalizedActiveName = sets[0].Name;
        return sets[0];
    }
}
