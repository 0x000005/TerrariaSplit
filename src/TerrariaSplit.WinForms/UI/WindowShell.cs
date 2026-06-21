using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

internal enum WindowCloseAction
{
    AllowClose,
    CancelAlreadyPending,
    StartFinalization
}

internal sealed class WindowShell
{
    private bool dragging;
    private Point dragStartCursor;
    private bool closeFinalizationPending;
    private bool closeFinalizationComplete;
    private string currentWindowText = string.Empty;

    public bool IsDragging => dragging;

    public bool IsClosing { get; private set; }

    public WindowCloseAction RequestClose()
    {
        if (closeFinalizationComplete)
        {
            return WindowCloseAction.AllowClose;
        }

        if (closeFinalizationPending)
        {
            return WindowCloseAction.CancelAlreadyPending;
        }

        closeFinalizationPending = true;
        return WindowCloseAction.StartFinalization;
    }

    public void CompleteCloseFinalization()
    {
        closeFinalizationPending = false;
        closeFinalizationComplete = true;
    }

    public void MarkClosing()
    {
        IsClosing = true;
    }

    public void BeginDrag(Point cursorPosition)
    {
        dragging = true;
        dragStartCursor = cursorPosition;
    }

    public bool TryMoveDrag(Point cursorPosition, out Point delta)
    {
        delta = default;
        if (!dragging)
        {
            return false;
        }

        delta = new Point(cursorPosition.X - dragStartCursor.X, cursorPosition.Y - dragStartCursor.Y);
        if (delta.X == 0 && delta.Y == 0)
        {
            return false;
        }

        dragStartCursor = cursorPosition;
        return true;
    }

    public void CancelDrag()
    {
        dragging = false;
    }

    public void SyncTitle(Form form, string title)
    {
        if (string.Equals(title, currentWindowText, StringComparison.Ordinal))
        {
            return;
        }

        currentWindowText = title;
        form.Text = title;
    }
}
