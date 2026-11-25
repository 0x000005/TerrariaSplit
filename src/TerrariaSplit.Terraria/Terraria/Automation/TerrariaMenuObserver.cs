using System.Diagnostics;
using TerrariaSplit.Compatibility;
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
        if (!IsSupported(version)) { process.Dispose(); throw new InvalidOperationException("No state-driven menu implementation is registered for this Terraria version."); }
        memory = new ProcessMemoryReader(process);
        identity = TerrariaProcessIdentity.FromProcess(process);
    }

    internal static bool IsSupported(string? version)
    {
        TerrariaFeatureMatch match = TerrariaFeatureCatalog.Resolve(TerrariaFeature.ForegroundWorldCreation, version);
        return match.CanAttempt && match.Implementation == TerrariaImplementation.StateDrivenMenu;
    }

    public TerrariaMenuSnapshot Read()
    {
        if (process.HasExited) return TerrariaMenuSnapshot.Unknown("Terraria exited.");
        layout = TerrariaRuntimeLayoutCache.Shared.TryGet(identity, out TerrariaRuntimeMemoryLayout? shared, TerrariaFeature.ForegroundWorldCreation)
            ? shared!.MenuAutomation : null;
        return layout is { } current ? reader.Read(memory, current) : TerrariaMenuSnapshot.Unknown("Menu layout not resolved.");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        TerrariaRuntimeMemoryLayout shared = await TerrariaRuntimeLayoutCache.Shared.GetAsync(identity, null, cancellationToken,
            TerrariaFeature.ForegroundWorldCreation).ConfigureAwait(false);
        layout = shared.MenuAutomation ?? throw new InvalidOperationException("Shared runtime layout has no state-driven menu fields.");
    }

    public bool TryReadButton(TerrariaMenuSnapshot menu, TerrariaMenuTarget target, out TerrariaMenuButtonPosition position, out string detail)
    {
        position = default;
        detail = "Menu button layout is not resolved.";
        return layout is { } current && buttons.TryRead(memory, current, menu, target, out position, out detail);
    }

    public void Dispose() => process.Dispose();
}
