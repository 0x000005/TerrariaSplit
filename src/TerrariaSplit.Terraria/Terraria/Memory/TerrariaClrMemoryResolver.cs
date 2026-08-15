using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using TerrariaSplit.MemoryBridge.Protocol;
using Process = System.Diagnostics.Process;

namespace TerrariaSplit.Terraria.Memory;

internal sealed class TerrariaClrMemoryResolver
{
    private static readonly TimeSpan ResolveRetryInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MemoryBridgeTimeout = TimeSpan.FromSeconds(15);
    private readonly MemoryBridgeClient bridgeClient;
    private Process? process;
    private int? processId;
    private DateTime nextResolveAttemptUtc = DateTime.MinValue;
    private TerrariaRuntimeMemoryLayout? runtimeLayout;
    private int resolveAttempts;
    private DateTime? lastAttemptUtc;
    private DateTime? lastSuccessUtc;
    private int? lastExitCode;
    private string? lastError;

    public TerrariaClrMemoryResolver(MemoryBridgeClient? bridgeClient = null)
    {
        this.bridgeClient = bridgeClient ?? new MemoryBridgeClient();
    }

    public TerrariaLayoutProbeDiagnostics ProbeDiagnostics => new(
        resolveAttempts,
        lastAttemptUtc,
        lastSuccessUtc,
        LayoutStatus,
        lastExitCode,
        lastError,
        runtimeLayout?.ResolvedFieldCount ?? 0);

    public string LayoutStatus
    {
        get
        {
            if (runtimeLayout is null)
            {
                return DateTime.UtcNow < nextResolveAttemptUtc && resolveAttempts > 0
                    ? "retrying"
                    : "unavailable";
            }

            return IsPartial(runtimeLayout) ? "partial" : "resolved";
        }
    }

    public void SetProcess(Process? targetProcess)
    {
        int? targetProcessId = targetProcess is null ? null : GetProcessId(targetProcess);
        if (targetProcessId != processId)
        {
            Reset();
            processId = targetProcessId;
        }

        process = targetProcess;
    }

    public void Reset()
    {
        runtimeLayout = null;
        nextResolveAttemptUtc = DateTime.MinValue;
        resolveAttempts = 0;
        lastAttemptUtc = null;
        lastSuccessUtc = null;
        lastExitCode = null;
        lastError = null;
    }

    public void ResetLayout()
    {
        runtimeLayout = null;
        nextResolveAttemptUtc = DateTime.MinValue;
    }

    public bool TryGetRuntimeLayout(IProcessMemoryReader memory, out TerrariaRuntimeMemoryLayout layout)
    {
        layout = null!;
        if (memory.Is64Bit)
        {
            lastError = "x64 Terraria process is not supported by the x86 MemoryBridge resolver";
            return false;
        }

        if (runtimeLayout is not null)
        {
            layout = runtimeLayout;
            return true;
        }

        if (process is null || processId is null || DateTime.UtcNow < nextResolveAttemptUtc)
        {
            return false;
        }

        nextResolveAttemptUtc = DateTime.UtcNow + ResolveRetryInterval;
        resolveAttempts++;
        lastAttemptUtc = DateTime.UtcNow;
        if (TryResolveRuntimeLayoutWithMemoryBridge(processId.Value, out TerrariaRuntimeMemoryLayout? resolvedLayout) &&
            resolvedLayout is not null)
        {
            runtimeLayout = resolvedLayout;
            lastSuccessUtc = DateTime.UtcNow;
            lastError = null;
            layout = resolvedLayout;
            return true;
        }

        return false;
    }

    public bool TryPredictRandomSeedBatch(
        int count,
        out IReadOnlyList<string> seedTexts,
        out string detail)
    {
        seedTexts = Array.Empty<string>();
        detail = string.Empty;
        if (count is < 1 or > 256)
        {
            detail = "Random seed batch size must be between 1 and 256.";
            return false;
        }
        if (process is null || processId is null)
        {
            detail = "Terraria process is unavailable.";
            return false;
        }

        MemoryBridgeCommandResult commandResult = bridgeClient.Execute(
            MemoryBridgeCommands.RandomSeedBatch,
            MemoryBridgeTimeout,
            processId.Value.ToString(CultureInfo.InvariantCulture),
            count.ToString(CultureInfo.InvariantCulture));
        if (commandResult.TimedOut)
        {
            detail = "MemoryBridge random seed batch timed out.";
            return false;
        }

        try
        {
            RandomSeedBatchResponse? response = JsonSerializer.Deserialize<RandomSeedBatchResponse>(
                commandResult.StandardOutput);
            if (!commandResult.Succeeded || response?.Success != true || response.Seeds is null)
            {
                detail = response?.Error ?? commandResult.FailureDetail("MemoryBridge random seed batch failed.");
                return false;
            }
            if (response.Seeds.Count != count ||
                response.Seeds.Any(seed =>
                    !int.TryParse(
                        seed,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int parsed) ||
                    parsed < 0))
            {
                detail = "MemoryBridge returned an invalid random seed batch.";
                return false;
            }

            seedTexts = response.Seeds.ToArray();
            detail =
                $"predicted {seedTexts.Count} seeds from Terraria UI thread {response.OsThreadId?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}";
            return true;
        }
        catch (JsonException ex)
        {
            detail = commandResult.FailureDetail(ex.Message);
            return false;
        }
    }

    private static int? GetProcessId(Process targetProcess)
    {
        try
        {
            return targetProcess.HasExited ? null : targetProcess.Id;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private bool TryResolveRuntimeLayoutWithMemoryBridge(
        int targetProcessId,
        out TerrariaRuntimeMemoryLayout? layout)
    {
        layout = null;
        long startedTimestamp = Stopwatch.GetTimestamp();
        FileAppLogger.Instance.Info(
            $"MemoryBridge runtime-layout probe starting for Terraria PID {targetProcessId}; attempt={resolveAttempts}.");

        MemoryBridgeCommandResult commandResult = bridgeClient.Execute(
            MemoryBridgeCommands.RuntimeLayout,
            MemoryBridgeTimeout,
            targetProcessId.ToString(CultureInfo.InvariantCulture));
        lastExitCode = commandResult.ExitCode;
        if (commandResult.TimedOut)
        {
            lastError = "MemoryBridge timed out";
            LogProbeCompletion(targetProcessId, startedTimestamp);
            return false;
        }

        try
        {
            RuntimeLayoutResponse? response = JsonSerializer.Deserialize<RuntimeLayoutResponse>(
                commandResult.StandardOutput);
            if (commandResult.Succeeded && response?.Success == true && response.Layout is not null)
            {
                layout = response.Layout.ToRuntimeMemoryLayout();
                LogProbeCompletion(targetProcessId, startedTimestamp, layout.ResolvedFieldCount);
                return true;
            }

            lastError = response?.Error ?? commandResult.FailureDetail("MemoryBridge returned no runtime layout");
        }
        catch (JsonException ex)
        {
            lastError = commandResult.FailureDetail(ex.Message);
        }

        LogProbeCompletion(targetProcessId, startedTimestamp);
        return false;
    }

    private void LogProbeCompletion(int targetProcessId, long startedTimestamp, int? resolvedFieldCount = null)
    {
        TimeSpan elapsed = Stopwatch.GetElapsedTime(startedTimestamp);
        FileAppLogger.Instance.Info(
            $"MemoryBridge runtime-layout probe completed for Terraria PID {targetProcessId}; " +
            $"elapsedMs={elapsed.TotalMilliseconds:F0}, exitCode={lastExitCode?.ToString(CultureInfo.InvariantCulture) ?? "<none>"}, " +
            $"fields={resolvedFieldCount?.ToString(CultureInfo.InvariantCulture) ?? "<none>"}, " +
            $"error={lastError ?? "<none>"}.");
    }

    private static bool IsPartial(TerrariaRuntimeMemoryLayout layout)
    {
        return !layout.HasCore ||
            layout.Boss.ResolvedFactCount == 0 ||
            layout.Item is null ||
            layout.Npc is null ||
            layout.Biome is null ||
            layout.SeedUi is null ||
            !layout.WorldGeneration.HasAnySource;
    }
}
