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
    private readonly TerrariaRuntimeLayoutCache layoutCache;
    private Process? process;
    private TerrariaProcessIdentity? processIdentity;
    private DateTime nextResolveAttemptUtc = DateTime.MinValue;
    private TerrariaRuntimeMemoryLayout? runtimeLayout;
    private TerrariaRuntimeMemoryLayout? invalidatedLayout;
    private Task<TerrariaRuntimeMemoryLayout>? pendingResolution;
    private int resolveAttempts;
    private DateTime? lastAttemptUtc;
    private DateTime? lastSuccessUtc;
    private int? lastExitCode;
    private string? lastError;

    public TerrariaClrMemoryResolver(MemoryBridgeClient? bridgeClient = null, TerrariaRuntimeLayoutCache? layoutCache = null)
    {
        this.bridgeClient = bridgeClient ?? new MemoryBridgeClient();
        this.layoutCache = layoutCache ?? TerrariaRuntimeLayoutCache.Shared;
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
        TerrariaProcessIdentity? targetIdentity = targetProcess is null ? null : GetProcessIdentity(targetProcess);
        if (targetIdentity != processIdentity)
        {
            Reset();
            processIdentity = targetIdentity;
        }

        process = targetProcess;
    }

    public void Reset()
    {
        runtimeLayout = null;
        invalidatedLayout = null;
        pendingResolution = null;
        nextResolveAttemptUtc = DateTime.MinValue;
        resolveAttempts = 0;
        lastAttemptUtc = null;
        lastSuccessUtc = null;
        lastExitCode = null;
        lastError = null;
    }

    public void ResetLayout()
    {
        invalidatedLayout = runtimeLayout ?? invalidatedLayout;
        runtimeLayout = null;
        pendingResolution = null;
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

        if (process is null || processIdentity is not { } identity || DateTime.UtcNow < nextResolveAttemptUtc)
        {
            return false;
        }

        nextResolveAttemptUtc = DateTime.UtcNow + ResolveRetryInterval;
        resolveAttempts++;
        lastAttemptUtc = DateTime.UtcNow;
        long started = Stopwatch.GetTimestamp();
        try
        {
            pendingResolution ??= layoutCache.GetAsync(identity, invalidatedLayout, CancellationToken.None);
            if (!pendingResolution.IsCompleted)
            {
                lastError = "Waiting for Terraria loading and shared runtime initialization.";
                return false;
            }
            TerrariaRuntimeMemoryLayout resolvedLayout = pendingResolution.GetAwaiter().GetResult();
            pendingResolution = null;
            runtimeLayout = resolvedLayout;
            invalidatedLayout = null;
            lastSuccessUtc = DateTime.UtcNow;
            lastError = null;
            lastExitCode = 0;
            layout = resolvedLayout;
            AppLog.ForSource().LogInformation($"Terraria shared runtime layout ready; pid={identity.ProcessId}; elapsedMs={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0}.");
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            pendingResolution = null;
            lastError = ex.Message;
            lastExitCode = null;
            AppLog.ForSource().LogInformation($"Terraria shared runtime layout unavailable; pid={identity.ProcessId}; error={lastError}.");
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
        int? processId = processIdentity?.ProcessId;
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

    private static TerrariaProcessIdentity? GetProcessIdentity(Process targetProcess)
    {
        try
        {
            return targetProcess.HasExited ? null : TerrariaProcessIdentity.FromProcess(targetProcess);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
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
