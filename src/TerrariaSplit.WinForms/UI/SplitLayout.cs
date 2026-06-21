using System.Drawing;

namespace TerrariaSplit.UI;

internal readonly record struct SplitLayout(Rectangle FirstRowRect, Rectangle TimerRect, int RowGap)
{
    public Rectangle GetRowRect(int index)
    {
        return new Rectangle(
            FirstRowRect.X,
            FirstRowRect.Y + index * (FirstRowRect.Height + RowGap),
            FirstRowRect.Width,
            FirstRowRect.Height);
    }
}
