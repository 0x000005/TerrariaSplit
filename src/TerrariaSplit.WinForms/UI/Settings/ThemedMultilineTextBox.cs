using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class ThemedMultilineTextBox : TextBox
{
    private const int WmPaint = 0x000F;
    private const int WmVScroll = 0x0115;
    private const int WmMouseWheel = 0x020A;
    private const int EmLineScroll = 0x00B6;
    private const int EmGetLineCount = 0x00BA;
    private const int EmSetMargins = 0x00D3;
    private const int EmGetFirstVisibleLine = 0x00CE;
    private const int EcRightMargin = 0x0002;
    private const int WsVScroll = 0x00200000;
    private const int ScrollBarGutterWidth = 34;
    private const int ScrollBarTrackWidth = 12;
    private const int MinThumbHeight = 36;

    private bool draggingThumb;
    private int dragStartY;
    private int dragStartFirstVisibleLine;

    public ThemedMultilineTextBox()
    {
        Multiline = true;
        ScrollBars = ScrollBars.None;
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

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTextMargins();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ApplyTextMargins();
        Invalidate();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        ApplyTextMargins();
        Invalidate();
    }

    protected override void WndProc(ref Message m)
    {
        int message = m.Msg;
        base.WndProc(ref m);
        if (message is WmPaint or WmVScroll or WmMouseWheel or EmLineScroll)
        {
            PaintScrollBar();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (GetThumbBounds().Contains(e.Location))
            {
                draggingThumb = true;
                dragStartY = e.Y;
                dragStartFirstVisibleLine = GetFirstVisibleLine();
                Capture = true;
                Focus();
                return;
            }

            if (GetTrackBounds().Contains(e.Location))
            {
                ScrollToFirstVisibleLine(PointToFirstVisibleLine(e.Y));
                Focus();
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
        int nextLine = dragStartFirstVisibleLine + (int)Math.Round(delta * (GetMaxFirstVisibleLine() / (float)travel));
        ScrollToFirstVisibleLine(nextLine);
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

    private void ApplyTextMargins()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        int rightMargin = ShouldShowScrollBar()
            ? ScaledScrollBarGutterWidth + UiDpiScale.ScaleIntFromBase200(4)
            : 0;
        NativeMethods.SendMessage(
            Handle,
            EmSetMargins,
            new IntPtr(EcRightMargin),
            new IntPtr(rightMargin << 16));
    }

    private void PaintScrollBar()
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }

        Rectangle gutter = GetGutterBounds();
        if (gutter.IsEmpty)
        {
            return;
        }

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

        int width = Math.Min(ScaledScrollBarGutterWidth, ClientSize.Width);
        return new Rectangle(Math.Max(0, ClientSize.Width - width), 0, width, ClientSize.Height);
    }

    private Rectangle GetTrackBounds()
    {
        Rectangle gutter = GetGutterBounds();
        if (gutter.IsEmpty)
        {
            return Rectangle.Empty;
        }

        int width = Math.Min(ScaledScrollBarTrackWidth, gutter.Width);
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

        int visibleLineCount = GetVisibleLineCount();
        int lineCount = GetLineCount();
        int thumbHeight = Math.Clamp(
            (int)Math.Round(track.Height * (visibleLineCount / (float)Math.Max(visibleLineCount, lineCount))),
            Math.Min(ScaledMinThumbHeight, track.Height),
            track.Height);
        int travel = Math.Max(1, track.Height - thumbHeight);
        int maxFirstVisibleLine = GetMaxFirstVisibleLine();
        int thumbY = track.Y + (maxFirstVisibleLine == 0
            ? 0
            : (int)Math.Round(travel * (GetFirstVisibleLine() / (float)maxFirstVisibleLine)));
        return new Rectangle(track.X, thumbY, track.Width, thumbHeight);
    }

    private int PointToFirstVisibleLine(int y)
    {
        Rectangle track = GetTrackBounds();
        Rectangle thumb = GetThumbBounds();
        int travel = Math.Max(1, track.Height - thumb.Height);
        int relativeY = Math.Clamp(y - track.Y - thumb.Height / 2, 0, travel);
        return (int)Math.Round(relativeY * (GetMaxFirstVisibleLine() / (float)travel));
    }

    private bool ShouldShowScrollBar()
    {
        return GetLineCount() > GetVisibleLineCount();
    }

    private int GetLineCount()
    {
        return !IsHandleCreated
            ? Math.Max(1, Lines.Length)
            : Math.Max(1, NativeMethods.SendMessage(Handle, EmGetLineCount, IntPtr.Zero, IntPtr.Zero).ToInt32());
    }

    private int GetVisibleLineCount()
    {
        return Math.Max(1, ClientSize.Height / Math.Max(1, Font.Height));
    }

    private int GetMaxFirstVisibleLine()
    {
        return Math.Max(0, GetLineCount() - GetVisibleLineCount());
    }

    private int GetFirstVisibleLine()
    {
        if (!IsHandleCreated)
        {
            return 0;
        }

        return Math.Clamp(
            NativeMethods.SendMessage(Handle, EmGetFirstVisibleLine, IntPtr.Zero, IntPtr.Zero).ToInt32(),
            0,
            GetMaxFirstVisibleLine());
    }

    private void ScrollToFirstVisibleLine(int line)
    {
        if (!IsHandleCreated)
        {
            return;
        }

        int nextLine = Math.Clamp(line, 0, GetMaxFirstVisibleLine());
        int currentLine = GetFirstVisibleLine();
        int delta = nextLine - currentLine;
        if (delta == 0)
        {
            PaintScrollBar();
            return;
        }

        NativeMethods.SendMessage(Handle, EmLineScroll, IntPtr.Zero, new IntPtr(delta));
        PaintScrollBar();
    }

    private static int ScaledScrollBarGutterWidth => UiDpiScale.ScaleIntFromBase200(ScrollBarGutterWidth);

    private static int ScaledScrollBarTrackWidth => UiDpiScale.ScaleIntFromBase200(ScrollBarTrackWidth);

    private static int ScaledMinThumbHeight => UiDpiScale.ScaleIntFromBase200(MinThumbHeight);
}
