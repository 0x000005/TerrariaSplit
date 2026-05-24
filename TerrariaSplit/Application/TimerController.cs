using System.Diagnostics;

namespace TerrariaSplit;

internal sealed class TimerController
{
    private readonly SplitTimer runTimer;
    private readonly BossSplitTracker splitTracker;
    private readonly PendingMenuActionScheduler pendingMenuActions;
    private readonly TimeSpan pendingMenuGraceDuration;

    public TimerController(
        SplitTimer runTimer,
        BossSplitTracker splitTracker,
        PendingMenuActionScheduler pendingMenuActions,
        TimeSpan pendingMenuGraceDuration)
    {
        this.runTimer = runTimer;
        this.splitTracker = splitTracker;
        this.pendingMenuActions = pendingMenuActions;
        this.pendingMenuGraceDuration = pendingMenuGraceDuration;
    }

    public IReadOnlyList<RunEvent> Tick(TerrariaWatchSnapshot snapshot)
    {
        return Tick(snapshot, Stopwatch.GetTimestamp());
    }

    public IReadOnlyList<RunEvent> Tick(
        TerrariaWatchSnapshot snapshot,
        long observedTimestamp)
    {
        bool runStarted = false;
        int? completedSplitIndex = null;
        bool runCompleted = false;

        var events = new List<RunEvent>(4);
        if (TryConsumePendingMenuAction(snapshot, out MenuActionKind pendingAction))
        {
            events.Add(new RunEvent(RunEventKind.MenuActionRequested, MenuAction: pendingAction));
            return events;
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

        if (runStarted)
        {
            events.Add(new RunEvent(RunEventKind.RunStarted));
        }

        if (completedSplitIndex is int completedIndex)
        {
            events.Add(new RunEvent(RunEventKind.SplitCompleted, completedIndex));
        }

        if (runCompleted)
        {
            events.Add(new RunEvent(RunEventKind.RunCompleted));
        }

        return events;
    }

    public void QueuePendingMenuAction(MenuActionKind kind, DateTime requestedAtUtc)
    {
        pendingMenuActions.Queue(kind, requestedAtUtc, pendingMenuGraceDuration);
    }

    private bool TryConsumePendingMenuAction(TerrariaWatchSnapshot snapshot, out MenuActionKind kind)
    {
        return pendingMenuActions.TryConsume(
            pendingKind => CanExecutePendingMenuAction(pendingKind, snapshot),
            out kind);
    }

    private static bool CanExecutePendingMenuAction(
        MenuActionKind kind,
        TerrariaWatchSnapshot snapshot)
    {
        return kind switch
        {
            MenuActionKind.Reset or
                MenuActionKind.CreateWorld or
                MenuActionKind.PracticeWorld => snapshot.IsGameMenu == true,
            _ => false
        };
    }
}
