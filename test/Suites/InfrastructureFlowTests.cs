namespace TerrariaSplit.Tests;

internal static class InfrastructureFlowTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("Windows process liveness probe reports current and missing processes", TestSuite.Windows, ProcessLiveness);
        yield return TestCase.Async("high precision scheduler starts, retimes, stops and disposes without callbacks after shutdown", TestSuite.Windows, SchedulerLifecycle);
    }

    private static void ProcessLiveness()
    {
        Check.True(ProcessLivenessProbe.IsRunning(Environment.ProcessId));
        Check.False(ProcessLivenessProbe.IsRunning(int.MaxValue));
    }

    private static async Task SchedulerLifecycle(CancellationToken cancellationToken)
    {
        int count = 0;
        var firstTicks = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var scheduler = new HighPrecisionScheduler("test-scheduler", tick =>
        {
            Check.True(tick.Interval >= TimeSpan.FromMilliseconds(1));
            if (Interlocked.Increment(ref count) >= 3) firstTicks.TrySetResult();
        });
        scheduler.Start(TimeSpan.FromMilliseconds(4));
        await firstTicks.Task.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
        Check.True(scheduler.IsRunning);
        scheduler.UpdateInterval(TimeSpan.Zero);
        await Task.Delay(20, cancellationToken);
        scheduler.Stop();
        Check.False(scheduler.IsRunning);
        int stoppedCount = Volatile.Read(ref count);
        await Task.Delay(20, cancellationToken);
        Check.Equal(stoppedCount, Volatile.Read(ref count));
        scheduler.Dispose();
        scheduler.Start(TimeSpan.FromMilliseconds(1));
        Check.False(scheduler.IsRunning);
    }
}

