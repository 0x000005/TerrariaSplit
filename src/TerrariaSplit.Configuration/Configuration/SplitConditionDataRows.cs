namespace TerrariaSplit.Configuration;

public sealed record SplitConditionDataRow(
    string Key,
    string DisplayName,
    string SplitId,
    string SplitDisplayName,
    SplitCondition Condition,
    int RouteIndex,
    int ConditionIndex,
    bool IsAttached = false);

public static class SplitConditionDataRows
{
    public static IReadOnlyList<string> BuildKeys(AppSettings settings)
    {
        return BuildKeys(settings.Route.SplitRoute);
    }

    public static IReadOnlyList<string> BuildKeys(IEnumerable<SplitRouteEntry> route)
    {
        var keys = new List<string>();
        var seenKeys = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (SplitRouteEntry entry in route)
        {
            if (!entry.Enabled || string.IsNullOrWhiteSpace(entry.Id))
            {
                continue;
            }

            string splitId = entry.Id.Trim();
            if (entry.IsAttached)
            {
                keys.Add(CreateCompletedSplitKey(splitId));
                continue;
            }

            foreach (SplitCondition fact in (entry.Condition ?? SplitCondition.All([]))
                         .ToFlatGroup()
                         .GetFactConditions())
            {
                string baseKey = CreateBaseKey(splitId, fact);
                int occurrence = seenKeys.TryGetValue(baseKey, out int count) ? count + 1 : 1;
                seenKeys[baseKey] = occurrence;
                keys.Add(occurrence == 1 ? baseKey : $"{baseKey}:{occurrence}");
            }
        }

        return keys;
    }

    public static IReadOnlyList<SplitConditionDataRow> Build(AppSettings settings)
    {
        return Build(settings.Route.SplitRoute, settings.General.Language);
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
            BuildTimingRows(settings.Route.SplitRoute, definition.Id),
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
        SplitRouteEntry? entry = settings.Route.SplitRoute.FirstOrDefault(routeEntry =>
            routeEntry.Enabled &&
            string.Equals(routeEntry.Id, splitId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return false;
        }

        return TryGetSplitTime(
            values,
            BuildTimingRows(settings.Route.SplitRoute, splitId),
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
        IReadOnlyList<SplitTimingRow> rows,
        SplitCondition condition,
        bool isAttached,
        out TimeSpan split)
    {
        split = TimeSpan.Zero;
        var parsed = new List<TimeSpan>(rows.Count);
        Dictionary<string, List<TimeSpan>>? factTimes = isAttached
            ? null
            : new Dictionary<string, List<TimeSpan>>(StringComparer.OrdinalIgnoreCase);
        foreach (SplitTimingRow row in rows)
        {
            if (!TryGetTime(values, row.Key, out TimeSpan value))
            {
                continue;
            }

            parsed.Add(value);
            if (factTimes is not null)
            {
                string signature = CreateFactSignature(row.Condition);
                if (!factTimes.TryGetValue(signature, out List<TimeSpan>? times))
                {
                    times = [];
                    factTimes[signature] = times;
                }

                times.Add(value);
            }
        }

        if (parsed.Count == 0)
        {
            return false;
        }

        if (isAttached)
        {
            split = parsed.Max();
            return true;
        }

        foreach (List<TimeSpan> times in factTimes!.Values)
        {
            times.Sort();
        }

        return TryGetConditionCompletionTime(condition ?? SplitCondition.All([]), factTimes, out split);
    }

    private static IReadOnlyList<SplitTimingRow> BuildTimingRows(
        IEnumerable<SplitRouteEntry> route,
        string splitId)
    {
        var rows = new List<SplitTimingRow>();
        var seenKeys = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (SplitRouteEntry entry in route)
        {
            if (!entry.Enabled ||
                string.IsNullOrWhiteSpace(entry.Id) ||
                !string.Equals(entry.Id.Trim(), splitId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string normalizedSplitId = entry.Id.Trim();
            SplitCondition condition = entry.Condition ?? SplitCondition.All([]);
            if (entry.IsAttached)
            {
                rows.Add(new SplitTimingRow(CreateCompletedSplitKey(normalizedSplitId), condition));
                continue;
            }

            foreach (SplitCondition fact in condition.ToFlatGroup().GetFactConditions())
            {
                string baseKey = CreateBaseKey(normalizedSplitId, fact);
                int occurrence = seenKeys.TryGetValue(baseKey, out int count) ? count + 1 : 1;
                seenKeys[baseKey] = occurrence;
                rows.Add(new SplitTimingRow(
                    occurrence == 1 ? baseKey : $"{baseKey}:{occurrence}",
                    fact));
            }
        }

        return rows;
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

    private readonly record struct SplitTimingRow(string Key, SplitCondition Condition);
}
