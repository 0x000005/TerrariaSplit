namespace TerrariaSplit.Terraria.Memory;

internal readonly record struct TerrariaMemoryResolution(
    IntPtr GameMenuAddress,
    IntPtr StatusTextAddress,
    IntPtr MenuUiAddress,
    int BossFactAddressCount,
    IntPtr HardmodeAddress,
    IntPtr CurrentGenerationProgressAddress,
    IntPtr CurrentControllerAddress,
    bool HasSeedUiLayout)
{
    public bool HasGameMenuAddress => GameMenuAddress != IntPtr.Zero;

    public bool HasResolvedBossAddresses => BossFactAddressCount > 0;

    public bool HasResolvedWorldGenerationAddresses =>
        StatusTextAddress != IntPtr.Zero ||
        CurrentGenerationProgressAddress != IntPtr.Zero &&
        CurrentControllerAddress != IntPtr.Zero;
}
