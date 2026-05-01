using System.Diagnostics;

namespace TerrariaSplit;

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
        SplitTimerPhase.Running => elapsedBeforePause + ElapsedSince(runningSinceTimestamp),
        SplitTimerPhase.Paused => elapsedBeforePause,
        _ => elapsedBeforePause
    };

    public void Start()
    {
        elapsedBeforePause = TimeSpan.Zero;
        runningSinceTimestamp = Stopwatch.GetTimestamp();
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
        if (Phase == SplitTimerPhase.Running)
        {
            elapsedBeforePause += ElapsedSince(runningSinceTimestamp);
        }

        runningSinceTimestamp = 0;
        Phase = SplitTimerPhase.Paused;
    }

    public void TogglePause()
    {
        if (Phase == SplitTimerPhase.NotStarted)
        {
            return;
        }

        if (Phase == SplitTimerPhase.Running)
        {
            elapsedBeforePause += ElapsedSince(runningSinceTimestamp);
            Phase = SplitTimerPhase.Paused;
            return;
        }

        runningSinceTimestamp = Stopwatch.GetTimestamp();
        Phase = SplitTimerPhase.Running;
    }

    private static TimeSpan ElapsedSince(long timestamp)
    {
        long elapsedTicks = Stopwatch.GetTimestamp() - timestamp;
        return TimeSpan.FromSeconds(elapsedTicks / (double)Stopwatch.Frequency);
    }
}
