namespace TerrariaSplit;

internal static class BossRouteGroups
{
    public static List<RouteGroup> Build(AppSettings settings)
    {
        IReadOnlyDictionary<string, BossUnitDefinition> units = BossSplitDefinitions.Units.ToDictionary(
            unit => unit.Id,
            StringComparer.OrdinalIgnoreCase);

        return settings.Route
            .Select((entry, index) => new IndexedRouteEntry(entry, index))
            .Where(entry => units.ContainsKey(entry.Entry.BossId))
            .OrderBy(entry => entry.Entry.Segment)
            .ThenBy(entry => entry.Index)
            .GroupBy(entry => Math.Max(1, (int)Math.Truncate(entry.Entry.Segment)))
            .Where(group => group.Any(entry => entry.Entry.Enabled))
            .Select(group =>
            {
                List<IndexedRouteEntry> entries = group
                    .Where(entry => entry.Entry.Enabled)
                    .ToList();
                return new RouteGroup(
                    GetGroupKey(entries),
                    entries.Select(entry => entry.Entry).ToList());
            })
            .ToList();
    }

    public static string GetGroupDisplayName(RouteGroup group, AppSettings settings)
    {
        IReadOnlyDictionary<string, BossUnitDefinition> units = BossSplitDefinitions.Units.ToDictionary(
            unit => unit.Id,
            StringComparer.OrdinalIgnoreCase);

        return string.Join(" / ", group.Entries
            .Where(entry => units.ContainsKey(entry.BossId))
            .Select(entry => Localizer.Get(units[entry.BossId].DisplayName, settings)));
    }

    private static string GetGroupKey(IReadOnlyList<IndexedRouteEntry> entries)
    {
        return string.Join("+", entries.Select(entry => entry.Entry.BossId));
    }

    private sealed record IndexedRouteEntry(BossRouteEntry Entry, int Index);
}

internal sealed record RouteGroup(string Key, IReadOnlyList<BossRouteEntry> Entries);
