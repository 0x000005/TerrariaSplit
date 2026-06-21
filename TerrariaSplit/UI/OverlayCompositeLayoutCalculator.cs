using System.Drawing;

namespace TerrariaSplit.UI;

internal static class OverlayCompositeLayoutCalculator
{
    private const float DefaultDpi = 96f;
    private static readonly Lazy<float> SystemDpiScale = new(GetSystemDpiScale);

    public static bool TryCreate(
        Rectangle compositeBounds,
        AppSettings settings,
        int statusCount,
        int visibleStatusCount,
        int baseRowGap,
        out OverlayCompositeLayout layout)
    {
        layout = default;
        if (compositeBounds.Width <= 0 || compositeBounds.Height <= 0)
        {
            return false;
        }

        int animationRowCount = SplitCompletionAnimationRenderer.ReservedRowCount;
        statusCount = Math.Max(animationRowCount, Math.Max(statusCount, visibleStatusCount));
        visibleStatusCount = Math.Clamp(Math.Max(visibleStatusCount, animationRowCount), 1, statusCount);
        var localBounds = new Rectangle(0, 0, compositeBounds.Width, compositeBounds.Height);
        if (!SplitLayoutCalculator.TryCreate(localBounds, statusCount, baseRowGap, value => OverlayRenderContext.ScaleInt(settings, value), out SplitLayout splitLayout))
        {
            return false;
        }

        SplitLayout visibleLayout = OffsetRowsFromBottom(splitLayout, statusCount - visibleStatusCount);

        Rectangle timerTextBounds = TimerRenderer.GetTimerTextBounds(settings, visibleLayout.TimerRect);
        int maxFontHeight = GetTimerMaxFontPixelHeight(settings);
        int effectBleed = GetTimerTextEffectBleed(settings, maxFontHeight);
        int verticalBleed = Math.Max(
            OverlayRenderContext.ScaleInt(settings, 28),
            maxFontHeight + OverlayRenderContext.ScaleInt(settings, 12) + effectBleed);
        int timerBottomBleed = verticalBleed + Math.Max(
            OverlayRenderContext.ScaleInt(settings, 10),
            maxFontHeight / 8);
        int timerTop = Math.Min(visibleLayout.TimerRect.Top, timerTextBounds.Top - verticalBleed);
        int requestedTimerBottom = Math.Max(visibleLayout.TimerRect.Bottom, timerTextBounds.Bottom + timerBottomBleed);
        if (requestedTimerBottom > compositeBounds.Height)
        {
            return false;
        }

        timerTop = Math.Clamp(timerTop, 0, compositeBounds.Height - 1);
        int timerBottom = Math.Clamp(requestedTimerBottom, timerTop + 1, compositeBounds.Height);
        int statusBottom = GetStatusBottom(settings, visibleLayout, visibleStatusCount, compositeBounds.Height);

        layout = new OverlayCompositeLayout(
            compositeBounds,
            visibleLayout,
            new Rectangle(0, 0, compositeBounds.Width, statusBottom),
            new Rectangle(0, timerTop, compositeBounds.Width, timerBottom - timerTop));
        return true;
    }

    public static bool TryCreate(
        Rectangle compositeBounds,
        AppSettings settings,
        int statusCount,
        int baseRowGap,
        out OverlayCompositeLayout layout)
    {
        return TryCreate(compositeBounds, settings, statusCount, statusCount, baseRowGap, out layout);
    }

    public static int GetFittingHeight(
        int width,
        int initialHeight,
        AppSettings settings,
        int statusCount,
        int visibleStatusCount,
        int baseRowGap)
    {
        int height = Math.Max(1, initialHeight);
        if (CanCreate(height))
        {
            return height;
        }

        int step = Math.Max(OverlayRenderContext.ScaleInt(settings, 24), 8);
        int high = height;
        while (high < 10000)
        {
            high = Math.Min(10000, high + step);
            if (CanCreate(high))
            {
                break;
            }
        }

        if (!CanCreate(high))
        {
            return height;
        }

        int low = height;
        while (low + 1 < high)
        {
            int middle = low + (high - low) / 2;
            if (CanCreate(middle))
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        return high;

        bool CanCreate(int candidateHeight)
        {
            return TryCreate(
                new Rectangle(0, 0, width, candidateHeight),
                settings,
                statusCount,
                visibleStatusCount,
                baseRowGap,
                out _);
        }
    }

    private static SplitLayout OffsetRowsFromBottom(SplitLayout layout, int rowOffset)
    {
        return rowOffset <= 0
            ? layout
            : new SplitLayout(layout.GetRowRect(rowOffset), layout.TimerRect, layout.RowGap);
    }

    private static int GetStatusBottom(AppSettings settings, SplitLayout splitLayout, int statusCount, int compositeHeight)
    {
        if (statusCount <= 0)
        {
            return Math.Clamp(splitLayout.FirstRowRect.Bottom, 1, compositeHeight);
        }

        Rectangle lastRow = splitLayout.GetRowRect(statusCount - 1);
        int bleed = Math.Max(
            OverlayRenderContext.ScaleInt(settings, 16),
            GetStatusMaxFontPixelHeight(settings) + GetStatusTextEffectBleed(settings));
        return Math.Clamp(lastRow.Bottom + bleed, 1, compositeHeight);
    }

    private static int GetTimerMaxFontPixelHeight(AppSettings settings)
    {
        float scaleFactor = OverlayRenderContext.GetScaleFactor(settings);
        float dpiMultiplier = SystemDpiScale.Value * DefaultDpi / 72f;
        float timerSize = OverlayFontCache.GetColumnFontSize(settings.Columns.Timer, scaleFactor);
        float millisecondsSize = OverlayFontCache.GetColumnFontSize(settings.Columns.TimerMilliseconds, scaleFactor);
        return Math.Max(1, (int)Math.Ceiling(Math.Max(timerSize, millisecondsSize) * dpiMultiplier));
    }

    private static int GetStatusMaxFontPixelHeight(AppSettings settings)
    {
        float scaleFactor = OverlayRenderContext.GetScaleFactor(settings);
        float dpiMultiplier = SystemDpiScale.Value * DefaultDpi / 72f;
        float timeSize = OverlayFontCache.GetColumnFontSize(settings.Columns.Time, scaleFactor);
        float deltaSize = OverlayFontCache.GetColumnFontSize(settings.Columns.Delta, scaleFactor);
        float attachedTimeSize = OverlayFontCache.GetColumnFontSize(settings.Columns.AttachedTime, scaleFactor);
        float attachedDeltaSize = OverlayFontCache.GetColumnFontSize(settings.Columns.AttachedDelta, scaleFactor);
        float maxSize = Math.Max(
            Math.Max(timeSize, deltaSize),
            Math.Max(attachedTimeSize, attachedDeltaSize));
        return Math.Max(1, (int)Math.Ceiling(maxSize * dpiMultiplier));
    }

    private static int GetTimerTextEffectBleed(AppSettings settings, int maxFontPixels)
    {
        int mainBleed = EstimateTextEffectBleed(
            maxFontPixels,
            settings.TextEffects.TimerShadowPercent,
            settings.TextEffects.TimerOutlineThicknessPercent);
        int millisecondsBleed = EstimateTextEffectBleed(
            maxFontPixels,
            settings.TextEffects.TimerMillisecondsShadowPercent,
            settings.TextEffects.TimerMillisecondsOutlineThicknessPercent);
        return Math.Max(mainBleed, millisecondsBleed);
    }

    private static int GetStatusTextEffectBleed(AppSettings settings)
    {
        int fontPixels = GetStatusMaxFontPixelHeight(settings);
        int timeBleed = EstimateTextEffectBleed(
            fontPixels,
            settings.TextEffects.TimeShadowPercent,
            settings.TextEffects.TimeOutlineThicknessPercent);
        int deltaBleed = EstimateTextEffectBleed(
            fontPixels,
            settings.TextEffects.DeltaShadowPercent,
            settings.TextEffects.DeltaOutlineThicknessPercent);
        int attachedTimeBleed = EstimateTextEffectBleed(
            fontPixels,
            settings.TextEffects.AttachedTimeShadowPercent,
            settings.TextEffects.AttachedTimeOutlineThicknessPercent);
        int attachedDeltaBleed = EstimateTextEffectBleed(
            fontPixels,
            settings.TextEffects.AttachedDeltaShadowPercent,
            settings.TextEffects.AttachedDeltaOutlineThicknessPercent);
        return Math.Max(
            Math.Max(timeBleed, deltaBleed),
            Math.Max(attachedTimeBleed, attachedDeltaBleed));
    }

    private static int EstimateTextEffectBleed(int fontPixels, int shadowPercent, int outlineThicknessPercent)
    {
        float shadowOffset = Math.Clamp(shadowPercent, 0, 100) > 0
            ? Math.Clamp(fontPixels * 0.08f, 1f, 4f)
            : 0f;
        float outlineRadius = Math.Clamp(outlineThicknessPercent, 0, 100) > 0
            ? Math.Clamp(fontPixels * 0.075f * (Math.Clamp(outlineThicknessPercent, 0, 100) / 100f) + 0.15f, 0.2f, 3.5f)
            : 0f;
        return (int)Math.Ceiling(Math.Max(shadowOffset, outlineRadius) + 3f);
    }

    private static float GetSystemDpiScale()
    {
        try
        {
            using Graphics graphics = Graphics.FromHwnd(IntPtr.Zero);
            return Math.Clamp(graphics.DpiY / DefaultDpi, 0.75f, 4f);
        }
        catch
        {
            return 1f;
        }
    }
}
