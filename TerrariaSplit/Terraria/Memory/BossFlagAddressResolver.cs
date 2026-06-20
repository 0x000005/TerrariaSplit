namespace TerrariaSplit;

internal sealed record BossFlagAddressResolution(
    BossFlagMemoryBlock BossFlags,
    IntPtr HardmodeAddress,
    bool UsedProgressionFallback);

internal sealed class BossFlagAddressResolver
{
    public bool TryResolve(
        IProcessMemoryReader memory,
        TerrariaMemoryProfile profile,
        IntPtr? updateTimeAddress,
        out BossFlagAddressResolution resolution,
        out SignatureScanDiagnostics? scanDiagnostics)
    {
        scanDiagnostics = null;
        if (updateTimeAddress.HasValue &&
            TryResolvePrimary(memory, profile, updateTimeAddress.Value, out resolution))
        {
            return true;
        }

        return TryResolveFromProgressionFallback(
            memory,
            profile,
            out resolution,
            out _,
            out scanDiagnostics);
    }

    public bool TryResolveFromProgressionFallback(
        IProcessMemoryReader memory,
        TerrariaMemoryProfile profile,
        out BossFlagAddressResolution resolution,
        out IntPtr fallbackAnchorAddress,
        out SignatureScanDiagnostics? scanDiagnostics)
    {
        resolution = null!;
        fallbackAnchorAddress = SignatureScanner.Scan(
            memory,
            profile.BossProgressionFallbackSignature,
            profile.SignatureScanScopeLabel,
            out SignatureScanDiagnostics fallbackScanDiagnostics);
        scanDiagnostics = fallbackScanDiagnostics;
        return fallbackAnchorAddress != IntPtr.Zero &&
            TryResolveFromProgressionFallback(memory, profile, fallbackAnchorAddress, out resolution);
    }

    private static bool TryResolvePrimary(
        IProcessMemoryReader memory,
        TerrariaMemoryProfile profile,
        IntPtr updateTimeAddress,
        out BossFlagAddressResolution resolution)
    {
        resolution = null!;
        IntPtr bossFlagsPointerLocation = IntPtr.Add(updateTimeAddress, profile.BossFlagsPointerOffset);
        if (!memory.TryReadPointer(bossFlagsPointerLocation, out IntPtr bossFlagsBaseAddress))
        {
            return false;
        }

        IntPtr hardmodePointerLocation = IntPtr.Add(updateTimeAddress, profile.HardmodePointerOffset);
        if (!memory.TryReadPointer(hardmodePointerLocation, out IntPtr hardmodeAddress))
        {
            return false;
        }

        if (!Validate(memory, profile, bossFlagsBaseAddress, hardmodeAddress))
        {
            return false;
        }

        resolution = new BossFlagAddressResolution(
            new BossFlagMemoryBlock(bossFlagsBaseAddress),
            hardmodeAddress,
            UsedProgressionFallback: false);
        return true;
    }

    private static bool TryResolveFromProgressionFallback(
        IProcessMemoryReader memory,
        TerrariaMemoryProfile profile,
        IntPtr fallbackAnchorAddress,
        out BossFlagAddressResolution resolution)
    {
        resolution = null!;
        IntPtr skeletronInlineAddressLocation = IntPtr.Add(
            fallbackAnchorAddress,
            profile.BossProgressionFallbackSkeletronInlineAddressOffset);
        if (!memory.TryReadPointer(skeletronInlineAddressLocation, out IntPtr skeletronAddress))
        {
            return false;
        }

        IntPtr hardmodeInlineAddressLocation = IntPtr.Add(
            fallbackAnchorAddress,
            profile.BossProgressionFallbackHardmodeInlineAddressOffset);
        if (!memory.TryReadPointer(hardmodeInlineAddressLocation, out IntPtr hardmodeAddress))
        {
            return false;
        }

        IntPtr bossFlagsBaseAddress = IntPtr.Add(
            skeletronAddress,
            -profile.SkeletronDefeatedFlagOffset);
        if (!Validate(memory, profile, bossFlagsBaseAddress, hardmodeAddress))
        {
            return false;
        }

        resolution = new BossFlagAddressResolution(
            new BossFlagMemoryBlock(bossFlagsBaseAddress),
            hardmodeAddress,
            UsedProgressionFallback: true);
        return true;
    }

    private static bool Validate(
        IProcessMemoryReader memory,
        TerrariaMemoryProfile profile,
        IntPtr bossFlagsBaseAddress,
        IntPtr hardmodeAddress)
    {
        return memory.TryReadBool(IntPtr.Add(bossFlagsBaseAddress, profile.SkeletronDefeatedFlagOffset), out _) &&
            memory.TryReadBool(IntPtr.Add(bossFlagsBaseAddress, profile.MoonLordDefeatedFlagOffset), out _) &&
            memory.TryReadBool(hardmodeAddress, out _);
    }
}
