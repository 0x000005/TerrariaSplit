using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class ThemedListBox : ListBox
{
    private const int WmPaint = 0x000F;
    private const int WmVScroll = 0x0115;
    private const int WmMouseWheel = 0x020A;
    private const int LbAddString = 0x0180;
    private const int LbInsertString = 0x0181;
    private const int LbDeleteString = 0x0182;
    private const int LbResetContent = 0x0184;
    private const int LbSetTopIndex = 0x0197;
    private const int WsVScroll = 0x00200000;
    private const int ScrollBarGutterWidth = 34;
    private const int ScrollBarTrackWidth = 12;
    private const int MinThumbHeight = 36;

    private bool draggingThumb;
    private int dragStartY;
    private int dragStartTopIndex;

    public ThemedListBox()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        HorizontalScrollbar = false;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams createParams = base.CreateParams;
            createParams.Style &= ~WsVScroll;
            return createParams;
        }
    }

    public Rectangle GetItemContentBounds(Rectangle bounds)
    {
        int rightInset = ShouldShowScrollBar() ? ScrollBarGutterWidth : 0;
        return new Rectangle(
            bounds.Left,
            bounds.Top,
            Math.Max(0, bounds.Width - rightInset),
            bounds.Height);
    }

    protected override void WndProc(ref Message m)
    {
        int message = m.Msg;
        base.WndProc(ref m);

        if (message is WmPaint or WmVScroll or WmMouseWheel or LbSetTopIndex)
        {
            PaintScrollBar();
            return;
        }

        if (message is LbAddString or LbInsertString or LbDeleteString or LbResetContent)
        {
            Invalidate();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (GetThumbBounds().Contains(e.Location))
            {
                draggingThumb = true;
                dragStartY = e.Y;
                dragStartTopIndex = SafeTopIndex();
                Capture = true;
                Focus();
                return;
            }

            if (GetTrackBounds().Contains(e.Location))
            {
                TopIndex = PointToTopIndex(e.Y);
                Focus();
                Invalidate();
                return;
            }

            if (GetGutterBounds().Contains(e.Location))
            {
                Focus();
                return;
            }
        }

        base.OnMouseDown(e);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!draggingThumb)
        {
            base.OnMouseMove(e);
            return;
        }

        Rectangle track = GetTrackBounds();
        Rectangle thumb = GetThumbBounds();
        int travel = Math.Max(1, track.Height - thumb.Height);
        int delta = e.Y - dragStartY;
        TopIndex = dragStartTopIndex + (int)Math.Round(delta * (GetMaxTopIndex() / (float)travel));
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (draggingThumb)
        {
            draggingThumb = false;
            Capture = false;
            Invalidate();
            return;
        }

        base.OnMouseUp(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Invalidate();
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        Invalidate();
    }

    private void PaintScrollBar()
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }

        Rectangle gutter = GetGutterBounds();
        using Graphics graphics = CreateGraphics();
        using (var gutterBrush = new SolidBrush(UiTheme.Surface))
        {
            graphics.FillRectangle(gutterBrush, gutter);
        }

        Rectangle track = GetTrackBounds();
        if (track.IsEmpty)
        {
            return;
        }

        using (var trackBrush = new SolidBrush(Color.FromArgb(24, 30, 34)))
        using (var separatorPen = new Pen(UiTheme.Border))
        {
            graphics.FillRectangle(trackBrush, track);
            graphics.DrawLine(separatorPen, gutter.Left, gutter.Top, gutter.Left, gutter.Bottom);
        }

        Rectangle thumb = GetThumbBounds();
        if (!thumb.IsEmpty)
        {
            Color thumbColor = draggingThumb ? UiTheme.AccentDown : UiTheme.SurfaceRaised;
            using var thumbBrush = new SolidBrush(thumbColor);
            graphics.FillRectangle(thumbBrush, thumb);
        }
    }

    private Rectangle GetGutterBounds()
    {
        if (!ShouldShowScrollBar())
        {
            return Rectangle.Empty;
        }

        int width = Math.Min(ScrollBarGutterWidth, ClientSize.Width);
        return new Rectangle(Math.Max(0, ClientSize.Width - width), 0, width, ClientSize.Height);
    }

    private Rectangle GetTrackBounds()
    {
        Rectangle gutter = GetGutterBounds();
        if (gutter.IsEmpty)
        {
            return Rectangle.Empty;
        }

        int width = Math.Min(ScrollBarTrackWidth, gutter.Width);
        return new Rectangle(
            gutter.Left + Math.Max(0, (gutter.Width - width) / 2),
            gutter.Top,
            width,
            gutter.Height);
    }

    private Rectangle GetThumbBounds()
    {
        Rectangle track = GetTrackBounds();
        if (track.IsEmpty)
        {
            return Rectangle.Empty;
        }

        int visibleCount = GetVisibleItemCount();
        int thumbHeight = Math.Clamp(
            (int)Math.Round(track.Height * (visibleCount / (float)Math.Max(visibleCount, Items.Count))),
            Math.Min(MinThumbHeight, track.Height),
            track.Height);
        int travel = Math.Max(1, track.Height - thumbHeight);
        int maxTopIndex = GetMaxTopIndex();
        int thumbY = track.Y + (maxTopIndex == 0
            ? 0
            : (int)Math.Round(travel * (SafeTopIndex() / (float)maxTopIndex)));
        return new Rectangle(track.X, thumbY, track.Width, thumbHeight);
    }

    private int PointToTopIndex(int y)
    {
        Rectangle track = GetTrackBounds();
        Rectangle thumb = GetThumbBounds();
        int travel = Math.Max(1, track.Height - thumb.Height);
        int relativeY = Math.Clamp(y - track.Y - thumb.Height / 2, 0, travel);
        return (int)Math.Round(relativeY * (GetMaxTopIndex() / (float)travel));
    }

    private bool ShouldShowScrollBar()
    {
        return Items.Count > GetVisibleItemCount();
    }

    private int GetVisibleItemCount()
    {
        return Math.Max(1, ClientSize.Height / Math.Max(1, ItemHeight));
    }

    private int GetMaxTopIndex()
    {
        return Math.Max(0, Items.Count - GetVisibleItemCount());
    }

    private int SafeTopIndex()
    {
        if (Items.Count == 0)
        {
            return 0;
        }

        return Math.Clamp(TopIndex, 0, GetMaxTopIndex());
    }
}
