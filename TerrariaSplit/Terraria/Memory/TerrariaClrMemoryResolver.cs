using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Diagnostics.Runtime;

namespace TerrariaSplit;

internal sealed class TerrariaClrMemoryResolver
{
    private static readonly TimeSpan ResolveRetryInterval = TimeSpan.FromSeconds(2);
    private const int MemoryProbeTimeoutMilliseconds = 15000;
    private const string MemoryProbeExecutableName = "TerrariaSplit.MemoryProbe.exe";

    private Process? process;
    private int? processId;
    private DateTime nextResolveAttemptUtc = DateTime.MinValue;
    private TerrariaItemMemoryLayout? itemLayout;
    private TerrariaNpcMemoryLayout? npcLayout;
    private TerrariaBiomeMemoryLayout? biomeLayout;

    public void SetProcess(Process? targetProcess)
    {
        int? targetProcessId = targetProcess is null ? null : GetProcessId(targetProcess);
        if (targetProcessId != processId)
        {
            Reset();
            processId = targetProcessId;
        }

        process = targetProcess;
    }

    public void Reset()
    {
        itemLayout = null;
        npcLayout = null;
        biomeLayout = null;
        nextResolveAttemptUtc = DateTime.MinValue;
    }

    public bool TryGetItemLayout(IProcessMemoryReader memory, out TerrariaItemMemoryLayout layout)
    {
        layout = null!;
        if (memory.Is64Bit)
        {
            return false;
        }

        if (itemLayout is not null)
        {
            layout = itemLayout;
            return true;
        }

        if (process is null || processId is null || DateTime.UtcNow < nextResolveAttemptUtc)
        {
            return false;
        }

        nextResolveAttemptUtc = DateTime.UtcNow + ResolveRetryInterval;
        if (TryResolveManagedLayouts(
                processId.Value,
                memory,
                out TerrariaItemMemoryLayout? resolvedItemLayout,
                out TerrariaNpcMemoryLayout? resolvedNpcLayout,
                out TerrariaBiomeMemoryLayout? resolvedBiomeLayout))
        {
            itemLayout = resolvedItemLayout;
            npcLayout = resolvedNpcLayout;
            biomeLayout = resolvedBiomeLayout;
            if (resolvedItemLayout is not null)
            {
                layout = resolvedItemLayout;
                return true;
            }
        }

        return false;
    }

    public bool TryGetNpcLayout(IProcessMemoryReader memory, out TerrariaNpcMemoryLayout layout)
    {
        layout = null!;
        if (memory.Is64Bit)
        {
            return false;
        }

        if (npcLayout is not null)
        {
            layout = npcLayout;
            return true;
        }

        if (process is null || processId is null || DateTime.UtcNow < nextResolveAttemptUtc)
        {
            return false;
        }

        nextResolveAttemptUtc = DateTime.UtcNow + ResolveRetryInterval;
        if (TryResolveManagedLayouts(
                processId.Value,
                memory,
                out TerrariaItemMemoryLayout? resolvedItemLayout,
                out TerrariaNpcMemoryLayout? resolvedNpcLayout,
                out TerrariaBiomeMemoryLayout? resolvedBiomeLayout))
        {
            itemLayout = resolvedItemLayout;
            npcLayout = resolvedNpcLayout;
            biomeLayout = resolvedBiomeLayout;
            if (resolvedNpcLayout is not null)
            {
                layout = resolvedNpcLayout;
                return true;
            }
        }

        return false;
    }

    public bool TryGetBiomeLayout(IProcessMemoryReader memory, out TerrariaBiomeMemoryLayout layout)
    {
        layout = null!;
        if (memory.Is64Bit)
        {
            return false;
        }

        if (biomeLayout is not null)
        {
            layout = biomeLayout;
            return true;
        }

        if (process is null || processId is null || DateTime.UtcNow < nextResolveAttemptUtc)
        {
            return false;
        }

        nextResolveAttemptUtc = DateTime.UtcNow + ResolveRetryInterval;
        if (TryResolveManagedLayouts(
                processId.Value,
                memory,
                out TerrariaItemMemoryLayout? resolvedItemLayout,
                out TerrariaNpcMemoryLayout? resolvedNpcLayout,
                out TerrariaBiomeMemoryLayout? resolvedBiomeLayout))
        {
            itemLayout = resolvedItemLayout;
            npcLayout = resolvedNpcLayout;
            biomeLayout = resolvedBiomeLayout;
            if (resolvedBiomeLayout is not null)
            {
                layout = resolvedBiomeLayout;
                return true;
            }
        }

        return false;
    }

    private static int? GetProcessId(Process targetProcess)
    {
        try
        {
            return targetProcess.HasExited ? null : targetProcess.Id;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static bool TryResolveManagedLayouts(
        int targetProcessId,
        IProcessMemoryReader memory,
        out TerrariaItemMemoryLayout? itemLayout,
        out TerrariaNpcMemoryLayout? npcLayout,
        out TerrariaBiomeMemoryLayout? biomeLayout)
    {
        itemLayout = null;
        npcLayout = null;
        biomeLayout = null;
        if (Environment.Is64BitProcess != memory.Is64Bit)
        {
            return TryResolveManagedLayoutsWithMemoryProbe(targetProcessId, out itemLayout, out npcLayout, out biomeLayout);
        }

        try
        {
            using DataTarget target = DataTarget.CreateSnapshotAndAttach(targetProcessId);
            ClrInfo? clrInfo = target.ClrVersions.FirstOrDefault();
            if (clrInfo is null)
            {
                return false;
            }

            using ClrRuntime runtime = clrInfo.CreateRuntime();
            ClrType? mainType = FindType(runtime, "Terraria.Main");
            ClrType? playerType = FindType(runtime, "Terraria.Player");
            ClrType? chestType = FindType(runtime, "Terraria.Chest");
            ClrType? itemType = FindType(runtime, "Terraria.Item");
            ClrType? npcType = FindType(runtime, "Terraria.NPC");
            if (mainType is null || playerType is null || chestType is null || itemType is null || npcType is null)
            {
                return false;
            }

            ClrStaticField? playerStatic = mainType.GetStaticFieldByName("player");
            ClrStaticField? myPlayerStatic = mainType.GetStaticFieldByName("myPlayer");
            ClrStaticField? npcStatic = mainType.GetStaticFieldByName("npc");
            ClrAppDomain? domain = runtime.AppDomains.FirstOrDefault(appDomain =>
                playerStatic?.IsInitialized(appDomain) == true &&
                myPlayerStatic?.IsInitialized(appDomain) == true &&
                npcStatic?.IsInitialized(appDomain) == true);
            if (playerStatic is null || myPlayerStatic is null || npcStatic is null || domain is null)
            {
                return false;
            }

            ulong playerStaticAddress = playerStatic.GetAddress(domain);
            ulong myPlayerStaticAddress = myPlayerStatic.GetAddress(domain);
            ulong npcStaticAddress = npcStatic.GetAddress(domain);
            if (playerStaticAddress == 0 || myPlayerStaticAddress == 0 || npcStaticAddress == 0)
            {
                return false;
            }

            int arrayLengthOffset = memory.Is64Bit ? 0x8 : 0x4;
            int objectReferenceSize = memory.Is64Bit ? 8 : 4;
            int? arrayFirstElementOffset = null;

            ClrObject playerArrayObject = playerStatic.ReadObject(domain);
            int myPlayer = myPlayerStatic.Read<int>(domain);
            ulong localPlayerAddress = 0;
            if (!playerArrayObject.IsNull)
            {
                ClrArray playerArray = playerArrayObject.AsArray();
                arrayFirstElementOffset = GetArrayFirstElementOffset(playerArray.Type, playerArray.Address);
                if (myPlayer >= 0 && myPlayer < playerArray.Length)
                {
                    localPlayerAddress = ReadFirstArrayReference(playerArray, myPlayer);
                }
            }

            ClrObject localPlayer = localPlayerAddress == 0
                ? default
                : runtime.Heap.GetObject(localPlayerAddress);
            if (localPlayer.IsNull)
            {
                localPlayerAddress = 0;
            }

            ClrObject npcArrayObject = npcStatic.ReadObject(domain);
            ulong firstNpcAddress = 0;
            if (!npcArrayObject.IsNull)
            {
                ClrArray npcArray = npcArrayObject.AsArray();
                arrayFirstElementOffset ??= GetArrayFirstElementOffset(npcArray.Type, npcArray.Address);
                firstNpcAddress = ReadFirstNonNullArrayReference(npcArray);
            }

            if (arrayFirstElementOffset is null)
            {
                return false;
            }

            int npcTypeFieldOffset = GetNpcFieldOffset(npcType, "type", firstNpcAddress);
            int npcActiveFieldOffset = GetNpcFieldOffset(npcType, "active", firstNpcAddress);
            int npcTownNpcFieldOffset = GetNpcFieldOffset(npcType, "townNPC", firstNpcAddress);
            int npcHomelessFieldOffset = GetNpcFieldOffset(npcType, "homeless", firstNpcAddress);
            int npcHomeTileXFieldOffset = GetNpcFieldOffset(npcType, "homeTileX", firstNpcAddress);
            int npcHomeTileYFieldOffset = GetNpcFieldOffset(npcType, "homeTileY", firstNpcAddress);

            if (localPlayerAddress != 0)
            {
                Dictionary<string, int> zoneBitsByteFieldOffsets =
                    ResolveZoneBitsByteFieldOffsets(playerType, localPlayerAddress);
                if (zoneBitsByteFieldOffsets.Count > 0)
                {
                    biomeLayout = new TerrariaBiomeMemoryLayout(
                        ToIntPtr(playerStaticAddress),
                        ToIntPtr(myPlayerStaticAddress),
                        zoneBitsByteFieldOffsets,
                        arrayLengthOffset,
                        arrayFirstElementOffset.Value,
                        objectReferenceSize);
                }
            }

            if (localPlayerAddress != 0 &&
                TryResolveInventoryArray(playerType, localPlayer, out ClrArray inventoryArray) &&
                TryReadFirstNonNullArrayReference(inventoryArray, out ulong firstItemAddress))
            {
                int itemArrayFirstElementOffset = GetArrayFirstElementOffset(inventoryArray.Type, inventoryArray.Address);
                itemLayout = new TerrariaItemMemoryLayout(
                    ToIntPtr(playerStaticAddress),
                    ToIntPtr(myPlayerStaticAddress),
                    GetRequiredFieldOffset(playerType, "armor", localPlayer.Address),
                    GetRequiredFieldOffset(playerType, "dye", localPlayer.Address),
                    GetRequiredFieldOffset(playerType, "miscEquips", localPlayer.Address),
                    GetRequiredFieldOffset(playerType, "miscDyes", localPlayer.Address),
                    GetRequiredFieldOffset(playerType, "trashItem", localPlayer.Address),
                    GetRequiredFieldOffset(playerType, "inventory", localPlayer.Address),
                    GetRequiredFieldOffset(playerType, "bank", localPlayer.Address),
                    GetRequiredFieldOffset(playerType, "bank2", localPlayer.Address),
                    GetRequiredFieldOffset(playerType, "bank3", localPlayer.Address),
                    GetRequiredFieldOffset(playerType, "bank4", localPlayer.Address),
                    GetChestItemArrayFieldOffset(chestType, playerType, localPlayer),
                    GetRequiredFieldOffset(itemType, "type", firstItemAddress),
                    GetRequiredFieldOffset(itemType, "stack", firstItemAddress),
                    arrayLengthOffset,
                    itemArrayFirstElementOffset,
                    objectReferenceSize);
            }

            npcLayout = new TerrariaNpcMemoryLayout(
                ToIntPtr(npcStaticAddress),
                npcTypeFieldOffset,
                npcActiveFieldOffset,
                npcTownNpcFieldOffset,
                npcHomelessFieldOffset,
                npcHomeTileXFieldOffset,
                npcHomeTileYFieldOffset,
                arrayLengthOffset,
                arrayFirstElementOffset.Value,
                objectReferenceSize);
            return itemLayout is not null || npcLayout is not null || biomeLayout is not null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (ClrDiagnosticsException)
        {
            return false;
        }
    }

    private static bool TryResolveManagedLayoutsWithMemoryProbe(
        int targetProcessId,
        out TerrariaItemMemoryLayout? itemLayout,
        out TerrariaNpcMemoryLayout? npcLayout,
        out TerrariaBiomeMemoryLayout? biomeLayout)
    {
        itemLayout = null;
        npcLayout = null;
        biomeLayout = null;
        string? probePath = FindMemoryProbeExecutable();
        if (probePath is null)
        {
            return false;
        }

        try
        {
            using Process? probe = StartMemoryProbe(probePath, targetProcessId);
            if (probe is null)
            {
                return false;
            }

            if (!probe.WaitForExit(MemoryProbeTimeoutMilliseconds))
            {
                TryKill(probe);
                return false;
            }

            string output = probe.StandardOutput.ReadToEnd();
            _ = probe.StandardError.ReadToEnd();
            ItemLayoutProbeResponse? response = JsonSerializer.Deserialize<ItemLayoutProbeResponse>(output.Trim());
            if (response?.Success == true && response.Layout is not null)
            {
                itemLayout = response.Layout.ToItemMemoryLayout();
                npcLayout = response.Layout.ToNpcMemoryLayout();
                biomeLayout = response.Layout.ToBiomeMemoryLayout();
                return true;
            }
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static Process? StartMemoryProbe(string probePath, int targetProcessId)
    {
        var startInfo = new ProcessStartInfo(probePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("item-layout");
        startInfo.ArgumentList.Add(targetProcessId.ToString(CultureInfo.InvariantCulture));
        return Process.Start(startInfo);
    }

    private static string? FindMemoryProbeExecutable()
    {
        foreach (string path in EnumerateMemoryProbeCandidatePaths())
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateMemoryProbeCandidatePaths()
    {
        foreach (string baseDirectory in EnumerateBaseDirectories())
        {
            yield return Path.Combine(baseDirectory, MemoryProbeExecutableName);
            yield return Path.Combine(baseDirectory, "TerrariaSplit.MemoryProbe", MemoryProbeExecutableName);

            DirectoryInfo? directory = new(baseDirectory);
            for (int depth = 0; directory is not null && depth < 8; depth++, directory = directory.Parent)
            {
                foreach (string configuration in new[] { "Debug", "Release" })
                {
                    yield return Path.Combine(
                        directory.FullName,
                        "TerrariaSplit.MemoryProbe",
                        "bin",
                        configuration,
                        "net10.0-windows",
                        "win-x86",
                        MemoryProbeExecutableName);
                    yield return Path.Combine(
                        directory.FullName,
                        "TerrariaSplit.MemoryProbe",
                        "bin",
                        configuration,
                        "net10.0-windows",
                        MemoryProbeExecutableName);
                    yield return Path.Combine(
                        directory.FullName,
                        "TerrariaSplit.MemoryProbe",
                        ".codex-build",
                        "bin",
                        configuration,
                        "net10.0-windows",
                        "win-x86",
                        MemoryProbeExecutableName);
                    yield return Path.Combine(
                        directory.FullName,
                        "TerrariaSplit.MemoryProbe",
                        ".codex-build",
                        "bin",
                        configuration,
                        "net10.0-windows",
                        MemoryProbeExecutableName);
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateBaseDirectories()
    {
        yield return AppContext.BaseDirectory;
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill();
        }
        catch (InvalidOperationException)
        {
        }
        catch (Win32Exception)
        {
        }
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

    private static int GetRequiredFieldOffset(ClrType type, string fieldName)
    {
        ClrInstanceField? field = type.GetFieldByName(fieldName);
        if (field is null)
        {
            throw new InvalidOperationException($"Missing CLR field {type.Name}.{fieldName}.");
        }

        return field.Offset;
    }

    private static Dictionary<string, int> ResolveZoneBitsByteFieldOffsets(ClrType playerType, ulong playerAddress)
    {
        Dictionary<string, int> offsets = new(StringComparer.OrdinalIgnoreCase);
        foreach (string zoneFieldName in TerrariaBiomeCatalog.RequiredZoneFieldNames)
        {
            if (TryGetFieldOffset(playerType, zoneFieldName, playerAddress, out int offset))
            {
                offsets[zoneFieldName] = offset;
            }
        }

        return offsets;
    }

    private static bool TryGetFieldOffset(ClrType type, string fieldName, out int offset)
    {
        ClrInstanceField? field = type.GetFieldByName(fieldName);
        if (field is null)
        {
            offset = 0;
            return false;
        }

        offset = field.Offset;
        return true;
    }

    private static bool TryGetFieldOffset(ClrType type, string fieldName, ulong objectAddress, out int offset)
    {
        if (!TryGetFieldOffset(type, fieldName, out offset))
        {
            return false;
        }

        ClrInstanceField? field = type.GetFieldByName(fieldName);
        if (field is null || objectAddress == 0)
        {
            return true;
        }

        ulong fieldAddress = field.GetAddress(objectAddress, interior: false);
        if (fieldAddress < objectAddress)
        {
            return true;
        }

        offset = checked((int)(fieldAddress - objectAddress));
        return true;
    }

    private static int GetRequiredFieldOffset(ClrType type, string fieldName, ulong objectAddress)
    {
        ClrInstanceField? field = type.GetFieldByName(fieldName);
        if (field is null)
        {
            throw new InvalidOperationException($"Missing CLR field {type.Name}.{fieldName}.");
        }

        ulong fieldAddress = field.GetAddress(objectAddress, interior: false);
        if (fieldAddress < objectAddress)
        {
            throw new InvalidOperationException($"Invalid CLR field address {type.Name}.{fieldName}.");
        }

        return checked((int)(fieldAddress - objectAddress));
    }

    private static int GetNpcFieldOffset(ClrType npcType, string fieldName, ulong firstNpcAddress)
    {
        return firstNpcAddress == 0
            ? GetRequiredFieldOffset(npcType, fieldName)
            : GetRequiredFieldOffset(npcType, fieldName, firstNpcAddress);
    }

    private static bool TryResolveInventoryArray(ClrType playerType, ClrObject localPlayer, out ClrArray inventoryArray)
    {
        inventoryArray = default;
        ClrInstanceField? inventoryField = playerType.GetFieldByName("inventory");
        if (inventoryField is null || localPlayer.IsNull)
        {
            return false;
        }

        ClrObject inventoryArrayObject = inventoryField.ReadObject(localPlayer.Address, interior: false);
        if (inventoryArrayObject.IsNull)
        {
            return false;
        }

        inventoryArray = inventoryArrayObject.AsArray();
        return inventoryArray.Length > 0;
    }

    private static int GetChestItemArrayFieldOffset(ClrType chestType, ClrType playerType, ClrObject localPlayer)
    {
        foreach (string bankFieldName in new[] { "bank", "bank2", "bank3", "bank4" })
        {
            ClrInstanceField? bankField = playerType.GetFieldByName(bankFieldName);
            if (bankField is null)
            {
                continue;
            }

            ClrObject chestObject = bankField.ReadObject(localPlayer.Address, interior: false);
            if (!chestObject.IsNull)
            {
                return GetRequiredFieldOffset(chestType, "item", chestObject.Address);
            }
        }

        return GetRequiredFieldOffset(chestType, "item");
    }

    private static int GetArrayFirstElementOffset(ClrType arrayType, ulong arrayAddress)
    {
        ulong elementAddress = arrayType.GetArrayElementAddress(arrayAddress, 0);
        if (elementAddress < arrayAddress)
        {
            throw new InvalidOperationException($"Invalid CLR array element address {arrayType.Name}.");
        }

        return checked((int)(elementAddress - arrayAddress));
    }

    private static ulong ReadFirstArrayReference(ClrArray array, int index)
    {
        IEnumerable<ulong>? values = array.ReadValues<ulong>(index, 1);
        return values?.FirstOrDefault() ?? 0;
    }

    private static bool TryReadFirstNonNullArrayReference(ClrArray array, out ulong address)
    {
        address = ReadFirstNonNullArrayReference(array);
        return address != 0;
    }

    private static ulong ReadFirstNonNullArrayReference(ClrArray array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            ulong address = ReadFirstArrayReference(array, i);
            if (address != 0)
            {
                return address;
            }
        }

        return 0;
    }

    private static IntPtr ToIntPtr(ulong address)
    {
        return IntPtr.Size == 8
            ? new IntPtr(unchecked((long)address))
            : new IntPtr(unchecked((int)address));
    }

    private sealed record ItemLayoutProbeResponse(bool Success, string? Error, ItemLayoutProbeDto? Layout);

    private sealed record ItemLayoutProbeDto(
        long PlayerArrayStaticFieldAddress,
        long MyPlayerStaticFieldAddress,
        int PlayerArmorFieldOffset,
        int PlayerDyeFieldOffset,
        int PlayerMiscEquipsFieldOffset,
        int PlayerMiscDyesFieldOffset,
        int PlayerTrashItemFieldOffset,
        int PlayerInventoryFieldOffset,
        int PlayerBankFieldOffset,
        int PlayerBank2FieldOffset,
        int PlayerBank3FieldOffset,
        int PlayerBank4FieldOffset,
        int ChestItemArrayFieldOffset,
        int ItemTypeFieldOffset,
        int ItemStackFieldOffset,
        long NpcArrayStaticFieldAddress,
        int NpcTypeFieldOffset,
        int NpcActiveFieldOffset,
        int NpcTownNpcFieldOffset,
        int NpcHomelessFieldOffset,
        int NpcHomeTileXFieldOffset,
        int NpcHomeTileYFieldOffset,
        Dictionary<string, int>? ZoneBitsByteFieldOffsets,
        int ManagedArrayLengthOffset,
        int ManagedArrayFirstElementOffset,
        int ObjectReferenceSize)
    {
        public TerrariaItemMemoryLayout ToItemMemoryLayout()
        {
            return new TerrariaItemMemoryLayout(
                new IntPtr(PlayerArrayStaticFieldAddress),
                new IntPtr(MyPlayerStaticFieldAddress),
                PlayerArmorFieldOffset,
                PlayerDyeFieldOffset,
                PlayerMiscEquipsFieldOffset,
                PlayerMiscDyesFieldOffset,
                PlayerTrashItemFieldOffset,
                PlayerInventoryFieldOffset,
                PlayerBankFieldOffset,
                PlayerBank2FieldOffset,
                PlayerBank3FieldOffset,
                PlayerBank4FieldOffset,
                ChestItemArrayFieldOffset,
                ItemTypeFieldOffset,
                ItemStackFieldOffset,
                ManagedArrayLengthOffset,
                ManagedArrayFirstElementOffset,
                ObjectReferenceSize);
        }

        public TerrariaNpcMemoryLayout ToNpcMemoryLayout()
        {
            return new TerrariaNpcMemoryLayout(
                new IntPtr(NpcArrayStaticFieldAddress),
                NpcTypeFieldOffset,
                NpcActiveFieldOffset,
                NpcTownNpcFieldOffset,
                NpcHomelessFieldOffset,
                NpcHomeTileXFieldOffset,
                NpcHomeTileYFieldOffset,
                ManagedArrayLengthOffset,
                ManagedArrayFirstElementOffset,
                ObjectReferenceSize);
        }

        public TerrariaBiomeMemoryLayout ToBiomeMemoryLayout()
        {
            return new TerrariaBiomeMemoryLayout(
                new IntPtr(PlayerArrayStaticFieldAddress),
                new IntPtr(MyPlayerStaticFieldAddress),
                ZoneBitsByteFieldOffsets ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                ManagedArrayLengthOffset,
                ManagedArrayFirstElementOffset,
                ObjectReferenceSize);
        }
    }
}
