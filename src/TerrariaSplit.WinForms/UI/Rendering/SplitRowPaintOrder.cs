namespace TerrariaSplit.UI.Rendering;

internal static class SplitRowPaintOrder
{
    public static IReadOnlyList<SplitDisplayRow> Create(IReadOnlyList<SplitDisplayRow> rows, int focusIndex)
    {
        if (focusIndex < 0 || rows.Count <= 1)
        {
            return rows;
        }

        return rows
            .OrderByDescending(row => Math.Abs(row.RowIndex - focusIndex))
            .ThenBy(row => row.RowIndex)
            .ToArray();
    }
}
