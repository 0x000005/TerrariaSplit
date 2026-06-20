namespace TerrariaSplit;

internal sealed record BossFlagMemoryBlock(IntPtr BaseAddress);

internal sealed record TerrariaMemoryContext(
    BossFlagMemoryBlock? BossFlags,
    IntPtr HardmodeAddress,
    IntPtr LocalPlayerAddress,
    TerrariaItemMemoryLayout? ItemLayout,
    TerrariaNpcMemoryLayout? NpcLayout,
    TerrariaBiomeMemoryLayout? BiomeLayout,
    bool Is64Bit)
{
    public static TerrariaMemoryContext Empty(bool is64Bit)
    {
        return new TerrariaMemoryContext(null, IntPtr.Zero, IntPtr.Zero, null, null, null, is64Bit);
    }
}
