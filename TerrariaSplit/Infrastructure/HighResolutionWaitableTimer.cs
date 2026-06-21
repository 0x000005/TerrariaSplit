using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace TerrariaSplit.Infrastructure;

/// <summary>
/// Windows high-resolution waitable timer (Windows 10 1803+). Lets a thread sleep
/// until a precise due time without spin-waiting. <see cref="TryCreate"/> returns
/// null where the OS does not support the high-resolution flag, so callers must
/// keep a fallback wait strategy.
/// </summary>
internal sealed class HighResolutionWaitableTimer : IDisposable
{
    private const uint CreateWaitableTimerHighResolution = 0x00000002;
    private const uint TimerModifyState = 0x0002;
    private const uint Synchronize = 0x00100000;

    private readonly TimerWaitHandle waitHandle;

    private HighResolutionWaitableTimer(SafeWaitHandle handle)
    {
        waitHandle = new TimerWaitHandle(handle);
    }

    public WaitHandle WaitHandle => waitHandle;

    public static HighResolutionWaitableTimer? TryCreate()
    {
        try
        {
            SafeWaitHandle handle = CreateWaitableTimerExW(
                IntPtr.Zero,
                IntPtr.Zero,
                CreateWaitableTimerHighResolution,
                TimerModifyState | Synchronize);
            if (handle.IsInvalid)
            {
                handle.Dispose();
                return null;
            }

            return new HighResolutionWaitableTimer(handle);
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

    /// <summary>
    /// Arms the timer to signal once after <paramref name="dueTime"/>. Re-arming an
    /// active or signaled timer resets it to the nonsignaled state first, so callers
    /// never observe stale signals as long as they arm before each wait.
    /// </summary>
    public bool TrySet(TimeSpan dueTime)
    {
        long relativeDueTime = -Math.Max(1, dueTime.Ticks);
        return SetWaitableTimer(
            waitHandle.SafeWaitHandle,
            ref relativeDueTime,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            false);
    }

    public void Dispose()
    {
        waitHandle.Dispose();
    }

    private sealed class TimerWaitHandle : WaitHandle
    {
        public TimerWaitHandle(SafeWaitHandle handle)
        {
            SafeWaitHandle = handle;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeWaitHandle CreateWaitableTimerExW(
        IntPtr timerAttributes,
        IntPtr timerName,
        uint flags,
        uint desiredAccess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWaitableTimer(
        SafeWaitHandle timer,
        ref long dueTime,
        int period,
        IntPtr completionRoutine,
        IntPtr argToCompletionRoutine,
        bool resume);
}
