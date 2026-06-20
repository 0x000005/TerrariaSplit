namespace TerrariaSplit;

internal static class SplitCompletionAnimationFactory
{
    public static SplitCompletionAnimation? Create(
        AppSettings settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        int completedIndex,
        DateTime startedAtUtc)
    {
        if (completedIndex < 0 ||
            completedIndex >= statuses.Count ||
            statuses[completedIndex].Time is not TimeSpan splitTime)
        {
            return null;
        }

        if (!SplitRenderData.TryGetCompletedSegmentTime(settings, statuses, completedIndex, out TimeSpan segmentTime))
        {
            return null;
        }

        SplitStatusSnapshot status = statuses[completedIndex];
        SplitDefinition definition = status.Definition;
        SplitDefinition displayDefinition = SplitRenderData.GetDisplayDefinition(status);
        string groupKey = SplitRenderData.GetSplitCompletionGroupKey(settings, definition);
        string segmentBestDeltaHighlightStyle = SplitRenderData.GetSegmentBestDeltaHighlightStyle(settings, groupKey);

        return new SplitCompletionAnimation(
            displayDefinition,
            segmentTime,
            splitTime,
            SplitRenderData.GetReferenceSplitComparison(settings, definition, splitTime),
            SplitRenderData.GetPersonalBestSegmentComparison(settings, definition, segmentTime),
            SplitRenderData.IsSplitCompletionSplitComparisonEnabled(settings, groupKey),
            SplitRenderData.GetSplitCompletionOutlineStyle(settings.SplitCompletionOutlineSplitStyles, groupKey),
            SplitRenderData.IsSplitCompletionSegmentComparisonEnabled(settings, groupKey),
            SplitRenderData.GetSplitCompletionOutlineStyle(settings.SplitCompletionOutlineSegmentStyles, groupKey),
            segmentBestDeltaHighlightStyle,
            startedAtUtc);
    }
}
