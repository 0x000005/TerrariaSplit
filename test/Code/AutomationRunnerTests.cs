using TerrariaSplit;

namespace TerrariaSplit.Tests;

internal static class AutomationRunnerTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("AutomationRunner rejects duplicate runs and cancels current run", AutomationRunnerSingleRunAndCancel);
        yield return ("AutomationRunner converts unhandled errors to failure result", AutomationRunnerConvertsUnhandledError);
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
                    return AutomationResult.Success("released");
                }
                catch (OperationCanceledException)
                {
                    observedCancellation = true;
                    throw;
                }
            });

        Task<AutomationResult> firstRun = runner.StartAsync(1);
        entered.Task.GetAwaiter().GetResult();
        AutomationResult duplicateRun = runner.StartAsync(2).GetAwaiter().GetResult();

        TestAssert.Equal(1, starts);
        TestAssert.Equal(true, runner.IsRunning);
        TestAssert.Equal(true, duplicateRun.Succeeded);
        TestAssert.Equal(true, runner.Cancel());

        AutomationResult cancelledRun = firstRun.GetAwaiter().GetResult();

        TestAssert.Equal(false, cancelledRun.Succeeded);
        TestAssert.Equal(true, cancelledRun.Cancelled);
        TestAssert.Equal(true, observedCancellation);
        TestAssert.Equal(false, runner.IsRunning);
    }

    private static void AutomationRunnerConvertsUnhandledError()
    {
        var error = new InvalidOperationException("boom");
        using var runner = new AutomationRunner<int>(
            "test",
            (_, _) => Task.FromException<AutomationResult>(error));

        AutomationResult result = runner.StartAsync(1).GetAwaiter().GetResult();

        TestAssert.Equal(false, result.Succeeded);
        TestAssert.Equal(false, result.Cancelled);
        TestAssert.Equal("test automation failed.", result.UserMessage);
        TestAssert.Equal(true, ReferenceEquals(error, result.Exception));
        TestAssert.Equal(false, runner.IsRunning);
    }
}
