using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class SplitConditionEditorController
{
    private int dragIndex = -1;
    private Point dragStartPoint;

    public SplitCondition CurrentCondition { get; set; } = SplitCondition.AtLeast([], 1);

    public bool PreserveCurrentCondition { get; set; }

    public bool AdvancedMode { get; set; }

    public string AdvancedError { get; set; } = string.Empty;

    public bool UpdatingSettings { get; set; }

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
