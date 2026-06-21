using System.Drawing;

namespace TerrariaSplit.UI.Rendering;

internal static class SplitRenderData
{
    public static SplitDefinition GetDisplayDefinition(SplitStatusSnapshot status)
    {
        return GetDisplayDefinition(status, facts: null);
    }

    public static SplitDefinition GetDisplayDefinition(SplitStatusSnapshot status, TerrariaGameFacts? facts)
    {
        if (status.Definition.IconLightingConditions.Count > 0)
        {
            return status.Definition;
        }

        IReadOnlyList<string> visibleFactKeys = GetVisibleIconFactKeys(status, facts);
        if (visibleFactKeys.Count == 0)
        {
            return status.Definition;
        }

        HashSet<string> visibleTargetIds = visibleFactKeys
            .Select(factKey => SplitCatalog.TryGetTargetByFactKey(factKey, out SplitTargetDefinition target)
                ? target.Id
                : string.Empty)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (visibleTargetIds.Count == 0)
        {
            return status.Definition;
        }

        var iconFileNames = new List<string>();
        var iconKeys = new List<string>();
        int count = Math.Min(status.Definition.IconFileNames.Count, status.Definition.IconKeys.Count);
        for (int i = 0; i < count; i++)
        {
            string iconKey = status.Definition.IconKeys[i];
            if (!visibleTargetIds.Contains(iconKey))
            {
                continue;
            }

            iconKeys.Add(iconKey);
            iconFileNames.Add(status.Definition.IconFileNames[i]);
        }

        if (iconKeys.Count == 0)
        {
            return status.Definition;
        }

        return status.Definition with
        {
            IconFileNames = iconFileNames,
            IconKeys = iconKeys,
            TargetIds = iconKeys
        };
    }

    private static IReadOnlyList<string> GetVisibleIconFactKeys(SplitStatusSnapshot status, TerrariaGameFacts? facts)
    {
        if ((status.IsCompleted || status.IsSkipped) && status.CompletedFactKeys.Count > 0)
        {
            return status.CompletedFactKeys;
        }

        string kind = SplitConditionKind.Normalize(status.Definition.Condition.Kind);
        if (facts is not null &&
            kind is SplitConditionKind.Any or SplitConditionKind.AtLeast &&
            status.Definition.Condition.Evaluate(facts) == SplitConditionResult.True)
        {
            return status.Definition.Condition.GetSatisfiedFactKeys(facts);
        }

        return [];
    }

    public static string FormatReferenceTime(AppSettings settings, SplitDefinition definition)
    {
        return ReferenceSplitSetService.TryGetReferenceSplit(settings, definition, out TimeSpan split)
            ? TimeText.FormatSplit(split)
            : "--";
    }

    public static SplitComparison GetSplitComparison(
        AppSettings settings,
        SplitTimerPhase timerPhase,
        TimeSpan timerElapsed,
        SplitStatusSnapshot status,
        bool isCurrent)
    {
        if (!ReferenceSplitSetService.TryGetReferenceSplit(settings, status.Definition, out TimeSpan referenceTime))
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
        TimeSpan visibleDeltaDistance = TimeSpan.FromSeconds(settings.Overlay.EarlyDeltaTimeSeconds);
        bool showRunningDelta = settings.Overlay.ShowEarlyDeltaTime && runningDelta >= -visibleDeltaDistance;
        return new SplitComparison(runningDelta, showRunningDelta);
    }

    public static string FormatSplitDelta(AppSettings settings, SplitComparison comparison)
    {
        return comparison.ShowDelta && comparison.Delta is TimeSpan delta
            ? TimeText.FormatDelta(delta, settings.Overlay.EnableDynamicDeltaTimeUnits)
            : string.Empty;
    }

    public static SplitComparison GetReferenceSplitComparison(
        AppSettings settings,
        SplitDefinition definition,
        TimeSpan splitTime)
    {
        if (!ReferenceSplitSetService.TryGetReferenceSplit(settings, definition, out TimeSpan referenceSplit))
        {
            return SplitComparison.Empty;
        }

        return new SplitComparison(splitTime - referenceSplit, ShowDelta: true);
    }

    public static SplitComparison GetPersonalBestSegmentComparison(
        AppSettings settings,
        SplitDefinition definition,
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
        SplitDefinition definition,
        out TimeSpan segment)
    {
        return SplitTimingComparisons.TryGetPersonalBestSegment(settings, definition, out segment);
    }

    public static bool TryGetCompletedSegmentTime(
        AppSettings settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        int completedIndex,
        out TimeSpan segmentTime)
    {
        return SplitTimingComparisons.TryGetCompletedSegmentTime(settings, statuses, completedIndex, out segmentTime);
    }

    public static string GetSplitCompletionGroupKey(AppSettings settings, SplitDefinition definition)
    {
        return SplitTimingComparisons.GetSplitCompletionGroupKey(settings, definition);
    }

    public static string GetSplitCompletionOutlineStyle(Dictionary<string, string> values, string groupKey)
    {
        return values.TryGetValue(groupKey, out string? style)
            ? SplitCompletionOutlineStyles.Normalize(style)
            : SplitCompletionOutlineStyles.Rainbow;
    }

    public static bool IsSplitCompletionSplitComparisonEnabled(AppSettings settings, string groupKey)
    {
        return !settings.Overlay.SplitCompletionSplitComparisons.TryGetValue(groupKey, out bool enabled) || enabled;
    }

    public static bool IsSplitCompletionSegmentComparisonEnabled(AppSettings settings, string groupKey)
    {
        return !settings.Overlay.SplitCompletionSegmentComparisons.TryGetValue(groupKey, out bool enabled) || enabled;
    }

    public static string GetSegmentBestDeltaHighlightStyle(AppSettings settings, string groupKey)
    {
        return settings.Overlay.SegmentBestDeltaHighlightStyles.TryGetValue(groupKey, out string? style)
            ? SegmentBestDeltaHighlightStyles.Normalize(style)
            : SegmentBestDeltaHighlightStyles.Aurora;
    }
}

internal static class OverlayTextStyles
{
    public static float GetIconOpacity(AppSettings settings, bool attached = false)
    {
        return GetOpacity(attached
            ? settings.Overlay.TextEffects.AttachedIconOpacityPercent
            : settings.Overlay.TextEffects.IconOpacityPercent);
    }

    public static float GetTimeTextOpacity(AppSettings settings, bool attached = false)
    {
        return GetOpacity(attached
            ? settings.Overlay.TextEffects.AttachedTimeOpacityPercent
            : settings.Overlay.TextEffects.TimeOpacityPercent);
    }

    public static float GetDeltaTextOpacity(AppSettings settings, bool attached = false)
    {
        return GetOpacity(attached
            ? settings.Overlay.TextEffects.AttachedDeltaOpacityPercent
            : settings.Overlay.TextEffects.DeltaOpacityPercent);
    }

    public static float GetTimerTextOpacity(AppSettings settings, bool milliseconds)
    {
        return GetOpacity(milliseconds
            ? settings.Overlay.TextEffects.TimerMillisecondsOpacityPercent
            : settings.Overlay.TextEffects.TimerOpacityPercent);
    }

    public static TextRenderStyle GetReferenceTextStyle(
        AppSettings settings,
        UiPalette palette,
        bool active,
        bool attached = false)
    {
        return active
            ? CreateReferenceTextStyle(
                settings,
                palette.ActiveReferenceText,
                palette.ActiveReferenceTextOutline,
                palette.ActiveReferenceTextShadow,
                attached)
            : CreateReferenceTextStyle(
                settings,
                palette.ReferenceText,
                palette.ReferenceTextOutline,
                palette.ReferenceTextShadow,
                attached);
    }

    public static TextRenderStyle GetSplitTextStyle(AppSettings settings, UiPalette palette, bool attached = false)
    {
        return new TextRenderStyle(
            palette.SplitText,
            palette.SplitTextOutline,
            palette.SplitTextShadow,
            attached ? settings.Overlay.TextEffects.AttachedTimeShadowPercent : settings.Overlay.TextEffects.TimeShadowPercent,
            attached ? settings.Overlay.TextEffects.AttachedTimeOutlineThicknessPercent : settings.Overlay.TextEffects.TimeOutlineThicknessPercent);
    }

    public static TextRenderStyle GetDeltaTextStyle(
        AppSettings settings,
        SplitComparison comparison,
        UiPalette palette,
        bool attached = false)
    {
        bool ahead = comparison.Delta is TimeSpan delta && delta < TimeSpan.Zero;
        return ahead
            ? CreateDeltaTextStyle(
                settings,
                palette.DeltaAheadText,
                palette.DeltaAheadTextOutline,
                palette.DeltaAheadTextShadow,
                attached)
            : CreateDeltaTextStyle(
                settings,
                palette.DeltaBehindText,
                palette.DeltaBehindTextOutline,
                palette.DeltaBehindTextShadow,
                attached);
    }

    public static TextRenderStyle GetTimerTextStyle(
        AppSettings settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
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

        if (TryGetCompletedMoonLordStatus(statuses, out SplitStatusSnapshot moonLordStatus, out TimeSpan moonLordTime) &&
            ReferenceSplitSetService.TryGetReferenceSplit(settings, moonLordStatus.Definition, out TimeSpan moonLordReference))
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
            if (ReferenceSplitSetService.TryGetReferenceSplit(settings, statuses[^1].Definition, out TimeSpan finalReference) &&
                finalTime < finalReference)
            {
                return CreateTimerTextStyle(
                    settings,
                    palette.TimerRecordText,
                    palette.TimerRecordTextOutline,
                    palette.TimerRecordTextShadow,
                    milliseconds);
            }

            if (ReferenceSplitSetService.TryGetReferenceSplit(settings, statuses[^1].Definition, out finalReference) &&
                settings.Overlay.EnableTimerGradientColor)
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

        if (TryGetTimerComparisonDefinition(settings, statuses, currentSplitIndex, out SplitDefinition comparisonDefinition) &&
            ReferenceSplitSetService.TryGetReferenceSplit(settings, comparisonDefinition, out TimeSpan currentReference))
        {
            if (settings.Overlay.EnableTimerGradientColor)
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

    private static bool TryGetTimerComparisonDefinition(
        AppSettings settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        int currentSplitIndex,
        out SplitDefinition definition)
    {
        definition = null!;
        if (currentSplitIndex < 0 || currentSplitIndex >= statuses.Count)
        {
            return false;
        }

        SplitDefinition current = statuses[currentSplitIndex].Definition;
        definition = current;
        return true;
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
        Color shadow,
        bool attached = false)
    {
        return new TextRenderStyle(
            fill,
            outline,
            shadow,
            attached ? settings.Overlay.TextEffects.AttachedTimeShadowPercent : settings.Overlay.TextEffects.TimeShadowPercent,
            attached ? settings.Overlay.TextEffects.AttachedTimeOutlineThicknessPercent : settings.Overlay.TextEffects.TimeOutlineThicknessPercent);
    }

    private static TextRenderStyle CreateDeltaTextStyle(
        AppSettings settings,
        Color fill,
        Color outline,
        Color shadow,
        bool attached = false)
    {
        return new TextRenderStyle(
            fill,
            outline,
            shadow,
            attached ? settings.Overlay.TextEffects.AttachedDeltaShadowPercent : settings.Overlay.TextEffects.DeltaShadowPercent,
            attached ? settings.Overlay.TextEffects.AttachedDeltaOutlineThicknessPercent : settings.Overlay.TextEffects.DeltaOutlineThicknessPercent);
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
                ? settings.Overlay.TextEffects.TimerMillisecondsShadowPercent
                : settings.Overlay.TextEffects.TimerShadowPercent,
            milliseconds
                ? settings.Overlay.TextEffects.TimerMillisecondsOutlineThicknessPercent
                : settings.Overlay.TextEffects.TimerOutlineThicknessPercent);
    }

    private static float GetOpacity(int opacityPercent)
    {
        return Math.Clamp(opacityPercent, 0, 100) / 100f;
    }

    private static bool TryGetCompletedMoonLordStatus(
        IReadOnlyList<SplitStatusSnapshot> statuses,
        out SplitStatusSnapshot moonLordStatus,
        out TimeSpan moonLordTime)
    {
        SplitStatusSnapshot? match = statuses.FirstOrDefault(status =>
            !status.IsSkipped &&
            status.Time is not null &&
            SplitCatalog.IsMoonLordSplit(status.Definition));
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

        if (TimeText.IsDeltaDisplayedAsZero(delta.Value, settings.Overlay.EnableDynamicDeltaTimeUnits))
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

        float thresholdSeconds = Math.Max(1, settings.Overlay.DeltaGradientThresholdSeconds);
        float magnitude = Math.Min(1f, (float)(Math.Abs(delta.TotalSeconds) / thresholdSeconds));
        float amount = DeltaGradientCurves.Evaluate(settings.Overlay.DeltaGradientCurve, magnitude);
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
