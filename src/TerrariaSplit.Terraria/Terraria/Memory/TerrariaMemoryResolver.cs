using System.Diagnostics;
using System.Globalization;
using Process = System.Diagnostics.Process;

namespace TerrariaSplit.Terraria.Memory;

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
    private readonly BossFlagAddressResolver bossFlagAddressResolver = new();
    private readonly TerrariaClrMemoryResolver clrMemoryResolver = new();
    private readonly TerrariaGameFactReader factReader = new();
    private IntPtr updateTimeAddress;
    private IntPtr gameMenuAddress;
    private IntPtr gameMenuSecondaryAddress;
    private IntPtr bossFlagsBaseAddress;
    private IntPtr hardmodeAddress;
    private IntPtr currentGenerationProgressAddress;
    private IntPtr currentControllerAddress;
    private bool usingBossProgressionMenuFallback;
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
        usingBossProgressionMenuFallback,
        usingGameMenuFallback,
        usingBossProgressionFallback);

    public bool HasGameMenuAddress => gameMenuAddress != IntPtr.Zero;

    public bool HasResolvedBossAddresses => bossFlagsBaseAddress != IntPtr.Zero && hardmodeAddress != IntPtr.Zero;

    public bool HasResolvedWorldGenerationAddresses =>
        currentGenerationProgressAddress != IntPtr.Zero &&
        currentControllerAddress != IntPtr.Zero;

    public void SetProcess(Process? process)
    {
        clrMemoryResolver.SetProcess(process);
    }

    public void Reset()
    {
        clrMemoryResolver.Reset();
        updateTimeAddress = IntPtr.Zero;
        gameMenuAddress = IntPtr.Zero;
        gameMenuSecondaryAddress = IntPtr.Zero;
        bossFlagsBaseAddress = IntPtr.Zero;
        hardmodeAddress = IntPtr.Zero;
        currentGenerationProgressAddress = IntPtr.Zero;
        currentControllerAddress = IntPtr.Zero;
        usingBossProgressionMenuFallback = false;
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
        usingBossProgressionMenuFallback = false;
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
                usingBossProgressionMenuFallback = false;
                usingGameMenuFallback = false;
                TryResolveBossAddressesWithFallbacks(memory, resolvedUpdateTimeAddress);
                TryResolveWorldGenerationAddresses(memory);
                return new TerrariaMemoryResolveResult(
                    BuildResolutionStage(),
                    BuildResolutionStatusDetail(),
                    isGameMenu);
            }
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
            usingBossProgressionMenuFallback = false;
            usingGameMenuFallback = true;
            TryResolveBossAddressesWithFallbacks(memory, null);
            TryResolveWorldGenerationAddresses(memory);
            return new TerrariaMemoryResolveResult(
                BuildResolutionStage(),
                BuildResolutionStatusDetail(),
                fallbackGameMenu);
        }

        if (TryResolveGameMenuFromBossProgressionFallback(memory, out bool bossProgressionGameMenu))
        {
            usingBossProgressionMenuFallback = true;
            usingGameMenuFallback = false;
            usingBossProgressionFallback = true;
            TryResolveWorldGenerationAddresses(memory);
            return new TerrariaMemoryResolveResult(
                BuildResolutionStage(),
                BuildResolutionStatusDetail(),
                bossProgressionGameMenu);
        }

        if (gameMenuAddress != IntPtr.Zero)
        {
            return new TerrariaMemoryResolveResult(
                BuildResolutionStage(),
                BuildResolutionStatusDetail(),
                null);
        }

        updateTimeAddress = IntPtr.Zero;
        if (resolvedUpdateTimeAddress != IntPtr.Zero)
        {
            return new TerrariaMemoryResolveResult(
                "menu state pointer unreadable",
                "found UpdateTime signature but neither primary nor fallback menu-state route resolved",
                null);
        }

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

    public TerrariaGameFacts ReadGameFacts(
        IProcessMemoryReader memory,
        IReadOnlyCollection<string>? observedFactKeys = null)
    {
        TerrariaFactReadPlan readPlan = TerrariaFactReadPlan.FromObservedFactKeys(observedFactKeys);
        TerrariaMemoryContext context = CreateContext(memory, readPlan);
        TerrariaGameFacts facts = factReader.Read(memory, context, readPlan);
        if (context.BossFlags is not null &&
            readPlan.ReadsBossFacts &&
            !facts.Values.Any(value => value.Key.StartsWith("boss:", StringComparison.OrdinalIgnoreCase) &&
                value.Value.Kind != FactValueKind.Unknown))
        {
            bossFlagsBaseAddress = IntPtr.Zero;
        }

        if (context.HardmodeAddress != IntPtr.Zero &&
            readPlan.IncludesBossFactKey(SplitCatalog.BossFacts.First(boss => boss.AddressKind == BossFactAddressKind.Hardmode).FactKey) &&
            facts.Get(SplitCatalog.BossFacts.First(boss => boss.AddressKind == BossFactAddressKind.Hardmode).FactKey).Kind == FactValueKind.Unknown)
        {
            hardmodeAddress = IntPtr.Zero;
        }

        return facts;
    }

    private TerrariaMemoryContext CreateContext(IProcessMemoryReader memory, TerrariaFactReadPlan readPlan)
    {
        TerrariaItemMemoryLayout? itemLayout = readPlan.ReadsItemFacts &&
            clrMemoryResolver.TryGetItemLayout(memory, out TerrariaItemMemoryLayout resolvedItemLayout)
            ? resolvedItemLayout
            : null;
        TerrariaNpcMemoryLayout? npcLayout = readPlan.ReadsNpcFacts &&
            clrMemoryResolver.TryGetNpcLayout(memory, out TerrariaNpcMemoryLayout resolvedNpcLayout)
            ? resolvedNpcLayout
            : null;
        TerrariaBiomeMemoryLayout? biomeLayout = readPlan.ReadsBiomeFacts &&
            clrMemoryResolver.TryGetBiomeLayout(memory, out TerrariaBiomeMemoryLayout resolvedBiomeLayout)
            ? resolvedBiomeLayout
            : null;
        IntPtr localPlayerAddress = ResolveLocalPlayerAddress(memory, itemLayout, biomeLayout);

        return new TerrariaMemoryContext(
            bossFlagsBaseAddress == IntPtr.Zero ? null : new BossFlagMemoryBlock(bossFlagsBaseAddress),
            hardmodeAddress,
            localPlayerAddress,
            itemLayout,
            npcLayout,
            biomeLayout,
            memory.Is64Bit);
    }

    private static IntPtr ResolveLocalPlayerAddress(
        IProcessMemoryReader memory,
        TerrariaItemMemoryLayout? itemLayout,
        TerrariaBiomeMemoryLayout? biomeLayout)
    {
        if (memory.Is64Bit)
        {
            return IntPtr.Zero;
        }

        TerrariaLocalPlayerMemoryLayout? layout = itemLayout is not null
            ? itemLayout
            : biomeLayout;
        return layout is not null &&
            TerrariaLocalPlayerResolver.TryResolve(memory, layout, out IntPtr localPlayerAddress)
            ? localPlayerAddress
            : IntPtr.Zero;
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

            if (usingBossProgressionMenuFallback)
            {
                return "ready via boss progression menu fallback";
            }

            if (usingBossProgressionFallback)
            {
                return "ready via boss fallback";
            }

            return "ready";
        }

        if (HasResolvedBossAddresses)
        {
            if (usingGameMenuFallback)
            {
                return "world generation pointers pending via fallback";
            }

            return usingBossProgressionMenuFallback
                ? "world generation pointers pending via boss progression menu fallback"
                : "world generation pointers pending";
        }

        if (usingGameMenuFallback)
        {
            return "timer ready via fallback";
        }

        return usingBossProgressionMenuFallback
            ? "timer ready via boss progression menu fallback"
            : "boss pointers pending";
    }

    public string BuildResolutionStatusDetail()
    {
        if (HasResolvedBossAddresses && HasResolvedWorldGenerationAddresses)
        {
            return BuildResolutionStage();
        }

        if (HasResolvedBossAddresses)
        {
            if (usingGameMenuFallback)
            {
                return "timer and boss pointers ready via fallback; world generation scan pending";
            }

            return usingBossProgressionMenuFallback
                ? "timer and boss pointers ready via boss progression menu fallback; world generation scan pending"
                : "timer and boss pointers ready; world generation scan pending";
        }

        if (usingGameMenuFallback)
        {
            return "timer ready via fallback; boss scan pending";
        }

        return usingBossProgressionMenuFallback
            ? "timer ready via boss progression menu fallback; boss scan pending"
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

    private bool TryResolveBossAddressesWithFallbacks(IProcessMemoryReader memory, IntPtr? resolvedUpdateTimeAddress)
    {
        if (bossFlagAddressResolver.TryResolve(
            memory,
            profile,
            resolvedUpdateTimeAddress,
            out BossFlagAddressResolution resolution,
            out SignatureScanDiagnostics? scanDiagnostics))
        {
            bossFlagsBaseAddress = resolution.BossFlags.BaseAddress;
            hardmodeAddress = resolution.HardmodeAddress;
            usingBossProgressionFallback = resolution.UsedProgressionFallback;
            if (scanDiagnostics is not null)
            {
                LastSignatureScan = scanDiagnostics;
            }

            return true;
        }

        bossFlagsBaseAddress = IntPtr.Zero;
        hardmodeAddress = IntPtr.Zero;
        usingBossProgressionFallback = false;
        return false;
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
        if (memory.TryReadPointer(gameMenuInlineAddressLocation, out IntPtr resolvedGameMenuAddress) &&
            memory.TryReadBool(resolvedGameMenuAddress, out isGameMenu))
        {
            gameMenuAddress = resolvedGameMenuAddress;
            gameMenuSecondaryAddress = IntPtr.Zero;
            return true;
        }

        return false;
    }

    private bool TryResolveGameMenuFromBossProgressionFallback(
        IProcessMemoryReader memory,
        out bool isGameMenu)
    {
        isGameMenu = false;
        if (!bossFlagAddressResolver.TryResolveFromProgressionFallback(
            memory,
            profile,
            out BossFlagAddressResolution resolution,
            out _,
            out SignatureScanDiagnostics? scanDiagnostics))
        {
            return false;
        }

        LastSignatureScan = scanDiagnostics;
        bossFlagsBaseAddress = resolution.BossFlags.BaseAddress;
        hardmodeAddress = resolution.HardmodeAddress;
        IntPtr resolvedGameMenuAddress = IntPtr.Add(
            hardmodeAddress,
            profile.BossProgressionFallbackGameMenuFromHardmodeOffset);
        if (!memory.TryReadBool(resolvedGameMenuAddress, out isGameMenu))
        {
            bossFlagsBaseAddress = IntPtr.Zero;
            hardmodeAddress = IntPtr.Zero;
            return false;
        }

        gameMenuAddress = resolvedGameMenuAddress;
        gameMenuSecondaryAddress = IntPtr.Zero;
        return true;
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
