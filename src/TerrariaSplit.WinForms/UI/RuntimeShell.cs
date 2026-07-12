using System.Threading;

namespace TerrariaSplit.UI;

internal sealed class RuntimeShell : IDisposable
{
    private readonly object watcherStateLock = new();
    private int controlTickDispatchPending;
    private int statusPaintDispatchPending;
    private int overlayPaintSuspensionCount;
    private bool controlSchedulerSuspendedForOverlayPaint;
    private TerrariaMonitorCoordinator? monitorCoordinator;
    private HighPrecisionScheduler? controlScheduler;
    private HighPrecisionScheduler? statusPaintScheduler;
    private Action dispatchedControlTick = static () => { };
    private Action dispatchedStatusPaintTick = static () => { };
    private TerrariaWatchSnapshot snapshot = new(
        false,
        null,
        false,
        null,
        TerrariaGameFacts.Unknown,
        TerrariaWorldGenerationState.Unknown,
        false,
        "waiting for Terraria.exe");
    private TerrariaWatcherDiagnostics watcherDiagnostics = TerrariaWatcherDiagnosticsDefaults.Empty;

    public RuntimeShell(TimeSpan controlTickInterval, TimeSpan statusPaintInterval)
    {
        ControlTickInterval = controlTickInterval;
        StatusPaintInterval = statusPaintInterval;
    }

    public TerrariaWatchSnapshot CurrentSnapshot
    {
        get
        {
            lock (watcherStateLock)
            {
                return snapshot;
            }
        }
    }

    public TerrariaMonitorCoordinator MonitorCoordinator =>
        monitorCoordinator ?? throw new InvalidOperationException("Terraria monitor coordinator has not been attached.");

    public HighPrecisionScheduler ControlScheduler =>
        controlScheduler ?? throw new InvalidOperationException("Control scheduler has not been attached.");

    public HighPrecisionScheduler StatusPaintScheduler =>
        statusPaintScheduler ?? throw new InvalidOperationException("Status paint scheduler has not been attached.");

    public bool IsRuntimeAttached =>
        monitorCoordinator is not null &&
        controlScheduler is not null &&
        statusPaintScheduler is not null;

    public Action DispatchedControlTick => dispatchedControlTick;

    public Action DispatchedStatusPaintTick => dispatchedStatusPaintTick;

    public TimeSpan ControlTickInterval { get; private set; }

    public TimeSpan StatusPaintInterval { get; private set; }

    public bool IsOverlayPaintSuspended => overlayPaintSuspensionCount > 0;

    public void AttachDispatchActions(Action dispatchedControlTick, Action dispatchedStatusPaintTick)
    {
        this.dispatchedControlTick = dispatchedControlTick;
        this.dispatchedStatusPaintTick = dispatchedStatusPaintTick;
    }

    public void AttachRuntimeComponents(
        TerrariaMonitorCoordinator monitorCoordinator,
        HighPrecisionScheduler controlScheduler,
        HighPrecisionScheduler statusPaintScheduler)
    {
        this.monitorCoordinator = monitorCoordinator;
        this.controlScheduler = controlScheduler;
        this.statusPaintScheduler = statusPaintScheduler;
    }

    public bool TryMarkControlTickDispatchPending()
    {
        return Interlocked.Exchange(ref controlTickDispatchPending, 1) == 0;
    }

    public void ClearControlTickDispatchPending()
    {
        Interlocked.Exchange(ref controlTickDispatchPending, 0);
    }

    public bool TryMarkStatusPaintDispatchPending()
    {
        return Interlocked.Exchange(ref statusPaintDispatchPending, 1) == 0;
    }

    public void ClearStatusPaintDispatchPending()
    {
        Interlocked.Exchange(ref statusPaintDispatchPending, 0);
    }

    public bool UpdateControlTickInterval(TimeSpan interval)
    {
        if (ControlTickInterval == interval)
        {
            return false;
        }

        ControlTickInterval = interval;
        return true;
    }

    public bool UpdateStatusPaintInterval(TimeSpan interval)
    {
        if (StatusPaintInterval == interval)
        {
            return false;
        }

        StatusPaintInterval = interval;
        return true;
    }

    public RuntimeOverlayPaintSuspension BeginOverlayPaintSuspension(bool controlSchedulerIsRunning)
    {
        bool firstSuspension = overlayPaintSuspensionCount == 0;
        overlayPaintSuspensionCount++;
        if (!firstSuspension)
        {
            return new RuntimeOverlayPaintSuspension(false, false);
        }

        controlSchedulerSuspendedForOverlayPaint = controlSchedulerIsRunning;
        return new RuntimeOverlayPaintSuspension(true, controlSchedulerIsRunning);
    }

    public RuntimeOverlayPaintResume EndOverlayPaintSuspension(bool canRestartControlScheduler)
    {
        if (overlayPaintSuspensionCount <= 0)
        {
            return new RuntimeOverlayPaintResume(false, false);
        }

        overlayPaintSuspensionCount--;
        if (overlayPaintSuspensionCount > 0)
        {
            return new RuntimeOverlayPaintResume(false, false);
        }

        bool shouldRestartControlScheduler =
            controlSchedulerSuspendedForOverlayPaint &&
            canRestartControlScheduler;
        controlSchedulerSuspendedForOverlayPaint = false;
        return new RuntimeOverlayPaintResume(true, shouldRestartControlScheduler);
    }

    public void ApplyWatcherNotification(WatcherPollNotification notification)
    {
        lock (watcherStateLock)
        {
            snapshot = notification.Snapshot;
            watcherDiagnostics = notification.Diagnostics;
        }
    }

    public TerrariaWatcherDiagnostics CurrentWatcherDiagnostics
    {
        get
        {
            lock (watcherStateLock)
            {
                return watcherDiagnostics;
            }
        }
    }

    public void Dispose()
    {
        controlScheduler?.Dispose();
        statusPaintScheduler?.Dispose();
        monitorCoordinator?.Dispose();
    }
}

internal readonly record struct RuntimeOverlayPaintSuspension(
    bool Started,
    bool ShouldStopControlScheduler);

internal readonly record struct RuntimeOverlayPaintResume(
    bool Completed,
    bool ShouldRestartControlScheduler);
