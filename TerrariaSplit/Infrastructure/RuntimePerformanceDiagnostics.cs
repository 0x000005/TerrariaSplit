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
    int PaintCount,
    double LastPaintMilliseconds,
    double AveragePaintMilliseconds,
    double MaxPaintMilliseconds,
    DateTime? LastPaintUtc,
    double ControlTickIntervalMilliseconds,
    double TimerRenderIntervalMilliseconds,
    double WatcherPollIntervalMilliseconds,
    double ProcessLookupIntervalMilliseconds,
    double ActualControlTickIntervalMilliseconds,
    double ActualWatcherPollIntervalMilliseconds,
    double ActualPaintIntervalMilliseconds)
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
        PaintCount: 0,
        LastPaintMilliseconds: 0,
        AveragePaintMilliseconds: 0,
        MaxPaintMilliseconds: 0,
        LastPaintUtc: null,
        ControlTickIntervalMilliseconds: 0,
        TimerRenderIntervalMilliseconds: 0,
        WatcherPollIntervalMilliseconds: 0,
        ProcessLookupIntervalMilliseconds: 0,
        ActualControlTickIntervalMilliseconds: 0,
        ActualWatcherPollIntervalMilliseconds: 0,
        ActualPaintIntervalMilliseconds: 0);
}

internal sealed class RuntimePerformanceTracker
{
    private readonly RollingPerformanceCounter controlTicks = new();
    private readonly RollingPerformanceCounter watcherPolls = new();
    private readonly RollingPerformanceCounter paints = new();
    private readonly RollingPerformanceCounter controlTickIntervals = new();
    private readonly RollingPerformanceCounter watcherPollIntervals = new();
    private readonly RollingPerformanceCounter paintIntervals = new();
    private DateTime? lastControlTickUtc;
    private DateTime? lastWatcherPollUtc;
    private DateTime? lastPaintUtc;
    private long? lastControlTickTimestamp;
    private long? lastWatcherPollTimestamp;
    private long? lastPaintTimestamp;

    public TimeSpan ControlTickInterval { get; set; }

    public TimeSpan TimerRenderInterval { get; set; }

    public TimeSpan WatcherPollInterval { get; set; }

    public TimeSpan ProcessLookupInterval { get; set; }

    public void RecordControlTick(TimeSpan elapsed)
    {
        controlTicks.Record(elapsed);
        RecordInterval(controlTickIntervals, ref lastControlTickTimestamp);
        lastControlTickUtc = DateTime.UtcNow;
    }

    public void RecordWatcherPoll(TimeSpan elapsed)
    {
        RecordWatcherPoll(elapsed, Stopwatch.GetTimestamp());
    }

    public void RecordWatcherPoll(TimeSpan elapsed, long completedTimestamp)
    {
        watcherPolls.Record(elapsed);
        RecordInterval(watcherPollIntervals, ref lastWatcherPollTimestamp, completedTimestamp);
        lastWatcherPollUtc = DateTime.UtcNow;
    }

    public void RecordPaint(TimeSpan elapsed)
    {
        paints.Record(elapsed);
        RecordInterval(paintIntervals, ref lastPaintTimestamp);
        lastPaintUtc = DateTime.UtcNow;
    }

    public RuntimePerformanceDiagnostics Snapshot()
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
            paints.TotalCount,
            paints.LastMilliseconds,
            paints.AverageMilliseconds,
            paints.MaxMilliseconds,
            lastPaintUtc,
            ControlTickInterval.TotalMilliseconds,
            TimerRenderInterval.TotalMilliseconds,
            WatcherPollInterval.TotalMilliseconds,
            ProcessLookupInterval.TotalMilliseconds,
            controlTickIntervals.AverageMilliseconds,
            watcherPollIntervals.AverageMilliseconds,
            paintIntervals.AverageMilliseconds);
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
