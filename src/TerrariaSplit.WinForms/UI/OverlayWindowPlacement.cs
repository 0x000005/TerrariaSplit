using System.Drawing;

namespace TerrariaSplit.UI;

internal static class OverlayWindowPlacement
{
    private const int MinimumVisibleLength = 48;

    public static Point Resolve(
        Size windowSize,
        int? savedX,
        int? savedY,
        Rectangle fallbackWorkingArea,
        IEnumerable<Rectangle> workingAreas)
    {
        if (savedX.HasValue && savedY.HasValue &&
            savedX.Value <= int.MaxValue - windowSize.Width &&
            savedY.Value <= int.MaxValue - windowSize.Height)
        {
            Rectangle savedBounds = new(savedX.Value, savedY.Value, windowSize.Width, windowSize.Height);
            if (workingAreas.Any(area => HasUsefulVisibleArea(savedBounds, area)))
            {
                return savedBounds.Location;
            }
        }

        return new Point(
            fallbackWorkingArea.Left + Math.Max(0, (fallbackWorkingArea.Width - windowSize.Width) / 2),
            fallbackWorkingArea.Top + Math.Max(0, (fallbackWorkingArea.Height - windowSize.Height) / 2));
    }

    private static bool HasUsefulVisibleArea(Rectangle windowBounds, Rectangle workingArea)
    {
        Rectangle visibleBounds = Rectangle.Intersect(windowBounds, workingArea);
        int requiredWidth = Math.Min(MinimumVisibleLength, windowBounds.Width);
        int requiredHeight = Math.Min(MinimumVisibleLength, windowBounds.Height);
        return visibleBounds.Width >= requiredWidth && visibleBounds.Height >= requiredHeight;
    }
}
