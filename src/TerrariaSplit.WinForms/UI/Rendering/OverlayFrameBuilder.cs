namespace TerrariaSplit.UI.Rendering;

internal static class OverlayFrameBuilder
{
    public static OverlayFrame Build(OverlayRenderContext context)
    {
        IReadOnlyList<SplitDisplayRow> rows = SplitDisplayRows.Build(
            context.Settings,
            context.Statuses,
            context.CurrentSplitIndex,
            context.VisibleStatusRowCount,
            context.IgnoreVisibleGroupLimit);
        int focusRowIndex = GetCurrentSplitHighlightIndex(context, rows);
        IReadOnlyList<OverlayFrameRow> frameRows = BuildFrameRows(context, rows);
        IReadOnlyList<OverlayFrameRow> paintOrderRows = BuildPaintOrderRows(rows, frameRows, focusRowIndex);
        return new OverlayFrame(
            context.Settings,
            frameRows,
            paintOrderRows,
            focusRowIndex,
            context.TimerPhase,
            context.TimerElapsed);
    }

    public static int GetCurrentSplitHighlightIndex(
        OverlayRenderContext context,
        IReadOnlyList<SplitDisplayRow> rows)
    {
        return context.Settings.Overlay.ShowCurrentSplitHighlight &&
            context.TimerPhase != SplitTimerPhase.NotStarted &&
            context.CurrentSplitIndex >= 0 &&
            context.CurrentSplitIndex < context.Statuses.Count
            ? rows.FirstOrDefault(row => row.StatusIndex == context.CurrentSplitIndex, new SplitDisplayRow(-1, -1)).RowIndex
            : -1;
    }

    private static IReadOnlyList<OverlayFrameRow> BuildFrameRows(
        OverlayRenderContext context,
        IReadOnlyList<SplitDisplayRow> rows)
    {
        return rows.Select(row => BuildFrameRow(context, row)).ToArray();
    }

    private static IReadOnlyList<OverlayFrameRow> BuildPaintOrderRows(
        IReadOnlyList<SplitDisplayRow> rows,
        IReadOnlyList<OverlayFrameRow> frameRows,
        int focusRowIndex)
    {
        Dictionary<SplitDisplayRow, OverlayFrameRow> frameRowsByDisplayRow = frameRows.ToDictionary(row => row.DisplayRow);
        return SplitRowPaintOrder.Create(rows, focusRowIndex)
            .Select(row => frameRowsByDisplayRow[row])
            .ToArray();
    }

    private static OverlayFrameRow BuildFrameRow(OverlayRenderContext context, SplitDisplayRow row)
    {
        SplitStatusSnapshot status = context.Statuses[row.StatusIndex];
        bool isCurrent = row.StatusIndex == context.CurrentSplitIndex &&
            context.TimerPhase != SplitTimerPhase.NotStarted;
        SplitExpandedConditionRow? expandedRow = TryGetExpandedRow(context, row, out SplitExpandedConditionRow expanded)
            ? expanded
            : null;

        SplitComparison comparison = expandedRow is SplitExpandedConditionRow expandedComparison
            ? GetExpandedComparison(expandedComparison)
            : SplitComparisonService.GetSplitComparison(
                context.Settings,
                context.TimerPhase,
                context.TimerElapsed,
                status,
                isCurrent);
        bool showSplitTimeStyle = GetUseSplitTimeStyle(status, expandedRow);
        bool useCompletedDeltaGradient = expandedRow is SplitExpandedConditionRow expandedDelta
            ? expandedDelta.CompletionTime.HasValue
            : status.Time.HasValue;

        return new OverlayFrameRow(
            row,
            comparison,
            FormatTimeText(context.Settings, status, expandedRow),
            showSplitTimeStyle,
            useCompletedDeltaGradient,
            expandedRow,
            expandedRow is SplitExpandedConditionRow expandedIconRow &&
                IsFirstExpandedConditionRow(context, row, expandedIconRow));
    }

    private static bool TryGetExpandedRow(
        OverlayRenderContext context,
        SplitDisplayRow row,
        out SplitExpandedConditionRow expandedRow)
    {
        if (!row.IsExpandedCondition)
        {
            expandedRow = default;
            return false;
        }

        return SplitExpandedConditionRows.TryGetRow(context.Settings, context.Statuses, row, out expandedRow);
    }

    private static bool IsFirstExpandedConditionRow(
        OverlayRenderContext context,
        SplitDisplayRow row,
        SplitExpandedConditionRow expandedRow)
    {
        IReadOnlyList<SplitExpandedConditionRow> expandedRows =
            SplitExpandedConditionRows.Build(context.Settings, context.Statuses, row.StatusIndex);
        return expandedRows.Count > 0 &&
            expandedRows[0].ConditionIndex == expandedRow.ConditionIndex;
    }

    private static SplitComparison GetExpandedComparison(SplitExpandedConditionRow row)
    {
        if (row.ReferenceTime is not TimeSpan reference)
        {
            return SplitComparison.Empty;
        }

        if (row.CompletionTime is TimeSpan completion)
        {
            return new SplitComparison(completion - reference, ShowDelta: true);
        }

        return SplitComparison.Empty;
    }

    private static bool GetUseSplitTimeStyle(
        SplitStatusSnapshot status,
        SplitExpandedConditionRow? expandedRow)
    {
        if (expandedRow is SplitExpandedConditionRow expanded)
        {
            return expanded.CompletionTime.HasValue;
        }

        return status.IsCompleted && status.Time is not null ||
            SplitRenderData.ShouldShowSkippedTime(status);
    }

    private static string FormatTimeText(
        AppSettings settings,
        SplitStatusSnapshot status,
        SplitExpandedConditionRow? expandedRow)
    {
        if (expandedRow is SplitExpandedConditionRow expanded)
        {
            return FormatExpandedTime(expanded);
        }

        if (status.IsCompleted && status.Time is TimeSpan splitTime)
        {
            return TimeText.FormatSplit(splitTime);
        }

        if (SplitRenderData.ShouldShowSkippedTime(status))
        {
            return "--";
        }

        return SplitRenderData.FormatReferenceTime(settings, status.Definition);
    }

    private static string FormatExpandedTime(SplitExpandedConditionRow row)
    {
        if (row.CompletionTime is TimeSpan completion)
        {
            return TimeText.FormatSplit(completion);
        }

        return row.ReferenceTime is TimeSpan reference
            ? TimeText.FormatSplit(reference)
            : "--";
    }
}
