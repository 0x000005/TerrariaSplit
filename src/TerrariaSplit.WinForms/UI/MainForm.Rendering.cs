using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal sealed partial class MainForm : Form
{
    private void DrawStatusOverlay(Graphics graphics)
    {
        bool traceFirstRender = Interlocked.Increment(ref statusRenderCount) == 1;
        if (traceFirstRender)
        {
            StartupDiagnostics.RecordTrace("StatusRenderStarted");
        }

        if (!overlayShell.WindowsInitialized ||
            overlayShell.WindowInitializationInProgress ||
            !TryGetLayout(out SplitLayout layout))
        {
            return;
        }

        startupCore.StatusIconPreloadTask.GetAwaiter().GetResult();

        TimeSpan elapsed = timerElapsed;
        bool ignoreVisibleGroupLimit = ShouldIgnoreVisibleGroupLimitForCompletedRun();
        var context = new OverlayRenderContext(
            settings,
            overlayShell.Palette,
            runtimeShell.CurrentSnapshot,
            splitStatuses,
            currentSplitIndex,
            timerPhase,
            elapsed,
            layout,
            GetCurrentVisibleStatusRowCount(),
            overlayShell.MouseClickThrough,
            overlayShell.Animations.SplitCompletionAnimation,
            overlayShell.Animations.SegmentBestDeltaHighlights,
            DateTime.UtcNow,
            ignoreVisibleGroupLimit,
            TimerFillOverride: runtimeServices?.RaceShell.GetMainTimerRankColor(timerPhase, splitStatuses),
            CheatFilterIndicator: GetCheatFilterIndicatorLevel());
        OverlayRenderResult result = OverlayRenderer.RenderStatus(
            graphics,
            context,
            overlayShell.RenderResources,
            overlayShell.StatusOverlayPartialClipBounds);
        if (traceFirstRender)
        {
            StartupDiagnostics.RecordTrace("StatusContentRendered");
        }
        overlayShell.Animations.UpdateAfterRender(result);
        DrawStartupIndicator(graphics);

        overlayShell.RecordStatusOverlayRender(ComputeStatusOverlayDynamicKey(elapsed), result.AnimatedIconsActive);
        UpdateStatusPaintSchedulerState();
    }

    private void DrawStartupIndicator(Graphics graphics)
    {
        if (IsRuntimeReady)
        {
            return;
        }

        string text = Localizer.Get("Initializing...", settings);
        Font font = SystemFonts.MessageBoxFont ?? Control.DefaultFont;
        SizeF measured = graphics.MeasureString(text, font);
        int horizontalPadding = Math.Max(6, ScaleInt(6));
        int verticalPadding = Math.Max(3, ScaleInt(3));
        var bounds = new RectangleF(
            Math.Max(4, ScaleInt(4)),
            Math.Max(4, ScaleInt(4)),
            measured.Width + horizontalPadding * 2,
            measured.Height + verticalPadding * 2);
        using var background = new SolidBrush(Color.FromArgb(210, 28, 28, 32));
        using var foreground = new SolidBrush(Color.White);
        graphics.FillRectangle(background, bounds);
        graphics.DrawString(
            text,
            font,
            foreground,
            bounds.Left + horizontalPadding,
            bounds.Top + verticalPadding);
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

        SplitComparison comparison;
        IReadOnlyList<SplitExpandedConditionRow> expandedRows =
            SplitExpandedConditionRows.Build(settings, splitStatuses, index);
        SplitExpandedConditionRow? activeExpandedRow = expandedRows
            .Where(static row =>
                !row.CompletionTime.HasValue && row.ReferenceTime.HasValue)
            .Select(static row => (SplitExpandedConditionRow?)row)
            .FirstOrDefault();
        if (activeExpandedRow is SplitExpandedConditionRow expanded)
        {
            comparison = SplitComparisonService.GetRunningComparison(
                settings,
                timerPhase,
                elapsed,
                expanded.ReferenceTime.GetValueOrDefault(),
                isCurrent: true);
        }
        else
        {
            comparison = SplitComparisonService.GetSplitComparison(
                settings,
                timerPhase,
                elapsed,
                status,
                isCurrent: true);
        }
        string deltaText = SplitRenderData.FormatSplitDelta(settings, comparison);
        if (deltaText.Length == 0)
        {
            return new StatusOverlayDynamicKey(index, string.Empty, 0);
        }

        bool enableDeltaGradient = activeExpandedRow is null && status.Time is TimeSpan
            ? settings.Overlay.EnableDeltaGradientColor
            : settings.Overlay.EnableCurrentDeltaGradientColor;
        Color deltaColor = OverlayColorMath.GetDeltaComparisonColor(
            settings,
            comparison,
            overlayShell.Palette,
            enableDeltaGradient);
        return new StatusOverlayDynamicKey(index, deltaText, deltaColor.ToArgb());
    }

    private bool StatusOverlayHighlightsActive =>
        settings.Overlay.ShowSegmentBestDeltaHighlight &&
        overlayShell.Animations.SegmentBestDeltaHighlights.Count > 0;

    private Rectangle? ComputeStatusOverlayDynamicRegion()
    {
        if (!overlayShell.WindowsInitialized || !TryGetLayout(out SplitLayout layout))
        {
            return null;
        }

        OverlayCompositeLayout compositeLayout = overlayShell.BoundsController.CurrentLayout;
        int bleed = SplitListRenderer.GetRowBleedMargin(settings);
        bool ignoreVisibleGroupLimit = ShouldIgnoreVisibleGroupLimitForCompletedRun();
        IReadOnlyList<SplitDisplayRow> displayRows = SplitDisplayRows.Build(
            settings,
            splitStatuses,
            currentSplitIndex,
            GetCurrentVisibleStatusRowCount(),
            ignoreVisibleGroupLimit);
        Rectangle? region = null;

        void AddRows(int statusIndex)
        {
            foreach (SplitDisplayRow displayRow in displayRows.Where(row =>
                         row.StatusIndex == statusIndex))
            {
                Rectangle rect = Rectangle.Inflate(
                    compositeLayout.ToStatusLocal(layout.GetRowRect(displayRow.RowIndex)),
                    bleed,
                    bleed);
                region = region is Rectangle existing
                    ? Rectangle.Union(existing, rect)
                    : rect;
            }
        }

        AddRows(currentSplitIndex);
        if (StatusOverlayHighlightsActive)
        {
            foreach (int row in overlayShell.Animations.SegmentBestDeltaHighlights.Keys)
            {
                AddRows(row);
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

        overlayShell.BeginStatusOverlayPartialClip(region);
        try
        {
            return overlayShell.WindowController.RenderRegionImmediately(region);
        }
        finally
        {
            overlayShell.EndStatusOverlayPartialClip();
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

        OverlayCompositeLayout? compositeLayout = overlayShell.WindowsInitialized
            ? overlayShell.BoundsController.CurrentLayout
            : null;
        Point compositePoint = compositeLayout?.MapStatusPointToComposite(point) ?? point;

        foreach (SplitDisplayRow row in SplitDisplayRows.Build(
            settings,
            statuses,
            currentSplitIndex,
            GetCurrentVisibleStatusRowCount(),
            ShouldIgnoreVisibleGroupLimitForCompletedRun()))
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
        if (IsRaceRoomActive)
        {
            return;
        }

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
        if (IsRaceRoomActive)
        {
            return;
        }

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
        if (overlayShell.WindowsInitialized)
        {
            layout = overlayShell.BoundsController.CurrentLayout.Layout;
            return true;
        }

        if (!SplitLayoutCalculator.TryCreate(
                ClientRectangle,
                GetCurrentVisibleStatusRowCount(),
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
        return SplitDisplayRows.GetRequiredRowCount(
            settings,
            splitStatuses,
            currentSplitIndex,
            ShouldIgnoreVisibleGroupLimitForCompletedRun());
    }

    private int GetCurrentVisibleStatusRowCount()
    {
        return Math.Max(GetCurrentLayoutRowCount(), SplitCompletionAnimationRenderer.ReservedRowCount);
    }

    private int GetCurrentReservedLayoutRowCount()
    {
        return SplitDisplayRows.GetReservedRowCount(settings, splitStatuses);
    }

    private bool ShouldIgnoreVisibleGroupLimitForCompletedRun()
    {
        if (!settings.Route.EnableVisibleGroupCountLimit ||
            !settings.Route.ShowAllVisibleGroupsAfterFinalGroup ||
            timerPhase != SplitTimerPhase.Paused ||
            splitStatuses.Count == 0 ||
            currentSplitIndex < splitStatuses.Count)
        {
            return false;
        }

        SplitStatusSnapshot finalStatus = splitStatuses[^1];
        return finalStatus.IsCompleted || finalStatus.IsSkipped;
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
        overlayShell.Animations.StartSplitCompletionAnimation(settings, splitStatuses, completedIndex);
        UpdateStatusPaintSchedulerState();
        QueueStatusOverlayRender();
    }

    private void TrackSegmentBestDeltaHighlight(int completedIndex)
    {
        overlayShell.Animations.TrackSegmentBestDeltaHighlight(settings, splitStatuses, completedIndex);
        UpdateStatusPaintSchedulerState();
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
