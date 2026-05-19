using System.Drawing;
using System.Threading;

namespace TerrariaSplit;

internal sealed class TimerOverlayWindowHost : IDisposable
{
    private readonly Action<Action> mainThreadDispatch;
    private readonly Action<TimeSpan> recordPaint;
    private readonly Action<HighPrecisionSchedulerTick> recordPaintTick;
    private readonly Action recordPaintDispatchSkipped;
    private readonly Action recordPaintInputSkipped;
    private readonly object sync = new();
    private readonly ManualResetEventSlim ready = new(false);
    private Thread? thread;
    private TimerOverlayForm? form;
    private OverlayCompositeLayout? latestLayout;
    private TimerOverlayRenderState? latestRenderState;
    private TimerOverlayStateKey? latestRenderStateKey;
    private TimeSpan latestRefreshInterval = TimeSpan.FromMilliseconds(16);
    private bool latestTopMost = true;
    private bool latestMouseClickThrough;
    private bool latestPaintSuspended;
    private bool disposed;

    public TimerOverlayWindowHost(
        Action<Action> mainThreadDispatch,
        Action<TimeSpan> recordPaint,
        Action<HighPrecisionSchedulerTick> recordPaintTick,
        Action recordPaintDispatchSkipped,
        Action recordPaintInputSkipped)
    {
        this.mainThreadDispatch = mainThreadDispatch;
        this.recordPaint = recordPaint;
        this.recordPaintTick = recordPaintTick;
        this.recordPaintDispatchSkipped = recordPaintDispatchSkipped;
        this.recordPaintInputSkipped = recordPaintInputSkipped;
    }

    public event Action<Point>? DragDeltaRequested;

    public event Action<Rectangle>? UserResizeBoundsChanged;

    public event Action<TimerOverlayRightClickRequest>? RightClickRequested;

    public void Start()
    {
        if (thread is not null)
        {
            return;
        }

        thread = new Thread(ThreadMain)
        {
            IsBackground = true,
            Name = "TimerOverlayThread"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait(TimeSpan.FromSeconds(2));
    }

    public void ApplyOverlayLayout(OverlayCompositeLayout layout)
    {
        lock (sync)
        {
            latestLayout = layout;
        }

        InvokeForm(formValue => formValue.ApplyOverlayLayout(layout));
    }

    public void ApplyRenderState(TimerOverlayRenderState renderState, TimerOverlayStateKey stateKey, bool forceRender)
    {
        lock (sync)
        {
            if (!forceRender && latestRenderStateKey.HasValue && latestRenderStateKey.Value == stateKey)
            {
                return;
            }

            latestRenderState = renderState;
            latestRenderStateKey = stateKey;
        }

        InvokeForm(formValue => formValue.ApplyRenderState(renderState, forceRender));
    }

    public void ApplyRefreshInterval(TimeSpan interval)
    {
        lock (sync)
        {
            latestRefreshInterval = interval;
        }

        InvokeForm(formValue => formValue.ApplyRefreshInterval(interval));
    }

    public void ApplyPaintSuspended(bool suspended)
    {
        lock (sync)
        {
            latestPaintSuspended = suspended;
        }

        InvokeForm(formValue => formValue.ApplyPaintSuspended(suspended));
    }

    public void ApplyTopMost(bool topMost)
    {
        lock (sync)
        {
            latestTopMost = topMost;
        }

        InvokeForm(formValue => formValue.ApplyTopMost(topMost));
    }

    public void ApplyMouseClickThrough(bool enabled)
    {
        lock (sync)
        {
            latestMouseClickThrough = enabled;
        }

        InvokeForm(formValue => formValue.ApplyMouseClickThrough(enabled));
    }

    public void RequestRender()
    {
        InvokeForm(formValue => formValue.RequestRender());
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        TimerOverlayForm? formValue;
        Thread? threadValue;
        lock (sync)
        {
            formValue = form;
            threadValue = thread;
        }

        if (formValue is not null && formValue.IsHandleCreated && !formValue.IsDisposed)
        {
            try
            {
                formValue.BeginInvoke(new Action(() => formValue.Close()));
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        if (threadValue is not null && threadValue.IsAlive)
        {
            threadValue.Join(TimeSpan.FromSeconds(2));
        }

        ready.Dispose();
    }

    private void ThreadMain()
    {
        using var overlayForm = new TimerOverlayForm(
            recordPaint,
            recordPaintTick,
            recordPaintDispatchSkipped,
            recordPaintInputSkipped);
        overlayForm.DragDeltaRequested += delta => DispatchToMain(() => DragDeltaRequested?.Invoke(delta));
        overlayForm.UserResizeBoundsChanged += bounds => DispatchToMain(() => UserResizeBoundsChanged?.Invoke(bounds));
        overlayForm.RightClickRequested += request => DispatchToMain(() => RightClickRequested?.Invoke(request));

        lock (sync)
        {
            form = overlayForm;
        }

        ApplyLatestState(overlayForm);
        ready.Set();
        Application.Run(overlayForm);

        lock (sync)
        {
            form = null;
        }
    }

    private void ApplyLatestState(TimerOverlayForm overlayForm)
    {
        OverlayCompositeLayout? layout;
        TimerOverlayRenderState? renderState;
        TimeSpan refreshInterval;
        bool topMost;
        bool mouseClickThrough;
        bool paintSuspended;
        lock (sync)
        {
            layout = latestLayout;
            renderState = latestRenderState;
            refreshInterval = latestRefreshInterval;
            topMost = latestTopMost;
            mouseClickThrough = latestMouseClickThrough;
            paintSuspended = latestPaintSuspended;
        }

        overlayForm.ApplyTopMost(topMost);
        overlayForm.ApplyMouseClickThrough(mouseClickThrough);
        overlayForm.ApplyRefreshInterval(refreshInterval);
        overlayForm.ApplyPaintSuspended(paintSuspended);
        if (layout is OverlayCompositeLayout layoutValue)
        {
            overlayForm.ApplyOverlayLayout(layoutValue);
        }

        if (renderState is not null)
        {
            overlayForm.ApplyRenderState(renderState, forceRender: true);
        }
    }

    private void InvokeForm(Action<TimerOverlayForm> action)
    {
        TimerOverlayForm? formValue;
        lock (sync)
        {
            formValue = form;
        }

        if (formValue is null || !formValue.IsHandleCreated || formValue.IsDisposed)
        {
            return;
        }

        try
        {
            formValue.BeginInvoke(action, formValue);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void DispatchToMain(Action action)
    {
        try
        {
            mainThreadDispatch(action);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
