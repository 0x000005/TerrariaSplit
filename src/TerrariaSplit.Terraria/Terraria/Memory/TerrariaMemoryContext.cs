namespace TerrariaSplit.Terraria.Memory;

internal sealed record BossFlagMemoryBlock(IntPtr BaseAddress);

internal sealed record TerrariaMemoryContext(
    TerrariaBossMemoryLayout? BossLayout,
    IntPtr LocalPlayerAddress,
    TerrariaItemMemoryLayout? ItemLayout,
    TerrariaNpcMemoryLayout? NpcLayout,
    TerrariaBiomeMemoryLayout? BiomeLayout,
    bool Is64Bit)
{
    public TerrariaMemoryContext(
        BossFlagMemoryBlock? bossFlags,
        IntPtr hardmodeAddress,
        IntPtr localPlayerAddress,
        TerrariaItemMemoryLayout? itemLayout,
        TerrariaNpcMemoryLayout? npcLayout,
        TerrariaBiomeMemoryLayout? biomeLayout,
        bool Is64Bit)
        : this(
            CreateLegacyBossLayout(hardmodeAddress),
            localPlayerAddress,
            itemLayout,
            npcLayout,
            biomeLayout,
            Is64Bit)
    {
        _ = bossFlags;
    }

    public static TerrariaMemoryContext Empty(bool is64Bit)
    {
        return new TerrariaMemoryContext(null, IntPtr.Zero, null, null, null, is64Bit);
    }

    private static TerrariaBossMemoryLayout? CreateLegacyBossLayout(IntPtr hardmodeAddress)
    {
        if (hardmodeAddress == IntPtr.Zero)
        {
            return null;
        }

        BossFactDescriptor hardmodeFact = SplitCatalog.BossFacts.First(boss =>
            boss.AddressKind == BossFactAddressKind.Hardmode);
        return new TerrariaBossMemoryLayout(new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase)
        {
            [hardmodeFact.FactKey] = hardmodeAddress
        });
    }
}
