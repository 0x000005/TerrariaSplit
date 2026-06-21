using System.Threading;

namespace TerrariaSplit.UI;

internal sealed class RuntimeShell
{
    private readonly object watcherStateLock = new();
    private int controlTickDispatchPending;
    private int statusPaintDispatchPending;
    private int overlayPaintSuspensionCount;
    private bool controlSchedulerSuspendedForOverlayPaint;
    private TerrariaWatchSnapshot snapshot = RuntimeDebugSnapshot.Empty.WatchSnapshot;
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

    public TimeSpan ControlTickInterval { get; private set; }

    public TimeSpan StatusPaintInterval { get; private set; }

    public bool IsOverlayPaintSuspended => overlayPaintSuspensionCount > 0;

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

    public RuntimeDebugSnapshot CreateDebugSnapshot(
        RuntimePerformanceDiagnostics performance,
        SplitTimerPhase timerPhase)
    {
        lock (watcherStateLock)
        {
            return new RuntimeDebugSnapshot(snapshot, watcherDiagnostics, performance, timerPhase);
        }
    }
}

internal readonly record struct RuntimeOverlayPaintSuspension(
    bool Started,
    bool ShouldStopControlScheduler);

internal readonly record struct RuntimeOverlayPaintResume(
    bool Completed,
    bool ShouldRestartControlScheduler);
