namespace TerrariaSplit;

internal sealed record TerrariaNpcMemoryLayout(
    IntPtr NpcArrayStaticFieldAddress,
    int NpcTypeFieldOffset,
    int NpcActiveFieldOffset,
    int NpcTownNpcFieldOffset,
    int NpcHomelessFieldOffset,
    int NpcHomeTileXFieldOffset,
    int NpcHomeTileYFieldOffset,
    int ManagedArrayLengthOffset,
    int ManagedArrayFirstElementOffset,
    int ObjectReferenceSize);
