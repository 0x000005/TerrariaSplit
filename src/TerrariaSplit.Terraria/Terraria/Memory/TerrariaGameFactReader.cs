namespace TerrariaSplit.Terraria.Memory;

internal sealed class TerrariaGameFactReader
{
    private readonly BossFactProvider bossFacts = new();
    private readonly ItemFactProvider itemFacts = new();
    private readonly NpcFactProvider npcFacts = new();
    private readonly BiomeFactProvider biomeFacts = new();
    private TerrariaGameFacts? lastBossFacts;
    private TerrariaGameFacts? lastItemFacts;
    private TerrariaGameFacts? lastNpcFacts;
    private TerrariaGameFacts? lastBiomeFacts;
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
        TerrariaGameFacts boss = bossFacts.Read(memory, context, readPlan);
        TerrariaGameFacts item = itemFacts.Read(memory, context, readPlan);
        TerrariaGameFacts npc = npcFacts.Read(memory, context, readPlan);
        TerrariaGameFacts biome = biomeFacts.Read(memory, context, readPlan);

        if (lastFacts is not null &&
            ReferenceEquals(boss, lastBossFacts) &&
            ReferenceEquals(item, lastItemFacts) &&
            ReferenceEquals(npc, lastNpcFacts) &&
            ReferenceEquals(biome, lastBiomeFacts))
        {
            return lastFacts;
        }

        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        builder.Merge(boss);
        builder.Merge(item);
        builder.Merge(npc);
        builder.Merge(biome);
        TerrariaGameFacts facts = builder.Build();

        lastBossFacts = boss;
        lastItemFacts = item;
        lastNpcFacts = npc;
        lastBiomeFacts = biome;
        lastFacts = facts;
        return facts;
    }
}
