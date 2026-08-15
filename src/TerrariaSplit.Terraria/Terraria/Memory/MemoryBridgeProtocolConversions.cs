using TerrariaSplit.MemoryBridge.Protocol;

namespace TerrariaSplit.Terraria.Memory;

internal static class MemoryBridgeProtocolConversions
{
    public static TerrariaRuntimeMemoryLayout ToRuntimeMemoryLayout(this RuntimeLayoutDto layout)
    {
        return new TerrariaRuntimeMemoryLayout(
            layout.TerrariaVersion,
            layout.Core.ToCoreLayout(),
            layout.Boss.ToBossLayout(),
            layout.Item?.ToItemMemoryLayout(),
            layout.Npc?.ToNpcMemoryLayout(),
            layout.Biome?.ToBiomeMemoryLayout(),
            layout.SeedUi?.ToSeedUiLayout(),
            layout.WorldGeneration.ToWorldGenerationLayout(),
            layout.ResolvedFieldCount);
    }

    private static TerrariaCoreMemoryLayout ToCoreLayout(this CoreLayoutDto layout)
    {
        return new TerrariaCoreMemoryLayout(
            ToIntPtr(layout.GameMenuStaticFieldAddress),
            ToIntPtr(layout.StatusTextStaticFieldAddress),
            ToIntPtr(layout.MenuUiStaticFieldAddress));
    }

    private static TerrariaBossMemoryLayout ToBossLayout(this BossLayoutDto layout)
    {
        Dictionary<string, IntPtr> addresses = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string factKey, long address) in layout.FactStaticFieldAddresses)
        {
            addresses[factKey] = ToIntPtr(address);
        }

        return new TerrariaBossMemoryLayout(addresses);
    }

    private static TerrariaItemMemoryLayout ToItemMemoryLayout(this PlayerItemLayoutDto layout)
    {
        return new TerrariaItemMemoryLayout(
            ToIntPtr(layout.PlayerArrayStaticFieldAddress),
            ToIntPtr(layout.MyPlayerStaticFieldAddress),
            ToIntPtr(layout.MouseItemStaticFieldAddress),
            layout.PlayerArmorFieldOffset,
            layout.PlayerDyeFieldOffset,
            layout.PlayerMiscEquipsFieldOffset,
            layout.PlayerMiscDyesFieldOffset,
            layout.PlayerTrashItemFieldOffset,
            layout.PlayerInventoryFieldOffset,
            layout.PlayerBankFieldOffset,
            layout.PlayerBank2FieldOffset,
            layout.PlayerBank3FieldOffset,
            layout.PlayerBank4FieldOffset,
            layout.ChestItemArrayFieldOffset,
            layout.ItemTypeFieldOffset,
            layout.ItemStackFieldOffset,
            layout.ManagedArrayLengthOffset,
            layout.ManagedArrayFirstElementOffset,
            layout.ObjectReferenceSize);
    }

    private static TerrariaNpcMemoryLayout ToNpcMemoryLayout(this NpcLayoutDto layout)
    {
        return new TerrariaNpcMemoryLayout(
            ToIntPtr(layout.NpcArrayStaticFieldAddress),
            layout.NpcTypeFieldOffset,
            layout.NpcActiveFieldOffset,
            layout.NpcTownNpcFieldOffset,
            layout.NpcHomelessFieldOffset,
            layout.NpcHomeTileXFieldOffset,
            layout.NpcHomeTileYFieldOffset,
            layout.ManagedArrayLengthOffset,
            layout.ManagedArrayFirstElementOffset,
            layout.ObjectReferenceSize);
    }

    private static TerrariaBiomeMemoryLayout ToBiomeMemoryLayout(this BiomeLayoutDto layout)
    {
        return new TerrariaBiomeMemoryLayout(
            ToIntPtr(layout.PlayerArrayStaticFieldAddress),
            ToIntPtr(layout.MyPlayerStaticFieldAddress),
            layout.ZoneBitsByteFieldOffsets ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            layout.ManagedArrayLengthOffset,
            layout.ManagedArrayFirstElementOffset,
            layout.ObjectReferenceSize);
    }

    private static TerrariaWorldCreationSeedMemoryLayout ToSeedUiLayout(this SeedUiLayoutDto layout)
    {
        return new TerrariaWorldCreationSeedMemoryLayout(
            ToIntPtr(layout.MenuUiStaticFieldAddress),
            layout.UserInterfaceCurrentStateFieldOffset,
            layout.UiStateNestedReferenceScanStart,
            layout.UiStateNestedReferenceScanEnd,
            layout.WorldCreationAdvancedCreationStateFieldOffset,
            layout.WorldCreationAdvancedSeedPlateFieldOffset,
            layout.WorldNameFieldOffset,
            layout.SeedFieldOffset,
            layout.NamePlateFieldOffset,
            layout.SeedPlateFieldOffset,
            layout.CharacterNameButtonActualContentsOffset,
            layout.ObjectReferenceSize);
    }

    private static TerrariaWorldGenerationMemoryLayout ToWorldGenerationLayout(
        this WorldGenerationLayoutDto layout)
    {
        return new TerrariaWorldGenerationMemoryLayout(
            ToIntPtr(layout.StatusTextStaticFieldAddress),
            ToIntPtr(layout.CurrentGenerationProgressStaticFieldAddress),
            ToIntPtr(layout.CurrentControllerStaticFieldAddress),
            layout.GenerationProgressMessageFieldOffset,
            layout.GenerationProgressValueFieldOffset,
            layout.GenerationProgressTotalWeightedProgressFieldOffset,
            layout.GenerationProgressTotalWeightFieldOffset,
            layout.GenerationProgressCurrentPassWeightFieldOffset,
            layout.ControllerGeneratorFieldOffset,
            layout.WorldGeneratorCurrentPassFieldOffset,
            layout.GenPassNameFieldOffset);
    }

    private static IntPtr ToIntPtr(long address)
    {
        return IntPtr.Size == 8
            ? new IntPtr(address)
            : new IntPtr(unchecked((int)address));
    }
}
