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
        IReadOnlyList<SplitConditionDataRow> conditionRows = SplitConditionDataRows.Build(settings);
        var rows = new List<StatisticsTableRow>();
        TimeSpan previousReferenceSplit = TimeSpan.Zero;
        TimeSpan previousPersonalSplit = TimeSpan.Zero;
        Dictionary<string, RouteGroup> mainGroupsByEntryId = SplitRouteGroups.Build(settings)
            .SelectMany(group => group.Entries.Select(entry => (entry.Id, group)))
            .ToDictionary(item => item.Id, item => item.group, StringComparer.OrdinalIgnoreCase);

        foreach (SplitRouteEntry entry in settings.SplitRoute)
        {
            if (!entry.Enabled || string.IsNullOrWhiteSpace(entry.Id))
            {
                continue;
            }

            if (entry.IsAttached)
            {
                AddAttachedRows(
                    rows,
                    conditionRows,
                    referenceTimeSet,
                    personalSplits,
                    settings,
                    entry);
                continue;
            }

            if (!mainGroupsByEntryId.TryGetValue(entry.Id, out RouteGroup? group))
            {
                continue;
            }

            string referenceSegmentText = FormatSegment(
                settings,
                referenceTimeSet.Splits,
                group,
                ref previousReferenceSplit);
            string personalSegmentText = FormatSegment(
                settings,
                personalSplits,
                group,
                ref previousPersonalSplit);

            List<SplitConditionDataRow> groupConditionRows = group.Entries
                .SelectMany(entry => conditionRows.Where(row =>
                    string.Equals(row.SplitId, entry.Id, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            for (int i = 0; i < groupConditionRows.Count; i++)
            {
                SplitConditionDataRow conditionRow = groupConditionRows[i];
                rows.Add(new StatisticsTableRow(
                    conditionRow,
                    GetValue(referenceTimeSet.Splits, conditionRow.Key),
                    GetConditionOrSplitValue(personalSplits, conditionRow),
                    referenceSegmentText,
                    personalSegmentText,
                    GetValue(settings.PersonalBestTimes, conditionRow.Key),
                    GetValue(settings.PersonalBestSegmentTimes, group.Key),
                    groupConditionRows.Count,
                    i));
            }
        }

        return rows;
    }

    private static void AddAttachedRows(
        List<StatisticsTableRow> rows,
        IReadOnlyList<SplitConditionDataRow> conditionRows,
        ReferenceSplitSet referenceTimeSet,
        Dictionary<string, string> personalSplits,
        AppSettings settings,
        SplitRouteEntry entry)
    {
        List<SplitConditionDataRow> attachedRows = conditionRows
            .Where(row => string.Equals(row.SplitId, entry.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        for (int i = 0; i < attachedRows.Count; i++)
        {
            SplitConditionDataRow conditionRow = attachedRows[i];
            rows.Add(new StatisticsTableRow(
                conditionRow,
                GetValue(referenceTimeSet.Splits, conditionRow.Key),
                GetConditionOrSplitValue(personalSplits, conditionRow),
                "--",
                "--",
                GetValue(settings.PersonalBestTimes, conditionRow.Key),
                "--",
                attachedRows.Count,
                i));
        }
    }

    private static string FormatSegment(
        AppSettings settings,
        Dictionary<string, string> values,
        RouteGroup group,
        ref TimeSpan previousSplit)
    {
        if (!TryGetGroupTime(settings, values, group, out TimeSpan split))
        {
            return "--";
        }

        TimeSpan segment = split - previousSplit;
        previousSplit = split;
        return segment >= TimeSpan.Zero
            ? TimeText.FormatRecord(segment)
            : "--";
    }

    private static bool TryGetGroupTime(
        AppSettings settings,
        Dictionary<string, string> values,
        RouteGroup group,
        out TimeSpan split)
    {
        split = TimeSpan.Zero;
        bool found = false;
        foreach (SplitRouteEntry entry in group.Entries)
        {
            if ((SplitConditionDataRows.TryGetSplitTime(settings, values, entry.Id, out TimeSpan candidate) ||
                    TryGetTime(values, entry.Id, out candidate)) &&
                (!found || candidate > split))
            {
                split = candidate;
                found = true;
            }
        }

        return found;
    }

    private static bool TryGetTime(Dictionary<string, string> values, string splitId, out TimeSpan split)
    {
        split = TimeSpan.Zero;
        return values.TryGetValue(splitId, out string? value) &&
            TimeText.TryParse(value, out split);
    }

    private static string GetValue(Dictionary<string, string> values, string id)
    {
        return values.TryGetValue(id, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : "--";
    }

    private static string GetConditionOrSplitValue(
        Dictionary<string, string> values,
        SplitConditionDataRow conditionRow)
    {
        string value = GetValue(values, conditionRow.Key);
        return value != "--"
            ? value
            : GetValue(values, conditionRow.SplitId);
    }
}
