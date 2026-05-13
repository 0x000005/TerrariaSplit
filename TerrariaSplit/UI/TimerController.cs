namespace TerrariaSplit;

internal enum TimerHotkeyAction
{
    PauseResume,
    Reset,
    MouseClickThrough,
    CreateWorld
}

internal readonly record struct TimerHotkeyRequest(TimerHotkeyAction Action, DateTime RequestedAtUtc);

internal sealed class TimerController
{
    private readonly SplitTimer runTimer;
    private readonly BossSplitTracker splitTracker;
    private readonly TerrariaWorldWatcher watcher;
    private readonly PendingMenuHotkeyScheduler pendingMenuHotkeys;
    private readonly TimeSpan pendingMenuGraceDuration;

    public TimerController(
        SplitTimer runTimer,
        BossSplitTracker splitTracker,
        TerrariaWorldWatcher watcher,
        PendingMenuHotkeyScheduler pendingMenuHotkeys,
        TimeSpan pendingMenuGraceDuration)
    {
        this.runTimer = runTimer;
        this.splitTracker = splitTracker;
        this.watcher = watcher;
        this.pendingMenuHotkeys = pendingMenuHotkeys;
        this.pendingMenuGraceDuration = pendingMenuGraceDuration;
    }

    public TimerControllerTickResult Tick(IReadOnlyCollection<TimerHotkeyRequest> hotkeyRequests)
    {
        TerrariaWatchSnapshot snapshot = watcher.Poll();
        bool pauseSoundRequested = false;
        bool toggleMouseClickThroughRequested = false;
        DateTime? createWorldRequestedAtUtc = null;
        bool runStarted = false;
        int? completedSplitIndex = null;
        bool runCompleted = false;

        foreach (TimerHotkeyRequest request in hotkeyRequests)
        {
            TimerHotkeyAction action = request.Action;
            if (action == TimerHotkeyAction.PauseResume)
            {
                SplitTimerPhase previousPhase = runTimer.Phase;
                runTimer.TogglePause();
                pauseSoundRequested = previousPhase == SplitTimerPhase.Running && runTimer.Phase == SplitTimerPhase.Paused;
            }
            else if (action == TimerHotkeyAction.Reset)
            {
                if (CanReset(snapshot))
                {
                    return TimerControllerTickResult.RequestMenuAction(
                        snapshot,
                        MenuHotkeyActionKind.Reset,
                        pauseSoundRequested,
                        toggleMouseClickThroughRequested);
                }

                QueuePendingMenuHotkeyAction(MenuHotkeyActionKind.Reset, request.RequestedAtUtc);
            }
            else if (action == TimerHotkeyAction.MouseClickThrough)
            {
                toggleMouseClickThroughRequested = true;
            }
            else if (action == TimerHotkeyAction.CreateWorld)
            {
                createWorldRequestedAtUtc = request.RequestedAtUtc;
            }
        }

        if (TryConsumePendingMenuHotkeyAction(snapshot, out MenuHotkeyActionKind pendingAction))
        {
            return TimerControllerTickResult.RequestMenuAction(
                snapshot,
                pendingAction,
                pauseSoundRequested,
                toggleMouseClickThroughRequested,
                createWorldRequestedAtUtc);
        }

        if (snapshot.EnteredWorld && runTimer.Phase == SplitTimerPhase.NotStarted)
        {
            runStarted = true;
            runTimer.Start();
            splitTracker.OnRunStarted(snapshot);
        }

        if (runTimer.Phase == SplitTimerPhase.Running)
        {
            BossSplitRecord? split = splitTracker.Update(snapshot, runTimer.Elapsed);
            if (split is not null)
            {
                completedSplitIndex = splitTracker.CurrentIndex - 1;
                if (splitTracker.CurrentIndex >= splitTracker.Statuses.Count)
                {
                    runCompleted = true;
                    runTimer.Stop();
                }
            }
        }

        return new TimerControllerTickResult(
            snapshot,
            pauseSoundRequested,
            toggleMouseClickThroughRequested,
            createWorldRequestedAtUtc,
            RequestedMenuAction: null,
            runStarted,
            completedSplitIndex,
            runCompleted);
    }

    private void QueuePendingMenuHotkeyAction(MenuHotkeyActionKind kind, DateTime requestedAtUtc)
    {
        pendingMenuHotkeys.Queue(kind, requestedAtUtc, pendingMenuGraceDuration);
    }

    private bool TryConsumePendingMenuHotkeyAction(TerrariaWatchSnapshot snapshot, out MenuHotkeyActionKind kind)
    {
        return pendingMenuHotkeys.TryConsume(
            pendingKind => CanExecutePendingMenuHotkeyAction(pendingKind, snapshot),
            out kind);
    }

    private static bool CanExecutePendingMenuHotkeyAction(
        MenuHotkeyActionKind kind,
        TerrariaWatchSnapshot snapshot)
    {
        return kind switch
        {
            MenuHotkeyActionKind.Reset => snapshot.IsGameMenu == true,
            _ => false
        };
    }

    private static bool CanReset(TerrariaWatchSnapshot snapshot)
    {
        return snapshot.IsGameMenu != false;
    }
}

internal readonly record struct TimerControllerTickResult(
    TerrariaWatchSnapshot Snapshot,
    bool PauseSoundRequested,
    bool ToggleMouseClickThroughRequested,
    DateTime? CreateWorldRequestedAtUtc,
    MenuHotkeyActionKind? RequestedMenuAction,
    bool RunStarted,
    int? CompletedSplitIndex,
    bool RunCompleted)
{
    public static TimerControllerTickResult RequestMenuAction(
        TerrariaWatchSnapshot snapshot,
        MenuHotkeyActionKind action,
        bool pauseSoundRequested,
        bool toggleMouseClickThroughRequested,
        DateTime? createWorldRequestedAtUtc = null)
    {
        return new TimerControllerTickResult(
            snapshot,
            pauseSoundRequested,
            toggleMouseClickThroughRequested,
            createWorldRequestedAtUtc,
            action,
            RunStarted: false,
            CompletedSplitIndex: null,
            RunCompleted: false);
    }
}
