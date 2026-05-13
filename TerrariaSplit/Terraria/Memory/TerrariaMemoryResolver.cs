namespace TerrariaSplit;

internal sealed class TerrariaMemoryResolver
{
    private readonly TerrariaMemoryProfile profile;
    private IntPtr updateTimeAddress;
    private IntPtr gameMenuAddress;
    private IntPtr gameMenuSecondaryAddress;
    private IntPtr bossFlagsBaseAddress;
    private IntPtr hardmodeAddress;
    private bool usingGameMenuFallback;
    private bool usingBossProgressionFallback;

    public TerrariaMemoryResolver(TerrariaMemoryProfile profile)
    {
        this.profile = profile;
    }

    public int SignatureScanAttempts { get; private set; }

    public DateTime? LastSignatureScanUtc { get; private set; }

    public SignatureScanDiagnostics? LastSignatureScan { get; private set; }

    public TerrariaMemoryResolution Resolution => new(
        updateTimeAddress,
        gameMenuAddress,
        gameMenuSecondaryAddress,
        bossFlagsBaseAddress,
        hardmodeAddress,
        usingGameMenuFallback,
        usingBossProgressionFallback);

    public bool HasGameMenuAddress => gameMenuAddress != IntPtr.Zero;

    public bool HasResolvedBossAddresses => bossFlagsBaseAddress != IntPtr.Zero && hardmodeAddress != IntPtr.Zero;

    public void Reset()
    {
        updateTimeAddress = IntPtr.Zero;
        gameMenuAddress = IntPtr.Zero;
        gameMenuSecondaryAddress = IntPtr.Zero;
        bossFlagsBaseAddress = IntPtr.Zero;
        hardmodeAddress = IntPtr.Zero;
        usingGameMenuFallback = false;
        usingBossProgressionFallback = false;
        SignatureScanAttempts = 0;
        LastSignatureScanUtc = null;
        LastSignatureScan = null;
    }

    public void ResetResolvedAddresses()
    {
        updateTimeAddress = IntPtr.Zero;
        gameMenuAddress = IntPtr.Zero;
        gameMenuSecondaryAddress = IntPtr.Zero;
        bossFlagsBaseAddress = IntPtr.Zero;
        hardmodeAddress = IntPtr.Zero;
        usingGameMenuFallback = false;
        usingBossProgressionFallback = false;
    }

    public TerrariaMemoryResolveResult Resolve(IProcessMemoryReader memory)
    {
        SignatureScanAttempts++;
        LastSignatureScanUtc = DateTime.UtcNow;

        IntPtr resolvedUpdateTimeAddress = SignatureScanner.Scan(
            memory,
            profile.UpdateTimeSignature,
            profile.SignatureScanScopeLabel,
            out SignatureScanDiagnostics updateTimeScanDiagnostics);
        LastSignatureScan = updateTimeScanDiagnostics;
        if (resolvedUpdateTimeAddress != IntPtr.Zero)
        {
            updateTimeAddress = resolvedUpdateTimeAddress;
            if (TryResolveGameMenuFromUpdateTime(memory, resolvedUpdateTimeAddress, out bool isGameMenu))
            {
                usingGameMenuFallback = false;
                TryResolveBossAddressesWithFallbacks(memory, resolvedUpdateTimeAddress);
                return new TerrariaMemoryResolveResult(
                    BuildResolutionStage(),
                    BuildResolutionStatusDetail(),
                    isGameMenu);
            }

            if (gameMenuAddress != IntPtr.Zero)
            {
                return new TerrariaMemoryResolveResult(
                    "menu state target unreadable",
                    "menu-state pointer became unreadable",
                    null);
            }

            return new TerrariaMemoryResolveResult(
                "menu state pointer unreadable",
                "found signature but not menu-state pointer",
                null);
        }

        IntPtr fallbackAnchorAddress = SignatureScanner.Scan(
            memory,
            profile.GameMenuFallbackSignature,
            profile.SignatureScanScopeLabel,
            out SignatureScanDiagnostics fallbackScanDiagnostics);
        LastSignatureScan = fallbackScanDiagnostics;
        if (fallbackAnchorAddress != IntPtr.Zero &&
            TryResolveGameMenuFromFallback(memory, fallbackAnchorAddress, out bool fallbackGameMenu))
        {
            updateTimeAddress = fallbackAnchorAddress;
            usingGameMenuFallback = true;
            TryResolveBossAddressesWithFallbacks(memory, null);
            return new TerrariaMemoryResolveResult(
                BuildResolutionStage(),
                BuildResolutionStatusDetail(),
                fallbackGameMenu);
        }

        if (gameMenuAddress != IntPtr.Zero)
        {
            return new TerrariaMemoryResolveResult(
                BuildResolutionStage(),
                BuildResolutionStatusDetail(),
                null);
        }

        updateTimeAddress = IntPtr.Zero;
        return new TerrariaMemoryResolveResult(
            "signature missing",
            "waiting for UpdateTime signature",
            null);
    }

    public bool TryReadGameMenuState(IProcessMemoryReader memory, out bool isGameMenu)
    {
        isGameMenu = false;

        if (gameMenuAddress == IntPtr.Zero)
        {
            return false;
        }

        if (!memory.TryReadBool(gameMenuAddress, out bool firstValue))
        {
            return false;
        }

        if (gameMenuSecondaryAddress == IntPtr.Zero)
        {
            isGameMenu = firstValue;
            return true;
        }

        if (!memory.TryReadBool(gameMenuSecondaryAddress, out bool secondValue))
        {
            return false;
        }

        isGameMenu = firstValue || secondValue;
        return true;
    }

    public TerrariaBossStates ReadBossStates(IProcessMemoryReader memory)
    {
        bool? wallOfFlesh = ReadHardmodeFlag(memory);
        if (!TryReadBossFlagBlock(memory, out byte[] bossFlags, out int minimumBossFlagOffset))
        {
            return new TerrariaBossStates(
                null,
                wallOfFlesh,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        return new TerrariaBossStates(
            ReadBossFlag(bossFlags, minimumBossFlagOffset, profile.SkeletronDefeatedFlagOffset),
            wallOfFlesh,
            ReadBossFlag(bossFlags, minimumBossFlagOffset, profile.DestroyerDefeatedFlagOffset),
            ReadBossFlag(bossFlags, minimumBossFlagOffset, profile.TwinsDefeatedFlagOffset),
            ReadBossFlag(bossFlags, minimumBossFlagOffset, profile.SkeletronPrimeDefeatedFlagOffset),
            ReadBossFlag(bossFlags, minimumBossFlagOffset, profile.PlanteraDefeatedFlagOffset),
            ReadBossFlag(bossFlags, minimumBossFlagOffset, profile.GolemDefeatedFlagOffset),
            ReadBossFlag(bossFlags, minimumBossFlagOffset, profile.LunaticCultistDefeatedFlagOffset),
            ReadBossFlag(bossFlags, minimumBossFlagOffset, profile.MoonLordDefeatedFlagOffset));
    }

    public string BuildResolutionStage()
    {
        if (HasResolvedBossAddresses)
        {
            if (usingGameMenuFallback && usingBossProgressionFallback)
            {
                return "ready via fallback";
            }

            if (usingGameMenuFallback)
            {
                return "ready via gameMenu fallback";
            }

            if (usingBossProgressionFallback)
            {
                return "ready via boss fallback";
            }

            return "ready";
        }

        return usingGameMenuFallback ? "timer ready via fallback" : "boss pointers pending";
    }

    public string BuildResolutionStatusDetail()
    {
        if (HasResolvedBossAddresses)
        {
            return BuildResolutionStage();
        }

        return usingGameMenuFallback
            ? "timer ready via fallback; boss scan pending"
            : "boss scan pending";
    }

    private bool TryResolveBossAddresses(IProcessMemoryReader memory, IntPtr resolvedUpdateTimeAddress)
    {
        IntPtr bossFlagsPointerLocation = IntPtr.Add(resolvedUpdateTimeAddress, profile.BossFlagsPointerOffset);
        if (!memory.TryReadPointer(bossFlagsPointerLocation, out IntPtr resolvedBossFlagsBaseAddress))
        {
            return false;
        }

        IntPtr hardmodePointerLocation = IntPtr.Add(resolvedUpdateTimeAddress, profile.HardmodePointerOffset);
        if (!memory.TryReadPointer(hardmodePointerLocation, out IntPtr resolvedHardmodeAddress))
        {
            return false;
        }

        if (!memory.TryReadBool(IntPtr.Add(resolvedBossFlagsBaseAddress, profile.SkeletronDefeatedFlagOffset), out _))
        {
            return false;
        }

        if (!memory.TryReadBool(resolvedHardmodeAddress, out _))
        {
            return false;
        }

        bossFlagsBaseAddress = resolvedBossFlagsBaseAddress;
        hardmodeAddress = resolvedHardmodeAddress;
        return true;
    }

    private bool TryResolveBossAddressesWithFallbacks(IProcessMemoryReader memory, IntPtr? resolvedUpdateTimeAddress)
    {
        if (resolvedUpdateTimeAddress.HasValue &&
            TryResolveBossAddresses(memory, resolvedUpdateTimeAddress.Value))
        {
            usingBossProgressionFallback = false;
            return true;
        }

        IntPtr fallbackAnchorAddress = SignatureScanner.Scan(
            memory,
            profile.BossProgressionFallbackSignature,
            profile.SignatureScanScopeLabel,
            out SignatureScanDiagnostics fallbackScanDiagnostics);
        LastSignatureScan = fallbackScanDiagnostics;
        if (fallbackAnchorAddress != IntPtr.Zero &&
            TryResolveBossAddressesFromProgressionFallback(memory, fallbackAnchorAddress))
        {
            usingBossProgressionFallback = true;
            return true;
        }

        bossFlagsBaseAddress = IntPtr.Zero;
        hardmodeAddress = IntPtr.Zero;
        usingBossProgressionFallback = false;
        return false;
    }

    private bool TryResolveBossAddressesFromProgressionFallback(IProcessMemoryReader memory, IntPtr fallbackAnchorAddress)
    {
        IntPtr skeletronInlineAddressLocation = IntPtr.Add(
            fallbackAnchorAddress,
            profile.BossProgressionFallbackSkeletronInlineAddressOffset);
        if (!memory.TryReadPointer(skeletronInlineAddressLocation, out IntPtr resolvedSkeletronAddress))
        {
            return false;
        }

        IntPtr hardmodeInlineAddressLocation = IntPtr.Add(
            fallbackAnchorAddress,
            profile.BossProgressionFallbackHardmodeInlineAddressOffset);
        if (!memory.TryReadPointer(hardmodeInlineAddressLocation, out IntPtr resolvedHardmodeAddress))
        {
            return false;
        }

        IntPtr resolvedBossFlagsBaseAddress = IntPtr.Add(
            resolvedSkeletronAddress,
            -profile.SkeletronDefeatedFlagOffset);
        if (!memory.TryReadBool(resolvedSkeletronAddress, out _))
        {
            return false;
        }

        if (!memory.TryReadBool(resolvedHardmodeAddress, out _))
        {
            return false;
        }

        if (!memory.TryReadBool(IntPtr.Add(resolvedBossFlagsBaseAddress, profile.MoonLordDefeatedFlagOffset), out _))
        {
            return false;
        }

        bossFlagsBaseAddress = resolvedBossFlagsBaseAddress;
        hardmodeAddress = resolvedHardmodeAddress;
        return true;
    }

    private bool TryResolveGameMenuFromUpdateTime(
        IProcessMemoryReader memory,
        IntPtr resolvedUpdateTimeAddress,
        out bool isGameMenu)
    {
        isGameMenu = false;

        IntPtr pointerLocation = IntPtr.Add(resolvedUpdateTimeAddress, profile.GameMenuPointerOffset);
        if (!memory.TryReadPointer(pointerLocation, out IntPtr resolvedGameMenuAddress))
        {
            return false;
        }

        if (!memory.TryReadBool(resolvedGameMenuAddress, out isGameMenu))
        {
            return false;
        }

        gameMenuAddress = resolvedGameMenuAddress;
        gameMenuSecondaryAddress = IntPtr.Zero;
        return true;
    }

    private bool TryResolveGameMenuFromFallback(
        IProcessMemoryReader memory,
        IntPtr fallbackAnchorAddress,
        out bool isGameMenu)
    {
        isGameMenu = false;

        IntPtr gameMenuInlineAddressLocation = IntPtr.Add(
            fallbackAnchorAddress,
            profile.GameMenuFallbackGameMenuInlineAddressOffset);
        if (!memory.TryReadPointer(gameMenuInlineAddressLocation, out IntPtr resolvedGameMenuAddress))
        {
            return false;
        }

        if (!memory.TryReadBool(resolvedGameMenuAddress, out isGameMenu))
        {
            return false;
        }

        gameMenuAddress = resolvedGameMenuAddress;
        gameMenuSecondaryAddress = IntPtr.Zero;
        return true;
    }

    private bool TryReadBossFlagBlock(
        IProcessMemoryReader memory,
        out byte[] bytes,
        out int minimumOffset)
    {
        bytes = null!;
        minimumOffset = 0;

        if (bossFlagsBaseAddress == IntPtr.Zero)
        {
            return false;
        }

        int[] offsets =
        [
            profile.SkeletronDefeatedFlagOffset,
            profile.DestroyerDefeatedFlagOffset,
            profile.TwinsDefeatedFlagOffset,
            profile.SkeletronPrimeDefeatedFlagOffset,
            profile.PlanteraDefeatedFlagOffset,
            profile.GolemDefeatedFlagOffset,
            profile.LunaticCultistDefeatedFlagOffset,
            profile.MoonLordDefeatedFlagOffset
        ];
        minimumOffset = offsets.Min();
        int maximumOffset = offsets.Max();
        int length = maximumOffset - minimumOffset + 1;

        if (memory.TryReadBytes(IntPtr.Add(bossFlagsBaseAddress, minimumOffset), length, out byte[]? readBytes))
        {
            bytes = readBytes;
            return true;
        }

        bossFlagsBaseAddress = IntPtr.Zero;
        return false;
    }

    private static bool? ReadBossFlag(byte[] bytes, int minimumOffset, int offset)
    {
        int index = offset - minimumOffset;
        return index >= 0 && index < bytes.Length
            ? bytes[index] != 0
            : null;
    }

    private bool? ReadHardmodeFlag(IProcessMemoryReader memory)
    {
        if (hardmodeAddress == IntPtr.Zero)
        {
            return null;
        }

        if (memory.TryReadBool(hardmodeAddress, out bool value))
        {
            return value;
        }

        hardmodeAddress = IntPtr.Zero;
        return null;
    }
}
