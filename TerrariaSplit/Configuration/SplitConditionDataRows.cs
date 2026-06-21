namespace TerrariaSplit.Configuration;

internal sealed record SplitConditionDataRow(
    string Key,
    string DisplayName,
    string SplitId,
    string SplitDisplayName,
    SplitCondition Condition,
    int RouteIndex,
    int ConditionIndex,
    bool IsAttached = false);

internal static class SplitConditionDataRows
{
    public static IReadOnlyList<SplitConditionDataRow> Build(AppSettings settings)
    {
        return Build(settings.SplitRoute, settings.Language);
    }

    public static IReadOnlyList<SplitConditionDataRow> Build(IEnumerable<SplitRouteEntry> route, string? language = null)
    {
        var rows = new List<SplitConditionDataRow>();
        var seenKeys = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int routeIndex = 0;
        foreach (SplitRouteEntry entry in route)
        {
            if (!entry.Enabled || string.IsNullOrWhiteSpace(entry.Id))
            {
                continue;
            }

            string splitId = entry.Id.Trim();
            string splitDisplayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? splitId
                : entry.DisplayName.Trim();
            SplitCondition condition = entry.Condition ?? SplitCondition.All([]);
            if (entry.IsAttached)
            {
                rows.Add(new SplitConditionDataRow(
                    CreateCompletedSplitKey(splitId),
                    splitDisplayName,
                    splitId,
                    splitDisplayName,
                    condition.Clone(),
                    routeIndex,
                    ConditionIndex: -1,
                    IsAttached: true));
                routeIndex++;
                continue;
            }

            List<SplitCondition> facts = (entry.Condition ?? SplitCondition.All([]))
                .ToFlatGroup()
                .GetFactConditions()
                .ToList();

            for (int conditionIndex = 0; conditionIndex < facts.Count; conditionIndex++)
            {
                SplitCondition fact = facts[conditionIndex];
                string baseKey = CreateBaseKey(splitId, fact);
                int occurrence = seenKeys.TryGetValue(baseKey, out int count) ? count + 1 : 1;
                seenKeys[baseKey] = occurrence;

                rows.Add(new SplitConditionDataRow(
                    occurrence == 1 ? baseKey : $"{baseKey}:{occurrence}",
                    CreateDisplayName(splitDisplayName, fact, facts.Count, language),
                    splitId,
                    splitDisplayName,
                    fact.Clone(),
                    routeIndex,
                    conditionIndex));
            }

            routeIndex++;
        }

        return rows;
    }

    public static bool TryGetSplitTime(
        AppSettings settings,
        IReadOnlyDictionary<string, string> values,
        SplitDefinition definition,
        out TimeSpan split)
    {
        return TryGetSplitTime(
            values,
            Build(settings).Where(row => string.Equals(row.SplitId, definition.Id, StringComparison.OrdinalIgnoreCase)),
            definition.Condition,
            definition.IsAttached,
            out split);
    }

    public static bool TryGetSplitTime(
        AppSettings settings,
        IReadOnlyDictionary<string, string> values,
        string splitId,
        out TimeSpan split)
    {
        split = TimeSpan.Zero;
        SplitRouteEntry? entry = settings.SplitRoute.FirstOrDefault(routeEntry =>
            routeEntry.Enabled &&
            string.Equals(routeEntry.Id, splitId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return false;
        }

        return TryGetSplitTime(
            values,
            Build(settings).Where(row => string.Equals(row.SplitId, splitId, StringComparison.OrdinalIgnoreCase)),
            entry.Condition,
            entry.IsAttached,
            out split);
    }

    public static IEnumerable<SplitConditionDataRow> ForSplit(AppSettings settings, string splitId)
    {
        return Build(settings).Where(row => string.Equals(row.SplitId, splitId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetSplitTime(
        IReadOnlyDictionary<string, string> values,
        IEnumerable<SplitConditionDataRow> rows,
        SplitCondition condition,
        bool isAttached,
        out TimeSpan split)
    {
        split = TimeSpan.Zero;
        List<TimeSpan> parsed = rows
            .Select(row => TryGetTime(values, row.Key, out TimeSpan value) ? value : (TimeSpan?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();
        if (parsed.Count == 0)
        {
            return false;
        }

        if (isAttached)
        {
            split = parsed.Max();
            return true;
        }

        Dictionary<string, List<TimeSpan>> factTimes = rows
            .Select(row => (
                Key: CreateFactSignature(row.Condition),
                Time: TryGetTime(values, row.Key, out TimeSpan value) ? value : (TimeSpan?)null))
            .Where(item => item.Time.HasValue)
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Time!.Value).OrderBy(value => value).ToList(),
                StringComparer.OrdinalIgnoreCase);

        return TryGetConditionCompletionTime(condition ?? SplitCondition.All([]), factTimes, out split);
    }

    private static bool TryGetTime(
        IReadOnlyDictionary<string, string> values,
        string key,
        out TimeSpan split)
    {
        split = TimeSpan.Zero;
        return values.TryGetValue(key, out string? value) &&
            TimeText.TryParse(value, out split);
    }

    private static string CreateBaseKey(string splitId, SplitCondition fact)
    {
        string suffix = Sanitize(CreateFactSignature(fact));
        return $"condition:{splitId}:{suffix}";
    }

    private static string CreateFactSignature(SplitCondition fact)
    {
        string factKey = NormalizeItemFactKey(fact.FactKey);
        return $"{factKey}:{SplitFactComparison.Normalize(fact.Comparison)}:{Math.Max(1, fact.Value)}";
    }

    private static string CreateCompletedSplitKey(string splitId)
    {
        return $"condition:{splitId}:complete";
    }

    private static string NormalizeItemFactKey(string factKey)
    {
        return SplitCatalog.TryParseItemOwnedCountFactKey(factKey, out int itemId)
            ? SplitCatalog.CreateItemEverOwnedFactKey(itemId)
            : factKey;
    }

    private static string Sanitize(string value)
    {
        string normalized = new(value
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-')
            .ToArray());
        return normalized.Trim('-');
    }

    private static string CreateDisplayName(string splitDisplayName, SplitCondition fact, int conditionCount, string? language)
    {
        string factName = SplitTargetDisplayNames.FormatFact(fact, language);
        return conditionCount == 1 && string.Equals(splitDisplayName, factName, StringComparison.OrdinalIgnoreCase)
            ? factName
            : $"{splitDisplayName}：{factName}";
    }

    private static bool TryGetConditionCompletionTime(
        SplitCondition condition,
        IReadOnlyDictionary<string, List<TimeSpan>> factTimes,
        out TimeSpan time)
    {
        time = TimeSpan.Zero;
        string kind = SplitConditionKind.Normalize(condition.Kind);
        if (kind == SplitConditionKind.Fact)
        {
            return TryGetFactCompletionTime(condition, factTimes, out time);
        }

        List<TimeSpan> childTimes = new();
        IReadOnlyList<SplitCondition> children = condition.Children ?? [];
        foreach (SplitCondition child in children)
        {
            if (TryGetConditionCompletionTime(child, factTimes, out TimeSpan childTime))
            {
                childTimes.Add(childTime);
            }
        }

        if (kind == SplitConditionKind.All)
        {
            if (childTimes.Count != children.Count || childTimes.Count == 0)
            {
                return false;
            }

            time = childTimes.Max();
            return true;
        }

        int requiredCount = kind == SplitConditionKind.AtLeast
            ? Math.Max(1, condition.Value)
            : 1;
        if (childTimes.Count < requiredCount)
        {
            return false;
        }

        time = childTimes
            .OrderBy(value => value)
            .ElementAt(requiredCount - 1);
        return true;
    }

    private static bool TryGetFactCompletionTime(
        SplitCondition condition,
        IReadOnlyDictionary<string, List<TimeSpan>> factTimes,
        out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (!factTimes.TryGetValue(CreateFactSignature(condition), out List<TimeSpan>? times) ||
            times.Count == 0)
        {
            return false;
        }

        time = times[0];
        return true;
    }
}
