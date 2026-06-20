namespace TerrariaSplit;

internal sealed class ItemFactProvider
{
    public TerrariaGameFacts Read(IProcessMemoryReader memory, TerrariaMemoryContext context)
    {
        if (context.Is64Bit ||
            context.ItemLayout is null ||
            !TerrariaLocalPlayerResolver.TryResolve(memory, context.ItemLayout, out IntPtr localPlayerAddress))
        {
            return TerrariaGameFacts.Unknown;
        }

        Dictionary<int, int> counts = new();
        bool allContainersRead = true;
        foreach (PlayerItemContainerDescriptor container in CreateContainers(context.ItemLayout))
        {
            allContainersRead &= ReadContainer(memory, localPlayerAddress, context.ItemLayout, container, counts);
        }

        if (!allContainersRead)
        {
            return TerrariaGameFacts.Unknown;
        }

        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        for (int itemId = 1; itemId <= SplitCatalog.MaxItemId; itemId++)
        {
            counts.TryGetValue(itemId, out int count);
            builder.SetInteger(SplitCatalog.CreateItemFactKey(itemId), count);
        }

        return builder.Build();
    }

    private static PlayerItemContainerDescriptor[] CreateContainers(TerrariaItemMemoryLayout layout)
    {
        return
        [
            new("armor", layout.PlayerArmorFieldOffset, IsArraySlot: true),
            new("dye", layout.PlayerDyeFieldOffset, IsArraySlot: true),
            new("miscEquips", layout.PlayerMiscEquipsFieldOffset, IsArraySlot: true),
            new("miscDyes", layout.PlayerMiscDyesFieldOffset, IsArraySlot: true),
            new("trashItem", layout.PlayerTrashItemFieldOffset, IsArraySlot: false, IsRequired: false),
            new("inventory", layout.PlayerInventoryFieldOffset, IsArraySlot: true),
            new("bank", layout.PlayerBankFieldOffset, IsArraySlot: false, IsChest: true, IsRequired: false),
            new("bank2", layout.PlayerBank2FieldOffset, IsArraySlot: false, IsChest: true, IsRequired: false),
            new("bank3", layout.PlayerBank3FieldOffset, IsArraySlot: false, IsChest: true, IsRequired: false),
            new("bank4", layout.PlayerBank4FieldOffset, IsArraySlot: false, IsChest: true, IsRequired: false)
        ];
    }

    private static bool ReadContainer(
        IProcessMemoryReader memory,
        IntPtr playerAddress,
        TerrariaItemMemoryLayout layout,
        PlayerItemContainerDescriptor container,
        Dictionary<int, int> counts)
    {
        IntPtr fieldAddress = IntPtr.Add(playerAddress, container.FieldOffset);
        if (!memory.TryReadPointerValue(fieldAddress, out IntPtr objectAddress) || objectAddress == IntPtr.Zero)
        {
            return !container.IsRequired;
        }

        bool read;
        if (container.IsChest)
        {
            IntPtr itemArrayFieldAddress = IntPtr.Add(objectAddress, layout.ChestItemArrayFieldOffset);
            read = memory.TryReadPointerValue(itemArrayFieldAddress, out IntPtr chestItemArrayAddress) &&
                ReadItemArray(memory, layout, chestItemArrayAddress, counts);
        }
        else
        {
            read = container.IsArraySlot
                ? ReadItemArray(memory, layout, objectAddress, counts)
                : ReadItemObject(memory, layout, objectAddress, counts);
        }

        return read || !container.IsRequired;
    }

    private static bool ReadItemArray(
        IProcessMemoryReader memory,
        TerrariaItemMemoryLayout layout,
        IntPtr arrayAddress,
        Dictionary<int, int> counts)
    {
        if (arrayAddress == IntPtr.Zero ||
            !memory.TryReadInt32(IntPtr.Add(arrayAddress, layout.ManagedArrayLengthOffset), out int length) ||
            length < 0 ||
            length > 512)
        {
            return false;
        }

        if (length == 0)
        {
            return true;
        }

        bool anyRead = false;
        for (int i = 0; i < length; i++)
        {
            IntPtr elementAddress = IntPtr.Add(
                arrayAddress,
                layout.ManagedArrayFirstElementOffset + i * layout.ObjectReferenceSize);
            if (memory.TryReadPointerValue(elementAddress, out IntPtr itemAddress) &&
                ReadItemObject(memory, layout, itemAddress, counts))
            {
                anyRead = true;
            }
        }

        return anyRead;
    }

    private static bool ReadItemObject(
        IProcessMemoryReader memory,
        TerrariaItemMemoryLayout layout,
        IntPtr itemAddress,
        Dictionary<int, int> counts)
    {
        if (itemAddress == IntPtr.Zero ||
            !memory.TryReadInt32(IntPtr.Add(itemAddress, layout.ItemTypeFieldOffset), out int itemType) ||
            !memory.TryReadInt32(IntPtr.Add(itemAddress, layout.ItemStackFieldOffset), out int stack))
        {
            return false;
        }

        if (itemType <= 0 || itemType > SplitCatalog.MaxItemId || stack <= 0)
        {
            return true;
        }

        counts.TryGetValue(itemType, out int existing);
        counts[itemType] = existing + stack;
        return true;
    }

    private sealed record PlayerItemContainerDescriptor(
        string Name,
        int FieldOffset,
        bool IsArraySlot,
        bool IsChest = false,
        bool IsRequired = true);
}
