namespace TerrariaSplit;

internal sealed class WatcherRuntimeProcessor
{
    private readonly SplitTimer timer = new();
    private readonly BossSplitTracker splitTracker = new();
    private readonly PendingMenuHotkeyScheduler pendingMenuHotkeys = new();
    private readonly TimerController timerController;

    public WatcherRuntimeProcessor(TimeSpan pendingMenuGraceDuration)
    {
        timerController = new TimerController(
            timer,
            splitTracker,
            pendingMenuHotkeys,
            pendingMenuGraceDuration);
    }

    public void SetDefinitions(IReadOnlyList<BossSplitDefinition> definitions)
    {
        splitTracker.SetDefinitions(definitions);
        timer.Reset();
        pendingMenuHotkeys.Clear();
    }

    public void Reset()
    {
        timer.Reset();
        splitTracker.Reset();
        pendingMenuHotkeys.Clear();
    }

    public void ClearPendingHotkeys()
    {
        pendingMenuHotkeys.Clear();
    }

    public void ReplaceState(SplitTimerState timerState, BossSplitTrackerState trackerState)
    {
        timer.ApplyState(timerState);
        splitTracker.ApplyState(trackerState);
    }

    public TimerControllerTickResult Tick(
        TerrariaWatchSnapshot snapshot,
        long observedTimestamp,
        IReadOnlyCollection<TimerHotkeyRequest> hotkeyRequests)
    {
        return timerController.Tick(snapshot, observedTimestamp, hotkeyRequests);
    }

    public ProcessedRunState CaptureState()
    {
        return new ProcessedRunState(timer.CaptureState(), splitTracker.CaptureState());
    }
}

internal readonly record struct ProcessedRunState(
    SplitTimerState TimerState,
    BossSplitTrackerState SplitTrackerState);
