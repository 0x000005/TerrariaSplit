namespace TerrariaSplit;

internal enum TimerHotkeyAction
{
    PauseResume,
    Reset,
    MouseClickThrough,
    CreateWorld
}

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

    public TimerControllerTickResult Tick(
        AppSettings settings,
        IReadOnlyCollection<TimerHotkeyAction> hotkeyActions,
        Func<TerrariaWatchSnapshot, bool> canStartCreateWorldAutomation)
    {
        TerrariaWatchSnapshot snapshot = watcher.Poll();
        bool pauseSoundRequested = false;
        bool toggleMouseClickThroughRequested = false;
        bool runStarted = false;
        int? completedSplitIndex = null;
        bool runCompleted = false;

        foreach (TimerHotkeyAction action in hotkeyActions)
        {
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

                QueuePendingMenuHotkeyAction(MenuHotkeyActionKind.Reset);
            }
            else if (action == TimerHotkeyAction.MouseClickThrough)
            {
                toggleMouseClickThroughRequested = true;
            }
            else if (action == TimerHotkeyAction.CreateWorld)
            {
                if (canStartCreateWorldAutomation(snapshot))
                {
                    return TimerControllerTickResult.RequestMenuAction(
                        snapshot,
                        MenuHotkeyActionKind.CreateWorld,
                        pauseSoundRequested,
                        toggleMouseClickThroughRequested);
                }

                QueuePendingMenuHotkeyAction(MenuHotkeyActionKind.CreateWorld);
            }
        }

        if (TryConsumePendingMenuHotkeyAction(snapshot, canStartCreateWorldAutomation, out MenuHotkeyActionKind pendingAction))
        {
            return TimerControllerTickResult.RequestMenuAction(
                snapshot,
                pendingAction,
                pauseSoundRequested,
                toggleMouseClickThroughRequested);
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
            RequestedMenuAction: null,
            runStarted,
            completedSplitIndex,
            runCompleted);
    }

    private void QueuePendingMenuHotkeyAction(MenuHotkeyActionKind kind)
    {
        pendingMenuHotkeys.Queue(kind, pendingMenuGraceDuration);
    }

    private bool TryConsumePendingMenuHotkeyAction(
        TerrariaWatchSnapshot snapshot,
        Func<TerrariaWatchSnapshot, bool> canStartCreateWorldAutomation,
        out MenuHotkeyActionKind kind)
    {
        return pendingMenuHotkeys.TryConsume(
            pendingKind => CanExecutePendingMenuHotkeyAction(pendingKind, snapshot, canStartCreateWorldAutomation),
            out kind);
    }

    private static bool CanExecutePendingMenuHotkeyAction(
        MenuHotkeyActionKind kind,
        TerrariaWatchSnapshot snapshot,
        Func<TerrariaWatchSnapshot, bool> canStartCreateWorldAutomation)
    {
        return kind switch
        {
            MenuHotkeyActionKind.Reset => snapshot.IsGameMenu == true,
            MenuHotkeyActionKind.CreateWorld => canStartCreateWorldAutomation(snapshot),
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
    MenuHotkeyActionKind? RequestedMenuAction,
    bool RunStarted,
    int? CompletedSplitIndex,
    bool RunCompleted)
{
    public static TimerControllerTickResult RequestMenuAction(
        TerrariaWatchSnapshot snapshot,
        MenuHotkeyActionKind action,
        bool pauseSoundRequested,
        bool toggleMouseClickThroughRequested)
    {
        return new TimerControllerTickResult(
            snapshot,
            pauseSoundRequested,
            toggleMouseClickThroughRequested,
            action,
            RunStarted: false,
            CompletedSplitIndex: null,
            RunCompleted: false);
    }
}
