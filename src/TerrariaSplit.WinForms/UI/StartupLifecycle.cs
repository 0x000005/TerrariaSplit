using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

namespace TerrariaSplit.UI;

internal enum StartupPhase
{
    Constructing,
    FirstFramePresented,
    InitializingRuntime,
    FullyReady,
    Failed,
    Stopping
}

internal sealed class StartupCommandGate
{
    private readonly Queue<AppCommand> pending = new();
    private StartupCommandGateState state;

    public int PendingCount => pending.Count;

    public bool IsOpen => state == StartupCommandGateState.Open;

    public bool Submit(AppCommand command, Action<AppCommand> dispatch)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(dispatch);

        if (state == StartupCommandGateState.Cancelled)
        {
            return false;
        }

        if (state == StartupCommandGateState.Open)
        {
            dispatch(command);
            return true;
        }

        pending.Enqueue(command);
        return true;
    }

    public void Open(Action<AppCommand> dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        if (state is StartupCommandGateState.Open or StartupCommandGateState.Cancelled)
        {
            return;
        }

        state = StartupCommandGateState.Draining;
        while (pending.Count > 0)
        {
            dispatch(pending.Dequeue());
        }

        state = StartupCommandGateState.Open;
    }

    public void Cancel()
    {
        pending.Clear();
        state = StartupCommandGateState.Cancelled;
    }

    private enum StartupCommandGateState
    {
        Pending,
        Draining,
        Open,
        Cancelled
    }
}

internal sealed class RuntimeBootstrapper : IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly object sync = new();
    private StartupPhase phase = StartupPhase.Constructing;

    public StartupPhase Phase
    {
        get
        {
            lock (sync)
            {
                return phase;
            }
        }
    }

    public CancellationToken CancellationToken => cancellation.Token;

    public bool TryMarkFirstFramePresented()
    {
        lock (sync)
        {
            if (phase != StartupPhase.Constructing)
            {
                return false;
            }

            phase = StartupPhase.FirstFramePresented;
            return true;
        }
    }

    public async Task<T> InitializeAsync<T>(Func<CancellationToken, Task<T>> initialize)
    {
        ArgumentNullException.ThrowIfNull(initialize);
        lock (sync)
        {
            if (phase != StartupPhase.FirstFramePresented)
            {
                throw new InvalidOperationException($"Runtime initialization cannot start from phase {phase}.");
            }

            phase = StartupPhase.InitializingRuntime;
        }

        return await initialize(cancellation.Token).ConfigureAwait(false);
    }

    public void MarkFullyReady()
    {
        lock (sync)
        {
            if (phase != StartupPhase.InitializingRuntime)
            {
                throw new InvalidOperationException($"Runtime cannot become ready from phase {phase}.");
            }

            phase = StartupPhase.FullyReady;
        }
    }

    public void MarkFailed()
    {
        lock (sync)
        {
            if (phase != StartupPhase.Stopping)
            {
                phase = StartupPhase.Failed;
            }
        }

        cancellation.Cancel();
    }

    public void Cancel()
    {
        lock (sync)
        {
            if (phase == StartupPhase.Stopping)
            {
                return;
            }

            phase = StartupPhase.Stopping;
        }

        cancellation.Cancel();
    }

    public void Dispose()
    {
        Cancel();
        cancellation.Dispose();
    }
}

internal static class StartupDiagnostics
{
    public const string FirstFrameEventEnvironmentVariable = "TERRARIA_SPLIT_STARTUP_FIRST_FRAME_EVENT";
    public const string FullyReadyEventEnvironmentVariable = "TERRARIA_SPLIT_STARTUP_FULLY_READY_EVENT";
    public const string TracePathEnvironmentVariable = "TERRARIA_SPLIT_STARTUP_TRACE_PATH";
    private static readonly string? TracePath = Environment.GetEnvironmentVariable(TracePathEnvironmentVariable);
    private static readonly ConcurrentQueue<StartupTraceEntry> TraceEntries = new();
    private static long traceOriginTimestamp;

    public static void SignalFirstFrame()
    {
        SignalNamedEvent(FirstFrameEventEnvironmentVariable);
    }

    public static void SignalFullyReady()
    {
        SignalNamedEvent(FullyReadyEventEnvironmentVariable);
    }

    public static void RecordTrace(string stage)
    {
        if (string.IsNullOrWhiteSpace(TracePath))
        {
            return;
        }

        long now = Stopwatch.GetTimestamp();
        long origin = Interlocked.CompareExchange(ref traceOriginTimestamp, now, 0);
        if (origin == 0)
        {
            origin = now;
        }

        TraceEntries.Enqueue(new StartupTraceEntry(stage, Stopwatch.GetElapsedTime(origin, now).TotalMilliseconds));
    }

    public static void FlushTrace()
    {
        if (string.IsNullOrWhiteSpace(TracePath))
        {
            return;
        }

        try
        {
            string fullPath = Path.GetFullPath(TracePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            IEnumerable<string> lines = new[] { "Stage,ManagedElapsedMs" }
                .Concat(TraceEntries.Select(entry =>
                    $"{entry.Stage},{entry.ManagedElapsedMilliseconds.ToString("F3", CultureInfo.InvariantCulture)}"));
            File.WriteAllLines(fullPath, lines);
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, "Failed to write startup trace.");
        }
    }

    private static void SignalNamedEvent(string environmentVariable)
    {
        string? eventName = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return;
        }

        try
        {
            if (EventWaitHandle.TryOpenExisting(eventName, out EventWaitHandle? startupEvent))
            {
                using (startupEvent)
                {
                    startupEvent.Set();
                }
            }
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, $"Failed to signal startup event from {environmentVariable}.");
        }
    }

    private readonly record struct StartupTraceEntry(string Stage, double ManagedElapsedMilliseconds);
}
