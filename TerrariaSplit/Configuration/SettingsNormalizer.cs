namespace TerrariaSplit.Configuration;

internal static class SettingsNormalizer
{
    public static void Normalize(AppSettings settings)
    {
        SettingsMigrator.Migrate(settings);
        AppSettings defaults = AppSettingsDefaults.Create();
        settings.SplitRoute ??= new List<SplitRouteEntry>();
        settings.ReferenceSplitSets ??= new List<ReferenceSplitSet>();
        settings.PersonalBestTimeSets ??= new List<ReferenceSplitSet>();
        settings.PersonalBestSegmentSets ??= new List<ReferenceSplitSet>();
        settings.PersonalBestTimes ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.PersonalBestSegmentTimes ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.SplitCompletionSplitComparisons ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        settings.SplitCompletionSegmentComparisons ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        settings.SplitCompletionOutlineSplitStyles ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.SplitCompletionOutlineSegmentStyles ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.SegmentBestDeltaHighlightStyles ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        settings.Colors ??= defaults.Colors;
        settings.Sounds ??= defaults.Sounds;
        settings.Columns ??= defaults.Columns;
        settings.TextEffects ??= defaults.TextEffects;
        settings.AutoCreate ??= defaults.AutoCreate;
        settings.PracticeWorlds ??= new PracticeWorldSettings();
        settings.Advanced ??= defaults.Advanced;
        SettingsSectionNormalizer.NormalizeAutoCreate(settings.AutoCreate);
        SettingsSectionNormalizer.NormalizePracticeWorlds(settings.PracticeWorlds);
        SettingsSectionNormalizer.NormalizeAdvanced(settings.Advanced);
        settings.SplitCompletionAnimationDurationSeconds = Math.Clamp(settings.SplitCompletionAnimationDurationSeconds, 2f, 20f);
        settings.SplitCompletionOutlineThicknessPercent = Math.Clamp(settings.SplitCompletionOutlineThicknessPercent, 0, 100);
        settings.CurrentSplitHighlightScalePercent = Math.Clamp(settings.CurrentSplitHighlightScalePercent, 100, 140);
        settings.CurrentSplitDepthStrengthPercent = Math.Clamp(settings.CurrentSplitDepthStrengthPercent, 0, 100);
        settings.EarlyDeltaTimeSeconds = Math.Clamp(settings.EarlyDeltaTimeSeconds, 0, 3600);
        settings.DeltaGradientThresholdSeconds = Math.Clamp(settings.DeltaGradientThresholdSeconds, 1, 3600);
        settings.DeltaGradientCurve = DeltaGradientCurves.Normalize(settings.DeltaGradientCurve);
        settings.CurrentBossIconGrayscaleWeakenPercent = Math.Clamp(settings.CurrentBossIconGrayscaleWeakenPercent, 0, 100);
        settings.CurrentBossIconBrightnessBoostPercent = Math.Clamp(settings.CurrentBossIconBrightnessBoostPercent, 0, 100);
        NormalizeRoute(settings);
        SettingsSectionNormalizer.NormalizeColumnSettings(settings.Columns, defaults.Columns);
        SettingsSectionNormalizer.NormalizeTextEffects(settings.TextEffects);

        IReadOnlyList<SplitConditionDataRow> conditionRows = SplitConditionDataRows.Build(settings);
        HashSet<string> conditionRowKeys = conditionRows
            .Select(row => row.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        RemoveUnknownCumulativeKeys(settings, conditionRowKeys);

        foreach (string key in conditionRowKeys)
        {
            settings.PersonalBestTimes.TryAdd(key, string.Empty);
        }

        foreach (RouteGroup group in SplitRouteGroups.Build(settings))
        {
            settings.PersonalBestSegmentTimes.TryAdd(group.Key, string.Empty);
            settings.SplitCompletionSplitComparisons.TryAdd(group.Key, true);
            settings.SplitCompletionSegmentComparisons.TryAdd(group.Key, true);
            settings.SplitCompletionOutlineSplitStyles[group.Key] = GetNormalizedOutlineStyle(
                settings.SplitCompletionOutlineSplitStyles,
                group.Key,
                SplitCompletionOutlineStyles.Rainbow);
            settings.SplitCompletionOutlineSegmentStyles[group.Key] = GetNormalizedOutlineStyle(
                settings.SplitCompletionOutlineSegmentStyles,
                group.Key,
                SplitCompletionOutlineStyles.Aurora);
            settings.SegmentBestDeltaHighlightStyles[group.Key] = GetNormalizedDeltaHighlightStyle(settings.SegmentBestDeltaHighlightStyles, group.Key);
        }

        RemoveUnknownRouteGroupKeys(settings);

        SettingsSplitSetNormalizer.NormalizeReferenceSets(settings);
        SettingsSplitSetNormalizer.NormalizePersonalBestTimeSets(settings);
        SettingsSplitSetNormalizer.NormalizePersonalBestSegmentSets(settings);
    }


    private static void NormalizeRoute(AppSettings settings)
    {
        if (settings.SplitRoute.Count == 0)
        {
            settings.SplitRoute = SplitCatalog.CreateDefaultRoute();
            return;
        }

        var normalized = new List<SplitRouteEntry>();
        HashSet<string> seenIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (SplitRouteEntry entry in settings.SplitRoute)
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

        settings.SplitRoute = normalized;
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
        SettingsNormalizationHelpers.RemoveKeysExcept(settings.PersonalBestTimes, conditionRowKeys);

        foreach (ReferenceSplitSet set in settings.ReferenceSplitSets)
        {
            SettingsNormalizationHelpers.RemoveKeysExcept(set.Splits, conditionRowKeys);
        }
    }

    private static void RemoveUnknownRouteGroupKeys(AppSettings settings)
    {
        HashSet<string> validGroupKeys = SplitRouteGroups.Build(settings)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        SettingsNormalizationHelpers.RemoveKeysExcept(settings.PersonalBestSegmentTimes, validGroupKeys);
        SettingsNormalizationHelpers.RemoveKeysExcept(settings.SplitCompletionSplitComparisons, validGroupKeys);
        SettingsNormalizationHelpers.RemoveKeysExcept(settings.SplitCompletionSegmentComparisons, validGroupKeys);
        SettingsNormalizationHelpers.RemoveKeysExcept(settings.SplitCompletionOutlineSplitStyles, validGroupKeys);
        SettingsNormalizationHelpers.RemoveKeysExcept(settings.SplitCompletionOutlineSegmentStyles, validGroupKeys);
        SettingsNormalizationHelpers.RemoveKeysExcept(settings.SegmentBestDeltaHighlightStyles, validGroupKeys);
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
