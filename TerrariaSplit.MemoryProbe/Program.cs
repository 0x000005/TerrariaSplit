using Microsoft.Diagnostics.Runtime;
using System.Globalization;
using System.Text.Json;

namespace TerrariaSplit.MemoryProbe;

internal static class Program
{
    private static readonly string[] ZoneBitsByteFieldNames = ["zone1", "zone2", "zone3", "zone4", "zone5"];

    private static int Main(string[] args)
    {
        if (args.Length != 2 ||
            !string.Equals(args[0], "item-layout", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int processId))
        {
            WriteResponse(new ItemLayoutProbeResponse(false, "usage: item-layout <pid>", null));
            return 2;
        }

        if (Environment.Is64BitProcess)
        {
            WriteResponse(new ItemLayoutProbeResponse(false, "memory probe must run as x86", null));
            return 3;
        }

        try
        {
            if (!TryResolveItemLayout(processId, out ItemLayoutDto? layout))
            {
                WriteResponse(new ItemLayoutProbeResponse(false, "item layout unavailable", null));
                return 1;
            }

            WriteResponse(new ItemLayoutProbeResponse(true, null, layout));
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            WriteResponse(new ItemLayoutProbeResponse(false, ex.Message, null));
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            WriteResponse(new ItemLayoutProbeResponse(false, ex.Message, null));
            return 1;
        }
        catch (ClrDiagnosticsException ex)
        {
            WriteResponse(new ItemLayoutProbeResponse(false, ex.Message, null));
            return 1;
        }
    }

    private static bool TryResolveItemLayout(int targetProcessId, out ItemLayoutDto? layout)
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

        ClrObject playerArrayObject = playerStatic.ReadObject(domain);
        int myPlayer = myPlayerStatic.Read<int>(domain);
        if (playerArrayObject.IsNull || myPlayer < 0)
        {
            return false;
        }

        ClrArray playerArray = playerArrayObject.AsArray();
        if (myPlayer >= playerArray.Length)
        {
            return false;
        }

        ulong localPlayerAddress = ReadFirstArrayReference(playerArray, myPlayer);
        if (localPlayerAddress == 0)
        {
            return false;
        }

        int arrayFirstElementOffset = GetArrayFirstElementOffset(playerArray.Type, playerArray.Address);
        ClrObject localPlayer = runtime.Heap.GetObject(localPlayerAddress);
        if (localPlayer.IsNull)
        {
            return false;
        }

        ClrInstanceField inventoryField = GetRequiredField(playerType, "inventory");
        ClrObject inventoryArrayObject = inventoryField.ReadObject(localPlayer.Address, interior: false);
        if (inventoryArrayObject.IsNull)
        {
            return false;
        }

        ClrArray inventoryArray = inventoryArrayObject.AsArray();
        if (inventoryArray.Length == 0)
        {
            return false;
        }

        ulong firstItemAddress = ReadFirstNonNullArrayReference(inventoryArray);
        if (firstItemAddress == 0)
        {
            return false;
        }

        ClrObject npcArrayObject = npcStatic.ReadObject(domain);
        if (npcArrayObject.IsNull)
        {
            return false;
        }

        ClrArray npcArray = npcArrayObject.AsArray();
        ulong firstNpcAddress = ReadFirstNonNullArrayReference(npcArray);
        if (firstNpcAddress == 0)
        {
            return false;
        }

        Dictionary<string, int> zoneBitsByteFieldOffsets = ResolveZoneBitsByteFieldOffsets(playerType, localPlayerAddress);

        layout = new ItemLayoutDto(
            unchecked((long)playerStaticAddress),
            unchecked((long)myPlayerStaticAddress),
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
            unchecked((long)npcStaticAddress),
            GetRequiredFieldOffset(npcType, "type", firstNpcAddress),
            GetRequiredFieldOffset(npcType, "active", firstNpcAddress),
            GetRequiredFieldOffset(npcType, "townNPC", firstNpcAddress),
            GetRequiredFieldOffset(npcType, "homeless", firstNpcAddress),
            GetRequiredFieldOffset(npcType, "homeTileX", firstNpcAddress),
            GetRequiredFieldOffset(npcType, "homeTileY", firstNpcAddress),
            zoneBitsByteFieldOffsets,
            ManagedArrayLengthOffset: 0x4,
            ManagedArrayFirstElementOffset: arrayFirstElementOffset,
            ObjectReferenceSize: 4);
        return true;
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

    private static ClrInstanceField GetRequiredField(ClrType type, string fieldName)
    {
        ClrInstanceField? field = type.GetFieldByName(fieldName);
        if (field is null)
        {
            throw new InvalidOperationException($"Missing CLR field {type.Name}.{fieldName}.");
        }

        return field;
    }

    private static int GetRequiredFieldOffset(ClrType type, string fieldName, ulong objectAddress)
    {
        ClrInstanceField field = GetRequiredField(type, fieldName);
        ulong fieldAddress = field.GetAddress(objectAddress, interior: false);
        if (fieldAddress < objectAddress)
        {
            throw new InvalidOperationException($"Invalid CLR field address {type.Name}.{fieldName}.");
        }

        return checked((int)(fieldAddress - objectAddress));
    }

    private static int GetFieldOffset(ClrType type, string fieldName)
    {
        return GetRequiredField(type, fieldName).Offset;
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

        return GetFieldOffset(chestType, "item");
    }

    private static Dictionary<string, int> ResolveZoneBitsByteFieldOffsets(ClrType playerType, ulong playerAddress)
    {
        Dictionary<string, int> offsets = new(StringComparer.OrdinalIgnoreCase);
        foreach (string zoneFieldName in ZoneBitsByteFieldNames)
        {
            if (TryGetFieldOffset(playerType, zoneFieldName, playerAddress, out int offset))
            {
                offsets[zoneFieldName] = offset;
            }
        }

        return offsets;
    }

    private static bool TryGetFieldOffset(ClrType type, string fieldName, ulong objectAddress, out int offset)
    {
        offset = 0;
        ClrInstanceField? field = type.GetFieldByName(fieldName);
        if (field is null)
        {
            return false;
        }

        if (objectAddress != 0)
        {
            ulong fieldAddress = field.GetAddress(objectAddress, interior: false);
            if (fieldAddress >= objectAddress)
            {
                offset = checked((int)(fieldAddress - objectAddress));
                return true;
            }
        }

        offset = field.Offset;
        return true;
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

    private static void WriteResponse(ItemLayoutProbeResponse response)
    {
        Console.WriteLine(JsonSerializer.Serialize(response));
    }
}

internal sealed record ItemLayoutProbeResponse(bool Success, string? Error, ItemLayoutDto? Layout);

internal sealed record ItemLayoutDto(
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
    int ObjectReferenceSize);
