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
        foreach (BossFactDescriptor boss in bosses)
        {
            builder.SetBoolean(boss.FactKey, ReadFact(memory, context, boss.FactKey));
        }

        TerrariaGameFacts facts = builder.Build();
        if (lastFacts is not null && facts.Equals(lastFacts))
        {
            return lastFacts;
        }

        lastFacts = facts;
        return facts;
    }

    private static bool? ReadFact(
        IProcessMemoryReader memory,
        TerrariaMemoryContext context,
        string factKey)
    {
        if (context.BossLayout is null ||
            !context.BossLayout.TryGetFactAddress(factKey, out IntPtr address))
        {
            return null;
        }

        return memory.TryReadBool(address, out bool value)
            ? value
            : null;
    }
}
