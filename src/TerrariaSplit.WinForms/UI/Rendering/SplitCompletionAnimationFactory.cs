namespace TerrariaSplit.UI.Rendering;

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

        if (!SplitComparisonService.TryGetCompletedSegmentTime(settings, statuses, completedIndex, out TimeSpan segmentTime))
        {
            return null;
        }

        SplitStatusSnapshot status = statuses[completedIndex];
        SplitDefinition definition = status.Definition;
        SplitDefinition displayDefinition = SplitRenderData.GetDisplayDefinition(status);
        string groupKey = SplitComparisonService.GetSplitCompletionGroupKey(settings, definition);
        string segmentBestDeltaHighlightStyle = SplitRenderData.GetSegmentBestDeltaHighlightStyle(settings, groupKey);

        return new SplitCompletionAnimation(
            displayDefinition,
            segmentTime,
            splitTime,
            SplitComparisonService.GetReferenceSplitComparison(settings, definition, splitTime),
            SplitComparisonService.GetPersonalBestSegmentComparison(settings, definition, segmentTime),
            SplitRenderData.IsSplitCompletionSplitComparisonEnabled(settings, groupKey),
            SplitRenderData.GetSplitCompletionOutlineStyle(settings.Overlay.SplitCompletionOutlineSplitStyles, groupKey),
            SplitRenderData.IsSplitCompletionSegmentComparisonEnabled(settings, groupKey),
            SplitRenderData.GetSplitCompletionOutlineStyle(settings.Overlay.SplitCompletionOutlineSegmentStyles, groupKey),
            segmentBestDeltaHighlightStyle,
            startedAtUtc);
    }
}
