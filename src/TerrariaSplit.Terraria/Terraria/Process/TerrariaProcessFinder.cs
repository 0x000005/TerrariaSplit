using System.ComponentModel;
using System.Diagnostics;
using Process = System.Diagnostics.Process;

namespace TerrariaSplit.Terraria.Processes;

internal static class TerrariaProcessFinder
{
    private const string ProcessName = "Terraria";

    public static Process? FindNewest()
    {
        Process[] processes = Process.GetProcessesByName(ProcessName);
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
