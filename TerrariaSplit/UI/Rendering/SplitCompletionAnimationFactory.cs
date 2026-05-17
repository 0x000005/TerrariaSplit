namespace TerrariaSplit;

internal static class SplitCompletionAnimationFactory
{
    public static SplitCompletionAnimation? Create(
        AppSettings settings,
        IReadOnlyList<BossSplitStatus> statuses,
        int completedIndex,
        DateTime startedAtUtc)
    {
        if (completedIndex < 0 ||
            completedIndex >= statuses.Count ||
            statuses[completedIndex].Time is not TimeSpan splitTime)
        {
            return null;
        }

        if (!SplitRenderData.TryGetCompletedSegmentTime(statuses, completedIndex, out TimeSpan segmentTime))
        {
            return null;
        }

        BossSplitDefinition definition = statuses[completedIndex].Definition;
        string groupKey = SplitRenderData.GetSplitCompletionGroupKey(definition);
        string segmentBestDeltaHighlightStyle = SplitRenderData.GetSegmentBestDeltaHighlightStyle(settings, groupKey);

        return new SplitCompletionAnimation(
            definition,
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
