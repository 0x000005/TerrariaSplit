using System.Diagnostics;
using TerrariaSplit.MemoryBridge.Protocol;
using TerrariaSplit.Terraria.Memory;

namespace TerrariaSplit.Terraria.Processes;

internal static class TerrariaStartup
{
    public static async Task WaitAsync(TerrariaProcessIdentity identity, RuntimeStartupLayoutDto startup)
    {
        if (startup.ShowSplashAddress == 0 || startup.EngineLoadedAddress == 0)
            throw new InvalidOperationException("Terraria startup flag addresses are unavailable.");
        using Process process = Process.GetProcessById(identity.ProcessId);
        if (TerrariaProcessIdentity.FromProcess(process) != identity)
            throw new InvalidOperationException("Terraria restarted while waiting for startup.");
        var memory = new ProcessMemoryReader(process);
        AppLog.ForSource().LogInformation($"Waiting for Terraria loading to finish; pid={identity.ProcessId}; no complete runtime layout has been cached.");
        while (!process.HasExited)
        {
            if (!TryReadReadiness(memory, startup, out bool ready))
                throw new InvalidOperationException("Terraria startup flags became unreadable.");
            if (ready) return;
            await Task.Delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
        }
        throw new InvalidOperationException("Terraria exited while waiting for startup.");
    }

    internal static bool TryReadReadiness(IProcessMemoryReader memory, RuntimeStartupLayoutDto startup, out bool ready)
    {
        ready = false;
        if (startup.ShowSplashAddress == 0 || startup.EngineLoadedAddress == 0 ||
            !memory.TryReadBool(new IntPtr(startup.ShowSplashAddress), out bool splash) ||
            !memory.TryReadBool(new IntPtr(startup.EngineLoadedAddress), out bool engine)) return false;
        ready = !splash && engine;
        return true;
    }
}
