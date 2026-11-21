namespace TerrariaSplit.MemoryBridge.Protocol;

internal static class MemoryBridgeCommands
{
    public const string Inject = "inject";
    public const string RuntimeLayout = "runtime-layout";
    public const string RandomSeedBatch = "random-seed-batch";
}

internal sealed record RuntimeLayoutResponse(bool Success, string? Error, RuntimeLayoutDto? Layout);

internal sealed record RandomSeedBatchResponse(
    bool Success,
    string? Error,
    IReadOnlyList<string>? Seeds,
    uint? OsThreadId);

internal sealed record RuntimeLayoutDto(
    string? TerrariaVersion,
    long ProcessStartTimeUtcTicks,
    CoreLayoutDto Core,
    BossLayoutDto Boss,
    PlayerItemLayoutDto? Item,
    NpcLayoutDto? Npc,
    BiomeLayoutDto? Biome,
    SeedUiLayoutDto? SeedUi,
    WorldGenerationLayoutDto WorldGeneration,
    int ResolvedFieldCount,
    MenuAutomationLayoutDto? MenuAutomation,
    RuntimeStartupLayoutDto? Startup);

internal sealed record RuntimeStartupLayoutDto(long ShowSplashAddress, long EngineLoadedAddress, bool ReadyAtProbe);

internal sealed record MenuAutomationLayoutDto(
    Dictionary<string, long> Statics,
    Dictionary<string, int> Fields,
    Dictionary<string, long> Types,
    int[] KeyboardWordOffsets)
{
    internal static readonly string[] RequiredStatics = ["MenuUI", "gameMenu", "menuMode", "mouseLeft", "maxTilesX",
        "ActiveWorldFileData", "ActivePlayerFileData", "inputText", "_uiScaleUsed", "Evil", "generatingWorld", "WorldOptions",
        "mouseLeftRelease", "ScreenWidth", "ScreenHeight", "MouseX", "MouseY", "MainMenuPositions", "MainMenuLastDrew"];
    internal static readonly string[] RequiredFields = ["UI._currentState", "UI._isStateDirty", "UI._clickDisabledTimeRemaining",
        "UI._lastElementHover", "UI.LeftMouse", "UI.MousePosition", "WorldData.GameMode", "Pointer.WasDown", "Pointer.LastDown",
        "Character._player", "Character._selectedPicker", "World._optionSeed", "World._secretSeedTextsEntered", "Advanced._creationState",
        "Keyboard._textBox", "Keyboard._canSubmit", "TextBox._text", "TextBox._maxLength", "FileData._path", "Player.difficulty",
        "Player.hair", "Player.skinVariant", "Player.voiceVariant", "Player.voicePitchOffset", "Player.hairColor", "Player.skinColor",
        "Player.shirtColor", "Player.underShirtColor", "Player.pantsColor", "Player.shoeColor", "Player.eyeColor", "Option._enabled",
        "ReferenceList._items", "ReferenceList._size", "Element.Elements", "Element._dimensions", "Element._snapPoint",
        "Snap.Name", "Snap.<Id>k__BackingField", "PlayerSelect._newPanel", "WorldSelect._newPanel",
        "Character._charInfoCategoryButton", "Character._clothingStylesCategoryButton", "Character._pasteTemplateButton",
        "World._sizeButtons", "World._difficultyButtons", "World._evilButtons", "World._seedPlate", "World._advancedSeedButton",
        "Advanced._randomButton", "Advanced._seedPlate", "Advanced._seedButtons", "Advanced._backButton", "Keyboard._submitButton",
        "Keyboard._submitButton2", "Keyboard.outerLayer1", "Keyboard.outerLayer2"];
    internal static readonly string[] RequiredTypes = ["UI", "WorldData", "Pointer", "PlayerSelect", "WorldSelect", "Character",
        "World", "Advanced", "Keyboard", "TextBox", "FileData", "Player", "Option", "KeyboardState",
        "SeedOption.0", "SeedOption.1", "SeedOption.2", "SeedOption.3", "SeedOption.4", "SeedOption.5", "SeedOption.6",
        "SeedOption.7", "SeedOption.8", "SeedOption.9", "Element", "Snap"];

    public string? ValidationError()
    {
        string[] missing = RequiredStatics.Where(key => Statics.GetValueOrDefault(key) == 0).Select(key => "static:" + key)
            .Concat(RequiredFields.Where(key => !Fields.TryGetValue(key, out int offset) || offset < 0).Select(key => "field:" + key))
            .Concat(RequiredTypes.Where(key => Types.GetValueOrDefault(key) == 0).Select(key => "type:" + key)).ToArray();
        if (missing.Length > 0) return "Incomplete menu layout: " + string.Join(", ", missing);
        return KeyboardWordOffsets.Length != 8 || KeyboardWordOffsets.Any(offset => offset < 0) || KeyboardWordOffsets.Distinct().Count() != 8
            ? "Incomplete menu layout: KeyboardState word offsets." : null;
    }
}

internal sealed record CoreLayoutDto(
    long GameMenuStaticFieldAddress,
    long StatusTextStaticFieldAddress,
    long MenuUiStaticFieldAddress);

internal sealed record BossLayoutDto(Dictionary<string, long> FactStaticFieldAddresses);

internal sealed record PlayerItemLayoutDto(
    long PlayerArrayStaticFieldAddress,
    long MyPlayerStaticFieldAddress,
    long MouseItemStaticFieldAddress,
    int PlayerArmorFieldOffset,
    int PlayerDyeFieldOffset,
    int PlayerMiscEquipsFieldOffset,
    int PlayerMiscDyesFieldOffset,
    int PlayerTrashItemFieldOffset,
    int PlayerInventoryFieldOffset,
    int PlayerBankFieldOffset,
    int PlayerBank2FieldOffset,
    int PlayerBank3FieldOffset,
    int PlayerBank4FieldOffset,
    int ChestItemArrayFieldOffset,
    int ItemTypeFieldOffset,
    int ItemStackFieldOffset,
    int ManagedArrayLengthOffset,
    int ManagedArrayFirstElementOffset,
    int ObjectReferenceSize);

internal sealed record NpcLayoutDto(
    long NpcArrayStaticFieldAddress,
    int NpcTypeFieldOffset,
    int NpcActiveFieldOffset,
    int NpcTownNpcFieldOffset,
    int NpcHomelessFieldOffset,
    int NpcHomeTileXFieldOffset,
    int NpcHomeTileYFieldOffset,
    int ManagedArrayLengthOffset,
    int ManagedArrayFirstElementOffset,
    int ObjectReferenceSize);

internal sealed record BiomeLayoutDto(
    long PlayerArrayStaticFieldAddress,
    long MyPlayerStaticFieldAddress,
    Dictionary<string, int>? ZoneBitsByteFieldOffsets,
    int ManagedArrayLengthOffset,
    int ManagedArrayFirstElementOffset,
    int ObjectReferenceSize);

internal sealed record SeedUiLayoutDto(
    long MenuUiStaticFieldAddress,
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
    int ObjectReferenceSize);

internal sealed record WorldGenerationLayoutDto(
    long StatusTextStaticFieldAddress,
    long CurrentGenerationProgressStaticFieldAddress,
    long CurrentControllerStaticFieldAddress,
    int GenerationProgressMessageFieldOffset,
    int GenerationProgressValueFieldOffset,
    int GenerationProgressTotalWeightedProgressFieldOffset,
    int GenerationProgressTotalWeightFieldOffset,
    int GenerationProgressCurrentPassWeightFieldOffset,
    int ControllerGeneratorFieldOffset,
    int WorldGeneratorCurrentPassFieldOffset,
    int GenPassNameFieldOffset);
