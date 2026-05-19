namespace TerrariaSplit;

internal readonly record struct TerrariaMemoryResolution(
    IntPtr UpdateTimeAddress,
    IntPtr GameMenuAddress,
    IntPtr GameMenuSecondaryAddress,
    IntPtr BossFlagsBaseAddress,
    IntPtr HardmodeAddress,
    IntPtr CurrentGenerationProgressAddress,
    IntPtr CurrentControllerAddress,
    bool UsingGameMenuFallback,
    bool UsingBossProgressionFallback)
{
    public bool HasGameMenuAddress => GameMenuAddress != IntPtr.Zero;

    public bool HasResolvedBossAddresses => BossFlagsBaseAddress != IntPtr.Zero && HardmodeAddress != IntPtr.Zero;

    public bool HasResolvedWorldGenerationAddresses =>
        CurrentGenerationProgressAddress != IntPtr.Zero &&
        CurrentControllerAddress != IntPtr.Zero;
}
