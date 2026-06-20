namespace TerrariaSplit;

internal sealed class TerrariaGameFactReader
{
    private readonly BossFactProvider bossFacts = new();
    private readonly ItemFactProvider itemFacts = new();
    private readonly NpcFactProvider npcFacts = new();
    private readonly BiomeFactProvider biomeFacts = new();

    public TerrariaGameFacts Read(IProcessMemoryReader memory, TerrariaMemoryContext context)
    {
        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        builder.Merge(bossFacts.Read(memory, context));
        builder.Merge(itemFacts.Read(memory, context));
        builder.Merge(npcFacts.Read(memory, context));
        builder.Merge(biomeFacts.Read(memory, context));
        return builder.Build();
    }
}
