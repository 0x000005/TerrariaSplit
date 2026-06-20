using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class TimerOverlayForm : Form
{
    private static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromMilliseconds(16);
    private static readonly TerrariaWatchSnapshot UnknownSnapshot =
        new(false, null, false, null, TerrariaGameFacts.Unknown, TerrariaWorldGenerationState.Unknown, false, string.Empty);
    private static readonly Dictionary<int, SegmentBestDeltaHighlight> EmptySegmentBestDeltaHighlights = new();
    private const string MainTimerWindowTitle = "TerrariaSplit - Main Timer";
    private const int ResizeBorder = 8;
    private const int WsExTransparent = 0x20;
    private const int WsExLayered = 0x80000;
    private const int WsExNoActivate = 0x08000000;
    private readonly OverlayWindowController overlayWindowController;
    private readonly OverlayRenderResources renderResources = new();
    private readonly HighPrecisionScheduler paintScheduler;
    private readonly Action<HighPrecisionSchedulerTick> recordPaintTick;
    private readonly Action recordPaintDispatchSkipped;
    private readonly Action recordPaintInputSkipped;
    private readonly Func<bool> isInteractionBlocked;
    private readonly Action dispatchedPaintTick;
    private TimeSpan paintInterval = DefaultRefreshInterval;
    private bool mouseClickThrough;
    private bool interactionBlocked;
    private bool dragging;
    private Point dragStartCursor;
    private bool suppressBoundsNotification;
    private bool paintSuspended;
    private OverlayCompositeLayout? currentLayout;
    private TimerOverlayRenderState? currentState;
    private int paintDispatchPending;

    public TimerOverlayForm(
        Action<TimeSpan> recordPaint,
        Action<HighPrecisionSchedulerTick> recordPaintTick,
        Action recordPaintDispatchSkipped,
        Action recordPaintInputSkipped,
        Func<bool> isInteractionBlocked)
    {
        this.recordPaintTick = recordPaintTick;
        this.recordPaintDispatchSkipped = recordPaintDispatchSkipped;
        this.recordPaintInputSkipped = recordPaintInputSkipped;
        this.isInteractionBlocked = isInteractionBlocked;
        dispatchedPaintTick = DispatchedPaintTick;
        overlayWindowController = new OverlayWindowController(
            this,
            DrawOverlay,
            recordPaint);
        Text = MainTimerWindowTitle;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.Black;
        TransparencyKey = Color.Empty;
        Padding = Padding.Empty;
        paintScheduler = new HighPrecisionScheduler("TerrariaSplit timer paint", QueueTimerOverlayPaintTick);
    }

    public event Action<Point>? DragDeltaRequested;

    public event Action<Rectangle>? UserResizeBoundsChanged;

    public event Action<TimerOverlayRightClickRequest>? RightClickRequested;

    public event Action? ModalActivationRequested;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.Style = OverlayWindowController.ComposeBorderlessStyle(parameters.Style);
            parameters.ExStyle |= WsExLayered;
            if (IsInteractionBlocked())
            {
                parameters.ExStyle |= WsExNoActivate;
            }

            if (mouseClickThrough)
            {
                parameters.ExStyle |= WsExTransparent;
            }

            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        overlayWindowController.ApplyWindowStyle(mouseClickThrough, IsInteractionBlocked());
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        overlayWindowController.QueueRender();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        overlayWindowController.QueueRender();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (IsInteractionBlocked())
        {
            RequestModalActivation();
            return;
        }

        if (e.Button != MouseButtons.Left ||
            !IsTimerInteractionPoint(e.Location) ||
            OverlayResizeHitTest.IsResizeZone(e.Location, ClientSize, ResizeBorder, OverlayResizeEdges.Left | OverlayResizeEdges.Right | OverlayResizeEdges.Bottom))
        {
            return;
        }

        dragging = true;
        dragStartCursor = Cursor.Position;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (IsInteractionBlocked())
        {
            return;
        }

        if (!dragging)
        {
            return;
        }

        Point currentCursor = Cursor.Position;
        Point delta = new(currentCursor.X - dragStartCursor.X, currentCursor.Y - dragStartCursor.Y);
        if (delta.X == 0 && delta.Y == 0)
        {
            return;
        }

        dragStartCursor = currentCursor;
        DragDeltaRequested?.Invoke(delta);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (IsInteractionBlocked())
        {
            dragging = false;
            RequestModalActivation();
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            dragging = false;
        }

        if (e.Button == MouseButtons.Right)
        {
            RightClickRequested?.Invoke(new TimerOverlayRightClickRequest(e.Location, PointToScreen(e.Location)));
        }
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        overlayWindowController.QueueRender();
        NotifyUserResizeBoundsChanged();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        overlayWindowController.QueueRender();
        NotifyUserResizeBoundsChanged();
    }

    protected override void WndProc(ref Message m)
    {
        const int wmMouseActivate = 0x21;
        const int wmNcHitTest = 0x84;
        const int maNoActivateAndEat = 4;
        const int htTransparent = -1;
        const int htClient = 1;

        if (IsInteractionBlocked() && m.Msg == wmMouseActivate)
        {
            RequestModalActivation();
            m.Result = (IntPtr)maNoActivateAndEat;
            return;
        }

        base.WndProc(ref m);

        if (mouseClickThrough && m.Msg == wmNcHitTest)
        {
            m.Result = (IntPtr)htTransparent;
            return;
        }

        if (m.Msg != wmNcHitTest || m.Result != (IntPtr)htClient)
        {
            return;
        }

        long lParam = m.LParam.ToInt64();
        int x = unchecked((short)(lParam & 0xFFFF));
        int y = unchecked((short)((lParam >> 16) & 0xFFFF));
        Point point = PointToClient(new Point(x, y));
        IntPtr? hit = OverlayResizeHitTest.Resolve(
            point,
            ClientSize,
            ResizeBorder,
            OverlayResizeEdges.Left | OverlayResizeEdges.Right | OverlayResizeEdges.Bottom);
        if (hit.HasValue)
        {
            m.Result = hit.Value;
            return;
        }

        if (!IsTimerInteractionPoint(point))
        {
            m.Result = (IntPtr)htTransparent;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            paintScheduler.Dispose();
            overlayWindowController.Dispose();
            renderResources.Dispose();
        }

        base.Dispose(disposing);
    }

    public void ApplyOverlayLayout(OverlayCompositeLayout layout)
    {
        currentLayout = layout;
        ApplyWindowBounds(layout.TimerScreenBounds);
        overlayWindowController.QueueRender();
    }

    public void ApplyRenderState(TimerOverlayRenderState renderState, bool forceRender)
    {
        TimerOverlayRenderState? previousState = currentState;
        bool previousMouseClickThrough = mouseClickThrough;
        currentState = renderState;
        mouseClickThrough = renderState.MouseClickThrough;
        overlayWindowController.ApplyWindowStyle(mouseClickThrough, IsInteractionBlocked());
        UpdateTimerOverlayPaintSchedulerState();
        if (forceRender || ShouldRenderImmediately(previousState, renderState, previousMouseClickThrough))
        {
            overlayWindowController.QueueRender();
        }
    }

    public void ApplyRefreshInterval(TimeSpan interval)
    {
        if (paintInterval != interval)
        {
            paintInterval = interval;
            paintScheduler.UpdateInterval(paintInterval);
        }

        UpdateTimerOverlayPaintSchedulerState();
    }

    public void ApplyPaintSuspended(bool suspended)
    {
        if (paintSuspended == suspended)
        {
            return;
        }

        paintSuspended = suspended;
        UpdateTimerOverlayPaintSchedulerState();
        if (!paintSuspended)
        {
            overlayWindowController.QueueRender();
        }
    }

    public void ApplyInteractionBlocked(bool blocked)
    {
        if (interactionBlocked == blocked)
        {
            return;
        }

        interactionBlocked = blocked;
        dragging = false;
        overlayWindowController.ApplyWindowStyle(mouseClickThrough, IsInteractionBlocked());
    }

    private bool IsInteractionBlocked()
    {
        return interactionBlocked || isInteractionBlocked?.Invoke() == true;
    }

    private void RequestModalActivation()
    {
        ModalActivationRequested?.Invoke();
    }

    public void ApplyMouseClickThrough(bool enabled)
    {
        mouseClickThrough = enabled;
        overlayWindowController.ApplyWindowStyle(mouseClickThrough, IsInteractionBlocked());
        overlayWindowController.QueueRender();
    }

    public void RequestRender()
    {
        overlayWindowController.QueueRender();
    }

    public void ApplyWindowBounds(Rectangle bounds)
    {
        if (Bounds == bounds)
        {
            return;
        }

        suppressBoundsNotification = true;
        try
        {
            Bounds = bounds;
        }
        finally
        {
            suppressBoundsNotification = false;
        }
    }

    private void NotifyUserResizeBoundsChanged()
    {
        if (suppressBoundsNotification || dragging)
        {
            return;
        }

        UserResizeBoundsChanged?.Invoke(Bounds);
    }

    private void UpdateTimerOverlayPaintSchedulerState()
    {
        bool shouldRun = !paintSuspended &&
            currentState?.TimerState.Phase == SplitTimerPhase.Running;
        if (shouldRun && !paintScheduler.IsRunning)
        {
            paintScheduler.Start(paintInterval);
        }
        else if (!shouldRun && paintScheduler.IsRunning)
        {
            paintScheduler.Stop();
        }
    }

    private void QueueTimerOverlayPaintTick(HighPrecisionSchedulerTick tick)
    {
        recordPaintTick(tick);

        if (!CanDispatchToUiThread())
        {
            return;
        }

        if (Interlocked.Exchange(ref paintDispatchPending, 1) == 1)
        {
            recordPaintDispatchSkipped();
            return;
        }

        try
        {
            BeginInvoke(dispatchedPaintTick);
        }
        catch (ObjectDisposedException)
        {
            Interlocked.Exchange(ref paintDispatchPending, 0);
        }
        catch (InvalidOperationException)
        {
            Interlocked.Exchange(ref paintDispatchPending, 0);
        }
    }

    private void DispatchedPaintTick()
    {
        try
        {
            if (!CanDispatchToUiThread())
            {
                return;
            }

            if (!UiInputMessageProbe.HasPendingInputMessage())
            {
                overlayWindowController.RenderImmediately();
            }
            else
            {
                recordPaintInputSkipped();
            }
        }
        finally
        {
            Interlocked.Exchange(ref paintDispatchPending, 0);
        }
    }

    private bool CanDispatchToUiThread()
    {
        return IsHandleCreated && !IsDisposed && !Disposing;
    }

    private static bool ShouldRenderImmediately(
        TimerOverlayRenderState? previousState,
        TimerOverlayRenderState currentState,
        bool previousMouseClickThrough)
    {
        if (previousState is null)
        {
            return true;
        }

        if (previousMouseClickThrough != currentState.MouseClickThrough)
        {
            return true;
        }

        SplitTimerPhase previousPhase = previousState.TimerState.Phase;
        SplitTimerPhase currentPhase = currentState.TimerState.Phase;
        if (previousPhase != currentPhase)
        {
            return true;
        }

        return currentPhase != SplitTimerPhase.Running;
    }

    private bool DrawOverlay(Graphics graphics)
    {
        if (currentLayout is not OverlayCompositeLayout layout || currentState is null)
        {
            return true;
        }

        long nowTimestamp = Stopwatch.GetTimestamp();
        TimeSpan timerElapsed = SplitTimer.ElapsedAt(currentState.TimerState, nowTimestamp);
        graphics.TranslateTransform(-layout.TimerLocalBounds.X, -layout.TimerLocalBounds.Y);
        try
        {
            var context = new OverlayRenderContext(
                currentState.Settings,
                currentState.Palette,
                UnknownSnapshot,
                currentState.Statuses,
                currentState.CurrentSplitIndex,
                currentState.TimerState.Phase,
                timerElapsed,
                layout.Layout,
                1,
                currentState.MouseClickThrough,
                null,
                EmptySegmentBestDeltaHighlights,
                DateTime.UtcNow);
            OverlayRenderer.RenderTimer(graphics, context, renderResources);
            return true;
        }
        finally
        {
            graphics.ResetTransform();
        }
    }

    private bool IsTimerInteractionPoint(Point point)
    {
        if (currentLayout is not OverlayCompositeLayout layout || currentState is null)
        {
            return false;
        }

        Point compositePoint = layout.MapTimerPointToComposite(point);
        Rectangle timerTextBounds = TimerRenderer.GetTimerTextBounds(currentState.Settings, layout.Layout.TimerRect);
        Rectangle interactionBounds = Rectangle.Union(layout.Layout.TimerRect, timerTextBounds);
        int padding = Math.Max(
            OverlayRenderContext.ScaleInt(currentState.Settings, 18),
            ResizeBorder);
        interactionBounds.Inflate(padding, padding);
        return interactionBounds.Contains(compositePoint);
    }
}
