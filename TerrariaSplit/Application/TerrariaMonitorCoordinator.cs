using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace TerrariaSplit;

internal sealed class TerrariaMonitorCoordinator : IDisposable
{
    private static readonly TimeSpan RuntimePendingMenuGraceDuration = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan WatcherRunningPollInterval = TimeSpan.FromMilliseconds(5);
    private static readonly TimeSpan WatcherIdlePollInterval = TimeSpan.FromMilliseconds(5);
    private static readonly TimeSpan WatcherScanPollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan WatcherProcessLookupInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan UiScalePatchRetryInterval = TimeSpan.FromSeconds(2);
    private const int MaxWatcherCompletionsPerDispatch = 8;

    private readonly ITerrariaWorldWatcher watcher;
    private readonly ITerrariaUiScalePatchApplier uiScalePatchApplier;
    private readonly Action<Action> dispatch;
    private readonly Func<DateTime> utcNowProvider;
    private readonly Func<int, bool> isProcessStillRunning;
    private readonly Func<bool> shouldYieldDispatch;
    private readonly Action<string> logInfo;
    private readonly Action<Exception, string> logError;
    private readonly ConcurrentQueue<WatcherPollCompletion> pendingWatcherCompletions = new();
    private readonly ConcurrentQueue<RuntimeProcessorCommand> pendingRuntimeCommands = new();
    private readonly object lifecycleLock = new();
    private readonly AutoResetEvent watcherLoopSignal = new(false);
    private readonly WatcherRuntimeProcessor runtimeProcessor = new(RuntimePendingMenuGraceDuration);
    private DateTime nextUiScalePatchAttemptUtc = DateTime.MinValue;
    private bool uiScalePatchInFlight;
    private int currentRunPhaseValue;
    private long readyWatcherPollIntervalTicks = WatcherRunningPollInterval.Ticks;
    private int watcherDispatchPending;
    private int uiDispatchSuspended;
    private bool disposed;
    private CancellationTokenSource? watcherLoopCancellation;
    private Task? watcherLoopTask;
    private int? uiScalePatchAppliedProcessId;
    private string? lastUiScalePatchLogKey;
    private long issuedRuntimeCommandSequence;
    private long appliedRuntimeCommandSequence;

    public TerrariaMonitorCoordinator(
        ITerrariaWorldWatcher watcher,
        ITerrariaUiScalePatchApplier uiScalePatchApplier,
        Action<Action> dispatch,
        Action<string>? logInfo = null,
        Action<Exception, string>? logError = null,
        Func<DateTime>? utcNowProvider = null,
        Func<int, bool>? isProcessStillRunning = null,
        Func<bool>? shouldYieldDispatch = null)
    {
        this.watcher = watcher;
        this.uiScalePatchApplier = uiScalePatchApplier;
        this.dispatch = dispatch;
        this.logInfo = logInfo ?? AppLogger.Info;
        this.logError = logError ?? AppLogger.Error;
        this.utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);
        this.isProcessStillRunning = isProcessStillRunning ?? IsProcessStillRunning;
        this.shouldYieldDispatch = shouldYieldDispatch ?? (() => false);
        CurrentSnapshot = new TerrariaWatchSnapshot(
            false,
            null,
            false,
            null,
            TerrariaBossStates.Unknown,
            TerrariaWorldGenerationState.Unknown,
            false,
            "waiting for Terraria.exe");
        WatcherPollInterval = WatcherProcessLookupInterval;
        ProcessLookupInterval = WatcherProcessLookupInterval;
    }

    public TerrariaWatchSnapshot CurrentSnapshot { get; private set; }

    public TerrariaWatcherDiagnostics CurrentDiagnostics { get; private set; } =
        TerrariaWatcherDiagnosticsDefaults.Empty;

    public TimeSpan WatcherPollInterval { get; private set; }

    public TimeSpan ProcessLookupInterval { get; private set; }

    public event Action<WatcherPollNotification>? WatcherPollCompleted;

    public event Action<TerrariaUiScalePatchResult>? UiScalePatchCompleted;

    public void Tick(
        SplitTimerPhase runPhase,
        bool patchEnabled)
    {
        if (disposed)
        {
            return;
        }

        UpdateRunPhase(runPhase);
        StartWatcherLoopIfNeeded();
        ScheduleTerrariaUiScalePatch(patchEnabled);
    }

    public void UpdateRunPhase(SplitTimerPhase runPhase)
    {
        Volatile.Write(ref currentRunPhaseValue, (int)runPhase);
    }

    public void UpdateReadyWatcherPollInterval(TimeSpan interval)
    {
        TimeSpan normalized = interval <= TimeSpan.Zero ? WatcherRunningPollInterval : interval;
        long newTicks = normalized.Ticks;
        long previousTicks = Volatile.Read(ref readyWatcherPollIntervalTicks);
        if (previousTicks == newTicks)
        {
            return;
        }

        Volatile.Write(ref readyWatcherPollIntervalTicks, newTicks);
        watcherLoopSignal.Set();
    }

    public void ApplyUiDispatchSuspended(bool suspended)
    {
        int nextValue = suspended ? 1 : 0;
        int previousValue = Interlocked.Exchange(ref uiDispatchSuspended, nextValue);
        if (previousValue == nextValue)
        {
            return;
        }

        watcherLoopSignal.Set();
        if (!suspended && !pendingWatcherCompletions.IsEmpty)
        {
            RequestWatcherCompletionDispatch();
        }
    }

    public void ResetUiScalePatchState()
    {
        nextUiScalePatchAttemptUtc = DateTime.MinValue;
        uiScalePatchAppliedProcessId = null;
        lastUiScalePatchLogKey = null;
    }

    public long SetRuntimeDefinitions(IReadOnlyList<BossSplitDefinition> definitions)
    {
        return SubmitRuntimeCommand(RuntimeCommand.SetDefinitions(definitions));
    }

    public long ResetRuntimeState()
    {
        return SubmitRuntimeCommand(RuntimeCommand.Reset());
    }

    public long ClearPendingMenuActions()
    {
        return SubmitRuntimeCommand(RuntimeCommand.ClearPendingMenuActions());
    }

    public long SubmitRuntimeCommand(RuntimeCommand command)
    {
        return QueueRuntimeCommand(command);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        CancellationTokenSource? cancellation;
        Task? loopTask;
        lock (lifecycleLock)
        {
            disposed = true;
            cancellation = watcherLoopCancellation;
            loopTask = watcherLoopTask;
        }

        cancellation?.Cancel();
        bool loopCompleted = loopTask is null;
        try
        {
            loopCompleted = loopTask?.Wait(TimeSpan.FromMilliseconds(500)) ?? true;
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(error => error is OperationCanceledException))
        {
            loopCompleted = true;
        }
        catch (ObjectDisposedException)
        {
            loopCompleted = true;
        }

        watcher.Dispose();
        watcherLoopSignal.Dispose();
        if (loopCompleted)
        {
            cancellation?.Dispose();
        }
    }

    internal static TimeSpan GetNextWatcherPollInterval(TerrariaWatchSnapshot snapshot, SplitTimerPhase runPhase)
    {
        return GetNextWatcherPollInterval(snapshot, runPhase, WatcherIdlePollInterval);
    }

    internal static TimeSpan GetNextWatcherPollInterval(
        TerrariaWatchSnapshot snapshot,
        SplitTimerPhase runPhase,
        TimeSpan readyPollInterval)
    {
        if (!snapshot.IsAttached)
        {
            return WatcherProcessLookupInterval;
        }

        if (!snapshot.IsReady)
        {
            return WatcherScanPollInterval;
        }

        return readyPollInterval <= TimeSpan.Zero ? WatcherIdlePollInterval : readyPollInterval;
    }

    private void StartWatcherLoopIfNeeded()
    {
        lock (lifecycleLock)
        {
            if (disposed || watcherLoopTask is not null)
            {
                return;
            }

            watcherLoopCancellation = new CancellationTokenSource();
            CancellationToken token = watcherLoopCancellation.Token;
            watcherLoopTask = Task.Factory.StartNew(
                () => RunWatcherLoop(token),
                token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
    }

    private void RunWatcherLoop(CancellationToken cancellationToken)
    {
        using HighResolutionTimerPeriod? timerPeriod = HighResolutionTimerPeriod.TryBegin(1);
        while (!cancellationToken.IsCancellationRequested)
        {
            if (IsUiDispatchSuspended())
            {
                int suspendedWaitResult = WaitHandle.WaitAny(
                    [cancellationToken.WaitHandle, watcherLoopSignal],
                    WatcherScanPollInterval);
                if (suspendedWaitResult == 0)
                {
                    return;
                }

                continue;
            }

            RuntimeCommandDrainResult commandResult = DrainRuntimeCommands();
            WatcherPollCompletion completion = PollWatcher(
                commandResult.LatestAppliedSequence,
                commandResult.Events);
            QueueWatcherCompletion(completion);

            int signaled = WaitHandle.WaitAny(
                [cancellationToken.WaitHandle, watcherLoopSignal],
                completion.NextPollInterval);
            if (signaled == 0)
            {
                return;
            }
        }
    }

    private WatcherPollCompletion PollWatcher(
        long runtimeCommandSequence,
        IReadOnlyList<RunEvent> commandEvents)
    {
        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            TerrariaWatchSnapshot snapshot = watcher.Poll();
            TerrariaWatcherDiagnostics diagnostics = watcher.GetDiagnostics();
            long completedTimestamp = Stopwatch.GetTimestamp();
            RuntimeProcessorTickResult runtimeTickResult = runtimeProcessor.Tick(
                snapshot,
                completedTimestamp,
                commandEvents);
            TimeSpan nextPollInterval = GetNextWatcherPollInterval(snapshot, GetCurrentRunPhase(), GetReadyWatcherPollInterval());
            return new WatcherPollCompletion(
                snapshot,
                diagnostics,
                runtimeTickResult.Snapshot,
                runtimeTickResult.Events,
                runtimeCommandSequence,
                Stopwatch.GetElapsedTime(startTimestamp, completedTimestamp),
                completedTimestamp,
                nextPollInterval,
                snapshot.IsAttached ? TimeSpan.Zero : nextPollInterval,
                null);
        }
        catch (Exception ex)
        {
            long completedTimestamp = Stopwatch.GetTimestamp();
            var snapshot = new TerrariaWatchSnapshot(
                false,
                null,
                false,
                null,
                TerrariaBossStates.Unknown,
                TerrariaWorldGenerationState.Unknown,
                false,
                $"watcher poll failed: {ex.Message}");
            RuntimeProcessorTickResult runtimeTickResult = runtimeProcessor.Tick(
                snapshot,
                completedTimestamp,
                commandEvents);
            TimeSpan nextPollInterval = GetNextWatcherPollInterval(snapshot, GetCurrentRunPhase(), GetReadyWatcherPollInterval());
            return new WatcherPollCompletion(
                snapshot,
                watcher.GetDiagnostics(),
                runtimeTickResult.Snapshot,
                runtimeTickResult.Events,
                runtimeCommandSequence,
                Stopwatch.GetElapsedTime(startTimestamp, completedTimestamp),
                completedTimestamp,
                nextPollInterval,
                nextPollInterval,
                ex);
        }
    }

    private SplitTimerPhase GetCurrentRunPhase()
    {
        return (SplitTimerPhase)Volatile.Read(ref currentRunPhaseValue);
    }

    private TimeSpan GetReadyWatcherPollInterval()
    {
        long ticks = Volatile.Read(ref readyWatcherPollIntervalTicks);
        return ticks > 0 ? new TimeSpan(ticks) : WatcherIdlePollInterval;
    }

    private long QueueRuntimeCommand(RuntimeCommand command)
    {
        long sequence = Interlocked.Increment(ref issuedRuntimeCommandSequence);
        pendingRuntimeCommands.Enqueue(new RuntimeProcessorCommand(sequence, command));
        watcherLoopSignal.Set();
        StartWatcherLoopIfNeeded();
        return sequence;
    }

    private RuntimeCommandDrainResult DrainRuntimeCommands()
    {
        long latestAppliedSequence = Volatile.Read(ref appliedRuntimeCommandSequence);
        List<RunEvent>? events = null;
        while (pendingRuntimeCommands.TryDequeue(out RuntimeProcessorCommand command))
        {
            IReadOnlyList<RunEvent> commandEvents = runtimeProcessor.ApplyCommand(
                command.Command,
                Stopwatch.GetTimestamp());
            if (commandEvents.Count > 0)
            {
                events ??= new List<RunEvent>();
                events.AddRange(commandEvents);
            }

            latestAppliedSequence = command.Sequence;
        }

        Volatile.Write(ref appliedRuntimeCommandSequence, latestAppliedSequence);
        return new RuntimeCommandDrainResult(latestAppliedSequence, events ?? []);
    }

    private void QueueWatcherCompletion(WatcherPollCompletion completion)
    {
        pendingWatcherCompletions.Enqueue(completion);
        RequestWatcherCompletionDispatch();
    }

    private void RequestWatcherCompletionDispatch()
    {
        if (disposed ||
            IsUiDispatchSuspended() ||
            Interlocked.Exchange(ref watcherDispatchPending, 1) == 1)
        {
            return;
        }

        try
        {
            dispatch(DrainWatcherCompletions);
        }
        catch (ObjectDisposedException)
        {
            Interlocked.Exchange(ref watcherDispatchPending, 0);
        }
        catch (InvalidOperationException)
        {
            Interlocked.Exchange(ref watcherDispatchPending, 0);
        }
    }

    private void DrainWatcherCompletions()
    {
        Interlocked.Exchange(ref watcherDispatchPending, 0);
        if (IsUiDispatchSuspended())
        {
            return;
        }

        int processedCount = 0;
        while (pendingWatcherCompletions.TryDequeue(out WatcherPollCompletion completion))
        {
            CompleteWatcherPoll(completion);
            processedCount++;
            if (processedCount >= MaxWatcherCompletionsPerDispatch || shouldYieldDispatch())
            {
                break;
            }
        }

        if (!pendingWatcherCompletions.IsEmpty)
        {
            RequestWatcherCompletionDispatch();
        }
    }

    private void CompleteWatcherPoll(WatcherPollCompletion completion)
    {
        if (completion.Error is not null)
        {
            logError(completion.Error, "Unhandled watcher poll error.");
        }

        TerrariaWatchSnapshot previousSnapshot = CurrentSnapshot;
        CurrentSnapshot = completion.Snapshot;
        CurrentDiagnostics = completion.Diagnostics;
        WatcherPollInterval = completion.NextPollInterval;
        ProcessLookupInterval = completion.ProcessLookupInterval;
        WatcherPollCompleted?.Invoke(new WatcherPollNotification(
            completion.Snapshot,
            previousSnapshot,
            completion.Diagnostics,
            completion.RuntimeSnapshot,
            completion.RunEvents,
            completion.RuntimeCommandSequence,
            completion.Elapsed,
            completion.CompletedTimestamp,
            completion.NextPollInterval,
            completion.ProcessLookupInterval,
            completion.Error));
    }

    private bool IsUiDispatchSuspended()
    {
        return Volatile.Read(ref uiDispatchSuspended) != 0;
    }

    private void ScheduleTerrariaUiScalePatch(bool patchEnabled)
    {
        if (!patchEnabled)
        {
            uiScalePatchAppliedProcessId = null;
            return;
        }

        if (uiScalePatchAppliedProcessId is int appliedProcessId)
        {
            if (CurrentSnapshot.ProcessId == appliedProcessId ||
                (!CurrentSnapshot.ProcessId.HasValue && isProcessStillRunning(appliedProcessId)))
            {
                return;
            }

            uiScalePatchAppliedProcessId = null;
        }

        if (uiScalePatchInFlight || utcNowProvider() < nextUiScalePatchAttemptUtc)
        {
            return;
        }

        uiScalePatchInFlight = true;
        int? fallbackProcessId = CurrentSnapshot.ProcessId;
        _ = Task.Run(uiScalePatchApplier.TryApply).ContinueWith(task =>
        {
            TerrariaUiScalePatchResult result = task.Status == TaskStatus.RanToCompletion
                ? task.Result
                : new TerrariaUiScalePatchResult(
                    TerrariaUiScalePatchStatus.Failed,
                    fallbackProcessId,
                    task.Exception?.GetBaseException().Message ?? "Unexpected Terraria UI scale patch failure.");

            if (disposed)
            {
                return;
            }

            try
            {
                dispatch(() => CompleteTerrariaUiScalePatch(result));
            }
            catch (ObjectDisposedException)
            {
                uiScalePatchInFlight = false;
            }
            catch (InvalidOperationException)
            {
                uiScalePatchInFlight = false;
            }
        }, TaskScheduler.Default);
    }

    private void CompleteTerrariaUiScalePatch(TerrariaUiScalePatchResult result)
    {
        uiScalePatchInFlight = false;
        nextUiScalePatchAttemptUtc = utcNowProvider() + UiScalePatchRetryInterval;

        if (result.Status == TerrariaUiScalePatchStatus.NoProcess)
        {
            uiScalePatchAppliedProcessId = null;
            UiScalePatchCompleted?.Invoke(result);
            return;
        }

        if (result.IsSuccess && result.ProcessId.HasValue)
        {
            uiScalePatchAppliedProcessId = result.ProcessId.Value;
        }

        LogTerrariaUiScalePatchResult(result);
        UiScalePatchCompleted?.Invoke(result);
    }

    private void LogTerrariaUiScalePatchResult(TerrariaUiScalePatchResult result)
    {
        string logKey = string.Create(
            CultureInfo.InvariantCulture,
            $"{result.Status}:{result.ProcessId}:{result.Message}");
        if (string.Equals(logKey, lastUiScalePatchLogKey, StringComparison.Ordinal))
        {
            return;
        }

        lastUiScalePatchLogKey = logKey;
        string pid = result.ProcessId.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"PID {result.ProcessId.Value}")
            : "no PID";
        logInfo($"Terraria UI scale enhancement {result.Status} for {pid}: {result.Message}");
    }

    private static bool IsProcessStillRunning(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private sealed class HighResolutionTimerPeriod : IDisposable
    {
        private readonly uint milliseconds;

        private HighResolutionTimerPeriod(uint milliseconds)
        {
            this.milliseconds = milliseconds;
        }

        public static HighResolutionTimerPeriod? TryBegin(uint milliseconds)
        {
            try
            {
                return TimeBeginPeriod(milliseconds) == 0
                    ? new HighResolutionTimerPeriod(milliseconds)
                    : null;
            }
            catch (DllNotFoundException)
            {
                return null;
            }
            catch (EntryPointNotFoundException)
            {
                return null;
            }
        }

        public void Dispose()
        {
            try
            {
                _ = TimeEndPeriod(milliseconds);
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint milliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint milliseconds);
    }
}

internal readonly record struct WatcherPollNotification(
    TerrariaWatchSnapshot Snapshot,
    TerrariaWatchSnapshot PreviousSnapshot,
    TerrariaWatcherDiagnostics Diagnostics,
    RuntimeRunSnapshot RuntimeSnapshot,
    IReadOnlyList<RunEvent> RunEvents,
    long RuntimeCommandSequence,
    TimeSpan Elapsed,
    long CompletedTimestamp,
    TimeSpan NextPollInterval,
    TimeSpan ProcessLookupInterval,
    Exception? Error);

internal readonly record struct WatcherPollCompletion(
    TerrariaWatchSnapshot Snapshot,
    TerrariaWatcherDiagnostics Diagnostics,
    RuntimeRunSnapshot RuntimeSnapshot,
    IReadOnlyList<RunEvent> RunEvents,
    long RuntimeCommandSequence,
    TimeSpan Elapsed,
    long CompletedTimestamp,
    TimeSpan NextPollInterval,
    TimeSpan ProcessLookupInterval,
    Exception? Error);

internal readonly record struct RuntimeProcessorCommand(
    long Sequence,
    RuntimeCommand Command);
