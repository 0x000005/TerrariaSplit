namespace TerrariaSplit;

internal sealed class TerrariaPracticeWorldAutomation : IDisposable
{
    private readonly object syncRoot = new();
    private readonly PracticeWorldWorkflow workflow = new();
    private CancellationTokenSource? runCancellation;

    public bool IsRunning => workflow.IsRunning;

    public Task RunAsync(AppSettings settings, PracticeWorldSlot slot, CancellationToken cancellationToken = default)
    {
        CancellationTokenSource linkedCancellation;
        lock (syncRoot)
        {
            if (workflow.IsRunning)
            {
                return Task.CompletedTask;
            }

            linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            runCancellation?.Dispose();
            runCancellation = linkedCancellation;
        }

        return RunWithTrackedCancellationAsync(settings, slot, linkedCancellation);
    }

    public bool Cancel()
    {
        lock (syncRoot)
        {
            if (runCancellation is null || runCancellation.IsCancellationRequested || !workflow.IsRunning)
            {
                return false;
            }

            AppLogger.Info("Practice world automation cancellation requested.");
            runCancellation.Cancel();
            return true;
        }
    }

    private async Task RunWithTrackedCancellationAsync(
        AppSettings settings,
        PracticeWorldSlot slot,
        CancellationTokenSource linkedCancellation)
    {
        try
        {
            await workflow.RunAsync(settings, slot, linkedCancellation.Token);
        }
        finally
        {
            lock (syncRoot)
            {
                if (ReferenceEquals(runCancellation, linkedCancellation))
                {
                    runCancellation = null;
                }
            }

            linkedCancellation.Dispose();
        }
    }

    public void Dispose()
    {
        Cancel();
        lock (syncRoot)
        {
            runCancellation?.Dispose();
            runCancellation = null;
        }

        workflow.Dispose();
    }
}
