using System.Drawing;
using System.Threading;

namespace TerrariaSplit.UI;

internal sealed class TimerOverlayWindowHost : IDisposable
{
    private readonly Action<Action> mainThreadDispatch;
    private readonly object sync = new();
    private readonly TaskCompletionSource<IntPtr> handleReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> firstFramePresented = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Thread? thread;
    private TimerOverlayForm? form;
    private IntPtr formHandle;
    private OverlayCompositeLayout? latestLayout;
    private TimerOverlayRenderState? latestRenderState;
    private TimerOverlayStateKey? latestRenderStateKey;
    private TimeSpan latestRefreshInterval = TimeSpan.FromMilliseconds(16);
    private bool latestMouseClickThrough;
    private bool latestInteractionBlocked;
    private bool latestPaintSuspended;
    private bool disposed;

    public TimerOverlayWindowHost(Action<Action> mainThreadDispatch)
    {
        this.mainThreadDispatch = mainThreadDispatch;
    }

    public event Action<Point>? DragDeltaRequested;

    public event Action? DragCompleted;

    public event Action<Rectangle>? UserResizeBoundsChanged;

    public event Action<TimerOverlayRightClickRequest>? RightClickRequested;

    public event Action<IntPtr>? Activated;

    public event Action? ModalActivationRequested;

    public event Action? FirstFrameRendered;

    public IntPtr WindowHandle
    {
        get
        {
            lock (sync)
            {
                return formHandle;
            }
        }
    }

    public Task<IntPtr> HandleReady => handleReady.Task;

    public Task FirstFramePresented => firstFramePresented.Task;

    public Task<IntPtr> StartAsync(CancellationToken cancellationToken = default)
    {
        Thread? threadToStart = null;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (thread is null)
            {
                thread = new Thread(ThreadMain)
                {
                    IsBackground = true,
                    Name = "TimerOverlayThread"
                };
                thread.SetApartmentState(ApartmentState.STA);
                threadToStart = thread;
            }
        }

        threadToStart?.Start();
        return cancellationToken.CanBeCanceled
            ? handleReady.Task.WaitAsync(cancellationToken)
            : handleReady.Task;
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

    public void ApplyInteractionBlocked(bool blocked)
    {
        lock (sync)
        {
            latestInteractionBlocked = blocked;
        }

        InvokeForm(formValue => formValue.ApplyInteractionBlocked(blocked));
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
        handleReady.TrySetCanceled();
        firstFramePresented.TrySetCanceled();
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

    }

    private void ThreadMain()
    {
        try
        {
            using var overlayForm = new TimerOverlayForm(
                IsInteractionBlocked,
                () =>
                {
                    if (firstFramePresented.TrySetResult(true))
                    {
                        StartupDiagnostics.RecordTrace("TimerFrame");
                        FirstFrameRendered?.Invoke();
                    }
                });
            overlayForm.DragDeltaRequested += delta => DispatchToMain(() => DragDeltaRequested?.Invoke(delta));
            overlayForm.DragCompleted += () => DispatchToMain(() => DragCompleted?.Invoke());
            overlayForm.UserResizeBoundsChanged += bounds => DispatchToMain(() => UserResizeBoundsChanged?.Invoke(bounds));
            overlayForm.RightClickRequested += request => DispatchToMain(() => RightClickRequested?.Invoke(request));
            overlayForm.ModalActivationRequested += () => DispatchToMain(() => ModalActivationRequested?.Invoke());
            overlayForm.Activated += (_, _) =>
            {
                IntPtr activatedHandle;
                lock (sync)
                {
                    activatedHandle = formHandle;
                }

                if (activatedHandle != IntPtr.Zero)
                {
                    DispatchToMain(() => Activated?.Invoke(activatedHandle));
                }
            };
            overlayForm.HandleCreated += (_, _) =>
            {
                IntPtr handle = overlayForm.Handle;
                lock (sync)
                {
                    formHandle = handle;
                }

                handleReady.TrySetResult(handle);
                ApplyLatestState(overlayForm);
            };
            overlayForm.HandleDestroyed += (_, _) =>
            {
                lock (sync)
                {
                    formHandle = IntPtr.Zero;
                }
            };

            lock (sync)
            {
                form = overlayForm;
            }

            _ = overlayForm.Handle;
            ApplyLatestState(overlayForm);
            System.Windows.Forms.Application.Run(overlayForm);
        }
        catch (Exception ex)
        {
            handleReady.TrySetException(ex);
            firstFramePresented.TrySetException(ex);
            FileAppLogger.Instance.Error(ex, "Timer overlay thread failed during startup.");
        }
        finally
        {
            lock (sync)
            {
                form = null;
                formHandle = IntPtr.Zero;
            }

            if (!disposed)
            {
                handleReady.TrySetCanceled();
                firstFramePresented.TrySetCanceled();
            }
        }
    }

    private void ApplyLatestState(TimerOverlayForm overlayForm)
    {
        OverlayCompositeLayout? layout;
        TimerOverlayRenderState? renderState;
        TimeSpan refreshInterval;
        bool mouseClickThrough;
        bool interactionBlocked;
        bool paintSuspended;
        lock (sync)
        {
            layout = latestLayout;
            renderState = latestRenderState;
            refreshInterval = latestRefreshInterval;
            mouseClickThrough = latestMouseClickThrough;
            interactionBlocked = latestInteractionBlocked;
            paintSuspended = latestPaintSuspended;
        }

        overlayForm.ApplyInteractionBlocked(interactionBlocked);
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

    private bool IsInteractionBlocked()
    {
        lock (sync)
        {
            return latestInteractionBlocked;
        }
    }
}
