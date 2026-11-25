using TerrariaSplit.Compatibility;

namespace TerrariaSplit.Terraria.Memory;

internal sealed record TerrariaRuntimeMemoryLayout(
    string? TerrariaVersion,
    TerrariaCoreMemoryLayout Core,
    TerrariaBossMemoryLayout Boss,
    TerrariaItemMemoryLayout? Item,
    TerrariaNpcMemoryLayout? Npc,
    TerrariaBiomeMemoryLayout? Biome,
    TerrariaWorldCreationSeedMemoryLayout? SeedUi,
    TerrariaWorldGenerationMemoryLayout WorldGeneration,
    int ResolvedFieldCount,
    TerrariaSplit.MemoryBridge.Protocol.MenuAutomationLayoutDto? MenuAutomation = null,
    TerrariaSplit.MemoryBridge.Protocol.RuntimeStartupLayoutDto? Startup = null,
    IReadOnlyDictionary<string, string>? CapabilityErrors = null)
{
    public bool HasCore => Core.GameMenuStaticFieldAddress != IntPtr.Zero;

    public string? CacheValidationError()
    {
        if (!HasCore) return "Runtime layout has no game-menu address.";
        if (Startup is { ReadyAtProbe: false }) return "Terraria startup has not completed.";
        return null;
    }

    public string? ValidationError(TerrariaFeature feature)
    {
        TerrariaFeatureMatch match = TerrariaFeatureCatalog.Resolve(feature, TerrariaVersion);
        if (!match.CanAttempt) return match.Diagnostic;
        if (CapabilityErrors?.TryGetValue(feature.ToString(), out string? error) == true) return error;
        return feature switch
        {
            TerrariaFeature.GameStateDetection => CacheValidationError(),
            TerrariaFeature.BossAndEventDetection => Boss.ResolvedFactCount > 0 ? null : "Boss and event fields are unavailable.",
            TerrariaFeature.ItemDetection => Item is not null ? null : "Item fields are unavailable.",
            TerrariaFeature.BiomeDetection => Biome is not null ? null : "Biome fields are unavailable.",
            TerrariaFeature.NpcDetection => Npc is not null ? null : "NPC fields are unavailable.",
            TerrariaFeature.ForegroundWorldCreation when match.Implementation == TerrariaImplementation.LegacyMenu => null,
            TerrariaFeature.ForegroundWorldCreation when match.Implementation == TerrariaImplementation.StateDrivenMenu => MenuValidationError(),
            _ => throw new ArgumentException("This feature is not owned by the runtime layout.", nameof(feature))
        };
    }

    private string? MenuValidationError()
    {
        if (Startup is not { ReadyAtProbe: true, ShowSplashAddress: not 0, EngineLoadedAddress: not 0 })
            return "Waiting for Terraria startup: showSplash=false and IsEngineLoaded=true.";
        return MenuAutomation is null ? "Runtime layout has no menu metadata." : MenuAutomation.ValidationError();
    }
}

internal sealed record TerrariaCoreMemoryLayout(
    IntPtr GameMenuStaticFieldAddress,
    IntPtr StatusTextStaticFieldAddress,
    IntPtr MenuUiStaticFieldAddress);

internal sealed class TerrariaBossMemoryLayout
{
    public TerrariaBossMemoryLayout(IReadOnlyDictionary<string, IntPtr> factAddresses)
    {
        FactAddresses = new Dictionary<string, IntPtr>(factAddresses, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, IntPtr> FactAddresses { get; }

    public int ResolvedFactCount => FactAddresses.Count(pair => pair.Value != IntPtr.Zero);

    public bool TryGetFactAddress(string factKey, out IntPtr address)
    {
        return FactAddresses.TryGetValue(factKey, out address) && address != IntPtr.Zero;
    }
}

internal sealed record TerrariaWorldCreationSeedMemoryLayout(
    IntPtr MenuUiStaticFieldAddress,
    int UserInterfaceCurrentStateFieldOffset,
    int UiStateNestedReferenceScanStart,
    int UiStateNestedReferenceScanEnd,
    int WorldCreationAdvancedCreationStateFieldOffset,
    int WorldCreationAdvancedSeedPlateFieldOffset,
    int WorldNameFieldOffset,
    int SeedFieldOffset,
    int NamePlateFieldOffset,
    int SeedPlateFieldOffset,
    int CharacterNameButtonActualContentsOffset,
    int ObjectReferenceSize)
{
    public bool HasAdvancedState =>
        WorldCreationAdvancedCreationStateFieldOffset >= 0 &&
        WorldCreationAdvancedSeedPlateFieldOffset >= 0;
}

internal sealed record TerrariaWorldGenerationMemoryLayout(
    IntPtr StatusTextStaticFieldAddress,
    IntPtr CurrentGenerationProgressStaticFieldAddress,
    IntPtr CurrentControllerStaticFieldAddress,
    int GenerationProgressMessageFieldOffset,
    int GenerationProgressValueFieldOffset,
    int GenerationProgressTotalWeightedProgressFieldOffset,
    int GenerationProgressTotalWeightFieldOffset,
    int GenerationProgressCurrentPassWeightFieldOffset,
    int ControllerGeneratorFieldOffset,
    int WorldGeneratorCurrentPassFieldOffset,
    int GenPassNameFieldOffset)
{
    public bool HasStructuredProgress =>
        CurrentGenerationProgressStaticFieldAddress != IntPtr.Zero &&
        GenerationProgressMessageFieldOffset >= 0 &&
        GenerationProgressValueFieldOffset >= 0 &&
        GenerationProgressTotalWeightedProgressFieldOffset >= 0 &&
        GenerationProgressTotalWeightFieldOffset >= 0 &&
        GenerationProgressCurrentPassWeightFieldOffset >= 0;

    public bool HasStructuredController =>
        CurrentControllerStaticFieldAddress != IntPtr.Zero &&
        ControllerGeneratorFieldOffset >= 0 &&
        WorldGeneratorCurrentPassFieldOffset >= 0 &&
        GenPassNameFieldOffset >= 0;

    public bool HasStatusTextFallback => StatusTextStaticFieldAddress != IntPtr.Zero;

    public bool HasAnySource =>
        HasStructuredProgress ||
        HasStructuredController ||
        HasStatusTextFallback;
}
