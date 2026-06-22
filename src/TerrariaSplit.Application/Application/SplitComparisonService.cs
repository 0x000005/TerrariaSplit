namespace TerrariaSplit.Application;

internal static class SplitComparisonService
{
    public static SplitComparison GetSplitComparison(
        AppSettings settings,
        SplitTimerPhase timerPhase,
        TimeSpan timerElapsed,
        SplitStatusSnapshot status,
        bool isCurrent)
    {
        if (!ReferenceSplitSetService.TryGetReferenceSplit(settings, status.Definition, out TimeSpan referenceTime))
        {
            return SplitComparison.Empty;
        }

        if (status.Time is TimeSpan splitTime)
        {
            return new SplitComparison(splitTime - referenceTime, ShowDelta: true);
        }

        if (!isCurrent || timerPhase == SplitTimerPhase.NotStarted)
        {
            return SplitComparison.Empty;
        }

        TimeSpan runningDelta = timerElapsed - referenceTime;
        TimeSpan visibleDeltaDistance = TimeSpan.FromSeconds(settings.Overlay.EarlyDeltaTimeSeconds);
        bool showRunningDelta = settings.Overlay.ShowEarlyDeltaTime && runningDelta >= -visibleDeltaDistance;
        return new SplitComparison(runningDelta, showRunningDelta);
    }

    public static SplitComparison GetReferenceSplitComparison(
        AppSettings settings,
        SplitDefinition definition,
        TimeSpan splitTime)
    {
        if (!ReferenceSplitSetService.TryGetReferenceSplit(settings, definition, out TimeSpan referenceSplit))
        {
            return SplitComparison.Empty;
        }

        return new SplitComparison(splitTime - referenceSplit, ShowDelta: true);
    }

    public static SplitComparison GetPersonalBestSegmentComparison(
        AppSettings settings,
        SplitDefinition definition,
        TimeSpan segmentTime)
    {
        if (!TryGetPersonalBestSegment(settings, definition, out TimeSpan personalBestSegment))
        {
            return SplitComparison.Empty;
        }

        return new SplitComparison(segmentTime - personalBestSegment, ShowDelta: true);
    }

    public static bool TryGetPersonalBestSegment(
        AppSettings settings,
        SplitDefinition definition,
        out TimeSpan segment)
    {
        return SplitTimingComparisons.TryGetPersonalBestSegment(settings, definition, out segment);
    }

    public static bool TryGetCompletedSegmentTime(
        AppSettings settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        int completedIndex,
        out TimeSpan segmentTime)
    {
        return SplitTimingComparisons.TryGetCompletedSegmentTime(settings, statuses, completedIndex, out segmentTime);
    }

    public static string GetSplitCompletionGroupKey(AppSettings settings, SplitDefinition definition)
    {
        return SplitTimingComparisons.GetSplitCompletionGroupKey(settings, definition);
    }
}
