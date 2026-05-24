namespace TerrariaSplit;

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

        BossSplitDefinition definition = statuses[completedIndex].Definition;
        TimeSpan? referenceSplit = settings.TryGetReferenceSplit(definition, out TimeSpan configuredReferenceSplit)
            ? configuredReferenceSplit
            : null;
        TimeSpan? segmentTime = SplitRenderData.TryGetCompletedSegmentTime(statuses, completedIndex, out TimeSpan completedSegmentTime)
            ? completedSegmentTime
            : null;
        TimeSpan? personalBestSegment = SplitRenderData.TryGetPersonalBestSegment(settings, definition, out TimeSpan configuredPersonalBestSegment)
            ? configuredPersonalBestSegment
            : null;

        return SplitSoundSelector.GetPath(
            settings.Sounds,
            definition,
            splitTime,
            referenceSplit,
            segmentTime,
            personalBestSegment);
    }
}
