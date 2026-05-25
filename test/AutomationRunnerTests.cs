using TerrariaSplit;

namespace TerrariaSplit.Tests;

internal static class AutomationRunnerTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("AutomationRunner rejects duplicate runs and cancels current run", AutomationRunnerSingleRunAndCancel);
    }

    private static void AutomationRunnerSingleRunAndCancel()
    {
        int starts = 0;
        bool observedCancellation = false;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var runner = new AutomationRunner<int>(
            "test",
            async (_, cancellationToken) =>
            {
                starts++;
                entered.SetResult();
                try
                {
                    await release.Task.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    observedCancellation = true;
                    throw;
                }
            });

        Task firstRun = runner.StartAsync(1);
        entered.Task.GetAwaiter().GetResult();
        Task duplicateRun = runner.StartAsync(2);

        TestAssert.Equal(1, starts);
        TestAssert.Equal(true, runner.IsRunning);
        TestAssert.Equal(true, duplicateRun.IsCompletedSuccessfully);
        TestAssert.Equal(true, runner.Cancel());

        try
        {
            firstRun.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        TestAssert.Equal(true, observedCancellation);
        TestAssert.Equal(false, runner.IsRunning);
    }
}
