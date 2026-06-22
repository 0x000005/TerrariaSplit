using System.Diagnostics;
using Process = System.Diagnostics.Process;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class ProcessLifecycleGuard : IDisposable
{
    private static readonly TimeSpan KillWaitTimeout = TimeSpan.FromSeconds(5);

    private readonly Action<int?>? clearTracking;
    private readonly string killFailureMessage;
    private Process? process;
    private int? processId;
    private bool disposed;

    public ProcessLifecycleGuard(
        Process process,
        Action<Process>? track = null,
        Action<int?>? clearTracking = null,
        string killFailureMessage = "Failed to stop process.")
    {
        this.process = process;
        this.clearTracking = clearTracking;
        this.killFailureMessage = killFailureMessage;
        processId = TryGetProcessId(process);
        track?.Invoke(process);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Process? processToKill = process;
        process = null;
        if (processToKill is not null)
        {
            processId ??= TryGetProcessId(processToKill);
        }

        TryKill(processToKill, killFailureMessage);
        clearTracking?.Invoke(processId);
    }

    public static bool ProcessIdMatches(Process process, int? processId)
    {
        if (!processId.HasValue)
        {
            return true;
        }

        try
        {
            return process.Id == processId.Value;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            return true;
        }
    }

    public static int? TryGetProcessId(Process process)
    {
        try
        {
            return process.Id;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            return null;
        }
    }

    public static bool TryGetProcessStartTimeUtcTicks(Process process, out long ticks)
    {
        try
        {
            ticks = process.StartTime.ToUniversalTime().Ticks;
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            ticks = 0;
            return false;
        }
    }

    public static bool TryGetProcessPath(Process process, out string? path)
    {
        try
        {
            path = process.MainModule?.FileName;
            return !string.IsNullOrWhiteSpace(path);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            path = null;
            return false;
        }
    }

    public static void TryKill(Process? process, string failureMessage)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit((int)KillWaitTimeout.TotalMilliseconds);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException or ObjectDisposedException)
        {
            StaticAppLogger.Instance.Error(ex, failureMessage);
        }
        finally
        {
            process.Dispose();
        }
    }
}
