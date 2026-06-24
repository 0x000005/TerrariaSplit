namespace TerrariaSplit.Terraria.Memory;

internal sealed class ItemFactProvider
{
    private Dictionary<int, int>? lastCounts;
    private Dictionary<int, int>? lastRawCounts;
    private bool lastReadsAll;
    private int[]? lastItemIds;
    private TerrariaGameFacts? lastFacts;

    public TerrariaGameFacts Read(IProcessMemoryReader memory, TerrariaMemoryContext context)
    {
        return Read(memory, context, TerrariaFactReadPlan.ReadAll);
    }

    public TerrariaGameFacts Read(
        IProcessMemoryReader memory,
        TerrariaMemoryContext context,
        TerrariaFactReadPlan readPlan)
    {
        if (context.Is64Bit ||
            !readPlan.ReadsItemFacts ||
            context.ItemLayout is null ||
            !TryResolveLocalPlayer(memory, context, out IntPtr localPlayerAddress))
        {
            return TerrariaGameFacts.Unknown;
        }

        Dictionary<int, int> counts = new();
        HashSet<IntPtr> seenItemAddresses = new();
        bool allContainersRead = true;
        foreach (PlayerItemContainerDescriptor container in CreateContainers(context.ItemLayout))
        {
            allContainersRead &= ReadContainer(
                memory,
                localPlayerAddress,
                context.ItemLayout,
                container,
                counts,
                seenItemAddresses,
                readPlan);
        }
        ReadMouseItem(memory, context.ItemLayout, counts, seenItemAddresses, readPlan);

        if (!allContainersRead)
        {
            return TerrariaGameFacts.Unknown;
        }

        int[] selectedItemIds = GetSelectedItemIds(readPlan);
        bool sameSelection = SelectionEquals(readPlan, selectedItemIds);
        Dictionary<int, int> publishedCounts = StabilizeCounts(counts, sameSelection);
        if (lastCounts is not null &&
            lastFacts is not null &&
            sameSelection &&
            CountsEqual(lastCounts, publishedCounts))
        {
            lastRawCounts = new Dictionary<int, int>(counts);
            return lastFacts;
        }

        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        IEnumerable<int> itemIds = readPlan.ReadsAll
            ? Enumerable.Range(1, SplitCatalog.MaxItemId)
            : selectedItemIds;
        foreach (int itemId in itemIds)
        {
            publishedCounts.TryGetValue(itemId, out int count);
            builder.SetInteger(SplitCatalog.CreateItemFactKey(itemId), count);
            if (!readPlan.ReadsAll)
            {
                builder.SetInteger(SplitCatalog.CreateItemEverOwnedFactKey(itemId), count);
            }
        }

        TerrariaGameFacts facts = builder.Build();
        lastCounts = publishedCounts;
        lastRawCounts = new Dictionary<int, int>(counts);
        lastReadsAll = readPlan.ReadsAll;
        lastItemIds = selectedItemIds;
        lastFacts = facts;
        return facts;
    }

    private static bool TryResolveLocalPlayer(
        IProcessMemoryReader memory,
        TerrariaMemoryContext context,
        out IntPtr localPlayerAddress)
    {
        localPlayerAddress = context.LocalPlayerAddress;
        return localPlayerAddress != IntPtr.Zero ||
            (context.ItemLayout is not null &&
                TerrariaLocalPlayerResolver.TryResolve(memory, context.ItemLayout, out localPlayerAddress));
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
            new("inventory", layout.PlayerInventoryFieldOffset, IsArraySlot: true, ExcludedArraySlotIndex: 58),
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
        Dictionary<int, int> counts,
        HashSet<IntPtr> seenItemAddresses,
        TerrariaFactReadPlan readPlan)
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
                ReadItemArray(memory, layout, chestItemArrayAddress, counts, seenItemAddresses, readPlan);
        }
        else
        {
            read = container.IsArraySlot
                ? ReadItemArray(memory, layout, objectAddress, counts, seenItemAddresses, readPlan, container.ExcludedArraySlotIndex)
                : ReadItemObject(memory, layout, objectAddress, counts, seenItemAddresses, readPlan);
        }

        return read || !container.IsRequired;
    }

    private static void ReadMouseItem(
        IProcessMemoryReader memory,
        TerrariaItemMemoryLayout layout,
        Dictionary<int, int> counts,
        HashSet<IntPtr> seenItemAddresses,
        TerrariaFactReadPlan readPlan)
    {
        if (layout.MouseItemStaticFieldAddress == IntPtr.Zero ||
            !memory.TryReadPointerValue(layout.MouseItemStaticFieldAddress, out IntPtr mouseItemAddress))
        {
            return;
        }

        _ = ReadItemObject(memory, layout, mouseItemAddress, counts, seenItemAddresses, readPlan);
    }

    private static bool ReadItemArray(
        IProcessMemoryReader memory,
        TerrariaItemMemoryLayout layout,
        IntPtr arrayAddress,
        Dictionary<int, int> counts,
        HashSet<IntPtr> seenItemAddresses,
        TerrariaFactReadPlan readPlan,
        int? excludedSlotIndex = null)
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
            if (excludedSlotIndex == i)
            {
                continue;
            }

            IntPtr elementAddress = IntPtr.Add(
                arrayAddress,
                layout.ManagedArrayFirstElementOffset + i * layout.ObjectReferenceSize);
            if (memory.TryReadPointerValue(elementAddress, out IntPtr itemAddress) &&
                ReadItemObject(memory, layout, itemAddress, counts, seenItemAddresses, readPlan))
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
        Dictionary<int, int> counts,
        HashSet<IntPtr> seenItemAddresses,
        TerrariaFactReadPlan readPlan)
    {
        if (itemAddress == IntPtr.Zero)
        {
            return false;
        }

        if (!memory.TryReadInt32(IntPtr.Add(itemAddress, layout.ItemTypeFieldOffset), out int itemType) ||
            !memory.TryReadInt32(IntPtr.Add(itemAddress, layout.ItemStackFieldOffset), out int stack))
        {
            return false;
        }

        if (!seenItemAddresses.Add(itemAddress))
        {
            return true;
        }

        if (itemType <= 0 ||
            itemType > SplitCatalog.MaxItemId ||
            stack <= 0 ||
            !readPlan.IncludesItemId(itemType))
        {
            return true;
        }

        counts.TryGetValue(itemType, out int existing);
        counts[itemType] = existing + stack;
        return true;
    }

    private Dictionary<int, int> StabilizeCounts(IReadOnlyDictionary<int, int> rawCounts, bool sameSelection)
    {
        if (!sameSelection || lastCounts is null || lastRawCounts is null)
        {
            return new Dictionary<int, int>(rawCounts);
        }

        Dictionary<int, int> publishedCounts = new();
        HashSet<int> itemIds = new(rawCounts.Keys);
        itemIds.UnionWith(lastCounts.Keys);

        foreach (int itemId in itemIds)
        {
            rawCounts.TryGetValue(itemId, out int rawCount);
            lastCounts.TryGetValue(itemId, out int lastPublishedCount);
            lastRawCounts.TryGetValue(itemId, out int lastRawCount);

            int publishedCount = rawCount;
            if (rawCount > lastPublishedCount && rawCount != lastRawCount)
            {
                publishedCount = lastPublishedCount;
            }

            if (publishedCount > 0)
            {
                publishedCounts[itemId] = publishedCount;
            }
        }

        return publishedCounts;
    }

    private bool SelectionEquals(TerrariaFactReadPlan readPlan, IReadOnlyList<int> selectedItemIds)
    {
        if (lastReadsAll != readPlan.ReadsAll)
        {
            return false;
        }

        return readPlan.ReadsAll ||
            (lastItemIds is not null && lastItemIds.SequenceEqual(selectedItemIds));
    }

    private static int[] GetSelectedItemIds(TerrariaFactReadPlan readPlan)
    {
        return readPlan.ReadsAll
            ? []
            : readPlan.ItemIds.OrderBy(itemId => itemId).ToArray();
    }

    private static bool CountsEqual(
        IReadOnlyDictionary<int, int> left,
        IReadOnlyDictionary<int, int> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach ((int itemId, int count) in left)
        {
            if (!right.TryGetValue(itemId, out int otherCount) || otherCount != count)
            {
                return false;
            }
        }

        return true;
    }

    private sealed record PlayerItemContainerDescriptor(
        string Name,
        int FieldOffset,
        bool IsArraySlot,
        bool IsChest = false,
        bool IsRequired = true,
        int? ExcludedArraySlotIndex = null);
}
