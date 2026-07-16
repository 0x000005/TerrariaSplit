using System.Diagnostics;
using System.Globalization;
using Process = System.Diagnostics.Process;

namespace TerrariaSplit.Terraria.Memory;

internal sealed class TerrariaMemoryResolver
{
    private static readonly string HardmodeFactKey = SplitCatalog.BossFacts
        .First(boss => boss.AddressKind == BossFactAddressKind.Hardmode)
        .FactKey;

    private readonly TerrariaClrMemoryResolver clrMemoryResolver;
    private readonly TerrariaGameFactReader factReader = new();
    private TerrariaRuntimeMemoryLayout? runtimeLayout;

    public TerrariaMemoryResolver()
        : this(new TerrariaClrMemoryResolver())
    {
    }

    internal TerrariaMemoryResolver(TerrariaClrMemoryResolver clrMemoryResolver)
    {
        this.clrMemoryResolver = clrMemoryResolver;
    }

    public TerrariaLayoutProbeDiagnostics ProbeDiagnostics => clrMemoryResolver.ProbeDiagnostics;

    public string LayoutStatus => clrMemoryResolver.LayoutStatus;

    public TerrariaMemoryResolution Resolution => BuildResolution(runtimeLayout);

    public TerrariaWorldCreationSeedMemoryLayout? SeedUiLayout => runtimeLayout?.SeedUi;

    public bool HasGameMenuAddress => runtimeLayout?.Core.GameMenuStaticFieldAddress != IntPtr.Zero;

    public bool HasResolvedBossAddresses => runtimeLayout?.Boss.ResolvedFactCount > 0;

    public bool HasResolvedWorldGenerationAddresses => runtimeLayout?.WorldGeneration.HasAnySource == true;

    public void SetProcess(Process? process)
    {
        clrMemoryResolver.SetProcess(process);
    }

    public void Reset()
    {
        clrMemoryResolver.Reset();
        runtimeLayout = null;
    }

    public void ResetResolvedAddresses()
    {
        clrMemoryResolver.ResetLayout();
        runtimeLayout = null;
    }

    internal void SetRuntimeLayoutForTests(TerrariaRuntimeMemoryLayout layout)
    {
        runtimeLayout = layout;
    }

    public TerrariaMemoryResolveResult Resolve(IProcessMemoryReader memory)
    {
        if (!EnsureRuntimeLayout(memory, out TerrariaRuntimeMemoryLayout layout))
        {
            return new TerrariaMemoryResolveResult(
                "layout resolving",
                "waiting for MemoryBridge runtime layout",
                null);
        }

        if (layout.Core.GameMenuStaticFieldAddress == IntPtr.Zero)
        {
            return new TerrariaMemoryResolveResult(
                "core layout missing",
                "MemoryBridge did not resolve Terraria.Main.gameMenu",
                null);
        }

        if (TryReadGameMenuState(memory, out bool isGameMenu))
        {
            return new TerrariaMemoryResolveResult(
                BuildResolutionStage(),
                BuildResolutionStatusDetail(),
                isGameMenu);
        }

        return new TerrariaMemoryResolveResult(
            "menu state unreadable",
            "MemoryBridge resolved Terraria.Main.gameMenu, but the static field address is unreadable",
            null);
    }

    public bool TryReadGameMenuState(IProcessMemoryReader memory, out bool isGameMenu)
    {
        isGameMenu = false;
        IntPtr gameMenuAddress = runtimeLayout?.Core.GameMenuStaticFieldAddress ?? IntPtr.Zero;
        return gameMenuAddress != IntPtr.Zero &&
            memory.TryReadBool(gameMenuAddress, out isGameMenu);
    }

    public TerrariaGameFacts ReadGameFacts(
        IProcessMemoryReader memory,
        IReadOnlyCollection<string>? observedFactKeys = null)
    {
        return ReadGameFacts(memory, TerrariaFactReadPlan.FromObservedFactKeys(observedFactKeys));
    }

    internal TerrariaGameFacts ReadGameFacts(
        IProcessMemoryReader memory,
        TerrariaFactReadPlan readPlan)
    {
        TerrariaMemoryContext context = CreateContext(memory, readPlan);
        return factReader.Read(memory, context, readPlan);
    }

    private TerrariaMemoryContext CreateContext(IProcessMemoryReader memory, TerrariaFactReadPlan readPlan)
    {
        _ = readPlan;
        _ = EnsureRuntimeLayout(memory, out TerrariaRuntimeMemoryLayout layout);
        TerrariaItemMemoryLayout? itemLayout = readPlan.ReadsItemFacts
            ? layout.Item
            : null;
        TerrariaNpcMemoryLayout? npcLayout = readPlan.ReadsNpcFacts
            ? layout.Npc
            : null;
        TerrariaBiomeMemoryLayout? biomeLayout = readPlan.ReadsBiomeFacts
            ? layout.Biome
            : null;
        IntPtr localPlayerAddress = ResolveLocalPlayerAddress(memory, itemLayout, biomeLayout);

        return new TerrariaMemoryContext(
            readPlan.ReadsBossFacts ? layout.Boss : null,
            localPlayerAddress,
            itemLayout,
            npcLayout,
            biomeLayout,
            memory.Is64Bit);
    }

    private bool EnsureRuntimeLayout(IProcessMemoryReader memory, out TerrariaRuntimeMemoryLayout layout)
    {
        if (runtimeLayout is not null)
        {
            layout = runtimeLayout;
            return true;
        }

        if (clrMemoryResolver.TryGetRuntimeLayout(memory, out layout))
        {
            runtimeLayout = layout;
            return true;
        }

        layout = EmptyRuntimeLayout();
        return false;
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
        if (memory.Is64Bit ||
            !EnsureRuntimeLayout(memory, out TerrariaRuntimeMemoryLayout layout))
        {
            return TerrariaWorldGenerationState.Unknown;
        }

        TerrariaWorldGenerationMemoryLayout worldGeneration = layout.WorldGeneration;
        string? currentPassName = null;
        string? progressMessage = null;
        double? currentProgress = null;
        double? totalProgress = null;
        bool progressSlotRead = false;
        bool controllerSlotRead = false;
        IntPtr progressObjectAddress = IntPtr.Zero;
        IntPtr controllerObjectAddress = IntPtr.Zero;

        if (worldGeneration.HasStructuredProgress)
        {
            progressSlotRead = TryReadObjectSlot(
                memory,
                worldGeneration.CurrentGenerationProgressStaticFieldAddress,
                out progressObjectAddress);
            if (progressSlotRead && progressObjectAddress != IntPtr.Zero)
            {
                TryReadGenerationProgress(
                    memory,
                    worldGeneration,
                    progressObjectAddress,
                    out progressMessage,
                    out currentProgress,
                    out totalProgress);
            }
        }

        if (worldGeneration.HasStructuredController)
        {
            controllerSlotRead = TryReadObjectSlot(
                memory,
                worldGeneration.CurrentControllerStaticFieldAddress,
                out controllerObjectAddress);
            if (controllerSlotRead && controllerObjectAddress != IntPtr.Zero)
            {
                currentPassName = ReadCurrentPassName(memory, worldGeneration, controllerObjectAddress);
            }
        }

        var structuredState = new TerrariaWorldGenerationState(
            currentPassName,
            progressMessage,
            currentProgress,
            totalProgress);
        if (structuredState.HasAnyData)
        {
            return structuredState;
        }

        bool structuredGenerationEnded =
            (worldGeneration.HasStructuredProgress || worldGeneration.HasStructuredController) &&
            (!worldGeneration.HasStructuredProgress || (progressSlotRead && progressObjectAddress == IntPtr.Zero)) &&
            (!worldGeneration.HasStructuredController || (controllerSlotRead && controllerObjectAddress == IntPtr.Zero));
        if (structuredGenerationEnded)
        {
            // Terraria clears both structured world-generation slots when generation
            // finishes, but Main.statusText retains the final progress message. Do not
            // mistake that stale fallback text for an active generation.
            return TerrariaWorldGenerationState.Unknown;
        }

        return TryReadStatusTextFallback(memory, worldGeneration, out TerrariaWorldGenerationState statusTextState)
            ? statusTextState
            : TerrariaWorldGenerationState.Unknown;
    }

    public string BuildResolutionStage()
    {
        if (runtimeLayout is null)
        {
            return "layout resolving";
        }

        if (!runtimeLayout.HasCore)
        {
            return "core layout missing";
        }

        bool bossReady = runtimeLayout.Boss.ResolvedFactCount > 0;
        bool worldGenerationReady = runtimeLayout.WorldGeneration.HasAnySource;
        return (bossReady, worldGenerationReady) switch
        {
            (true, true) => "ready",
            (true, false) => "world generation layout pending",
            (false, true) => "boss layout pending",
            _ => "fact layouts pending"
        };
    }

    public string BuildResolutionStatusDetail()
    {
        if (runtimeLayout is null)
        {
            return $"MemoryBridge layout {clrMemoryResolver.LayoutStatus}";
        }

        if (!runtimeLayout.HasCore)
        {
            return "MemoryBridge returned a layout without Terraria.Main.gameMenu";
        }

        bool bossReady = runtimeLayout.Boss.ResolvedFactCount > 0;
        bool worldGenerationReady = runtimeLayout.WorldGeneration.HasAnySource;
        return (bossReady, worldGenerationReady) switch
        {
            (true, true) => "runtime layout ready",
            (true, false) => "timer and boss layouts ready; world generation layout unavailable",
            (false, true) => "timer and world generation layouts ready; boss layout unavailable",
            _ => "timer layout ready; fact layouts unavailable"
        };
    }

    private static TerrariaMemoryResolution BuildResolution(TerrariaRuntimeMemoryLayout? layout)
    {
        if (layout is null)
        {
            return default;
        }

        layout.Boss.TryGetFactAddress(HardmodeFactKey, out IntPtr hardmodeAddress);
        return new TerrariaMemoryResolution(
            layout.Core.GameMenuStaticFieldAddress,
            layout.Core.StatusTextStaticFieldAddress,
            layout.Core.MenuUiStaticFieldAddress,
            layout.Boss.ResolvedFactCount,
            hardmodeAddress,
            layout.WorldGeneration.CurrentGenerationProgressStaticFieldAddress,
            layout.WorldGeneration.CurrentControllerStaticFieldAddress,
            layout.SeedUi is not null);
    }

    private static TerrariaRuntimeMemoryLayout EmptyRuntimeLayout()
    {
        return new TerrariaRuntimeMemoryLayout(
            null,
            new TerrariaCoreMemoryLayout(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero),
            new TerrariaBossMemoryLayout(new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase)),
            null,
            null,
            null,
            null,
            new TerrariaWorldGenerationMemoryLayout(
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero,
                -1,
                -1,
                -1,
                -1,
                -1,
                -1,
                -1,
                -1),
            0);
    }

    private static bool TryReadObjectSlot(
        IProcessMemoryReader memory,
        IntPtr slotAddress,
        out IntPtr objectAddress)
    {
        objectAddress = IntPtr.Zero;
        return slotAddress != IntPtr.Zero &&
            memory.TryReadPointerValue(slotAddress, out objectAddress);
    }

    private static bool TryReadGenerationProgress(
        IProcessMemoryReader memory,
        TerrariaWorldGenerationMemoryLayout layout,
        IntPtr progressObjectAddress,
        out string? progressMessage,
        out double? currentProgress,
        out double? totalProgress)
    {
        progressMessage = null;
        currentProgress = null;
        totalProgress = null;

        IntPtr messageFieldAddress = IntPtr.Add(progressObjectAddress, layout.GenerationProgressMessageFieldOffset);
        if (memory.TryReadPointerValue(messageFieldAddress, out IntPtr messageObjectAddress) &&
            messageObjectAddress != IntPtr.Zero &&
            ManagedObjectMemoryReader.TryReadManagedString(memory, messageObjectAddress, out string? messageTemplate) &&
            !string.IsNullOrWhiteSpace(messageTemplate))
        {
            progressMessage = messageTemplate;
        }

        if (memory.TryReadDouble(IntPtr.Add(progressObjectAddress, layout.GenerationProgressValueFieldOffset), out double value))
        {
            currentProgress = value;
        }

        if (memory.TryReadDouble(IntPtr.Add(progressObjectAddress, layout.GenerationProgressTotalWeightedProgressFieldOffset), out double totalWeightedProgress) &&
            memory.TryReadDouble(IntPtr.Add(progressObjectAddress, layout.GenerationProgressTotalWeightFieldOffset), out double totalWeight) &&
            memory.TryReadDouble(IntPtr.Add(progressObjectAddress, layout.GenerationProgressCurrentPassWeightFieldOffset), out double currentPassWeight) &&
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
                // Leave Terraria's raw message template when it uses an unexpected format string.
            }
        }

        currentProgress = SanitizeProgress(currentProgress);
        totalProgress = SanitizeProgress(totalProgress);
        if (string.IsNullOrWhiteSpace(progressMessage))
        {
            currentProgress = null;
            totalProgress = null;
            return false;
        }

        return true;
    }

    private static string? ReadCurrentPassName(
        IProcessMemoryReader memory,
        TerrariaWorldGenerationMemoryLayout layout,
        IntPtr controllerObjectAddress)
    {
        if (!memory.TryReadPointerValue(IntPtr.Add(controllerObjectAddress, layout.ControllerGeneratorFieldOffset), out IntPtr worldGeneratorObjectAddress) ||
            worldGeneratorObjectAddress == IntPtr.Zero ||
            !memory.TryReadPointerValue(IntPtr.Add(worldGeneratorObjectAddress, layout.WorldGeneratorCurrentPassFieldOffset), out IntPtr currentPassObjectAddress) ||
            currentPassObjectAddress == IntPtr.Zero ||
            !memory.TryReadPointerValue(IntPtr.Add(currentPassObjectAddress, layout.GenPassNameFieldOffset), out IntPtr nameObjectAddress) ||
            nameObjectAddress == IntPtr.Zero)
        {
            return null;
        }

        return ManagedObjectMemoryReader.TryReadManagedString(memory, nameObjectAddress, out string? currentPassName)
            ? currentPassName
            : null;
    }

    private static bool TryReadStatusTextFallback(
        IProcessMemoryReader memory,
        TerrariaWorldGenerationMemoryLayout layout,
        out TerrariaWorldGenerationState state)
    {
        state = TerrariaWorldGenerationState.Unknown;
        if (!layout.HasStatusTextFallback ||
            !memory.TryReadPointerValue(layout.StatusTextStaticFieldAddress, out IntPtr statusTextObjectAddress) ||
            statusTextObjectAddress == IntPtr.Zero ||
            !ManagedObjectMemoryReader.TryReadManagedString(memory, statusTextObjectAddress, out string? statusText) ||
            string.IsNullOrWhiteSpace(statusText))
        {
            return false;
        }

        state = ParseStatusText(statusText.Trim());
        return state.HasAnyData;
    }

    private static TerrariaWorldGenerationState ParseStatusText(string statusText)
    {
        string[] parts = statusText.Split(" - ", StringSplitOptions.None);
        if (parts.Length >= 3 &&
            TryParsePercent(parts[0], out double totalProgress) &&
            TryParsePercent(parts[^1], out double currentProgress))
        {
            string message = string.Join(" - ", parts.Skip(1).Take(parts.Length - 2));
            return new TerrariaWorldGenerationState(
                null,
                string.IsNullOrWhiteSpace(message) ? statusText : message,
                currentProgress,
                totalProgress);
        }

        return new TerrariaWorldGenerationState(null, statusText, null, null);
    }

    private static bool TryParsePercent(string value, out double progress)
    {
        progress = 0d;
        string trimmed = value.Trim();
        if (!trimmed.EndsWith("%", StringComparison.Ordinal))
        {
            return false;
        }

        trimmed = trimmed[..^1].Trim();
        if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
        {
            return false;
        }

        progress = percent / 100d;
        return double.IsFinite(progress);
    }

    private static double? SanitizeProgress(double? value)
    {
        if (!value.HasValue ||
            !double.IsFinite(value.Value) ||
            value.Value < -0.001d ||
            value.Value > 1.001d)
        {
            return null;
        }

        return value;
    }
}
