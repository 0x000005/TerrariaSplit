using System.Globalization;

namespace TerrariaSplit.Application;

internal sealed class UiScalePatchScheduler
{
    private readonly ITerrariaUiScalePatchApplier patchApplier;
    private readonly Action<Action> dispatch;
    private readonly Func<DateTime> utcNowProvider;
    private readonly Func<int, bool> isProcessStillRunning;
    private readonly Action<string> logInfo;
    private readonly TimeSpan retryInterval;
    private DateTime nextAttemptUtc = DateTime.MinValue;
    private bool inFlight;
    private int? appliedProcessId;
    private string? lastLogKey;
    private bool disposed;

    public UiScalePatchScheduler(
        ITerrariaUiScalePatchApplier patchApplier,
        Action<Action> dispatch,
        Func<DateTime> utcNowProvider,
        Func<int, bool> isProcessStillRunning,
        Action<string> logInfo,
        TimeSpan retryInterval)
    {
        this.patchApplier = patchApplier;
        this.dispatch = dispatch;
        this.utcNowProvider = utcNowProvider;
        this.isProcessStillRunning = isProcessStillRunning;
        this.logInfo = logInfo;
        this.retryInterval = retryInterval;
    }

    public event Action<TerrariaUiScalePatchResult>? Completed;

    public void Reset()
    {
        nextAttemptUtc = DateTime.MinValue;
        appliedProcessId = null;
        lastLogKey = null;
    }

    public void Dispose()
    {
        disposed = true;
    }

    public void Schedule(bool patchEnabled, TerrariaWatchSnapshot currentSnapshot)
    {
        if (!patchEnabled)
        {
            appliedProcessId = null;
            return;
        }

        if (appliedProcessId is int appliedProcess)
        {
            if (currentSnapshot.ProcessId == appliedProcess ||
                (!currentSnapshot.ProcessId.HasValue && isProcessStillRunning(appliedProcess)))
            {
                return;
            }

            appliedProcessId = null;
        }

        if (inFlight || utcNowProvider() < nextAttemptUtc)
        {
            return;
        }

        inFlight = true;
        int? fallbackProcessId = currentSnapshot.ProcessId;
        _ = Task.Run(patchApplier.TryApply).ContinueWith(task =>
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
                dispatch(() => Complete(result));
            }
            catch (ObjectDisposedException)
            {
                inFlight = false;
            }
            catch (InvalidOperationException)
            {
                inFlight = false;
            }
        }, TaskScheduler.Default);
    }

    private void Complete(TerrariaUiScalePatchResult result)
    {
        inFlight = false;
        nextAttemptUtc = utcNowProvider() + retryInterval;

        if (result.Status == TerrariaUiScalePatchStatus.NoProcess)
        {
            appliedProcessId = null;
            Completed?.Invoke(result);
            return;
        }

        if (result.IsSuccess && result.ProcessId.HasValue)
        {
            appliedProcessId = result.ProcessId.Value;
        }

        LogResult(result);
        Completed?.Invoke(result);
    }

    private void LogResult(TerrariaUiScalePatchResult result)
    {
        string logKey = string.Create(
            CultureInfo.InvariantCulture,
            $"{result.Status}:{result.ProcessId}:{result.Message}");
        if (string.Equals(logKey, lastLogKey, StringComparison.Ordinal))
        {
            return;
        }

        lastLogKey = logKey;
        string pid = result.ProcessId.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"PID {result.ProcessId.Value}")
            : "no PID";
        logInfo($"Terraria UI scale enhancement {result.Status} for {pid}: {result.Message}");
    }
}
