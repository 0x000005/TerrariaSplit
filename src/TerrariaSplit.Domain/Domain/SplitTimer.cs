using System.Diagnostics;

namespace TerrariaSplit.Domain;

internal enum SplitTimerPhase
{
    NotStarted,
    Running,
    Paused
}

internal sealed class SplitTimer
{
    private long runningSinceTimestamp;
    private TimeSpan elapsedBeforePause = TimeSpan.Zero;

    public SplitTimerPhase Phase { get; private set; } = SplitTimerPhase.NotStarted;

    public TimeSpan Elapsed => Phase switch
    {
        SplitTimerPhase.Running => elapsedBeforePause + ElapsedSince(runningSinceTimestamp, Stopwatch.GetTimestamp()),
        SplitTimerPhase.Paused => elapsedBeforePause,
        _ => elapsedBeforePause
    };

    public TimeSpan ElapsedAt(long timestamp) => Phase switch
    {
        SplitTimerPhase.Running => elapsedBeforePause + ElapsedSince(runningSinceTimestamp, timestamp),
        SplitTimerPhase.Paused => elapsedBeforePause,
        _ => elapsedBeforePause
    };

    public SplitTimerState CaptureState()
    {
        return new SplitTimerState(Phase, elapsedBeforePause, runningSinceTimestamp);
    }

    public static TimeSpan ElapsedAt(SplitTimerState state, long timestamp)
    {
        return state.Phase switch
        {
            SplitTimerPhase.Running => state.ElapsedBeforePause + ElapsedSince(state.RunningSinceTimestamp, timestamp),
            SplitTimerPhase.Paused => state.ElapsedBeforePause,
            _ => state.ElapsedBeforePause
        };
    }

    public void ApplyState(SplitTimerState state)
    {
        Phase = state.Phase;
        elapsedBeforePause = state.ElapsedBeforePause < TimeSpan.Zero ? TimeSpan.Zero : state.ElapsedBeforePause;
        runningSinceTimestamp = state.RunningSinceTimestamp;
    }

    public void Start()
    {
        StartAt(Stopwatch.GetTimestamp());
    }

    public void StartAt(long timestamp)
    {
        elapsedBeforePause = TimeSpan.Zero;
        runningSinceTimestamp = timestamp;
        Phase = SplitTimerPhase.Running;
    }

    public void Reset()
    {
        elapsedBeforePause = TimeSpan.Zero;
        runningSinceTimestamp = 0;
        Phase = SplitTimerPhase.NotStarted;
    }

    public void SetPracticeElapsed(TimeSpan elapsed)
    {
        elapsedBeforePause = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        runningSinceTimestamp = Phase == SplitTimerPhase.Running ? Stopwatch.GetTimestamp() : 0;
    }

    public void Stop()
    {
        StopAt(Stopwatch.GetTimestamp());
    }

    public void StopAt(long timestamp)
    {
        if (Phase == SplitTimerPhase.Running)
        {
            elapsedBeforePause += ElapsedSince(runningSinceTimestamp, timestamp);
        }

        runningSinceTimestamp = 0;
        Phase = SplitTimerPhase.Paused;
    }

    public void TogglePause()
    {
        TogglePauseAt(Stopwatch.GetTimestamp());
    }

    public void TogglePauseAt(long timestamp)
    {
        if (Phase == SplitTimerPhase.NotStarted)
        {
            return;
        }

        if (Phase == SplitTimerPhase.Running)
        {
            elapsedBeforePause += ElapsedSince(runningSinceTimestamp, timestamp);
            Phase = SplitTimerPhase.Paused;
            return;
        }

        runningSinceTimestamp = timestamp;
        Phase = SplitTimerPhase.Running;
    }

    private static TimeSpan ElapsedSince(long startTimestamp, long endTimestamp)
    {
        long elapsedTicks = Math.Max(0, endTimestamp - startTimestamp);
        return TimeSpan.FromSeconds(elapsedTicks / (double)Stopwatch.Frequency);
    }
}

internal readonly record struct SplitTimerState(
    SplitTimerPhase Phase,
    TimeSpan ElapsedBeforePause,
    long RunningSinceTimestamp);
