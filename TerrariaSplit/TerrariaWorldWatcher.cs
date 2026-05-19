using System.ComponentModel;
using System.Diagnostics;

namespace TerrariaSplit;

internal sealed class TerrariaWorldWatcher : ITerrariaWorldWatcher
{
    private readonly TerrariaMemoryProfile profile;
    private readonly TerrariaMemoryResolver resolver;
    private readonly TimeSpan initialScanInterval = TimeSpan.FromMilliseconds(250);
    private readonly TimeSpan rescanInterval = TimeSpan.FromSeconds(2);
    private readonly TimeSpan processLookupInterval = TimeSpan.FromSeconds(1);

    private Process? process;
    private ProcessMemoryReader? memory;
    private bool? previousGameMenu;
    private bool awaitingInitialMenuObservation;
    private DateTime nextScanUtc = DateTime.MinValue;
    private DateTime nextProcessLookupUtc = DateTime.MinValue;
    private string diagnosticStage = "waiting for process";
    private string status = "waiting for Terraria.exe";

    public TerrariaWorldWatcher()
        : this(Terraria1456Memory.Profile)
    {
    }

    public TerrariaWorldWatcher(TerrariaMemoryProfile profile)
    {
        this.profile = profile;
        resolver = new TerrariaMemoryResolver(profile);
    }

    public TerrariaWatchSnapshot Poll()
    {
        if (!HasLiveProcess())
        {
            if (DateTime.UtcNow >= nextProcessLookupUtc)
            {
                AttachToProcess();
            }
            else
            {
                process?.Dispose();
                process = null;
                memory = null;
                resolver.Reset();
                previousGameMenu = null;
                awaitingInitialMenuObservation = false;
                diagnosticStage = "waiting for process";
                status = "waiting for Terraria.exe";
            }
        }

        if (process is null || memory is null)
        {
            return new TerrariaWatchSnapshot(
                false,
                null,
                false,
                null,
                TerrariaBossStates.Unknown,
                TerrariaWorldGenerationState.Unknown,
                false,
                status);
        }

        if (!resolver.HasGameMenuAddress)
        {
            TryResolveMemoryAddresses();
            return new TerrariaWatchSnapshot(
                true,
                process.Id,
                false,
                previousGameMenu,
                TerrariaBossStates.Unknown,
                TerrariaWorldGenerationState.Unknown,
                false,
                status);
        }

        if ((!resolver.HasResolvedBossAddresses || !resolver.HasResolvedWorldGenerationAddresses) &&
            DateTime.UtcNow >= nextScanUtc)
        {
            TryResolveMemoryAddresses();
        }

        if (!resolver.TryReadGameMenuState(memory, out bool isGameMenu))
        {
            resolver.ResetResolvedAddresses();
            previousGameMenu = null;
            awaitingInitialMenuObservation = false;
            diagnosticStage = "menu state pointer lost";
            status = BuildAttachedStatus("lost menu-state pointer; rescanning");
            nextScanUtc = DateTime.MinValue;
            return new TerrariaWatchSnapshot(
                true,
                process.Id,
                false,
                null,
                TerrariaBossStates.Unknown,
                TerrariaWorldGenerationState.Unknown,
                false,
                status);
        }

        TerrariaBossStates bossStates = resolver.ReadBossStates(memory);
        TerrariaWorldGenerationState worldGeneration = resolver.ReadWorldGenerationState(memory);

        bool enteredWorld = !awaitingInitialMenuObservation
            && previousGameMenu == true
            && !isGameMenu;
        if (awaitingInitialMenuObservation && isGameMenu)
        {
            awaitingInitialMenuObservation = false;
        }

        previousGameMenu = isGameMenu;
        diagnosticStage = BuildOperationalStage();
        status = BuildOperationalStatus();

        return new TerrariaWatchSnapshot(
            true,
            process.Id,
            true,
            isGameMenu,
            bossStates,
            worldGeneration,
            enteredWorld,
            status);
    }

    public void Dispose()
    {
        process?.Dispose();
    }

    public TerrariaWatcherDiagnostics GetDiagnostics()
    {
        TerrariaMemoryResolution resolution = resolver.Resolution;
        return new TerrariaWatcherDiagnostics(
            diagnosticStage,
            profile.SupportedVersionLabel,
            profile.SignatureProfileLabel,
            memory?.Is64Bit,
            FormatProcessArchitecture(),
            TryGetProcessPath(),
            TryGetProcessVersion(),
            TryGetMainModuleBaseAddress(),
            TryGetMainModuleSize(),
            resolver.SignatureScanAttempts,
            resolver.LastSignatureScanUtc,
            resolver.LastSignatureScan,
            resolution.UpdateTimeAddress,
            resolution.GameMenuAddress,
            resolution.GameMenuSecondaryAddress,
            resolution.BossFlagsBaseAddress,
            resolution.HardmodeAddress,
            resolution.CurrentGenerationProgressAddress,
            resolution.CurrentControllerAddress,
            BuildCompatibilityHint(resolution));
    }

    private bool HasLiveProcess()
    {
        try
        {
            return process is { HasExited: false };
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void AttachToProcess()
    {
        process?.Dispose();
        process = null;
        memory = null;
        resolver.Reset();
        previousGameMenu = null;
        awaitingInitialMenuObservation = false;

        Process? candidate = TerrariaProcessFinder.FindNewest(profile);
        if (candidate is null)
        {
            diagnosticStage = "waiting for process";
            status = "waiting for Terraria.exe";
            nextProcessLookupUtc = DateTime.UtcNow + processLookupInterval;
            return;
        }

        try
        {
            memory = new ProcessMemoryReader(candidate);
            process = candidate;
            nextProcessLookupUtc = DateTime.MinValue;
            nextScanUtc = DateTime.MinValue;
            diagnosticStage = "scanning for signature";
            status = BuildAttachedStatus($"scanning for {profile.SupportedVersionLabel} memory");
        }
        catch (Win32Exception ex)
        {
            candidate.Dispose();
            diagnosticStage = "cannot read process";
            status = $"cannot read Terraria process: {ex.Message}";
            nextProcessLookupUtc = DateTime.UtcNow + processLookupInterval;
        }
        catch (InvalidOperationException ex)
        {
            candidate.Dispose();
            diagnosticStage = "cannot attach process";
            status = $"cannot attach to Terraria process: {ex.Message}";
            nextProcessLookupUtc = DateTime.UtcNow + processLookupInterval;
        }
    }

    private void TryResolveMemoryAddresses()
    {
        if (process is null || memory is null || DateTime.UtcNow < nextScanUtc)
        {
            return;
        }

        nextScanUtc = DateTime.UtcNow + GetNextScanInterval();
        TerrariaMemoryResolveResult result = resolver.Resolve(memory);

        if (result.ObservedGameMenu.HasValue)
        {
            InitializeGameMenuState(result.ObservedGameMenu.Value);
            diagnosticStage = BuildOperationalStage();
            status = BuildOperationalStatus();
            return;
        }

        if (resolver.HasGameMenuAddress &&
            string.Equals(result.Stage, resolver.BuildResolutionStage(), StringComparison.Ordinal))
        {
            diagnosticStage = BuildOperationalStage();
            status = BuildOperationalStatus();
            return;
        }

        diagnosticStage = result.Stage;
        status = BuildAttachedStatus(result.StatusDetail);
    }

    private TimeSpan GetNextScanInterval()
    {
        return resolver.HasGameMenuAddress
            ? rescanInterval
            : initialScanInterval;
    }

    private void InitializeGameMenuState(bool isGameMenu)
    {
        awaitingInitialMenuObservation = previousGameMenu is null && !isGameMenu;
        previousGameMenu = isGameMenu;
    }

    private bool IsTimerStartPending()
    {
        return awaitingInitialMenuObservation && previousGameMenu == false;
    }

    private string BuildOperationalStage()
    {
        string stage = resolver.BuildResolutionStage();
        return IsTimerStartPending()
            ? $"{stage}; start pending"
            : stage;
    }

    private string BuildOperationalStatus()
    {
        string operationalStatus = resolver.HasResolvedBossAddresses && resolver.BuildResolutionStatusDetail() == "ready"
            ? $"attached to Terraria PID {process!.Id}"
            : BuildAttachedStatus(resolver.BuildResolutionStatusDetail());

        return IsTimerStartPending()
            ? $"{operationalStatus}; return to menu once to arm timer start"
            : operationalStatus;
    }

    private string BuildAttachedStatus(string detail)
    {
        return process is null
            ? status
            : $"attached to Terraria PID {process.Id}, {detail}";
    }

    private string FormatProcessArchitecture()
    {
        if (memory is null)
        {
            return "Unknown";
        }

        return memory.Is64Bit ? "x64" : "x86";
    }

    private string? TryGetProcessPath()
    {
        return TryGetMainModule()?.FileName;
    }

    private string? TryGetProcessVersion()
    {
        try
        {
            return TryGetMainModule()?.FileVersionInfo.FileVersion;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    private IntPtr TryGetMainModuleBaseAddress()
    {
        try
        {
            return TryGetMainModule()?.BaseAddress ?? IntPtr.Zero;
        }
        catch (Win32Exception)
        {
            return IntPtr.Zero;
        }
    }

    private int? TryGetMainModuleSize()
    {
        try
        {
            return TryGetMainModule()?.ModuleMemorySize;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    private ProcessModule? TryGetMainModule()
    {
        if (process is null)
        {
            return null;
        }

        try
        {
            return process.MainModule;
        }
        catch (Win32Exception)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private string BuildCompatibilityHint(TerrariaMemoryResolution resolution)
    {
        if (process is null || memory is null)
        {
            return "Waiting for Terraria process.";
        }

        if (memory.Is64Bit)
        {
            return "Target Terraria process is x64. The current UpdateTime signature was authored from an x86-style function prologue.";
        }

        if (IsTimerStartPending())
        {
            return "Watcher first became ready while Terraria was already in a world. The timer starts only on a menu-to-world transition, so return to the main menu once and enter the world again.";
        }

        if (resolution.UsingGameMenuFallback && resolution.UsingBossProgressionFallback)
        {
            return "Fallback signatures resolved menu state and boss progression when the primary UpdateTime anchor was unavailable on this runtime.";
        }

        if (resolution.UsingGameMenuFallback)
        {
            return "Fallback menu-state signature resolved a stronger UpdateTime-adjacent gameMenu access pattern when the direct UpdateTime anchor was unavailable on this runtime.";
        }

        if (resolution.UsingBossProgressionFallback)
        {
            return "Boss progression fallback resolved hardmode and boss flags when the UpdateTime-relative boss pointer offsets were unavailable.";
        }

        if (resolution.UpdateTimeAddress == IntPtr.Zero)
        {
            return "UpdateTime did not match any scanned private or image executable page.";
        }

        if (resolution.GameMenuAddress == IntPtr.Zero)
        {
            return "UpdateTime matched, but the expected menu-state pointer offset did not resolve to readable memory.";
        }

        if (!resolution.HasResolvedBossAddresses)
        {
            return "gameMenu resolved, but boss and hardmode pointers are still pending or unreadable.";
        }

        if (!resolution.HasResolvedWorldGenerationAddresses)
        {
            return "Watcher resolved timer and boss pointers, but world generation pointers are still pending or unreadable.";
        }

        return "Watcher resolved all current pointers.";
    }
}
