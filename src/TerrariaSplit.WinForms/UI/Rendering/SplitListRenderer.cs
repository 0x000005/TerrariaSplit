using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Rendering;

internal static class SplitListRenderer
{
    private const float MinimumFittingTextSize = 1f;

    public static void Render(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        float listOpacity,
        Rectangle? clipBounds = null)
    {
        Render(graphics, context, resources, OverlayFrameBuilder.Build(context), listOpacity, clipBounds);
    }

    public static void Render(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        OverlayFrame frame,
        float listOpacity,
        Rectangle? clipBounds = null)
    {
        foreach (OverlayFrameRow row in frame.PaintOrderRows)
        {
            RenderRow(graphics, context, resources, listOpacity, row, frame.FocusRowIndex, clipBounds);
        }
    }

    /// <summary>
    /// Margin around a row rect that covers every pixel the row can emit
    /// (shadow offsets and outline radii are absolute-capped well below this).
    /// Partial redraw uses it both to build dirty regions and to decide which
    /// rows intersect them.
    /// </summary>
    public static int GetRowBleedMargin(AppSettings settings)
    {
        return Math.Max(OverlayRenderContext.ScaleInt(settings, 8), 10);
    }

    private static void RenderRow(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        float listOpacity,
        OverlayFrameRow row,
        int focusIndex,
        Rectangle? clipBounds)
    {
        Rectangle rowRect = context.Layout.GetRowRect(row.RowIndex);
        if (clipBounds is Rectangle clip)
        {
            int bleed = GetRowBleedMargin(context.Settings);
            if (!Rectangle.Inflate(rowRect, bleed, bleed).IntersectsWith(clip))
            {
                return;
            }
        }

        SplitStatusSnapshot status = context.Statuses[row.StatusIndex];
        bool isCurrent = row.StatusIndex == context.CurrentSplitIndex &&
            context.TimerPhase != SplitTimerPhase.NotStarted;
        float depthScale = GetCurrentSplitDepthScale(context.Settings, row.RowIndex, focusIndex);
        DrawSplitRow(
            graphics,
            context,
            resources,
            rowRect,
            status,
            row.StatusIndex,
            row.RowIndex,
            isCurrent,
            GetCurrentSplitDepthOpacity(context.Settings, row.RowIndex, focusIndex, listOpacity),
            depthScale,
            row);
    }

    public static float GetCurrentSplitDepthScale(AppSettings settings, int rowIndex, int focusIndex)
    {
        if (focusIndex < 0)
        {
            return 1f;
        }

        float maximumScale = Math.Clamp(settings.Overlay.CurrentSplitHighlightScalePercent, 100, 140) / 100f;
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

        float strength = Math.Clamp(settings.Overlay.CurrentSplitDepthStrengthPercent * 2f, 0f, 100f) / 100f;
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

    public static ColumnRects GetColumnRects(AppSettings settings, Rectangle rect, bool attached = false)
    {
        Span<ColumnWidth> visibleColumns = stackalloc ColumnWidth[3];
        int columnCount = 0;
        UiColumnSettings iconSettings = GetIconColumnSettings(settings, attached);
        UiColumnSettings timeSettings = GetTimeColumnSettings(settings, attached);
        UiColumnSettings deltaSettings = GetDeltaColumnSettings(settings, attached);
        AddColumn(visibleColumns, ref columnCount, SplitColumn.Icon, iconSettings);
        AddColumn(visibleColumns, ref columnCount, SplitColumn.Time, timeSettings);
        AddColumn(visibleColumns, ref columnCount, SplitColumn.Delta, deltaSettings);

        int requestedWidth = 0;
        for (int i = 0; i < columnCount; i++)
        {
            requestedWidth += OverlayRenderContext.ScaleInt(settings, visibleColumns[i].Width);
        }

        float scale = requestedWidth > rect.Width && requestedWidth > 0
            ? rect.Width / (float)requestedWidth
            : 1f;

        Rectangle? icon = null;
        Rectangle? time = null;
        Rectangle? delta = null;

        int x = rect.X;
        for (int i = 0; i < columnCount; i++)
        {
            ColumnWidth column = visibleColumns[i];
            int width = i == columnCount - 1
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

    public static UiColumnSettings GetIconColumnSettings(AppSettings settings, bool attached)
    {
        return GetColumnSettings(settings, attached ? UiColumnDescriptors.AttachedIcon : UiColumnDescriptors.Icon);
    }

    public static UiColumnSettings GetTimeColumnSettings(AppSettings settings, bool attached)
    {
        return GetColumnSettings(settings, attached ? UiColumnDescriptors.AttachedTime : UiColumnDescriptors.Time);
    }

    public static UiColumnSettings GetDeltaColumnSettings(AppSettings settings, bool attached)
    {
        return GetColumnSettings(settings, attached ? UiColumnDescriptors.AttachedDelta : UiColumnDescriptors.Delta);
    }

    private static UiColumnSettings GetColumnSettings(AppSettings settings, UiColumnDescriptor descriptor)
    {
        return descriptor.GetValue(settings.Overlay.Columns) ?? new UiColumnSettings();
    }

    private static void DrawSplitRow(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        Rectangle rect,
        SplitStatusSnapshot status,
        int statusIndex,
        int rowIndex,
        bool isCurrent,
        float opacity,
        float wheelScale,
        OverlayFrameRow frameRow)
    {
        if (opacity <= 0.01f)
        {
            return;
        }

        bool attached = status.Definition.IsAttached;
        UiColumnSettings iconSettings = GetIconColumnSettings(context.Settings, attached);
        UiColumnSettings timeSettings = GetTimeColumnSettings(context.Settings, attached);
        UiColumnSettings deltaSettings = GetDeltaColumnSettings(context.Settings, attached);
        ColumnRects columns = GetColumnRects(context.Settings, rect, attached);

        SplitExpandedConditionRow? expandedRow = frameRow.ExpandedRow;
        if (columns.Icon is Rectangle iconColumnRect)
        {
            Rectangle iconRect = Rectangle.Inflate(iconColumnRect, -2, 0);
            if (expandedRow is null || frameRow.DrawExpandedIcons)
            {
                DrawIcons(
                    graphics,
                    context,
                    resources,
                    iconRect,
                    status,
                    iconSettings,
                    opacity * OverlayTextStyles.GetIconOpacity(context.Settings, attached),
                    wheelScale,
                    context.Settings.Overlay.EnableDefeatedBossIconLighting &&
                        statusIndex == context.CurrentSplitIndex);
            }
        }

        SplitComparison comparison = frameRow.Comparison;

        if (columns.Time is Rectangle timeRect)
        {
            TextRenderStyle timeStyle = frameRow.UseSplitTimeStyle
                ? OverlayTextStyles.GetSplitTextStyle(context.Settings, context.Palette, attached)
                : OverlayTextStyles.GetReferenceTextStyle(context.Settings, context.Palette, isCurrent, attached);

            TextEffectRenderer.DrawStyledText(
                graphics,
                frameRow.TimeText,
                GetFittingColumnFont(
                    graphics,
                    resources,
                    frameRow.TimeText,
                    timeSettings,
                    context.ScaleFactor,
                    wheelScale,
                    timeStyle,
                    timeRect),
                timeStyle,
                timeRect,
                ContentAlignment.MiddleRight,
                opacity * OverlayTextStyles.GetTimeTextOpacity(context.Settings, attached),
                supersampleEffects: false);
        }

        if (columns.Delta is Rectangle deltaRect)
        {
            bool enableDeltaGradient = frameRow.UseCompletedDeltaGradient
                ? context.Settings.Overlay.EnableDeltaGradientColor
                : context.Settings.Overlay.EnableCurrentDeltaGradientColor;
            Color deltaColor = OverlayColorMath.GetDeltaComparisonColor(
                context.Settings,
                comparison,
                context.Palette,
                enableDeltaGradient);
            TextRenderStyle deltaStyle = OverlayTextStyles.GetDeltaTextStyle(
                context.Settings,
                comparison,
                context.Palette,
                attached);
            if (TryGetSegmentBestDeltaHighlight(context, statusIndex, out SegmentBestDeltaHighlight highlight))
            {
                double seconds = (context.NowUtc - highlight.StartedAtUtc).TotalSeconds;
                deltaColor = SegmentBestDeltaHighlightStyles.Apply(deltaColor, highlight.Style, seconds);
            }

            TextEffectRenderer.DrawStyledText(
                graphics,
                SplitRenderData.FormatSplitDelta(context.Settings, comparison),
                GetFittingColumnFont(
                    graphics,
                    resources,
                    SplitRenderData.FormatSplitDelta(context.Settings, comparison),
                    deltaSettings,
                    context.ScaleFactor,
                    wheelScale,
                    deltaStyle,
                    deltaRect),
                deltaStyle with { Fill = deltaColor },
                deltaRect,
                ContentAlignment.MiddleLeft,
                opacity * OverlayTextStyles.GetDeltaTextOpacity(context.Settings, attached),
                supersampleEffects: false);
        }
    }

    private static Font GetFittingColumnFont(
        Graphics graphics,
        OverlayRenderResources resources,
        string text,
        UiColumnSettings columnSettings,
        float scaleFactor,
        float baseSizeScale,
        TextRenderStyle style,
        Rectangle bounds)
    {
        Font font = resources.Fonts.GetColumnFont(columnSettings, scaleFactor, sizeScale: baseSizeScale);
        float textScale = GetFittingColumnTextScale(graphics, text, font, style, bounds);
        return textScale >= 0.995f
            ? font
            : resources.Fonts.GetColumnFont(
                columnSettings,
                scaleFactor,
                sizeScale: baseSizeScale * textScale,
                minimumSize: MinimumFittingTextSize);
    }

    private static float GetFittingColumnTextScale(
        Graphics graphics,
        string text,
        Font font,
        TextRenderStyle style,
        Rectangle bounds)
    {
        if (string.IsNullOrEmpty(text) || bounds.Width <= 1 || font.Size <= MinimumFittingTextSize)
        {
            return 1f;
        }

        using var format = new StringFormat
        {
            Trimming = StringTrimming.None,
            FormatFlags = StringFormatFlags.NoWrap,
            LineAlignment = StringAlignment.Center
        };

        SizeF measured = graphics.MeasureString(
            text,
            font,
            new SizeF(10000f, Math.Max(1, bounds.Height)),
            format);
        if (measured.Width <= 1f)
        {
            return 1f;
        }

        float outline = TextEffectGeometry.GetTextOutlineRadius(graphics, font, style);
        float shadow = TextEffectGeometry.GetTextShadowOpacity(style) > 0f
            ? TextEffectGeometry.GetTextShadowOffset(graphics, font, style)
            : 0f;
        float availableWidth = Math.Max(1f, bounds.Width - MathF.Ceiling(outline * 2f + shadow + 2f));
        if (measured.Width <= availableWidth)
        {
            return 1f;
        }

        float minimumScale = MinimumFittingTextSize / font.Size;
        return Math.Clamp(availableWidth / measured.Width, minimumScale, 1f);
    }

    private static void DrawIcons(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        Rectangle rect,
        SplitStatusSnapshot status,
        UiColumnSettings iconSettings,
        float opacity = 1f,
        float sizeScale = 1f,
        bool brighten = false)
    {
        SplitDefinition definition = status.Definition;
        IReadOnlyList<int> iconOrder = GetIconDrawOrder(context, status, definition);
        int count = iconOrder.Count;
        if (count == 0)
        {
            return;
        }

        ImageRenderStyle iconStyle = OverlayTextStyles.GetIconImageStyle(
            context.Settings,
            context.Palette,
            definition.IsAttached);

        if (count == 1)
        {
            int iconIndex = iconOrder[0];
            IconPair icon = resources.BossIcons.Load(definition, iconIndex, context.Settings);
            resources.BossIcons.TrackRendered(icon);
            bool lit = IsIconLit(context, status, definition, iconIndex);
            int singleIconSize = Math.Min(
                Math.Min(Math.Max(12, context.ScaleInt((int)Math.Round(iconSettings.FontSize * sizeScale))), rect.Height),
                rect.Width);
            var iconRect = new Rectangle(
                rect.Right - singleIconSize,
                rect.Y + Math.Max(0, (rect.Height - singleIconSize) / 2),
                singleIconSize,
                singleIconSize);
            Image image = lit
                ? icon.GetLitImage(context.NowUtc)
                : brighten
                    ? icon.GetCurrentImage(context.NowUtc)
                    : icon.GetUndefeatedImage(context.NowUtc);
            TextEffectRenderer.DrawImage(graphics, image, iconRect, opacity, iconStyle);
            return;
        }

        int iconGap = context.ScaleInt(6);
        int size = Math.Min(
            Math.Min(Math.Max(12, context.ScaleInt((int)Math.Round(iconSettings.FontSize * sizeScale))), rect.Height),
            Math.Max(12, (rect.Width - Math.Max(0, count - 1) * iconGap) / count));
        int totalWidth = count * size + (count - 1) * iconGap;
        int startX = rect.Right - totalWidth;
        int y = rect.Y + Math.Max(0, (rect.Height - size) / 2);
        for (int i = 0; i < count; i++)
        {
            int iconIndex = iconOrder[i];
            IconPair icon = resources.BossIcons.Load(definition, iconIndex, context.Settings);
            resources.BossIcons.TrackRendered(icon);
            bool lit = IsIconLit(context, status, definition, iconIndex);
            Image image = lit
                ? icon.GetLitImage(context.NowUtc)
                : brighten
                    ? icon.GetCurrentImage(context.NowUtc)
                    : icon.GetUndefeatedImage(context.NowUtc);
            TextEffectRenderer.DrawImage(
                graphics,
                image,
                new Rectangle(startX + i * (size + iconGap), y, size, size),
                opacity,
                iconStyle);
        }
    }

    private static IReadOnlyList<int> GetIconDrawOrder(
        OverlayRenderContext context,
        SplitStatusSnapshot status,
        SplitDefinition definition)
    {
        int count = definition.IconFileNames.Count;
        if (count == 0)
        {
            return [];
        }

        if (definition.IconLightingConditions.Count > 0)
        {
            return Enumerable.Range(0, count).ToArray();
        }

        var satisfied = new List<(int Index, TimeSpan? CompletionTime, int CompletionOrder)>();
        var pending = new List<int>();
        for (int index = 0; index < count; index++)
        {
            if (IsIconSatisfied(context, status, definition, index, out TimeSpan? completionTime))
            {
                satisfied.Add((index, completionTime, GetIconCompletionOrder(status, definition, index)));
            }
            else
            {
                pending.Add(index);
            }
        }

        int[] satisfiedOrder = satisfied
            .OrderBy(item => GetIconCompletionSortTime(item.CompletionTime, item.CompletionOrder))
            .ThenBy(item => item.CompletionOrder)
            .ThenBy(item => item.Index)
            .Select(item => item.Index)
            .ToArray();
        if (!ShouldHidePendingIcons(context, status, definition))
        {
            return satisfiedOrder.Concat(pending).ToArray();
        }

        return satisfiedOrder.Length > 0
            ? satisfiedOrder
            : Enumerable.Range(0, count).ToArray();
    }

    private static bool ShouldHidePendingIcons(
        OverlayRenderContext context,
        SplitStatusSnapshot status,
        SplitDefinition definition)
    {
        if (status.IsCompleted)
        {
            return true;
        }

        if (status.IsSkipped)
        {
            return status.CompletedFactKeys.Count > 0 ||
                status.FactCompletionTimes?.Count is > 0;
        }

        if (context.TimerPhase == SplitTimerPhase.NotStarted)
        {
            return false;
        }

        return EvaluateConditionWithItemFactFallback(definition.Condition, context.Snapshot.Facts) ==
            SplitConditionResult.True;
    }

    private static TimeSpan GetIconCompletionSortTime(TimeSpan? completionTime, int completionOrder)
    {
        if (completionTime is TimeSpan time)
        {
            return time;
        }

        return completionOrder < int.MaxValue
            ? TimeSpan.Zero
            : TimeSpan.MaxValue;
    }

    private static bool IsIconLit(
        OverlayRenderContext context,
        SplitStatusSnapshot status,
        SplitDefinition displayDefinition,
        int iconIndex)
    {
        if (!context.Settings.Overlay.EnableDefeatedBossIconLighting)
        {
            return true;
        }

        if (displayDefinition.IconLightingConditions.Count > 0)
        {
            if (status.IsCompleted)
            {
                return true;
            }

            if (status.IsSkipped)
            {
                return status.CompletedFactKeys.Count > 0;
            }

            if (context.TimerPhase == SplitTimerPhase.NotStarted)
            {
                return false;
            }

            return displayDefinition.IconLightingConditions
                .Any(condition => IsIconLightingConditionSatisfied(context, condition));
        }

        if (status.IsCompleted &&
            status.CompletedFactKeys.Count == 0 &&
            status.FactCompletionTimes?.Count is not > 0)
        {
            return true;
        }

        return IsIconSatisfied(context, status, displayDefinition, iconIndex, out _);
    }

    private static bool IsIconLightingConditionSatisfied(
        OverlayRenderContext context,
        SplitCondition condition)
    {
        return EvaluateConditionWithItemFactFallback(condition, context.Snapshot.Facts) == SplitConditionResult.True;
    }

    private static SplitConditionResult EvaluateConditionWithItemFactFallback(
        SplitCondition condition,
        TerrariaGameFacts facts)
    {
        SplitConditionResult result = condition.Evaluate(facts);
        if (result == SplitConditionResult.True)
        {
            return result;
        }

        SplitCondition fallback = condition.Clone();
        return RewriteEverOwnedItemFactsToCurrentOwned(fallback)
            ? fallback.Evaluate(facts)
            : result;
    }

    private static bool RewriteEverOwnedItemFactsToCurrentOwned(SplitCondition condition)
    {
        bool changed = false;
        if (SplitCatalog.TryParseItemEverOwnedFactKey(condition.FactKey, out int itemId))
        {
            condition.FactKey = SplitCatalog.CreateItemFactKey(itemId);
            changed = true;
        }

        foreach (SplitCondition child in condition.Children)
        {
            changed |= RewriteEverOwnedItemFactsToCurrentOwned(child);
        }

        return changed;
    }

    private static bool IsIconSatisfied(
        OverlayRenderContext context,
        SplitStatusSnapshot status,
        SplitDefinition displayDefinition,
        int iconIndex,
        out TimeSpan? completionTime)
    {
        completionTime = null;
        if (TryGetIconCompletionTime(status, displayDefinition, iconIndex, out TimeSpan time))
        {
            completionTime = time;
            return true;
        }

        if (!TryGetIconFactKeys(displayDefinition, iconIndex, out string[] factKeys))
        {
            return false;
        }

        if (status.CompletedFactKeys.Any(completedFactKey =>
            factKeys.Contains(completedFactKey, StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (status.IsSkipped || context.TimerPhase == SplitTimerPhase.NotStarted)
        {
            return false;
        }

        return factKeys.Any(factKey => IsFactSatisfied(
            context.Snapshot.Facts,
            factKey,
            displayDefinition.IconKeys[iconIndex],
            displayDefinition.Condition));
    }

    private static bool TryGetIconFactKeys(
        SplitDefinition displayDefinition,
        int iconIndex,
        out string[] factKeys)
    {
        factKeys = [];
        if (iconIndex < 0 || iconIndex >= displayDefinition.IconKeys.Count)
        {
            return false;
        }

        string targetId = displayDefinition.IconKeys[iconIndex];
        string factKey;
        if (SplitCatalog.TryParseItemTargetId(targetId, out int itemId))
        {
            factKey = SplitCatalog.CreateItemFactKey(itemId);
        }
        else if (SplitCatalog.TryGetTarget(targetId, out SplitTargetDefinition target))
        {
            factKey = target.FactKey;
        }
        else
        {
            return false;
        }

        factKeys = GetEquivalentItemFactKeys(factKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return factKeys.Length > 0;
    }

    private static IEnumerable<string> GetEquivalentItemFactKeys(string factKey)
    {
        if (!string.IsNullOrWhiteSpace(factKey))
        {
            yield return factKey;
        }

        if (SplitCatalog.TryParseItemFactKey(factKey, out int itemId))
        {
            yield return SplitCatalog.CreateItemFactKey(itemId);
            yield return SplitCatalog.CreateItemEverOwnedFactKey(itemId);
        }
    }

    private static int GetIconCompletionOrder(
        SplitStatusSnapshot status,
        SplitDefinition displayDefinition,
        int iconIndex)
    {
        if (!TryGetIconFactKeys(displayDefinition, iconIndex, out string[] factKeys))
        {
            return int.MaxValue;
        }

        int order = int.MaxValue;
        foreach (string factKey in factKeys)
        {
            for (int i = 0; i < status.CompletedFactKeys.Count; i++)
            {
                if (string.Equals(status.CompletedFactKeys[i], factKey, StringComparison.OrdinalIgnoreCase))
                {
                    order = Math.Min(order, i);
                    break;
                }
            }
        }

        return order;
    }

    private static bool IsFactSatisfied(
        TerrariaGameFacts facts,
        string factKey,
        string targetId,
        SplitCondition splitCondition)
    {
        if (SplitCatalog.TryParseItemTargetId(targetId, out int itemId))
        {
            return IsItemFactSatisfied(facts, factKey, itemId, splitCondition);
        }

        if (!SplitCatalog.TryGetTarget(targetId, out SplitTargetDefinition target))
        {
            return false;
        }

        FactValue value = facts.Get(factKey);
        return target.Kind switch
        {
            SplitTargetKind.Boss => value.AsBoolean() == true,
            SplitTargetKind.Npc => value.AsBoolean() == true,
            _ => false
        };
    }

    private static bool IsItemFactSatisfied(
        TerrariaGameFacts facts,
        string factKey,
        int itemId,
        SplitCondition splitCondition)
    {
        int requiredQuantity = GetMinimumRequiredItemQuantity(splitCondition, itemId);
        return facts.Get(factKey).AsInteger() is int currentQuantity &&
            currentQuantity >= requiredQuantity;
    }

    private static int GetMinimumRequiredItemQuantity(SplitCondition splitCondition, int itemId)
    {
        int? minimumQuantity = null;
        foreach (SplitCondition factCondition in splitCondition.GetFactConditions())
        {
            if (!SplitCatalog.TryParseItemFactKey(factCondition.FactKey, out int conditionItemId) ||
                conditionItemId != itemId ||
                !TryGetPositiveItemQuantityRequirement(factCondition, out int requiredQuantity))
            {
                continue;
            }

            minimumQuantity = minimumQuantity.HasValue
                ? Math.Min(minimumQuantity.Value, requiredQuantity)
                : requiredQuantity;
        }

        return minimumQuantity ?? 1;
    }

    private static bool TryGetPositiveItemQuantityRequirement(SplitCondition condition, out int requiredQuantity)
    {
        requiredQuantity = Math.Max(1, condition.Value);
        return SplitFactComparison.Normalize(condition.Comparison) switch
        {
            SplitFactComparison.AtLeast => true,
            SplitFactComparison.Equal => true,
            SplitFactComparison.IsTrue => true,
            _ => false
        };
    }

    private static bool TryGetIconCompletionTime(
        SplitStatusSnapshot status,
        SplitDefinition displayDefinition,
        int iconIndex,
        out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (!TryGetIconFactKeys(displayDefinition, iconIndex, out string[] factKeys))
        {
            return false;
        }

        bool found = false;
        foreach (string factKey in factKeys)
        {
            if (status.TryGetFactCompletionTime(factKey, out TimeSpan candidate) &&
                (!found || candidate < time))
            {
                time = candidate;
                found = true;
            }
        }

        return found;
    }

    private static bool TryGetSegmentBestDeltaHighlight(
        OverlayRenderContext context,
        int statusIndex,
        out SegmentBestDeltaHighlight highlight)
    {
        if (context.Settings.Overlay.ShowSegmentBestDeltaHighlight &&
            context.SegmentBestDeltaHighlights.TryGetValue(statusIndex, out highlight) &&
            statusIndex >= 0 &&
            statusIndex < context.Statuses.Count &&
            context.Statuses[statusIndex].IsCompleted &&
            SegmentBestDeltaHighlightStyles.Normalize(highlight.Style) != SegmentBestDeltaHighlightStyles.None)
        {
            return true;
        }

        highlight = default;
        return false;
    }

    private static void AddColumn(Span<ColumnWidth> columns, ref int columnCount, SplitColumn column, UiColumnSettings settings)
    {
        if (settings.Show)
        {
            columns[columnCount++] = new ColumnWidth(column, Math.Max(1, settings.Width));
        }
    }
}
