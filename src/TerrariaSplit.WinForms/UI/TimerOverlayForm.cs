using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace TerrariaSplit.UI;

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
    private readonly Func<bool> isInteractionBlocked;
    private readonly Action dispatchedPaintTick;
    private TimeSpan paintInterval = DefaultRefreshInterval;
    private bool mouseClickThrough;
    private bool interactionBlocked;
    private bool dragging;
    private bool dragMoved;
    private Point dragStartCursor;
    private bool suppressBoundsNotification;
    private bool paintSuspended;
    private OverlayCompositeLayout? currentLayout;
    private TimerOverlayRenderState? currentState;
    private int paintDispatchPending;
    private int renderStateRevision;
    private int queuedPaintStateRevision;
    private TimerPaintFrame? previousRunningTimerPaintFrame;
    private TimeSpan? currentPaintTimerElapsed;
    private bool runningTimerPaintRequiresFullRender;
    private bool renderingRunningTimerRegion;

    public TimerOverlayForm(
        Func<bool> isInteractionBlocked,
        Action firstFramePresented)
    {
        this.isInteractionBlocked = isInteractionBlocked;
        dispatchedPaintTick = DispatchedPaintTick;
        overlayWindowController = new OverlayWindowController(
            this,
            DrawOverlay);
        overlayWindowController.FrameRendered += () =>
        {
            if (currentLayout is not null && currentState is not null)
            {
                firstFramePresented();
            }
        };
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

    public event Action? DragCompleted;

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

    protected override void SetVisibleCore(bool value)
    {
        base.SetVisibleCore(value && currentLayout is not null && currentState is not null);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        overlayWindowController.ApplyWindowStyle(mouseClickThrough, IsInteractionBlocked());
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        QueueFullRender();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        QueueFullRender();
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
        dragMoved = false;
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
        dragMoved = true;
        DragDeltaRequested?.Invoke(delta);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (IsInteractionBlocked())
        {
            dragging = false;
            dragMoved = false;
            RequestModalActivation();
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            bool moved = dragging && dragMoved;
            dragging = false;
            dragMoved = false;
            if (moved)
            {
                DragCompleted?.Invoke();
            }
        }

        if (e.Button == MouseButtons.Right)
        {
            RightClickRequested?.Invoke(new TimerOverlayRightClickRequest(e.Location, PointToScreen(e.Location)));
        }
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        QueueFullRender();
        NotifyUserResizeBoundsChanged();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        QueueFullRender();
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
        ShowIfInitialStateReady();
        QueueFullRender();
    }

    public void ApplyRenderState(TimerOverlayRenderState renderState, bool forceRender)
    {
        TimerOverlayRenderState? previousState = currentState;
        currentState = renderState;
        mouseClickThrough = renderState.MouseClickThrough;
        Interlocked.Increment(ref renderStateRevision);
        overlayWindowController.ApplyWindowStyle(mouseClickThrough, IsInteractionBlocked());
        UpdateTimerOverlayPaintSchedulerState();
        ShowIfInitialStateReady();
        if (forceRender || ShouldRenderImmediately(previousState, renderState))
        {
            RenderStateChange(renderState);
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
            QueueFullRender();
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
        QueueFullRender();
    }

    public void RequestRender()
    {
        QueueFullRender();
    }

    private void ShowIfInitialStateReady()
    {
        if (!Visible && currentLayout is not null && currentState is not null)
        {
            Show();
        }
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

    private void QueueTimerOverlayPaintTick(HighPrecisionSchedulerTick _)
    {
        if (!CanDispatchToUiThread())
        {
            return;
        }

        int queuedStateRevision = Volatile.Read(ref renderStateRevision);
        if (Interlocked.Exchange(ref paintDispatchPending, 1) == 1)
        {
            return;
        }

        Volatile.Write(ref queuedPaintStateRevision, queuedStateRevision);
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

            if (IsQueuedPaintStateStale())
            {
                return;
            }

            if (!UiInputMessageProbe.HasPendingInputMessage())
            {
                RenderTimerOverlayPaintTick();
            }
        }
        finally
        {
            Interlocked.Exchange(ref paintDispatchPending, 0);
        }
    }

    private bool IsQueuedPaintStateStale()
    {
        return Volatile.Read(ref queuedPaintStateRevision) !=
            Volatile.Read(ref renderStateRevision);
    }

    private void QueueFullRender()
    {
        previousRunningTimerPaintFrame = null;
        runningTimerPaintRequiresFullRender = true;
        overlayWindowController.QueueRender();
    }

    private void RenderStateChange(TimerOverlayRenderState renderState)
    {
        previousRunningTimerPaintFrame = null;
        runningTimerPaintRequiresFullRender = true;
        if (renderState.TimerState.Phase == SplitTimerPhase.Running)
        {
            overlayWindowController.QueueRender();
            return;
        }

        RenderImmediately(timerElapsed: null);
    }

    private void RenderTimerOverlayPaintTick()
    {
        TimeSpan? timerElapsed = TryGetRunningTimerElapsed();
        if (timerElapsed.HasValue && TryRenderRunningTimerRegion(timerElapsed.Value))
        {
            return;
        }

        previousRunningTimerPaintFrame = null;
        RenderImmediately(timerElapsed);
    }

    private TimeSpan? TryGetRunningTimerElapsed()
    {
        if (currentState?.TimerState.Phase != SplitTimerPhase.Running)
        {
            return null;
        }

        long nowTimestamp = Stopwatch.GetTimestamp();
        return SplitTimer.ElapsedAt(currentState.TimerState, nowTimestamp);
    }

    private void RenderImmediately(TimeSpan? timerElapsed)
    {
        currentPaintTimerElapsed = timerElapsed;
        try
        {
            overlayWindowController.RenderImmediately();
            runningTimerPaintRequiresFullRender = false;
            previousRunningTimerPaintFrame = null;
        }
        finally
        {
            currentPaintTimerElapsed = null;
        }
    }

    private bool TryRenderRunningTimerRegion(TimeSpan timerElapsed)
    {
        if (currentLayout is null || runningTimerPaintRequiresFullRender)
        {
            return false;
        }

        TimerPaintFrame? currentPaintFrame = null;
        bool rendered;
        currentPaintTimerElapsed = timerElapsed;
        renderingRunningTimerRegion = true;
        try
        {
            rendered = overlayWindowController.RenderRegionImmediately(graphics =>
            {
                RunningTimerPaintUpdate update = GetRunningTimerPaintUpdate(graphics, timerElapsed);
                currentPaintFrame = update.Frame;
                return update.DirtyRect ?? Rectangle.Empty;
            });
        }
        finally
        {
            renderingRunningTimerRegion = false;
            currentPaintTimerElapsed = null;
        }

        if (rendered && currentPaintFrame.HasValue)
        {
            previousRunningTimerPaintFrame = currentPaintFrame.Value;
        }

        return rendered;
    }

    private RunningTimerPaintUpdate GetRunningTimerPaintUpdate(Graphics graphics, TimeSpan timerElapsed)
    {
        if (currentLayout is not OverlayCompositeLayout layout || currentState is null)
        {
            return new RunningTimerPaintUpdate(TimerPaintFrame.Empty, null);
        }

        OverlayRenderContext context = CreateRenderContext(layout, currentState, timerElapsed);
        TimerPaintFrame frame = TimerRenderer.GetTimerPaintFrame(graphics, context, renderResources);
        Rectangle compositeDirtyRect = GetChangedTimerPaintBounds(previousRunningTimerPaintFrame, frame);
        if (compositeDirtyRect.Width <= 0 || compositeDirtyRect.Height <= 0)
        {
            return new RunningTimerPaintUpdate(frame, null);
        }

        int guard = GetRunningTimerPaintGuard(graphics);
        compositeDirtyRect.Inflate(guard, guard);

        Rectangle localDirtyRect = layout.ToTimerLocal(compositeDirtyRect);
        localDirtyRect.Intersect(new Rectangle(Point.Empty, ClientSize));
        return new RunningTimerPaintUpdate(
            frame,
            localDirtyRect.Width > 0 && localDirtyRect.Height > 0
                ? localDirtyRect
                : null);
    }

    private static int GetRunningTimerPaintGuard(Graphics graphics)
    {
        float dpiScale = Math.Max(graphics.DpiX, graphics.DpiY) / 96f;
        return Math.Clamp((int)Math.Ceiling(2f * dpiScale), 2, 8);
    }

    private static Rectangle GetChangedTimerPaintBounds(TimerPaintFrame? previousFrame, TimerPaintFrame currentFrame)
    {
        if (!previousFrame.HasValue)
        {
            return currentFrame.PaintBounds;
        }

        Rectangle dirty = Rectangle.Empty;
        TimerPaintFrame previous = previousFrame.Value;
        AddChangedTimerElementBounds(ref dirty, previous.Main, currentFrame.Main);
        AddChangedTimerElementBounds(ref dirty, previous.Milliseconds, currentFrame.Milliseconds);
        AddChangedTimerElementBounds(ref dirty, previous.Indicator, currentFrame.Indicator);
        AddChangedTimerElementBounds(ref dirty, previous.PyramidFilterIndicator, currentFrame.PyramidFilterIndicator);
        return dirty;
    }

    private static void AddChangedTimerElementBounds(
        ref Rectangle dirty,
        TimerPaintElement previous,
        TimerPaintElement current)
    {
        if (previous.Equals(current))
        {
            return;
        }

        AddTimerElementBounds(ref dirty, previous);
        AddTimerElementBounds(ref dirty, current);
    }

    private static void AddTimerElementBounds(ref Rectangle dirty, TimerPaintElement element)
    {
        if (!element.HasPaint)
        {
            return;
        }

        dirty = dirty.IsEmpty ? element.Bounds : Rectangle.Union(dirty, element.Bounds);
    }

    private bool CanDispatchToUiThread()
    {
        return IsHandleCreated && !IsDisposed && !Disposing;
    }

    private static bool ShouldRenderImmediately(
        TimerOverlayRenderState? previousState,
        TimerOverlayRenderState currentState)
    {
        if (previousState is null)
        {
            return true;
        }

        if (previousState.MouseClickThrough != currentState.MouseClickThrough)
        {
            return true;
        }

        if (previousState.CheatFilterIndicator != currentState.CheatFilterIndicator)
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

        TimeSpan timerElapsed = currentPaintTimerElapsed ??
            SplitTimer.ElapsedAt(currentState.TimerState, Stopwatch.GetTimestamp());
        graphics.TranslateTransform(-layout.TimerLocalBounds.X, -layout.TimerLocalBounds.Y);
        try
        {
            OverlayRenderContext context = CreateRenderContext(layout, currentState, timerElapsed);
            OverlayRenderer.RenderTimer(graphics, context, renderResources);
            if (!renderingRunningTimerRegion && currentState.TimerState.Phase == SplitTimerPhase.Running)
            {
                runningTimerPaintRequiresFullRender = false;
                previousRunningTimerPaintFrame = null;
            }

            return true;
        }
        finally
        {
            graphics.ResetTransform();
        }
    }

    private static OverlayRenderContext CreateRenderContext(
        OverlayCompositeLayout layout,
        TimerOverlayRenderState state,
        TimeSpan timerElapsed)
    {
        return new OverlayRenderContext(
            state.Settings,
            state.Palette,
            UnknownSnapshot,
            state.Statuses,
            state.CurrentSplitIndex,
            state.TimerState.Phase,
            timerElapsed,
            layout.Layout,
            1,
            state.MouseClickThrough,
            null,
            EmptySegmentBestDeltaHighlights,
            DateTime.UtcNow,
            TimerFillOverride: state.TimerFillOverride,
            CheatFilterIndicator: state.CheatFilterIndicator);
    }

    private readonly record struct RunningTimerPaintUpdate(TimerPaintFrame Frame, Rectangle? DirtyRect);

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
