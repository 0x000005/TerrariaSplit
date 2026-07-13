using System.Drawing;

namespace TerrariaSplit.UI;

internal static class SplitLayoutCalculator
{
    public static bool TryCreate(
        Rectangle bounds,
        int statusCount,
        int baseRowGap,
        Func<int, int> scaleInt,
        out SplitLayout layout)
    {
        layout = default;

        int margin = scaleInt(12);
        if (bounds.Width < scaleInt(160) || bounds.Height < scaleInt(160))
        {
            return false;
        }

        Rectangle content = Rectangle.Inflate(bounds, -margin, -margin);
        int timerHeight = Math.Clamp((int)(content.Height * 0.17), scaleInt(82), scaleInt(110));
        int rowGap = scaleInt(baseRowGap);
        int listSpace = content.Height - timerHeight - scaleInt(10);
        int rowHeight = Math.Clamp(
            (listSpace - Math.Max(0, statusCount - 1) * rowGap) / Math.Max(1, statusCount),
            scaleInt(42),
            scaleInt(58));
        if (rowHeight <= 0)
        {
            return false;
        }

        int timerY = content.Y + statusCount * rowHeight + Math.Max(0, statusCount - 1) * rowGap + scaleInt(2);
        if (timerY + timerHeight > content.Bottom)
        {
            return false;
        }

        layout = new SplitLayout(
            new Rectangle(content.X + scaleInt(2), content.Y, content.Width - scaleInt(4), rowHeight),
            new Rectangle(content.X, timerY, content.Width, timerHeight),
            rowGap);
        return true;
    }

    public static int GetDefaultWindowWidth(AppSettings settings)
    {
        float scale = Math.Clamp(settings.Overlay.Columns.ScalePercent, 25, 300) / 100f;
        int columnsWidth = GetSplitColumnsWidth(settings, scale);
        int splitWidth = columnsWidth + (int)Math.Round(28 * scale);
        return Math.Clamp(Math.Max(splitWidth, GetMinimumTimerWindowWidth(settings)), 300, 2400);
    }

    private static int GetSplitColumnsWidth(AppSettings settings, float scale)
    {
        UiColumnLayoutSettings columns = settings.Overlay.Columns;
        bool showIcon = columns.Icon.Show || columns.AttachedIcon.Show;
        bool showName = columns.Name.Show || columns.AttachedName.Show;
        bool showTime = columns.Time.Show || columns.AttachedTime.Show;
        bool showDelta = columns.Delta.Show || columns.AttachedDelta.Show;

        int width = 0;
        width += GetVisibleColumnWidth(columns, UiColumnDescriptors.Icon, showIcon, scale);
        width += GetVisibleColumnWidth(columns, UiColumnDescriptors.Name, showName, scale);
        width += GetVisibleColumnWidth(columns, UiColumnDescriptors.Time, showTime, scale);
        width += GetVisibleColumnWidth(columns, UiColumnDescriptors.Delta, showDelta, scale);
        width += showIcon && showName ? (int)Math.Round(columns.IconNameGap * scale) : 0;
        width += showTime && (showIcon || showName) ? (int)Math.Round(columns.NameTimeGap * scale) : 0;
        width += showDelta && (showIcon || showName || showTime) ? (int)Math.Round(columns.TimeDeltaGap * scale) : 0;
        return width;
    }

    private static int GetVisibleColumnWidth(
        UiColumnLayoutSettings columns,
        UiColumnDescriptor descriptor,
        bool show,
        float scale)
    {
        return show
            ? (int)Math.Round(Math.Max(1, UiColumnDescriptors.GetSharedWidth(columns, descriptor)) * scale)
            : 0;
    }

    public static int GetDefaultWindowHeight(AppSettings settings)
    {
        float scale = Math.Clamp(settings.Overlay.Columns.ScalePercent, 25, 300) / 100f;
        return Math.Clamp((int)Math.Round(720 * scale), 420, 2160);
    }

    public static int GetMinimumWindowHeightForRows(AppSettings settings, int statusCount, int baseRowGap)
    {
        int rows = Math.Max(1, Math.Max(statusCount, SplitCompletionAnimationRenderer.ReservedRowCount));
        int margin = ScaleInt(settings, 12);
        int rowHeight = ScaleInt(settings, 42);
        int rowGap = ScaleInt(settings, baseRowGap);
        int timerHeight = ScaleInt(settings, 110);
        int timerGap = ScaleInt(settings, 2);
        int height =
            margin * 2 +
            rows * rowHeight +
            Math.Max(0, rows - 1) * rowGap +
            timerGap +
            timerHeight;
        return Math.Max(GetMinimumWindowSize(settings).Height, height);
    }

    public static Size GetMinimumWindowSize(AppSettings settings)
    {
        float scale = Math.Clamp(settings.Overlay.Columns.ScalePercent, 25, 300) / 100f;
        return new Size(
            Math.Clamp(Math.Max((int)Math.Round(300 * scale), GetMinimumTimerWindowWidth(settings)), 220, 2400),
            Math.Clamp((int)Math.Round(420 * scale), 260, 1600));
    }

    private static int GetMinimumTimerWindowWidth(AppSettings settings)
    {
        if (!settings.Overlay.Columns.Timer.Show && !settings.Overlay.Columns.TimerMilliseconds.Show)
        {
            return 0;
        }

        float scale = Math.Clamp(settings.Overlay.Columns.ScalePercent, 25, 300) / 100f;
        int margin = ScaleInt(settings, 12);
        int timerTextPadding = ScaleInt(settings, 8);
        int offsetX = Math.Abs(ScaleIntAllowZero(settings, settings.Overlay.Columns.TimerOffsetX));
        int timerTextWidth = EstimateTimerTextWidth(settings, scale);
        int indicatorWidth = EstimateTimerIndicatorWidth(settings, scale, indicatorCount: 2);
        return margin * 2 + timerTextPadding + offsetX + timerTextWidth + indicatorWidth;
    }

    private static int EstimateTimerTextWidth(AppSettings settings, float scale)
    {
        const float PointToPixel = 96f / 72f;
        int width = 0;
        if (settings.Overlay.Columns.Timer.Show)
        {
            float fontSize = OverlayFontCache.GetColumnFontSize(settings.Overlay.Columns.Timer, scale);
            width += (int)Math.Ceiling(fontSize * PointToPixel * 0.75f * 8f);
        }

        if (settings.Overlay.Columns.Timer.Show && settings.Overlay.Columns.TimerMilliseconds.Show)
        {
            width += ScaleInt(settings, 2);
        }

        if (settings.Overlay.Columns.TimerMilliseconds.Show)
        {
            float fontSize = OverlayFontCache.GetColumnFontSize(settings.Overlay.Columns.TimerMilliseconds, scale);
            width += (int)Math.Ceiling(fontSize * PointToPixel * 0.70f * 3f);
        }

        int maxFontPixels = GetTimerMaxFontPixels(settings, scale);
        int effectBleed = EstimateTimerTextEffectBleed(settings, maxFontPixels);
        return width + effectBleed * 2;
    }

    private static int EstimateTimerIndicatorWidth(AppSettings settings, float scale, int indicatorCount)
    {
        if (indicatorCount <= 0)
        {
            return 0;
        }

        int maxFontPixels = GetTimerMaxFontPixels(settings, scale);
        int diameter = Math.Clamp((int)Math.Ceiling(maxFontPixels * 0.18f), 9, 28);
        int textGap = Math.Max(6, (int)Math.Ceiling(diameter * 0.45f));
        int indicatorGap = Math.Max(4, (int)Math.Ceiling(diameter * 0.28f));
        return textGap + indicatorCount * diameter + Math.Max(0, indicatorCount - 1) * indicatorGap + ScaleInt(settings, 6);
    }

    private static int GetTimerMaxFontPixels(AppSettings settings, float scale)
    {
        const float PointToPixel = 96f / 72f;
        float timerSize = settings.Overlay.Columns.Timer.Show
            ? OverlayFontCache.GetColumnFontSize(settings.Overlay.Columns.Timer, scale)
            : 0f;
        float millisecondsSize = settings.Overlay.Columns.TimerMilliseconds.Show
            ? OverlayFontCache.GetColumnFontSize(settings.Overlay.Columns.TimerMilliseconds, scale)
            : 0f;
        return Math.Max(1, (int)Math.Ceiling(Math.Max(timerSize, millisecondsSize) * PointToPixel));
    }

    private static int EstimateTimerTextEffectBleed(AppSettings settings, int maxFontPixels)
    {
        int timerBleed = EstimateTextEffectBleed(
            maxFontPixels,
            settings.Overlay.TextEffects.TimerShadowPercent,
            settings.Overlay.TextEffects.TimerOutlineThicknessPercent);
        int millisecondsBleed = EstimateTextEffectBleed(
            maxFontPixels,
            settings.Overlay.TextEffects.TimerMillisecondsShadowPercent,
            settings.Overlay.TextEffects.TimerMillisecondsOutlineThicknessPercent);
        return Math.Max(timerBleed, millisecondsBleed);
    }

    private static int EstimateTextEffectBleed(int fontPixels, int shadowPercent, int outlinePercent)
    {
        float shadow = Math.Max(0, shadowPercent) / 100f * fontPixels * 0.18f;
        float outline = Math.Max(0, outlinePercent) / 100f * fontPixels * 0.08f;
        return (int)Math.Ceiling(shadow + outline + 2f);
    }

    private static int ScaleInt(AppSettings settings, int value)
    {
        return Math.Max(1, ScaleIntAllowZero(settings, value));
    }

    private static int ScaleIntAllowZero(AppSettings settings, int value)
    {
        float scale = Math.Clamp(settings.Overlay.Columns.ScalePercent, 25, 300) / 100f;
        return (int)Math.Round(value * scale, MidpointRounding.AwayFromZero);
    }
}
