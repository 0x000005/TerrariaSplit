using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed partial class MainForm : Form
{
    private void DrawStatusOverlay(Graphics graphics)
    {
        if (!overlayWindowsInitialized ||
            overlayWindowInitializationInProgress ||
            !TryGetLayout(out SplitLayout layout))
        {
            return;
        }

        var context = new OverlayRenderContext(
            settings,
            palette,
            snapshot,
            splitTracker.Statuses,
            splitTracker.CurrentIndex,
            runTimer.Phase,
            runTimer.Elapsed,
            layout,
            mouseClickThrough,
            splitCompletionAnimation,
            segmentBestDeltaHighlights,
            DateTime.UtcNow);
        OverlayRenderResult result = OverlayRenderer.RenderStatus(graphics, context, renderResources);
        if (splitCompletionAnimation is not null && !result.SplitCompletionAnimationActive)
        {
            splitCompletionAnimation = null;
        }
    }

    private bool TryGetSplitRowAt(Point point, out int rowIndex, out Rectangle rowRect)
    {
        rowIndex = -1;
        rowRect = Rectangle.Empty;
        IReadOnlyList<BossSplitStatus> statuses = splitTracker.Statuses;
        if (!TryGetLayout(out SplitLayout layout))
        {
            return false;
        }

        OverlayCompositeLayout? compositeLayout = overlayWindowsInitialized
            ? overlayBoundsController.CurrentLayout
            : null;
        Point compositePoint = compositeLayout?.MapStatusPointToComposite(point) ?? point;

        for (int i = 0; i < statuses.Count; i++)
        {
            Rectangle currentRowRect = layout.GetRowRect(i);
            if (currentRowRect.Contains(compositePoint))
            {
                rowIndex = i;
                rowRect = compositeLayout?.ToStatusLocal(currentRowRect) ?? currentRowRect;
                return true;
            }
        }

        return false;
    }

    private void TryOpenPracticeEdit(Point point)
    {
        if (!TryGetSplitRowAt(point, out int rowIndex, out Rectangle rowRect))
        {
            return;
        }

        BossSplitStatus status = splitTracker.Statuses[rowIndex];
        ColumnRects columns = SplitListRenderer.GetColumnRects(settings, rowRect);

        if (columns.Time is Rectangle timeRect && timeRect.Contains(point) && status.IsCompleted)
        {
            EditPracticeSplitTime(rowIndex, status);
        }
    }

    private void EditPracticeSplitTime(int rowIndex, BossSplitStatus status)
    {
        string currentText = status.Time is TimeSpan time ? TimeText.FormatRecord(time) : string.Empty;
        if (!PromptForTime(Localizer.Get("Edit split time", settings), currentText, allowEmpty: true, out string? editedText))
        {
            return;
        }

        TimeSpan? parsedTime = null;
        if (!string.IsNullOrWhiteSpace(editedText))
        {
            if (!TimeText.TryParse(editedText, out TimeSpan value))
            {
                return;
            }

            parsedTime = value;
        }

        splitTracker.SetPracticeTime(rowIndex, parsedTime);
        TrackSegmentBestDeltaHighlight(rowIndex);
        SyncBackgroundRuntimeState();
        PublishTimerOverlaySnapshot();
        Invalidate();
    }

    private void EditPracticeTotalTime()
    {
        string currentText = TimeText.FormatRecord(runTimer.Elapsed);
        if (!PromptForTime(Localizer.Get("Edit total time", settings), currentText, allowEmpty: false, out string? editedText) ||
            !TimeText.TryParse(editedText, out TimeSpan editedTime))
        {
            return;
        }

        runTimer.SetPracticeElapsed(editedTime);
        splitTracker.ClampCompletedTimes(editedTime);
        SyncBackgroundRuntimeState();
        PublishTimerOverlaySnapshot();
        Invalidate();
    }

    private bool PromptForTime(string title, string value, bool allowEmpty, out string editedText)
    {
        string localEditedText = value;
        bool accepted = RunWithSuspendedRuntimeOverlayPaint(() =>
            TimeEditDialog.TryShow(
                this,
                settings,
                title,
                value,
                allowEmpty,
                form => modalWindows.RegisterModalForm(form),
                out localEditedText));
        editedText = localEditedText;
        return accepted;
    }

    private bool TryGetLayout(out SplitLayout layout)
    {
        if (overlayWindowsInitialized)
        {
            layout = overlayBoundsController.CurrentLayout.Layout;
            return true;
        }

        if (!SplitLayoutCalculator.TryCreate(
                ClientRectangle,
                splitTracker.Statuses.Count,
                RowGap,
                value => OverlayRenderContext.ScaleInt(settings, value),
                out layout))
        {
            return false;
        }

        return true;
    }

    private int ScaleInt(int value)
    {
        return OverlayRenderContext.ScaleInt(settings, value);
    }

    private void StartSplitCompletionAnimation(int completedIndex)
    {
        splitCompletionAnimation = SplitCompletionAnimationFactory.Create(
            settings,
            splitTracker.Statuses,
            completedIndex,
            DateTime.UtcNow);
    }

    private void TrackSegmentBestDeltaHighlight(int completedIndex)
    {
        segmentBestDeltaHighlights.Remove(completedIndex);

        IReadOnlyList<BossSplitStatus> statuses = splitTracker.Statuses;
        if (completedIndex < 0 ||
            completedIndex >= statuses.Count ||
            !settings.ShowSegmentBestDeltaHighlight ||
            !SplitRenderData.TryGetCompletedSegmentTime(statuses, completedIndex, out TimeSpan segmentTime))
        {
            return;
        }

        BossSplitDefinition definition = statuses[completedIndex].Definition;
        if (!SplitRenderData.TryGetPersonalBestSegment(settings, definition, out TimeSpan personalBestSegment) ||
            segmentTime >= personalBestSegment)
        {
            return;
        }

        string style = SplitRenderData.GetSegmentBestDeltaHighlightStyle(
            settings,
            SplitRenderData.GetSplitCompletionGroupKey(definition));
        if (SegmentBestDeltaHighlightStyles.Normalize(style) == SegmentBestDeltaHighlightStyles.None)
        {
            return;
        }

        segmentBestDeltaHighlights[completedIndex] = new SegmentBestDeltaHighlight(style, DateTime.UtcNow);
    }

    private void PlaySplitSound(int completedIndex)
    {
        IReadOnlyList<BossSplitStatus> statuses = splitTracker.Statuses;
        if (completedIndex < 0 ||
            completedIndex >= statuses.Count ||
            statuses[completedIndex].Time is not TimeSpan splitTime)
        {
            return;
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

        string path = SplitSoundSelector.GetPath(
            settings.Sounds,
            definition,
            splitTime,
            referenceSplit,
            segmentTime,
            personalBestSegment);
        soundPlayer.Play(path);
    }
}
