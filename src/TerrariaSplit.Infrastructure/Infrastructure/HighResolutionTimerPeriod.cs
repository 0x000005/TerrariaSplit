using System.Runtime.InteropServices;

namespace TerrariaSplit.Infrastructure;

internal sealed class HighResolutionTimerPeriod : IDisposable
{
    private readonly uint milliseconds;

    private HighResolutionTimerPeriod(uint milliseconds)
    {
        this.milliseconds = milliseconds;
    }

    public static HighResolutionTimerPeriod? TryBegin(uint milliseconds)
    {
        try
        {
            return TimeBeginPeriod(milliseconds) == 0
                ? new HighResolutionTimerPeriod(milliseconds)
                : null;
        }
        catch (DllNotFoundException)
        {
            return null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        try
        {
            _ = TimeEndPeriod(milliseconds);
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint milliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint milliseconds);
}
