using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Process = System.Diagnostics.Process;

namespace TerrariaSplit.Terraria.Memory;

internal sealed class TerrariaClrMemoryResolver
{
    private static readonly TimeSpan ResolveRetryInterval = TimeSpan.FromSeconds(2);
    private const int MemoryProbeTimeoutMilliseconds = 15000;
    private const string MemoryBridgeExecutableName = "TerrariaSplit.MemoryBridge.exe";

    private Process? process;
    private int? processId;
    private DateTime nextResolveAttemptUtc = DateTime.MinValue;
    private TerrariaRuntimeMemoryLayout? runtimeLayout;
    private int resolveAttempts;
    private DateTime? lastAttemptUtc;
    private DateTime? lastSuccessUtc;
    private int? lastExitCode;
    private string? lastError;

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
        if (TryResolveRuntimeLayoutWithMemoryProbe(processId.Value, out TerrariaRuntimeMemoryLayout? resolvedLayout) &&
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

    private bool TryResolveRuntimeLayoutWithMemoryProbe(
        int targetProcessId,
        out TerrariaRuntimeMemoryLayout? layout)
    {
        layout = null;
        string? probePath = FindMemoryProbeExecutable();
        if (probePath is null)
        {
            lastExitCode = null;
            lastError = "TerrariaSplit.MemoryBridge.exe not found";
            return false;
        }

        long startedTimestamp = Stopwatch.GetTimestamp();
        StaticAppLogger.Instance.Info(
            $"MemoryBridge runtime-layout probe starting for Terraria PID {targetProcessId}; attempt={resolveAttempts}.");
        try
        {
            using Process? probe = StartMemoryProbe(probePath, targetProcessId);
            if (probe is null)
            {
                lastExitCode = null;
                lastError = "failed to start TerrariaSplit.MemoryBridge.exe";
                LogProbeCompletion(targetProcessId, startedTimestamp);
                return false;
            }

            if (!probe.WaitForExit(MemoryProbeTimeoutMilliseconds))
            {
                TryKill(probe);
                lastExitCode = null;
                lastError = "MemoryBridge timed out";
                LogProbeCompletion(targetProcessId, startedTimestamp);
                return false;
            }

            lastExitCode = probe.ExitCode;
            string output = probe.StandardOutput.ReadToEnd();
            string errorOutput = probe.StandardError.ReadToEnd();
            RuntimeLayoutProbeResponse? response =
                JsonSerializer.Deserialize<RuntimeLayoutProbeResponse>(output.Trim());
            if (response?.Success == true && response.Layout is not null)
            {
                layout = response.Layout.ToRuntimeMemoryLayout();
                LogProbeCompletion(targetProcessId, startedTimestamp, layout.ResolvedFieldCount);
                return true;
            }

            lastError = response?.Error ??
                (string.IsNullOrWhiteSpace(errorOutput) ? "MemoryBridge returned no runtime layout" : errorOutput.Trim());
        }
        catch (InvalidOperationException ex)
        {
            lastExitCode = null;
            lastError = ex.Message;
        }
        catch (Win32Exception ex)
        {
            lastExitCode = null;
            lastError = ex.Message;
        }
        catch (IOException ex)
        {
            lastExitCode = null;
            lastError = ex.Message;
        }
        catch (JsonException ex)
        {
            lastExitCode = null;
            lastError = ex.Message;
        }

        LogProbeCompletion(targetProcessId, startedTimestamp);
        return false;
    }

    private void LogProbeCompletion(int targetProcessId, long startedTimestamp, int? resolvedFieldCount = null)
    {
        TimeSpan elapsed = Stopwatch.GetElapsedTime(startedTimestamp);
        StaticAppLogger.Instance.Info(
            $"MemoryBridge runtime-layout probe completed for Terraria PID {targetProcessId}; " +
            $"elapsedMs={elapsed.TotalMilliseconds:F0}, exitCode={lastExitCode?.ToString(CultureInfo.InvariantCulture) ?? "<none>"}, " +
            $"fields={resolvedFieldCount?.ToString(CultureInfo.InvariantCulture) ?? "<none>"}, " +
            $"error={lastError ?? "<none>"}.");
    }

    private static Process? StartMemoryProbe(string probePath, int targetProcessId)
    {
        var startInfo = new ProcessStartInfo(probePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("runtime-layout");
        startInfo.ArgumentList.Add(targetProcessId.ToString(CultureInfo.InvariantCulture));
        return Process.Start(startInfo);
    }

    private static string? FindMemoryProbeExecutable()
    {
        foreach (string path in EnumerateMemoryProbeCandidatePaths())
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateMemoryProbeCandidatePaths()
    {
        foreach (string baseDirectory in EnumerateBaseDirectories())
        {
            yield return Path.Combine(baseDirectory, MemoryBridgeExecutableName);
            yield return Path.Combine(baseDirectory, "TerrariaSplit.MemoryBridge", MemoryBridgeExecutableName);

            DirectoryInfo? directory = new(baseDirectory);
            for (int depth = 0; directory is not null && depth < 8; depth++, directory = directory.Parent)
            {
                foreach (string configuration in new[] { "Debug", "Release" })
                {
                    yield return Path.Combine(
                        directory.FullName,
                        "TerrariaSplit.MemoryProbe",
                        "bin",
                        configuration,
                        "net10.0-windows",
                        "win-x86",
                        MemoryBridgeExecutableName);
                    yield return Path.Combine(
                        directory.FullName,
                        "TerrariaSplit.MemoryProbe",
                        "bin",
                        configuration,
                        "net10.0-windows",
                        MemoryBridgeExecutableName);
                    yield return Path.Combine(
                        directory.FullName,
                        "TerrariaSplit.MemoryProbe",
                        ".codex-build",
                        "bin",
                        configuration,
                        "net10.0-windows",
                        "win-x86",
                        MemoryBridgeExecutableName);
                    yield return Path.Combine(
                        directory.FullName,
                        "TerrariaSplit.MemoryProbe",
                        ".codex-build",
                        "bin",
                        configuration,
                        "net10.0-windows",
                        MemoryBridgeExecutableName);
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateBaseDirectories()
    {
        yield return AppContext.BaseDirectory;
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill();
        }
        catch (InvalidOperationException)
        {
        }
        catch (Win32Exception)
        {
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

    private static IntPtr ToIntPtr(long address)
    {
        return IntPtr.Size == 8
            ? new IntPtr(address)
            : new IntPtr(unchecked((int)address));
    }

    private sealed record RuntimeLayoutProbeResponse(
        bool Success,
        string? Error,
        RuntimeLayoutProbeDto? Layout);

    private sealed record RuntimeLayoutProbeDto(
        string? TerrariaVersion,
        CoreLayoutProbeDto Core,
        BossLayoutProbeDto Boss,
        PlayerItemLayoutProbeDto? Item,
        NpcLayoutProbeDto? Npc,
        BiomeLayoutProbeDto? Biome,
        SeedUiLayoutProbeDto? SeedUi,
        WorldGenerationLayoutProbeDto WorldGeneration,
        int ResolvedFieldCount)
    {
        public TerrariaRuntimeMemoryLayout ToRuntimeMemoryLayout()
        {
            return new TerrariaRuntimeMemoryLayout(
                TerrariaVersion,
                Core.ToCoreLayout(),
                Boss.ToBossLayout(),
                Item?.ToItemMemoryLayout(),
                Npc?.ToNpcMemoryLayout(),
                Biome?.ToBiomeMemoryLayout(),
                SeedUi?.ToSeedUiLayout(),
                WorldGeneration.ToWorldGenerationLayout(),
                ResolvedFieldCount);
        }
    }

    private sealed record CoreLayoutProbeDto(
        long GameMenuStaticFieldAddress,
        long StatusTextStaticFieldAddress,
        long MenuUiStaticFieldAddress)
    {
        public TerrariaCoreMemoryLayout ToCoreLayout()
        {
            return new TerrariaCoreMemoryLayout(
                ToIntPtr(GameMenuStaticFieldAddress),
                ToIntPtr(StatusTextStaticFieldAddress),
                ToIntPtr(MenuUiStaticFieldAddress));
        }
    }

    private sealed record BossLayoutProbeDto(Dictionary<string, long> FactStaticFieldAddresses)
    {
        public TerrariaBossMemoryLayout ToBossLayout()
        {
            Dictionary<string, IntPtr> addresses = new(StringComparer.OrdinalIgnoreCase);
            foreach ((string factKey, long address) in FactStaticFieldAddresses)
            {
                addresses[factKey] = ToIntPtr(address);
            }

            return new TerrariaBossMemoryLayout(addresses);
        }
    }

    private sealed record PlayerItemLayoutProbeDto(
        long PlayerArrayStaticFieldAddress,
        long MyPlayerStaticFieldAddress,
        long MouseItemStaticFieldAddress,
        int PlayerArmorFieldOffset,
        int PlayerDyeFieldOffset,
        int PlayerMiscEquipsFieldOffset,
        int PlayerMiscDyesFieldOffset,
        int PlayerTrashItemFieldOffset,
        int PlayerInventoryFieldOffset,
        int PlayerBankFieldOffset,
        int PlayerBank2FieldOffset,
        int PlayerBank3FieldOffset,
        int PlayerBank4FieldOffset,
        int ChestItemArrayFieldOffset,
        int ItemTypeFieldOffset,
        int ItemStackFieldOffset,
        int ManagedArrayLengthOffset,
        int ManagedArrayFirstElementOffset,
        int ObjectReferenceSize)
    {
        public TerrariaItemMemoryLayout ToItemMemoryLayout()
        {
            return new TerrariaItemMemoryLayout(
                ToIntPtr(PlayerArrayStaticFieldAddress),
                ToIntPtr(MyPlayerStaticFieldAddress),
                ToIntPtr(MouseItemStaticFieldAddress),
                PlayerArmorFieldOffset,
                PlayerDyeFieldOffset,
                PlayerMiscEquipsFieldOffset,
                PlayerMiscDyesFieldOffset,
                PlayerTrashItemFieldOffset,
                PlayerInventoryFieldOffset,
                PlayerBankFieldOffset,
                PlayerBank2FieldOffset,
                PlayerBank3FieldOffset,
                PlayerBank4FieldOffset,
                ChestItemArrayFieldOffset,
                ItemTypeFieldOffset,
                ItemStackFieldOffset,
                ManagedArrayLengthOffset,
                ManagedArrayFirstElementOffset,
                ObjectReferenceSize);
        }
    }

    private sealed record NpcLayoutProbeDto(
        long NpcArrayStaticFieldAddress,
        int NpcTypeFieldOffset,
        int NpcActiveFieldOffset,
        int NpcTownNpcFieldOffset,
        int NpcHomelessFieldOffset,
        int NpcHomeTileXFieldOffset,
        int NpcHomeTileYFieldOffset,
        int ManagedArrayLengthOffset,
        int ManagedArrayFirstElementOffset,
        int ObjectReferenceSize)
    {
        public TerrariaNpcMemoryLayout ToNpcMemoryLayout()
        {
            return new TerrariaNpcMemoryLayout(
                ToIntPtr(NpcArrayStaticFieldAddress),
                NpcTypeFieldOffset,
                NpcActiveFieldOffset,
                NpcTownNpcFieldOffset,
                NpcHomelessFieldOffset,
                NpcHomeTileXFieldOffset,
                NpcHomeTileYFieldOffset,
                ManagedArrayLengthOffset,
                ManagedArrayFirstElementOffset,
                ObjectReferenceSize);
        }
    }

    private sealed record BiomeLayoutProbeDto(
        long PlayerArrayStaticFieldAddress,
        long MyPlayerStaticFieldAddress,
        Dictionary<string, int>? ZoneBitsByteFieldOffsets,
        int ManagedArrayLengthOffset,
        int ManagedArrayFirstElementOffset,
        int ObjectReferenceSize)
    {
        public TerrariaBiomeMemoryLayout ToBiomeMemoryLayout()
        {
            return new TerrariaBiomeMemoryLayout(
                ToIntPtr(PlayerArrayStaticFieldAddress),
                ToIntPtr(MyPlayerStaticFieldAddress),
                ZoneBitsByteFieldOffsets ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                ManagedArrayLengthOffset,
                ManagedArrayFirstElementOffset,
                ObjectReferenceSize);
        }
    }

    private sealed record SeedUiLayoutProbeDto(
        long MenuUiStaticFieldAddress,
        int UserInterfaceCurrentStateFieldOffset,
        int UiStateNestedReferenceScanStart,
        int UiStateNestedReferenceScanEnd,
        int WorldCreationAdvancedCreationStateFieldOffset,
        int WorldCreationAdvancedSeedPlateFieldOffset,
        int WorldNameFieldOffset,
        int SeedFieldOffset,
        int NamePlateFieldOffset,
        int SeedPlateFieldOffset,
        int CharacterNameButtonActualContentsOffset,
        int ObjectReferenceSize)
    {
        public TerrariaWorldCreationSeedMemoryLayout ToSeedUiLayout()
        {
            return new TerrariaWorldCreationSeedMemoryLayout(
                ToIntPtr(MenuUiStaticFieldAddress),
                UserInterfaceCurrentStateFieldOffset,
                UiStateNestedReferenceScanStart,
                UiStateNestedReferenceScanEnd,
                WorldCreationAdvancedCreationStateFieldOffset,
                WorldCreationAdvancedSeedPlateFieldOffset,
                WorldNameFieldOffset,
                SeedFieldOffset,
                NamePlateFieldOffset,
                SeedPlateFieldOffset,
                CharacterNameButtonActualContentsOffset,
                ObjectReferenceSize);
        }
    }

    private sealed record WorldGenerationLayoutProbeDto(
        long StatusTextStaticFieldAddress,
        long CurrentGenerationProgressStaticFieldAddress,
        long CurrentControllerStaticFieldAddress,
        int GenerationProgressMessageFieldOffset,
        int GenerationProgressValueFieldOffset,
        int GenerationProgressTotalWeightedProgressFieldOffset,
        int GenerationProgressTotalWeightFieldOffset,
        int GenerationProgressCurrentPassWeightFieldOffset,
        int ControllerGeneratorFieldOffset,
        int WorldGeneratorCurrentPassFieldOffset,
        int GenPassNameFieldOffset)
    {
        public TerrariaWorldGenerationMemoryLayout ToWorldGenerationLayout()
        {
            return new TerrariaWorldGenerationMemoryLayout(
                ToIntPtr(StatusTextStaticFieldAddress),
                ToIntPtr(CurrentGenerationProgressStaticFieldAddress),
                ToIntPtr(CurrentControllerStaticFieldAddress),
                GenerationProgressMessageFieldOffset,
                GenerationProgressValueFieldOffset,
                GenerationProgressTotalWeightedProgressFieldOffset,
                GenerationProgressTotalWeightFieldOffset,
                GenerationProgressCurrentPassWeightFieldOffset,
                ControllerGeneratorFieldOffset,
                WorldGeneratorCurrentPassFieldOffset,
                GenPassNameFieldOffset);
        }
    }
}
