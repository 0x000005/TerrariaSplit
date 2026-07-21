using System.Diagnostics;

namespace TerrariaSplit.Application;

public sealed record ApplicationViewState(
    AppSettings Settings,
    RuntimeRunSnapshot RuntimeSnapshot,
    IReadOnlyList<SplitStatusSnapshot> DisplayStatuses,
    int CurrentSplitIndex,
    SplitTimerState TimerState,
    int StatusHash,
    bool HasRuntimeSnapshot)
{
    public SplitTimerPhase TimerPhase => TimerState.Phase;

    public TimeSpan ElapsedAt(long timestamp)
    {
        return SplitTimer.ElapsedAt(TimerState, timestamp);
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
        SplitStatusSnapshot[] statuses = definitions
            .Select(SplitStatusSnapshot.FromDefinition)
            .ToArray();
        return new ApplicationViewState(
            settings,
            RuntimeRunSnapshot.Empty,
            statuses,
            statuses.Length == 0 ? 0 : 0,
            new SplitTimerState(SplitTimerPhase.NotStarted, TimeSpan.Zero, 0),
            ComputeStatusHash(statuses),
            HasRuntimeSnapshot: false);
    }

    public static ApplicationViewState FromRuntimeSnapshot(
        AppSettings settings,
        RuntimeRunSnapshot snapshot)
    {
        return new ApplicationViewState(
            settings,
            snapshot,
            snapshot.Statuses,
            snapshot.CurrentSplitIndex,
            snapshot.TimerState,
            snapshot.StatusHash,
            HasRuntimeSnapshot: true);
    }

    public ApplicationViewState WithSettings(AppSettings nextSettings)
    {
        return this with { Settings = nextSettings };
    }

    private static int ComputeStatusHash(IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        var hash = new HashCode();
        foreach (SplitStatusSnapshot status in statuses)
        {
            hash.Add(status.Time);
            hash.Add(status.IsSkipped);
            hash.Add(status.IsManuallyCompleted);
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
