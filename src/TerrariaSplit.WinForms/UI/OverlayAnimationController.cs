namespace TerrariaSplit.UI;

internal sealed class OverlayAnimationController
{
    private readonly Dictionary<int, SegmentBestDeltaHighlight> segmentBestDeltaHighlights = new();

    public SplitCompletionAnimation? SplitCompletionAnimation { get; private set; }

    public IReadOnlyDictionary<int, SegmentBestDeltaHighlight> SegmentBestDeltaHighlights => segmentBestDeltaHighlights;

    public void Clear()
    {
        SplitCompletionAnimation = null;
        segmentBestDeltaHighlights.Clear();
    }

    public void ClearSplitCompletionAnimation()
    {
        SplitCompletionAnimation = null;
    }

    public void UpdateAfterRender(OverlayRenderResult result)
    {
        if (SplitCompletionAnimation is not null && !result.SplitCompletionAnimationActive)
        {
            SplitCompletionAnimation = null;
        }
    }

    public void StartSplitCompletionAnimation(
        AppSettings settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        int completedIndex)
    {
        SplitCompletionAnimation = SplitCompletionAnimationFactory.Create(
            settings,
            statuses,
            completedIndex,
            DateTime.UtcNow);
    }

    public void TrackSegmentBestDeltaHighlight(
        AppSettings settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        int completedIndex)
    {
        segmentBestDeltaHighlights.Remove(completedIndex);

        if (completedIndex < 0 ||
            completedIndex >= statuses.Count ||
            !settings.Overlay.ShowSegmentBestDeltaHighlight ||
            !SplitComparisonService.TryGetCompletedSegmentTime(settings, statuses, completedIndex, out TimeSpan segmentTime))
        {
            return;
        }

        SplitDefinition definition = statuses[completedIndex].Definition;
        if (!SplitComparisonService.TryGetPersonalBestSegment(settings, definition, out TimeSpan personalBestSegment) ||
            segmentTime >= personalBestSegment)
        {
            return;
        }

        string style = SplitRenderData.GetSegmentBestDeltaHighlightStyle(
            settings,
            SplitComparisonService.GetSplitCompletionGroupKey(settings, definition));
        if (SegmentBestDeltaHighlightStyles.Normalize(style) == SegmentBestDeltaHighlightStyles.None)
        {
            return;
        }

        segmentBestDeltaHighlights[completedIndex] = new SegmentBestDeltaHighlight(style, DateTime.UtcNow);
    }
}
