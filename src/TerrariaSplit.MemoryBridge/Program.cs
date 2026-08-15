using Microsoft.Diagnostics.Runtime;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using TerrariaSplit.MemoryBridge.Protocol;

namespace TerrariaSplit.MemoryBridge;

internal static class Program
{
    private const int X86ArrayLengthOffset = 0x4;
    private const int X86ArrayFirstElementFallbackOffset = 0x8;
    private const int X86InstanceFieldOffsetBias = 0x4;
    private const int X86ObjectReferenceSize = 4;
    private const int UiStateNestedReferenceScanStart = 0x8;
    private const int UiStateNestedReferenceScanEnd = 0x300;
    private static readonly string[] ZoneBitsByteFieldNames = ["zone1", "zone2", "zone3", "zone4", "zone5"];
    private static readonly IReadOnlyDictionary<string, string> BossNpcFieldByFactKey =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["boss:king-slime:defeated"] = "downedSlimeKing",
            ["boss:eye-of-cthulhu:defeated"] = "downedBoss1",
            ["boss:eater-of-worlds:defeated"] = "downedBoss2",
            ["boss:brain-of-cthulhu:defeated"] = "downedBoss2",
            ["boss:queen-bee:defeated"] = "downedQueenBee",
            ["boss:skeletron:defeated"] = "downedBoss3",
            ["boss:deerclops:defeated"] = "downedDeerclops",
            ["boss:queen-slime:defeated"] = "downedQueenSlime",
            ["boss:destroyer:defeated"] = "downedMechBoss1",
            ["boss:twins:defeated"] = "downedMechBoss2",
            ["boss:skeletron-prime:defeated"] = "downedMechBoss3",
            ["boss:plantera:defeated"] = "downedPlantBoss",
            ["boss:golem:defeated"] = "downedGolemBoss",
            ["boss:duke-fishron:defeated"] = "downedFishron",
            ["boss:empress-of-light:defeated"] = "downedEmpressOfLight",
            ["boss:lunatic-cultist:defeated"] = "downedAncientCultist",
            ["boss:moon-lord:defeated"] = "downedMoonlord"
        };

    private static int Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], MemoryBridgeCommands.Inject, StringComparison.OrdinalIgnoreCase))
        {
            return InjectorCommand.Run(args[1..]);
        }

        if (args.Length > 0 &&
            string.Equals(args[0], MemoryBridgeCommands.RandomSeedBatch, StringComparison.OrdinalIgnoreCase))
        {
            return RunRandomSeedBatch(args);
        }

        if (args.Length != 2 ||
            !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int processId))
        {
            WriteResponse(new RuntimeLayoutResponse(false, "usage: runtime-layout <pid>", null));
            return 2;
        }

        if (!string.Equals(args[0], MemoryBridgeCommands.RuntimeLayout, StringComparison.OrdinalIgnoreCase))
        {
            WriteResponse(new RuntimeLayoutResponse(false, "usage: runtime-layout <pid>", null));
            return 2;
        }

        if (Environment.Is64BitProcess)
        {
            WriteResponse(new RuntimeLayoutResponse(false, "MemoryBridge must run as x86", null));
            return 3;
        }

        try
        {
            if (!TryResolveRuntimeLayout(processId, out RuntimeLayoutDto? layout) || layout is null)
            {
                WriteResponse(new RuntimeLayoutResponse(false, "runtime layout unavailable", null));
                return 1;
            }

            WriteResponse(new RuntimeLayoutResponse(true, null, layout));
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            WriteResponse(new RuntimeLayoutResponse(false, ex.Message, null));
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            WriteResponse(new RuntimeLayoutResponse(false, ex.Message, null));
            return 1;
        }
        catch (ClrDiagnosticsException ex)
        {
            WriteResponse(new RuntimeLayoutResponse(false, ex.Message, null));
            return 1;
        }
        catch (Win32Exception ex)
        {
            WriteResponse(new RuntimeLayoutResponse(false, ex.Message, null));
            return 1;
        }
    }

    private static int RunRandomSeedBatch(string[] args)
    {
        if (args.Length != 3 ||
            !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int processId) ||
            !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) ||
            count is < 1 or > 256)
        {
            WriteResponse(new RandomSeedBatchResponse(
                false,
                "usage: random-seed-batch <pid> <count:1..256>",
                null,
                null));
            return 2;
        }

        if (Environment.Is64BitProcess)
        {
            WriteResponse(new RandomSeedBatchResponse(
                false,
                "MemoryBridge must run as x86",
                null,
                null));
            return 3;
        }

        try
        {
            if (!TryPredictRandomSeedBatch(processId, count, out RandomSeedBatchDto? batch) ||
                batch is null)
            {
                WriteResponse(new RandomSeedBatchResponse(
                    false,
                    "Terraria.Main.rand on the UI thread is unavailable",
                    null,
                    null));
                return 1;
            }

            WriteResponse(new RandomSeedBatchResponse(true, null, batch.Seeds, batch.OsThreadId));
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            WriteResponse(new RandomSeedBatchResponse(false, ex.Message, null, null));
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            WriteResponse(new RandomSeedBatchResponse(false, ex.Message, null, null));
            return 1;
        }
        catch (ClrDiagnosticsException ex)
        {
            WriteResponse(new RandomSeedBatchResponse(false, ex.Message, null, null));
            return 1;
        }
        catch (Win32Exception ex)
        {
            WriteResponse(new RandomSeedBatchResponse(false, ex.Message, null, null));
            return 1;
        }
    }

    private static bool TryPredictRandomSeedBatch(
        int targetProcessId,
        int count,
        out RandomSeedBatchDto? batch)
    {
        batch = null;
        using DataTarget target = DataTarget.CreateSnapshotAndAttach(targetProcessId);
        ClrInfo? clrInfo = target.ClrVersions.FirstOrDefault();
        if (clrInfo is null)
        {
            return false;
        }

        using ClrRuntime runtime = clrInfo.CreateRuntime();
        ClrType? mainType = FindType(runtime, "Terraria.Main");
        ClrThreadStaticField? randomField = mainType?.ThreadStaticFields
            .FirstOrDefault(field => string.Equals(field.Name, "rand", StringComparison.Ordinal));
        if (randomField is null)
        {
            return false;
        }

        uint windowThreadId = GetTerrariaWindowThreadId(targetProcessId);
        RandomStateCandidate? selected = null;
        foreach (ClrThread thread in runtime.Threads)
        {
            if (!thread.IsAlive || !randomField.IsInitialized(thread))
            {
                continue;
            }

            ClrObject random = randomField.ReadObject(thread);
            if (!TryReadUnifiedRandom(random, out UnifiedRandomState? state) || state is null)
            {
                continue;
            }

            int score = ScoreRandomThread(thread, windowThreadId);
            if (selected is null || score > selected.Score)
            {
                selected = new RandomStateCandidate(thread.OSThreadId, score, state);
            }
        }

        if (selected is null)
        {
            return false;
        }

        string[] seeds = new string[count];
        for (int index = 0; index < seeds.Length; index++)
        {
            seeds[index] = Next(selected.State)
                .ToString(CultureInfo.InvariantCulture);
        }

        batch = new RandomSeedBatchDto(seeds, selected.OsThreadId);
        return true;
    }

    private static bool TryReadUnifiedRandom(
        ClrObject random,
        out UnifiedRandomState? state)
    {
        state = null;
        if (random.IsNull ||
            !string.Equals(
                random.Type?.Name,
                "Terraria.Utilities.UnifiedRandom",
                StringComparison.Ordinal))
        {
            return false;
        }

        uint inext = random.ReadField<uint>("inext");
        ClrObject seedArrayObject = random.ReadObjectField("SeedArray");
        if (seedArrayObject.IsNull)
        {
            return false;
        }

        ClrArray seedArray = seedArrayObject.AsArray();
        if (seedArray.Length != 56)
        {
            return false;
        }

        int[]? values = seedArray.ReadValues<int>(0, seedArray.Length);
        if (values is null || values.Length != 56)
        {
            return false;
        }

        state = new UnifiedRandomState(inext, values);
        return true;
    }

    private static int ScoreRandomThread(ClrThread thread, uint windowThreadId)
    {
        if (windowThreadId != 0 && thread.OSThreadId == windowThreadId)
        {
            return 100_000;
        }

        int score = 0;
        foreach (ClrStackFrame frame in thread.EnumerateStackTrace(includeContext: false, maxFrames: 64))
        {
            ClrMethod? method = frame.Method;
            if (method is null ||
                !string.Equals(method.Type?.Name, "Terraria.Main", StringComparison.Ordinal))
            {
                continue;
            }

            score = Math.Max(
                score,
                method.Name is "DoUpdate" or "Update" or "Run" ? 10_000 : 1_000);
        }

        return score;
    }

    private static uint GetTerrariaWindowThreadId(int targetProcessId)
    {
        try
        {
            using Process process = Process.GetProcessById(targetProcessId);
            IntPtr window = process.MainWindowHandle;
            if (window == IntPtr.Zero)
            {
                return 0;
            }

            uint threadId = GetWindowThreadProcessId(window, out uint windowProcessId);
            return windowProcessId == (uint)targetProcessId ? threadId : 0;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
        catch (Win32Exception)
        {
            return 0;
        }
    }

    private static int Next(UnifiedRandomState state)
    {
        uint index = state.Inext + 1;
        if (index > 55)
        {
            index = 1;
        }

        uint subtractIndex = index + 21;
        if (subtractIndex > 55)
        {
            subtractIndex -= 55;
        }

        int value = state.SeedArray[index] - state.SeedArray[subtractIndex];
        if (value == int.MaxValue)
        {
            value--;
        }

        value += (value >> 31) & int.MaxValue;
        state.SeedArray[index] = value;
        state.Inext = index;
        return value;
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr window,
        out uint processId);

    private static bool TryResolveRuntimeLayout(int targetProcessId, out RuntimeLayoutDto? layout)
    {
        layout = null;

        using DataTarget target = DataTarget.CreateSnapshotAndAttach(targetProcessId);
        ClrInfo? clrInfo = target.ClrVersions.FirstOrDefault();
        if (clrInfo is null)
        {
            return false;
        }

        using ClrRuntime runtime = clrInfo.CreateRuntime();
        ClrType? mainType = FindType(runtime, "Terraria.Main");
        ClrType? npcType = FindType(runtime, "Terraria.NPC");
        ClrAppDomain? domain = FindDomain(runtime, mainType);
        if (mainType is null || domain is null)
        {
            return false;
        }

        CoreLayoutDto core = ResolveCoreLayout(mainType, domain);
        Dictionary<string, long> bossFacts = ResolveBossFactAddresses(mainType, npcType, domain);
        PlayerItemLayoutDto? item = ResolveItemLayout(runtime, mainType, domain);
        NpcLayoutDto? npc = ResolveNpcLayout(runtime, mainType, domain);
        BiomeLayoutDto? biome = ResolveBiomeLayout(runtime, mainType, domain);
        SeedUiLayoutDto? seedUi = ResolveSeedUiLayout(runtime, core.MenuUiStaticFieldAddress);
        WorldGenerationLayoutDto worldGeneration = ResolveWorldGenerationLayout(runtime, core.StatusTextStaticFieldAddress, domain);
        int resolvedFieldCount =
            CountNonZero(core.GameMenuStaticFieldAddress, core.StatusTextStaticFieldAddress, core.MenuUiStaticFieldAddress) +
            bossFacts.Count(pair => pair.Value != 0) +
            CountItemFields(item) +
            CountNpcFields(npc) +
            CountBiomeFields(biome) +
            CountSeedFields(seedUi) +
            CountWorldGenerationFields(worldGeneration);

        layout = new RuntimeLayoutDto(
            TerrariaVersion: null,
            core,
            new BossLayoutDto(bossFacts),
            item,
            npc,
            biome,
            seedUi,
            worldGeneration,
            resolvedFieldCount);
        return core.GameMenuStaticFieldAddress != 0 || resolvedFieldCount > 0;
    }

    private static CoreLayoutDto ResolveCoreLayout(ClrType mainType, ClrAppDomain domain)
    {
        return new CoreLayoutDto(
            GetStaticFieldAddress(mainType, domain, "gameMenu"),
            GetStaticFieldAddress(mainType, domain, "statusText"),
            GetStaticFieldAddress(mainType, domain, "MenuUI"));
    }

    private static Dictionary<string, long> ResolveBossFactAddresses(
        ClrType mainType,
        ClrType? npcType,
        ClrAppDomain domain)
    {
        Dictionary<string, long> addresses = new(StringComparer.OrdinalIgnoreCase);
        addresses["boss:wall-of-flesh:defeated"] = GetStaticFieldAddress(mainType, domain, "hardMode");
        if (npcType is null)
        {
            return addresses;
        }

        foreach ((string factKey, string fieldName) in BossNpcFieldByFactKey)
        {
            addresses[factKey] = GetStaticFieldAddress(npcType, domain, fieldName);
        }

        return addresses;
    }

    private static PlayerItemLayoutDto? ResolveItemLayout(ClrRuntime runtime, ClrType mainType, ClrAppDomain domain)
    {
        ClrType? playerType = FindType(runtime, "Terraria.Player");
        ClrType? chestType = FindType(runtime, "Terraria.Chest");
        ClrType? itemType = FindType(runtime, "Terraria.Item");
        if (playerType is null || chestType is null || itemType is null)
        {
            return null;
        }

        long playerStaticAddress = GetStaticFieldAddress(mainType, domain, "player");
        long myPlayerStaticAddress = GetStaticFieldAddress(mainType, domain, "myPlayer");
        long mouseItemStaticAddress = GetStaticFieldAddress(mainType, domain, "mouseItem");
        if (playerStaticAddress == 0 || myPlayerStaticAddress == 0 || mouseItemStaticAddress == 0)
        {
            return null;
        }

        int arrayFirstElementOffset = TryGetArrayFirstElementOffset(mainType, domain, "player") ??
            X86ArrayFirstElementFallbackOffset;
        return new PlayerItemLayoutDto(
            playerStaticAddress,
            myPlayerStaticAddress,
            mouseItemStaticAddress,
            GetRequiredFieldOffset(playerType, "armor"),
            GetRequiredFieldOffset(playerType, "dye"),
            GetRequiredFieldOffset(playerType, "miscEquips"),
            GetRequiredFieldOffset(playerType, "miscDyes"),
            GetRequiredFieldOffset(playerType, "trashItem"),
            GetRequiredFieldOffset(playerType, "inventory"),
            GetRequiredFieldOffset(playerType, "bank"),
            GetRequiredFieldOffset(playerType, "bank2"),
            GetRequiredFieldOffset(playerType, "bank3"),
            GetRequiredFieldOffset(playerType, "bank4"),
            GetRequiredFieldOffset(chestType, "item"),
            GetRequiredFieldOffset(itemType, "type"),
            GetRequiredFieldOffset(itemType, "stack"),
            X86ArrayLengthOffset,
            arrayFirstElementOffset,
            X86ObjectReferenceSize);
    }

    private static NpcLayoutDto? ResolveNpcLayout(ClrRuntime runtime, ClrType mainType, ClrAppDomain domain)
    {
        ClrType? npcType = FindType(runtime, "Terraria.NPC");
        if (npcType is null)
        {
            return null;
        }

        long npcStaticAddress = GetStaticFieldAddress(mainType, domain, "npc");
        if (npcStaticAddress == 0)
        {
            return null;
        }

        int arrayFirstElementOffset = TryGetArrayFirstElementOffset(mainType, domain, "npc") ??
            X86ArrayFirstElementFallbackOffset;
        return new NpcLayoutDto(
            npcStaticAddress,
            GetRequiredFieldOffset(npcType, "type"),
            GetRequiredFieldOffset(npcType, "active"),
            GetRequiredFieldOffset(npcType, "townNPC"),
            GetRequiredFieldOffset(npcType, "homeless"),
            GetRequiredFieldOffset(npcType, "homeTileX"),
            GetRequiredFieldOffset(npcType, "homeTileY"),
            X86ArrayLengthOffset,
            arrayFirstElementOffset,
            X86ObjectReferenceSize);
    }

    private static BiomeLayoutDto? ResolveBiomeLayout(ClrRuntime runtime, ClrType mainType, ClrAppDomain domain)
    {
        ClrType? playerType = FindType(runtime, "Terraria.Player");
        if (playerType is null)
        {
            return null;
        }

        long playerStaticAddress = GetStaticFieldAddress(mainType, domain, "player");
        long myPlayerStaticAddress = GetStaticFieldAddress(mainType, domain, "myPlayer");
        if (playerStaticAddress == 0 || myPlayerStaticAddress == 0)
        {
            return null;
        }

        Dictionary<string, int> zoneBitsByteFieldOffsets = ResolveZoneBitsByteFieldOffsets(playerType);
        if (zoneBitsByteFieldOffsets.Count == 0)
        {
            return null;
        }

        int arrayFirstElementOffset = TryGetArrayFirstElementOffset(mainType, domain, "player") ??
            X86ArrayFirstElementFallbackOffset;
        return new BiomeLayoutDto(
            playerStaticAddress,
            myPlayerStaticAddress,
            zoneBitsByteFieldOffsets,
            X86ArrayLengthOffset,
            arrayFirstElementOffset,
            X86ObjectReferenceSize);
    }

    private static SeedUiLayoutDto? ResolveSeedUiLayout(ClrRuntime runtime, long menuUiStaticFieldAddress)
    {
        ClrType? userInterfaceType = FindType(runtime, "Terraria.UI.UserInterface");
        ClrType? worldCreationType = FindType(runtime, "Terraria.GameContent.UI.States.UIWorldCreation");
        ClrType? worldCreationAdvancedType = FindType(runtime, "Terraria.GameContent.UI.States.UIWorldCreationAdvanced");
        ClrType? characterNameButtonType = FindType(runtime, "Terraria.GameContent.UI.Elements.UICharacterNameButton");
        if (menuUiStaticFieldAddress == 0 ||
            userInterfaceType is null ||
            worldCreationType is null ||
            characterNameButtonType is null ||
            !TryGetFieldOffset(userInterfaceType, "_currentState", out int currentStateOffset) ||
            !TryGetFieldOffset(worldCreationType, "_optionwWorldName", out int worldNameOffset) ||
            !TryGetFieldOffset(worldCreationType, "_optionSeed", out int seedOffset) ||
            !TryGetFieldOffset(worldCreationType, "_namePlate", out int namePlateOffset) ||
            !TryGetFieldOffset(worldCreationType, "_seedPlate", out int seedPlateOffset) ||
            !TryGetFieldOffset(characterNameButtonType, "actualContents", out int actualContentsOffset))
        {
            return null;
        }

        int advancedCreationStateOffset = -1;
        int advancedSeedPlateOffset = -1;
        if (worldCreationAdvancedType is not null)
        {
            _ = TryGetFieldOffset(worldCreationAdvancedType, "_creationState", out advancedCreationStateOffset);
            _ = TryGetFieldOffset(worldCreationAdvancedType, "_seedPlate", out advancedSeedPlateOffset);
        }

        return new SeedUiLayoutDto(
            menuUiStaticFieldAddress,
            currentStateOffset,
            UiStateNestedReferenceScanStart,
            UiStateNestedReferenceScanEnd,
            advancedCreationStateOffset,
            advancedSeedPlateOffset,
            worldNameOffset,
            seedOffset,
            namePlateOffset,
            seedPlateOffset,
            actualContentsOffset,
            X86ObjectReferenceSize);
    }

    private static WorldGenerationLayoutDto ResolveWorldGenerationLayout(
        ClrRuntime runtime,
        long statusTextStaticFieldAddress,
        ClrAppDomain domain)
    {
        ClrType? worldGeneratorType = FindType(runtime, "Terraria.WorldBuilding.WorldGenerator");
        ClrType? generationProgressType = FindType(runtime, "Terraria.WorldBuilding.GenerationProgress");
        ClrType? controllerType = FindType(runtime, "Terraria.WorldBuilding.WorldGenerator+Controller");
        ClrType? genPassType = FindType(runtime, "Terraria.WorldBuilding.GenPass");

        long currentGenerationProgressAddress = worldGeneratorType is null
            ? 0
            : GetStaticFieldAddress(worldGeneratorType, domain, "CurrentGenerationProgress");
        long currentControllerAddress = worldGeneratorType is null
            ? 0
            : GetStaticFieldAddress(worldGeneratorType, domain, "CurrentController");

        return new WorldGenerationLayoutDto(
            statusTextStaticFieldAddress,
            currentGenerationProgressAddress,
            currentControllerAddress,
            GetOptionalFieldOffset(generationProgressType, "_message"),
            GetOptionalFieldOffset(generationProgressType, "_value"),
            GetOptionalFieldOffset(generationProgressType, "_totalWeightedProgress"),
            GetOptionalFieldOffset(generationProgressType, "TotalWeight"),
            GetOptionalFieldOffset(generationProgressType, "CurrentPassWeight"),
            GetOptionalFieldOffset(controllerType, "_generator"),
            GetOptionalFieldOffset(worldGeneratorType, "_currentPass"),
            GetOptionalFieldOffset(genPassType, "Name"));
    }

    private static ClrAppDomain? FindDomain(ClrRuntime runtime, ClrType? mainType)
    {
        if (mainType is null)
        {
            return null;
        }

        ClrStaticField? gameMenuStatic = mainType.GetStaticFieldByName("gameMenu");
        return runtime.AppDomains.FirstOrDefault(appDomain =>
                gameMenuStatic?.IsInitialized(appDomain) == true) ??
            runtime.AppDomains.FirstOrDefault();
    }

    private static ClrType? FindType(ClrRuntime runtime, string typeName)
    {
        foreach (ClrModule module in runtime.EnumerateModules())
        {
            ClrType? type = module.GetTypeByName(typeName);
            if (type is not null)
            {
                return type;
            }
        }

        return null;
    }

    private static long GetStaticFieldAddress(ClrType type, ClrAppDomain domain, string fieldName)
    {
        ClrStaticField? field = type.GetStaticFieldByName(fieldName);
        if (field is null || !field.IsInitialized(domain))
        {
            return 0;
        }

        return unchecked((long)field.GetAddress(domain));
    }

    private static int GetRequiredFieldOffset(ClrType type, string fieldName)
    {
        if (!TryGetFieldOffset(type, fieldName, out int offset))
        {
            throw new InvalidOperationException($"Missing CLR field {type.Name}.{fieldName}.");
        }

        return offset;
    }

    private static int GetOptionalFieldOffset(ClrType? type, string fieldName)
    {
        return type is not null && TryGetFieldOffset(type, fieldName, out int offset)
            ? offset
            : -1;
    }

    private static bool TryGetFieldOffset(ClrType type, string fieldName, out int offset)
    {
        ClrInstanceField? field = type.GetFieldByName(fieldName);
        if (field is null)
        {
            offset = -1;
            return false;
        }

        offset = field.Offset + X86InstanceFieldOffsetBias;
        return true;
    }

    private static Dictionary<string, int> ResolveZoneBitsByteFieldOffsets(ClrType playerType)
    {
        Dictionary<string, int> offsets = new(StringComparer.OrdinalIgnoreCase);
        foreach (string zoneFieldName in ZoneBitsByteFieldNames)
        {
            if (TryGetFieldOffset(playerType, zoneFieldName, out int offset))
            {
                offsets[zoneFieldName] = offset;
            }
        }

        return offsets;
    }

    private static int? TryGetArrayFirstElementOffset(ClrType mainType, ClrAppDomain domain, string staticFieldName)
    {
        ClrStaticField? staticField = mainType.GetStaticFieldByName(staticFieldName);
        if (staticField is null || !staticField.IsInitialized(domain))
        {
            return null;
        }

        ClrObject arrayObject = staticField.ReadObject(domain);
        if (arrayObject.IsNull)
        {
            return null;
        }

        ClrArray array = arrayObject.AsArray();
        if (array.Length <= 0)
        {
            return X86ArrayFirstElementFallbackOffset;
        }

        ulong elementAddress = array.Type.GetArrayElementAddress(array.Address, 0);
        return elementAddress >= array.Address
            ? checked((int)(elementAddress - array.Address))
            : null;
    }

    private static int CountNonZero(params long[] addresses)
    {
        return addresses.Count(address => address != 0);
    }

    private static int CountItemFields(PlayerItemLayoutDto? item)
    {
        return item is null
            ? 0
            : CountNonZero(
                item.PlayerArrayStaticFieldAddress,
                item.MyPlayerStaticFieldAddress,
                item.MouseItemStaticFieldAddress) + 16;
    }

    private static int CountNpcFields(NpcLayoutDto? npc)
    {
        return npc is null
            ? 0
            : CountNonZero(npc.NpcArrayStaticFieldAddress) + 9;
    }

    private static int CountBiomeFields(BiomeLayoutDto? biome)
    {
        return biome is null
            ? 0
            : CountNonZero(biome.PlayerArrayStaticFieldAddress, biome.MyPlayerStaticFieldAddress) +
                (biome.ZoneBitsByteFieldOffsets?.Count ?? 0) + 3;
    }

    private static int CountSeedFields(SeedUiLayoutDto? seedUi)
    {
        return seedUi is null
            ? 0
            : CountNonZero(seedUi.MenuUiStaticFieldAddress) + 11;
    }

    private static int CountWorldGenerationFields(WorldGenerationLayoutDto worldGeneration)
    {
        return CountNonZero(
                worldGeneration.StatusTextStaticFieldAddress,
                worldGeneration.CurrentGenerationProgressStaticFieldAddress,
                worldGeneration.CurrentControllerStaticFieldAddress) +
            CountPresentOffsets(
                worldGeneration.GenerationProgressMessageFieldOffset,
                worldGeneration.GenerationProgressValueFieldOffset,
                worldGeneration.GenerationProgressTotalWeightedProgressFieldOffset,
                worldGeneration.GenerationProgressTotalWeightFieldOffset,
                worldGeneration.GenerationProgressCurrentPassWeightFieldOffset,
                worldGeneration.ControllerGeneratorFieldOffset,
                worldGeneration.WorldGeneratorCurrentPassFieldOffset,
                worldGeneration.GenPassNameFieldOffset);
    }

    private static int CountPresentOffsets(params int[] offsets)
    {
        return offsets.Count(offset => offset >= 0);
    }

    private static void WriteResponse<T>(T response)
    {
        Console.WriteLine(JsonSerializer.Serialize(response));
    }
}

internal sealed record RandomSeedBatchDto(
    IReadOnlyList<string> Seeds,
    uint OsThreadId);

internal sealed record RandomStateCandidate(
    uint OsThreadId,
    int Score,
    UnifiedRandomState State);

internal sealed class UnifiedRandomState(uint inext, int[] seedArray)
{
    public uint Inext { get; set; } = inext;

    public int[] SeedArray { get; } = seedArray;
}
