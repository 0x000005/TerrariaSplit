using System.Diagnostics;

namespace TerrariaSplit;

internal readonly record struct HighPrecisionSchedulerTick(
    long ScheduledTimestamp,
    long ActualTimestamp,
    TimeSpan Interval,
    TimeSpan Delay);

internal sealed class HighPrecisionScheduler : IDisposable
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan SleepGuard = TimeSpan.FromMilliseconds(1);
    private static readonly long SpinThresholdTicks = Stopwatch.Frequency / 1000;

    private readonly string name;
    private readonly Action<HighPrecisionSchedulerTick> callback;
    private readonly object sync = new();
    private readonly AutoResetEvent signal = new(false);
    private Thread? thread;
    private TimeSpan interval = TimeSpan.FromMilliseconds(16);
    private bool running;
    private bool disposed;
    private int scheduleVersion;

    public HighPrecisionScheduler(string name, Action<HighPrecisionSchedulerTick> callback)
    {
        this.name = string.IsNullOrWhiteSpace(name) ? "HighPrecisionScheduler" : name;
        this.callback = callback;
    }

    public bool IsRunning
    {
        get
        {
            lock (sync)
            {
                return running;
            }
        }
    }

    public void Start(TimeSpan requestedInterval)
    {
        bool changed = false;
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            TimeSpan normalized = NormalizeInterval(requestedInterval);
            if (running && interval == normalized)
            {
                return;
            }

            EnsureThreadStarted();
            interval = normalized;
            running = true;
            scheduleVersion++;
            changed = true;
        }

        if (changed)
        {
            signal.Set();
        }
    }

    public void UpdateInterval(TimeSpan requestedInterval)
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            TimeSpan normalized = NormalizeInterval(requestedInterval);
            if (interval == normalized)
            {
                return;
            }

            interval = normalized;
            scheduleVersion++;
        }

        signal.Set();
    }

    public void Stop()
    {
        lock (sync)
        {
            if (!running)
            {
                return;
            }

            running = false;
            scheduleVersion++;
        }

        signal.Set();
    }

    public void Dispose()
    {
        Thread? threadToJoin;
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            running = false;
            scheduleVersion++;
            threadToJoin = thread;
        }

        signal.Set();
        if (threadToJoin is not null &&
            threadToJoin.IsAlive &&
            threadToJoin.ManagedThreadId != Environment.CurrentManagedThreadId)
        {
            threadToJoin.Join(TimeSpan.FromSeconds(1));
        }

        signal.Dispose();
    }

    private void EnsureThreadStarted()
    {
        if (thread is not null)
        {
            return;
        }

        thread = new Thread(Run)
        {
            IsBackground = true,
            Name = name
        };
        thread.Start();
    }

    private void Run()
    {
        using HighResolutionTimerPeriod? timerPeriod = HighResolutionTimerPeriod.TryBegin(1);
        long nextScheduledTimestamp = Stopwatch.GetTimestamp();
        int observedVersion = -1;

        while (true)
        {
            TimeSpan currentInterval;
            int currentVersion;
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }

                if (!running)
                {
                    currentInterval = TimeSpan.Zero;
                    currentVersion = scheduleVersion;
                }
                else
                {
                    currentInterval = interval;
                    currentVersion = scheduleVersion;
                }
            }

            if (currentInterval <= TimeSpan.Zero)
            {
                signal.WaitOne();
                observedVersion = currentVersion;
                nextScheduledTimestamp = Stopwatch.GetTimestamp();
                continue;
            }

            if (observedVersion != currentVersion)
            {
                observedVersion = currentVersion;
                nextScheduledTimestamp = Stopwatch.GetTimestamp() + ToStopwatchTicks(currentInterval);
            }

            if (!WaitUntil(nextScheduledTimestamp))
            {
                continue;
            }

            long actualTimestamp = Stopwatch.GetTimestamp();
            var tick = new HighPrecisionSchedulerTick(
                nextScheduledTimestamp,
                actualTimestamp,
                currentInterval,
                Stopwatch.GetElapsedTime(nextScheduledTimestamp, actualTimestamp));
            try
            {
                callback(tick);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, $"{name} callback failed.");
            }

            long intervalTicks = ToStopwatchTicks(currentInterval);
            nextScheduledTimestamp += intervalTicks;
            long now = Stopwatch.GetTimestamp();
            if (nextScheduledTimestamp <= now)
            {
                nextScheduledTimestamp = now + intervalTicks;
            }
        }
    }

    private bool WaitUntil(long scheduledTimestamp)
    {
        while (true)
        {
            lock (sync)
            {
                if (disposed)
                {
                    return false;
                }
            }

            long now = Stopwatch.GetTimestamp();
            long remainingTicks = scheduledTimestamp - now;
            if (remainingTicks <= 0)
            {
                return true;
            }

            TimeSpan remaining = TimeSpan.FromSeconds(remainingTicks / (double)Stopwatch.Frequency);
            if (remaining > SleepGuard)
            {
                TimeSpan wait = remaining - SleepGuard;
                if (signal.WaitOne(wait))
                {
                    return false;
                }

                continue;
            }

            if (remainingTicks > SpinThresholdTicks / 4)
            {
                Thread.Yield();
            }
            else
            {
                Thread.SpinWait(64);
            }
        }
    }

    private static TimeSpan NormalizeInterval(TimeSpan requestedInterval)
    {
        return requestedInterval < MinimumInterval ? MinimumInterval : requestedInterval;
    }

    private static long ToStopwatchTicks(TimeSpan value)
    {
        return Math.Max(1, (long)Math.Round(value.TotalSeconds * Stopwatch.Frequency));
    }
}
