namespace TerrariaSplit.Terraria.Memory;

internal sealed class TerrariaFactReadPlan
{
    private static readonly IReadOnlySet<string> EmptyFactKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlySet<int> EmptyIds = new HashSet<int>();

    private TerrariaFactReadPlan(
        bool readsAll,
        IReadOnlySet<string> bossFactKeys,
        IReadOnlySet<int> itemIds,
        IReadOnlySet<int> npcIds,
        IReadOnlySet<string> biomeIds)
    {
        ReadsAll = readsAll;
        BossFactKeys = bossFactKeys;
        ItemIds = itemIds;
        NpcIds = npcIds;
        BiomeIds = biomeIds;
    }

    public static TerrariaFactReadPlan ReadAll { get; } = new(
        true,
        EmptyFactKeys,
        EmptyIds,
        EmptyIds,
        EmptyFactKeys);

    public bool ReadsAll { get; }

    public IReadOnlySet<string> BossFactKeys { get; }

    public IReadOnlySet<int> ItemIds { get; }

    public IReadOnlySet<int> NpcIds { get; }

    public IReadOnlySet<string> BiomeIds { get; }

    public bool ReadsBossFacts => ReadsAll || BossFactKeys.Count > 0;

    public bool ReadsItemFacts => ReadsAll || ItemIds.Count > 0;

    public bool ReadsNpcFacts => ReadsAll || NpcIds.Count > 0;

    public bool ReadsBiomeFacts => ReadsAll || BiomeIds.Count > 0;

    public static TerrariaFactReadPlan FromObservedFactKeys(IReadOnlyCollection<string>? factKeys)
    {
        if (factKeys is null)
        {
            return ReadAll;
        }

        var bossFactKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var itemIds = new HashSet<int>();
        var npcIds = new HashSet<int>();
        var biomeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string factKey in factKeys)
        {
            AddFactKey(factKey, bossFactKeys, itemIds, npcIds, biomeIds);
        }

        return new TerrariaFactReadPlan(
            false,
            bossFactKeys,
            itemIds,
            npcIds,
            biomeIds);
    }

    public bool IncludesBossFactKey(string factKey)
    {
        return ReadsAll || BossFactKeys.Contains(factKey);
    }

    public bool IncludesItemId(int itemId)
    {
        return ReadsAll || ItemIds.Contains(itemId);
    }

    public bool IncludesNpcId(int npcId)
    {
        return ReadsAll || NpcIds.Contains(npcId);
    }

    public bool IncludesBiomeId(string biomeId)
    {
        return ReadsAll || BiomeIds.Contains(biomeId);
    }

    private static void AddFactKey(
        string factKey,
        HashSet<string> bossFactKeys,
        HashSet<int> itemIds,
        HashSet<int> npcIds,
        HashSet<string> biomeIds)
    {
        if (string.IsNullOrWhiteSpace(factKey))
        {
            return;
        }

        if (SplitCatalog.BossFacts.Any(boss =>
                string.Equals(boss.FactKey, factKey, StringComparison.OrdinalIgnoreCase)))
        {
            bossFactKeys.Add(factKey.Trim());
            return;
        }

        if (SplitCatalog.TryParseItemFactKey(factKey, out int itemId))
        {
            itemIds.Add(itemId);
            return;
        }

        if (SplitCatalog.TryParseNpcPresentFactKey(factKey, out int npcId))
        {
            npcIds.Add(npcId);
            return;
        }

        if (SplitCatalog.TryParseBiomeActiveFactKey(factKey, out string? biomeId))
        {
            biomeIds.Add(biomeId);
        }
    }
}
