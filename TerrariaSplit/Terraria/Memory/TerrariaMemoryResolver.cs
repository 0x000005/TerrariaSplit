using System.Globalization;

namespace TerrariaSplit;

internal sealed class TerrariaMemoryResolver
{
    private const int X86GenerationProgressMessageFieldOffset = 0x24;
    private const int X86GenerationProgressValueFieldOffset = 0x4;
    private const int X86GenerationProgressTotalWeightedProgressFieldOffset = 0xC;
    private const int X86GenerationProgressTotalWeightFieldOffset = 0x14;
    private const int X86GenerationProgressCurrentPassWeightFieldOffset = 0x1C;
    private const int X86ControllerGeneratorFieldOffset = 16;
    private const int X86WorldGeneratorCurrentPassFieldOffset = 0x18;
    private const int X86GenPassNameFieldOffset = 0xC;

    private readonly TerrariaMemoryProfile profile;
    private IntPtr updateTimeAddress;
    private IntPtr gameMenuAddress;
    private IntPtr gameMenuSecondaryAddress;
    private IntPtr bossFlagsBaseAddress;
    private IntPtr hardmodeAddress;
    private IntPtr currentGenerationProgressAddress;
    private IntPtr currentControllerAddress;
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
        currentGenerationProgressAddress,
        currentControllerAddress,
        usingGameMenuFallback,
        usingBossProgressionFallback);

    public bool HasGameMenuAddress => gameMenuAddress != IntPtr.Zero;

    public bool HasResolvedBossAddresses => bossFlagsBaseAddress != IntPtr.Zero && hardmodeAddress != IntPtr.Zero;

    public bool HasResolvedWorldGenerationAddresses =>
        currentGenerationProgressAddress != IntPtr.Zero &&
        currentControllerAddress != IntPtr.Zero;

    public void Reset()
    {
        updateTimeAddress = IntPtr.Zero;
        gameMenuAddress = IntPtr.Zero;
        gameMenuSecondaryAddress = IntPtr.Zero;
        bossFlagsBaseAddress = IntPtr.Zero;
        hardmodeAddress = IntPtr.Zero;
        currentGenerationProgressAddress = IntPtr.Zero;
        currentControllerAddress = IntPtr.Zero;
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
        currentGenerationProgressAddress = IntPtr.Zero;
        currentControllerAddress = IntPtr.Zero;
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
                TryResolveWorldGenerationAddresses(memory);
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
            TryResolveWorldGenerationAddresses(memory);
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

    public TerrariaWorldGenerationState ReadWorldGenerationState(IProcessMemoryReader memory)
    {
        if (memory.Is64Bit)
        {
            return new TerrariaWorldGenerationState(
                null,
                null,
                null,
                null);
        }

        string? currentPassName = null;
        string? progressMessage = null;
        double? currentProgress = null;
        double? totalProgress = null;

        if (TryReadObjectSlot(memory, ref currentGenerationProgressAddress, out IntPtr progressObjectAddress) &&
            progressObjectAddress != IntPtr.Zero)
        {
            TryReadGenerationProgress(memory, progressObjectAddress, out progressMessage, out currentProgress, out totalProgress);
        }

        if (TryReadObjectSlot(memory, ref currentControllerAddress, out IntPtr controllerObjectAddress) &&
            controllerObjectAddress != IntPtr.Zero)
        {
            currentPassName = ReadCurrentPassName(memory, controllerObjectAddress);
        }

        return new TerrariaWorldGenerationState(
            currentPassName,
            progressMessage,
            currentProgress,
            totalProgress);
    }

    public string BuildResolutionStage()
    {
        if (HasResolvedBossAddresses && HasResolvedWorldGenerationAddresses)
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

        if (HasResolvedBossAddresses)
        {
            return usingGameMenuFallback
                ? "world generation pointers pending via fallback"
                : "world generation pointers pending";
        }

        return usingGameMenuFallback ? "timer ready via fallback" : "boss pointers pending";
    }

    public string BuildResolutionStatusDetail()
    {
        if (HasResolvedBossAddresses && HasResolvedWorldGenerationAddresses)
        {
            return BuildResolutionStage();
        }

        if (HasResolvedBossAddresses)
        {
            return usingGameMenuFallback
                ? "timer and boss pointers ready via fallback; world generation scan pending"
                : "timer and boss pointers ready; world generation scan pending";
        }

        return usingGameMenuFallback
            ? "timer ready via fallback; boss scan pending"
            : "boss scan pending";
    }

    private void TryResolveWorldGenerationAddresses(IProcessMemoryReader memory)
    {
        if (currentControllerAddress == IntPtr.Zero)
        {
            TryResolveInlineAddress(memory, profile.CurrentControllerSignature, profile.CurrentControllerInlineAddressOffset, static (reader, address) =>
                reader.TryReadPointerValue(address, out _), out currentControllerAddress);
        }

        if (currentGenerationProgressAddress == IntPtr.Zero)
        {
            TryResolveInlineAddress(memory, profile.CurrentGenerationProgressSignature, profile.CurrentGenerationProgressInlineAddressOffset, static (reader, address) =>
                reader.TryReadPointerValue(address, out _), out currentGenerationProgressAddress);
        }

        int slotSize = memory.Is64Bit ? 8 : 4;
        if (currentControllerAddress == IntPtr.Zero &&
            currentGenerationProgressAddress != IntPtr.Zero)
        {
            IntPtr adjacentControllerAddress = IntPtr.Add(currentGenerationProgressAddress, slotSize);
            if (memory.TryReadPointerValue(adjacentControllerAddress, out _))
            {
                currentControllerAddress = adjacentControllerAddress;
            }
        }

        if (currentGenerationProgressAddress == IntPtr.Zero &&
            currentControllerAddress != IntPtr.Zero)
        {
            IntPtr adjacentProgressAddress = IntPtr.Add(currentControllerAddress, -slotSize);
            if (memory.TryReadPointerValue(adjacentProgressAddress, out _))
            {
                currentGenerationProgressAddress = adjacentProgressAddress;
            }
        }
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

    private static bool TryReadObjectSlot(IProcessMemoryReader memory, ref IntPtr slotAddress, out IntPtr objectAddress)
    {
        objectAddress = IntPtr.Zero;
        if (slotAddress == IntPtr.Zero)
        {
            return false;
        }

        if (memory.TryReadPointerValue(slotAddress, out objectAddress))
        {
            return true;
        }

        slotAddress = IntPtr.Zero;
        return false;
    }

    private static bool TryReadGenerationProgress(
        IProcessMemoryReader memory,
        IntPtr progressObjectAddress,
        out string? progressMessage,
        out double? currentProgress,
        out double? totalProgress)
    {
        progressMessage = null;
        currentProgress = null;
        totalProgress = null;

        IntPtr messageFieldAddress = IntPtr.Add(progressObjectAddress, X86GenerationProgressMessageFieldOffset);
        if (memory.TryReadPointerValue(messageFieldAddress, out IntPtr messageObjectAddress) &&
            messageObjectAddress != IntPtr.Zero &&
            ManagedObjectMemoryReader.TryReadManagedString(memory, messageObjectAddress, out string? messageTemplate) &&
            !string.IsNullOrWhiteSpace(messageTemplate))
        {
            progressMessage = messageTemplate;
        }

        if (memory.TryReadDouble(IntPtr.Add(progressObjectAddress, X86GenerationProgressValueFieldOffset), out double value))
        {
            currentProgress = value;
        }

        if (memory.TryReadDouble(IntPtr.Add(progressObjectAddress, X86GenerationProgressTotalWeightedProgressFieldOffset), out double totalWeightedProgress) &&
            memory.TryReadDouble(IntPtr.Add(progressObjectAddress, X86GenerationProgressTotalWeightFieldOffset), out double totalWeight) &&
            memory.TryReadDouble(IntPtr.Add(progressObjectAddress, X86GenerationProgressCurrentPassWeightFieldOffset), out double currentPassWeight) &&
            currentProgress.HasValue &&
            totalWeight != 0d)
        {
            totalProgress = (currentProgress.Value * currentPassWeight + totalWeightedProgress) / totalWeight;
        }

        if (!string.IsNullOrWhiteSpace(progressMessage) && currentProgress.HasValue)
        {
            try
            {
                progressMessage = string.Format(CultureInfo.InvariantCulture, progressMessage, currentProgress.Value);
            }
            catch (FormatException)
            {
                // Leave the raw template when Terraria uses an unexpected format string.
            }
        }

        if (currentProgress.HasValue &&
            (!double.IsFinite(currentProgress.Value) || currentProgress.Value < -0.001d || currentProgress.Value > 1.001d))
        {
            currentProgress = null;
        }

        if (totalProgress.HasValue &&
            (!double.IsFinite(totalProgress.Value) || totalProgress.Value < -0.001d || totalProgress.Value > 1.001d))
        {
            totalProgress = null;
        }

        if (string.IsNullOrWhiteSpace(progressMessage))
        {
            currentProgress = null;
            totalProgress = null;
            return false;
        }

        return true;
    }

    private static string? ReadCurrentPassName(IProcessMemoryReader memory, IntPtr controllerObjectAddress)
    {
        if (!memory.TryReadPointerValue(IntPtr.Add(controllerObjectAddress, X86ControllerGeneratorFieldOffset), out IntPtr worldGeneratorObjectAddress) ||
            worldGeneratorObjectAddress == IntPtr.Zero)
        {
            return null;
        }

        if (!memory.TryReadPointerValue(IntPtr.Add(worldGeneratorObjectAddress, X86WorldGeneratorCurrentPassFieldOffset), out IntPtr currentPassObjectAddress) ||
            currentPassObjectAddress == IntPtr.Zero)
        {
            return null;
        }

        if (!memory.TryReadPointerValue(IntPtr.Add(currentPassObjectAddress, X86GenPassNameFieldOffset), out IntPtr nameObjectAddress) ||
            nameObjectAddress == IntPtr.Zero)
        {
            return null;
        }

        return ManagedObjectMemoryReader.TryReadManagedString(memory, nameObjectAddress, out string? currentPassName)
            ? currentPassName
            : null;
    }

    private bool TryResolveInlineAddress(
        IProcessMemoryReader memory,
        SignaturePattern signature,
        int inlineAddressOffset,
        Func<IProcessMemoryReader, IntPtr, bool> validateAddress,
        out IntPtr resolvedAddress)
    {
        resolvedAddress = IntPtr.Zero;

        IntPtr anchorAddress = SignatureScanner.Scan(
            memory,
            signature,
            profile.SignatureScanScopeLabel,
            out SignatureScanDiagnostics scanDiagnostics);
        LastSignatureScan = scanDiagnostics;
        if (anchorAddress == IntPtr.Zero)
        {
            return false;
        }

        IntPtr inlineAddressLocation = IntPtr.Add(anchorAddress, inlineAddressOffset);
        if (!memory.TryReadPointer(inlineAddressLocation, out IntPtr candidateAddress))
        {
            return false;
        }

        if (!validateAddress(memory, candidateAddress))
        {
            return false;
        }

        resolvedAddress = candidateAddress;
        return true;
    }
}
