using System.Diagnostics;

namespace TerrariaSplit;

internal enum TimerHotkeyAction
{
    PauseResume,
    Reset,
    MouseClickThrough,
    CreateWorld,
    PracticeWorld
}

internal readonly record struct TimerHotkeyRequest(TimerHotkeyAction Action, DateTime RequestedAtUtc);

internal sealed class TimerController
{
    private readonly SplitTimer runTimer;
    private readonly BossSplitTracker splitTracker;
    private readonly PendingMenuHotkeyScheduler pendingMenuHotkeys;
    private readonly TimeSpan pendingMenuGraceDuration;

    public TimerController(
        SplitTimer runTimer,
        BossSplitTracker splitTracker,
        PendingMenuHotkeyScheduler pendingMenuHotkeys,
        TimeSpan pendingMenuGraceDuration)
    {
        this.runTimer = runTimer;
        this.splitTracker = splitTracker;
        this.pendingMenuHotkeys = pendingMenuHotkeys;
        this.pendingMenuGraceDuration = pendingMenuGraceDuration;
    }

    public TimerControllerTickResult Tick(
        TerrariaWatchSnapshot snapshot,
        IReadOnlyCollection<TimerHotkeyRequest> hotkeyRequests)
    {
        return Tick(snapshot, Stopwatch.GetTimestamp(), hotkeyRequests);
    }

    public TimerControllerTickResult Tick(
        TerrariaWatchSnapshot snapshot,
        long observedTimestamp,
        IReadOnlyCollection<TimerHotkeyRequest> hotkeyRequests)
    {
        bool pauseSoundRequested = false;
        bool resumeSoundRequested = false;
        bool toggleMouseClickThroughRequested = false;
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
                resumeSoundRequested = previousPhase == SplitTimerPhase.Paused && runTimer.Phase == SplitTimerPhase.Running;
            }
            else if (action == TimerHotkeyAction.Reset)
            {
                QueuePendingMenuHotkeyAction(MenuHotkeyActionKind.Reset, request.RequestedAtUtc);
            }
            else if (action == TimerHotkeyAction.MouseClickThrough)
            {
                toggleMouseClickThroughRequested = true;
            }
            else if (action == TimerHotkeyAction.CreateWorld)
            {
                QueuePendingMenuHotkeyAction(MenuHotkeyActionKind.CreateWorld, request.RequestedAtUtc);
            }
            else if (action == TimerHotkeyAction.PracticeWorld)
            {
                QueuePendingMenuHotkeyAction(MenuHotkeyActionKind.PracticeWorld, request.RequestedAtUtc);
            }
        }

        if (TryConsumePendingMenuHotkeyAction(snapshot, out MenuHotkeyActionKind pendingAction))
        {
            return TimerControllerTickResult.RequestMenuAction(
                snapshot,
                pendingAction,
                pauseSoundRequested,
                resumeSoundRequested,
                toggleMouseClickThroughRequested);
        }

        if (snapshot.EnteredWorld && runTimer.Phase == SplitTimerPhase.NotStarted)
        {
            runStarted = true;
            runTimer.StartAt(observedTimestamp);
            splitTracker.OnRunStarted(snapshot);
        }

        if (runTimer.Phase == SplitTimerPhase.Running)
        {
            BossSplitRecord? split = splitTracker.Update(snapshot, runTimer.ElapsedAt(observedTimestamp));
            if (split is not null)
            {
                completedSplitIndex = splitTracker.CurrentIndex - 1;
                if (splitTracker.CurrentIndex >= splitTracker.Statuses.Count)
                {
                    runCompleted = true;
                    runTimer.StopAt(observedTimestamp);
                }
            }
        }

        return new TimerControllerTickResult(
            snapshot,
            pauseSoundRequested,
            resumeSoundRequested,
            toggleMouseClickThroughRequested,
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
            MenuHotkeyActionKind.Reset or
                MenuHotkeyActionKind.CreateWorld or
                MenuHotkeyActionKind.PracticeWorld => snapshot.IsGameMenu == true,
            _ => false
        };
    }
}

internal readonly record struct TimerControllerTickResult(
    TerrariaWatchSnapshot Snapshot,
    bool PauseSoundRequested,
    bool ResumeSoundRequested,
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
        bool resumeSoundRequested,
        bool toggleMouseClickThroughRequested)
    {
        return new TimerControllerTickResult(
            snapshot,
            pauseSoundRequested,
            resumeSoundRequested,
            toggleMouseClickThroughRequested,
            action,
            RunStarted: false,
            CompletedSplitIndex: null,
            RunCompleted: false);
    }
}
