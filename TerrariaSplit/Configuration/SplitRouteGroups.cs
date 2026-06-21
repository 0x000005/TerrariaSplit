namespace TerrariaSplit.Configuration;

internal static class SplitRouteGroups
{
    public static List<RouteGroup> Build(AppSettings settings)
    {
        return settings.Route.SplitRoute
            .Where(entry => entry.Enabled)
            .Where(entry => !entry.IsAttached)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
            .Select(entry =>
            {
                string id = entry.Id.Trim();
                return new RouteGroup(id, GetEntryDisplayName(entry), [entry]);
            })
            .ToList();
    }

    public static bool TryGetGroupKey(AppSettings settings, string splitId, out string groupKey)
    {
        foreach (RouteGroup group in Build(settings))
        {
            if (group.Entries.Any(entry => string.Equals(entry.Id, splitId, StringComparison.OrdinalIgnoreCase)))
            {
                groupKey = group.Key;
                return true;
            }
        }

        groupKey = splitId;
        return false;
    }

    public static int GetGroupRowOffset(RouteGroup group, string splitId)
    {
        for (int i = 0; i < group.Entries.Count; i++)
        {
            if (string.Equals(group.Entries[i].Id, splitId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }

    private static string GetEntryDisplayName(SplitRouteEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.DisplayName)
            ? entry.Id
            : entry.DisplayName;
    }

    public static string GetGroupDisplayName(RouteGroup group, AppSettings settings)
    {
        return string.IsNullOrWhiteSpace(group.DisplayName)
            ? group.Key
            : group.DisplayName;
    }
}

internal sealed record RouteGroup(string Key, string DisplayName, IReadOnlyList<SplitRouteEntry> Entries);
