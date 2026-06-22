using System.Globalization;

namespace TerrariaSplit.Configuration;

public static class SplitTargetDisplayNames
{
    private static readonly IReadOnlyDictionary<string, string> BossChineseNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["boss:king-slime"] = "史莱姆王",
            ["boss:eye-of-cthulhu"] = "克苏鲁之眼",
            ["boss:eater-of-worlds"] = "世界吞噬怪",
            ["boss:brain-of-cthulhu"] = "克苏鲁之脑",
            ["boss:queen-bee"] = "蜂王",
            [SplitCatalog.Skeletron] = "骷髅王",
            ["boss:deerclops"] = "独眼巨鹿",
            [SplitCatalog.WallOfFlesh] = "血肉墙",
            ["boss:queen-slime"] = "史莱姆皇后",
            [SplitCatalog.Destroyer] = "毁灭者",
            [SplitCatalog.Twins] = "双子魔眼",
            [SplitCatalog.SkeletronPrime] = "机械骷髅王",
            [SplitCatalog.Plantera] = "世纪之花",
            [SplitCatalog.Golem] = "石巨人",
            ["boss:duke-fishron"] = "猪龙鱼公爵",
            ["boss:empress-of-light"] = "光之女皇",
            [SplitCatalog.LunaticCultist] = "拜月教邪教徒",
            [SplitCatalog.MoonLord] = "月亮领主"
        };

    public static string GetTargetName(SplitTargetDefinition target, string? language)
    {
        if (target.Kind == SplitTargetKind.Item &&
            SplitCatalog.TryParseItemTargetId(target.Id, out int itemId) &&
            TerrariaItemCatalog.ById.ContainsKey(itemId))
        {
            TerrariaItemDefinition item = TerrariaItemCatalog.ById[itemId];
            return LanguageNames.IsChinese(language) && !string.IsNullOrWhiteSpace(item.ChineseName)
                ? item.ChineseName
                : item.DisplayName;
        }

        if (target.Kind == SplitTargetKind.Npc &&
            SplitCatalog.TryParseNpcTargetId(target.Id, out int npcId) &&
            TerrariaNpcCatalog.ById.TryGetValue(npcId, out TerrariaNpcDefinition? npc) &&
            npc is not null)
        {
            return LanguageNames.IsChinese(language) && !string.IsNullOrWhiteSpace(npc.ChineseName)
                ? npc.ChineseName
                : npc.DisplayName;
        }

        if (target.Kind == SplitTargetKind.Biome &&
            SplitCatalog.TryParseBiomeTargetId(target.Id, out string? biomeId) &&
            TerrariaBiomeCatalog.ById.TryGetValue(biomeId, out TerrariaBiomeDefinition? biome) &&
            biome is not null)
        {
            return LanguageNames.IsChinese(language) && !string.IsNullOrWhiteSpace(biome.ChineseName)
                ? biome.ChineseName
                : biome.DisplayName;
        }

        return LanguageNames.IsChinese(language) && BossChineseNames.TryGetValue(target.Id, out string? chineseName)
            ? chineseName
            : target.DisplayName;
    }

    public static string FormatFact(SplitCondition condition, string? language)
    {
        if (!SplitCatalog.TryGetTargetByFactKey(condition.FactKey, out SplitTargetDefinition target))
        {
            return condition.FactKey;
        }

        string comparison = SplitFactComparison.Normalize(condition.Comparison);
        string targetName = GetTargetName(target, language);
        return comparison switch
        {
            SplitFactComparison.AtLeast => $"{targetName} >= {Math.Max(1, condition.Value).ToString(CultureInfo.InvariantCulture)}",
            SplitFactComparison.Equal => $"{targetName} = {condition.Value.ToString(CultureInfo.InvariantCulture)}",
            SplitFactComparison.IsFalse => LanguageNames.IsChinese(language) ? $"{targetName} 为假" : $"{targetName} is false",
            _ => targetName
        };
    }

    public static IEnumerable<string> GetSearchNames(SplitTargetDefinition target)
    {
        yield return target.DisplayName;
        yield return target.Id;

        if (BossChineseNames.TryGetValue(target.Id, out string? chineseName))
        {
            yield return chineseName;
        }

        if (target.Kind == SplitTargetKind.Item &&
            SplitCatalog.TryParseItemTargetId(target.Id, out int itemId) &&
            TerrariaItemCatalog.ById.ContainsKey(itemId))
        {
            TerrariaItemDefinition item = TerrariaItemCatalog.ById[itemId];
            yield return item.DisplayName;
            yield return item.ChineseName;
            yield return item.InternalName;
            yield return item.Id.ToString(CultureInfo.InvariantCulture);
        }

        if (target.Kind == SplitTargetKind.Npc &&
            SplitCatalog.TryParseNpcTargetId(target.Id, out int npcId) &&
            TerrariaNpcCatalog.ById.TryGetValue(npcId, out TerrariaNpcDefinition? npc) &&
            npc is not null)
        {
            yield return npc.DisplayName;
            yield return npc.ChineseName;
            yield return npc.InternalName;
            yield return npc.Id.ToString(CultureInfo.InvariantCulture);
        }

        if (target.Kind == SplitTargetKind.Biome &&
            SplitCatalog.TryParseBiomeTargetId(target.Id, out string? biomeId) &&
            TerrariaBiomeCatalog.ById.TryGetValue(biomeId, out TerrariaBiomeDefinition? biome) &&
            biome is not null)
        {
            yield return biome.Id;
            yield return biome.DisplayName;
            yield return biome.ChineseName;
        }
    }
}
