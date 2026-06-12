using System.Diagnostics;
using System.Reflection;

namespace TerrariaSplit.Tests;

internal static class HighPrecisionSchedulerTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("High precision scheduler updates interval on one thread", TestUpdateIntervalKeepsThread);
        yield return ("High precision scheduler start is idempotent", TestStartIsIdempotent);
        yield return ("High precision scheduler stops callbacks", TestStopStopsCallbacks);
        yield return ("High precision scheduler avoids callback reentry", TestNoCallbackReentry);
        yield return ("High precision scheduler disposes thread", TestDisposeStopsThread);
        yield return ("High precision scheduler paces ticks near requested interval", TestTickPacingNearRequestedInterval);
    }

    private static void TestTickPacingNearRequestedInterval()
    {
        const int RequiredTicks = 40;
        using var enoughTicks = new ManualResetEventSlim(false);
        var timestamps = new List<long>();
        using var scheduler = new HighPrecisionScheduler("test pacing", tick =>
        {
            lock (timestamps)
            {
                if (timestamps.Count < RequiredTicks)
                {
                    timestamps.Add(tick.ActualTimestamp);
                    if (timestamps.Count == RequiredTicks)
                    {
                        enoughTicks.Set();
                    }
                }
            }
        });

        scheduler.Start(TimeSpan.FromMilliseconds(5));
        if (!enoughTicks.Wait(TimeSpan.FromSeconds(3)))
        {
            throw new InvalidOperationException("Scheduler did not produce enough ticks before timeout.");
        }

        scheduler.Stop();
        long first;
        long last;
        lock (timestamps)
        {
            first = timestamps[0];
            last = timestamps[^1];
        }

        double averageMilliseconds =
            Stopwatch.GetElapsedTime(first, last).TotalMilliseconds / (RequiredTicks - 1);
        if (averageMilliseconds < 4.0 || averageMilliseconds > 7.5)
        {
            throw new InvalidOperationException(
                $"Average tick interval {averageMilliseconds:0.00}ms drifted from the requested 5ms.");
        }
    }

    private static void TestStartIsIdempotent()
    {
        using var scheduler = new HighPrecisionScheduler("test idempotent start", _ => { });
        scheduler.Start(TimeSpan.FromMilliseconds(10));
        int firstVersion = GetScheduleVersion(scheduler);

        scheduler.Start(TimeSpan.FromMilliseconds(10));

        TestAssert.Equal(firstVersion, GetScheduleVersion(scheduler));
    }

    private static void TestUpdateIntervalKeepsThread()
    {
        using var scheduler = new HighPrecisionScheduler("test update", _ => { });
        scheduler.Start(TimeSpan.FromMilliseconds(20));
        Thread? firstThread = GetSchedulerThread(scheduler);

        scheduler.UpdateInterval(TimeSpan.FromMilliseconds(10));
        scheduler.UpdateInterval(TimeSpan.FromMilliseconds(5));
        Thread? secondThread = GetSchedulerThread(scheduler);

        TestAssert.Equal(firstThread?.ManagedThreadId, secondThread?.ManagedThreadId);
    }

    private static void TestStopStopsCallbacks()
    {
        using var firstTick = new ManualResetEventSlim(false);
        int ticks = 0;
        using var scheduler = new HighPrecisionScheduler("test stop", _ =>
        {
            Interlocked.Increment(ref ticks);
            firstTick.Set();
        });

        scheduler.Start(TimeSpan.FromMilliseconds(5));
        if (!firstTick.Wait(TimeSpan.FromSeconds(1)))
        {
            throw new InvalidOperationException("Scheduler did not tick before timeout.");
        }

        scheduler.Stop();
        Thread.Sleep(50);
        int afterStop = Volatile.Read(ref ticks);
        Thread.Sleep(50);

        TestAssert.Equal(afterStop, Volatile.Read(ref ticks));
    }

    private static void TestNoCallbackReentry()
    {
        using var enoughTicks = new ManualResetEventSlim(false);
        int inCallback = 0;
        int reentered = 0;
        int ticks = 0;
        using var scheduler = new HighPrecisionScheduler("test reentry", _ =>
        {
            if (Interlocked.Increment(ref inCallback) > 1)
            {
                Interlocked.Exchange(ref reentered, 1);
            }

            try
            {
                Thread.Sleep(10);
                if (Interlocked.Increment(ref ticks) >= 4)
                {
                    enoughTicks.Set();
                }
            }
            finally
            {
                Interlocked.Decrement(ref inCallback);
            }
        });

        scheduler.Start(TimeSpan.FromMilliseconds(1));
        if (!enoughTicks.Wait(TimeSpan.FromSeconds(1)))
        {
            throw new InvalidOperationException("Scheduler did not produce enough ticks before timeout.");
        }

        scheduler.Stop();
        TestAssert.Equal(0, Volatile.Read(ref reentered));
    }

    private static void TestDisposeStopsThread()
    {
        var scheduler = new HighPrecisionScheduler("test dispose", _ => { });
        scheduler.Start(TimeSpan.FromMilliseconds(5));
        Thread? thread = GetSchedulerThread(scheduler);

        scheduler.Dispose();

        TestAssert.Equal(false, thread?.IsAlive ?? false);
    }

    private static Thread? GetSchedulerThread(HighPrecisionScheduler scheduler)
    {
        FieldInfo field = typeof(HighPrecisionScheduler).GetField(
                "thread",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing scheduler thread field.");
        return (Thread?)field.GetValue(scheduler);
    }

    private static int GetScheduleVersion(HighPrecisionScheduler scheduler)
    {
        FieldInfo field = typeof(HighPrecisionScheduler).GetField(
                "scheduleVersion",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing scheduler version field.");
        return (int)(field.GetValue(scheduler) ?? 0);
    }
}
