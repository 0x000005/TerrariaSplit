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
    CoreLayoutDto Core,
    BossLayoutDto Boss,
    PlayerItemLayoutDto? Item,
    NpcLayoutDto? Npc,
    BiomeLayoutDto? Biome,
    SeedUiLayoutDto? SeedUi,
    WorldGenerationLayoutDto WorldGeneration,
    int ResolvedFieldCount);

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
