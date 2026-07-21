using System.Diagnostics;

namespace TerrariaSplit.Application;

internal sealed class TimerController
{
    private readonly SplitTimer runTimer;
    private readonly SplitTracker splitTracker;
    private readonly PendingMenuActionScheduler pendingMenuActions;
    private readonly TimeSpan pendingMenuGraceDuration;

    public TimerController(
        SplitTimer runTimer,
        SplitTracker splitTracker,
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

        if (TryConsumePendingMenuAction(snapshot, out MenuActionKind pendingAction))
        {
            return [new RunEvent(RunEventKind.MenuActionRequested, MenuAction: pendingAction)];
        }

        if (snapshot.EnteredWorld && runTimer.Phase == SplitTimerPhase.NotStarted)
        {
            runStarted = true;
            runTimer.StartAt(observedTimestamp);
            // This snapshot can straddle the menu-to-world transition, so defer
            // initial fact resolution and split evaluation until the next poll.
            splitTracker.OnRunStarted(TerrariaGameFacts.Unknown);
        }

        if (runTimer.Phase == SplitTimerPhase.Running && !runStarted)
        {
            SplitRecord? split = splitTracker.Update(
                snapshot.Facts,
                snapshot.IsGameMenu,
                runTimer.ElapsedAt(observedTimestamp));
            if (split is not null)
            {
                completedSplitIndex = split.Value.Index;
                if (splitTracker.CurrentIndex >= splitTracker.Statuses.Count)
                {
                    runCompleted = true;
                    runTimer.StopAt(observedTimestamp);
                }
            }
        }

        if (!runStarted && completedSplitIndex is null && !runCompleted)
        {
            return [];
        }

        var events = new List<RunEvent>(3);
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

    public IReadOnlyList<RunEvent> CompleteNextSplitManually(long observedTimestamp)
    {
        if (runTimer.Phase == SplitTimerPhase.NotStarted)
        {
            if (splitTracker.CurrentIndex >= splitTracker.Statuses.Count)
            {
                return [];
            }

            runTimer.StartAt(observedTimestamp);
            splitTracker.OnRunStarted(TerrariaGameFacts.Unknown);
            return [new RunEvent(RunEventKind.RunStarted)];
        }

        SplitRecord? split = splitTracker.CompleteNextManually(runTimer.ElapsedAt(observedTimestamp));
        if (split is null)
        {
            return [];
        }

        bool runCompleted = splitTracker.CurrentIndex >= splitTracker.Statuses.Count;
        if (runCompleted)
        {
            runTimer.StopAt(observedTimestamp);
        }

        var events = new List<RunEvent>(2);
        events.Add(new RunEvent(RunEventKind.SplitCompleted, split.Value.Index));
        if (runCompleted)
        {
            events.Add(new RunEvent(RunEventKind.RunCompleted));
        }

        return events;
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
