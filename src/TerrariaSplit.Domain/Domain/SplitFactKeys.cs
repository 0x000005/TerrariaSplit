namespace TerrariaSplit.Domain;

internal static class SplitFactKeys
{
    public const int MaxItemId = 6146;

    public static string CreateItemFactKey(int itemId)
    {
        return $"item:{itemId}:owned-count";
    }

    public static string CreateItemEverOwnedFactKey(int itemId)
    {
        return $"item:{itemId}:ever-owned-count";
    }

    public static bool TryParseItemFactKey(string factKey, out int itemId)
    {
        return TryParseItemOwnedCountFactKey(factKey, out itemId) ||
            TryParseItemEverOwnedFactKey(factKey, out itemId);
    }

    public static bool TryParseItemOwnedCountFactKey(string factKey, out int itemId)
    {
        itemId = 0;
        if (!factKey.StartsWith("item:", StringComparison.OrdinalIgnoreCase) ||
            !factKey.EndsWith(":owned-count", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string id = factKey["item:".Length..^":owned-count".Length];
        return int.TryParse(id, out itemId) && itemId is >= 1 and <= MaxItemId;
    }

    public static bool TryParseItemEverOwnedFactKey(string factKey, out int itemId)
    {
        itemId = 0;
        if (!factKey.StartsWith("item:", StringComparison.OrdinalIgnoreCase) ||
            !factKey.EndsWith(":ever-owned-count", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string id = factKey["item:".Length..^":ever-owned-count".Length];
        return int.TryParse(id, out itemId) && itemId is >= 1 and <= MaxItemId;
    }
}
