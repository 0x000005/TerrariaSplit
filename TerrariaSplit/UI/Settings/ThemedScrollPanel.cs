using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class ThemedScrollPanel : Panel
{
    private const int ScrollBarWidth = 12;
    private const int ScrollStep = 42;
    private int scrollOffset;
    private bool draggingThumb;
    private int dragThumbStartY;
    private int dragStartOffset;
    private int contentUpdateDepth;
    private bool layoutContentPending;
    private readonly Dictionary<Control, AttachedContentHandlers> attachedContentHandlers = new();

    public ThemedScrollPanel()
    {
        AutoScroll = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        MouseWheel += (_, e) => ScrollBy(e.Delta);
    }

    public void BeginContentUpdate()
    {
        contentUpdateDepth++;
        SuspendLayout();
    }

    public void EndContentUpdate()
    {
        if (contentUpdateDepth > 0)
        {
            contentUpdateDepth--;
        }

        ResumeLayout(false);
        if (contentUpdateDepth == 0 && layoutContentPending)
        {
            layoutContentPending = false;
            LayoutContent();
        }
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control is not null)
        {
            AttachContent(e.Control);
        }

        RequestLayoutContent();
    }

    protected override void OnControlRemoved(ControlEventArgs e)
    {
        if (e.Control is not null)
        {
            DetachContent(e.Control);
        }

        base.OnControlRemoved(e);
        RequestLayoutContent();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Rectangle thumb = GetThumbBounds();
        if (thumb.Contains(e.Location))
        {
            draggingThumb = true;
            dragThumbStartY = e.Y;
            dragStartOffset = scrollOffset;
            Capture = true;
            return;
        }

        if (GetTrackBounds().Contains(e.Location))
        {
            ScrollToOffset(PointToOffset(e.Y));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!draggingThumb)
        {
            return;
        }

        int maxOffset = GetMaxOffset();
        Rectangle track = GetTrackBounds();
        Rectangle thumb = GetThumbBounds();
        int travel = Math.Max(1, track.Height - thumb.Height);
        int delta = e.Y - dragThumbStartY;
        int offset = dragStartOffset + (int)Math.Round(delta * (maxOffset / (float)travel));
        ScrollToOffset(offset);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        draggingThumb = false;
        Capture = false;
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        RequestLayoutContent();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Rectangle track = GetTrackBounds();
        if (track.Width <= 0 || track.Height <= 0)
        {
            return;
        }

        using (var trackBrush = new SolidBrush(UiTheme.Field))
        {
            e.Graphics.FillRectangle(trackBrush, track);
        }

        Rectangle thumb = GetThumbBounds();
        if (thumb.Width > 0 && thumb.Height > 0)
        {
            using var thumbBrush = new SolidBrush(UiTheme.SurfaceRaised);
            e.Graphics.FillRectangle(thumbBrush, thumb);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (Control control in attachedContentHandlers.Keys.ToArray())
            {
                DetachContent(control);
            }
        }

        base.Dispose(disposing);
    }

    private void AttachContent(Control control)
    {
        if (attachedContentHandlers.ContainsKey(control))
        {
            return;
        }

        EventHandler sizeChanged = (_, _) => RequestLayoutContent();
        MouseEventHandler mouseWheel = (_, e) =>
        {
            if (ShouldChildHandleMouseWheel(control))
            {
                return;
            }

            ScrollBy(e.Delta);
        };
        ControlEventHandler controlAdded = (_, e) =>
        {
            if (e.Control is not null)
            {
                AttachContent(e.Control);
            }

            RequestLayoutContent();
        };
        ControlEventHandler controlRemoved = (_, e) =>
        {
            if (e.Control is not null)
            {
                DetachContent(e.Control);
            }

            RequestLayoutContent();
        };

        attachedContentHandlers[control] = new AttachedContentHandlers(
            sizeChanged,
            mouseWheel,
            controlAdded,
            controlRemoved);

        control.SizeChanged += sizeChanged;
        control.MouseWheel += mouseWheel;
        control.ControlAdded += controlAdded;
        control.ControlRemoved += controlRemoved;

        foreach (Control child in control.Controls)
        {
            AttachContent(child);
        }
    }

    private void DetachContent(Control control)
    {
        foreach (Control child in control.Controls.Cast<Control>().ToArray())
        {
            DetachContent(child);
        }

        if (!attachedContentHandlers.Remove(control, out AttachedContentHandlers? handlers))
        {
            return;
        }

        control.SizeChanged -= handlers.SizeChanged;
        control.MouseWheel -= handlers.MouseWheel;
        control.ControlAdded -= handlers.ControlAdded;
        control.ControlRemoved -= handlers.ControlRemoved;
    }

    private void ScrollBy(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        ScrollToOffset(scrollOffset - Math.Sign(delta) * ScrollStep);
    }

    private void LayoutContent()
    {
        if (contentUpdateDepth > 0)
        {
            layoutContentPending = true;
            return;
        }

        if (Controls.Count == 0)
        {
            return;
        }

        Control content = Controls[0];
        int availableWidth = Math.Max(0, ClientSize.Width - Padding.Horizontal - ScrollBarWidth - 10);
        Size preferredSize = content.GetPreferredSize(new Size(availableWidth, 0));
        int preferredHeight = Math.Max(0, preferredSize.Height);
        if (content.Width != availableWidth || content.Height != preferredHeight)
        {
            content.Width = availableWidth;
            content.Height = preferredHeight;
        }

        scrollOffset = Math.Clamp(scrollOffset, 0, GetMaxOffset());
        content.Location = new Point(Padding.Left, Padding.Top - scrollOffset);
        Invalidate();
    }

    private void RequestLayoutContent()
    {
        if (contentUpdateDepth > 0)
        {
            layoutContentPending = true;
            return;
        }

        LayoutContent();
    }

    private int GetMaxOffset()
    {
        if (Controls.Count == 0)
        {
            return 0;
        }

        Control content = Controls[0];
        int visibleHeight = Math.Max(0, ClientSize.Height - Padding.Vertical);
        return Math.Max(0, content.Height - visibleHeight);
    }

    private Rectangle GetTrackBounds()
    {
        return new Rectangle(
            ClientSize.Width - Padding.Right - ScrollBarWidth,
            Padding.Top,
            ScrollBarWidth,
            Math.Max(0, ClientSize.Height - Padding.Vertical));
    }

    private Rectangle GetThumbBounds()
    {
        Rectangle track = GetTrackBounds();
        int maxOffset = GetMaxOffset();
        if (track.Height <= 0)
        {
            return Rectangle.Empty;
        }

        if (maxOffset <= 0 || Controls.Count == 0)
        {
            return new Rectangle(track.X, track.Y, track.Width, track.Height);
        }

        Control content = Controls[0];
        int visibleHeight = Math.Max(1, ClientSize.Height - Padding.Vertical);
        int thumbHeight = Math.Clamp(
            (int)Math.Round(track.Height * (visibleHeight / (float)Math.Max(visibleHeight, content.Height))),
            36,
            track.Height);
        int travel = Math.Max(1, track.Height - thumbHeight);
        int thumbY = track.Y + (int)Math.Round(travel * (scrollOffset / (float)maxOffset));
        return new Rectangle(track.X, thumbY, track.Width, thumbHeight);
    }

    private int PointToOffset(int y)
    {
        int maxOffset = GetMaxOffset();
        Rectangle track = GetTrackBounds();
        Rectangle thumb = GetThumbBounds();
        int travel = Math.Max(1, track.Height - thumb.Height);
        int relativeY = Math.Clamp(y - track.Y - thumb.Height / 2, 0, travel);
        return (int)Math.Round(relativeY * (maxOffset / (float)travel));
    }

    private void ScrollToOffset(int offset)
    {
        scrollOffset = Math.Clamp(offset, 0, GetMaxOffset());
        if (Controls.Count > 0)
        {
            Control content = Controls[0];
            content.Location = new Point(Padding.Left, Padding.Top - scrollOffset);
        }

        Invalidate();
    }

    private static bool ShouldChildHandleMouseWheel(Control control)
    {
        return control is TextBox textBox &&
            textBox.Multiline &&
            textBox.ScrollBars != ScrollBars.None;
    }

    private sealed record AttachedContentHandlers(
        EventHandler SizeChanged,
        MouseEventHandler MouseWheel,
        ControlEventHandler ControlAdded,
        ControlEventHandler ControlRemoved);
}
