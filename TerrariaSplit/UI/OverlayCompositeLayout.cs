using System.Drawing;

namespace TerrariaSplit.UI;

internal readonly record struct OverlayCompositeLayout(
    Rectangle CompositeBounds,
    SplitLayout Layout,
    Rectangle StatusLocalBounds,
    Rectangle TimerLocalBounds)
{
    public Rectangle StatusScreenBounds => Offset(StatusLocalBounds, CompositeBounds.Location);

    public Rectangle TimerScreenBounds => Offset(TimerLocalBounds, CompositeBounds.Location);

    public Point MapStatusPointToComposite(Point localPoint)
    {
        return new Point(localPoint.X + StatusLocalBounds.X, localPoint.Y + StatusLocalBounds.Y);
    }

    public Point MapTimerPointToComposite(Point localPoint)
    {
        return new Point(localPoint.X + TimerLocalBounds.X, localPoint.Y + TimerLocalBounds.Y);
    }

    public Rectangle ToStatusLocal(Rectangle compositeRect)
    {
        return new Rectangle(
            compositeRect.X - StatusLocalBounds.X,
            compositeRect.Y - StatusLocalBounds.Y,
            compositeRect.Width,
            compositeRect.Height);
    }

    public Rectangle ToTimerLocal(Rectangle compositeRect)
    {
        return new Rectangle(
            compositeRect.X - TimerLocalBounds.X,
            compositeRect.Y - TimerLocalBounds.Y,
            compositeRect.Width,
            compositeRect.Height);
    }

    private static Rectangle Offset(Rectangle rect, Point delta)
    {
        return new Rectangle(rect.X + delta.X, rect.Y + delta.Y, rect.Width, rect.Height);
    }
}
