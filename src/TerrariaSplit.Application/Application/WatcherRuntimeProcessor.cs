namespace TerrariaSplit.Application;

internal sealed class WatcherRuntimeProcessor
{
    private readonly SplitTimer timer = new();
    private readonly SplitTracker splitTracker = new();
    private readonly PendingMenuActionScheduler pendingMenuActions = new();
    private readonly TimerController timerController;
    private RuntimeRunSnapshot? lastCapturedSnapshot;

    public WatcherRuntimeProcessor(TimeSpan pendingMenuGraceDuration)
    {
        timerController = new TimerController(
            timer,
            splitTracker,
            pendingMenuActions,
            pendingMenuGraceDuration);
    }

    public void SetDefinitions(IReadOnlyList<SplitDefinition> definitions)
    {
        splitTracker.SetDefinitions(definitions);
        timer.Reset();
        pendingMenuActions.Clear();
    }

    public void Reset()
    {
        timer.Reset();
        splitTracker.Reset();
        pendingMenuActions.Clear();
    }

    public void ClearPendingMenuActions()
    {
        pendingMenuActions.Clear();
    }

    public IReadOnlyList<RunEvent> ApplyCommand(RuntimeCommand command, long observedTimestamp)
    {
        switch (command.Kind)
        {
            case RuntimeCommandKind.SetDefinitions:
                SetDefinitions(command.Definitions);
                return [];
            case RuntimeCommandKind.Reset:
                Reset();
                return [];
            case RuntimeCommandKind.TogglePause:
                return ApplyTogglePauseCommand(observedTimestamp);
            case RuntimeCommandKind.QueueMenuAction:
                timerController.QueuePendingMenuAction(command.MenuAction, command.RequestedAtUtc);
                return [];
            case RuntimeCommandKind.SetPracticeSplitTime:
                splitTracker.SetPracticeTime(command.SplitIndex, command.Time);
                return [new RunEvent(RunEventKind.PracticeSplitTimeEdited, command.SplitIndex)];
            case RuntimeCommandKind.SetPracticeTotalTime:
                if (command.Time is TimeSpan time)
                {
                    timer.SetPracticeElapsed(time, observedTimestamp);
                    splitTracker.ClampCompletedTimes(time);
                    return [new RunEvent(RunEventKind.PracticeTotalTimeEdited)];
                }

                return [];
            case RuntimeCommandKind.CompleteNextSplitManually:
                return timerController.CompleteNextSplitManually(observedTimestamp);
            case RuntimeCommandKind.AddElapsedPenalty:
                if (command.Time is TimeSpan penalty)
                {
                    timer.AddElapsedPenalty(penalty);
                }

                return [];
            case RuntimeCommandKind.ClearPendingMenuActions:
                ClearPendingMenuActions();
                return [];
            default:
                return [];
        }
    }

    public RuntimeProcessorTickResult Tick(
        TerrariaWatchSnapshot snapshot,
        long observedTimestamp,
        IReadOnlyList<RunEvent> commandEvents)
    {
        IReadOnlyList<RunEvent> tickEvents = commandEvents.Any(static item => item.Kind == RunEventKind.SplitCompleted)
            ? []
            : timerController.Tick(snapshot, observedTimestamp);
        IReadOnlyList<RunEvent> events;
        if (commandEvents.Count == 0)
        {
            events = tickEvents;
        }
        else if (tickEvents.Count == 0)
        {
            events = commandEvents;
        }
        else
        {
            var merged = new List<RunEvent>(commandEvents.Count + tickEvents.Count);
            merged.AddRange(commandEvents);
            merged.AddRange(tickEvents);
            events = merged;
        }

        return new RuntimeProcessorTickResult(CaptureSnapshot(observedTimestamp), events);
    }

    public RuntimeRunSnapshot CaptureSnapshot(long observedTimestamp)
    {
        // The watcher loop captures at poll rate while runs sit unchanged for
        // most polls, so reuse the previous snapshot instance when the timer
        // state and every tracker status still match it. The comparison is
        // against live state, so no mutation-side invalidation is needed.
        SplitTimerState timerState = timer.CaptureState();
        RuntimeRunSnapshot? cached = lastCapturedSnapshot;
        if (cached is not null &&
            cached.TimerState == timerState &&
            SnapshotMatchesTracker(cached))
        {
            return cached;
        }

        RuntimeRunSnapshot snapshot = RuntimeRunSnapshot.FromState(timerState, splitTracker, observedTimestamp);
        lastCapturedSnapshot = snapshot;
        return snapshot;
    }

    private bool SnapshotMatchesTracker(RuntimeRunSnapshot snapshot)
    {
        if (snapshot.CurrentSplitIndex != splitTracker.CurrentIndex)
        {
            return false;
        }

        IReadOnlyList<SplitStatus> statuses = splitTracker.Statuses;
        IReadOnlyList<SplitStatusSnapshot> copies = snapshot.Statuses;
        if (copies.Count != statuses.Count)
        {
            return false;
        }

        for (int i = 0; i < statuses.Count; i++)
        {
            SplitStatus status = statuses[i];
            SplitStatusSnapshot copy = copies[i];
            if (!ReferenceEquals(copy.Definition, status.Definition) ||
                copy.Time != status.Time ||
                copy.IsSkipped != status.IsSkipped ||
                copy.IsManuallyCompleted != status.IsManuallyCompleted ||
                !copy.CompletedFactKeys.SequenceEqual(status.CompletedFactKeys, StringComparer.OrdinalIgnoreCase) ||
                !FactCompletionTimesMatch(copy.FactCompletionTimes, status.FactCompletionTimes))
            {
                return false;
            }
        }

        return true;
    }

    private static bool FactCompletionTimesMatch(
        IReadOnlyDictionary<string, TimeSpan>? copy,
        IReadOnlyDictionary<string, TimeSpan> status)
    {
        if ((copy?.Count ?? 0) != status.Count)
        {
            return false;
        }

        if (copy is null)
        {
            return status.Count == 0;
        }

        foreach ((string factKey, TimeSpan time) in status)
        {
            if (!copy.TryGetValue(factKey, out TimeSpan copyTime) || copyTime != time)
            {
                return false;
            }
        }

        return true;
    }

    private IReadOnlyList<RunEvent> ApplyTogglePauseCommand(long observedTimestamp)
    {
        SplitTimerPhase previousPhase = timer.Phase;
        timer.TogglePauseAt(observedTimestamp);
        if (previousPhase == timer.Phase)
        {
            return [];
        }

        return [new RunEvent(
            RunEventKind.PauseChanged,
            PreviousPhase: previousPhase,
            CurrentPhase: timer.Phase)];
    }

}
