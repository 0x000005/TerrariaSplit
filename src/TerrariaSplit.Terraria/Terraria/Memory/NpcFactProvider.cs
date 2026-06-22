namespace TerrariaSplit.Terraria.Memory;

internal sealed class NpcFactProvider
{
    private HashSet<int>? lastPresentNpcIds;
    private bool lastReadsAll;
    private int[]? lastNpcIds;
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
            !readPlan.ReadsNpcFacts ||
            context.NpcLayout is null ||
            !TryReadPresentNpcIds(memory, context.NpcLayout, readPlan, out HashSet<int> presentNpcIds))
        {
            return TerrariaGameFacts.Unknown;
        }

        int[] selectedNpcIds = GetSelectedNpcIds(readPlan);
        if (lastPresentNpcIds is not null &&
            lastFacts is not null &&
            SelectionEquals(readPlan, selectedNpcIds) &&
            lastPresentNpcIds.SetEquals(presentNpcIds))
        {
            return lastFacts;
        }

        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        IEnumerable<int> npcIds = readPlan.ReadsAll
            ? TerrariaNpcCatalog.Items.Select(npc => npc.Id)
            : selectedNpcIds;
        foreach (int npcId in npcIds)
        {
            builder.SetBoolean(SplitCatalog.CreateNpcPresentFactKey(npcId), presentNpcIds.Contains(npcId));
        }

        TerrariaGameFacts facts = builder.Build();
        lastPresentNpcIds = new HashSet<int>(presentNpcIds);
        lastReadsAll = readPlan.ReadsAll;
        lastNpcIds = selectedNpcIds;
        lastFacts = facts;
        return facts;
    }

    private static bool TryReadPresentNpcIds(
        IProcessMemoryReader memory,
        TerrariaNpcMemoryLayout layout,
        TerrariaFactReadPlan readPlan,
        out HashSet<int> presentNpcIds)
    {
        presentNpcIds = new HashSet<int>();
        if (!memory.TryReadPointerValue(layout.NpcArrayStaticFieldAddress, out IntPtr npcArrayAddress) ||
            npcArrayAddress == IntPtr.Zero ||
            !memory.TryReadInt32(IntPtr.Add(npcArrayAddress, layout.ManagedArrayLengthOffset), out int length) ||
            length <= 0 ||
            length > 1024)
        {
            return false;
        }

        bool readAnyNpc = false;
        for (int i = 0; i < length; i++)
        {
            IntPtr elementAddress = IntPtr.Add(
                npcArrayAddress,
                layout.ManagedArrayFirstElementOffset + i * layout.ObjectReferenceSize);
            if (!memory.TryReadPointerValue(elementAddress, out IntPtr npcAddress))
            {
                continue;
            }

            if (npcAddress == IntPtr.Zero)
            {
                continue;
            }

            if (!memory.TryReadBool(IntPtr.Add(npcAddress, layout.NpcActiveFieldOffset), out bool active) ||
                !memory.TryReadBool(IntPtr.Add(npcAddress, layout.NpcTownNpcFieldOffset), out bool townNpc) ||
                !memory.TryReadInt32(IntPtr.Add(npcAddress, layout.NpcTypeFieldOffset), out int type))
            {
                continue;
            }

            readAnyNpc = true;
            if (active &&
                townNpc &&
                (readPlan.ReadsAll
                    ? TerrariaNpcCatalog.ById.ContainsKey(type)
                    : readPlan.IncludesNpcId(type)))
            {
                presentNpcIds.Add(type);
                if (!readPlan.ReadsAll && presentNpcIds.Count == readPlan.NpcIds.Count)
                {
                    break;
                }
            }
        }

        return readAnyNpc;
    }

    private bool SelectionEquals(TerrariaFactReadPlan readPlan, IReadOnlyList<int> selectedNpcIds)
    {
        if (lastReadsAll != readPlan.ReadsAll)
        {
            return false;
        }

        return readPlan.ReadsAll ||
            (lastNpcIds is not null && lastNpcIds.SequenceEqual(selectedNpcIds));
    }

    private static int[] GetSelectedNpcIds(TerrariaFactReadPlan readPlan)
    {
        return readPlan.ReadsAll
            ? []
            : readPlan.NpcIds.OrderBy(npcId => npcId).ToArray();
    }
}
