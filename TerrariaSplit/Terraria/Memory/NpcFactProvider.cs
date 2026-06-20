namespace TerrariaSplit;

internal sealed class NpcFactProvider
{
    public TerrariaGameFacts Read(IProcessMemoryReader memory, TerrariaMemoryContext context)
    {
        if (context.Is64Bit ||
            context.NpcLayout is null ||
            !TryReadPresentNpcIds(memory, context.NpcLayout, out HashSet<int> presentNpcIds))
        {
            return TerrariaGameFacts.Unknown;
        }

        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        foreach (TerrariaNpcDefinition npc in TerrariaNpcCatalog.Items)
        {
            builder.SetBoolean(SplitCatalog.CreateNpcPresentFactKey(npc.Id), presentNpcIds.Contains(npc.Id));
        }

        return builder.Build();
    }

    private static bool TryReadPresentNpcIds(
        IProcessMemoryReader memory,
        TerrariaNpcMemoryLayout layout,
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
            if (active && townNpc && TerrariaNpcCatalog.ById.ContainsKey(type))
            {
                presentNpcIds.Add(type);
            }
        }

        return readAnyNpc;
    }
}
