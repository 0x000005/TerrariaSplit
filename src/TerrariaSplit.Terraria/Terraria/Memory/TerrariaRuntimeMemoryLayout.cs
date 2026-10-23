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
    TerrariaSplit.MemoryBridge.Protocol.RuntimeStartupLayoutDto? Startup = null)
{
    public bool HasCore => Core.GameMenuStaticFieldAddress != IntPtr.Zero;

    public string? CacheValidationError()
    {
        if (!HasCore) return "Runtime layout has no game-menu address.";
        if (!Version.TryParse(TerrariaVersion?.Trim().TrimStart('v'), out Version? version) || version != new Version(1, 4, 5, 8)) return null;
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
