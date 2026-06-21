namespace TerrariaSplit.UI.Rendering;

internal readonly record struct SplitExpandedConditionRow(
    int ConditionIndex,
    SplitCondition Condition,
    TimeSpan? ReferenceTime,
    TimeSpan? CompletionTime);

internal static class SplitExpandedConditionRows
{
    public static IReadOnlyList<SplitExpandedConditionRow> Build(
        AppSettings? settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        int statusIndex)
    {
        if (!ShouldExpand(settings, statuses, statusIndex))
        {
            return [];
        }

        SplitStatusSnapshot status = statuses[statusIndex];
        List<SplitCondition> facts = status.Definition.Condition
            .ToFlatGroup()
            .GetFactConditions()
            .ToList();
        int requiredCount = Math.Clamp(status.Definition.Condition.GetRequiredCount(), 1, facts.Count);
        List<SplitExpandedConditionRow> rows = facts
            .Select((fact, index) => CreateRow(settings, status, fact, index))
            .OrderBy(row => row.CompletionTime.HasValue ? 0 : 1)
            .ThenBy(row => row.CompletionTime ?? TimeSpan.MaxValue)
            .ThenBy(row => row.ReferenceTime ?? TimeSpan.MaxValue)
            .ThenBy(row => row.ConditionIndex)
            .ToList();
        int completedCount = rows.Count(row => row.CompletionTime.HasValue);
        int visibleCount = Math.Clamp(completedCount + 1, 1, requiredCount);
        return rows.Take(visibleCount).ToArray();
    }

    public static bool ShouldExpand(
        AppSettings? settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        int statusIndex)
    {
        if (settings?.Route.ExpandSplitDetails != true)
        {
            return false;
        }

        if (statusIndex < 0 || statusIndex >= statuses.Count)
        {
            return false;
        }

        SplitStatusSnapshot status = statuses[statusIndex];
        if (status.Definition.IsAttached ||
            status.IsSkipped)
        {
            return false;
        }

        if (status.IsCompleted && settings.Route.CollapseSplitDetailsOnCompletion)
        {
            return false;
        }

        SplitCondition flat = status.Definition.Condition.ToFlatGroup();
        int factCount = flat.GetFactConditions().Count();
        int requiredCount = flat.GetRequiredCount();
        if (factCount <= 1 || requiredCount <= 1)
        {
            return false;
        }

        if (status.IsCompleted)
        {
            return true;
        }

        for (int i = 0; i < statusIndex; i++)
        {
            SplitStatusSnapshot previous = statuses[i];
            if (!previous.Definition.IsAttached &&
                !previous.IsCompleted &&
                !previous.IsSkipped)
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryGetRow(
        AppSettings? settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        SplitDisplayRow displayRow,
        out SplitExpandedConditionRow expandedRow)
    {
        foreach (SplitExpandedConditionRow row in Build(settings, statuses, displayRow.StatusIndex))
        {
            if (row.ConditionIndex == displayRow.ConditionIndex)
            {
                expandedRow = row;
                return true;
            }
        }

        expandedRow = default;
        return false;
    }

    private static SplitExpandedConditionRow CreateRow(
        AppSettings? settings,
        SplitStatusSnapshot status,
        SplitCondition fact,
        int conditionIndex)
    {
        return new SplitExpandedConditionRow(
            conditionIndex,
            fact.Clone(),
            TryGetReferenceTime(settings, status.Definition.Id, conditionIndex, out TimeSpan referenceTime)
                ? referenceTime
                : null,
            GetCompletionSortTime(status, fact.FactKey));
    }

    private static TimeSpan? GetCompletionSortTime(SplitStatusSnapshot status, string factKey)
    {
        if (status.TryGetFactCompletionTime(factKey, out TimeSpan completionTime))
        {
            return completionTime;
        }

        return status.CompletedFactKeys.Contains(factKey, StringComparer.OrdinalIgnoreCase)
            ? TimeSpan.Zero
            : null;
    }

    private static bool TryGetReferenceTime(
        AppSettings? settings,
        string splitId,
        int conditionIndex,
        out TimeSpan referenceTime)
    {
        referenceTime = TimeSpan.Zero;
        if (settings is null)
        {
            return false;
        }

        SplitConditionDataRow? row = SplitConditionDataRows.ForSplit(settings, splitId)
            .FirstOrDefault(candidate => candidate.ConditionIndex == conditionIndex);
        return row is not null &&
            ReferenceSplitSetService.GetActiveReferenceSet(settings).Splits.TryGetValue(row.Key, out string? value) &&
            TimeText.TryParse(value, out referenceTime);
    }
}
