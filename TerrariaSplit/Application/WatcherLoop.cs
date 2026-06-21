using System.Threading;
using System.Threading.Tasks;

namespace TerrariaSplit.Application;

internal sealed class WatcherLoop : IDisposable
{
    private readonly WatcherCompletionDispatcher watcherCompletions;
    private readonly RuntimeCommandSequencer runtimeCommands;
    private readonly Func<long, IReadOnlyList<RunEvent>, WatcherPollCompletion> pollWatcher;
    private readonly Action<WatcherPollCompletion> queueCompletion;
    private readonly Func<WatcherPollCompletion, WatcherPublishState, TimeSpan, bool> shouldPublish;
    private readonly Action<TimeSpan, long>? recordPoll;
    private readonly TimeSpan suspendedPollInterval;
    private readonly TimeSpan heartbeatInterval;
    private readonly object lifecycleLock = new();
    private readonly AutoResetEvent signal = new(false);
    private bool disposed;
    private CancellationTokenSource? cancellation;
    private Task? task;

    public WatcherLoop(
        WatcherCompletionDispatcher watcherCompletions,
        RuntimeCommandSequencer runtimeCommands,
        Func<long, IReadOnlyList<RunEvent>, WatcherPollCompletion> pollWatcher,
        Action<WatcherPollCompletion> queueCompletion,
        Func<WatcherPollCompletion, WatcherPublishState, TimeSpan, bool> shouldPublish,
        Action<TimeSpan, long>? recordPoll,
        TimeSpan suspendedPollInterval,
        TimeSpan heartbeatInterval)
    {
        this.watcherCompletions = watcherCompletions;
        this.runtimeCommands = runtimeCommands;
        this.pollWatcher = pollWatcher;
        this.queueCompletion = queueCompletion;
        this.shouldPublish = shouldPublish;
        this.recordPoll = recordPoll;
        this.suspendedPollInterval = suspendedPollInterval;
        this.heartbeatInterval = heartbeatInterval;
    }

    public void StartIfNeeded()
    {
        lock (lifecycleLock)
        {
            if (disposed || task is not null)
            {
                return;
            }

            cancellation = new CancellationTokenSource();
            CancellationToken token = cancellation.Token;
            task = Task.Factory.StartNew(
                () => Run(token),
                token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
    }

    public void Signal()
    {
        if (disposed)
        {
            return;
        }

        try
        {
            signal.Set();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public long QueueCommand(RuntimeCommand command)
    {
        long sequence = runtimeCommands.Queue(command);
        Signal();
        StartIfNeeded();
        return sequence;
    }

    public bool Stop(TimeSpan waitTimeout)
    {
        CancellationTokenSource? cancellationToStop;
        Task? taskToWait;
        lock (lifecycleLock)
        {
            if (disposed)
            {
                return task?.IsCompleted != false;
            }

            disposed = true;
            cancellationToStop = cancellation;
            taskToWait = task;
        }

        cancellationToStop?.Cancel();
        bool completed = taskToWait is null;
        try
        {
            completed = taskToWait?.Wait(waitTimeout) ?? true;
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(error => error is OperationCanceledException))
        {
            completed = true;
        }
        catch (ObjectDisposedException)
        {
            completed = true;
        }

        if (completed)
        {
            signal.Dispose();
            cancellationToStop?.Dispose();
        }

        return completed;
    }

    public void Dispose()
    {
        Stop(TimeSpan.FromMilliseconds(500));
    }

    private void Run(CancellationToken cancellationToken)
    {
        using HighResolutionTimerPeriod? timerPeriod = HighResolutionTimerPeriod.TryBegin(1);
        WaitHandle[] waitHandles = [cancellationToken.WaitHandle, signal];
        var publishState = WatcherPublishState.Empty;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (watcherCompletions.IsSuspended)
            {
                int suspendedWaitResult = WaitHandle.WaitAny(waitHandles, suspendedPollInterval);
                if (suspendedWaitResult == 0)
                {
                    return;
                }

                continue;
            }

            RuntimeCommandDrainResult commandResult = runtimeCommands.Drain();
            WatcherPollCompletion completion = pollWatcher(
                commandResult.LatestAppliedSequence,
                commandResult.Events);
            recordPoll?.Invoke(completion.Elapsed, completion.CompletedTimestamp);

            if (shouldPublish(completion, publishState, heartbeatInterval))
            {
                publishState = WatcherPublishState.FromCompletion(completion);
                queueCompletion(completion);
            }

            int signaled = WaitHandle.WaitAny(waitHandles, completion.NextPollInterval);
            if (signaled == 0)
            {
                return;
            }
        }
    }
}
