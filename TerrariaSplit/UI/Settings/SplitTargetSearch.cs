namespace TerrariaSplit.UI.Settings;

internal static class SplitTargetSearch
{
    public static IEnumerable<SplitTargetDefinition> QueryTargets(string query, string targetKind)
    {
        if (string.Equals(targetKind, SplitTargetKind.Boss, StringComparison.OrdinalIgnoreCase))
        {
            foreach (BossFactDescriptor boss in SplitCatalog.BossFacts)
            {
                var target = new SplitTargetDefinition(
                    boss.TargetId,
                    SplitTargetKind.Boss,
                    boss.DisplayName,
                    boss.FactKey,
                    boss.IconFileName);
                if (MatchesTarget(query, target))
                {
                    yield return target;
                }
            }

            yield break;
        }

        if (string.Equals(targetKind, SplitTargetKind.Item, StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(query, out int itemId) &&
                SplitCatalog.TryGetTarget(SplitCatalog.CreateItemTargetId(itemId), out SplitTargetDefinition exactItemTarget))
            {
                yield return exactItemTarget;
                yield break;
            }

            foreach (TerrariaItemDefinition item in TerrariaItemCatalog.Items
                .Where(item => SplitCatalog.TryGetTarget(SplitCatalog.CreateItemTargetId(item.Id), out SplitTargetDefinition target) &&
                    MatchesTarget(query, target)))
            {
                if (SplitCatalog.TryGetTarget(SplitCatalog.CreateItemTargetId(item.Id), out SplitTargetDefinition target))
                {
                    yield return target;
                }
            }

            yield break;
        }

        if (string.Equals(targetKind, SplitTargetKind.Npc, StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(query, out int npcId) &&
                SplitCatalog.TryGetTarget(SplitCatalog.CreateNpcTargetId(npcId), out SplitTargetDefinition exactNpcTarget))
            {
                yield return exactNpcTarget;
                yield break;
            }

            foreach (TerrariaNpcDefinition npc in TerrariaNpcCatalog.Items)
            {
                if (SplitCatalog.TryGetTarget(SplitCatalog.CreateNpcTargetId(npc.Id), out SplitTargetDefinition target) &&
                    MatchesTarget(query, target))
                {
                    yield return target;
                }
            }

            yield break;
        }

        if (string.Equals(targetKind, SplitTargetKind.Biome, StringComparison.OrdinalIgnoreCase))
        {
            foreach (TerrariaBiomeDefinition biome in TerrariaBiomeCatalog.Items)
            {
                if (SplitCatalog.TryGetTarget(SplitCatalog.CreateBiomeTargetId(biome.Id), out SplitTargetDefinition target) &&
                    MatchesTarget(query, target))
                {
                    yield return target;
                }
            }
        }
    }

    private static bool MatchesTarget(string query, SplitTargetDefinition target)
    {
        return SplitTargetDisplayNames.GetSearchNames(target)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Any(value => Matches(query, value) || MatchesNormalized(query, value));
    }

    private static bool Matches(string query, string value)
    {
        return string.IsNullOrWhiteSpace(query) ||
            value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesNormalized(string query, string value)
    {
        string normalizedQuery = NormalizeSearchText(query);
        return normalizedQuery.Length > 0 &&
            NormalizeSearchText(value).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSearchText(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}
