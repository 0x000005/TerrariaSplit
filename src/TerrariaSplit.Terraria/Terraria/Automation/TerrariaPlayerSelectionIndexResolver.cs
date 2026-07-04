namespace TerrariaSplit.Terraria.Automation;

public readonly record struct TerrariaPlayerSelectionEntry(
    string FileName,
    string DisplayName,
    bool IsFavorite,
    DateTime LastWriteTimeUtc);

public static class TerrariaPlayerSelectionIndexResolver
{
    public static int ResolveCreatedPlayerIndex(
        TerrariaMenuProfile profile,
        IReadOnlyList<TerrariaPlayerSelectionEntry> players,
        string? createdFileName,
        int fallbackIndex)
    {
        if (players.Count == 0 || string.IsNullOrWhiteSpace(createdFileName))
        {
            return ClampFallback(fallbackIndex, players.Count);
        }

        IEnumerable<TerrariaPlayerSelectionEntry> ordered = profile.Kind == TerrariaMenuProfileKind.Legacy1449
            ? SortLegacy1449(players)
            : SortModern1456(players);

        int index = 0;
        foreach (TerrariaPlayerSelectionEntry player in ordered)
        {
            if (string.Equals(player.FileName, createdFileName, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }

            index++;
        }

        return ClampFallback(fallbackIndex, players.Count);
    }

    private static IOrderedEnumerable<TerrariaPlayerSelectionEntry> SortLegacy1449(
        IEnumerable<TerrariaPlayerSelectionEntry> players)
    {
        return players
            .OrderByDescending(static player => player.IsFavorite)
            .ThenBy(static player => player.DisplayName, StringComparer.CurrentCulture)
            .ThenBy(static player => player.FileName, StringComparer.CurrentCulture);
    }

    private static IOrderedEnumerable<TerrariaPlayerSelectionEntry> SortModern1456(
        IEnumerable<TerrariaPlayerSelectionEntry> players)
    {
        return players
            .OrderByDescending(static player => player.IsFavorite)
            .ThenByDescending(static player => player.LastWriteTimeUtc)
            .ThenBy(static player => player.DisplayName, StringComparer.CurrentCulture)
            .ThenBy(static player => player.FileName, StringComparer.CurrentCulture);
    }

    private static int ClampFallback(int fallbackIndex, int playerCount)
    {
        if (playerCount <= 0)
        {
            return Math.Max(0, fallbackIndex);
        }

        return Math.Clamp(fallbackIndex, 0, playerCount - 1);
    }
}
