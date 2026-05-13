namespace TerrariaSplit;

internal sealed class TerrariaCreateWorldAutomation : IDisposable
{
    private readonly object syncRoot = new();
    private readonly CreateWorldWorkflow workflow = new();
    private CancellationTokenSource? runCancellation;

    public bool IsRunning => workflow.IsRunning;

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        return RunAsync(new AppSettings(), cancellationToken);
    }

    public Task RunAsync(AppSettings settings, CancellationToken cancellationToken = default)
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

        return RunWithTrackedCancellationAsync(settings, linkedCancellation);
    }

    public bool Cancel()
    {
        lock (syncRoot)
        {
            if (runCancellation is null || runCancellation.IsCancellationRequested || !workflow.IsRunning)
            {
                return false;
            }

            AppLogger.Info("Create world automation cancellation requested.");
            runCancellation.Cancel();
            return true;
        }
    }

    private async Task RunWithTrackedCancellationAsync(AppSettings settings, CancellationTokenSource linkedCancellation)
    {
        try
        {
            await workflow.RunAsync(settings, linkedCancellation.Token);
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
