using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal static class SplitListRenderer
{
    public static void Render(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        float listOpacity)
    {
        int focusIndex = GetCurrentSplitHighlightIndex(context);
        IEnumerable<int> rowOrder = Enumerable.Range(0, context.Statuses.Count);
        if (focusIndex >= 0)
        {
            rowOrder = rowOrder
                .OrderByDescending(index => Math.Abs(index - focusIndex))
                .ThenBy(index => index);
        }

        foreach (int i in rowOrder)
        {
            BossSplitStatus status = context.Statuses[i];
            bool isCurrent = i == context.CurrentSplitIndex && context.TimerPhase != SplitTimerPhase.NotStarted;
            float depthScale = GetCurrentSplitDepthScale(context.Settings, i, focusIndex);
            DrawSplitRow(
                graphics,
                context,
                resources,
                context.Layout.GetRowRect(i),
                status,
                i,
                isCurrent,
                GetCurrentSplitDepthOpacity(context.Settings, i, focusIndex, listOpacity),
                depthScale);
        }
    }

    public static int GetCurrentSplitHighlightIndex(OverlayRenderContext context)
    {
        return context.Settings.ShowCurrentSplitHighlight &&
            context.TimerPhase != SplitTimerPhase.NotStarted &&
            context.CurrentSplitIndex >= 0 &&
            context.CurrentSplitIndex < context.Statuses.Count
            ? context.CurrentSplitIndex
            : -1;
    }

    public static float GetCurrentSplitDepthScale(AppSettings settings, int rowIndex, int focusIndex)
    {
        if (focusIndex < 0)
        {
            return 1f;
        }

        float maximumScale = Math.Clamp(settings.CurrentSplitHighlightScalePercent, 100, 140) / 100f;
        float lift = maximumScale - 1f;
        if (lift <= 0.001f)
        {
            return 1f;
        }

        int distance = Math.Abs(rowIndex - focusIndex);
        float falloff = distance switch
        {
            0 => 1f,
            1 => 0.58f,
            2 => 0.28f,
            3 => 0.10f,
            _ => 0f
        };
        return 1f + lift * falloff;
    }

    public static float GetCurrentSplitDepthOpacity(
        AppSettings settings,
        int rowIndex,
        int focusIndex,
        float baseOpacity)
    {
        if (focusIndex < 0)
        {
            return baseOpacity;
        }

        float strength = Math.Clamp(settings.CurrentSplitDepthStrengthPercent * 2f, 0f, 100f) / 100f;
        int distance = Math.Abs(rowIndex - focusIndex);
        float depthLoss = distance switch
        {
            0 => 0f,
            1 => 0.24f,
            2 => 0.46f,
            3 => 0.62f,
            _ => 0.72f
        };
        float depthOpacity = 1f - depthLoss * strength;
        return baseOpacity * depthOpacity;
    }

    public static ColumnRects GetColumnRects(AppSettings settings, Rectangle rect)
    {
        List<ColumnWidth> visibleColumns = new();
        AddColumn(visibleColumns, SplitColumn.Icon, settings.Columns.Icon);
        AddColumn(visibleColumns, SplitColumn.Time, settings.Columns.Time);
        AddColumn(visibleColumns, SplitColumn.Delta, settings.Columns.Delta);

        int requestedWidth = visibleColumns.Sum(column => OverlayRenderContext.ScaleInt(settings, column.Width));
        float scale = requestedWidth > rect.Width && requestedWidth > 0
            ? rect.Width / (float)requestedWidth
            : 1f;

        Rectangle? icon = null;
        Rectangle? time = null;
        Rectangle? delta = null;

        int x = rect.X;
        for (int i = 0; i < visibleColumns.Count; i++)
        {
            ColumnWidth column = visibleColumns[i];
            int width = i == visibleColumns.Count - 1
                ? rect.Right - x
                : Math.Max(1, (int)Math.Round(OverlayRenderContext.ScaleInt(settings, column.Width) * scale));
            var columnRect = new Rectangle(x, rect.Y, width, rect.Height);
            x += width;

            switch (column.Column)
            {
                case SplitColumn.Icon:
                    icon = columnRect;
                    break;
                case SplitColumn.Time:
                    time = Rectangle.Inflate(columnRect, -4, 0);
                    break;
                case SplitColumn.Delta:
                    delta = Rectangle.Inflate(columnRect, -4, 0);
                    break;
            }
        }

        return new ColumnRects(icon, time, delta);
    }

    private static void DrawSplitRow(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        Rectangle rect,
        BossSplitStatus status,
        int rowIndex,
        bool isCurrent,
        float opacity,
        float wheelScale)
    {
        if (opacity <= 0.01f)
        {
            return;
        }

        ColumnRects columns = GetColumnRects(context.Settings, rect);

        if (columns.Icon is Rectangle iconColumnRect)
        {
            Rectangle iconRect = Rectangle.Inflate(iconColumnRect, -2, 0);
            DrawIcons(
                graphics,
                context,
                resources,
                iconRect,
                status,
                opacity * OverlayTextStyles.GetIconOpacity(context.Settings),
                wheelScale,
                context.Settings.EnableDefeatedBossIconLighting &&
                    rowIndex == GetCurrentSplitHighlightIndex(context));
        }

        SplitComparison comparison = SplitRenderData.GetSplitComparison(
            context.Settings,
            context.TimerPhase,
            context.TimerElapsed,
            status,
            isCurrent);

        if (columns.Time is Rectangle timeRect)
        {
            bool showSplitTime = status.IsCompleted && status.Time is not null;
            string timeText = showSplitTime
                ? TimeText.FormatSplit(status.Time!.Value)
                : SplitRenderData.FormatReferenceTime(context.Settings, status.Definition);
            TextRenderStyle timeStyle = showSplitTime
                ? OverlayTextStyles.GetSplitTextStyle(context.Settings, context.Palette)
                : OverlayTextStyles.GetReferenceTextStyle(context.Settings, context.Palette, isCurrent);

            TextEffectRenderer.DrawStyledText(
                graphics,
                timeText,
                resources.Fonts.GetColumnFont(context.Settings.Columns.Time, context.ScaleFactor, sizeScale: wheelScale),
                timeStyle,
                timeRect,
                ContentAlignment.MiddleRight,
                opacity * OverlayTextStyles.GetTimeTextOpacity(context.Settings),
                supersampleEffects: false);
        }

        if (columns.Delta is Rectangle deltaRect)
        {
            bool enableDeltaGradient = status.Time is TimeSpan
                ? context.Settings.EnableDeltaGradientColor
                : context.Settings.EnableCurrentDeltaGradientColor;
            Color deltaColor = OverlayColorMath.GetDeltaComparisonColor(
                context.Settings,
                comparison,
                context.Palette,
                enableDeltaGradient);
            TextRenderStyle deltaStyle = OverlayTextStyles.GetDeltaTextStyle(
                context.Settings,
                comparison,
                context.Palette);
            if (TryGetSegmentBestDeltaHighlight(context, rowIndex, out SegmentBestDeltaHighlight highlight))
            {
                double seconds = (context.NowUtc - highlight.StartedAtUtc).TotalSeconds;
                deltaColor = SegmentBestDeltaHighlightStyles.Apply(deltaColor, highlight.Style, seconds);
            }

            TextEffectRenderer.DrawStyledText(
                graphics,
                SplitRenderData.FormatSplitDelta(context.Settings, comparison),
                resources.Fonts.GetColumnFont(context.Settings.Columns.Delta, context.ScaleFactor, sizeScale: wheelScale),
                deltaStyle with { Fill = deltaColor },
                deltaRect,
                ContentAlignment.MiddleLeft,
                opacity * OverlayTextStyles.GetDeltaTextOpacity(context.Settings),
                supersampleEffects: false);
        }
    }

    private static void DrawIcons(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        Rectangle rect,
        BossSplitStatus status,
        float opacity = 1f,
        float sizeScale = 1f,
        bool brighten = false)
    {
        BossSplitDefinition definition = status.Definition;
        int count = definition.IconFileNames.Count;
        if (count == 0)
        {
            return;
        }

        if (count == 1)
        {
            IconPair icon = resources.BossIcons.Load(definition, definition.IconFileNames[0], context.Settings);
            bool lit = IsIconLit(context, status, 0);
            int singleIconSize = Math.Min(
                Math.Min(Math.Max(12, context.ScaleInt((int)Math.Round(context.Settings.Columns.Icon.FontSize * sizeScale))), rect.Height),
                rect.Width);
            var iconRect = new Rectangle(
                rect.Right - singleIconSize,
                rect.Y + Math.Max(0, (rect.Height - singleIconSize) / 2),
                singleIconSize,
                singleIconSize);
            Image image = lit ? icon.Lit : brighten ? icon.Current : icon.Undefeated;
            TextEffectRenderer.DrawImage(graphics, image, iconRect, opacity);
            return;
        }

        int iconGap = context.ScaleInt(6);
        int size = Math.Min(
            Math.Min(Math.Max(12, context.ScaleInt((int)Math.Round(context.Settings.Columns.Icon.FontSize * sizeScale))), rect.Height),
            Math.Max(12, (rect.Width - Math.Max(0, count - 1) * iconGap) / count));
        int totalWidth = count * size + (count - 1) * iconGap;
        int startX = rect.Right - totalWidth;
        int y = rect.Y + Math.Max(0, (rect.Height - size) / 2);
        for (int i = 0; i < count; i++)
        {
            IconPair icon = resources.BossIcons.Load(definition, definition.IconFileNames[i], context.Settings);
            bool lit = IsIconLit(context, status, i);
            Image image = lit ? icon.Lit : brighten ? icon.Current : icon.Undefeated;
            TextEffectRenderer.DrawImage(
                graphics,
                image,
                new Rectangle(startX + i * (size + iconGap), y, size, size),
                opacity);
        }
    }

    private static bool IsIconLit(OverlayRenderContext context, BossSplitStatus status, int iconIndex)
    {
        if (!context.Settings.EnableDefeatedBossIconLighting)
        {
            return true;
        }

        if (status.IsCompleted || status.IsSkipped)
        {
            return true;
        }

        if (context.TimerPhase == SplitTimerPhase.NotStarted)
        {
            return false;
        }

        if (iconIndex < 0 || iconIndex >= status.Definition.IconKeys.Count)
        {
            return false;
        }

        return BossSplitDefinitions.TryGetUnit(status.Definition.IconKeys[iconIndex], out BossUnitDefinition unit) &&
            unit.RequiredFlags.All(flag => context.Snapshot.BossStates.Get(flag) == true);
    }

    private static bool TryGetSegmentBestDeltaHighlight(
        OverlayRenderContext context,
        int rowIndex,
        out SegmentBestDeltaHighlight highlight)
    {
        if (context.Settings.ShowSegmentBestDeltaHighlight &&
            context.SegmentBestDeltaHighlights.TryGetValue(rowIndex, out highlight) &&
            rowIndex >= 0 &&
            rowIndex < context.Statuses.Count &&
            context.Statuses[rowIndex].IsCompleted &&
            SegmentBestDeltaHighlightStyles.Normalize(highlight.Style) != SegmentBestDeltaHighlightStyles.None)
        {
            return true;
        }

        highlight = default;
        return false;
    }

    private static void AddColumn(List<ColumnWidth> columns, SplitColumn column, UiColumnSettings settings)
    {
        if (settings.Show)
        {
            columns.Add(new ColumnWidth(column, Math.Max(1, settings.Width)));
        }
    }
}
