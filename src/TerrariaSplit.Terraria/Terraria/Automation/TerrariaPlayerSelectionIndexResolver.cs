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

        if (profile.Kind != TerrariaMenuProfileKind.Legacy1449)
        {
            TerrariaPlayerSelectionEntry? createdPlayer = players
                .FirstOrDefault(player => string.Equals(
                    player.FileName,
                    createdFileName,
                    StringComparison.OrdinalIgnoreCase));
            if (createdPlayer is { FileName: not null, IsFavorite: false })
            {
                // Terraria 1.4.5.7 orders by favorite and then PlayerFileData.LastPlayed.
                // A player saved by this creation flow is the newest non-favorite entry,
                // so its exact UI row is immediately after all favorite players. File
                // LastWriteTime is not a reliable substitute for the saved LastPlayed value.
                return players.Count(static player => player.IsFavorite);
            }
        }

        IEnumerable<TerrariaPlayerSelectionEntry> ordered = profile.Kind == TerrariaMenuProfileKind.Legacy1449
            ? SortLegacy1449(players)
            : SortModern1457(players);

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

    private static IOrderedEnumerable<TerrariaPlayerSelectionEntry> SortModern1457(
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
