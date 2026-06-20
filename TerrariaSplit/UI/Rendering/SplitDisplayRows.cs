namespace TerrariaSplit;

internal readonly record struct SplitDisplayRow(int StatusIndex, int RowIndex, int ConditionIndex = -1)
{
    public bool IsExpandedCondition => ConditionIndex >= 0;
}

internal static class SplitDisplayRows
{
    public static IReadOnlyList<SplitDisplayRow> Build(IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        return Build(settings: null, statuses);
    }

    public static IReadOnlyList<SplitDisplayRow> Build(AppSettings? settings, IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        List<AttachedBlock> lockedBlocks = settings?.AutoHideAttachedGroups == false
            ? new List<AttachedBlock>()
            : GetLockedAttachedBlocks(statuses);
        Dictionary<int, int> attachedRowOverrides = BuildAttachedRowOverrides(statuses, lockedBlocks);
        var baseRows = new List<SplitDisplayRow>(statuses.Count);
        for (int index = 0; index < statuses.Count; index++)
        {
            if (IsHiddenAttachedIndex(index, lockedBlocks))
            {
                continue;
            }

            int rowIndex = attachedRowOverrides.TryGetValue(index, out int attachedRow)
                ? attachedRow
                : GetVisualRowIndex(index, lockedBlocks);
            baseRows.Add(new SplitDisplayRow(index, rowIndex));
        }

        return ExpandRows(settings, statuses, baseRows
            .OrderBy(row => row.RowIndex)
            .ThenBy(row => row.StatusIndex)
            .ToArray());
    }

    public static int GetRequiredRowCount(IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        return GetRequiredRowCount(settings: null, statuses);
    }

    public static int GetRequiredRowCount(AppSettings? settings, IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        IReadOnlyList<SplitDisplayRow> rows = Build(settings, statuses);
        return rows.Count == 0
            ? Math.Max(1, statuses.Count)
            : Math.Max(statuses.Count, rows.Max(row => row.RowIndex) + 1);
    }

    public static int GetReservedRowCount(IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        return GetReservedRowCount(settings: null, statuses);
    }

    public static int GetReservedRowCount(AppSettings? settings, IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        int rowCount = Math.Max(1, statuses.Count);
        foreach (SplitStatusSnapshot status in statuses)
        {
            rowCount += GetReservedExpansionRowCount(settings, status.Definition);
        }

        return rowCount;
    }

    private static List<AttachedBlock> GetLockedAttachedBlocks(IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        var blocks = new List<AttachedBlock>();
        int index = 0;
        while (index < statuses.Count)
        {
            if (!statuses[index].Definition.IsAttached)
            {
                index++;
                continue;
            }

            int attachedStart = index;
            while (index < statuses.Count && statuses[index].Definition.IsAttached)
            {
                index++;
            }

            int attachedEnd = index - 1;
            bool revealAttachedRows = ShouldShowAttachedBlock(statuses, attachedStart, index);
            if (!revealAttachedRows)
            {
                blocks.Add(new AttachedBlock(attachedStart, attachedEnd));
            }
        }

        return blocks;
    }

    private static bool ShouldShowAttachedBlock(
        IReadOnlyList<SplitStatusSnapshot> statuses,
        int attachedStartIndex,
        int nextNonAttachedIndex)
    {
        if (nextNonAttachedIndex >= statuses.Count || IsMarkedComplete(statuses[nextNonAttachedIndex]))
        {
            return false;
        }

        for (int i = 0; i < attachedStartIndex; i++)
        {
            if (!statuses[i].Definition.IsAttached && !IsMarkedComplete(statuses[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsMarkedComplete(SplitStatusSnapshot status)
    {
        return status.IsCompleted || status.IsSkipped;
    }

    private static int GetReservedExpansionRowCount(AppSettings? settings, SplitDefinition definition)
    {
        if (settings?.ExpandSplitDetails != true || definition.IsAttached)
        {
            return 0;
        }

        SplitCondition flat = definition.Condition.ToFlatGroup();
        int factCount = flat.GetFactConditions().Count();
        int requiredCount = flat.GetRequiredCount();
        return factCount > 1 && requiredCount > 1
            ? Math.Clamp(requiredCount, 1, factCount) - 1
            : 0;
    }

    private static Dictionary<int, int> BuildAttachedRowOverrides(
        IReadOnlyList<SplitStatusSnapshot> statuses,
        IReadOnlyList<AttachedBlock> lockedBlocks)
    {
        var overrides = new Dictionary<int, int>();
        int index = 0;
        while (index < statuses.Count)
        {
            if (!statuses[index].Definition.IsAttached)
            {
                index++;
                continue;
            }

            int attachedStart = index;
            while (index < statuses.Count && statuses[index].Definition.IsAttached)
            {
                index++;
            }

            int attachedEnd = index - 1;
            if (IsHiddenAttachedIndex(attachedStart, lockedBlocks))
            {
                continue;
            }

            int count = attachedEnd - attachedStart + 1;
            var visualRows = new List<int>(count);
            var attachedStatuses = new List<int>(count);
            for (int attachedIndex = attachedStart; attachedIndex <= attachedEnd; attachedIndex++)
            {
                visualRows.Add(GetVisualRowIndex(attachedIndex, lockedBlocks));
                attachedStatuses.Add(attachedIndex);
            }

            visualRows.Sort();
            attachedStatuses.Sort(CompareAttachedStatuses);
            for (int i = 0; i < attachedStatuses.Count; i++)
            {
                overrides[attachedStatuses[i]] = visualRows[i];
            }
        }

        return overrides;

        int CompareAttachedStatuses(int left, int right)
        {
            TimeSpan? leftTime = statuses[left].Time;
            TimeSpan? rightTime = statuses[right].Time;
            if (leftTime is TimeSpan leftValue && rightTime is TimeSpan rightValue)
            {
                int timeComparison = leftValue.CompareTo(rightValue);
                return timeComparison != 0
                    ? timeComparison
                    : left.CompareTo(right);
            }

            if (leftTime.HasValue != rightTime.HasValue)
            {
                return leftTime.HasValue ? -1 : 1;
            }

            return left.CompareTo(right);
        }
    }

    private static bool IsHiddenAttachedIndex(int statusIndex, IReadOnlyList<AttachedBlock> lockedBlocks)
    {
        foreach (AttachedBlock block in lockedBlocks)
        {
            if (statusIndex >= block.AttachedStartIndex && statusIndex <= block.AttachedEndIndex)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetVisualRowIndex(int statusIndex, IReadOnlyList<AttachedBlock> lockedBlocks)
    {
        int rowIndex = statusIndex;
        foreach (AttachedBlock block in lockedBlocks)
        {
            if (statusIndex < block.AttachedStartIndex)
            {
                rowIndex += block.AttachedCount;
            }
        }

        return rowIndex;
    }

    public static bool TryGetRowIndex(
        IReadOnlyList<SplitStatusSnapshot> statuses,
        int statusIndex,
        out int rowIndex)
    {
        return TryGetRowIndex(settings: null, statuses, statusIndex, out rowIndex);
    }

    public static bool TryGetRowIndex(
        AppSettings? settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        int statusIndex,
        out int rowIndex)
    {
        foreach (SplitDisplayRow row in Build(settings, statuses))
        {
            if (row.StatusIndex == statusIndex)
            {
                rowIndex = row.RowIndex;
                return true;
            }
        }

        rowIndex = -1;
        return false;
    }

    private static IReadOnlyList<SplitDisplayRow> ExpandRows(
        AppSettings? settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        IReadOnlyList<SplitDisplayRow> baseRows)
    {
        var rows = new List<SplitDisplayRow>(baseRows.Count);
        int extraRowsBefore = 0;
        foreach (SplitDisplayRow baseRow in baseRows)
        {
            int rowIndex = baseRow.RowIndex + extraRowsBefore;
            IReadOnlyList<SplitExpandedConditionRow> expandedRows =
                SplitExpandedConditionRows.Build(settings, statuses, baseRow.StatusIndex);
            if (expandedRows.Count == 0)
            {
                rows.Add(baseRow with { RowIndex = rowIndex });
                continue;
            }

            for (int i = 0; i < expandedRows.Count; i++)
            {
                rows.Add(new SplitDisplayRow(
                    baseRow.StatusIndex,
                    rowIndex + i,
                    expandedRows[i].ConditionIndex));
            }

            extraRowsBefore += expandedRows.Count - 1;
        }

        return rows;
    }

    private readonly record struct AttachedBlock(int AttachedStartIndex, int AttachedEndIndex)
    {
        public int AttachedCount => AttachedEndIndex - AttachedStartIndex + 1;
    }
}
