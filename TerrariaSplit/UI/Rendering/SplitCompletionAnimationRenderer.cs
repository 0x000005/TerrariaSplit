using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit;

internal static class SplitCompletionAnimationRenderer
{
    private static readonly TimeSpan SplitCompletionFadeDuration = TimeSpan.FromSeconds(0.45);
    private static readonly TimeSpan SplitCompletionDeltaIntroGap = TimeSpan.FromSeconds(0.06);
    private const float SplitCompletionLabelFontRatio = 0.58f;
    private const float SplitCompletionDeltaFontRatio = 0.85f;
    private const float SplitCompletionDeltaOutroLeadRatio = 0.55f;
    private const float SplitCompletionDeltaIntroDurationRatio = 0.85f;
    private const float SplitCompletionDeltaSlideDistanceRatio = 0.75f;
    private const float SplitCompletionDeltaMinSlideDistance = 10f;
    private const float SplitCompletionDeltaMaxSlideDistance = 28f;

    public static bool TryGetActiveAnimation(
        AppSettings settings,
        SplitCompletionAnimation? animation,
        DateTime nowUtc,
        out TimeSpan elapsed,
        out float opacity)
    {
        elapsed = TimeSpan.Zero;
        opacity = 0f;
        if (animation is null)
        {
            return false;
        }

        elapsed = nowUtc - animation.StartedAtUtc;
        TimeSpan duration = GetAnimationDuration(settings);
        if (elapsed >= duration)
        {
            return false;
        }

        opacity = GetAnimationOpacity(elapsed, duration);
        return true;
    }

    public static TimeSpan GetAnimationDuration(AppSettings settings)
    {
        return TimeSpan.FromSeconds(Math.Clamp(settings.SplitCompletionAnimationDurationSeconds, 2f, 20f));
    }

    public static float GetAnimationOpacity(TimeSpan elapsed, TimeSpan duration)
    {
        if (elapsed < TimeSpan.Zero || elapsed >= duration)
        {
            return 0f;
        }

        TimeSpan fadeDuration = GetFadeDuration(duration);
        if (elapsed < fadeDuration)
        {
            return EaseInOut((float)(elapsed.TotalMilliseconds / fadeDuration.TotalMilliseconds));
        }

        TimeSpan fadeOutStart = duration - fadeDuration;
        if (elapsed > fadeOutStart)
        {
            return EaseInOut((float)((duration - elapsed).TotalMilliseconds / fadeDuration.TotalMilliseconds));
        }

        return 1f;
    }

    public static SplitCompletionDeltaMotion GetDeltaMotion(
        TimeSpan elapsed,
        TimeSpan duration,
        float slideDistance)
    {
        if (slideDistance <= 0f || duration <= TimeSpan.Zero)
        {
            return new SplitCompletionDeltaMotion(0f, 1f);
        }

        TimeSpan fadeDuration = GetFadeDuration(duration);
        if (elapsed < TimeSpan.Zero || elapsed >= duration)
        {
            return new SplitCompletionDeltaMotion(slideDistance, 0f);
        }

        TimeSpan fadeOutStart = duration - fadeDuration;
        TimeSpan deltaFadeOutStart = fadeOutStart - TimeSpan.FromMilliseconds(
            fadeDuration.TotalMilliseconds * SplitCompletionDeltaOutroLeadRatio);
        TimeSpan deltaIntroStart = fadeDuration + SplitCompletionDeltaIntroGap;
        TimeSpan deltaIntroDuration = TimeSpan.FromMilliseconds(Math.Max(
            0.24 * 1000d,
            Math.Min(
                0.40 * 1000d,
                fadeDuration.TotalMilliseconds * SplitCompletionDeltaIntroDurationRatio)));
        TimeSpan deltaIntroEnd = deltaIntroStart + deltaIntroDuration;

        if (elapsed < deltaIntroStart)
        {
            return new SplitCompletionDeltaMotion(slideDistance, 0f);
        }

        if (elapsed < deltaIntroEnd)
        {
            float progress = (float)((elapsed - deltaIntroStart).TotalMilliseconds / deltaIntroDuration.TotalMilliseconds);
            float reveal = EaseInOut(progress);
            return new SplitCompletionDeltaMotion(slideDistance * (1f - reveal), reveal);
        }

        if (elapsed > deltaFadeOutStart)
        {
            float progress = (float)((elapsed - deltaFadeOutStart).TotalMilliseconds / fadeDuration.TotalMilliseconds);
            float hide = EaseInOut(progress);
            return new SplitCompletionDeltaMotion(slideDistance * hide, 1f - hide);
        }

        return new SplitCompletionDeltaMotion(0f, 1f);
    }

    public static float GetDeltaSlideDistance(float deltaFontSize)
    {
        return Math.Clamp(
            deltaFontSize * SplitCompletionDeltaSlideDistanceRatio,
            SplitCompletionDeltaMinSlideDistance,
            SplitCompletionDeltaMaxSlideDistance);
    }

    public static void Render(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        SplitCompletionAnimation animation,
        TimeSpan elapsed,
        float opacity)
    {
        if (context.Statuses.Count <= 0)
        {
            return;
        }

        Rectangle firstRow = context.Layout.GetRowRect(0);
        Rectangle lastRow = context.Layout.GetRowRect(context.Statuses.Count - 1);
        var listBounds = new Rectangle(firstRow.X, firstRow.Y, firstRow.Width, lastRow.Bottom - firstRow.Top);
        if (listBounds.Width <= 0 || listBounds.Height <= 0)
        {
            return;
        }

        float centerX = GetCenterX(graphics, context, resources, context.Layout.TimerRect, listBounds);
        DrawIcon(graphics, context, resources, listBounds, centerX, animation, elapsed, opacity);
        DrawTimes(graphics, context, resources, listBounds, centerX, animation, elapsed, opacity);
    }

    private static TimeSpan GetFadeDuration(TimeSpan duration)
    {
        double seconds = Math.Min(SplitCompletionFadeDuration.TotalSeconds, duration.TotalSeconds * 0.45);
        return TimeSpan.FromSeconds(Math.Max(0.05, seconds));
    }

    private static float EaseInOut(float value)
    {
        float t = Math.Clamp(value, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static void DrawIcon(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        Rectangle listBounds,
        float centerX,
        SplitCompletionAnimation animation,
        TimeSpan elapsed,
        float opacity)
    {
        IReadOnlyList<string> iconFileNames = animation.Definition.IconFileNames;
        if (iconFileNames.Count == 0)
        {
            return;
        }

        Rectangle iconRect = GetIconRect(context, listBounds, centerX);

        if (iconFileNames.Count == 1)
        {
            DrawIconFrame(graphics, context, resources, animation, iconFileNames[0], iconRect, opacity);
            return;
        }

        TimeSpan duration = GetAnimationDuration(context.Settings);
        float progress = Math.Clamp((float)(elapsed.TotalMilliseconds / duration.TotalMilliseconds), 0f, 0.999f);
        float position = progress * iconFileNames.Count;
        int iconIndex = Math.Min(iconFileNames.Count - 1, (int)position);
        float localProgress = position - iconIndex;
        bool hasNextIcon = iconIndex < iconFileNames.Count - 1;
        float fadeProgress = hasNextIcon
            ? EaseInOut((localProgress - 0.68f) / 0.32f)
            : 0f;

        DrawIconFrame(
            graphics,
            context,
            resources,
            animation,
            iconFileNames[iconIndex],
            iconRect,
            opacity * (1f - fadeProgress));

        if (hasNextIcon && fadeProgress > 0.01f)
        {
            DrawIconFrame(
                graphics,
                context,
                resources,
                animation,
                iconFileNames[iconIndex + 1],
                iconRect,
                opacity * fadeProgress);
        }
    }

    private static Rectangle GetIconRect(OverlayRenderContext context, Rectangle listBounds, float centerX)
    {
        int maxIconSize = Math.Max(1, Math.Min((int)(listBounds.Width * 0.475f), (int)(listBounds.Height * 0.425f)));
        int minIconSize = Math.Min(context.ScaleInt(90), maxIconSize);
        int iconSize = Math.Clamp(context.ScaleInt(188), minIconSize, maxIconSize);
        int iconX = (int)Math.Round(centerX - iconSize / 2f, MidpointRounding.AwayFromZero);
        iconX = Math.Clamp(iconX, listBounds.Left, listBounds.Right - iconSize);
        return new Rectangle(
            iconX,
            listBounds.Top + Math.Max(0, (int)(listBounds.Height * 0.12f)),
            iconSize,
            iconSize);
    }

    private static void DrawIconFrame(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        SplitCompletionAnimation animation,
        string iconFileName,
        Rectangle iconRect,
        float opacity)
    {
        if (opacity <= 0.01f)
        {
            return;
        }

        IconPair icon = resources.BossIcons.Load(animation.Definition, iconFileName, context.Settings);
        TextEffectRenderer.DrawImage(graphics, icon.Lit, iconRect, opacity);
    }

    private static void DrawTimes(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        Rectangle listBounds,
        float centerX,
        SplitCompletionAnimation animation,
        TimeSpan elapsed,
        float opacity)
    {
        Rectangle iconRect = GetIconRect(context, listBounds, centerX);
        int sidePadding = Math.Max(context.ScaleInt(8), (int)Math.Round(listBounds.Width * 0.03f));
        int top = iconRect.Bottom + Math.Max(context.ScaleInt(6), (int)Math.Round(listBounds.Height * 0.02f));
        int bottom = listBounds.Bottom - context.ScaleInt(2);
        float leftLimit = listBounds.Left + sidePadding;
        float rightLimit = listBounds.Right - sidePadding;
        float textCenterX = Math.Clamp(centerX, leftLimit, rightLimit);
        float halfWidth = Math.Max(0f, Math.Min(textCenterX - leftLimit, rightLimit - textCenterX));
        var textBounds = Rectangle.FromLTRB(
            (int)Math.Floor(textCenterX - halfWidth),
            Math.Min(top, bottom),
            (int)Math.Ceiling(textCenterX + halfWidth),
            bottom);
        if (textBounds.Width <= 0 || textBounds.Height <= 0)
        {
            return;
        }

        string segmentValue = SplitTimerFormatter.Format(animation.SegmentTime);
        string segmentDelta = GetDeltaText(context.Settings, animation.PersonalBestSegmentComparison, animation.ShowSegmentComparison);
        string splitValue = SplitTimerFormatter.Format(animation.SplitTime);
        string splitDelta = GetDeltaText(context.Settings, animation.ReferenceSplitComparison, animation.ShowSplitComparison);
        float valueSize = GetValueFontSize(
            graphics,
            textBounds.Width,
            textBounds.Height,
            segmentValue,
            segmentDelta,
            splitValue,
            splitDelta,
            context.ScaleFactor);
        float labelSize = valueSize * SplitCompletionLabelFontRatio;
        float deltaSize = valueSize * SplitCompletionDeltaFontRatio;
        TimeSpan animationDuration = GetAnimationDuration(context.Settings);

        using var labelFont = OverlayTextMetrics.CreatePixelFont(labelSize, FontStyle.Bold);
        using var valueFont = OverlayTextMetrics.CreatePixelFont(valueSize, FontStyle.Bold);
        using var deltaFont = OverlayTextMetrics.CreatePixelFont(deltaSize, FontStyle.Bold);

        int labelHeight = Math.Max(1, (int)Math.Ceiling(labelFont.GetHeight(graphics)));
        int valueHeight = Math.Max(1, (int)Math.Ceiling(valueFont.GetHeight(graphics)) + context.ScaleInt(2));
        int rowHeight = labelHeight + valueHeight + context.ScaleInt(2);
        float reservedGap = string.IsNullOrEmpty(segmentDelta) && string.IsNullOrEmpty(splitDelta)
            ? 0f
            : Math.Max(6f, valueFont.Size * 0.55f);
        int gap = Math.Max(3, (int)Math.Round(valueFont.Size * 0.32f));
        int totalHeight = rowHeight * 2 + gap;
        int startY = textBounds.Top + Math.Max(0, (textBounds.Height - totalHeight) / 2);

        var segmentRect = new Rectangle(textBounds.Left, startY, textBounds.Width, rowHeight);
        var splitRect = new Rectangle(textBounds.Left, startY + rowHeight + gap, textBounds.Width, rowHeight);

        DrawTimeRow(
            graphics,
            context,
            segmentRect,
            Localizer.Get("Segment time", context.Settings),
            segmentValue,
            animation.PersonalBestSegmentComparison,
            animation.ShowSegmentComparison,
            animation.SegmentTimeOutlineStyle,
            labelFont,
            valueFont,
            deltaFont,
            reservedGap,
            animationDuration,
            elapsed,
            opacity,
            animation.SegmentBestDeltaHighlightStyle);
        DrawTimeRow(
            graphics,
            context,
            splitRect,
            Localizer.Get("Split time", context.Settings),
            splitValue,
            animation.ReferenceSplitComparison,
            animation.ShowSplitComparison,
            animation.SplitTimeOutlineStyle,
            labelFont,
            valueFont,
            deltaFont,
            reservedGap,
            animationDuration,
            elapsed,
            opacity,
            SegmentBestDeltaHighlightStyles.None);
    }

    private static float GetCenterX(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        Rectangle timerRect,
        Rectangle listBounds)
    {
        if (!context.Settings.Columns.Timer.Show && !context.Settings.Columns.TimerMilliseconds.Show)
        {
            return listBounds.Left + listBounds.Width / 2f;
        }

        Rectangle timerTextBounds = TimerRenderer.GetTimerTextBounds(context, timerRect);
        float groupWidth = TimerRenderer.MeasureTimerTextGroupWidth(graphics, context, resources, timerTextBounds);
        float centerX = timerTextBounds.Left + groupWidth / 2f;
        return Math.Clamp(centerX, listBounds.Left, listBounds.Right);
    }

    private static string GetDeltaText(
        AppSettings settings,
        SplitComparison comparison,
        bool showComparison)
    {
        return showComparison && comparison.ShowDelta && comparison.Delta is TimeSpan delta
            ? TimeText.FormatDelta(delta, settings.EnableDynamicDeltaTimeUnits)
            : string.Empty;
    }

    private static float GetValueFontSize(
        Graphics graphics,
        int availableWidth,
        int availableHeight,
        string firstValue,
        string firstDelta,
        string secondValue,
        string secondDelta,
        float scale)
    {
        if (availableWidth <= 0 || availableHeight <= 0)
        {
            return 24f;
        }

        float low = 8f;
        float high = Math.Clamp(56f * scale, 24f, 96f);
        for (int i = 0; i < 12; i++)
        {
            float mid = (low + high) / 2f;
            if (DoesTextFit(
                graphics,
                availableWidth,
                availableHeight,
                mid,
                firstValue,
                firstDelta,
                secondValue,
                secondDelta))
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    private static bool DoesTextFit(
        Graphics graphics,
        int availableWidth,
        int availableHeight,
        float valueSize,
        string firstValue,
        string firstDelta,
        string secondValue,
        string secondDelta)
    {
        using var labelFont = OverlayTextMetrics.CreatePixelFont(valueSize * SplitCompletionLabelFontRatio, FontStyle.Bold);
        using var valueFont = OverlayTextMetrics.CreatePixelFont(valueSize, FontStyle.Bold);
        using var deltaFont = OverlayTextMetrics.CreatePixelFont(valueSize * SplitCompletionDeltaFontRatio, FontStyle.Bold);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            FormatFlags = StringFormatFlags.NoWrap
        };

        float firstValueWidth = graphics.MeasureString(firstValue, valueFont, Size.Empty, format).Width;
        float secondValueWidth = graphics.MeasureString(secondValue, valueFont, Size.Empty, format).Width;
        float firstDeltaWidth = MeasureDeltaTextWidth(graphics, deltaFont, firstDelta, format);
        float secondDeltaWidth = MeasureDeltaTextWidth(graphics, deltaFont, secondDelta, format);
        float slidePadding = GetDeltaSlideDistance(deltaFont.Size);
        float deltaGap = firstDeltaWidth > 0f || secondDeltaWidth > 0f
            ? Math.Max(6f, valueFont.Size * 0.55f)
            : 0f;
        float requiredHalfWidth = Math.Max(
            firstValueWidth / 2f + (firstDeltaWidth > 0f ? deltaGap + firstDeltaWidth + slidePadding : 0f),
            secondValueWidth / 2f + (secondDeltaWidth > 0f ? deltaGap + secondDeltaWidth + slidePadding : 0f));
        float labelHeight = labelFont.GetHeight(graphics);
        float valueHeight = valueFont.GetHeight(graphics) + 2f;
        float rowHeight = labelHeight + valueHeight + 2f;
        float totalHeight = rowHeight * 2f + Math.Max(3f, valueFont.Size * 0.32f);
        return requiredHalfWidth <= availableWidth / 2f && totalHeight <= availableHeight;
    }

    private static float MeasureDeltaTextWidth(
        Graphics graphics,
        Font deltaFont,
        string deltaText,
        StringFormat format)
    {
        return string.IsNullOrEmpty(deltaText)
            ? 0f
            : graphics.MeasureString(deltaText, deltaFont, Size.Empty, format).Width;
    }

    private static void DrawTimeRow(
        Graphics graphics,
        OverlayRenderContext context,
        Rectangle bounds,
        string label,
        string value,
        SplitComparison comparison,
        bool showComparison,
        string outlineStyle,
        Font labelFont,
        Font valueFont,
        Font deltaFont,
        float reservedGap,
        TimeSpan animationDuration,
        TimeSpan elapsed,
        float opacity,
        string deltaHighlightStyle)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        string deltaText = GetDeltaText(context.Settings, comparison, showComparison);
        bool isAhead = SplitCompletionOutlineStyles.Normalize(outlineStyle) != SplitCompletionOutlineStyles.None &&
            comparison.Delta is TimeSpan aheadDelta &&
            aheadDelta < TimeSpan.Zero;

        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };

        int labelHeight = Math.Max(1, (int)Math.Ceiling(labelFont.GetHeight(graphics)));
        var labelRect = new Rectangle(bounds.Left, bounds.Top, bounds.Width, labelHeight);
        using var labelBrush = new SolidBrush(TextEffectRenderer.WithOpacity(context.Palette.SplitCompletionLabelText, opacity * 0.86f));
        TextEffectRenderer.DrawText(
            graphics,
            label,
            labelFont,
            labelBrush,
            labelRect,
            ContentAlignment.MiddleCenter);

        SizeF valueSize = graphics.MeasureString(value, valueFont, bounds.Size, format);
        float gap = string.IsNullOrEmpty(deltaText) ? 0f : reservedGap;
        float startX = bounds.Left + Math.Max(0f, (bounds.Width - valueSize.Width) / 2f);
        FontMetrics valueMetrics = OverlayTextMetrics.GetFontMetrics(graphics, valueFont);
        float valueTextHeight = valueMetrics.Ascent + valueMetrics.Descent;
        float valueBaselineY = bounds.Top + labelHeight + Math.Max(0f, (bounds.Height - labelHeight - valueTextHeight) / 2f) + valueMetrics.Ascent;
        float valueY = valueBaselineY - valueMetrics.Ascent;

        if (isAhead)
        {
            TextEffectRenderer.DrawOutlinedString(
                graphics,
                value,
                valueFont,
                context.Palette.SplitCompletionTimeText,
                startX,
                valueY,
                format,
                elapsed,
                context.Settings.SplitCompletionOutlineThicknessPercent,
                outlineStyle,
                opacity);
        }
        else
        {
            TextEffectRenderer.DrawString(
                graphics,
                value,
                valueFont,
                context.Palette.SplitCompletionTimeText,
                startX,
                valueY,
                format,
                opacity);
        }

        if (!string.IsNullOrEmpty(deltaText))
        {
            Color deltaColor = OverlayColorMath.GetDeltaComparisonColor(
                context.Settings,
                comparison,
                context.Palette,
                context.Settings.EnableCurrentDeltaGradientColor);
            if (context.Settings.ShowSegmentBestDeltaHighlight &&
                comparison.Delta is TimeSpan deltaValue &&
                deltaValue < TimeSpan.Zero)
            {
                deltaColor = SegmentBestDeltaHighlightStyles.Apply(deltaColor, deltaHighlightStyle, elapsed.TotalSeconds);
            }

            SplitCompletionDeltaMotion deltaMotion = GetDeltaMotion(
                elapsed,
                animationDuration,
                GetDeltaSlideDistance(deltaFont.Size));
            float deltaX = startX + valueSize.Width + gap + deltaMotion.OffsetX;
            float deltaY = TextEffectRenderer.AlignTextPathBottom(
                graphics,
                value,
                valueFont,
                startX,
                valueY,
                deltaText,
                deltaFont,
                deltaX,
                valueY,
                format);
            TextEffectRenderer.DrawString(
                graphics,
                deltaText,
                deltaFont,
                deltaColor,
                deltaX,
                deltaY,
                format,
                opacity * deltaMotion.Opacity);
        }
    }
}
