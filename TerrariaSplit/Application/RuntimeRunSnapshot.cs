using System.Diagnostics;

namespace TerrariaSplit.Application;

internal sealed record RuntimeRunSnapshot(
    SplitTimerState TimerState,
    IReadOnlyList<SplitStatusSnapshot> Statuses,
    int CurrentSplitIndex,
    long ObservedTimestamp,
    int StatusHash)
{
    public static RuntimeRunSnapshot Empty { get; } = new(
        new SplitTimerState(SplitTimerPhase.NotStarted, TimeSpan.Zero, 0),
        [],
        0,
        0,
        0);

    public SplitTimerPhase TimerPhase => TimerState.Phase;

    public TimeSpan ElapsedAt(long timestamp)
    {
        return SplitTimer.ElapsedAt(TimerState, timestamp);
    }

    public TimeSpan ElapsedNow()
    {
        return ElapsedAt(Stopwatch.GetTimestamp());
    }

    public static RuntimeRunSnapshot FromState(
        SplitTimerState timerState,
        SplitTracker tracker,
        long observedTimestamp)
    {
        SplitStatusSnapshot[] statusCopies = tracker.Statuses
            .Select(SplitStatusSnapshot.FromStatus)
            .ToArray();
        return new RuntimeRunSnapshot(
            timerState,
            statusCopies,
            tracker.CurrentIndex,
            observedTimestamp,
            ComputeStatusHash(statusCopies));
    }

    private static int ComputeStatusHash(IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        var hash = new HashCode();
        foreach (SplitStatusSnapshot status in statuses)
        {
            hash.Add(status.Time);
            hash.Add(status.IsSkipped);
            foreach (string factKey in status.CompletedFactKeys)
            {
                hash.Add(factKey, StringComparer.OrdinalIgnoreCase);
            }

            foreach ((string factKey, TimeSpan time) in status.FactCompletionTimes ?? new Dictionary<string, TimeSpan>())
            {
                hash.Add(factKey, StringComparer.OrdinalIgnoreCase);
                hash.Add(time);
            }
        }

        return hash.ToHashCode();
    }
}

internal enum RuntimeCommandKind
{
    SetDefinitions,
    Reset,
    TogglePause,
    QueueMenuAction,
    SetPracticeSplitTime,
    SetPracticeTotalTime,
    ClearPendingMenuActions
}

internal sealed record RuntimeCommand
{
    private RuntimeCommand(RuntimeCommandKind kind)
    {
        Kind = kind;
    }

    public RuntimeCommandKind Kind { get; }

    public IReadOnlyList<SplitDefinition> Definitions { get; private init; } = [];

    public MenuActionKind MenuAction { get; private init; }

    public DateTime RequestedAtUtc { get; private init; }

    public int SplitIndex { get; private init; } = -1;

    public TimeSpan? Time { get; private init; }

    public static RuntimeCommand SetDefinitions(IReadOnlyList<SplitDefinition> definitions)
    {
        return new RuntimeCommand(RuntimeCommandKind.SetDefinitions)
        {
            Definitions = definitions.ToArray()
        };
    }

    public static RuntimeCommand Reset()
    {
        return new RuntimeCommand(RuntimeCommandKind.Reset);
    }

    public static RuntimeCommand TogglePause()
    {
        return new RuntimeCommand(RuntimeCommandKind.TogglePause);
    }

    public static RuntimeCommand QueueMenuAction(MenuActionKind action, DateTime requestedAtUtc)
    {
        return new RuntimeCommand(RuntimeCommandKind.QueueMenuAction)
        {
            MenuAction = action,
            RequestedAtUtc = requestedAtUtc
        };
    }

    public static RuntimeCommand SetPracticeSplitTime(int splitIndex, TimeSpan? time)
    {
        return new RuntimeCommand(RuntimeCommandKind.SetPracticeSplitTime)
        {
            SplitIndex = splitIndex,
            Time = time
        };
    }

    public static RuntimeCommand SetPracticeTotalTime(TimeSpan time)
    {
        return new RuntimeCommand(RuntimeCommandKind.SetPracticeTotalTime)
        {
            Time = time
        };
    }

    public static RuntimeCommand ClearPendingMenuActions()
    {
        return new RuntimeCommand(RuntimeCommandKind.ClearPendingMenuActions);
    }
}

internal enum RunEventKind
{
    RunStarted,
    PauseChanged,
    SplitCompleted,
    RunCompleted,
    MenuActionRequested,
    PracticeSplitTimeEdited,
    PracticeTotalTimeEdited
}

internal readonly record struct RunEvent(
    RunEventKind Kind,
    int SplitIndex = -1,
    MenuActionKind? MenuAction = null,
    SplitTimerPhase PreviousPhase = SplitTimerPhase.NotStarted,
    SplitTimerPhase CurrentPhase = SplitTimerPhase.NotStarted);

internal readonly record struct RuntimeProcessorTickResult(
    RuntimeRunSnapshot Snapshot,
    IReadOnlyList<RunEvent> Events);

internal readonly record struct RuntimeCommandDrainResult(
    long LatestAppliedSequence,
    IReadOnlyList<RunEvent> Events);
