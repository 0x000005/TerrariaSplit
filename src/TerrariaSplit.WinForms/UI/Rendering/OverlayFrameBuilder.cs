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
        return new OverlayFrame(
            context.Settings,
            rows,
            SplitRowPaintOrder.Create(rows, focusRowIndex),
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
}
