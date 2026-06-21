namespace TerrariaSplit.Application;

internal sealed class AutomationRunner<TRequest> : IDisposable
{
    private readonly object syncRoot = new();
    private readonly string name;
    private readonly Func<TRequest, CancellationToken, Task> runAsync;
    private readonly Action? dispose;
    private CancellationTokenSource? runCancellation;
    private bool isRunning;

    public AutomationRunner(
        string name,
        Func<TRequest, CancellationToken, Task> runAsync,
        Action? dispose = null)
    {
        this.name = name;
        this.runAsync = runAsync;
        this.dispose = dispose;
    }

    public bool IsRunning
    {
        get
        {
            lock (syncRoot)
            {
                return isRunning;
            }
        }
    }

    public Task StartAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        CancellationTokenSource linkedCancellation;
        lock (syncRoot)
        {
            if (isRunning)
            {
                return Task.CompletedTask;
            }

            linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            runCancellation?.Dispose();
            runCancellation = linkedCancellation;
            isRunning = true;
        }

        return RunWithTrackedCancellationAsync(request, linkedCancellation);
    }

    public bool Cancel()
    {
        lock (syncRoot)
        {
            if (runCancellation is null || runCancellation.IsCancellationRequested || !isRunning)
            {
                return false;
            }

            AppLogger.Info($"{name} automation cancellation requested.");
            runCancellation.Cancel();
            return true;
        }
    }

    private async Task RunWithTrackedCancellationAsync(TRequest request, CancellationTokenSource linkedCancellation)
    {
        try
        {
            await runAsync(request, linkedCancellation.Token);
        }
        finally
        {
            lock (syncRoot)
            {
                if (ReferenceEquals(runCancellation, linkedCancellation))
                {
                    runCancellation = null;
                    isRunning = false;
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
            isRunning = false;
        }

        dispose?.Invoke();
    }
}
