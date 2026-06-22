namespace TerrariaSplit.Terraria.Memory;

internal sealed class BossFactProvider
{
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
        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        if (!readPlan.ReadsBossFacts)
        {
            return TerrariaGameFacts.Unknown;
        }

        BossFactDescriptor[] bosses = SplitCatalog.BossFacts
            .Where(boss => readPlan.IncludesBossFactKey(boss.FactKey))
            .ToArray();
        byte[]? flagBytes = null;
        int minimumOffset = 0;
        bool hasFlagBlock = TryReadFlagBlock(memory, context, bosses, out flagBytes, out minimumOffset);

        foreach (BossFactDescriptor boss in bosses)
        {
            bool? value = boss.AddressKind switch
            {
                BossFactAddressKind.Hardmode => ReadHardmode(memory, context),
                BossFactAddressKind.BossFlagBlock when hasFlagBlock => ReadFlag(flagBytes!, minimumOffset, boss.Offset),
                _ => null
            };
            builder.SetBoolean(boss.FactKey, value);
        }

        TerrariaGameFacts facts = builder.Build();
        if (lastFacts is not null && facts.Equals(lastFacts))
        {
            return lastFacts;
        }

        lastFacts = facts;
        return facts;
    }

    private static bool TryReadFlagBlock(
        IProcessMemoryReader memory,
        TerrariaMemoryContext context,
        IReadOnlyCollection<BossFactDescriptor> bosses,
        out byte[]? bytes,
        out int minimumOffset)
    {
        bytes = null;
        minimumOffset = 0;
        if (context.BossFlags is null ||
            context.BossFlags.BaseAddress == IntPtr.Zero ||
            !bosses.Any(boss => boss.AddressKind == BossFactAddressKind.BossFlagBlock))
        {
            return false;
        }

        int[] offsets = bosses
            .Where(boss => boss.AddressKind == BossFactAddressKind.BossFlagBlock)
            .Select(boss => boss.Offset)
            .ToArray();
        minimumOffset = offsets.Min();
        int maximumOffset = offsets.Max();
        int length = maximumOffset - minimumOffset + 1;
        return memory.TryReadBytes(IntPtr.Add(context.BossFlags.BaseAddress, minimumOffset), length, out bytes);
    }

    private static bool? ReadFlag(byte[] bytes, int minimumOffset, int offset)
    {
        int index = offset - minimumOffset;
        return index >= 0 && index < bytes.Length
            ? bytes[index] != 0
            : null;
    }

    private static bool? ReadHardmode(IProcessMemoryReader memory, TerrariaMemoryContext context)
    {
        if (context.HardmodeAddress == IntPtr.Zero)
        {
            return null;
        }

        return memory.TryReadBool(context.HardmodeAddress, out bool value)
            ? value
            : null;
    }
}
