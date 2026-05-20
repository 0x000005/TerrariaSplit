using System.Diagnostics;

namespace TerrariaSplit;

internal readonly record struct RuntimePerformanceDiagnostics(
    int ControlTickCount,
    double LastControlTickMilliseconds,
    double AverageControlTickMilliseconds,
    double MaxControlTickMilliseconds,
    DateTime? LastControlTickUtc,
    int WatcherPollCount,
    double LastWatcherPollMilliseconds,
    double AverageWatcherPollMilliseconds,
    double MaxWatcherPollMilliseconds,
    DateTime? LastWatcherPollUtc,
    int StatusPaintCount,
    double LastStatusPaintMilliseconds,
    double AverageStatusPaintMilliseconds,
    double MaxStatusPaintMilliseconds,
    DateTime? LastStatusPaintUtc,
    int TimerOverlayPaintCount,
    double LastTimerOverlayPaintMilliseconds,
    double AverageTimerOverlayPaintMilliseconds,
    double MaxTimerOverlayPaintMilliseconds,
    DateTime? LastTimerOverlayPaintUtc,
    double ControlTickIntervalMilliseconds,
    double StatusPaintIntervalMilliseconds,
    double TimerOverlayPaintIntervalMilliseconds,
    double WatcherPollIntervalMilliseconds,
    double ProcessLookupIntervalMilliseconds,
    double ActualControlTickIntervalMilliseconds,
    double ActualWatcherPollIntervalMilliseconds,
    double ActualStatusPaintIntervalMilliseconds,
    double ActualTimerOverlayPaintIntervalMilliseconds,
    int StatusPaintTickCount,
    int TimerOverlayPaintTickCount,
    double ActualStatusPaintTickIntervalMilliseconds,
    double ActualTimerOverlayPaintTickIntervalMilliseconds,
    double MaxStatusPaintTickIntervalMilliseconds,
    double MaxTimerOverlayPaintTickIntervalMilliseconds,
    double AverageStatusPaintTickDelayMilliseconds,
    double AverageTimerOverlayPaintTickDelayMilliseconds,
    double MaxStatusPaintTickDelayMilliseconds,
    double MaxTimerOverlayPaintTickDelayMilliseconds,
    int StatusPaintDispatchSkipCount,
    int TimerOverlayPaintDispatchSkipCount,
    int TimerOverlayPaintInputSkipCount,
    double MaxControlTickIntervalMilliseconds,
    double MaxWatcherPollIntervalMilliseconds,
    double MaxStatusPaintIntervalMilliseconds,
    double MaxTimerOverlayPaintIntervalMilliseconds)
{
    public static RuntimePerformanceDiagnostics Empty => new(
        ControlTickCount: 0,
        LastControlTickMilliseconds: 0,
        AverageControlTickMilliseconds: 0,
        MaxControlTickMilliseconds: 0,
        LastControlTickUtc: null,
        WatcherPollCount: 0,
        LastWatcherPollMilliseconds: 0,
        AverageWatcherPollMilliseconds: 0,
        MaxWatcherPollMilliseconds: 0,
        LastWatcherPollUtc: null,
        StatusPaintCount: 0,
        LastStatusPaintMilliseconds: 0,
        AverageStatusPaintMilliseconds: 0,
        MaxStatusPaintMilliseconds: 0,
        LastStatusPaintUtc: null,
        TimerOverlayPaintCount: 0,
        LastTimerOverlayPaintMilliseconds: 0,
        AverageTimerOverlayPaintMilliseconds: 0,
        MaxTimerOverlayPaintMilliseconds: 0,
        LastTimerOverlayPaintUtc: null,
        ControlTickIntervalMilliseconds: 0,
        StatusPaintIntervalMilliseconds: 0,
        TimerOverlayPaintIntervalMilliseconds: 0,
        WatcherPollIntervalMilliseconds: 0,
        ProcessLookupIntervalMilliseconds: 0,
        ActualControlTickIntervalMilliseconds: 0,
        ActualWatcherPollIntervalMilliseconds: 0,
        ActualStatusPaintIntervalMilliseconds: 0,
        ActualTimerOverlayPaintIntervalMilliseconds: 0,
        StatusPaintTickCount: 0,
        TimerOverlayPaintTickCount: 0,
        ActualStatusPaintTickIntervalMilliseconds: 0,
        ActualTimerOverlayPaintTickIntervalMilliseconds: 0,
        MaxStatusPaintTickIntervalMilliseconds: 0,
        MaxTimerOverlayPaintTickIntervalMilliseconds: 0,
        AverageStatusPaintTickDelayMilliseconds: 0,
        AverageTimerOverlayPaintTickDelayMilliseconds: 0,
        MaxStatusPaintTickDelayMilliseconds: 0,
        MaxTimerOverlayPaintTickDelayMilliseconds: 0,
        StatusPaintDispatchSkipCount: 0,
        TimerOverlayPaintDispatchSkipCount: 0,
        TimerOverlayPaintInputSkipCount: 0,
        MaxControlTickIntervalMilliseconds: 0,
        MaxWatcherPollIntervalMilliseconds: 0,
        MaxStatusPaintIntervalMilliseconds: 0,
        MaxTimerOverlayPaintIntervalMilliseconds: 0);
}

internal sealed class RuntimePerformanceTracker
{
    private readonly object sync = new();
    private readonly RollingPerformanceCounter controlTicks = new();
    private readonly RollingPerformanceCounter watcherPolls = new();
    private readonly RollingPerformanceCounter statusPaints = new();
    private readonly RollingPerformanceCounter timerOverlayPaints = new();
    private readonly RollingPerformanceCounter controlTickIntervals = new();
    private readonly RollingPerformanceCounter watcherPollIntervals = new();
    private readonly RollingPerformanceCounter statusPaintIntervals = new();
    private readonly RollingPerformanceCounter timerOverlayPaintIntervals = new();
    private readonly RollingPerformanceCounter statusPaintTickIntervals = new();
    private readonly RollingPerformanceCounter timerOverlayPaintTickIntervals = new();
    private readonly RollingPerformanceCounter statusPaintTickDelays = new();
    private readonly RollingPerformanceCounter timerOverlayPaintTickDelays = new();
    private DateTime? lastControlTickUtc;
    private DateTime? lastWatcherPollUtc;
    private DateTime? lastStatusPaintUtc;
    private DateTime? lastTimerOverlayPaintUtc;
    private long? lastControlTickTimestamp;
    private long? lastWatcherPollTimestamp;
    private long? lastStatusPaintTimestamp;
    private long? lastTimerOverlayPaintTimestamp;
    private long? lastStatusPaintTickTimestamp;
    private long? lastTimerOverlayPaintTickTimestamp;
    private int statusPaintDispatchSkipCount;
    private int timerOverlayPaintDispatchSkipCount;
    private int timerOverlayPaintInputSkipCount;

    public TimeSpan ControlTickInterval { get; set; }

    public TimeSpan StatusPaintInterval { get; set; }

    public TimeSpan TimerOverlayPaintInterval { get; set; }

    public TimeSpan WatcherPollInterval { get; set; }

    public TimeSpan ProcessLookupInterval { get; set; }

    public void RecordControlTick(TimeSpan elapsed)
    {
        lock (sync)
        {
            controlTicks.Record(elapsed);
            RecordInterval(controlTickIntervals, ref lastControlTickTimestamp);
            lastControlTickUtc = DateTime.UtcNow;
        }
    }

    public void RecordWatcherPoll(TimeSpan elapsed)
    {
        RecordWatcherPoll(elapsed, Stopwatch.GetTimestamp());
    }

    public void RecordWatcherPoll(TimeSpan elapsed, long completedTimestamp)
    {
        lock (sync)
        {
            watcherPolls.Record(elapsed);
            RecordInterval(watcherPollIntervals, ref lastWatcherPollTimestamp, completedTimestamp);
            lastWatcherPollUtc = DateTime.UtcNow;
        }
    }

    public void RecordStatusPaint(TimeSpan elapsed)
    {
        lock (sync)
        {
            statusPaints.Record(elapsed);
            RecordInterval(statusPaintIntervals, ref lastStatusPaintTimestamp);
            lastStatusPaintUtc = DateTime.UtcNow;
        }
    }

    public void RecordStatusPaintTick(HighPrecisionSchedulerTick tick)
    {
        lock (sync)
        {
            RecordInterval(statusPaintTickIntervals, ref lastStatusPaintTickTimestamp, tick.ActualTimestamp);
            statusPaintTickDelays.Record(tick.Delay);
        }
    }

    public void RecordStatusPaintDispatchSkipped()
    {
        lock (sync)
        {
            statusPaintDispatchSkipCount++;
        }
    }

    public void RecordTimerOverlayPaint(TimeSpan elapsed)
    {
        lock (sync)
        {
            timerOverlayPaints.Record(elapsed);
            RecordInterval(timerOverlayPaintIntervals, ref lastTimerOverlayPaintTimestamp);
            lastTimerOverlayPaintUtc = DateTime.UtcNow;
        }
    }

    public void RecordTimerOverlayPaintTick(HighPrecisionSchedulerTick tick)
    {
        lock (sync)
        {
            RecordInterval(timerOverlayPaintTickIntervals, ref lastTimerOverlayPaintTickTimestamp, tick.ActualTimestamp);
            timerOverlayPaintTickDelays.Record(tick.Delay);
        }
    }

    public void RecordTimerOverlayPaintDispatchSkipped()
    {
        lock (sync)
        {
            timerOverlayPaintDispatchSkipCount++;
        }
    }

    public void RecordTimerOverlayPaintInputSkipped()
    {
        lock (sync)
        {
            timerOverlayPaintInputSkipCount++;
        }
    }

    public RuntimePerformanceDiagnostics Snapshot()
    {
        lock (sync)
        {
            return new RuntimePerformanceDiagnostics(
                controlTicks.TotalCount,
                controlTicks.LastMilliseconds,
                controlTicks.AverageMilliseconds,
                controlTicks.MaxMilliseconds,
                lastControlTickUtc,
                watcherPolls.TotalCount,
                watcherPolls.LastMilliseconds,
                watcherPolls.AverageMilliseconds,
                watcherPolls.MaxMilliseconds,
                lastWatcherPollUtc,
                statusPaints.TotalCount,
                statusPaints.LastMilliseconds,
                statusPaints.AverageMilliseconds,
                statusPaints.MaxMilliseconds,
                lastStatusPaintUtc,
                timerOverlayPaints.TotalCount,
                timerOverlayPaints.LastMilliseconds,
                timerOverlayPaints.AverageMilliseconds,
                timerOverlayPaints.MaxMilliseconds,
                lastTimerOverlayPaintUtc,
                ControlTickInterval.TotalMilliseconds,
                StatusPaintInterval.TotalMilliseconds,
                TimerOverlayPaintInterval.TotalMilliseconds,
                WatcherPollInterval.TotalMilliseconds,
                ProcessLookupInterval.TotalMilliseconds,
                controlTickIntervals.AverageMilliseconds,
                watcherPollIntervals.AverageMilliseconds,
                statusPaintIntervals.AverageMilliseconds,
                timerOverlayPaintIntervals.AverageMilliseconds,
                statusPaintTickDelays.TotalCount,
                timerOverlayPaintTickDelays.TotalCount,
                statusPaintTickIntervals.AverageMilliseconds,
                timerOverlayPaintTickIntervals.AverageMilliseconds,
                statusPaintTickIntervals.MaxMilliseconds,
                timerOverlayPaintTickIntervals.MaxMilliseconds,
                statusPaintTickDelays.AverageMilliseconds,
                timerOverlayPaintTickDelays.AverageMilliseconds,
                statusPaintTickDelays.MaxMilliseconds,
                timerOverlayPaintTickDelays.MaxMilliseconds,
                statusPaintDispatchSkipCount,
                timerOverlayPaintDispatchSkipCount,
                timerOverlayPaintInputSkipCount,
                controlTickIntervals.MaxMilliseconds,
                watcherPollIntervals.MaxMilliseconds,
                statusPaintIntervals.MaxMilliseconds,
                timerOverlayPaintIntervals.MaxMilliseconds);
        }
    }

    private static void RecordInterval(RollingPerformanceCounter counter, ref long? lastTimestamp)
    {
        RecordInterval(counter, ref lastTimestamp, Stopwatch.GetTimestamp());
    }

    private static void RecordInterval(RollingPerformanceCounter counter, ref long? lastTimestamp, long now)
    {
        if (lastTimestamp.HasValue)
        {
            counter.Record(Stopwatch.GetElapsedTime(lastTimestamp.Value, now));
        }

        lastTimestamp = now;
    }
}

internal sealed class RollingPerformanceCounter
{
    private readonly double[] samples;
    private int nextIndex;
    private int sampleCount;
    private double sampleSum;

    public RollingPerformanceCounter(int capacity = 64)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        }

        samples = new double[capacity];
    }

    public int TotalCount { get; private set; }

    public int SampleCount => sampleCount;

    public double LastMilliseconds { get; private set; }

    public double AverageMilliseconds => sampleCount == 0 ? 0 : sampleSum / sampleCount;

    public double MaxMilliseconds
    {
        get
        {
            double max = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                max = Math.Max(max, samples[i]);
            }

            return max;
        }
    }

    public void Record(TimeSpan elapsed)
    {
        Record(elapsed.TotalMilliseconds);
    }

    public void Record(double milliseconds)
    {
        double value = Math.Max(0, milliseconds);
        LastMilliseconds = value;
        TotalCount++;

        if (sampleCount < samples.Length)
        {
            samples[nextIndex] = value;
            sampleSum += value;
            sampleCount++;
        }
        else
        {
            sampleSum -= samples[nextIndex];
            samples[nextIndex] = value;
            sampleSum += value;
        }

        nextIndex = (nextIndex + 1) % samples.Length;
    }
}
