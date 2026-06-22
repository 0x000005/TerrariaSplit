using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class SplitRouteListController
{
    private int dragIndex = -1;
    private Point dragStartPoint;

    public bool Dirty { get; private set; }

    public bool Refreshing { get; set; }

    public int LoadedEntryIndex { get; set; } = -1;

    public void MarkDirty()
    {
        Dirty = true;
    }

    public void ClearDirty()
    {
        Dirty = false;
    }

    public void ClearLoadedEntry()
    {
        LoadedEntryIndex = -1;
    }

    public void CancelDrag()
    {
        dragIndex = -1;
    }

    public void BeginDrag(int index, Point startPoint)
    {
        dragIndex = index;
        dragStartPoint = startPoint;
    }

    public bool TryConsumeDrag(MouseButtons button, Point currentPoint, out int index)
    {
        index = -1;
        if (dragIndex < 0 ||
            button != MouseButtons.Left ||
            !HasMovedBeyondDragThreshold(dragStartPoint, currentPoint))
        {
            return false;
        }

        index = dragIndex;
        dragIndex = -1;
        return true;
    }

    private static bool HasMovedBeyondDragThreshold(Point startPoint, Point currentPoint)
    {
        Size dragSize = SystemInformation.DragSize;
        Rectangle dragBounds = new(
            startPoint.X - (dragSize.Width / 2),
            startPoint.Y - (dragSize.Height / 2),
            dragSize.Width,
            dragSize.Height);
        return !dragBounds.Contains(currentPoint);
    }
}
