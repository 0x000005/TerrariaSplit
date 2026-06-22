namespace TerrariaSplit.Application;

internal static class SoundFeedbackService
{
    public static string GetSplitSoundPath(
        AppSettings settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        int completedIndex)
    {
        if (completedIndex < 0 ||
            completedIndex >= statuses.Count ||
            statuses[completedIndex].Time is not TimeSpan splitTime)
        {
            return string.Empty;
        }

        SplitDefinition definition = statuses[completedIndex].Definition;
        bool isFinalGroupCompletion = IsFinalGroupCompletion(settings, definition.Id);
        TimeSpan? referenceSplit = ReferenceSplitSetService.TryGetReferenceSplit(settings, definition, out TimeSpan configuredReferenceSplit)
            ? configuredReferenceSplit
            : null;
        TimeSpan? segmentTime = SplitTimingComparisons.TryGetCompletedSegmentTime(settings, statuses, completedIndex, out TimeSpan completedSegmentTime)
            ? completedSegmentTime
            : null;
        TimeSpan? personalBestSegment = SplitTimingComparisons.TryGetPersonalBestSegment(settings, definition, out TimeSpan configuredPersonalBestSegment)
            ? configuredPersonalBestSegment
            : null;

        return SplitSoundSelector.GetPath(
            settings.Overlay.Sounds,
            isFinalGroupCompletion,
            splitTime,
            referenceSplit,
            segmentTime,
            personalBestSegment);
    }

    private static bool IsFinalGroupCompletion(AppSettings settings, string splitId)
    {
        List<RouteGroup> groups = SplitRouteGroups.Build(settings);
        return groups.Count > 0 &&
            groups[^1].Entries.Any(entry => string.Equals(entry.Id, splitId, StringComparison.OrdinalIgnoreCase));
    }
}
