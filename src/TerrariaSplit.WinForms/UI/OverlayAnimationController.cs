using System.Drawing;

namespace TerrariaSplit.UI;

internal sealed class OverlayAnimationController
{
    private readonly Dictionary<int, SegmentBestDeltaHighlight> segmentBestDeltaHighlights = new();
    private ListPresentation? lastListPresentation;
    private SplitCompletionAnimationGeometry? timerRelativeGeometry;
    private float lastPresentedListOpacity = 1f;
    private bool lastRenderedOutgoingList;
    private bool completionLayoutApplied;

    public OverlayRenderContext? OutgoingSplitList { get; private set; }

    public float OutgoingListOpacity { get; private set; } = 1f;

    public SplitCompletionAnimation? SplitCompletionAnimation { get; private set; }

    public IReadOnlyDictionary<int, SegmentBestDeltaHighlight> SegmentBestDeltaHighlights => segmentBestDeltaHighlights;

    public void Clear()
    {
        ClearSplitCompletionAnimation();
        lastListPresentation = null;
        lastPresentedListOpacity = 1f;
        lastRenderedOutgoingList = false;
        segmentBestDeltaHighlights.Clear();
    }

    public void ClearSplitCompletionAnimation()
    {
        SplitCompletionAnimation = null;
        OutgoingSplitList = null;
        OutgoingListOpacity = 1f;
        timerRelativeGeometry = null;
        completionLayoutApplied = false;
    }

    public void UpdateAfterRender(OverlayRenderResult result)
    {
        lastPresentedListOpacity = result.ListOpacity;
        lastRenderedOutgoingList = result.OutgoingListRendered;
        if (SplitCompletionAnimation is not null && !result.SplitCompletionAnimationActive)
        {
            ClearSplitCompletionAnimation();
        }
    }

    public void StartSplitCompletionAnimation(
        AppSettings settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        int completedIndex,
        DateTime? nowUtc = null)
    {
        DateTime now = nowUtc ?? DateTime.UtcNow;
        bool replacing = SplitCompletionAnimation is not null;
        bool outgoing = replacing && lastRenderedOutgoingList;
        OutgoingListOpacity = replacing ? lastPresentedListOpacity : 1f;
        completionLayoutApplied = false;
        if (!outgoing)
        {
            OverlayRenderContext? previous = lastListPresentation?.Context;
            OutgoingSplitList = previous is null ? null : previous with
            {
                Settings = AppSettingsCloner.Clone(previous.Settings),
                SegmentBestDeltaHighlights = new Dictionary<int, SegmentBestDeltaHighlight>(previous.SegmentBestDeltaHighlights)
            };
        }

        if (!replacing)
        {
            timerRelativeGeometry = lastListPresentation?.TimerRelativeGeometry;
        }

        SplitCompletionAnimation = SplitCompletionAnimationFactory.Create(
            settings,
            statuses,
            completedIndex,
            now);
        if (SplitCompletionAnimation is null)
        {
            ClearSplitCompletionAnimation();
        }
    }

    public void RecordListPresentation(OverlayRenderContext context, SplitCompletionAnimationGeometry geometry,
        OverlayCompositeLayout layout)
    {
        Point relativeTimer = GetTimerOrigin(layout);
        OverlayRenderContext snapshot = context with
        {
            SplitCompletionAnimation = null,
            OutgoingSplitList = null,
            OutgoingListOpacity = 1f,
            CompletionGeometry = null,
            SegmentBestDeltaHighlights = new Dictionary<int, SegmentBestDeltaHighlight>(context.SegmentBestDeltaHighlights)
        };
        lastListPresentation = new ListPresentation(snapshot,
            geometry.Offset(new Point(-relativeTimer.X, -relativeTimer.Y)));
    }

    public SplitCompletionAnimationGeometry? ResolveGeometry(OverlayCompositeLayout layout) =>
        timerRelativeGeometry?.Offset(GetTimerOrigin(layout));

    public bool CanApplyCompletionLayout(AppSettings settings, DateTime nowUtc)
    {
        if (SplitCompletionAnimation is null)
        {
            return true;
        }

        TimeSpan elapsed = nowUtc - SplitCompletionAnimation.StartedAtUtc;
        TimeSpan duration = SplitCompletionAnimationRenderer.GetAnimationDuration(settings);
        return !SplitCompletionAnimationRenderer.IsOutgoingListPhase(elapsed, duration) &&
            (!completionLayoutApplied || SplitCompletionAnimationRenderer.IsIncomingListPhase(elapsed, duration));
    }

    public void MarkCompletionLayoutApplied() => completionLayoutApplied = true;

    public Rectangle? GetRequiredStatusBounds(AppSettings settings, DateTime nowUtc)
    {
        if (SplitCompletionAnimation is null || timerRelativeGeometry is not { } geometry)
        {
            return null;
        }

        TimeSpan elapsed = nowUtc - SplitCompletionAnimation.StartedAtUtc;
        if (SplitCompletionAnimationRenderer.IsIncomingListPhase(elapsed,
                SplitCompletionAnimationRenderer.GetAnimationDuration(settings)))
        {
            return null;
        }

        Rectangle bounds = geometry.Bounds;
        int bleed = OverlayRenderContext.ScaleInt(settings, 16);
        bounds.Inflate(bleed, bleed);
        return bounds;
    }

    private static Point GetTimerOrigin(OverlayCompositeLayout layout) => new(
        layout.TimerScreenBounds.Left - layout.StatusScreenBounds.Left,
        layout.TimerScreenBounds.Top - layout.StatusScreenBounds.Top);

    private sealed record ListPresentation(OverlayRenderContext Context, SplitCompletionAnimationGeometry TimerRelativeGeometry);

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
