using System.Diagnostics;
using TerrariaSplit.MemoryBridge.Protocol;
using TerrariaSplit.Terraria.Memory;

namespace TerrariaSplit.Terraria.Automation;

internal interface ITerrariaMenuObserver : IDisposable
{
    TerrariaMenuSnapshot Read();
    bool TryReadButton(TerrariaMenuSnapshot menu, TerrariaMenuTarget target, out TerrariaMenuButtonPosition position, out string detail);
    Task InitializeAsync(CancellationToken cancellationToken);
}

internal sealed class TerrariaMenuObserver : ITerrariaMenuObserver
{
    private readonly Process process;
    private readonly ProcessMemoryReader memory;
    private readonly TerrariaProcessIdentity identity;
    private readonly TerrariaMenuStateReader reader = new();
    private readonly TerrariaMenuButtonReader buttons = new();
    private MenuAutomationLayoutDto? layout;

    public TerrariaMenuObserver()
    {
        process = TerrariaProcessFinder.FindNewest() ?? throw new InvalidOperationException("Terraria process not found.");
        string? version = process.MainModule?.FileVersionInfo.FileVersion;
        if (!IsSupported(version)) { process.Dispose(); throw new InvalidOperationException("State-driven menus require Terraria 1.4.5.8."); }
        memory = new ProcessMemoryReader(process);
        identity = TerrariaProcessIdentity.FromProcess(process);
    }

    internal static bool IsSupported(string? version) => Version.TryParse(version?.Trim().TrimStart('v'), out Version? value) && value == new Version(1, 4, 5, 8);

    public TerrariaMenuSnapshot Read()
    {
        if (process.HasExited) return TerrariaMenuSnapshot.Unknown("Terraria exited.");
        if (TerrariaRuntimeLayoutCache.Shared.TryGet(identity, out TerrariaRuntimeMemoryLayout? shared))
            layout = shared!.MenuAutomation;
        return layout is { } current ? reader.Read(memory, current) : TerrariaMenuSnapshot.Unknown("Menu layout not resolved.");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        TerrariaRuntimeMemoryLayout shared = await TerrariaRuntimeLayoutCache.Shared.GetAsync(identity, null, cancellationToken).ConfigureAwait(false);
        layout = shared.MenuAutomation ?? throw new InvalidOperationException("Shared runtime layout has no Terraria 1.4.5.8 menu fields.");
    }

    public bool TryReadButton(TerrariaMenuSnapshot menu, TerrariaMenuTarget target, out TerrariaMenuButtonPosition position, out string detail)
    {
        position = default;
        detail = "Menu button layout is not resolved.";
        return layout is { } current && buttons.TryRead(memory, current, menu, target, out position, out detail);
    }

    public void Dispose() => process.Dispose();
}
