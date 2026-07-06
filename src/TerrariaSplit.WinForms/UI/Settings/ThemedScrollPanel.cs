using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class ThemedScrollPanel : Panel
{
    private const int WmMouseWheel = 0x020A;
    private const int EmGetLineCount = 0x00BA;
    private const int EmGetFirstVisibleLine = 0x00CE;
    private const int EmLineScroll = 0x00B6;
    private const int WheelDelta = 120;
    private const int ScrollBarGutterWidth = 34;
    private const int ScrollBarTrackWidth = 12;
    private const int ScrollStep = 40;
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
        MouseWheel += HandleOuterMouseWheel;
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
        e.Graphics.Clear(BackColor);

        Rectangle gutter = GetGutterBounds();
        if (gutter.Width > 0 && gutter.Height > 0)
        {
            using var gutterBrush = new SolidBrush(UiTheme.Surface);
            e.Graphics.FillRectangle(gutterBrush, gutter);
        }

        Rectangle track = GetTrackBounds();
        if (track.Width <= 0 || track.Height <= 0)
        {
            return;
        }

        using (var trackBrush = new SolidBrush(Color.FromArgb(24, 30, 34)))
        using (var separatorPen = new Pen(UiTheme.Border))
        {
            e.Graphics.FillRectangle(trackBrush, track);
            if (gutter.Width > 0 && gutter.Left > 0)
            {
                e.Graphics.DrawLine(separatorPen, gutter.Left, gutter.Top, gutter.Left, gutter.Bottom);
            }
        }

        Rectangle thumb = GetThumbBounds();
        if (thumb.Width > 0 && thumb.Height > 0)
        {
            Color thumbColor = draggingThumb ? UiTheme.AccentDown : UiTheme.SurfaceRaised;
            using var thumbBrush = new SolidBrush(thumbColor);
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
        EventHandler visibleChanged = (_, _) => RequestLayoutContent();
        MouseEventHandler mouseWheel = HandleOuterMouseWheel;
        TextBoxWheelRouter? textBoxWheelRouter = control is TextBox textBox && ShouldRouteTextBoxMouseWheel(textBox)
            ? new TextBoxWheelRouter(this, textBox)
            : null;
        ListBoxWheelRouter? listBoxWheelRouter = textBoxWheelRouter is null && control is ListBox listBox
            ? new ListBoxWheelRouter(this, listBox)
            : null;
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
            visibleChanged,
            textBoxWheelRouter is null && listBoxWheelRouter is null ? mouseWheel : null,
            controlAdded,
            controlRemoved,
            textBoxWheelRouter,
            listBoxWheelRouter);

        control.SizeChanged += sizeChanged;
        control.VisibleChanged += visibleChanged;
        if (textBoxWheelRouter is null && listBoxWheelRouter is null)
        {
            control.MouseWheel += mouseWheel;
        }

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
        control.VisibleChanged -= handlers.VisibleChanged;
        if (handlers.MouseWheel is not null)
        {
            control.MouseWheel -= handlers.MouseWheel;
        }

        control.ControlAdded -= handlers.ControlAdded;
        control.ControlRemoved -= handlers.ControlRemoved;
        handlers.TextBoxWheelRouter?.Dispose();
        handlers.ListBoxWheelRouter?.Dispose();
    }

    private void ScrollBy(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        ScrollToOffset(scrollOffset - Math.Sign(delta) * ScaledScrollStep);
    }

    private void HandleOuterMouseWheel(object? sender, MouseEventArgs e)
    {
        if (e is HandledMouseEventArgs { Handled: true })
        {
            return;
        }

        ScrollBy(e.Delta);
        if (e is HandledMouseEventArgs handled)
        {
            handled.Handled = true;
        }
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
        int availableWidth = Math.Max(0, ClientSize.Width - Padding.Left - Padding.Right - ScaledScrollBarGutterWidth);
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

    private Rectangle GetGutterBounds()
    {
        return new Rectangle(
            Math.Max(0, ClientSize.Width - ScaledScrollBarGutterWidth),
            0,
            Math.Min(ScaledScrollBarGutterWidth, ClientSize.Width),
            ClientSize.Height);
    }

    private Rectangle GetTrackBounds()
    {
        Rectangle gutter = GetGutterBounds();
        int width = Math.Min(ScaledScrollBarTrackWidth, gutter.Width);
        return new Rectangle(
            gutter.Left + Math.Max(0, (gutter.Width - width) / 2),
            Padding.Top,
            width,
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
            UiDpiScale.ScaleIntFromBase200(36),
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

    private void RouteTextBoxMouseWheel(TextBox textBox, int delta)
    {
        if (!TryScrollTextBox(textBox, delta))
        {
            ScrollBy(delta);
        }
    }

    private void RouteListBoxMouseWheel(ListBox listBox, int delta)
    {
        if (!TryScrollListBox(listBox, delta))
        {
            ScrollBy(delta);
        }
    }

    private static bool ShouldRouteTextBoxMouseWheel(TextBox textBox)
    {
        return textBox.Multiline;
    }

    private static bool TryScrollTextBox(TextBox textBox, int delta)
    {
        if (delta == 0 || !textBox.IsHandleCreated)
        {
            return false;
        }

        int firstVisibleLine = NativeMethods.SendMessage(
            textBox.Handle,
            EmGetFirstVisibleLine,
            IntPtr.Zero,
            IntPtr.Zero).ToInt32();

        if (delta > 0)
        {
            if (firstVisibleLine <= 0)
            {
                return false;
            }
        }
        else
        {
            int lineCount = NativeMethods.SendMessage(
                textBox.Handle,
                EmGetLineCount,
                IntPtr.Zero,
                IntPtr.Zero).ToInt32();
            int visibleLineCount = Math.Max(1, textBox.ClientSize.Height / Math.Max(1, textBox.Font.Height));
            if (firstVisibleLine + visibleLineCount >= lineCount)
            {
                return false;
            }
        }

        int lineStep = Math.Max(1, (int)Math.Round(ScaledScrollStep / (float)Math.Max(1, textBox.Font.Height)));
        int signedLineStep = delta > 0 ? -lineStep : lineStep;
        NativeMethods.SendMessage(textBox.Handle, EmLineScroll, IntPtr.Zero, new IntPtr(signedLineStep));

        int newFirstVisibleLine = NativeMethods.SendMessage(
            textBox.Handle,
            EmGetFirstVisibleLine,
            IntPtr.Zero,
            IntPtr.Zero).ToInt32();
        return newFirstVisibleLine != firstVisibleLine;
    }

    private static bool TryScrollListBox(ListBox listBox, int delta)
    {
        if (delta == 0 || listBox.Items.Count == 0 || listBox.ItemHeight <= 0)
        {
            return false;
        }

        int visibleItemCount = Math.Max(1, listBox.ClientSize.Height / listBox.ItemHeight);
        int maxTopIndex = Math.Max(0, listBox.Items.Count - visibleItemCount);
        if (maxTopIndex <= 0)
        {
            return false;
        }

        int currentTopIndex = Math.Clamp(listBox.TopIndex, 0, maxTopIndex);
        int wheelLines = SystemInformation.MouseWheelScrollLines;
        int lineStep = wheelLines <= 0 || wheelLines >= int.MaxValue
            ? visibleItemCount
            : wheelLines;
        int wheelNotches = Math.Max(1, Math.Abs(delta) / WheelDelta);
        int signedStep = Math.Max(1, lineStep * wheelNotches);
        int nextTopIndex = delta > 0
            ? currentTopIndex - signedStep
            : currentTopIndex + signedStep;
        nextTopIndex = Math.Clamp(nextTopIndex, 0, maxTopIndex);
        if (nextTopIndex == currentTopIndex)
        {
            return false;
        }

        listBox.TopIndex = nextTopIndex;
        return listBox.TopIndex != currentTopIndex;
    }

    private sealed record AttachedContentHandlers(
        EventHandler SizeChanged,
        EventHandler VisibleChanged,
        MouseEventHandler? MouseWheel,
        ControlEventHandler ControlAdded,
        ControlEventHandler ControlRemoved,
        TextBoxWheelRouter? TextBoxWheelRouter,
        ListBoxWheelRouter? ListBoxWheelRouter);

    private static int ScaledScrollBarGutterWidth => UiDpiScale.ScaleIntFromBase200(ScrollBarGutterWidth);

    private static int ScaledScrollBarTrackWidth => UiDpiScale.ScaleIntFromBase200(ScrollBarTrackWidth);

    private static int ScaledScrollStep => UiDpiScale.ScaleIntFromBase200(ScrollStep);

    private sealed class TextBoxWheelRouter : NativeWindow, IDisposable
    {
        private readonly ThemedScrollPanel owner;
        private readonly TextBox textBox;

        public TextBoxWheelRouter(ThemedScrollPanel owner, TextBox textBox)
        {
            this.owner = owner;
            this.textBox = textBox;
            textBox.HandleCreated += HandleCreated;
            textBox.HandleDestroyed += HandleDestroyed;
            if (textBox.IsHandleCreated)
            {
                AssignHandle(textBox.Handle);
            }
        }

        public void Dispose()
        {
            textBox.HandleCreated -= HandleCreated;
            textBox.HandleDestroyed -= HandleDestroyed;
            ReleaseHandle();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmMouseWheel)
            {
                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xffff);
                owner.RouteTextBoxMouseWheel(textBox, delta);
                return;
            }

            base.WndProc(ref m);
        }

        private void HandleCreated(object? sender, EventArgs e)
        {
            AssignHandle(textBox.Handle);
        }

        private void HandleDestroyed(object? sender, EventArgs e)
        {
            ReleaseHandle();
        }
    }

    private sealed class ListBoxWheelRouter : NativeWindow, IDisposable
    {
        private readonly ThemedScrollPanel owner;
        private readonly ListBox listBox;

        public ListBoxWheelRouter(ThemedScrollPanel owner, ListBox listBox)
        {
            this.owner = owner;
            this.listBox = listBox;
            listBox.HandleCreated += HandleCreated;
            listBox.HandleDestroyed += HandleDestroyed;
            if (listBox.IsHandleCreated)
            {
                AssignHandle(listBox.Handle);
            }
        }

        public void Dispose()
        {
            listBox.HandleCreated -= HandleCreated;
            listBox.HandleDestroyed -= HandleDestroyed;
            ReleaseHandle();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmMouseWheel)
            {
                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xffff);
                owner.RouteListBoxMouseWheel(listBox, delta);
                return;
            }

            base.WndProc(ref m);
        }

        private void HandleCreated(object? sender, EventArgs e)
        {
            AssignHandle(listBox.Handle);
        }

        private void HandleDestroyed(object? sender, EventArgs e)
        {
            ReleaseHandle();
        }
    }
}
