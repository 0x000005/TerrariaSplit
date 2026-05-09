using System.Diagnostics;
using System.ComponentModel;

namespace TerrariaSplit;

internal sealed class TerrariaMenuStateReader : IDisposable
{
    private static readonly HashSet<int> KnownMenuModes = new()
    {
        -71, -7, -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
        16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 30, 31, 40, 100,
        111, 112, 131, 200, 201, 222, 252, 888, 889, 1212, 1213, 2008, 3000,
        5000, 1000000, 1000001, 272727
    };

    private Process? process;
    private ProcessMemoryReader? memory;
    private IntPtr menuModeAddress;
    private List<MenuModeCandidate> candidates = new();

    public bool TryReadMenuMode(out int menuMode)
    {
        return TryReadMenuMode(out menuMode, preferredModes: null);
    }

    public bool TryReadMenuMode(out int menuMode, IReadOnlyCollection<int>? preferredModes)
    {
        menuMode = 0;
        if (!HasLiveProcess())
        {
            Attach();
        }

        if (memory is null)
        {
            return false;
        }

        if (menuModeAddress == IntPtr.Zero)
        {
            EnsureCandidates(memory);
            menuModeAddress = ResolveMenuModeAddress(memory, candidates, preferredModes);
        }

        if (menuModeAddress != IntPtr.Zero &&
            memory.TryReadInt32(menuModeAddress, out menuMode) &&
            IsPlausibleMenuMode(menuMode))
        {
            if (preferredModes is null || preferredModes.Contains(menuMode))
            {
                return true;
            }
        }

        if (preferredModes is not null)
        {
            EnsureCandidates(memory);
            IntPtr preferredAddress = ResolveMenuModeAddress(memory, candidates, preferredModes);
            if (preferredAddress != IntPtr.Zero &&
                preferredAddress != menuModeAddress &&
                memory.TryReadInt32(preferredAddress, out int preferredMode) &&
                IsPlausibleMenuMode(preferredMode))
            {
                menuModeAddress = preferredAddress;
                menuMode = preferredMode;
                return true;
            }
        }

        return menuModeAddress != IntPtr.Zero && IsPlausibleMenuMode(menuMode);
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

    private void Attach()
    {
        process?.Dispose();
        process = null;
        memory = null;
        menuModeAddress = IntPtr.Zero;
        candidates.Clear();

        Process? candidate = FindTerrariaProcess();
        if (candidate is null)
        {
            return;
        }

        try
        {
            memory = new ProcessMemoryReader(candidate);
            process = candidate;
        }
        catch (Win32Exception ex)
        {
            AppLogger.Error(ex, "Cannot read Terraria process for menu state.");
            candidate.Dispose();
        }
        catch (InvalidOperationException ex)
        {
            AppLogger.Error(ex, "Cannot attach to Terraria process for menu state.");
            candidate.Dispose();
        }
    }

    private void EnsureCandidates(ProcessMemoryReader reader)
    {
        if (candidates.Count > 0)
        {
            return;
        }

        candidates = FindMenuModeCandidates(reader);
    }

    private static IntPtr ResolveMenuModeAddress(
        ProcessMemoryReader reader,
        IEnumerable<MenuModeCandidate> candidates,
        IReadOnlyCollection<int>? preferredModes)
    {
        foreach (MenuModeCandidate candidate in candidates)
        {
            if (!reader.TryReadInt32(candidate.Address, out int value) || !IsPlausibleMenuMode(value))
            {
                continue;
            }

            if (preferredModes is null || preferredModes.Contains(value))
            {
                return candidate.Address;
            }
        }

        return IntPtr.Zero;
    }

    private static List<MenuModeCandidate> FindMenuModeCandidates(ProcessMemoryReader reader)
    {
        Dictionary<uint, int> scores = new();
        foreach (MemoryPage page in reader.ExecutablePrivatePages())
        {
            if (page.RegionSize <= 0 || page.RegionSize > 64 * 1024 * 1024)
            {
                continue;
            }

            if (!reader.TryReadBytes(page.BaseAddress, checked((int)page.RegionSize), out byte[]? bytes))
            {
                continue;
            }

            for (int i = 0; i < bytes.Length - 10; i++)
            {
                if (bytes[i] == 0x81 && bytes[i + 1] == 0x3D)
                {
                    int value = BitConverter.ToInt32(bytes, i + 6);
                    if (KnownMenuModes.Contains(value))
                    {
                        AddScore(scores, BitConverter.ToUInt32(bytes, i + 2));
                    }
                }
                else if (bytes[i] == 0x83 && bytes[i + 1] == 0x3D)
                {
                    int value = unchecked((sbyte)bytes[i + 6]);
                    if (KnownMenuModes.Contains(value))
                    {
                        AddScore(scores, BitConverter.ToUInt32(bytes, i + 2));
                    }
                }
                else if (bytes[i] == 0xC7 && bytes[i + 1] == 0x05)
                {
                    int value = BitConverter.ToInt32(bytes, i + 6);
                    if (KnownMenuModes.Contains(value))
                    {
                        AddScore(scores, BitConverter.ToUInt32(bytes, i + 2), 2);
                    }
                }
            }
        }

        return scores
            .OrderByDescending(pair => pair.Value)
            .Select(pair => new MenuModeCandidate(new IntPtr(unchecked((int)pair.Key)), pair.Value))
            .ToList();
    }

    private static void AddScore(Dictionary<uint, int> scores, uint address, int amount = 1)
    {
        scores.TryGetValue(address, out int score);
        scores[address] = score + amount;
    }

    private static bool IsPlausibleMenuMode(int menuMode)
    {
        return menuMode is >= -100 and <= 1000001 || menuMode == 272727;
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
        catch
        {
            return DateTime.MinValue;
        }
    }

    private readonly record struct MenuModeCandidate(IntPtr Address, int Score);
}
