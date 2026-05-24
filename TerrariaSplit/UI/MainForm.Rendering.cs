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
            splitStatuses,
            currentSplitIndex,
            timerPhase,
            timerElapsed,
            layout,
            mouseClickThrough,
            overlayAnimations.SplitCompletionAnimation,
            overlayAnimations.SegmentBestDeltaHighlights,
            DateTime.UtcNow);
        OverlayRenderResult result = OverlayRenderer.RenderStatus(graphics, context, renderResources);
        overlayAnimations.UpdateAfterRender(result);
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

        SplitStatusSnapshot status = splitStatuses[rowIndex];
        ColumnRects columns = SplitListRenderer.GetColumnRects(settings, rowRect);

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
                splitStatuses.Count,
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
        overlayAnimations.StartSplitCompletionAnimation(settings, splitStatuses, completedIndex);
    }

    private void TrackSegmentBestDeltaHighlight(int completedIndex)
    {
        overlayAnimations.TrackSegmentBestDeltaHighlight(settings, splitStatuses, completedIndex);
    }

}
