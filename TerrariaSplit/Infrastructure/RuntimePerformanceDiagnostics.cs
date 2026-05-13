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
    double TimerRenderIntervalMilliseconds,
    double WatcherPollIntervalMilliseconds,
    double ProcessLookupIntervalMilliseconds)
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
        TimerRenderIntervalMilliseconds: 0,
        WatcherPollIntervalMilliseconds: 0,
        ProcessLookupIntervalMilliseconds: 0);
}

internal sealed class RuntimePerformanceTracker
{
    private readonly RollingPerformanceCounter controlTicks = new();
    private readonly RollingPerformanceCounter watcherPolls = new();
    private readonly RollingPerformanceCounter paints = new();
    private DateTime? lastControlTickUtc;
    private DateTime? lastWatcherPollUtc;
    private DateTime? lastPaintUtc;

    public TimeSpan TimerRenderInterval { get; set; }

    public TimeSpan WatcherPollInterval { get; set; }

    public TimeSpan ProcessLookupInterval { get; set; }

    public void RecordControlTick(TimeSpan elapsed)
    {
        controlTicks.Record(elapsed);
        lastControlTickUtc = DateTime.UtcNow;
    }

    public void RecordWatcherPoll(TimeSpan elapsed)
    {
        watcherPolls.Record(elapsed);
        lastWatcherPollUtc = DateTime.UtcNow;
    }

    public void RecordPaint(TimeSpan elapsed)
    {
        paints.Record(elapsed);
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
            TimerRenderInterval.TotalMilliseconds,
            WatcherPollInterval.TotalMilliseconds,
            ProcessLookupInterval.TotalMilliseconds);
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
