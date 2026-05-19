using System.Drawing;

namespace TerrariaSplit;

internal static class OverlayCompositeLayoutCalculator
{
    private const float DefaultDpi = 96f;
    private static readonly Lazy<float> SystemDpiScale = new(GetSystemDpiScale);

    public static bool TryCreate(
        Rectangle compositeBounds,
        AppSettings settings,
        int statusCount,
        int baseRowGap,
        out OverlayCompositeLayout layout)
    {
        layout = default;
        if (compositeBounds.Width <= 0 || compositeBounds.Height <= 0)
        {
            return false;
        }

        var localBounds = new Rectangle(0, 0, compositeBounds.Width, compositeBounds.Height);
        if (!SplitLayoutCalculator.TryCreate(localBounds, statusCount, baseRowGap, value => OverlayRenderContext.ScaleInt(settings, value), out SplitLayout splitLayout))
        {
            return false;
        }

        Rectangle timerTextBounds = TimerRenderer.GetTimerTextBounds(settings, splitLayout.TimerRect);
        int maxFontHeight = GetTimerMaxFontPixelHeight(settings);
        int effectBleed = GetTimerTextEffectBleed(settings, maxFontHeight);
        int verticalBleed = Math.Max(
            OverlayRenderContext.ScaleInt(settings, 28),
            maxFontHeight + OverlayRenderContext.ScaleInt(settings, 12) + effectBleed);
        int timerTop = Math.Min(splitLayout.TimerRect.Top, timerTextBounds.Top - verticalBleed);
        int timerBottom = Math.Max(splitLayout.TimerRect.Bottom, timerTextBounds.Bottom + verticalBleed);
        timerTop = Math.Clamp(timerTop, 0, compositeBounds.Height - 1);
        timerBottom = Math.Clamp(timerBottom, timerTop + 1, compositeBounds.Height);
        int statusBottom = GetStatusBottom(settings, splitLayout, statusCount, compositeBounds.Height);

        layout = new OverlayCompositeLayout(
            compositeBounds,
            splitLayout,
            new Rectangle(0, 0, compositeBounds.Width, statusBottom),
            new Rectangle(0, timerTop, compositeBounds.Width, timerBottom - timerTop));
        return true;
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
        return Math.Max(1, (int)Math.Ceiling(Math.Max(timeSize, deltaSize) * dpiMultiplier));
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
        return Math.Max(timeBleed, deltaBleed);
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
