namespace TerrariaSplit;

internal static class StatisticsTableBuilder
{
    public static List<StatisticsTableRow> Build(AppSettings settings, RunStats stats)
    {
        return Build(
            settings,
            settings.GetActiveReferenceSet(),
            stats.LastRunSplits);
    }

    public static List<StatisticsTableRow> Build(
        AppSettings settings,
        ReferenceSplitSet referenceTimeSet,
        Dictionary<string, string> personalSplits)
    {
        IReadOnlyDictionary<string, BossUnitDefinition> units = BossSplitDefinitions.Units.ToDictionary(
            unit => unit.Id,
            StringComparer.OrdinalIgnoreCase);
        var route = settings.Route
            .Select((entry, index) => new IndexedRouteEntry(entry, index))
            .Where(entry => units.ContainsKey(entry.Entry.BossId))
            .OrderBy(entry => entry.Entry.Segment)
            .ThenBy(entry => entry.Index)
            .ToList();

        var rows = new List<StatisticsTableRow>();
        TimeSpan previousReferenceGroupSplit = TimeSpan.Zero;
        TimeSpan previousPersonalGroupSplit = TimeSpan.Zero;
        foreach (IGrouping<int, IndexedRouteEntry> routeGroup in route.GroupBy(entry => GetSegmentGroup(entry.Entry)))
        {
            List<IndexedRouteEntry> entries = routeGroup.ToList();
            string bestSegmentText = GetValue(settings.PersonalBestSegmentTimes, GetGroupKey(entries));
            string referenceSegmentText = FormatGroupSegment(entries, referenceTimeSet.Splits, previousReferenceGroupSplit);
            string personalSegmentText = FormatGroupSegment(entries, personalSplits, previousPersonalGroupSplit);

            if (TryGetGroupMaxSplit(entries, referenceTimeSet.Splits, enabledOnly: true, out TimeSpan referenceGroupMaxSplit))
            {
                previousReferenceGroupSplit = referenceGroupMaxSplit;
            }

            if (TryGetGroupMaxSplit(entries, personalSplits, enabledOnly: true, out TimeSpan personalGroupMaxSplit))
            {
                previousPersonalGroupSplit = personalGroupMaxSplit;
            }

            int visibleRowsInGroup = entries.Count(entry => entry.Entry.Enabled);
            for (int i = 0; i < entries.Count; i++)
            {
                BossRouteEntry entry = entries[i].Entry;
                if (!entry.Enabled)
                {
                    continue;
                }

                BossUnitDefinition unit = units[entry.BossId];
                rows.Add(new StatisticsTableRow(
                    unit,
                    FormatReference(referenceTimeSet, unit),
                    GetValue(personalSplits, unit.Id),
                    referenceSegmentText,
                    personalSegmentText,
                    GetValue(settings.PersonalBestTimes, unit.Id),
                    bestSegmentText,
                    visibleRowsInGroup,
                    entries.Take(i).Count(entry => entry.Entry.Enabled)));
            }
        }

        return rows;
    }

    private static string FormatGroupSegment(
        IReadOnlyList<IndexedRouteEntry> group,
        Dictionary<string, string> values,
        TimeSpan previousGroupSplit)
    {
        if (TryGetGroupMaxSplit(group, values, enabledOnly: true, out TimeSpan groupMaxSplit))
        {
            return TimeText.FormatRecord(groupMaxSplit - previousGroupSplit);
        }

        return "--";
    }

    private static bool TryGetGroupMaxSplit(
        IReadOnlyList<IndexedRouteEntry> group,
        Dictionary<string, string> values,
        bool enabledOnly,
        out TimeSpan split)
    {
        split = TimeSpan.Zero;
        bool found = false;
        foreach (IndexedRouteEntry entry in group)
        {
            if (enabledOnly && !entry.Entry.Enabled)
            {
                continue;
            }

            if (values.TryGetValue(entry.Entry.BossId, out string? value) &&
                TimeText.TryParse(value, out TimeSpan candidate) &&
                (!found || candidate > split))
            {
                split = candidate;
                found = true;
            }
        }

        return found;
    }

    private static string FormatReference(ReferenceSplitSet referenceTimeSet, BossUnitDefinition unit)
    {
        string referenceText = referenceTimeSet.Splits.TryGetValue(unit.Id, out string? value)
            ? value
            : string.Empty;
        return TimeText.TryParse(referenceText, out TimeSpan reference)
            ? TimeText.FormatRecord(reference)
            : "--";
    }

    private static string GetValue(Dictionary<string, string> values, string id)
    {
        return values.TryGetValue(id, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : "--";
    }

    private static int GetSegmentGroup(BossRouteEntry entry)
    {
        return Math.Max(1, (int)Math.Truncate(entry.Segment));
    }

    private static string GetGroupKey(IReadOnlyList<IndexedRouteEntry> entries)
    {
        return string.Join("+", entries
            .Where(entry => entry.Entry.Enabled)
            .Select(entry => entry.Entry.BossId));
    }

    private sealed record IndexedRouteEntry(BossRouteEntry Entry, int Index);
}
