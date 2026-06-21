using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

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

        TimeSpan elapsed = timerElapsed;
        var context = new OverlayRenderContext(
            settings,
            palette,
            snapshot,
            splitStatuses,
            currentSplitIndex,
            timerPhase,
            elapsed,
            layout,
            Math.Max(GetCurrentLayoutRowCount(), SplitCompletionAnimationRenderer.ReservedRowCount),
            mouseClickThrough,
            overlayAnimations.SplitCompletionAnimation,
            overlayAnimations.SegmentBestDeltaHighlights,
            DateTime.UtcNow);
        OverlayRenderResult result = OverlayRenderer.RenderStatus(
            graphics,
            context,
            renderResources,
            statusOverlayPartialClipBounds);
        overlayAnimations.UpdateAfterRender(result);

        lastStatusOverlayDynamicKey = ComputeStatusOverlayDynamicKey(elapsed);
        if (statusOverlayPartialClipBounds is null)
        {
            statusOverlayContentDirty = false;
        }
    }

    private StatusOverlayDynamicKey ComputeStatusOverlayDynamicKey(TimeSpan elapsed)
    {
        int index = currentSplitIndex;
        if (index < 0 ||
            index >= splitStatuses.Count ||
            timerPhase == SplitTimerPhase.NotStarted)
        {
            return new StatusOverlayDynamicKey(index, string.Empty, 0);
        }

        SplitStatusSnapshot status = splitStatuses[index];
        if (!SplitListRenderer.GetDeltaColumnSettings(settings, status.Definition.IsAttached).Show)
        {
            return new StatusOverlayDynamicKey(index, string.Empty, 0);
        }

        SplitComparison comparison = SplitRenderData.GetSplitComparison(
            settings,
            timerPhase,
            elapsed,
            status,
            isCurrent: true);
        string deltaText = SplitRenderData.FormatSplitDelta(settings, comparison);
        if (deltaText.Length == 0)
        {
            return new StatusOverlayDynamicKey(index, string.Empty, 0);
        }

        bool enableDeltaGradient = status.Time is TimeSpan
            ? settings.Overlay.EnableDeltaGradientColor
            : settings.Overlay.EnableCurrentDeltaGradientColor;
        Color deltaColor = OverlayColorMath.GetDeltaComparisonColor(
            settings,
            comparison,
            palette,
            enableDeltaGradient);
        return new StatusOverlayDynamicKey(index, deltaText, deltaColor.ToArgb());
    }

    private bool StatusOverlayHighlightsActive =>
        settings.Overlay.ShowSegmentBestDeltaHighlight &&
        overlayAnimations.SegmentBestDeltaHighlights.Count > 0;

    private Rectangle? ComputeStatusOverlayDynamicRegion()
    {
        if (!overlayWindowsInitialized || !TryGetLayout(out SplitLayout layout))
        {
            return null;
        }

        OverlayCompositeLayout compositeLayout = overlayBoundsController.CurrentLayout;
        int bleed = SplitListRenderer.GetRowBleedMargin(settings);
        Rectangle? region = null;

        void AddRow(int row)
        {
            if (!SplitDisplayRows.TryGetRowIndex(settings, splitStatuses, row, currentSplitIndex, out int visualRow))
            {
                return;
            }

            Rectangle rect = Rectangle.Inflate(
                compositeLayout.ToStatusLocal(layout.GetRowRect(visualRow)),
                bleed,
                bleed);
            region = region is Rectangle existing ? Rectangle.Union(existing, rect) : rect;
        }

        AddRow(currentSplitIndex);
        if (StatusOverlayHighlightsActive)
        {
            foreach (int row in overlayAnimations.SegmentBestDeltaHighlights.Keys)
            {
                AddRow(row);
            }
        }

        return region;
    }

    private bool TryRenderStatusOverlayRegion()
    {
        if (ComputeStatusOverlayDynamicRegion() is not Rectangle region)
        {
            return false;
        }

        statusOverlayPartialClipBounds = region;
        try
        {
            return overlayWindowController.RenderRegionImmediately(region);
        }
        finally
        {
            statusOverlayPartialClipBounds = null;
        }
    }

    private bool TryGetSplitRowAt(Point point, out int rowIndex, out Rectangle rowRect)
    {
        rowIndex = -1;
        rowRect = Rectangle.Empty;
        IReadOnlyList<SplitStatusSnapshot> statuses = splitStatuses;
        if (!TryGetLayout(out SplitLayout layout))
        {
            return false;
        }

        OverlayCompositeLayout? compositeLayout = overlayWindowsInitialized
            ? overlayBoundsController.CurrentLayout
            : null;
        Point compositePoint = compositeLayout?.MapStatusPointToComposite(point) ?? point;

        foreach (SplitDisplayRow row in SplitDisplayRows.Build(settings, statuses, currentSplitIndex))
        {
            Rectangle currentRowRect = layout.GetRowRect(row.RowIndex);
            if (currentRowRect.Contains(compositePoint))
            {
                rowIndex = row.StatusIndex;
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

        SplitStatusSnapshot status = splitStatuses[rowIndex];
        ColumnRects columns = SplitListRenderer.GetColumnRects(settings, rowRect, status.Definition.IsAttached);

        if (columns.Time is Rectangle timeRect && timeRect.Contains(point) && status.IsCompleted)
        {
            EditPracticeSplitTime(rowIndex, status);
        }
    }

    private void EditPracticeSplitTime(int rowIndex, SplitStatusSnapshot status)
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

        ExecuteAppCommand(AppCommand.EditPracticeSplitTime(rowIndex, parsedTime));
    }

    private void EditPracticeTotalTime()
    {
        string currentText = TimeText.FormatRecord(timerElapsed);
        if (!PromptForTime(Localizer.Get("Edit total time", settings), currentText, allowEmpty: false, out string? editedText) ||
            !TimeText.TryParse(editedText, out TimeSpan editedTime))
        {
            return;
        }

        ExecuteAppCommand(AppCommand.EditPracticeTotalTime(editedTime));
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
                GetCurrentLayoutRowCount(),
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

    private int GetCurrentLayoutRowCount()
    {
        return SplitDisplayRows.GetRequiredRowCount(settings, splitStatuses, currentSplitIndex);
    }

    private int GetCurrentReservedLayoutRowCount()
    {
        return SplitDisplayRows.GetReservedRowCount(settings, splitStatuses);
    }

    private static int GetLayoutRowCount(AppSettings settings)
    {
        SplitStatusSnapshot[] statuses = SplitCatalog.Build(settings)
            .Select(SplitStatusSnapshot.FromDefinition)
            .ToArray();
        return SplitDisplayRows.GetReservedRowCount(settings, statuses);
    }

    private void StartSplitCompletionAnimation(int completedIndex)
    {
        overlayAnimations.StartSplitCompletionAnimation(settings, splitStatuses, completedIndex);
        UpdateStatusPaintSchedulerState();
        QueueStatusOverlayRender();
    }

    private void TrackSegmentBestDeltaHighlight(int completedIndex)
    {
        overlayAnimations.TrackSegmentBestDeltaHighlight(settings, splitStatuses, completedIndex);
        QueueStatusOverlayRender();
    }

}

/// <summary>
/// Per-frame dynamic content of the status overlay while a run is in progress.
/// When this key is unchanged (and no highlight/completion animation is active),
/// the previously rendered frame is still pixel-accurate and painting is skipped.
/// </summary>
internal readonly record struct StatusOverlayDynamicKey(
    int CurrentSplitIndex,
    string DeltaText,
    int DeltaColorArgb);
