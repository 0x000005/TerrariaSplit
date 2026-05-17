using System.Diagnostics;
using System.Globalization;

namespace TerrariaSplit;

internal sealed class TerrariaMonitorCoordinator : IDisposable
{
    private static readonly TimeSpan WatcherRunningPollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan WatcherIdlePollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan WatcherScanPollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan WatcherProcessLookupInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan UiScalePatchRetryInterval = TimeSpan.FromSeconds(2);

    private readonly ITerrariaWorldWatcher watcher;
    private readonly ITerrariaUiScalePatchApplier uiScalePatchApplier;
    private readonly Action<Action> dispatch;
    private readonly Func<DateTime> utcNowProvider;
    private readonly Func<int, bool> isProcessStillRunning;
    private readonly Action<string> logInfo;
    private readonly Action<Exception, string> logError;
    private DateTime nextWatcherPollUtc = DateTime.MinValue;
    private DateTime nextUiScalePatchAttemptUtc = DateTime.MinValue;
    private bool watcherPollInFlight;
    private bool uiScalePatchInFlight;
    private SplitTimerPhase currentRunPhase;
    private bool disposed;
    private int? uiScalePatchAppliedProcessId;
    private string? lastUiScalePatchLogKey;

    public TerrariaMonitorCoordinator(
        ITerrariaWorldWatcher watcher,
        ITerrariaUiScalePatchApplier uiScalePatchApplier,
        Action<Action> dispatch,
        Action<string>? logInfo = null,
        Action<Exception, string>? logError = null,
        Func<DateTime>? utcNowProvider = null,
        Func<int, bool>? isProcessStillRunning = null)
    {
        this.watcher = watcher;
        this.uiScalePatchApplier = uiScalePatchApplier;
        this.dispatch = dispatch;
        this.logInfo = logInfo ?? AppLogger.Info;
        this.logError = logError ?? AppLogger.Error;
        this.utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);
        this.isProcessStillRunning = isProcessStillRunning ?? IsProcessStillRunning;
        CurrentSnapshot = new TerrariaWatchSnapshot(
            false,
            null,
            false,
            null,
            TerrariaBossStates.Unknown,
            false,
            "waiting for Terraria.exe");
        WatcherPollInterval = WatcherProcessLookupInterval;
        ProcessLookupInterval = WatcherProcessLookupInterval;
    }

    public TerrariaWatchSnapshot CurrentSnapshot { get; private set; }

    public TimeSpan WatcherPollInterval { get; private set; }

    public TimeSpan ProcessLookupInterval { get; private set; }

    public event Action<WatcherPollNotification>? WatcherPollCompleted;

    public event Action<TerrariaUiScalePatchResult>? UiScalePatchCompleted;

    public void Tick(SplitTimerPhase runPhase, bool patchEnabled)
    {
        if (disposed)
        {
            return;
        }

        currentRunPhase = runPhase;
        ScheduleWatcherPoll();
        ScheduleTerrariaUiScalePatch(patchEnabled);
    }

    public void ResetUiScalePatchState()
    {
        nextUiScalePatchAttemptUtc = DateTime.MinValue;
        uiScalePatchAppliedProcessId = null;
        lastUiScalePatchLogKey = null;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        watcher.Dispose();
    }

    internal static TimeSpan GetNextWatcherPollInterval(TerrariaWatchSnapshot snapshot, SplitTimerPhase runPhase)
    {
        if (!snapshot.IsAttached)
        {
            return WatcherProcessLookupInterval;
        }

        if (!snapshot.IsReady)
        {
            return WatcherScanPollInterval;
        }

        return runPhase == SplitTimerPhase.Running
            ? WatcherRunningPollInterval
            : WatcherIdlePollInterval;
    }

    private void ScheduleWatcherPoll()
    {
        if (watcherPollInFlight || utcNowProvider() < nextWatcherPollUtc)
        {
            return;
        }

        watcherPollInFlight = true;
        long startTimestamp = Stopwatch.GetTimestamp();
        _ = Task.Run(() =>
        {
            try
            {
                TerrariaWatchSnapshot snapshot = watcher.Poll();
                return new WatcherPollCompletion(snapshot, Stopwatch.GetElapsedTime(startTimestamp), null);
            }
            catch (Exception ex)
            {
                return new WatcherPollCompletion(
                    new TerrariaWatchSnapshot(
                        false,
                        null,
                        false,
                        null,
                        TerrariaBossStates.Unknown,
                        false,
                        $"watcher poll failed: {ex.Message}"),
                    Stopwatch.GetElapsedTime(startTimestamp),
                    ex);
            }
        }).ContinueWith(task =>
        {
            if (disposed)
            {
                return;
            }

            try
            {
                dispatch(() => CompleteWatcherPoll(task.Result));
            }
            catch (ObjectDisposedException)
            {
                watcherPollInFlight = false;
            }
            catch (InvalidOperationException)
            {
                watcherPollInFlight = false;
            }
        }, TaskScheduler.Default);
    }

    private void CompleteWatcherPoll(WatcherPollCompletion completion)
    {
        watcherPollInFlight = false;
        if (completion.Error is not null)
        {
            logError(completion.Error, "Unhandled watcher poll error.");
        }

        TerrariaWatchSnapshot previousSnapshot = CurrentSnapshot;
        CurrentSnapshot = completion.Snapshot;
        TimeSpan nextPollInterval = GetNextWatcherPollInterval(CurrentSnapshot, currentRunPhase);
        nextWatcherPollUtc = utcNowProvider() + nextPollInterval;
        WatcherPollInterval = nextPollInterval;
        ProcessLookupInterval = CurrentSnapshot.IsAttached ? TimeSpan.Zero : nextPollInterval;
        WatcherPollCompleted?.Invoke(new WatcherPollNotification(
            completion.Snapshot,
            previousSnapshot,
            completion.Elapsed,
            nextPollInterval,
            ProcessLookupInterval,
            completion.Error));
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
}

internal readonly record struct WatcherPollNotification(
    TerrariaWatchSnapshot Snapshot,
    TerrariaWatchSnapshot PreviousSnapshot,
    TimeSpan Elapsed,
    TimeSpan NextPollInterval,
    TimeSpan ProcessLookupInterval,
    Exception? Error);

internal readonly record struct WatcherPollCompletion(
    TerrariaWatchSnapshot Snapshot,
    TimeSpan Elapsed,
    Exception? Error);
