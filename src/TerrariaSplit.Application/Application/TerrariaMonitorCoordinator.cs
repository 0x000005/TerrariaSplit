using System.Diagnostics;

namespace TerrariaSplit.Application;

public sealed class TerrariaMonitorCoordinator : IDisposable
{
    private static readonly TimeSpan RuntimePendingMenuGraceDuration = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan WatcherRunningPollInterval = TimeSpan.FromMilliseconds(5);
    private static readonly TimeSpan WatcherIdlePollInterval = TimeSpan.FromMilliseconds(5);
    private static readonly TimeSpan WatcherScanPollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan WatcherProcessLookupInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan UiScalePatchRetryInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan WatcherHeartbeatInterval = TimeSpan.FromMilliseconds(250);
    private const int MaxWatcherCompletionsPerDispatch = 8;

    private readonly ITerrariaWorldWatcher watcher;
    private readonly Action<Exception, string> logError;
    private readonly WatcherCompletionDispatcher watcherCompletions;
    private readonly UiScalePatchScheduler uiScalePatchScheduler;
    private readonly WatcherRuntimeProcessor runtimeProcessor;
    private readonly RuntimeCommandSequencer runtimeCommands;
    private readonly WatcherLoop watcherLoop;
    private int currentRunPhaseValue;
    private long readyWatcherPollIntervalTicks = WatcherRunningPollInterval.Ticks;
    private bool disposed;

    public TerrariaMonitorCoordinator(
        ITerrariaWorldWatcher watcher,
        ITerrariaUiScalePatchApplier uiScalePatchApplier,
        Action<Action> dispatch,
        IAppLogger? logger = null,
        Action<string>? logInfo = null,
        Action<Exception, string>? logError = null,
        Func<DateTime>? utcNowProvider = null,
        Func<int, bool>? isProcessStillRunning = null,
        Func<bool>? shouldYieldDispatch = null,
        Action<TimeSpan, long>? recordPoll = null)
    {
        this.watcher = watcher;
        logger ??= NullAppLogger.Instance;
        Action<string> infoLogger = logInfo ?? logger.Info;
        this.logError = logError ?? logger.Error;
        Func<DateTime> nowProvider = utcNowProvider ?? (() => DateTime.UtcNow);
        Func<int, bool> processRunning = isProcessStillRunning ?? IsProcessStillRunning;
        runtimeProcessor = new WatcherRuntimeProcessor(RuntimePendingMenuGraceDuration);
        runtimeCommands = new RuntimeCommandSequencer(runtimeProcessor);
        watcherCompletions = new WatcherCompletionDispatcher(
            dispatch,
            shouldYieldDispatch ?? (() => false),
            CompleteWatcherPoll,
            MaxWatcherCompletionsPerDispatch);
        uiScalePatchScheduler = new UiScalePatchScheduler(
            uiScalePatchApplier,
            dispatch,
            nowProvider,
            processRunning,
            infoLogger,
            UiScalePatchRetryInterval);
        watcherLoop = new WatcherLoop(
            watcherCompletions,
            runtimeCommands,
            PollWatcher,
            watcherCompletions.Queue,
            (completion, lastPublished, heartbeatInterval) =>
                ShouldPublishWatcherCompletion(completion, lastPublished, heartbeatInterval),
            recordPoll,
            WatcherScanPollInterval,
            WatcherHeartbeatInterval);
        CurrentSnapshot = new TerrariaWatchSnapshot(
            false,
            null,
            false,
            null,
            TerrariaGameFacts.Unknown,
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

    public event Action<TerrariaUiScalePatchResult>? UiScalePatchCompleted
    {
        add => uiScalePatchScheduler.Completed += value;
        remove => uiScalePatchScheduler.Completed -= value;
    }

    public void Tick(
        SplitTimerPhase runPhase,
        bool patchEnabled)
    {
        if (disposed)
        {
            return;
        }

        UpdateRunPhase(runPhase);
        watcherLoop.StartIfNeeded();
        uiScalePatchScheduler.Schedule(patchEnabled, CurrentSnapshot);
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
        watcherLoop.Signal();
    }

    public void ApplyUiDispatchSuspended(bool suspended)
    {
        if (!watcherCompletions.SetSuspended(suspended))
        {
            return;
        }

        watcherLoop.Signal();
    }

    public void ResetUiScalePatchState()
    {
        uiScalePatchScheduler.Reset();
    }

    public long SetRuntimeDefinitions(IReadOnlyList<SplitDefinition> definitions)
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

        disposed = true;
        watcherCompletions.Dispose();
        uiScalePatchScheduler.Dispose();

        bool loopCompleted = watcherLoop.Stop(TimeSpan.FromMilliseconds(500));

        if (loopCompleted)
        {
            watcher.Dispose();
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

    internal static bool ShouldPublishWatcherCompletion(
        in WatcherPollCompletion completion,
        in WatcherPublishState lastPublished,
        TimeSpan heartbeatInterval)
    {
        if (!lastPublished.HasPublished ||
            completion.Error is not null ||
            completion.RunEvents.Count > 0 ||
            completion.RuntimeCommandSequence != lastPublished.RuntimeCommandSequence ||
            completion.Snapshot != lastPublished.Snapshot ||
            !ReferenceEquals(completion.RuntimeSnapshot, lastPublished.RuntimeSnapshot) ||
            completion.Diagnostics != lastPublished.Diagnostics)
        {
            return true;
        }

        return Stopwatch.GetElapsedTime(lastPublished.PublishedTimestamp, completion.CompletedTimestamp) >=
            heartbeatInterval;
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
                TerrariaGameFacts.Unknown,
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
        if (command.Kind == RuntimeCommandKind.SetDefinitions)
        {
            watcher.SetObservedFactKeys(RuntimeObservedFactKeys.FromDefinitions(command.Definitions));
        }

        return watcherLoop.QueueCommand(command);
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

}

public readonly record struct WatcherPollNotification(
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

internal readonly record struct WatcherPublishState(
    bool HasPublished,
    TerrariaWatchSnapshot Snapshot,
    RuntimeRunSnapshot? RuntimeSnapshot,
    TerrariaWatcherDiagnostics Diagnostics,
    long RuntimeCommandSequence,
    long PublishedTimestamp)
{
    public static WatcherPublishState Empty => default;

    public static WatcherPublishState FromCompletion(in WatcherPollCompletion completion)
    {
        return new WatcherPublishState(
            HasPublished: true,
            completion.Snapshot,
            completion.RuntimeSnapshot,
            completion.Diagnostics,
            completion.RuntimeCommandSequence,
            completion.CompletedTimestamp);
    }
}
