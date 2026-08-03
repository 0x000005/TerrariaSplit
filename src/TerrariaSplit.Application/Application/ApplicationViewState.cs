using System.Diagnostics;

namespace TerrariaSplit.Application;

public sealed record ApplicationViewState(
    AppSettings Settings,
    RuntimeRunSnapshot RuntimeSnapshot)
{
    public IReadOnlyList<SplitStatusSnapshot> DisplayStatuses => RuntimeSnapshot.Statuses;

    public int CurrentSplitIndex => RuntimeSnapshot.CurrentSplitIndex;

    public SplitTimerState TimerState => RuntimeSnapshot.TimerState;

    public int StatusHash => RuntimeSnapshot.StatusHash;

    public SplitTimerPhase TimerPhase => RuntimeSnapshot.TimerPhase;

    public TimeSpan ElapsedAt(long timestamp)
    {
        return RuntimeSnapshot.ElapsedAt(timestamp);
    }

    public TimeSpan ElapsedNow()
    {
        return ElapsedAt(Stopwatch.GetTimestamp());
    }

    public static ApplicationViewState FromSettings(AppSettings settings)
    {
        return FromDefinitions(settings, SplitCatalog.Build(settings));
    }

    public static ApplicationViewState FromDefinitions(
        AppSettings settings,
        IReadOnlyList<SplitDefinition> definitions)
    {
        return new ApplicationViewState(
            settings,
            RuntimeRunSnapshot.FromDefinitions(definitions));
    }

    public static ApplicationViewState FromRuntimeSnapshot(
        AppSettings settings,
        RuntimeRunSnapshot snapshot)
    {
        return new ApplicationViewState(settings, snapshot);
    }

    public ApplicationViewState WithSettings(AppSettings nextSettings)
    {
        return this with { Settings = nextSettings };
    }
}
