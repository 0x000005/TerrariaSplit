using System.Drawing;

namespace TerrariaSplit.UI.Rendering;

internal readonly record struct TextRenderStyle(
    Color Fill,
    Color Outline,
    Color Shadow,
    int ShadowPercent,
    int OutlineThicknessPercent,
    bool LinearEffects = false);

internal readonly record struct ImageRenderStyle(
    Color Outline,
    Color Shadow,
    int ShadowPercent,
    int OutlineThicknessPercent)
{
    public static ImageRenderStyle Empty => new(Color.Empty, Color.Empty, 0, 0);

    public bool HasEffects => ShadowPercent > 0 || OutlineThicknessPercent > 0;
}

internal readonly record struct UiPalette(
    Color ReferenceText,
    Color ReferenceTextOutline,
    Color ReferenceTextShadow,
    Color ActiveReferenceText,
    Color ActiveReferenceTextOutline,
    Color ActiveReferenceTextShadow,
    Color SplitText,
    Color SplitTextOutline,
    Color SplitTextShadow,
    Color IconOutline,
    Color IconShadow,
    Color DeltaAheadText,
    Color DeltaAheadTextOutline,
    Color DeltaAheadTextShadow,
    Color DeltaBehindText,
    Color DeltaBehindTextOutline,
    Color DeltaBehindTextShadow,
    Color TimerText,
    Color TimerTextOutline,
    Color TimerTextShadow,
    Color TimerAheadText,
    Color TimerAheadTextOutline,
    Color TimerAheadTextShadow,
    Color TimerBehindText,
    Color TimerBehindTextOutline,
    Color TimerBehindTextShadow,
    Color TimerRecordText,
    Color TimerRecordTextOutline,
    Color TimerRecordTextShadow,
    Color TimerNoRecordText,
    Color TimerNoRecordTextOutline,
    Color TimerNoRecordTextShadow,
    Color TimerPausedText,
    Color TimerPausedTextOutline,
    Color TimerPausedTextShadow,
    Color SplitCompletionSegmentLabelText,
    Color SplitCompletionLabelText,
    Color SplitCompletionSegmentTimeText,
    Color SplitCompletionTimeText)
{
    public static UiPalette From(UiColorSettings settings)
    {
        return new UiPalette(
            ColorText.Parse(settings.ReferenceText, Color.FromArgb(200, 200, 200)),
            ColorText.Parse(settings.ReferenceTextOutline, Color.FromArgb(16, 16, 16)),
            ColorText.Parse(settings.ReferenceTextShadow, Color.Black),
            ColorText.Parse(settings.ActiveReferenceText, Color.FromArgb(255, 211, 90)),
            ColorText.Parse(settings.ActiveReferenceTextOutline, Color.FromArgb(16, 16, 16)),
            ColorText.Parse(settings.ActiveReferenceTextShadow, Color.Black),
            ColorText.Parse(settings.SplitText, Color.FromArgb(240, 160, 64)),
            ColorText.Parse(settings.SplitTextOutline, Color.FromArgb(16, 16, 16)),
            ColorText.Parse(settings.SplitTextShadow, Color.Black),
            ColorText.Parse(settings.IconOutline, Color.FromArgb(16, 16, 16)),
            ColorText.Parse(settings.IconShadow, Color.Black),
            ColorText.Parse(settings.DeltaAheadText, Color.LightGreen),
            ColorText.Parse(settings.DeltaAheadTextOutline, Color.FromArgb(16, 16, 16)),
            ColorText.Parse(settings.DeltaAheadTextShadow, Color.Black),
            ColorText.Parse(settings.DeltaBehindText, Color.LightCoral),
            ColorText.Parse(settings.DeltaBehindTextOutline, Color.FromArgb(16, 16, 16)),
            ColorText.Parse(settings.DeltaBehindTextShadow, Color.Black),
            ColorText.Parse(settings.TimerText, Color.FromArgb(242, 242, 242)),
            ColorText.Parse(settings.TimerTextOutline, Color.FromArgb(16, 16, 16)),
            ColorText.Parse(settings.TimerTextShadow, Color.Black),
            ColorText.Parse(settings.TimerAheadText, Color.LightGreen),
            ColorText.Parse(settings.TimerAheadTextOutline, Color.FromArgb(16, 16, 16)),
            ColorText.Parse(settings.TimerAheadTextShadow, Color.Black),
            ColorText.Parse(settings.TimerBehindText, Color.LightCoral),
            ColorText.Parse(settings.TimerBehindTextOutline, Color.FromArgb(16, 16, 16)),
            ColorText.Parse(settings.TimerBehindTextShadow, Color.Black),
            ColorText.Parse(settings.TimerRecordText, Color.FromArgb(105, 167, 255)),
            ColorText.Parse(settings.TimerRecordTextOutline, Color.FromArgb(16, 16, 16)),
            ColorText.Parse(settings.TimerRecordTextShadow, Color.Black),
            ColorText.Parse(settings.TimerNoRecordText, Color.Red),
            ColorText.Parse(settings.TimerNoRecordTextOutline, Color.FromArgb(16, 16, 16)),
            ColorText.Parse(settings.TimerNoRecordTextShadow, Color.Black),
            ColorText.Parse(settings.TimerPausedText, Color.Gainsboro),
            ColorText.Parse(settings.TimerPausedTextOutline, Color.FromArgb(16, 16, 16)),
            ColorText.Parse(settings.TimerPausedTextShadow, Color.Black),
            ColorText.Parse(settings.SplitCompletionSegmentLabelText, Color.FromArgb(222, 222, 226)),
            ColorText.Parse(settings.SplitCompletionLabelText, Color.FromArgb(222, 222, 226)),
            ColorText.Parse(settings.SplitCompletionSegmentTimeText, Color.White),
            ColorText.Parse(settings.SplitCompletionTimeText, Color.White));
    }
}
