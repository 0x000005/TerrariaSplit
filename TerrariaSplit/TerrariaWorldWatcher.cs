using System.ComponentModel;
using System.Diagnostics;

namespace TerrariaSplit;

internal sealed class TerrariaWorldWatcher : IDisposable
{
    private static readonly SignaturePattern UpdateTimeSignature =
        SignaturePattern.Parse(Terraria1456Memory.UpdateTimeSignature);

    private readonly TimeSpan scanInterval = TimeSpan.FromSeconds(2);

    private Process? process;
    private ProcessMemoryReader? memory;
    private IntPtr gameMenuAddress;
    private IntPtr bossFlagsBaseAddress;
    private IntPtr hardmodeAddress;
    private bool? previousGameMenu;
    private DateTime nextScanUtc = DateTime.MinValue;
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

        if (!memory.TryReadBool(gameMenuAddress, out bool isGameMenu))
        {
            ResetResolvedAddresses();
            previousGameMenu = null;
            status = $"attached to Terraria PID {process.Id}, lost gameMenu pointer; rescanning";
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

        bool enteredWorld = previousGameMenu == true && !isGameMenu;
        previousGameMenu = isGameMenu;
        status = HasResolvedBossAddresses()
            ? $"attached to Terraria PID {process.Id}"
            : $"attached to Terraria PID {process.Id}, boss scan pending";

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
        previousGameMenu = null;

        Process? candidate = FindTerrariaProcess();
        if (candidate is null)
        {
            status = "waiting for Terraria.exe";
            return;
        }

        try
        {
            memory = new ProcessMemoryReader(candidate);
            process = candidate;
            nextScanUtc = DateTime.MinValue;
            status = $"attached to Terraria PID {process.Id}, scanning for 1.4.5.x memory";
        }
        catch (Win32Exception ex)
        {
            candidate.Dispose();
            status = $"cannot read Terraria process: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            candidate.Dispose();
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
        gameMenuAddress = IntPtr.Zero;
        bossFlagsBaseAddress = IntPtr.Zero;
        hardmodeAddress = IntPtr.Zero;
    }

    private void TryResolveMemoryAddresses()
    {
        if (process is null || memory is null || DateTime.UtcNow < nextScanUtc)
        {
            return;
        }

        nextScanUtc = DateTime.UtcNow + scanInterval;
        IntPtr updateTimeAddress = SignatureScanner.Scan(memory, UpdateTimeSignature);
        if (updateTimeAddress == IntPtr.Zero)
        {
            status = $"attached to Terraria PID {process.Id}, waiting for UpdateTime signature";
            return;
        }

        IntPtr pointerLocation = IntPtr.Add(updateTimeAddress, Terraria1456Memory.GameMenuPointerOffset);
        if (!memory.TryReadPointer(pointerLocation, out IntPtr resolvedGameMenuAddress))
        {
            status = $"attached to Terraria PID {process.Id}, found signature but not gameMenu pointer";
            return;
        }

        if (!memory.TryReadBool(resolvedGameMenuAddress, out bool isGameMenu))
        {
            status = $"attached to Terraria PID {process.Id}, found unreadable gameMenu pointer";
            return;
        }

        gameMenuAddress = resolvedGameMenuAddress;
        previousGameMenu = isGameMenu;

        bool bossReady = TryResolveBossAddresses(updateTimeAddress);
        status = bossReady
            ? $"attached to Terraria PID {process.Id}"
            : $"attached to Terraria PID {process.Id}, boss scan pending";
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
