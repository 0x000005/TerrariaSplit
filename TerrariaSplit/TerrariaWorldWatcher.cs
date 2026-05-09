using System.ComponentModel;
using System.Diagnostics;

namespace TerrariaSplit;

internal sealed class TerrariaWorldWatcher : IDisposable
{
    private static readonly SignaturePattern UpdateTimeSignature =
        SignaturePattern.Parse(Terraria1456Memory.UpdateTimeSignature);
    private static readonly SignaturePattern GameMenuFallbackSignature =
        SignaturePattern.Parse(Terraria1456Memory.GameMenuFallbackSignature);
    private static readonly SignaturePattern BossProgressionFallbackSignature =
        SignaturePattern.Parse(Terraria1456Memory.BossProgressionFallbackSignature);

    private readonly TimeSpan initialScanInterval = TimeSpan.FromMilliseconds(250);
    private readonly TimeSpan rescanInterval = TimeSpan.FromSeconds(2);

    private Process? process;
    private ProcessMemoryReader? memory;
    private IntPtr updateTimeAddress;
    private IntPtr gameMenuAddress;
    private IntPtr gameMenuSecondaryAddress;
    private IntPtr bossFlagsBaseAddress;
    private IntPtr hardmodeAddress;
    private bool? previousGameMenu;
    private bool awaitingInitialMenuObservation;
    private DateTime nextScanUtc = DateTime.MinValue;
    private int signatureScanAttempts;
    private DateTime? lastSignatureScanUtc;
    private SignatureScanDiagnostics? lastSignatureScan;
    private bool usingGameMenuFallback;
    private bool usingBossProgressionFallback;
    private string diagnosticStage = "waiting for process";
    private string status = "waiting for Terraria.exe";

    public TerrariaWatchSnapshot Poll()
    {
        if (!HasLiveProcess())
        {
            AttachToProcess();
        }

        if (process is null || memory is null)
        {
            return new TerrariaWatchSnapshot(
                false,
                null,
                false,
                null,
                TerrariaBossStates.Unknown,
                false,
                status);
        }

        if (gameMenuAddress == IntPtr.Zero)
        {
            TryResolveMemoryAddresses();
            return new TerrariaWatchSnapshot(
                true,
                process.Id,
                false,
                previousGameMenu,
                TerrariaBossStates.Unknown,
                false,
                status);
        }

        if (!HasResolvedBossAddresses() && DateTime.UtcNow >= nextScanUtc)
        {
            TryResolveMemoryAddresses();
        }

        if (!TryReadGameMenuState(out bool isGameMenu))
        {
            ResetResolvedAddresses();
            previousGameMenu = null;
            diagnosticStage = "menu state pointer lost";
            status = $"attached to Terraria PID {process.Id}, lost menu-state pointer; rescanning";
            nextScanUtc = DateTime.MinValue;
            return new TerrariaWatchSnapshot(
                true,
                process.Id,
                false,
                null,
                TerrariaBossStates.Unknown,
                false,
                status);
        }

        TerrariaBossStates bossStates = ReadBossStates();

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
            enteredWorld,
            status);
    }

    public void Dispose()
    {
        process?.Dispose();
    }

    public TerrariaWatcherDiagnostics GetDiagnostics()
    {
        return new TerrariaWatcherDiagnostics(
            diagnosticStage,
            Terraria1456Memory.SupportedVersionLabel,
            Terraria1456Memory.SignatureProfileLabel,
            memory?.Is64Bit,
            FormatProcessArchitecture(),
            TryGetProcessPath(),
            TryGetProcessVersion(),
            TryGetMainModuleBaseAddress(),
            TryGetMainModuleSize(),
            signatureScanAttempts,
            lastSignatureScanUtc,
            lastSignatureScan,
            updateTimeAddress,
            gameMenuAddress,
            gameMenuSecondaryAddress,
            bossFlagsBaseAddress,
            hardmodeAddress,
            BuildCompatibilityHint());
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
        ResetResolvedAddresses();
        ResetDiagnostics();
        previousGameMenu = null;

        Process? candidate = FindTerrariaProcess();
        if (candidate is null)
        {
            diagnosticStage = "waiting for process";
            status = "waiting for Terraria.exe";
            return;
        }

        try
        {
            memory = new ProcessMemoryReader(candidate);
            process = candidate;
            nextScanUtc = DateTime.MinValue;
            diagnosticStage = "scanning for signature";
            status = $"attached to Terraria PID {process.Id}, scanning for {Terraria1456Memory.SupportedVersionLabel} memory";
        }
        catch (Win32Exception ex)
        {
            candidate.Dispose();
            diagnosticStage = "cannot read process";
            status = $"cannot read Terraria process: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            candidate.Dispose();
            diagnosticStage = "cannot attach process";
            status = $"cannot attach to Terraria process: {ex.Message}";
        }
    }

    private bool HasResolvedBossAddresses()
    {
        return bossFlagsBaseAddress != IntPtr.Zero
            && hardmodeAddress != IntPtr.Zero;
    }

    private void ResetResolvedAddresses()
    {
        updateTimeAddress = IntPtr.Zero;
        gameMenuAddress = IntPtr.Zero;
        gameMenuSecondaryAddress = IntPtr.Zero;
        bossFlagsBaseAddress = IntPtr.Zero;
        hardmodeAddress = IntPtr.Zero;
        awaitingInitialMenuObservation = false;
        usingGameMenuFallback = false;
        usingBossProgressionFallback = false;
    }

    private void TryResolveMemoryAddresses()
    {
        if (process is null || memory is null || DateTime.UtcNow < nextScanUtc)
        {
            return;
        }

        nextScanUtc = DateTime.UtcNow + GetNextScanInterval();
        signatureScanAttempts++;
        lastSignatureScanUtc = DateTime.UtcNow;

        IntPtr resolvedUpdateTimeAddress = SignatureScanner.Scan(memory, UpdateTimeSignature, out SignatureScanDiagnostics updateTimeScanDiagnostics);
        lastSignatureScan = updateTimeScanDiagnostics;
        if (resolvedUpdateTimeAddress != IntPtr.Zero)
        {
            updateTimeAddress = resolvedUpdateTimeAddress;
            if (TryResolveGameMenuFromUpdateTime(resolvedUpdateTimeAddress, out bool isGameMenu))
            {
                InitializeGameMenuState(isGameMenu);
                usingGameMenuFallback = false;

                TryResolveBossAddressesWithFallbacks(resolvedUpdateTimeAddress);
                diagnosticStage = BuildOperationalStage();
                status = BuildOperationalStatus();
                return;
            }

            if (gameMenuAddress != IntPtr.Zero)
            {
                diagnosticStage = "menu state target unreadable";
                status = $"attached to Terraria PID {process.Id}, menu-state pointer became unreadable";
                return;
            }

            diagnosticStage = "menu state pointer unreadable";
            status = $"attached to Terraria PID {process.Id}, found signature but not menu-state pointer";
            return;
        }

        IntPtr fallbackAnchorAddress = SignatureScanner.Scan(memory, GameMenuFallbackSignature, out SignatureScanDiagnostics fallbackScanDiagnostics);
        lastSignatureScan = fallbackScanDiagnostics;
        if (fallbackAnchorAddress != IntPtr.Zero
            && TryResolveGameMenuFromFallback(fallbackAnchorAddress, out bool fallbackGameMenu))
        {
            updateTimeAddress = fallbackAnchorAddress;
            InitializeGameMenuState(fallbackGameMenu);
            usingGameMenuFallback = true;
            TryResolveBossAddressesWithFallbacks(null);
            diagnosticStage = BuildOperationalStage();
            status = BuildOperationalStatus();
            return;
        }

        if (gameMenuAddress != IntPtr.Zero)
        {
            diagnosticStage = BuildOperationalStage();
            status = BuildOperationalStatus();
            return;
        }

        updateTimeAddress = IntPtr.Zero;
        diagnosticStage = "signature missing";
        status = $"attached to Terraria PID {process.Id}, waiting for UpdateTime signature";
    }

    private bool TryResolveBossAddresses(IntPtr updateTimeAddress)
    {
        if (process is null || memory is null)
        {
            return false;
        }

        IntPtr bossFlagsPointerLocation = IntPtr.Add(updateTimeAddress, Terraria1456Memory.BossFlagsPointerOffset);
        if (!memory.TryReadPointer(bossFlagsPointerLocation, out IntPtr resolvedBossFlagsBaseAddress))
        {
            return false;
        }

        IntPtr hardmodePointerLocation = IntPtr.Add(updateTimeAddress, Terraria1456Memory.HardmodePointerOffset);
        if (!memory.TryReadPointer(hardmodePointerLocation, out IntPtr resolvedHardmodeAddress))
        {
            return false;
        }

        if (!memory.TryReadBool(
                IntPtr.Add(resolvedBossFlagsBaseAddress, Terraria1456Memory.SkeletronDefeatedFlagOffset),
                out _))
        {
            return false;
        }

        if (!memory.TryReadBool(resolvedHardmodeAddress, out _))
        {
            return false;
        }

        bossFlagsBaseAddress = resolvedBossFlagsBaseAddress;
        hardmodeAddress = resolvedHardmodeAddress;
        return true;
    }

    private bool TryResolveBossAddressesWithFallbacks(IntPtr? resolvedUpdateTimeAddress)
    {
        if (memory is null)
        {
            return false;
        }

        if (resolvedUpdateTimeAddress.HasValue
            && TryResolveBossAddresses(resolvedUpdateTimeAddress.Value))
        {
            usingBossProgressionFallback = false;
            return true;
        }

        IntPtr fallbackAnchorAddress = SignatureScanner.Scan(
            memory,
            BossProgressionFallbackSignature,
            out SignatureScanDiagnostics fallbackScanDiagnostics);
        lastSignatureScan = fallbackScanDiagnostics;
        if (fallbackAnchorAddress != IntPtr.Zero
            && TryResolveBossAddressesFromProgressionFallback(fallbackAnchorAddress))
        {
            usingBossProgressionFallback = true;
            return true;
        }

        bossFlagsBaseAddress = IntPtr.Zero;
        hardmodeAddress = IntPtr.Zero;
        usingBossProgressionFallback = false;
        return false;
    }

    private bool TryResolveBossAddressesFromProgressionFallback(IntPtr fallbackAnchorAddress)
    {
        if (memory is null)
        {
            return false;
        }

        IntPtr skeletronInlineAddressLocation = IntPtr.Add(
            fallbackAnchorAddress,
            Terraria1456Memory.BossProgressionFallbackSkeletronInlineAddressOffset);
        if (!memory.TryReadPointer(skeletronInlineAddressLocation, out IntPtr resolvedSkeletronAddress))
        {
            return false;
        }

        IntPtr hardmodeInlineAddressLocation = IntPtr.Add(
            fallbackAnchorAddress,
            Terraria1456Memory.BossProgressionFallbackHardmodeInlineAddressOffset);
        if (!memory.TryReadPointer(hardmodeInlineAddressLocation, out IntPtr resolvedHardmodeAddress))
        {
            return false;
        }

        IntPtr resolvedBossFlagsBaseAddress = IntPtr.Add(
            resolvedSkeletronAddress,
            -Terraria1456Memory.SkeletronDefeatedFlagOffset);
        if (!memory.TryReadBool(resolvedSkeletronAddress, out _))
        {
            return false;
        }

        if (!memory.TryReadBool(resolvedHardmodeAddress, out _))
        {
            return false;
        }

        if (!memory.TryReadBool(
                IntPtr.Add(resolvedBossFlagsBaseAddress, Terraria1456Memory.MoonLordDefeatedFlagOffset),
                out _))
        {
            return false;
        }

        bossFlagsBaseAddress = resolvedBossFlagsBaseAddress;
        hardmodeAddress = resolvedHardmodeAddress;
        return true;
    }

    private bool TryResolveGameMenuFromUpdateTime(IntPtr resolvedUpdateTimeAddress, out bool isGameMenu)
    {
        isGameMenu = false;

        if (memory is null)
        {
            return false;
        }

        IntPtr pointerLocation = IntPtr.Add(resolvedUpdateTimeAddress, Terraria1456Memory.GameMenuPointerOffset);
        if (!memory.TryReadPointer(pointerLocation, out IntPtr resolvedGameMenuAddress))
        {
            return false;
        }

        if (!memory.TryReadBool(resolvedGameMenuAddress, out isGameMenu))
        {
            return false;
        }

        gameMenuAddress = resolvedGameMenuAddress;
        gameMenuSecondaryAddress = IntPtr.Zero;
        return true;
    }

    private bool TryResolveGameMenuFromFallback(IntPtr fallbackAnchorAddress, out bool isGameMenu)
    {
        isGameMenu = false;

        if (memory is null)
        {
            return false;
        }

        IntPtr firstMenuModeInlineAddressLocation = IntPtr.Add(
            fallbackAnchorAddress,
            Terraria1456Memory.GameMenuFallbackMenuModeInlineAddressOffset);
        if (!memory.TryReadPointer(firstMenuModeInlineAddressLocation, out IntPtr resolvedMenuModeAddress))
        {
            return false;
        }

        IntPtr gameMenuInlineAddressLocation = IntPtr.Add(
            fallbackAnchorAddress,
            Terraria1456Memory.GameMenuFallbackGameMenuInlineAddressOffset);
        if (!memory.TryReadPointer(gameMenuInlineAddressLocation, out IntPtr resolvedGameMenuAddress))
        {
            return false;
        }

        IntPtr secondMenuModeInlineAddressLocation = IntPtr.Add(
            fallbackAnchorAddress,
            Terraria1456Memory.GameMenuFallbackSecondMenuModeInlineAddressOffset);
        if (!memory.TryReadPointer(secondMenuModeInlineAddressLocation, out IntPtr resolvedSecondMenuModeAddress))
        {
            return false;
        }

        if (resolvedMenuModeAddress != resolvedSecondMenuModeAddress)
        {
            return false;
        }

        if (!memory.TryReadInt32(resolvedMenuModeAddress, out _) ||
            !memory.TryReadBool(resolvedGameMenuAddress, out isGameMenu))
        {
            return false;
        }

        gameMenuAddress = resolvedGameMenuAddress;
        gameMenuSecondaryAddress = IntPtr.Zero;
        return true;
    }

    private bool TryReadGameMenuState(out bool isGameMenu)
    {
        isGameMenu = false;

        if (memory is null || gameMenuAddress == IntPtr.Zero)
        {
            return false;
        }

        if (!memory.TryReadBool(gameMenuAddress, out bool firstValue))
        {
            return false;
        }

        if (gameMenuSecondaryAddress == IntPtr.Zero)
        {
            isGameMenu = firstValue;
            return true;
        }

        if (!memory.TryReadBool(gameMenuSecondaryAddress, out bool secondValue))
        {
            return false;
        }

        isGameMenu = firstValue || secondValue;
        return true;
    }

    private TerrariaBossStates ReadBossStates()
    {
        if (memory is null)
        {
            return TerrariaBossStates.Unknown;
        }

        return new TerrariaBossStates(
            ReadBossFlag(Terraria1456Memory.SkeletronDefeatedFlagOffset),
            ReadHardmodeFlag(),
            ReadBossFlag(Terraria1456Memory.DestroyerDefeatedFlagOffset),
            ReadBossFlag(Terraria1456Memory.TwinsDefeatedFlagOffset),
            ReadBossFlag(Terraria1456Memory.SkeletronPrimeDefeatedFlagOffset),
            ReadBossFlag(Terraria1456Memory.PlanteraDefeatedFlagOffset),
            ReadBossFlag(Terraria1456Memory.GolemDefeatedFlagOffset),
            ReadBossFlag(Terraria1456Memory.LunaticCultistDefeatedFlagOffset),
            ReadBossFlag(Terraria1456Memory.MoonLordDefeatedFlagOffset));
    }

    private bool? ReadBossFlag(int offset)
    {
        if (memory is null || bossFlagsBaseAddress == IntPtr.Zero)
        {
            return null;
        }

        if (memory.TryReadBool(IntPtr.Add(bossFlagsBaseAddress, offset), out bool value))
        {
            return value;
        }

        bossFlagsBaseAddress = IntPtr.Zero;
        return null;
    }

    private bool? ReadHardmodeFlag()
    {
        if (memory is null || hardmodeAddress == IntPtr.Zero)
        {
            return null;
        }

        if (memory.TryReadBool(hardmodeAddress, out bool value))
        {
            return value;
        }

        hardmodeAddress = IntPtr.Zero;
        return null;
    }

    private void ResetDiagnostics()
    {
        signatureScanAttempts = 0;
        lastSignatureScanUtc = null;
        lastSignatureScan = null;
        diagnosticStage = "waiting for process";
    }

    private TimeSpan GetNextScanInterval()
    {
        return gameMenuAddress == IntPtr.Zero
            ? initialScanInterval
            : rescanInterval;
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
        string stage;
        if (HasResolvedBossAddresses())
        {
            if (usingGameMenuFallback && usingBossProgressionFallback)
            {
                stage = "ready via fallback";
            }
            else if (usingGameMenuFallback)
            {
                stage = "ready via gameMenu fallback";
            }
            else if (usingBossProgressionFallback)
            {
                stage = "ready via boss fallback";
            }
            else
            {
                stage = "ready";
            }
        }
        else
        {
            stage = usingGameMenuFallback ? "timer ready via fallback" : "boss pointers pending";
        }

        return IsTimerStartPending()
            ? $"{stage}; start pending"
            : stage;
    }

    private string BuildOperationalStatus()
    {
        if (process is null)
        {
            return status;
        }

        string operationalStatus;
        if (HasResolvedBossAddresses())
        {
            if (usingGameMenuFallback && usingBossProgressionFallback)
            {
                operationalStatus = $"attached to Terraria PID {process.Id}, ready via fallback";
            }
            else if (usingGameMenuFallback)
            {
                operationalStatus = $"attached to Terraria PID {process.Id}, ready via gameMenu fallback";
            }
            else if (usingBossProgressionFallback)
            {
                operationalStatus = $"attached to Terraria PID {process.Id}, ready via boss fallback";
            }
            else
            {
                operationalStatus = $"attached to Terraria PID {process.Id}";
            }
        }
        else
        {
            operationalStatus = usingGameMenuFallback
            ? $"attached to Terraria PID {process.Id}, timer ready via fallback; boss scan pending"
            : $"attached to Terraria PID {process.Id}, boss scan pending";
        }

        return IsTimerStartPending()
            ? $"{operationalStatus}; return to menu once to arm timer start"
            : operationalStatus;
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

    private string BuildCompatibilityHint()
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

        if (usingGameMenuFallback && usingBossProgressionFallback)
        {
            return "Fallback signatures resolved menu state and boss progression when the primary UpdateTime anchor was unavailable on this runtime.";
        }

        if (usingGameMenuFallback)
        {
            return "Fallback menu-state signature resolved a stronger UpdateTime-adjacent gameMenu access pattern when the direct UpdateTime anchor was unavailable on this runtime.";
        }

        if (usingBossProgressionFallback)
        {
            return "Boss progression fallback resolved hardmode and boss flags when the UpdateTime-relative boss pointer offsets were unavailable.";
        }

        if (updateTimeAddress == IntPtr.Zero)
        {
            return "UpdateTime did not match any scanned private or image executable page.";
        }

        if (gameMenuAddress == IntPtr.Zero)
        {
            return "UpdateTime matched, but the expected menu-state pointer offset did not resolve to readable memory.";
        }

        if (!HasResolvedBossAddresses())
        {
            return "gameMenu resolved, but boss and hardmode pointers are still pending or unreadable.";
        }

        return "Watcher resolved all current pointers.";
    }

    private static Process? FindTerrariaProcess()
    {
        Process[] processes = Process.GetProcessesByName(Terraria1456Memory.ProcessName);
        if (processes.Length == 0)
        {
            return null;
        }

        Process selected = processes
            .OrderByDescending(ProcessStartTimeOrMinValue)
            .First();

        foreach (Process process in processes)
        {
            if (!ReferenceEquals(process, selected))
            {
                process.Dispose();
            }
        }

        return selected;
    }

    private static DateTime ProcessStartTimeOrMinValue(Process process)
    {
        try
        {
            return process.StartTime;
        }
        catch (Win32Exception)
        {
            return DateTime.MinValue;
        }
        catch (InvalidOperationException)
        {
            return DateTime.MinValue;
        }
    }
}
