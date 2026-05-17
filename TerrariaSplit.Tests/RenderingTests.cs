using System.Drawing;
using TerrariaSplit;

namespace TerrariaSplit.Tests;

internal static class RenderingTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("UiPalette maps configured text colors", UiPaletteMapsConfiguredTextColors);
        yield return ("TextEffectRenderer applies opacity without changing RGB", TextEffectRendererAppliesOpacity);
        yield return ("OverlayFontCache keeps main timer font independent from milliseconds visibility", OverlayFontCacheKeepsMainTimerFontIndependentFromMillisecondsVisibility);
        yield return ("SplitListRenderer preserves current split depth curve", SplitListRendererPreservesCurrentSplitDepthCurve);
        yield return ("SplitCompletionAnimationRenderer preserves fade curve", SplitCompletionAnimationRendererPreservesFadeCurve);
        yield return ("SplitCompletionAnimationRenderer preserves delta slide curve", SplitCompletionAnimationRendererPreservesDeltaSlideCurve);
        yield return ("OverlayTextStyles maps text effect percentages", OverlayTextStylesMapsTextEffectPercentages);
    }

    private static void UiPaletteMapsConfiguredTextColors()
    {
        var colors = new UiColorSettings
        {
            TimerText = "#112233",
            TimerTextOutline = "#445566",
            TimerTextShadow = "#778899",
            SplitCompletionTimeText = "#AABBCC"
        };

        UiPalette palette = UiPalette.From(colors);

        TestAssert.Equal(Color.FromArgb(0x11, 0x22, 0x33), palette.TimerText);
        TestAssert.Equal(Color.FromArgb(0x44, 0x55, 0x66), palette.TimerTextOutline);
        TestAssert.Equal(Color.FromArgb(0x77, 0x88, 0x99), palette.TimerTextShadow);
        TestAssert.Equal(Color.FromArgb(0xAA, 0xBB, 0xCC), palette.SplitCompletionTimeText);
    }

    private static void TextEffectRendererAppliesOpacity()
    {
        Color color = TextEffectRenderer.WithOpacity(Color.FromArgb(200, 10, 20, 30), 0.5f);

        TestAssert.Equal(100, color.A);
        TestAssert.Equal(10, color.R);
        TestAssert.Equal(20, color.G);
        TestAssert.Equal(30, color.B);
    }

    private static void OverlayFontCacheKeepsMainTimerFontIndependentFromMillisecondsVisibility()
    {
        var settings = new AppSettings();
        settings.Columns.ScalePercent = 150;
        settings.Columns.Timer.FontSize = 38;
        settings.Columns.Timer.Show = true;
        settings.Columns.TimerMilliseconds.FontSize = 12;
        settings.Columns.TimerMilliseconds.Show = true;

        float withMilliseconds = OverlayFontCache.GetColumnFontSize(
            settings.Columns.Timer,
            OverlayRenderContext.GetScaleFactor(settings));

        settings.Columns.TimerMilliseconds.Show = false;
        float withoutMilliseconds = OverlayFontCache.GetColumnFontSize(
            settings.Columns.Timer,
            OverlayRenderContext.GetScaleFactor(settings));

        TestAssert.Equal(withMilliseconds, withoutMilliseconds);
        Nearly(57f, withMilliseconds);
    }

    private static void SplitListRendererPreservesCurrentSplitDepthCurve()
    {
        var settings = new AppSettings
        {
            CurrentSplitHighlightScalePercent = 130,
            CurrentSplitDepthStrengthPercent = 50
        };

        Nearly(1.30f, SplitListRenderer.GetCurrentSplitDepthScale(settings, rowIndex: 2, focusIndex: 2));
        Nearly(1.174f, SplitListRenderer.GetCurrentSplitDepthScale(settings, rowIndex: 1, focusIndex: 2));
        Nearly(1.084f, SplitListRenderer.GetCurrentSplitDepthScale(settings, rowIndex: 0, focusIndex: 2));
        Nearly(1.03f, SplitListRenderer.GetCurrentSplitDepthScale(settings, rowIndex: 5, focusIndex: 2));
        Nearly(1f, SplitListRenderer.GetCurrentSplitDepthScale(settings, rowIndex: 6, focusIndex: 2));

        Nearly(0.8f, SplitListRenderer.GetCurrentSplitDepthOpacity(settings, rowIndex: 2, focusIndex: 2, baseOpacity: 0.8f));
        Nearly(0.608f, SplitListRenderer.GetCurrentSplitDepthOpacity(settings, rowIndex: 1, focusIndex: 2, baseOpacity: 0.8f));
        Nearly(0.432f, SplitListRenderer.GetCurrentSplitDepthOpacity(settings, rowIndex: 0, focusIndex: 2, baseOpacity: 0.8f));
        Nearly(0.304f, SplitListRenderer.GetCurrentSplitDepthOpacity(settings, rowIndex: 5, focusIndex: 2, baseOpacity: 0.8f));
        Nearly(0.224f, SplitListRenderer.GetCurrentSplitDepthOpacity(settings, rowIndex: 6, focusIndex: 2, baseOpacity: 0.8f));
    }

    private static void SplitCompletionAnimationRendererPreservesFadeCurve()
    {
        TimeSpan duration = TimeSpan.FromSeconds(4);
        TimeSpan halfFade = TimeSpan.FromMilliseconds(225);

        Nearly(0f, SplitCompletionAnimationRenderer.GetAnimationOpacity(TimeSpan.Zero, duration));
        Nearly(0.5f, SplitCompletionAnimationRenderer.GetAnimationOpacity(halfFade, duration));
        Nearly(1f, SplitCompletionAnimationRenderer.GetAnimationOpacity(TimeSpan.FromSeconds(1), duration));
        Nearly(0.5f, SplitCompletionAnimationRenderer.GetAnimationOpacity(duration - halfFade, duration));
        Nearly(0f, SplitCompletionAnimationRenderer.GetAnimationOpacity(duration, duration));
    }

    private static void SplitCompletionAnimationRendererPreservesDeltaSlideCurve()
    {
        TimeSpan duration = TimeSpan.FromSeconds(4);
        float slide = SplitCompletionAnimationRenderer.GetDeltaSlideDistance(20f);

        SplitCompletionDeltaMotion beforeIntro = SplitCompletionAnimationRenderer.GetDeltaMotion(
            TimeSpan.FromMilliseconds(500),
            duration,
            slide);
        Nearly(slide, beforeIntro.OffsetX);
        Nearly(0f, beforeIntro.Opacity);

        SplitCompletionDeltaMotion visible = SplitCompletionAnimationRenderer.GetDeltaMotion(
            TimeSpan.FromSeconds(1.5),
            duration,
            slide);
        Nearly(0f, visible.OffsetX);
        Nearly(1f, visible.Opacity);

        SplitCompletionDeltaMotion ended = SplitCompletionAnimationRenderer.GetDeltaMotion(duration, duration, slide);
        Nearly(slide, ended.OffsetX);
        Nearly(0f, ended.Opacity);
    }

    private static void OverlayTextStylesMapsTextEffectPercentages()
    {
        var settings = new AppSettings
        {
            TextEffects = new UiTextEffectSettings
            {
                TimeShadowPercent = 11,
                TimeOutlineThicknessPercent = 12,
                DeltaShadowPercent = 21,
                DeltaOutlineThicknessPercent = 22,
                TimerShadowPercent = 31,
                TimerOutlineThicknessPercent = 32,
                TimerMillisecondsShadowPercent = 41,
                TimerMillisecondsOutlineThicknessPercent = 42
            }
        };
        UiPalette palette = UiPalette.From(settings.Colors);

        TextRenderStyle split = OverlayTextStyles.GetSplitTextStyle(settings, palette);
        TextRenderStyle delta = OverlayTextStyles.GetDeltaTextStyle(settings, new SplitComparison(TimeSpan.FromSeconds(-1), true), palette);
        TextRenderStyle timer = OverlayTextStyles.GetTimerTextStyle(
            settings,
            Array.Empty<BossSplitStatus>(),
            currentSplitIndex: 0,
            SplitTimerPhase.NotStarted,
            TimeSpan.Zero,
            palette,
            milliseconds: false);
        TextRenderStyle milliseconds = OverlayTextStyles.GetTimerTextStyle(
            settings,
            Array.Empty<BossSplitStatus>(),
            currentSplitIndex: 0,
            SplitTimerPhase.NotStarted,
            TimeSpan.Zero,
            palette,
            milliseconds: true);

        TestAssert.Equal(11, split.ShadowPercent);
        TestAssert.Equal(12, split.OutlineThicknessPercent);
        TestAssert.Equal(21, delta.ShadowPercent);
        TestAssert.Equal(22, delta.OutlineThicknessPercent);
        TestAssert.Equal(31, timer.ShadowPercent);
        TestAssert.Equal(32, timer.OutlineThicknessPercent);
        TestAssert.Equal(41, milliseconds.ShadowPercent);
        TestAssert.Equal(42, milliseconds.OutlineThicknessPercent);
    }

    private static void Nearly(float expected, float actual)
    {
        if (Math.Abs(expected - actual) > 0.001f)
        {
            throw new InvalidOperationException($"Expected approximately '{expected}', got '{actual}'.");
        }
    }
}
