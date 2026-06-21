namespace TerrariaSplit.UI;

internal sealed class RuntimeShell
{
    private readonly object watcherStateLock = new();
    private TerrariaWatchSnapshot snapshot = RuntimeDebugSnapshot.Empty.WatchSnapshot;
    private TerrariaWatcherDiagnostics watcherDiagnostics = TerrariaWatcherDiagnosticsDefaults.Empty;

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
