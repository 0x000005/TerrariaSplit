namespace TerrariaSplit.Terraria.Memory;

internal sealed record TerrariaItemMemoryLayout(
    IntPtr PlayerArrayStaticFieldAddress,
    IntPtr MyPlayerStaticFieldAddress,
    IntPtr MouseItemStaticFieldAddress,
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
    int ObjectReferenceSize) : TerrariaLocalPlayerMemoryLayout;
