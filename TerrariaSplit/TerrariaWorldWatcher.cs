using System.ComponentModel;
using System.Diagnostics;

namespace TerrariaSplit;

internal sealed class TerrariaWorldWatcher : ITerrariaWorldWatcher
{
    private readonly TerrariaMemoryProfile profile;
    private readonly TerrariaMemoryResolver resolver;
    private readonly TerrariaWorldCreationSeedReader worldCreationSeedReader = new();
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

    // Module facts and operational strings are stable between watcher state
    // transitions, so they are cached instead of being recomputed on every poll
    // (Process.MainModule enumerates the target's modules and FileVersionInfo
    // reads version resources from disk on each call).
    private static readonly TimeSpan ModuleFactsRetryInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SeedDiagnosticsReadInterval = TimeSpan.FromMilliseconds(100);
    private int? moduleFactsProcessId;
    private bool moduleFactsCaptured;
    private DateTime nextModuleFactsAttemptUtc = DateTime.MinValue;
    private string? cachedProcessPath;
    private string? cachedProcessVersion;
    private IntPtr cachedMainModuleBaseAddress;
    private int? cachedMainModuleSize;
    private TerrariaWorldCreationSeedSnapshot lastSeedDiagnostics = TerrariaWorldCreationSeedSnapshot.Unknown;
    private DateTime nextSeedDiagnosticsReadUtc = DateTime.MinValue;
    private (string Stage, string Detail, int ProcessId, bool StartPending)? operationalTextKey;
    private string? cachedOperationalStage;
    private string? cachedOperationalStatus;

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
                worldCreationSeedReader.Reset();
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
            worldCreationSeedReader.Reset();
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
        UpdateOperationalText();

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
        EnsureModuleFactsCaptured();
        TerrariaMemoryResolution resolution = resolver.Resolution;
        TerrariaWorldCreationSeedSnapshot worldCreationSeed = ReadWorldCreationSeedDiagnostics();
        return new TerrariaWatcherDiagnostics(
            diagnosticStage,
            profile.SupportedVersionLabel,
            profile.SignatureProfileLabel,
            memory?.Is64Bit,
            FormatProcessArchitecture(),
            cachedProcessPath,
            cachedProcessVersion,
            cachedMainModuleBaseAddress,
            cachedMainModuleSize,
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
            worldCreationSeed,
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
        worldCreationSeedReader.Reset();
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
            UpdateOperationalText();
            return;
        }

        if (resolver.HasGameMenuAddress &&
            string.Equals(result.Stage, resolver.BuildResolutionStage(), StringComparison.Ordinal))
        {
            UpdateOperationalText();
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

    private void UpdateOperationalText()
    {
        // Stage and detail are interned constants from the resolver, so the key
        // comparison is cheap; the interpolated strings are only rebuilt when the
        // resolver state, process id, or start-pending flag actually changes. The
        // reference checks guard against other paths having overwritten the text
        // (e.g. a transient pointer-lost stage) while the key stayed the same.
        (string, string, int, bool) key = (
            resolver.BuildResolutionStage(),
            resolver.BuildResolutionStatusDetail(),
            process!.Id,
            IsTimerStartPending());
        if (operationalTextKey == key &&
            ReferenceEquals(diagnosticStage, cachedOperationalStage) &&
            ReferenceEquals(status, cachedOperationalStatus))
        {
            return;
        }

        operationalTextKey = key;
        cachedOperationalStage = BuildOperationalStage();
        cachedOperationalStatus = BuildOperationalStatus();
        diagnosticStage = cachedOperationalStage;
        status = cachedOperationalStatus;
    }

    private string BuildAttachedStatus(string detail)
    {
        return process is null
            ? status
            : $"attached to Terraria PID {process.Id}, {detail}";
    }

    private TerrariaWorldCreationSeedSnapshot ReadWorldCreationSeedDiagnostics()
    {
        if (memory is null)
        {
            worldCreationSeedReader.Reset();
            nextSeedDiagnosticsReadUtc = DateTime.MinValue;
            lastSeedDiagnostics = TerrariaWorldCreationSeedSnapshot.Unknown;
            return lastSeedDiagnostics;
        }

        if (previousGameMenu != true)
        {
            worldCreationSeedReader.Reset();
            nextSeedDiagnosticsReadUtc = DateTime.MinValue;
            lastSeedDiagnostics = previousGameMenu == false
                ? TerrariaWorldCreationSeedSnapshot.NotOnWorldCreationPage
                : TerrariaWorldCreationSeedSnapshot.Unknown;
            return lastSeedDiagnostics;
        }

        DateTime now = DateTime.UtcNow;
        if (now < nextSeedDiagnosticsReadUtc)
        {
            return lastSeedDiagnostics;
        }

        nextSeedDiagnosticsReadUtc = now + SeedDiagnosticsReadInterval;
        lastSeedDiagnostics = worldCreationSeedReader.Read(memory);
        return lastSeedDiagnostics;
    }

    private string FormatProcessArchitecture()
    {
        if (memory is null)
        {
            return "Unknown";
        }

        return memory.Is64Bit ? "x64" : "x86";
    }

    private void EnsureModuleFactsCaptured()
    {
        if (process is null)
        {
            moduleFactsProcessId = null;
            moduleFactsCaptured = false;
            cachedProcessPath = null;
            cachedProcessVersion = null;
            cachedMainModuleBaseAddress = IntPtr.Zero;
            cachedMainModuleSize = null;
            return;
        }

        if (moduleFactsProcessId != process.Id)
        {
            moduleFactsProcessId = process.Id;
            moduleFactsCaptured = false;
            cachedProcessPath = null;
            cachedProcessVersion = null;
            cachedMainModuleBaseAddress = IntPtr.Zero;
            cachedMainModuleSize = null;
            nextModuleFactsAttemptUtc = DateTime.MinValue;
        }

        if (moduleFactsCaptured || DateTime.UtcNow < nextModuleFactsAttemptUtc)
        {
            return;
        }

        nextModuleFactsAttemptUtc = DateTime.UtcNow + ModuleFactsRetryInterval;
        ProcessModule? mainModule = TryGetMainModule();
        if (mainModule is null)
        {
            return;
        }

        cachedProcessPath = mainModule.FileName;
        cachedProcessVersion = TryGetFileVersion(mainModule);
        cachedMainModuleBaseAddress = mainModule.BaseAddress;
        cachedMainModuleSize = mainModule.ModuleMemorySize;
        moduleFactsCaptured = true;
    }

    private static string? TryGetFileVersion(ProcessModule module)
    {
        try
        {
            return module.FileVersionInfo.FileVersion;
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

        if (resolution.UsingBossProgressionMenuFallback)
        {
            return "Boss progression fallback resolved hardmode and boss flags, then inferred gameMenu from the Terraria static field layout after the primary menu routes failed.";
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
