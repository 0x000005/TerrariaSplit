namespace TerrariaSplit;

internal sealed record TerrariaBiomeMemoryLayout(
    IntPtr PlayerArrayStaticFieldAddress,
    IntPtr MyPlayerStaticFieldAddress,
    IReadOnlyDictionary<string, int> ZoneBitsByteFieldOffsets,
    int ManagedArrayLengthOffset,
    int ManagedArrayFirstElementOffset,
    int ObjectReferenceSize) : TerrariaLocalPlayerMemoryLayout;
