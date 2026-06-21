namespace TerrariaSplit.Application;

internal static class SplitTimingComparisons
{
    public static bool TryGetPersonalBestSegment(
        AppSettings settings,
        SplitDefinition definition,
        out TimeSpan segment)
    {
        segment = TimeSpan.Zero;
        string groupKey = GetSplitCompletionGroupKey(settings, definition);
        if (settings.Comparison.PersonalBestSegmentTimes.TryGetValue(groupKey, out string? value) &&
            TimeText.TryParse(value, out TimeSpan parsed))
        {
            segment = parsed;
            return true;
        }

        return false;
    }

    public static bool TryGetCompletedSegmentTime(
        AppSettings settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        int completedIndex,
        out TimeSpan segmentTime)
    {
        segmentTime = TimeSpan.Zero;
        if (completedIndex < 0 ||
            completedIndex >= statuses.Count ||
            statuses[completedIndex].Time is not TimeSpan)
        {
            return false;
        }

        Dictionary<string, SplitStatusSnapshot> statusById = statuses
            .ToDictionary(status => status.Definition.Id, StringComparer.OrdinalIgnoreCase);
        List<RouteGroup> groups = SplitRouteGroups.Build(settings);
        int groupIndex = groups.FindIndex(group =>
            group.Entries.Any(entry => string.Equals(
                entry.Id,
                statuses[completedIndex].Definition.Id,
                StringComparison.OrdinalIgnoreCase)));
        if (groupIndex < 0)
        {
            return TryGetAdjacentSegmentTime(statuses, completedIndex, out segmentTime);
        }

        if (!TryGetGroupSplitTime(groups[groupIndex], statusById, out TimeSpan groupSplitTime))
        {
            return false;
        }

        TimeSpan previousGroupSplitTime = TimeSpan.Zero;
        for (int i = groupIndex - 1; i >= 0; i--)
        {
            if (TryGetGroupSplitTime(groups[i], statusById, out TimeSpan previousTime))
            {
                previousGroupSplitTime = previousTime;
                break;
            }
        }

        segmentTime = groupSplitTime - previousGroupSplitTime;
        if (segmentTime < TimeSpan.Zero)
        {
            segmentTime = TimeSpan.Zero;
        }

        return true;
    }

    public static string GetSplitCompletionGroupKey(AppSettings settings, SplitDefinition definition)
    {
        return SplitRouteGroups.TryGetGroupKey(settings, definition.Id, out string groupKey)
            ? groupKey
            : definition.Id;
    }

    private static bool TryGetAdjacentSegmentTime(
        IReadOnlyList<SplitStatusSnapshot> statuses,
        int completedIndex,
        out TimeSpan segmentTime)
    {
        segmentTime = TimeSpan.Zero;
        if (statuses[completedIndex].Time is not TimeSpan splitTime)
        {
            return false;
        }

        TimeSpan previousSplitTime = TimeSpan.Zero;
        for (int i = completedIndex - 1; i >= 0; i--)
        {
            if (statuses[i].Time is TimeSpan previousTime)
            {
                previousSplitTime = previousTime;
                break;
            }
        }

        segmentTime = splitTime - previousSplitTime;
        if (segmentTime < TimeSpan.Zero)
        {
            segmentTime = TimeSpan.Zero;
        }

        return true;
    }

    private static bool TryGetGroupSplitTime(
        RouteGroup group,
        Dictionary<string, SplitStatusSnapshot> statusById,
        out TimeSpan splitTime)
    {
        splitTime = TimeSpan.Zero;
        bool found = false;
        foreach (SplitRouteEntry entry in group.Entries)
        {
            if (statusById.TryGetValue(entry.Id, out SplitStatusSnapshot? status) &&
                status.Time is TimeSpan candidate &&
                (!found || candidate > splitTime))
            {
                splitTime = candidate;
                found = true;
            }
        }

        return found;
    }
}
