using System.Drawing;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Rendering;

internal static class SplitCompletionAnimationRenderer
{
    public const int ReservedRowCount = 7;
    private static readonly TimeSpan SplitCompletionFadeDuration = TimeSpan.FromSeconds(0.45);
    private static readonly TimeSpan SplitCompletionDeltaIntroGap = TimeSpan.FromSeconds(0.06);
    private const float SplitCompletionLabelFontRatio = 0.64f;
    private const float SplitCompletionDeltaFontRatio = 0.82f;
    private const float SplitCompletionIconTopRatio = 0.05f;
    private const float SplitCompletionTextLeftPaddingRatio = 0.02f;
    private const float SplitCompletionTextRightPaddingRatio = 0.005f;
    private const float SplitCompletionIconTextGapRatio = 0.012f;
    private const float SplitCompletionDeltaGapRatio = 0.28f;
    private const float SplitCompletionRowGapRatio = 0.16f;
    private const float SplitCompletionDeltaOutroLeadRatio = 0.55f;
    private const float SplitCompletionDeltaIntroDurationRatio = 0.85f;
    private const float SplitCompletionDeltaSlideDistanceRatio = 0.55f;
    private const float SplitCompletionDeltaMinSlideDistance = 10f;
    private const float SplitCompletionDeltaMaxSlideDistance = 20f;

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
        return TimeSpan.FromSeconds(Math.Clamp(settings.Overlay.SplitCompletionAnimationDurationSeconds, 2f, 20f));
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

        Rectangle listBounds = GetAnimationBounds(context);
        if (listBounds.Width <= 0 || listBounds.Height <= 0)
        {
            return;
        }

        float centerX = GetCenterX(graphics, context, resources, context.Layout.TimerRect, listBounds);
        DrawIcon(graphics, context, resources, listBounds, centerX, animation, elapsed, opacity);
        DrawTimes(graphics, context, resources, listBounds, centerX, animation, elapsed, opacity);
    }

    public static Rectangle GetAnimationBounds(OverlayRenderContext context)
    {
        (int firstRowIndex, int lastRowIndex) = GetRenderedRowRange(context);
        Rectangle firstVisibleRow = context.Layout.GetRowRect(firstRowIndex);
        Rectangle lastVisibleRow = context.Layout.GetRowRect(lastRowIndex);
        int animationHeight = firstVisibleRow.Height * ReservedRowCount +
            context.Layout.RowGap * Math.Max(0, ReservedRowCount - 1);
        int visibleCenterY = firstVisibleRow.Top + (lastVisibleRow.Bottom - firstVisibleRow.Top) / 2;
        int animationTop = visibleCenterY - animationHeight / 2;
        return new Rectangle(firstVisibleRow.X, animationTop, firstVisibleRow.Width, animationHeight);
    }

    private static (int FirstRowIndex, int LastRowIndex) GetRenderedRowRange(OverlayRenderContext context)
    {
        IReadOnlyList<SplitDisplayRow> rows = SplitDisplayRows.Build(
            context.Settings,
            context.Statuses,
            context.CurrentSplitIndex,
            context.VisibleStatusRowCount,
            context.IgnoreVisibleGroupLimit);
        if (rows.Count == 0)
        {
            return (0, Math.Max(ReservedRowCount, context.VisibleStatusRowCount) - 1);
        }

        int first = rows.Min(row => row.RowIndex);
        int last = rows.Max(row => row.RowIndex);
        int span = last - first + 1;
        if (span >= ReservedRowCount)
        {
            return (first, last);
        }

        int totalRows = Math.Max(Math.Max(context.VisibleStatusRowCount, last + 1), ReservedRowCount);
        int start = first - (ReservedRowCount - span) / 2;
        start = Math.Clamp(start, 0, Math.Max(0, totalRows - ReservedRowCount));
        return (start, start + ReservedRowCount - 1);
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
            DrawIconFrame(graphics, context, resources, animation, 0, iconRect, opacity);
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
            iconIndex,
            iconRect,
            opacity * (1f - fadeProgress));

        if (hasNextIcon && fadeProgress > 0.01f)
        {
            DrawIconFrame(
                graphics,
                context,
                resources,
                animation,
                iconIndex + 1,
                iconRect,
                opacity * fadeProgress);
        }
    }

    private static Rectangle GetIconRect(OverlayRenderContext context, Rectangle listBounds, float centerX)
    {
        int maxIconSize = Math.Max(1, Math.Min((int)(listBounds.Width * 0.475f), (int)(listBounds.Height * 0.40f)));
        int minIconSize = Math.Min(context.ScaleInt(90), maxIconSize);
        int iconSize = Math.Clamp(context.ScaleInt(188), minIconSize, maxIconSize);
        int iconX = (int)Math.Round(centerX - iconSize / 2f, MidpointRounding.AwayFromZero);
        iconX = Math.Clamp(iconX, listBounds.Left, listBounds.Right - iconSize);
        return new Rectangle(
            iconX,
            listBounds.Top + Math.Max(0, (int)Math.Round(listBounds.Height * SplitCompletionIconTopRatio)),
            iconSize,
            iconSize);
    }

    private static void DrawIconFrame(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        SplitCompletionAnimation animation,
        int iconIndex,
        Rectangle iconRect,
        float opacity)
    {
        if (opacity <= 0.01f)
        {
            return;
        }

        IconPair icon = resources.BossIcons.Load(animation.Definition, iconIndex, context.Settings);
        resources.BossIcons.TrackRendered(icon);
        TextEffectRenderer.DrawImage(graphics, icon.GetLitImage(context.NowUtc), iconRect, opacity);
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
        int leftPadding = Math.Max(context.ScaleInt(6), (int)Math.Round(listBounds.Width * SplitCompletionTextLeftPaddingRatio));
        int rightPadding = Math.Max(context.ScaleInt(1), (int)Math.Round(listBounds.Width * SplitCompletionTextRightPaddingRatio));
        int top = iconRect.Bottom + Math.Max(context.ScaleInt(4), (int)Math.Round(listBounds.Height * SplitCompletionIconTextGapRatio));
        int bottom = listBounds.Bottom - context.ScaleInt(1);
        float leftLimit = listBounds.Left + leftPadding;
        float rightLimit = listBounds.Right - rightPadding;
        float textCenterX = Math.Clamp(centerX, leftLimit, rightLimit);
        var textBounds = Rectangle.FromLTRB(
            (int)Math.Floor(leftLimit),
            Math.Min(top, bottom),
            (int)Math.Ceiling(rightLimit),
            bottom);
        if (textBounds.Width <= 0 || textBounds.Height <= 0)
        {
            return;
        }

        var cacheKey = new SplitCompletionAnimationTextCacheKey(
            animation,
            textBounds,
            textCenterX,
            context.ScaleFactor,
            context.Settings.Overlay.Columns.Timer.FontFamily,
            context.Settings.General.Language,
            context.Settings.Overlay.EnableDynamicDeltaTimeUnits,
            graphics.DpiX,
            graphics.DpiY);
        SplitCompletionAnimationTextCache textCache = resources.SplitCompletionAnimationText;
        if (!textCache.TryGet(cacheKey, out SplitCompletionAnimationTextResources textResources))
        {
            textResources = CreateTextResources(
                graphics,
                context,
                animation,
                cacheKey,
                textCenterX - leftLimit,
                rightLimit - textCenterX,
                textCache.FontFactory);
            textCache.Store(textResources);
        }

        Font labelFont = textResources.LabelFont;
        Font valueFont = textResources.ValueFont;
        Font deltaFont = textResources.DeltaFont;
        TimeSpan animationDuration = GetAnimationDuration(context.Settings);

        int labelHeight = Math.Max(1, (int)Math.Ceiling(labelFont.GetHeight(graphics)));
        int valueHeight = Math.Max(1, (int)Math.Ceiling(valueFont.GetHeight(graphics)) + context.ScaleInt(2));
        int rowHeight = labelHeight + valueHeight + context.ScaleInt(2);
        float reservedGap = string.IsNullOrEmpty(textResources.SegmentDelta) && string.IsNullOrEmpty(textResources.SplitDelta)
            ? 0f
            : Math.Max(4f, valueFont.Size * SplitCompletionDeltaGapRatio);
        int gap = Math.Max(2, (int)Math.Round(valueFont.Size * SplitCompletionRowGapRatio));
        int totalHeight = rowHeight * 2 + gap;
        int startY = textBounds.Top + Math.Max(0, (textBounds.Height - totalHeight) / 2);

        var segmentRect = new Rectangle(textBounds.Left, startY, textBounds.Width, rowHeight);
        var splitRect = new Rectangle(textBounds.Left, startY + rowHeight + gap, textBounds.Width, rowHeight);

        DrawTimeRow(
            graphics,
            context,
            segmentRect,
            textCenterX,
            textResources.SegmentLabel,
            textResources.SegmentValue,
            textResources.SegmentDelta,
            animation.PersonalBestSegmentComparison,
            animation.SegmentTimeOutlineStyle,
            context.Palette.SplitCompletionSegmentLabelText,
            context.Palette.SplitCompletionSegmentTimeText,
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
            textCenterX,
            textResources.SplitLabel,
            textResources.SplitValue,
            textResources.SplitDelta,
            animation.ReferenceSplitComparison,
            animation.SplitTimeOutlineStyle,
            context.Palette.SplitCompletionLabelText,
            context.Palette.SplitCompletionTimeText,
            labelFont,
            valueFont,
            deltaFont,
            reservedGap,
            animationDuration,
            elapsed,
            opacity,
            SegmentBestDeltaHighlightStyles.None);
    }

    private static SplitCompletionAnimationTextResources CreateTextResources(
        Graphics graphics,
        OverlayRenderContext context,
        SplitCompletionAnimation animation,
        SplitCompletionAnimationTextCacheKey cacheKey,
        float availableLeftWidth,
        float availableRightWidth,
        IUiFontFactory fontFactory)
    {
        string segmentValue = SplitTimerFormatter.Format(animation.SegmentTime);
        string segmentDelta = GetDeltaText(
            context.Settings,
            animation.PersonalBestSegmentComparison,
            animation.ShowSegmentComparison);
        string splitValue = SplitTimerFormatter.Format(animation.SplitTime);
        string splitDelta = GetDeltaText(
            context.Settings,
            animation.ReferenceSplitComparison,
            animation.ShowSplitComparison);
        float valueSize = GetValueFontSize(
            graphics,
            availableLeftWidth,
            availableRightWidth,
            cacheKey.TextBounds.Height,
            segmentValue,
            segmentDelta,
            splitValue,
            splitDelta,
            cacheKey.Scale,
            cacheKey.FontFamily,
            fontFactory);

        Font? labelFont = null;
        Font? valueFont = null;
        Font? deltaFont = null;
        try
        {
            labelFont = OverlayTextMetrics.CreatePixelFont(
                valueSize * SplitCompletionLabelFontRatio,
                FontStyle.Bold,
                cacheKey.FontFamily,
                fontFactory);
            valueFont = OverlayTextMetrics.CreatePixelFont(
                valueSize,
                FontStyle.Bold,
                cacheKey.FontFamily,
                fontFactory);
            deltaFont = OverlayTextMetrics.CreatePixelFont(
                valueSize * SplitCompletionDeltaFontRatio,
                FontStyle.Bold,
                cacheKey.FontFamily,
                fontFactory);
            return new SplitCompletionAnimationTextResources(
                cacheKey,
                Localizer.Get("Segment time", context.Settings),
                segmentValue,
                segmentDelta,
                Localizer.Get("Cumulative time", context.Settings),
                splitValue,
                splitDelta,
                labelFont,
                valueFont,
                deltaFont);
        }
        catch
        {
            labelFont?.Dispose();
            valueFont?.Dispose();
            deltaFont?.Dispose();
            throw;
        }
    }

    private static float GetCenterX(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        Rectangle timerRect,
        Rectangle listBounds)
    {
        if (!context.Settings.Overlay.Columns.Timer.Show && !context.Settings.Overlay.Columns.TimerMilliseconds.Show)
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
            ? TimeText.FormatDelta(delta, settings.Overlay.EnableDynamicDeltaTimeUnits)
            : string.Empty;
    }

    private static float GetValueFontSize(
        Graphics graphics,
        float availableLeftWidth,
        float availableRightWidth,
        int availableHeight,
        string firstValue,
        string firstDelta,
        string secondValue,
        string secondDelta,
        float scale,
        string fontFamily,
        IUiFontFactory fontFactory)
    {
        if (availableLeftWidth <= 0f || availableRightWidth <= 0f || availableHeight <= 0)
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
                availableLeftWidth,
                availableRightWidth,
                availableHeight,
                mid,
                firstValue,
                firstDelta,
                secondValue,
                secondDelta,
                fontFamily,
                fontFactory))
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
        float availableLeftWidth,
        float availableRightWidth,
        int availableHeight,
        float valueSize,
        string firstValue,
        string firstDelta,
        string secondValue,
        string secondDelta,
        string fontFamily,
        IUiFontFactory fontFactory)
    {
        using var labelFont = OverlayTextMetrics.CreatePixelFont(
            valueSize * SplitCompletionLabelFontRatio,
            FontStyle.Bold,
            fontFamily,
            fontFactory);
        using var valueFont = OverlayTextMetrics.CreatePixelFont(
            valueSize,
            FontStyle.Bold,
            fontFamily,
            fontFactory);
        using var deltaFont = OverlayTextMetrics.CreatePixelFont(
            valueSize * SplitCompletionDeltaFontRatio,
            FontStyle.Bold,
            fontFamily,
            fontFactory);
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
            ? Math.Max(4f, valueFont.Size * SplitCompletionDeltaGapRatio)
            : 0f;
        float requiredLeftWidth = Math.Max(firstValueWidth / 2f, secondValueWidth / 2f);
        float requiredRightWidth = Math.Max(
            firstValueWidth / 2f + (firstDeltaWidth > 0f ? deltaGap + firstDeltaWidth + slidePadding : 0f),
            secondValueWidth / 2f + (secondDeltaWidth > 0f ? deltaGap + secondDeltaWidth + slidePadding : 0f));
        float labelHeight = labelFont.GetHeight(graphics);
        float valueHeight = valueFont.GetHeight(graphics) + 2f;
        float rowHeight = labelHeight + valueHeight + 2f;
        float totalHeight = rowHeight * 2f + Math.Max(2f, valueFont.Size * SplitCompletionRowGapRatio);
        return requiredLeftWidth <= availableLeftWidth &&
            requiredRightWidth <= availableRightWidth &&
            totalHeight <= availableHeight;
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
        float centerX,
        string label,
        string value,
        string deltaText,
        SplitComparison comparison,
        string outlineStyle,
        Color labelColor,
        Color valueColor,
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

        bool isAhead = SplitCompletionOutlineStyles.Normalize(outlineStyle) != SplitCompletionOutlineStyles.None &&
            comparison.Delta is TimeSpan aheadDelta &&
            aheadDelta < TimeSpan.Zero;

        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };

        int labelHeight = Math.Max(1, (int)Math.Ceiling(labelFont.GetHeight(graphics)));
        float labelHalfWidth = Math.Max(0f, Math.Min(centerX - bounds.Left, bounds.Right - centerX));
        var labelRect = Rectangle.FromLTRB(
            (int)Math.Floor(centerX - labelHalfWidth),
            bounds.Top,
            (int)Math.Ceiling(centerX + labelHalfWidth),
            bounds.Top + labelHeight);
        using var labelBrush = new SolidBrush(TextEffectRenderer.WithOpacity(labelColor, opacity * 0.86f));
        TextEffectRenderer.DrawText(
            graphics,
            label,
            labelFont,
            labelBrush,
            labelRect,
            ContentAlignment.MiddleCenter);

        SizeF valueSize = graphics.MeasureString(value, valueFont, bounds.Size, format);
        float gap = string.IsNullOrEmpty(deltaText) ? 0f : reservedGap;
        float startX = centerX - valueSize.Width / 2f;
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
                valueColor,
                startX,
                valueY,
                format,
                elapsed,
                context.Settings.Overlay.SplitCompletionOutlineThicknessPercent,
                outlineStyle,
                opacity);
        }
        else
        {
            TextEffectRenderer.DrawString(
                graphics,
                value,
                valueFont,
                valueColor,
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
                context.Settings.Overlay.EnableCurrentDeltaGradientColor);
            if (context.Settings.Overlay.ShowSegmentBestDeltaHighlight &&
                comparison.Delta is TimeSpan deltaValue &&
                deltaValue < TimeSpan.Zero)
            {
                deltaColor = SegmentBestDeltaHighlightColorMath.Apply(deltaColor, deltaHighlightStyle, elapsed.TotalSeconds);
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
