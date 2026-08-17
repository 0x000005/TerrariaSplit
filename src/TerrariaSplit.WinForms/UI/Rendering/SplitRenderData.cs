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
            .Select(GetTargetIdForFactKey)
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

    private static string GetTargetIdForFactKey(string factKey)
    {
        if (SplitCatalog.TryParseItemFactKey(factKey, out int itemId))
        {
            return SplitCatalog.CreateItemTargetId(itemId);
        }

        return SplitCatalog.TryGetTargetByFactKey(factKey, out SplitTargetDefinition target)
            ? target.Id
            : string.Empty;
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

    public static string FormatSplitDelta(AppSettings settings, SplitComparison comparison)
    {
        return comparison.ShowDelta && comparison.Delta is TimeSpan delta
            ? TimeText.FormatDelta(delta, settings.Overlay.EnableDynamicDeltaTimeUnits)
            : string.Empty;
    }

    public static bool ShouldShowSkippedTime(SplitStatusSnapshot status)
    {
        return status.IsSkipped &&
            status.Time is null;
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

    public static ImageRenderStyle GetIconImageStyle(AppSettings settings, UiPalette palette, bool attached = false)
    {
        return new ImageRenderStyle(
            palette.IconOutline,
            palette.IconShadow,
            attached
                ? settings.Overlay.TextEffects.AttachedIconShadowPercent
                : settings.Overlay.TextEffects.IconShadowPercent,
            attached
                ? settings.Overlay.TextEffects.AttachedIconOutlineThicknessPercent
                : settings.Overlay.TextEffects.IconOutlineThicknessPercent);
    }

    public static float GetTimeTextOpacity(AppSettings settings, bool attached = false)
    {
        return GetOpacity(attached
            ? settings.Overlay.TextEffects.AttachedTimeOpacityPercent
            : settings.Overlay.TextEffects.TimeOpacityPercent);
    }

    public static float GetNameTextOpacity(AppSettings settings, bool attached = false)
    {
        return GetOpacity(attached
            ? settings.Overlay.TextEffects.AttachedNameOpacityPercent
            : settings.Overlay.TextEffects.NameOpacityPercent);
    }

    public static TextRenderStyle GetNameTextStyle(
        AppSettings settings,
        UiPalette palette,
        bool current,
        bool completed,
        bool attached = false)
    {
        Color fill = completed
            ? palette.CompletedNameText
            : current ? palette.ActiveNameText : palette.NameText;
        Color outline = completed
            ? palette.CompletedNameTextOutline
            : current ? palette.ActiveNameTextOutline : palette.NameTextOutline;
        Color shadow = completed
            ? palette.CompletedNameTextShadow
            : current ? palette.ActiveNameTextShadow : palette.NameTextShadow;
        return new TextRenderStyle(
            fill,
            outline,
            shadow,
            attached
                ? settings.Overlay.TextEffects.AttachedNameShadowPercent
                : settings.Overlay.TextEffects.NameShadowPercent,
            attached
                ? settings.Overlay.TextEffects.AttachedNameOutlineThicknessPercent
                : settings.Overlay.TextEffects.NameOutlineThicknessPercent,
            LinearEffects: true);
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
            attached ? settings.Overlay.TextEffects.AttachedTimeOutlineThicknessPercent : settings.Overlay.TextEffects.TimeOutlineThicknessPercent,
            LinearEffects: true);
    }

    public static TextRenderStyle GetDeltaTextStyle(
        AppSettings settings,
        SplitComparison comparison,
        UiPalette palette,
        bool attached = false)
    {
        if (comparison.Delta is TimeSpan delta &&
            TimeText.IsDeltaDisplayedAsZero(delta, settings.Overlay.EnableDynamicDeltaTimeUnits))
        {
            return CreateDeltaTextStyle(
                settings,
                palette.DeltaEqualText,
                palette.DeltaEqualTextOutline,
                palette.DeltaEqualTextShadow,
                attached);
        }

        return comparison.Delta is TimeSpan signedDelta && signedDelta < TimeSpan.Zero
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
            bool hasFinalReference = TryGetTimerComparisonReference(
                settings,
                statuses,
                statuses.Count - 1,
                out TimeSpan finalReference);
            if (hasFinalReference && finalTime < finalReference)
            {
                return CreateTimerTextStyle(
                    settings,
                    palette.TimerRecordText,
                    palette.TimerRecordTextOutline,
                    palette.TimerRecordTextShadow,
                    milliseconds);
            }

            if (hasFinalReference && settings.Overlay.EnableTimerGradientColor)
            {
                return GetTimerGradientTextStyle(settings, finalTime - finalReference, palette, milliseconds);
            }

            if (hasFinalReference && finalTime == finalReference)
            {
                return CreateTimerTextStyle(
                    settings,
                    palette.TimerEqualText,
                    palette.TimerEqualTextOutline,
                    palette.TimerEqualTextShadow,
                    milliseconds);
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

        if (TryGetTimerComparisonReference(settings, statuses, currentSplitIndex, out TimeSpan currentReference))
        {
            if (settings.Overlay.EnableTimerGradientColor)
            {
                return GetTimerGradientTextStyle(settings, timerElapsed - currentReference, palette, milliseconds);
            }

            TimeSpan delta = timerElapsed - currentReference;
            if (delta == TimeSpan.Zero)
            {
                return CreateTimerTextStyle(
                    settings,
                    palette.TimerEqualText,
                    palette.TimerEqualTextOutline,
                    palette.TimerEqualTextShadow,
                    milliseconds);
            }

            return delta < TimeSpan.Zero
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

    private static bool TryGetTimerComparisonReference(
        AppSettings settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        int currentSplitIndex,
        out TimeSpan reference)
    {
        reference = TimeSpan.Zero;
        if (currentSplitIndex < 0 || currentSplitIndex >= statuses.Count)
        {
            return false;
        }

        foreach (SplitExpandedConditionRow row in SplitExpandedConditionRows.Build(settings, statuses, currentSplitIndex))
        {
            if (!row.CompletionTime.HasValue && row.ReferenceTime is TimeSpan rowReference)
            {
                reference = rowReference;
                return true;
            }
        }

        SplitDefinition definition = statuses[currentSplitIndex].Definition;
        return ReferenceSplitSetService.TryGetReferenceSplit(settings, definition, out reference);
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
                    palette.TimerEqualText,
                    palette.TimerEqualTextOutline,
                    palette.TimerEqualTextShadow,
                    milliseconds);
        return style with
        {
            Fill = OverlayColorMath.GetGradientDeltaColor(
                settings,
                delta,
                palette.TimerAheadText,
                palette.TimerEqualText,
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
            attached ? settings.Overlay.TextEffects.AttachedTimeOutlineThicknessPercent : settings.Overlay.TextEffects.TimeOutlineThicknessPercent,
            LinearEffects: true);
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
            attached ? settings.Overlay.TextEffects.AttachedDeltaOutlineThicknessPercent : settings.Overlay.TextEffects.DeltaOutlineThicknessPercent,
            LinearEffects: true);
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
                palette.DeltaEqualText,
                palette.DeltaBehindText);
        }

        if (TimeText.IsDeltaDisplayedAsZero(delta.Value, settings.Overlay.EnableDynamicDeltaTimeUnits))
        {
            return palette.DeltaEqualText;
        }

        if (delta < TimeSpan.Zero)
        {
            return palette.DeltaAheadText;
        }

        if (delta > TimeSpan.Zero)
        {
            return palette.DeltaBehindText;
        }

        return palette.DeltaEqualText;
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
        float amount = DeltaGradientCurveMath.Evaluate(settings.Overlay.DeltaGradientCurve, magnitude);
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
