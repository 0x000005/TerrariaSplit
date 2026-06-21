using System.Globalization;

namespace TerrariaSplit.Configuration;

internal static class SplitTargetTokenFormatter
{
    public static string Format(SplitTargetDefinition target)
    {
        if (target.Kind == SplitTargetKind.Item && SplitCatalog.TryParseItemTargetId(target.Id, out int itemId))
        {
            return $"Item:{itemId.ToString(CultureInfo.InvariantCulture)}";
        }

        if (target.Kind == SplitTargetKind.Npc && SplitCatalog.TryParseNpcTargetId(target.Id, out int npcId))
        {
            return $"NPC:{npcId.ToString(CultureInfo.InvariantCulture)}";
        }

        if (target.Kind == SplitTargetKind.Biome && SplitCatalog.TryParseBiomeTargetId(target.Id, out string biomeId))
        {
            return $"Biome:{ToPascalToken(biomeId)}";
        }

        return target.Id.StartsWith("boss:", StringComparison.OrdinalIgnoreCase)
            ? $"Boss:{target.Id["boss:".Length..]}"
            : target.Id;
    }

    public static string ToPascalToken(string value)
    {
        string[] parts = value.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Concat(parts.Select(part => part.Length == 0
            ? part
            : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));
    }
}
