using System.Drawing;

namespace TerrariaSplit;

internal static class SplitRenderData
{
    public static string FormatReferenceTime(AppSettings settings, BossSplitDefinition definition)
    {
        return settings.TryGetReferenceSplit(definition, out TimeSpan split)
            ? TimeText.FormatSplit(split)
            : "--";
    }

    public static SplitComparison GetSplitComparison(
        AppSettings settings,
        SplitTimerPhase timerPhase,
        TimeSpan timerElapsed,
        BossSplitStatus status,
        bool isCurrent)
    {
        if (!settings.TryGetReferenceSplit(status.Definition, out TimeSpan referenceTime))
        {
            return SplitComparison.Empty;
        }

        if (status.Time is TimeSpan splitTime)
        {
            return new SplitComparison(splitTime - referenceTime, ShowDelta: true);
        }

        if (!isCurrent || timerPhase == SplitTimerPhase.NotStarted)
        {
            return SplitComparison.Empty;
        }

        TimeSpan runningDelta = timerElapsed - referenceTime;
        TimeSpan visibleDeltaDistance = TimeSpan.FromSeconds(settings.EarlyDeltaTimeSeconds);
        bool showRunningDelta = settings.ShowEarlyDeltaTime && runningDelta >= -visibleDeltaDistance;
        return new SplitComparison(runningDelta, showRunningDelta);
    }

    public static string FormatSplitDelta(AppSettings settings, SplitComparison comparison)
    {
        return comparison.ShowDelta && comparison.Delta is TimeSpan delta
            ? TimeText.FormatDelta(delta, settings.EnableDynamicDeltaTimeUnits)
            : string.Empty;
    }

    public static SplitComparison GetReferenceSplitComparison(
        AppSettings settings,
        BossSplitDefinition definition,
        TimeSpan splitTime)
    {
        if (!settings.TryGetReferenceSplit(definition, out TimeSpan referenceSplit))
        {
            return SplitComparison.Empty;
        }

        return new SplitComparison(splitTime - referenceSplit, ShowDelta: true);
    }

    public static SplitComparison GetPersonalBestSegmentComparison(
        AppSettings settings,
        BossSplitDefinition definition,
        TimeSpan segmentTime)
    {
        if (!TryGetPersonalBestSegment(settings, definition, out TimeSpan personalBestSegment))
        {
            return SplitComparison.Empty;
        }

        return new SplitComparison(segmentTime - personalBestSegment, ShowDelta: true);
    }

    public static bool TryGetPersonalBestSegment(
        AppSettings settings,
        BossSplitDefinition definition,
        out TimeSpan segment)
    {
        segment = TimeSpan.Zero;
        string groupKey = GetSplitCompletionGroupKey(definition);
        if (settings.PersonalBestSegmentTimes.TryGetValue(groupKey, out string? value) &&
            TimeText.TryParse(value, out TimeSpan parsed))
        {
            segment = parsed;
            return true;
        }

        if (settings.PersonalBestSegmentTimes.TryGetValue(definition.Name, out value) &&
            TimeText.TryParse(value, out parsed))
        {
            segment = parsed;
            return true;
        }

        return false;
    }

    public static bool TryGetCompletedSegmentTime(
        IReadOnlyList<BossSplitStatus> statuses,
        int completedIndex,
        out TimeSpan segmentTime)
    {
        segmentTime = TimeSpan.Zero;
        if (completedIndex < 0 ||
            completedIndex >= statuses.Count ||
            statuses[completedIndex].Time is not TimeSpan splitTime)
        {
            return false;
        }

        TimeSpan previousSplitTime = TimeSpan.Zero;
        for (int i = completedIndex - 1; i >= 0; i--)
        {
            if (statuses[i].Time is TimeSpan previousTime)
            {
                previousSplitTime = previousTime;
                break;
            }
        }

        segmentTime = splitTime - previousSplitTime;
        if (segmentTime < TimeSpan.Zero)
        {
            segmentTime = TimeSpan.Zero;
        }

        return true;
    }

    public static string GetSplitCompletionGroupKey(BossSplitDefinition definition)
    {
        return string.Join("+", definition.BossIds);
    }

    public static string GetSplitCompletionOutlineStyle(Dictionary<string, string> values, string groupKey)
    {
        return values.TryGetValue(groupKey, out string? style)
            ? SplitCompletionOutlineStyles.Normalize(style)
            : SplitCompletionOutlineStyles.Rainbow;
    }

    public static bool IsSplitCompletionSplitComparisonEnabled(AppSettings settings, string groupKey)
    {
        return !settings.SplitCompletionSplitComparisons.TryGetValue(groupKey, out bool enabled) || enabled;
    }

    public static bool IsSplitCompletionSegmentComparisonEnabled(AppSettings settings, string groupKey)
    {
        return !settings.SplitCompletionSegmentComparisons.TryGetValue(groupKey, out bool enabled) || enabled;
    }

    public static string GetSegmentBestDeltaHighlightStyle(AppSettings settings, string groupKey)
    {
        return settings.SegmentBestDeltaHighlightStyles.TryGetValue(groupKey, out string? style)
            ? SegmentBestDeltaHighlightStyles.Normalize(style)
            : SegmentBestDeltaHighlightStyles.Aurora;
    }
}

internal static class OverlayTextStyles
{
    public static TextRenderStyle GetReferenceTextStyle(
        AppSettings settings,
        UiPalette palette,
        bool active)
    {
        return active
            ? CreateReferenceTextStyle(
                settings,
                palette.ActiveReferenceText,
                palette.ActiveReferenceTextOutline,
                palette.ActiveReferenceTextShadow)
            : CreateReferenceTextStyle(
                settings,
                palette.ReferenceText,
                palette.ReferenceTextOutline,
                palette.ReferenceTextShadow);
    }

    public static TextRenderStyle GetSplitTextStyle(AppSettings settings, UiPalette palette)
    {
        return new TextRenderStyle(
            palette.SplitText,
            palette.SplitTextOutline,
            palette.SplitTextShadow,
            settings.TextEffects.TimeShadowPercent,
            settings.TextEffects.TimeOutlineThicknessPercent);
    }

    public static TextRenderStyle GetDeltaTextStyle(
        AppSettings settings,
        SplitComparison comparison,
        UiPalette palette)
    {
        bool ahead = comparison.Delta is TimeSpan delta && delta < TimeSpan.Zero;
        return ahead
            ? CreateDeltaTextStyle(
                settings,
                palette.DeltaAheadText,
                palette.DeltaAheadTextOutline,
                palette.DeltaAheadTextShadow)
            : CreateDeltaTextStyle(
                settings,
                palette.DeltaBehindText,
                palette.DeltaBehindTextOutline,
                palette.DeltaBehindTextShadow);
    }

    public static TextRenderStyle GetTimerTextStyle(
        AppSettings settings,
        IReadOnlyList<BossSplitStatus> statuses,
        int currentSplitIndex,
        SplitTimerPhase timerPhase,
        TimeSpan timerElapsed,
        UiPalette palette,
        bool milliseconds)
    {
        if (timerPhase == SplitTimerPhase.NotStarted)
        {
            return CreateTimerTextStyle(settings, palette.TimerText, palette.TimerTextOutline, palette.TimerTextShadow, milliseconds);
        }

        if (TryGetCompletedMoonLordStatus(statuses, out BossSplitStatus moonLordStatus, out TimeSpan moonLordTime) &&
            settings.TryGetReferenceSplit(moonLordStatus.Definition, out TimeSpan moonLordReference))
        {
            return moonLordTime < moonLordReference
                ? CreateTimerTextStyle(
                    settings,
                    palette.TimerRecordText,
                    palette.TimerRecordTextOutline,
                    palette.TimerRecordTextShadow,
                    milliseconds)
                : CreateTimerTextStyle(
                    settings,
                    palette.TimerNoRecordText,
                    palette.TimerNoRecordTextOutline,
                    palette.TimerNoRecordTextShadow,
                    milliseconds);
        }

        if (statuses.Count > 0 && statuses[^1].Time is TimeSpan finalTime)
        {
            if (settings.TryGetReferenceSplit(statuses[^1].Definition, out TimeSpan finalReference) &&
                finalTime < finalReference)
            {
                return CreateTimerTextStyle(
                    settings,
                    palette.TimerRecordText,
                    palette.TimerRecordTextOutline,
                    palette.TimerRecordTextShadow,
                    milliseconds);
            }

            if (settings.TryGetReferenceSplit(statuses[^1].Definition, out finalReference) &&
                settings.EnableTimerGradientColor)
            {
                return GetTimerGradientTextStyle(settings, finalTime - finalReference, palette, milliseconds);
            }

            return timerPhase == SplitTimerPhase.Paused
                ? CreateTimerTextStyle(
                    settings,
                    palette.TimerPausedText,
                    palette.TimerPausedTextOutline,
                    palette.TimerPausedTextShadow,
                    milliseconds)
                : CreateTimerTextStyle(
                    settings,
                    palette.TimerBehindText,
                    palette.TimerBehindTextOutline,
                    palette.TimerBehindTextShadow,
                    milliseconds);
        }

        if (timerPhase == SplitTimerPhase.Paused)
        {
            return CreateTimerTextStyle(
                settings,
                palette.TimerPausedText,
                palette.TimerPausedTextOutline,
                palette.TimerPausedTextShadow,
                milliseconds);
        }

        if (currentSplitIndex < statuses.Count &&
            settings.TryGetReferenceSplit(statuses[currentSplitIndex].Definition, out TimeSpan currentReference))
        {
            if (settings.EnableTimerGradientColor)
            {
                return GetTimerGradientTextStyle(settings, timerElapsed - currentReference, palette, milliseconds);
            }

            return timerElapsed <= currentReference
                ? CreateTimerTextStyle(
                    settings,
                    palette.TimerAheadText,
                    palette.TimerAheadTextOutline,
                    palette.TimerAheadTextShadow,
                    milliseconds)
                : CreateTimerTextStyle(
                    settings,
                    palette.TimerBehindText,
                    palette.TimerBehindTextOutline,
                    palette.TimerBehindTextShadow,
                    milliseconds);
        }

        return CreateTimerTextStyle(settings, palette.TimerText, palette.TimerTextOutline, palette.TimerTextShadow, milliseconds);
    }

    private static TextRenderStyle GetTimerGradientTextStyle(
        AppSettings settings,
        TimeSpan delta,
        UiPalette palette,
        bool milliseconds)
    {
        TextRenderStyle style = delta < TimeSpan.Zero
            ? CreateTimerTextStyle(
                settings,
                palette.TimerAheadText,
                palette.TimerAheadTextOutline,
                palette.TimerAheadTextShadow,
                milliseconds)
            : delta > TimeSpan.Zero
                ? CreateTimerTextStyle(
                    settings,
                    palette.TimerBehindText,
                    palette.TimerBehindTextOutline,
                    palette.TimerBehindTextShadow,
                    milliseconds)
                : CreateTimerTextStyle(
                    settings,
                    palette.TimerText,
                    palette.TimerTextOutline,
                    palette.TimerTextShadow,
                    milliseconds);
        return style with
        {
            Fill = OverlayColorMath.GetGradientDeltaColor(
                settings,
                delta,
                palette.TimerAheadText,
                palette.TimerText,
                palette.TimerBehindText)
        };
    }

    private static TextRenderStyle CreateReferenceTextStyle(
        AppSettings settings,
        Color fill,
        Color outline,
        Color shadow)
    {
        return new TextRenderStyle(
            fill,
            outline,
            shadow,
            settings.TextEffects.TimeShadowPercent,
            settings.TextEffects.TimeOutlineThicknessPercent);
    }

    private static TextRenderStyle CreateDeltaTextStyle(
        AppSettings settings,
        Color fill,
        Color outline,
        Color shadow)
    {
        return new TextRenderStyle(
            fill,
            outline,
            shadow,
            settings.TextEffects.DeltaShadowPercent,
            settings.TextEffects.DeltaOutlineThicknessPercent);
    }

    private static TextRenderStyle CreateTimerTextStyle(
        AppSettings settings,
        Color fill,
        Color outline,
        Color shadow,
        bool milliseconds)
    {
        return new TextRenderStyle(
            fill,
            outline,
            shadow,
            milliseconds
                ? settings.TextEffects.TimerMillisecondsShadowPercent
                : settings.TextEffects.TimerShadowPercent,
            milliseconds
                ? settings.TextEffects.TimerMillisecondsOutlineThicknessPercent
                : settings.TextEffects.TimerOutlineThicknessPercent);
    }

    private static bool TryGetCompletedMoonLordStatus(
        IReadOnlyList<BossSplitStatus> statuses,
        out BossSplitStatus moonLordStatus,
        out TimeSpan moonLordTime)
    {
        BossSplitStatus? match = statuses.FirstOrDefault(status =>
            !status.IsSkipped &&
            status.Time is not null &&
            status.Definition.BossIds.Any(bossId => string.Equals(
                bossId,
                BossSplitDefinitions.MoonLord,
                StringComparison.OrdinalIgnoreCase)));
        if (match?.Time is TimeSpan time)
        {
            moonLordStatus = match;
            moonLordTime = time;
            return true;
        }

        moonLordStatus = null!;
        moonLordTime = TimeSpan.Zero;
        return false;
    }
}

internal static class OverlayColorMath
{
    public static Color GetDeltaComparisonColor(
        AppSettings settings,
        SplitComparison comparison,
        UiPalette palette,
        bool enableGradient)
    {
        TimeSpan? delta = comparison.Delta;
        if (delta is null)
        {
            return palette.DeltaBehindText;
        }

        if (enableGradient)
        {
            return GetGradientDeltaColor(
                settings,
                delta.Value,
                palette.DeltaAheadText,
                palette.TimerText,
                palette.DeltaBehindText);
        }

        if (TimeText.IsDeltaDisplayedAsZero(delta.Value, settings.EnableDynamicDeltaTimeUnits))
        {
            return palette.DeltaBehindText;
        }

        if (delta < TimeSpan.Zero)
        {
            return palette.DeltaAheadText;
        }

        if (delta > TimeSpan.Zero)
        {
            return palette.DeltaBehindText;
        }

        return palette.DeltaBehindText;
    }

    public static Color GetGradientDeltaColor(
        AppSettings settings,
        TimeSpan delta,
        Color aheadColor,
        Color baseColor,
        Color behindColor)
    {
        if (delta == TimeSpan.Zero)
        {
            return baseColor;
        }

        float thresholdSeconds = Math.Max(1, settings.DeltaGradientThresholdSeconds);
        float magnitude = Math.Min(1f, (float)(Math.Abs(delta.TotalSeconds) / thresholdSeconds));
        float amount = DeltaGradientCurves.Evaluate(settings.DeltaGradientCurve, magnitude);
        return delta < TimeSpan.Zero
            ? BlendColor(baseColor, aheadColor, amount)
            : BlendColor(baseColor, behindColor, amount);
    }

    private static Color BlendColor(Color from, Color to, float amount)
    {
        float t = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            Lerp(from.R, to.R, t),
            Lerp(from.G, to.G, t),
            Lerp(from.B, to.B, t));
    }

    private static int Lerp(int from, int to, float amount)
    {
        return Math.Clamp((int)Math.Round(from + (to - from) * amount), 0, 255);
    }
}
