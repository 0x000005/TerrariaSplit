namespace TerrariaSplit;

internal sealed class WatcherRuntimeProcessor
{
    private readonly SplitTimer timer = new();
    private readonly BossSplitTracker splitTracker = new();
    private readonly PendingMenuActionScheduler pendingMenuActions = new();
    private readonly TimerController timerController;

    public WatcherRuntimeProcessor(TimeSpan pendingMenuGraceDuration)
    {
        timerController = new TimerController(
            timer,
            splitTracker,
            pendingMenuActions,
            pendingMenuGraceDuration);
    }

    public void SetDefinitions(IReadOnlyList<BossSplitDefinition> definitions)
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
                    timer.SetPracticeElapsed(time);
                    splitTracker.ClampCompletedTimes(time);
                    return [new RunEvent(RunEventKind.PracticeTotalTimeEdited)];
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
        IReadOnlyList<RunEvent> tickEvents = timerController.Tick(snapshot, observedTimestamp);
        List<RunEvent> events = new(commandEvents.Count + tickEvents.Count);
        events.AddRange(commandEvents);
        events.AddRange(tickEvents);
        return new RuntimeProcessorTickResult(CaptureSnapshot(observedTimestamp), events);
    }

    public RuntimeRunSnapshot CaptureSnapshot(long observedTimestamp)
    {
        return RuntimeRunSnapshot.FromState(timer.CaptureState(), splitTracker, observedTimestamp);
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
