namespace TerrariaSplit.Configuration;

internal static class SettingsNormalizer
{
    public static void Normalize(AppSettings settings)
    {
        AppSettings defaults = AppSettingsDefaults.Create();
        settings.General ??= defaults.General;
        settings.Hotkeys ??= defaults.Hotkeys;
        settings.Route ??= defaults.Route;
        settings.Comparison ??= defaults.Comparison;
        settings.Overlay ??= defaults.Overlay;
        settings.Automation ??= defaults.Automation;
        settings.PracticeWorlds ??= defaults.PracticeWorlds;
        settings.Advanced ??= defaults.Advanced;
        SettingsMigrator.Migrate(settings);
        settings.Route.SplitRoute ??= new List<SplitRouteEntry>();
        settings.Comparison.ReferenceSplitSets ??= new List<ReferenceSplitSet>();
        settings.Comparison.PersonalBestTimeSets ??= new List<ReferenceSplitSet>();
        settings.Comparison.PersonalBestSegmentSets ??= new List<ReferenceSplitSet>();
        settings.Comparison.PersonalBestTimes ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.Comparison.PersonalBestSegmentTimes ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.Overlay.SplitCompletionSplitComparisons ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        settings.Overlay.SplitCompletionSegmentComparisons ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        settings.Overlay.SplitCompletionOutlineSplitStyles ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.Overlay.SplitCompletionOutlineSegmentStyles ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.Overlay.SegmentBestDeltaHighlightStyles ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.Overlay.Colors ??= defaults.Overlay.Colors;
        settings.Overlay.Sounds ??= defaults.Overlay.Sounds;
        settings.Overlay.Columns ??= defaults.Overlay.Columns;
        settings.Overlay.TextEffects ??= defaults.Overlay.TextEffects;
        settings.Automation.AutoCreate ??= defaults.Automation.AutoCreate;
        settings.PracticeWorlds ??= new PracticeWorldSettings();
        settings.Advanced ??= defaults.Advanced;
        SettingsSectionNormalizer.NormalizeAutoCreate(settings.Automation.AutoCreate);
        SettingsSectionNormalizer.NormalizePracticeWorlds(settings.PracticeWorlds);
        SettingsSectionNormalizer.NormalizeAdvanced(settings.Advanced);
        settings.Overlay.SplitCompletionAnimationDurationSeconds = Math.Clamp(settings.Overlay.SplitCompletionAnimationDurationSeconds, 2f, 20f);
        settings.Overlay.SplitCompletionOutlineThicknessPercent = Math.Clamp(settings.Overlay.SplitCompletionOutlineThicknessPercent, 0, 100);
        settings.Overlay.CurrentSplitHighlightScalePercent = Math.Clamp(settings.Overlay.CurrentSplitHighlightScalePercent, 100, 140);
        settings.Overlay.CurrentSplitDepthStrengthPercent = Math.Clamp(settings.Overlay.CurrentSplitDepthStrengthPercent, 0, 100);
        settings.Overlay.EarlyDeltaTimeSeconds = Math.Clamp(settings.Overlay.EarlyDeltaTimeSeconds, 0, 3600);
        settings.Overlay.DeltaGradientThresholdSeconds = Math.Clamp(settings.Overlay.DeltaGradientThresholdSeconds, 1, 3600);
        settings.Overlay.DeltaGradientCurve = DeltaGradientCurves.Normalize(settings.Overlay.DeltaGradientCurve);
        settings.Overlay.CurrentBossIconGrayscaleWeakenPercent = Math.Clamp(settings.Overlay.CurrentBossIconGrayscaleWeakenPercent, 0, 100);
        settings.Overlay.CurrentBossIconBrightnessBoostPercent = Math.Clamp(settings.Overlay.CurrentBossIconBrightnessBoostPercent, 0, 100);
        NormalizeRoute(settings);
        SettingsSectionNormalizer.NormalizeColumnSettings(settings.Overlay.Columns, defaults.Overlay.Columns);
        SettingsSectionNormalizer.NormalizeTextEffects(settings.Overlay.TextEffects);

        IReadOnlyList<SplitConditionDataRow> conditionRows = SplitConditionDataRows.Build(settings);
        HashSet<string> conditionRowKeys = conditionRows
            .Select(row => row.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        RemoveUnknownCumulativeKeys(settings, conditionRowKeys);

        foreach (string key in conditionRowKeys)
        {
            settings.Comparison.PersonalBestTimes.TryAdd(key, string.Empty);
        }

        foreach (RouteGroup group in SplitRouteGroups.Build(settings))
        {
            settings.Comparison.PersonalBestSegmentTimes.TryAdd(group.Key, string.Empty);
            settings.Overlay.SplitCompletionSplitComparisons.TryAdd(group.Key, true);
            settings.Overlay.SplitCompletionSegmentComparisons.TryAdd(group.Key, true);
            settings.Overlay.SplitCompletionOutlineSplitStyles[group.Key] = GetNormalizedOutlineStyle(
                settings.Overlay.SplitCompletionOutlineSplitStyles,
                group.Key,
                SplitCompletionOutlineStyles.Rainbow);
            settings.Overlay.SplitCompletionOutlineSegmentStyles[group.Key] = GetNormalizedOutlineStyle(
                settings.Overlay.SplitCompletionOutlineSegmentStyles,
                group.Key,
                SplitCompletionOutlineStyles.Aurora);
            settings.Overlay.SegmentBestDeltaHighlightStyles[group.Key] = GetNormalizedDeltaHighlightStyle(settings.Overlay.SegmentBestDeltaHighlightStyles, group.Key);
        }

        RemoveUnknownRouteGroupKeys(settings);

        SettingsSplitSetNormalizer.NormalizeReferenceSets(settings);
        SettingsSplitSetNormalizer.NormalizePersonalBestTimeSets(settings);
        SettingsSplitSetNormalizer.NormalizePersonalBestSegmentSets(settings);
    }


    private static void NormalizeRoute(AppSettings settings)
    {
        settings.Route.VisibleGroupCountLimit = Math.Clamp(settings.Route.VisibleGroupCountLimit, 1, 100);
        settings.Route.CurrentGroupPosition = Math.Clamp(
            settings.Route.CurrentGroupPosition,
            1,
            settings.Route.VisibleGroupCountLimit);

        if (settings.Route.SplitRoute.Count == 0)
        {
            settings.Route.SplitRoute = SplitCatalog.CreateDefaultRoute();
            return;
        }

        var normalized = new List<SplitRouteEntry>();
        HashSet<string> seenIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (SplitRouteEntry entry in settings.Route.SplitRoute)
        {
            if (entry is null)
            {
                continue;
            }

            entry.Condition = NormalizeCondition(entry.Condition ?? SplitCondition.All([]));
            entry.UseAdvancedConditionEditor = entry.UseAdvancedConditionEditor ||
                RequiresAdvancedConditionEditor(entry.Condition);

            entry.Id = CreateUniqueRouteEntryId(entry, normalized.Count + 1, seenIds);
            entry.DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? CreateRouteEntryDisplayName(entry, normalized.Count + 1)
                : entry.DisplayName.Trim();
            List<string> inferredTargetIds = SplitCatalog.InferTargetIds(entry.Condition)
                .Where(targetId => SplitCatalog.TryGetTarget(targetId, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            entry.IconTargetIds = inferredTargetIds;
            NormalizeIconOverride(entry, inferredTargetIds);
            normalized.Add(entry);
        }

        if (normalized.Count == 0)
        {
            normalized = SplitCatalog.CreateDefaultRoute();
        }

        bool hasFollowingEnabledAnchor = false;
        for (int i = normalized.Count - 1; i >= 0; i--)
        {
            SplitRouteEntry entry = normalized[i];
            if (!entry.Enabled)
            {
                continue;
            }

            if (entry.IsAttached && !hasFollowingEnabledAnchor)
            {
                entry.IsAttached = false;
            }

            if (!entry.IsAttached)
            {
                hasFollowingEnabledAnchor = true;
            }
        }

        settings.Route.SplitRoute = normalized;
    }

    private static bool RequiresAdvancedConditionEditor(SplitCondition condition)
    {
        string kind = SplitConditionKind.Normalize(condition.Kind);
        if (kind == SplitConditionKind.Fact)
        {
            return false;
        }

        if (kind != SplitConditionKind.All &&
            kind != SplitConditionKind.Any &&
            kind != SplitConditionKind.AtLeast)
        {
            return true;
        }

        return condition.Children.Any(child => SplitConditionKind.Normalize(child.Kind) != SplitConditionKind.Fact);
    }

    private static void NormalizeIconOverride(SplitRouteEntry entry, IReadOnlyCollection<string> conditionTargetIds)
    {
        entry.IconOverride ??= new SplitIconOverride();
        entry.IconOverride.Source = SplitIconOverrideSource.Normalize(entry.IconOverride.Source);
        entry.IconOverride.TargetId = entry.IconOverride.TargetId?.Trim() ?? string.Empty;
        entry.IconOverride.FilePath = entry.IconOverride.FilePath?.Trim() ?? string.Empty;

        if (entry.IconOverride.Source == SplitIconOverrideSource.Target)
        {
            if (!conditionTargetIds.Contains(entry.IconOverride.TargetId, StringComparer.OrdinalIgnoreCase))
            {
                ClearIconOverride(entry.IconOverride);
                return;
            }

            entry.IconOverride.FilePath = string.Empty;
            return;
        }

        if (entry.IconOverride.Source == SplitIconOverrideSource.CustomFile)
        {
            entry.IconOverride.TargetId = string.Empty;
            if (string.IsNullOrWhiteSpace(entry.IconOverride.FilePath))
            {
                ClearIconOverride(entry.IconOverride);
            }

            return;
        }

        ClearIconOverride(entry.IconOverride);
    }

    private static void ClearIconOverride(SplitIconOverride iconOverride)
    {
        iconOverride.Source = SplitIconOverrideSource.All;
        iconOverride.TargetId = string.Empty;
        iconOverride.FilePath = string.Empty;
    }

    private static string CreateUniqueRouteEntryId(SplitRouteEntry entry, int index, HashSet<string> seenIds)
    {
        string baseId = string.IsNullOrWhiteSpace(entry.Id)
            ? CreateRouteEntryId(entry, index)
            : entry.Id.Trim();
        if (string.IsNullOrWhiteSpace(baseId))
        {
            baseId = $"split:custom-{index}";
        }

        string id = baseId;
        int suffix = index;
        while (!seenIds.Add(id))
        {
            id = $"{baseId}-{suffix}";
            suffix++;
        }

        return id;
    }

    private static string CreateRouteEntryId(SplitRouteEntry entry, int index)
    {
        string factKey = entry.Condition.GetFactKeys().FirstOrDefault() ?? string.Empty;
        string suffix = string.IsNullOrWhiteSpace(factKey)
            ? $"custom-{index}"
            : new string(factKey
                .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-')
                .ToArray()).Trim('-');
        return string.IsNullOrWhiteSpace(suffix) ? $"split:custom-{index}" : $"split:{suffix}";
    }

    private static string CreateRouteEntryDisplayName(SplitRouteEntry entry, int index)
    {
        List<string> targetNames = entry.Condition
            .GetFactKeys()
            .Select(factKey => SplitCatalog.TryGetTargetByFactKey(factKey, out SplitTargetDefinition target)
                ? target.DisplayName
                : string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return targetNames.Count switch
        {
            1 => targetNames[0],
            > 1 => string.Join(" + ", targetNames),
            _ => $"Split {index}"
        };
    }

    private static SplitCondition NormalizeCondition(SplitCondition condition)
    {
        condition.Normalize();
        return NormalizeConditionNode(condition);
    }

    private static SplitCondition NormalizeConditionNode(SplitCondition condition)
    {
        string kind = SplitConditionKind.Normalize(condition.Kind);
        if (kind == SplitConditionKind.Fact)
        {
            SplitCondition fact = condition.Clone();
            fact.Normalize();
            if (SplitCatalog.TryParseItemOwnedCountFactKey(fact.FactKey, out int itemId))
            {
                fact.FactKey = SplitCatalog.CreateItemEverOwnedFactKey(itemId);
            }

            return fact;
        }

        List<SplitCondition> children = (condition.Children ?? [])
            .Select(NormalizeConditionNode)
            .ToList();
        if (kind == SplitConditionKind.All)
        {
            return SplitCondition.All(children);
        }

        int requiredCount = kind == SplitConditionKind.Any
            ? 1
            : condition.Value;
        return SplitCondition.AtLeast(
            children,
            Math.Clamp(requiredCount, 1, Math.Max(1, children.Count)));
    }

    private static void RemoveUnknownCumulativeKeys(AppSettings settings, HashSet<string> conditionRowKeys)
    {
        SettingsNormalizationHelpers.RemoveKeysExcept(settings.Comparison.PersonalBestTimes, conditionRowKeys);

        foreach (ReferenceSplitSet set in settings.Comparison.ReferenceSplitSets)
        {
            SettingsNormalizationHelpers.RemoveKeysExcept(set.Splits, conditionRowKeys);
        }
    }

    private static void RemoveUnknownRouteGroupKeys(AppSettings settings)
    {
        HashSet<string> validGroupKeys = SplitRouteGroups.Build(settings)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        SettingsNormalizationHelpers.RemoveKeysExcept(settings.Comparison.PersonalBestSegmentTimes, validGroupKeys);
        SettingsNormalizationHelpers.RemoveKeysExcept(settings.Overlay.SplitCompletionSplitComparisons, validGroupKeys);
        SettingsNormalizationHelpers.RemoveKeysExcept(settings.Overlay.SplitCompletionSegmentComparisons, validGroupKeys);
        SettingsNormalizationHelpers.RemoveKeysExcept(settings.Overlay.SplitCompletionOutlineSplitStyles, validGroupKeys);
        SettingsNormalizationHelpers.RemoveKeysExcept(settings.Overlay.SplitCompletionOutlineSegmentStyles, validGroupKeys);
        SettingsNormalizationHelpers.RemoveKeysExcept(settings.Overlay.SegmentBestDeltaHighlightStyles, validGroupKeys);
    }

    private static string GetNormalizedDeltaHighlightStyle(Dictionary<string, string> styles, string key)
    {
        return styles.TryGetValue(key, out string? existing)
            ? SegmentBestDeltaHighlightStyles.Normalize(existing)
            : SegmentBestDeltaHighlightStyles.Aurora;
    }

    private static string GetNormalizedOutlineStyle(Dictionary<string, string> styles, string key, string defaultStyle)
    {
        return styles.TryGetValue(key, out string? existing)
            ? SplitCompletionOutlineStyles.Normalize(existing)
            : defaultStyle;
    }

}
