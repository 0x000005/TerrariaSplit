using System.Collections.Concurrent;

namespace TerrariaSplit.Application;

internal sealed class WatcherCompletionDispatcher
{
    private readonly Action<Action> dispatch;
    private readonly Func<bool> shouldYieldDispatch;
    private readonly Action<WatcherPollCompletion> complete;
    private readonly ConcurrentQueue<WatcherPollCompletion> pendingCompletions = new();
    private readonly int maxCompletionsPerDispatch;
    private int dispatchPending;
    private int suspended;
    private bool disposed;

    public WatcherCompletionDispatcher(
        Action<Action> dispatch,
        Func<bool> shouldYieldDispatch,
        Action<WatcherPollCompletion> complete,
        int maxCompletionsPerDispatch)
    {
        this.dispatch = dispatch;
        this.shouldYieldDispatch = shouldYieldDispatch;
        this.complete = complete;
        this.maxCompletionsPerDispatch = maxCompletionsPerDispatch;
    }

    public bool IsSuspended => Volatile.Read(ref suspended) != 0;

    public bool SetSuspended(bool value)
    {
        int nextValue = value ? 1 : 0;
        int previousValue = Interlocked.Exchange(ref suspended, nextValue);
        if (previousValue == nextValue)
        {
            return false;
        }

        if (!value && !pendingCompletions.IsEmpty)
        {
            RequestDispatch();
        }

        return true;
    }

    public void Queue(WatcherPollCompletion completion)
    {
        pendingCompletions.Enqueue(completion);
        RequestDispatch();
    }

    public void Dispose()
    {
        disposed = true;
    }

    private void RequestDispatch()
    {
        if (disposed ||
            IsSuspended ||
            Interlocked.Exchange(ref dispatchPending, 1) == 1)
        {
            return;
        }

        try
        {
            dispatch(Drain);
        }
        catch (ObjectDisposedException)
        {
            Interlocked.Exchange(ref dispatchPending, 0);
        }
        catch (InvalidOperationException)
        {
            Interlocked.Exchange(ref dispatchPending, 0);
        }
    }

    private void Drain()
    {
        Interlocked.Exchange(ref dispatchPending, 0);
        if (IsSuspended)
        {
            return;
        }

        int processedCount = 0;
        while (pendingCompletions.TryDequeue(out WatcherPollCompletion completion))
        {
            complete(completion);
            processedCount++;
            if (processedCount >= maxCompletionsPerDispatch || shouldYieldDispatch())
            {
                break;
            }
        }

        if (!pendingCompletions.IsEmpty)
        {
            RequestDispatch();
        }
    }
}
