using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using TerrariaSplit.MemoryBridge.Protocol;

namespace TerrariaSplit.Terraria.Memory;

internal readonly record struct TerrariaProcessIdentity(int ProcessId, long StartTimeUtcTicks)
{
    public static TerrariaProcessIdentity FromProcess(Process process) => new(process.Id, process.StartTime.ToUniversalTime().Ticks);
}

// Monitoring and automation share metadata, never sampled values or references to live UI objects.
internal sealed class TerrariaRuntimeLayoutCache(Func<TerrariaProcessIdentity, Task<TerrariaRuntimeMemoryLayout>> probe,
    Func<TerrariaProcessIdentity, RuntimeStartupLayoutDto, Task>? waitForStartup = null)
{
    public static TerrariaRuntimeLayoutCache Shared { get; } = new(ProbeAsync, TerrariaSplit.Terraria.Processes.TerrariaStartup.WaitAsync);
    private readonly object gate = new();
    private Entry? current;
    private int probeCount;

    public int ProbeCount { get { lock (gate) return probeCount; } }

    private sealed class Entry(TerrariaProcessIdentity identity)
    {
        public TerrariaProcessIdentity Identity { get; } = identity;
        public TerrariaRuntimeMemoryLayout? Layout;
        public Task<TerrariaRuntimeMemoryLayout>? Pending;
    }

    public bool TryGet(TerrariaProcessIdentity identity, out TerrariaRuntimeMemoryLayout? layout)
    {
        lock (gate)
        {
            layout = current is not null && current.Identity == identity ? current.Layout : null;
            if (layout?.CacheValidationError() is not null)
            {
                current!.Layout = null;
                layout = null;
            }
            return layout is not null;
        }
    }

    public async Task<TerrariaRuntimeMemoryLayout> GetAsync(
        TerrariaProcessIdentity identity, TerrariaRuntimeMemoryLayout? invalidatedLayout, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Task<TerrariaRuntimeMemoryLayout> pending;
        lock (gate)
        {
            if (current is null || current.Identity != identity) current = new Entry(identity);
            // New readers reuse the result. A lost memory address explicitly invalidates that result.
            if (current.Layout is { } cached)
            {
                if (!ReferenceEquals(cached, invalidatedLayout) && cached.CacheValidationError() is null) return cached;
                current.Layout = null;
            }
            if (current.Pending is null || current.Pending.IsCompleted)
            {
                current.Pending = ProbeAndStoreAsync(current);
            }
            pending = current.Pending;
        }
        // Cancelling automation must not cancel discovery awaited by normal monitoring.
        return await pending.WaitAsync(token).ConfigureAwait(false);
    }

    private async Task<TerrariaRuntimeMemoryLayout> ProbeAndStoreAsync(Entry entry)
    {
        async Task<TerrariaRuntimeMemoryLayout> ResolveOnce()
        {
            lock (gate) probeCount++;
            return await probe(entry.Identity).ConfigureAwait(false);
        }
        TerrariaRuntimeMemoryLayout layout = await ResolveOnce().ConfigureAwait(false);
        if (layout.Startup is { ReadyAtProbe: false } startup)
        {
            if (waitForStartup is null) throw new InvalidOperationException("Terraria has not completed startup.");
            await waitForStartup(entry.Identity, startup).ConfigureAwait(false);
            layout = await ResolveOnce().ConfigureAwait(false);
        }
        if (layout.CacheValidationError() is { } error) throw new InvalidOperationException(error);
        lock (gate) entry.Layout = layout;
        return layout;
    }

    private static async Task<TerrariaRuntimeMemoryLayout> ProbeAsync(TerrariaProcessIdentity identity)
    {
        var clock = Stopwatch.StartNew();
        FileAppLogger.Instance.Info($"MemoryBridge shared runtime-layout probe starting; pid={identity.ProcessId}.");
        MemoryBridgeCommandResult result = await new MemoryBridgeClient().ExecuteAsync(
            MemoryBridgeCommands.RuntimeLayout, TimeSpan.FromSeconds(15), CancellationToken.None,
            identity.ProcessId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        RuntimeLayoutResponse? response;
        try { response = JsonSerializer.Deserialize<RuntimeLayoutResponse>(result.StandardOutput); }
        catch (JsonException ex) { throw new InvalidOperationException(result.FailureDetail("Invalid runtime-layout response."), ex); }
        if (!result.Succeeded || response?.Success != true || response.Layout is null)
            throw new InvalidOperationException(response?.Error ?? result.FailureDetail("Runtime layout discovery failed."));
        if (response.Layout.ProcessStartTimeUtcTicks != identity.StartTimeUtcTicks)
            throw new InvalidOperationException("Runtime layout target changed during discovery.");
        TerrariaRuntimeMemoryLayout layout = response.Layout.ToRuntimeMemoryLayout();
        if (layout.CacheValidationError() is null)
        {
            using Process process = Process.GetProcessById(identity.ProcessId);
            if (TerrariaProcessIdentity.FromProcess(process) != identity) throw new InvalidOperationException("Runtime layout target changed before validation.");
            var memory = new ProcessMemoryReader(process);
            if (!memory.TryReadBool(layout.Core.GameMenuStaticFieldAddress, out _) ||
                layout.MenuAutomation is { } menu &&
                    (!memory.TryReadInt32(new IntPtr(menu.Statics["menuMode"]), out _) ||
                     !memory.TryReadBool(new IntPtr(menu.Statics["generatingWorld"]), out _)))
                throw new InvalidOperationException("Resolved runtime layout failed its live memory-read validation.");
        }
        FileAppLogger.Instance.Info($"MemoryBridge shared runtime-layout probe completed; pid={identity.ProcessId}; elapsedMs={clock.Elapsed.TotalMilliseconds:F0}; fields={layout.ResolvedFieldCount}; menuTypes={layout.MenuAutomation?.Types.Count ?? 0}.");
        return layout;
    }
}
