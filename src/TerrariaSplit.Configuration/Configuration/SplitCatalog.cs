namespace TerrariaSplit.Configuration;

public enum BossFactAddressKind
{
    BossFlagBlock,
    Hardmode
}

public sealed record BossFactDescriptor(
    string TargetId,
    string DisplayName,
    string FactKey,
    BossFactAddressKind AddressKind,
    int Offset,
    string IconFileName);

public static class SplitCatalog
{
    public const string Skeletron = "boss:skeletron";
    public const string WallOfFlesh = "boss:wall-of-flesh";
    public const string Destroyer = "boss:destroyer";
    public const string SkeletronPrime = "boss:skeletron-prime";
    public const string Twins = "boss:twins";
    public const string Plantera = "boss:plantera";
    public const string Golem = "boss:golem";
    public const string LunaticCultist = "boss:lunatic-cultist";
    public const string MoonLord = "boss:moon-lord";

    public const int MaxItemId = SplitFactKeys.MaxItemId;

    private static readonly IReadOnlyDictionary<string, BossFactDescriptor> BossByTargetId;
    private static readonly IReadOnlyDictionary<string, SplitTargetDefinition> BossTargetsById;

    static SplitCatalog()
    {
        BossByTargetId = BossFacts.ToDictionary(item => item.TargetId, StringComparer.OrdinalIgnoreCase);
        BossTargetsById = BossFacts
            .Select(item => new SplitTargetDefinition(
                item.TargetId,
                SplitTargetKind.Boss,
                item.DisplayName,
                item.FactKey,
                item.IconFileName))
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
    }

    public static readonly IReadOnlyList<BossFactDescriptor> BossFacts =
    [
        new("boss:king-slime", "King Slime", "boss:king-slime:defeated", BossFactAddressKind.BossFlagBlock, 0, "king-slime.png"),
        new("boss:eye-of-cthulhu", "Eye of Cthulhu", "boss:eye-of-cthulhu:defeated", BossFactAddressKind.BossFlagBlock, -4, "eye-of-cthulhu.png"),
        new("boss:eater-of-worlds", "Eater of Worlds", "boss:eater-of-worlds:defeated", BossFactAddressKind.BossFlagBlock, -3, "eater-of-worlds.png"),
        new("boss:brain-of-cthulhu", "Brain of Cthulhu", "boss:brain-of-cthulhu:defeated", BossFactAddressKind.BossFlagBlock, -3, "brain-of-cthulhu.png"),
        new("boss:queen-bee", "Queen Bee", "boss:queen-bee:defeated", BossFactAddressKind.BossFlagBlock, -1, "queen-bee.png"),
        new(Skeletron, "Skeletron", "boss:skeletron:defeated", BossFactAddressKind.BossFlagBlock, -2, "skeletron.png"),
        new("boss:deerclops", "Deerclops", "boss:deerclops:defeated", BossFactAddressKind.BossFlagBlock, 22, "deerclops.png"),
        new(WallOfFlesh, "Wall of Flesh", "boss:wall-of-flesh:defeated", BossFactAddressKind.Hardmode, 0, "wof.png"),
        new("boss:queen-slime", "Queen Slime", "boss:queen-slime:defeated", BossFactAddressKind.BossFlagBlock, 21, "queen-slime.png"),
        new(Destroyer, "Destroyer", "boss:destroyer:defeated", BossFactAddressKind.BossFlagBlock, 29, "destroyer.png"),
        new(Twins, "The Twins", "boss:twins:defeated", BossFactAddressKind.BossFlagBlock, 30, "twins.png"),
        new(SkeletronPrime, "Skeletron Prime", "boss:skeletron-prime:defeated", BossFactAddressKind.BossFlagBlock, 31, "prime.png"),
        new(Plantera, "Plantera", "boss:plantera:defeated", BossFactAddressKind.BossFlagBlock, 5, "plantera.png"),
        new(Golem, "Golem", "boss:golem:defeated", BossFactAddressKind.BossFlagBlock, 6, "golem.png"),
        new("boss:duke-fishron", "Duke Fishron", "boss:duke-fishron:defeated", BossFactAddressKind.BossFlagBlock, 8, "duke-fishron.png"),
        new("boss:empress-of-light", "Empress of Light", "boss:empress-of-light:defeated", BossFactAddressKind.BossFlagBlock, 20, "empress-of-light.png"),
        new(LunaticCultist, "Lunatic Cultist", "boss:lunatic-cultist:defeated", BossFactAddressKind.BossFlagBlock, 14, "cultist.png"),
        new(MoonLord, "Moon Lord", "boss:moon-lord:defeated", BossFactAddressKind.BossFlagBlock, 15, "moonlord.png")
    ];

    public static IEnumerable<SplitTargetDefinition> Targets
    {
        get
        {
            foreach (SplitTargetDefinition boss in BossTargetsById.Values)
            {
                yield return boss;
            }

            foreach (TerrariaItemDefinition item in TerrariaItemCatalog.Items.Where(item => !TerrariaItemCatalog.IsDeprecated(item.Id)))
            {
                yield return CreateItemTarget(item.Id);
            }

            foreach (TerrariaNpcDefinition npc in TerrariaNpcCatalog.Items)
            {
                yield return CreateNpcTarget(npc.Id);
            }

            foreach (TerrariaBiomeDefinition biome in TerrariaBiomeCatalog.Items)
            {
                yield return CreateBiomeTarget(biome.Id);
            }
        }
    }

    public static bool TryGetReferenceIconFileName(string targetId, out string fileName)
    {
        fileName = string.Empty;
        if (BossByTargetId.TryGetValue(targetId, out BossFactDescriptor? boss))
        {
            fileName = boss.IconFileName;
            return true;
        }

        if (TryParseItemTargetId(targetId, out int itemId))
        {
            fileName = $"Item_{itemId}.png";
            return true;
        }

        if (TryParseNpcTargetId(targetId, out int npcId))
        {
            fileName = CreateNpcIconFileName(npcId);
            return true;
        }

        if (TryParseBiomeTargetId(targetId, out string? biomeId) &&
            TerrariaBiomeCatalog.ById.TryGetValue(biomeId, out TerrariaBiomeDefinition? biome))
        {
            fileName = biome.IconFileName;
            return true;
        }

        return false;
    }

    public static IReadOnlyList<SplitDefinition> Build(AppSettings settings)
    {
        return settings.Route.SplitRoute
            .Where(entry => entry.Enabled)
            .Select(BuildSplit)
            .Where(definition => definition is not null)
            .Cast<SplitDefinition>()
            .ToList();
    }

    public static List<SplitRouteEntry> CreateDefaultRoute()
    {
        return
        [
            CreateItemAnyRouteEntry("split:item-857", "金字塔", [857, 934]),
            CreateBossRouteEntry(Skeletron, "骷髅王"),
            CreateBossRouteEntry(WallOfFlesh, "血肉墙"),
            CreateItemAnyRouteEntry("split:item-525", "二级砧", [525, 1220], isAttached: true),
            new SplitRouteEntry
            {
                Id = "split:boss-destroyer",
                DisplayName = "三王",
                Enabled = true,
                Condition = SplitCondition.All(
                [
                    CreateBossFactCondition(Destroyer),
                    CreateBossFactCondition(Twins),
                    CreateBossFactCondition(SkeletronPrime)
                ]),
                IconTargetIds = [Destroyer, Twins, SkeletronPrime]
            },
            CreateBossRouteEntry(Plantera, "世纪之花"),
            CreateBossRouteEntry(Golem, "石巨人"),
            CreateBossRouteEntry(LunaticCultist, "拜月教邪教徒"),
            CreateBossRouteEntry(MoonLord, "月亮领主")
        ];
    }

    public static bool TryGetTarget(string id, out SplitTargetDefinition target)
    {
        target = null!;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if (BossTargetsById.TryGetValue(id, out target!))
        {
            return true;
        }

        if (TryParseItemTargetId(id, out int itemId))
        {
            target = CreateItemTarget(itemId);
            return true;
        }

        if (TryParseNpcTargetId(id, out int npcId))
        {
            target = CreateNpcTarget(npcId);
            return true;
        }

        if (TryParseBiomeTargetId(id, out string? biomeId))
        {
            target = CreateBiomeTarget(biomeId);
            return true;
        }

        return false;
    }

    public static bool IsKnownTargetId(string id)
    {
        return !string.IsNullOrWhiteSpace(id) &&
            (BossTargetsById.ContainsKey(id) ||
                TryParseItemTargetId(id, out _) ||
                TryParseNpcTargetId(id, out _) ||
                TryParseBiomeTargetId(id, out _));
    }

    public static bool TryGetTargetByFactKey(string factKey, out SplitTargetDefinition target)
    {
        target = null!;
        if (BossFacts.FirstOrDefault(item => string.Equals(item.FactKey, factKey, StringComparison.OrdinalIgnoreCase))
            is BossFactDescriptor boss)
        {
            return TryGetTarget(boss.TargetId, out target);
        }

        if (TryParseItemFactKey(factKey, out int itemId))
        {
            target = CreateItemTarget(itemId);
            return true;
        }

        if (TryParseNpcPresentFactKey(factKey, out int npcId))
        {
            target = CreateNpcTarget(npcId);
            return true;
        }

        if (TryParseBiomeActiveFactKey(factKey, out string? biomeId))
        {
            target = CreateBiomeTarget(biomeId);
            return true;
        }

        return false;
    }

    public static bool TryGetBossFact(string targetId, out BossFactDescriptor descriptor)
    {
        return BossByTargetId.TryGetValue(targetId, out descriptor!);
    }

    public static bool IsMoonLordSplit(SplitDefinition definition)
    {
        return definition.ContainsTarget(MoonLord);
    }

    public static string CreateItemTargetId(int itemId)
    {
        return $"item:{itemId}";
    }

    public static string CreateNpcTargetId(int npcId)
    {
        return $"npc:{npcId}";
    }

    public static string CreateBiomeTargetId(string biomeId)
    {
        return $"biome:{biomeId}";
    }

    public static string CreateItemFactKey(int itemId)
    {
        return SplitFactKeys.CreateItemFactKey(itemId);
    }

    public static string CreateItemEverOwnedFactKey(int itemId)
    {
        return SplitFactKeys.CreateItemEverOwnedFactKey(itemId);
    }

    public static string CreateNpcPresentFactKey(int npcId)
    {
        return $"npc:{npcId}:present";
    }

    public static string CreateBiomeActiveFactKey(string biomeId)
    {
        return $"biome:{biomeId}:active";
    }

    public static bool TryParseItemTargetId(string id, out int itemId)
    {
        itemId = 0;
        if (!id.StartsWith("item:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string suffix = id["item:".Length..];
        return int.TryParse(suffix, out itemId) && SplitFactKeys.IsSupportedItemId(itemId);
    }

    public static bool TryParseNpcTargetId(string id, out int npcId)
    {
        npcId = 0;
        if (!id.StartsWith("npc:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string suffix = id["npc:".Length..];
        return int.TryParse(suffix, out npcId) && TerrariaNpcCatalog.ById.ContainsKey(npcId);
    }

    public static bool TryParseBiomeTargetId(string id, out string biomeId)
    {
        biomeId = string.Empty;
        if (!id.StartsWith("biome:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string suffix = id["biome:".Length..].Trim();
        if (!TerrariaBiomeCatalog.ById.ContainsKey(suffix))
        {
            return false;
        }

        biomeId = suffix;
        return true;
    }

    public static SplitCondition CreateBossFactCondition(string bossTargetId)
    {
        return TryGetBossFact(bossTargetId, out BossFactDescriptor descriptor)
            ? SplitCondition.Fact(descriptor.FactKey)
            : SplitCondition.Fact(string.Empty);
    }

    public static SplitCondition CreateItemEverOwnedCondition(int itemId, int quantity)
    {
        return SplitCondition.Fact(
            CreateItemEverOwnedFactKey(itemId),
            SplitFactComparison.AtLeast,
            Math.Max(1, quantity));
    }

    public static SplitCondition CreateNpcPresentCondition(int npcId)
    {
        return SplitCondition.Fact(CreateNpcPresentFactKey(npcId));
    }

    public static SplitCondition CreateBiomeActiveCondition(string biomeId)
    {
        return SplitCondition.Fact(CreateBiomeActiveFactKey(biomeId));
    }

    private static SplitDefinition? BuildSplit(SplitRouteEntry entry)
    {
        SplitCondition condition = (entry.Condition ?? SplitCondition.All([])).Clone();
        condition.Normalize();

        string id = string.IsNullOrWhiteSpace(entry.Id)
            ? CreateStableSplitId(entry)
            : entry.Id.Trim();
        string displayName = string.IsNullOrWhiteSpace(entry.DisplayName)
            ? id
            : entry.DisplayName.Trim();
        string[] targetIds = InferTargetIds(condition)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (targetIds.Length == 0)
        {
            targetIds = entry.IconTargetIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        SplitIconData iconData = CreateIconData(entry, id, targetIds);
        return new SplitDefinition(
            id,
            displayName,
            condition,
            iconData.FileNames,
            iconData.Keys,
            targetIds,
            entry.IsAttached)
        {
            IconLightingConditions = iconData.LightingConditions
        };
    }

    private static SplitIconData CreateIconData(SplitRouteEntry entry, string splitId, IReadOnlyList<string> targetIds)
    {
        SplitIconOverride iconOverride = entry.IconOverride ?? new SplitIconOverride();
        string source = SplitIconOverrideSource.Normalize(iconOverride.Source);
        SplitCondition lightingCondition = (entry.Condition ?? SplitCondition.All([])).Clone();
        lightingCondition.Normalize();
        SplitCondition[] lightingConditions = [lightingCondition];

        if (source == SplitIconOverrideSource.Target &&
            targetIds.Contains(iconOverride.TargetId, StringComparer.OrdinalIgnoreCase) &&
            TryGetTargetIconData(
                iconOverride.TargetId,
                out string overrideTargetId,
                out string overrideIconFileName))
        {
            return new SplitIconData(
                [overrideIconFileName],
                [overrideTargetId],
                lightingConditions);
        }

        if (source == SplitIconOverrideSource.CustomFile &&
            !string.IsNullOrWhiteSpace(iconOverride.FilePath))
        {
            return new SplitIconData(
                [iconOverride.FilePath.Trim()],
                [$"custom-icon:{splitId}"],
                lightingConditions);
        }

        IReadOnlyDictionary<string, string> allIconFilePaths = iconOverride.AllIconFilePaths ??
            new Dictionary<string, string>();
        string[] iconFileNames = targetIds
            .Select(id => allIconFilePaths.TryGetValue(id, out string? filePath) &&
                !string.IsNullOrWhiteSpace(filePath)
                    ? filePath.Trim()
                    : TryGetTargetIconData(id, out _, out string iconFileName) ? iconFileName : "target.png")
            .ToArray();
        return new SplitIconData(iconFileNames, targetIds.ToArray(), []);
    }

    private static bool TryGetTargetIconData(
        string targetId,
        out string normalizedTargetId,
        out string iconFileName)
    {
        if (TryParseItemTargetId(targetId, out int itemId))
        {
            normalizedTargetId = CreateItemTargetId(itemId);
            iconFileName = $"item-{itemId}.png";
            return true;
        }

        if (TryGetTarget(targetId, out SplitTargetDefinition target))
        {
            normalizedTargetId = target.Id;
            iconFileName = target.IconFileName;
            return true;
        }

        normalizedTargetId = string.Empty;
        iconFileName = string.Empty;
        return false;
    }

    private static SplitRouteEntry CreateBossRouteEntry(string bossTargetId, string? displayName = null)
    {
        BossFactDescriptor boss = BossByTargetId[bossTargetId];
        return new SplitRouteEntry
        {
            Id = $"split:{boss.TargetId.Replace(':', '-')}",
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? boss.DisplayName : displayName,
            Enabled = true,
            Condition = SplitCondition.All([SplitCondition.Fact(boss.FactKey)]),
            IconTargetIds = [boss.TargetId]
        };
    }

    private static SplitRouteEntry CreateItemAnyRouteEntry(
        string id,
        string displayName,
        IReadOnlyList<int> itemIds,
        bool isAttached = false)
    {
        return new SplitRouteEntry
        {
            Id = id,
            DisplayName = displayName,
            Enabled = true,
            Condition = SplitCondition.Any(itemIds.Select(itemId => CreateItemEverOwnedCondition(itemId, 1))),
            IconTargetIds = itemIds.Select(CreateItemTargetId).ToList(),
            IsAttached = isAttached
        };
    }

    private static SplitTargetDefinition CreateItemTarget(int itemId)
    {
        string id = CreateItemTargetId(itemId);
        return new SplitTargetDefinition(
            id,
            SplitTargetKind.Item,
            TerrariaItemCatalog.ById.TryGetValue(itemId, out TerrariaItemDefinition item)
                ? item.DisplayName
                : $"Item {itemId}",
            CreateItemFactKey(itemId),
            $"item-{itemId}.png");
    }

    private static SplitTargetDefinition CreateNpcTarget(int npcId)
    {
        string id = CreateNpcTargetId(npcId);
        return new SplitTargetDefinition(
            id,
            SplitTargetKind.Npc,
            TerrariaNpcCatalog.ById.TryGetValue(npcId, out TerrariaNpcDefinition? npc) && npc is not null ? npc.DisplayName : $"NPC {npcId}",
            CreateNpcPresentFactKey(npcId),
            CreateNpcIconFileName(npcId));
    }

    private static SplitTargetDefinition CreateBiomeTarget(string biomeId)
    {
        string id = CreateBiomeTargetId(biomeId);
        return new SplitTargetDefinition(
            id,
            SplitTargetKind.Biome,
            TerrariaBiomeCatalog.ById.TryGetValue(biomeId, out TerrariaBiomeDefinition? biome) && biome is not null
                ? biome.DisplayName
                : $"Biome {biomeId}",
            CreateBiomeActiveFactKey(biomeId),
            TerrariaBiomeCatalog.ById.TryGetValue(biomeId, out TerrariaBiomeDefinition? iconBiome) && iconBiome is not null
                ? iconBiome.IconFileName
                : "target.png");
    }

    private static string CreateNpcIconFileName(int npcId)
    {
        return TerrariaNpcCatalog.ById.TryGetValue(npcId, out TerrariaNpcDefinition? npc) &&
            npc.DefaultHeadIndex > 0
            ? $"NPC_Head_{npc.DefaultHeadIndex}.png"
            : $"NPC_{npcId}.png";
    }

    public static IEnumerable<string> InferTargetIds(SplitCondition condition)
    {
        foreach (string factKey in condition.GetFactKeys())
        {
            BossFactDescriptor? boss = BossFacts.FirstOrDefault(item =>
                string.Equals(item.FactKey, factKey, StringComparison.OrdinalIgnoreCase));
            if (boss is not null)
            {
                yield return boss.TargetId;
                continue;
            }

            if (TryParseItemFactKey(factKey, out int itemId))
            {
                yield return CreateItemTargetId(itemId);
                continue;
            }

            if (TryParseNpcPresentFactKey(factKey, out int npcId))
            {
                yield return CreateNpcTargetId(npcId);
                continue;
            }

            if (TryParseBiomeActiveFactKey(factKey, out string? biomeId))
            {
                yield return CreateBiomeTargetId(biomeId);
            }
        }
    }

    public static bool TryParseItemFactKey(string factKey, out int itemId)
    {
        return SplitFactKeys.TryParseItemFactKey(factKey, out itemId);
    }

    public static bool TryParseNpcPresentFactKey(string factKey, out int npcId)
    {
        npcId = 0;
        if (!factKey.StartsWith("npc:", StringComparison.OrdinalIgnoreCase) ||
            !factKey.EndsWith(":present", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string id = factKey["npc:".Length..^":present".Length];
        return int.TryParse(id, out npcId) && TerrariaNpcCatalog.ById.ContainsKey(npcId);
    }

    public static bool TryParseBiomeActiveFactKey(string factKey, out string biomeId)
    {
        biomeId = string.Empty;
        if (!factKey.StartsWith("biome:", StringComparison.OrdinalIgnoreCase) ||
            !factKey.EndsWith(":active", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string id = factKey["biome:".Length..^":active".Length];
        if (!TerrariaBiomeCatalog.ById.ContainsKey(id))
        {
            return false;
        }

        biomeId = id;
        return true;
    }

    public static bool TryParseItemOwnedCountFactKey(string factKey, out int itemId)
    {
        return SplitFactKeys.TryParseItemOwnedCountFactKey(factKey, out itemId);
    }

    public static bool TryParseItemEverOwnedFactKey(string factKey, out int itemId)
    {
        return SplitFactKeys.TryParseItemEverOwnedFactKey(factKey, out itemId);
    }

    private static string CreateStableSplitId(SplitRouteEntry entry)
    {
        string joinedFacts = string.Join("+", entry.Condition.GetFactKeys().OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
        if (joinedFacts.Length == 0)
        {
            return "split:unnamed";
        }

        string normalized = new(joinedFacts
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-')
            .ToArray());
        return "split:" + normalized.Trim('-');
    }

    private sealed record SplitIconData(
        IReadOnlyList<string> FileNames,
        IReadOnlyList<string> Keys,
        IReadOnlyList<SplitCondition> LightingConditions);
}
