using System.Drawing;

namespace TerrariaSplit.UI.Rendering;

internal sealed record OverlayRenderContext(
    AppSettings Settings,
    UiPalette Palette,
    TerrariaWatchSnapshot Snapshot,
    IReadOnlyList<SplitStatusSnapshot> Statuses,
    int CurrentSplitIndex,
    SplitTimerPhase TimerPhase,
    TimeSpan TimerElapsed,
    SplitLayout Layout,
    int VisibleStatusRowCount,
    bool MouseClickThrough,
    SplitCompletionAnimation? SplitCompletionAnimation,
    IReadOnlyDictionary<int, SegmentBestDeltaHighlight> SegmentBestDeltaHighlights,
    DateTime NowUtc,
    bool IgnoreVisibleGroupLimit = false)
{
    public float ScaleFactor => GetScaleFactor(Settings);

    public int ScaleInt(int value)
    {
        return ScaleInt(Settings, value);
    }

    public static float GetScaleFactor(AppSettings settings)
    {
        return Math.Clamp(settings.Overlay.Columns.ScalePercent, 25, 300) / 100f;
    }

    public static int ScaleInt(AppSettings settings, int value)
    {
        if (value == 0)
        {
            return 0;
        }

        int scaled = (int)Math.Round(value * GetScaleFactor(settings), MidpointRounding.AwayFromZero);
        if (scaled == 0)
        {
            return value < 0 ? -1 : 1;
        }

        return scaled;
    }
}

internal readonly record struct OverlayRenderResult(bool SplitCompletionAnimationActive);

internal sealed record OverlayFrame(
    AppSettings Settings,
    IReadOnlyList<SplitDisplayRow> Rows,
    IReadOnlyList<SplitDisplayRow> PaintOrderRows,
    int FocusRowIndex,
    SplitTimerPhase TimerPhase,
    TimeSpan TimerElapsed);

internal readonly record struct ColumnRects(
    Rectangle? Icon,
    Rectangle? Time,
    Rectangle? Delta);

internal enum SplitColumn
{
    Icon,
    Time,
    Delta
}

internal readonly record struct SegmentBestDeltaHighlight(string Style, DateTime StartedAtUtc);

internal sealed record SplitCompletionAnimation(
    SplitDefinition Definition,
    TimeSpan SegmentTime,
    TimeSpan SplitTime,
    SplitComparison ReferenceSplitComparison,
    SplitComparison PersonalBestSegmentComparison,
    bool ShowSplitComparison,
    string SplitTimeOutlineStyle,
    bool ShowSegmentComparison,
    string SegmentTimeOutlineStyle,
    string SegmentBestDeltaHighlightStyle,
    DateTime StartedAtUtc);

internal readonly record struct SplitCompletionDeltaMotion(float OffsetX, float Opacity);

internal readonly record struct FontMetrics(float Ascent, float Descent);

internal readonly record struct TimerTextLayout(float Right, float Top, float Height, float Opacity)
{
    public static TimerTextLayout Empty => new(0f, 0f, 0f, 0f);
}

internal readonly record struct ColumnWidth(SplitColumn Column, int Width);
