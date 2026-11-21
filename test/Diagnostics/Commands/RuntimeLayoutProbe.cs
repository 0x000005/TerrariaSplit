using System.Diagnostics;
using System.Text.Json;
using TerrariaSplit.Terraria.Memory;
using TerrariaSplit.Terraria.Processes;

namespace TerrariaSplit.Diagnostics;

internal static class RuntimeLayoutProbe
{
    public static bool TryRun(string[] args)
    {
        if (args.Length == 0 || args[0] != "runtime-layout-probe") return false;
        if (args.Length != 2 || !int.TryParse(args[1], out int pid))
            throw new ArgumentException("Usage: runtime-layout-probe <Terraria 1.4.5.8 pid>");
        using Process process = Process.GetProcessById(pid);
        using Process? newest = TerrariaProcessFinder.FindNewest();
        if (newest?.Id != pid) throw new InvalidOperationException("The target must be the newest Terraria process.");
        var memory = new ProcessMemoryReader(process);
        var resolver = new TerrariaClrMemoryResolver();
        resolver.SetProcess(process);
        int before = TerrariaRuntimeLayoutCache.Shared.ProbeCount;
        var clock = Stopwatch.StartNew();
        if (!resolver.TryGetRuntimeLayout(memory, out TerrariaRuntimeMemoryLayout initial))
        {
            _ = TerrariaRuntimeLayoutCache.Shared.GetAsync(TerrariaProcessIdentity.FromProcess(process), null, CancellationToken.None)
                .GetAwaiter().GetResult();
            resolver.ResetLayout();
            if (!resolver.TryGetRuntimeLayout(memory, out initial)) throw new InvalidOperationException(resolver.ProbeDiagnostics.LastError);
        }
        double initializationMs = clock.Elapsed.TotalMilliseconds;
        var automation = new List<object>();
        for (int run = 0; run < 3; run++)
        {
            clock.Restart();
            using var observer = new TerrariaMenuObserver();
            observer.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
            double elapsed = clock.Elapsed.TotalMilliseconds;
            TerrariaMenuSnapshot state = observer.Read();
            if (!state.Valid) throw new InvalidOperationException(state.Detail);
            if (state.Page is TerrariaMenuPage.World or TerrariaMenuPage.Advanced &&
                (state.SpecialSeeds is null || state.SecretSeeds is null))
                throw new InvalidOperationException("World creation options were not readable: " + state.Detail);
            var buttons = Enum.GetValues<TerrariaMenuButton>().Select(button =>
            {
                var target = new TerrariaMenuTarget(button, button == TerrariaMenuButton.WorldSize ? 1 : 0);
                bool available = observer.TryReadButton(state, target, out TerrariaMenuButtonPosition position, out string detail);
                return new { Button = target.ToString(), Available = available, position.Point, Detail = detail };
            }).ToArray();
            automation.Add(new { Run = run + 1, InitializeMs = elapsed, Page = state.Page.ToString(), state.Ready,
                state.SpecialSeeds, state.SecretSeeds, state.TextMaximumLength, state.ActivePlayerPath, state.Detail, Buttons = buttons });
        }
        var secondResolver = new TerrariaClrMemoryResolver();
        secondResolver.SetProcess(process);
        if (!secondResolver.TryGetRuntimeLayout(memory, out TerrariaRuntimeMemoryLayout reused) || !ReferenceEquals(initial, reused))
            throw new InvalidOperationException("A new monitoring reader did not reuse the same layout.");
        int probes = TerrariaRuntimeLayoutCache.Shared.ProbeCount - before;
        if (probes is not (1 or 2)) throw new InvalidOperationException($"Expected one full shared probe, with at most one cold-start bootstrap, got {probes}.");
        Console.WriteLine(JsonSerializer.Serialize(new { ProcessId = pid, InitializationMs = initializationMs,
            SharedProbeCount = probes, Startup = initial.Startup, ReusedSameLayout = true, MenuTypeCount = initial.MenuAutomation?.Types.Count,
            MenuFieldCount = initial.MenuAutomation?.Fields.Count, Automation = automation }, new JsonSerializerOptions { WriteIndented = true }));
        return true;
    }
}
