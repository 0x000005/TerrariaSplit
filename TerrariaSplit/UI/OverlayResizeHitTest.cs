using System.Windows.Forms;

namespace TerrariaSplit;

internal static class OverlayResizeHitTest
{
    public static bool IsResizeZone(Point point, Size clientSize, int resizeBorder, OverlayResizeEdges allowedEdges)
    {
        return Resolve(point, clientSize, resizeBorder, allowedEdges).HasValue;
    }

    public static IntPtr? Resolve(Point point, Size clientSize, int resizeBorder, OverlayResizeEdges allowedEdges)
    {
        const int htLeft = 10;
        const int htRight = 11;
        const int htTop = 12;
        const int htTopLeft = 13;
        const int htTopRight = 14;
        const int htBottom = 15;
        const int htBottomLeft = 16;
        const int htBottomRight = 17;

        bool left = allowedEdges.HasFlag(OverlayResizeEdges.Left) && point.X <= resizeBorder;
        bool right = allowedEdges.HasFlag(OverlayResizeEdges.Right) && point.X >= clientSize.Width - resizeBorder;
        bool top = allowedEdges.HasFlag(OverlayResizeEdges.Top) && point.Y <= resizeBorder;
        bool bottom = allowedEdges.HasFlag(OverlayResizeEdges.Bottom) && point.Y >= clientSize.Height - resizeBorder;

        if (left && top)
        {
            return (IntPtr)htTopLeft;
        }

        if (right && top)
        {
            return (IntPtr)htTopRight;
        }

        if (left && bottom)
        {
            return (IntPtr)htBottomLeft;
        }

        if (right && bottom)
        {
            return (IntPtr)htBottomRight;
        }

        if (left)
        {
            return (IntPtr)htLeft;
        }

        if (right)
        {
            return (IntPtr)htRight;
        }

        if (top)
        {
            return (IntPtr)htTop;
        }

        if (bottom)
        {
            return (IntPtr)htBottom;
        }

        return null;
    }
}
