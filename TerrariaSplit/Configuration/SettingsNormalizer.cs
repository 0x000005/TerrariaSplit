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
        NormalizeAutoCreate(settings.AutoCreate);
        NormalizePracticeWorlds(settings.PracticeWorlds);
        NormalizeAdvanced(settings.Advanced);
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
        NormalizeColumnSettings(settings.Columns, defaults.Columns);
        NormalizeTextEffects(settings.TextEffects);

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

        NormalizeReferenceSets(settings);
        NormalizePersonalBestTimeSets(settings);
        NormalizePersonalBestSegmentSets(settings);
    }

    private static void NormalizeAutoCreate(AutoCreateWorldSettings autoCreate)
    {
        autoCreate.PlayerName ??= string.Empty;
        autoCreate.PlayerTemplateCode ??= string.Empty;
        autoCreate.PlayerDifficulty = AutoCreatePlayerDifficulty.Normalize(autoCreate.PlayerDifficulty);
        autoCreate.WorldSize = AutoCreateWorldSize.Normalize(autoCreate.WorldSize);
        autoCreate.WorldDifficulty = AutoCreateWorldDifficulty.Normalize(autoCreate.WorldDifficulty);
        autoCreate.WorldEvil = AutoCreateWorldEvil.Normalize(autoCreate.WorldEvil);
        autoCreate.SpecialSeeds = string.Join("|", AutoCreateSpecialWorldSeed.ParseList(autoCreate.SpecialSeeds));
        autoCreate.SecretSeeds = autoCreate.SecretSeeds?.Trim() ?? string.Empty;
        autoCreate.ZenithStarCatchStopStage = AutoCreateZenithStarCatchStage.Normalize(autoCreate.ZenithStarCatchStopStage);
        autoCreate.ZenithStarCatchSpeedSliderValue = AutoCreateZenithStarCatchSpeed.NormalizeSliderValue(autoCreate.ZenithStarCatchSpeedSliderValue);
        autoCreate.PyramidFilterItemMask = AutoCreatePyramidFilterItem.NormalizeMask(autoCreate.PyramidFilterItemMask);
        autoCreate.ShortActionDelayMilliseconds = Math.Clamp(autoCreate.ShortActionDelayMilliseconds, 0, 5000);
        autoCreate.MenuActionDelayMilliseconds = Math.Clamp(autoCreate.MenuActionDelayMilliseconds, 0, 5000);
        autoCreate.PyramidFilterPostDelayMilliseconds = Math.Clamp(autoCreate.PyramidFilterPostDelayMilliseconds, 0, 5000);
        autoCreate.WindowActivationDelayMilliseconds = Math.Clamp(autoCreate.WindowActivationDelayMilliseconds, 0, 5000);
        autoCreate.ClickFocusDelayMilliseconds = Math.Clamp(autoCreate.ClickFocusDelayMilliseconds, 0, 5000);
        autoCreate.InputPressDurationMilliseconds = Math.Clamp(autoCreate.InputPressDurationMilliseconds, 1, 5000);
        autoCreate.WorldPoolTargetCount = Math.Clamp(autoCreate.WorldPoolTargetCount, 1, 50);
    }

    private static void NormalizePracticeWorlds(PracticeWorldSettings practiceWorlds)
    {
        practiceWorlds.Slots ??= new List<PracticeWorldSlot>();
        while (practiceWorlds.Slots.Count < PracticeWorldSettings.SlotCount)
        {
            practiceWorlds.Slots.Add(new PracticeWorldSlot());
        }

        if (practiceWorlds.Slots.Count > PracticeWorldSettings.SlotCount)
        {
            practiceWorlds.Slots.RemoveRange(
                PracticeWorldSettings.SlotCount,
                practiceWorlds.Slots.Count - PracticeWorldSettings.SlotCount);
        }

        for (int i = 0; i < practiceWorlds.Slots.Count; i++)
        {
            practiceWorlds.Slots[i] ??= new PracticeWorldSlot();
            practiceWorlds.Slots[i].Name = practiceWorlds.Slots[i].Name?.Trim() ?? string.Empty;
            practiceWorlds.Slots[i].PlayerFilePath = practiceWorlds.Slots[i].PlayerFilePath?.Trim() ?? string.Empty;
            practiceWorlds.Slots[i].WorldFilePath = practiceWorlds.Slots[i].WorldFilePath?.Trim() ?? string.Empty;
        }
    }

    private static void NormalizeAdvanced(AdvancedSettings advanced)
    {
        advanced.ReadyWatcherPollHz = RefreshRateSettings.NormalizeReadyWatcherPollHz(advanced.ReadyWatcherPollHz);
        advanced.ReadyUiControlHz = RefreshRateSettings.NormalizeReadyUiControlHz(advanced.ReadyUiControlHz);
        advanced.RunningStatusPaintHz = RefreshRateSettings.NormalizeRunningStatusPaintHz(advanced.RunningStatusPaintHz);
        advanced.TimerOverlayRefreshHz = RefreshRateSettings.NormalizeTimerOverlayRefreshHz(advanced.TimerOverlayRefreshHz);
    }

    private static void NormalizeColumnSettings(UiColumnLayoutSettings columns, UiColumnLayoutSettings defaults)
    {
        columns.Icon ??= defaults.Icon;
        columns.Time ??= defaults.Time;
        columns.Delta ??= defaults.Delta;
        columns.AttachedIcon ??= defaults.AttachedIcon;
        columns.AttachedTime ??= defaults.AttachedTime;
        columns.AttachedDelta ??= defaults.AttachedDelta;
        columns.Timer ??= defaults.Timer;
        columns.TimerMilliseconds ??= defaults.TimerMilliseconds;
        columns.ScalePercent = Math.Clamp(columns.ScalePercent, 25, 300);

        NormalizeColumn(columns.Icon, defaults.Icon);
        NormalizeColumn(columns.Time, defaults.Time);
        NormalizeColumn(columns.Delta, defaults.Delta);
        NormalizeColumn(columns.AttachedIcon, defaults.AttachedIcon);
        NormalizeColumn(columns.AttachedTime, defaults.AttachedTime);
        NormalizeColumn(columns.AttachedDelta, defaults.AttachedDelta);
        NormalizeColumn(columns.Timer, defaults.Timer);
        NormalizeColumn(columns.TimerMilliseconds, defaults.TimerMilliseconds);
    }

    private static void NormalizeColumn(UiColumnSettings column, UiColumnSettings defaults)
    {
        column.FontFamily = UiFontSettings.NormalizeFamilyName(column.FontFamily);
        if (string.IsNullOrWhiteSpace(column.FontFamily))
        {
            column.FontFamily = defaults.FontFamily;
        }

        if (column.Width <= 0)
        {
            column.Width = defaults.Width;
        }

        if (column.FontSize <= 0)
        {
            column.FontSize = defaults.FontSize;
        }
    }

    private static void NormalizeTextEffects(UiTextEffectSettings effects)
    {
        effects.IconOpacityPercent = ClampPercent(effects.IconOpacityPercent);
        effects.TimeOpacityPercent = ClampPercent(effects.TimeOpacityPercent);
        effects.TimeShadowPercent = ClampPercent(effects.TimeShadowPercent);
        effects.TimeOutlineThicknessPercent = ClampOutlinePercent(effects.TimeOutlineThicknessPercent);
        effects.DeltaOpacityPercent = ClampPercent(effects.DeltaOpacityPercent);
        effects.DeltaShadowPercent = ClampPercent(effects.DeltaShadowPercent);
        effects.DeltaOutlineThicknessPercent = ClampOutlinePercent(effects.DeltaOutlineThicknessPercent);
        effects.AttachedIconOpacityPercent = ClampPercent(effects.AttachedIconOpacityPercent);
        effects.AttachedTimeOpacityPercent = ClampPercent(effects.AttachedTimeOpacityPercent);
        effects.AttachedTimeShadowPercent = ClampPercent(effects.AttachedTimeShadowPercent);
        effects.AttachedTimeOutlineThicknessPercent = ClampOutlinePercent(effects.AttachedTimeOutlineThicknessPercent);
        effects.AttachedDeltaOpacityPercent = ClampPercent(effects.AttachedDeltaOpacityPercent);
        effects.AttachedDeltaShadowPercent = ClampPercent(effects.AttachedDeltaShadowPercent);
        effects.AttachedDeltaOutlineThicknessPercent = ClampOutlinePercent(effects.AttachedDeltaOutlineThicknessPercent);
        effects.TimerOpacityPercent = ClampPercent(effects.TimerOpacityPercent);
        effects.TimerShadowPercent = ClampPercent(effects.TimerShadowPercent);
        effects.TimerOutlineThicknessPercent = ClampOutlinePercent(effects.TimerOutlineThicknessPercent);
        effects.TimerMillisecondsOpacityPercent = ClampPercent(effects.TimerMillisecondsOpacityPercent);
        effects.TimerMillisecondsShadowPercent = ClampPercent(effects.TimerMillisecondsShadowPercent);
        effects.TimerMillisecondsOutlineThicknessPercent = ClampOutlinePercent(effects.TimerMillisecondsOutlineThicknessPercent);
    }

    private static int ClampPercent(int value)
    {
        return Math.Clamp(value, 0, 100);
    }

    private static int ClampOutlinePercent(int value)
    {
        return Math.Clamp(value, 0, 200);
    }

    private static void NormalizeReferenceSets(AppSettings settings)
    {
        if (settings.ReferenceSplitSets.Count == 0)
        {
            settings.ReferenceSplitSets.Add(AppSettings.CreateReferenceSet(
                "WR",
                keys: SplitConditionDataRows.Build(settings).Select(row => row.Key)));
        }

        HashSet<string> conditionRowKeys = SplitConditionDataRows.Build(settings)
            .Select(row => row.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (ReferenceSplitSet set in settings.ReferenceSplitSets)
        {
            set.Name = string.IsNullOrWhiteSpace(set.Name) ? "Reference" : set.Name.Trim();
            set.Splits ??= new Dictionary<string, string>();
            RemoveKeysExcept(set.Splits, conditionRowKeys);

            foreach (string key in conditionRowKeys)
            {
                set.Splits.TryAdd(key, string.Empty);
            }
        }

        if (string.IsNullOrWhiteSpace(settings.ActiveReferenceSplitSet) ||
            !settings.ReferenceSplitSets.Any(set => string.Equals(
                set.Name,
                settings.ActiveReferenceSplitSet,
                StringComparison.OrdinalIgnoreCase)))
        {
            settings.ActiveReferenceSplitSet = settings.ReferenceSplitSets[0].Name;
        }
    }

    private static void NormalizePersonalBestTimeSets(AppSettings settings)
    {
        NormalizePersonalSets(
            settings.PersonalBestTimeSets,
            "Personal",
            validKeys: SplitConditionDataRows.Build(settings).Select(row => row.Key),
            activeName: settings.ActivePersonalBestTimeSet,
            setActiveName: value => settings.ActivePersonalBestTimeSet = value);
    }

    private static void NormalizePersonalBestSegmentSets(AppSettings settings)
    {
        NormalizePersonalSets(
            settings.PersonalBestSegmentSets,
            "Personal",
            validKeys: SplitRouteGroups.Build(settings).Select(group => group.Key),
            activeName: settings.ActivePersonalBestSegmentSet,
            setActiveName: value => settings.ActivePersonalBestSegmentSet = value);
    }

    private static void NormalizePersonalSets(
        List<ReferenceSplitSet> sets,
        string fallbackName,
        IEnumerable<string> validKeys,
        string activeName,
        Action<string> setActiveName)
    {
        HashSet<string> validKeySet = validKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (sets.Count == 0)
        {
            sets.Add(CreateEmptyPersonalSet(fallbackName, validKeySet));
        }

        foreach (ReferenceSplitSet set in sets)
        {
            set.Name = string.IsNullOrWhiteSpace(set.Name) ? fallbackName : set.Name.Trim();
            set.Splits ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            RemoveKeysExcept(set.Splits, validKeySet);
            foreach (string key in validKeySet)
            {
                set.Splits.TryAdd(key, string.Empty);
            }
        }

        if (string.IsNullOrWhiteSpace(activeName) ||
            !sets.Any(set => string.Equals(set.Name, activeName, StringComparison.OrdinalIgnoreCase)))
        {
            setActiveName(sets[0].Name);
        }
    }

    private static ReferenceSplitSet CreateEmptyPersonalSet(string name, IEnumerable<string> keys)
    {
        var set = new ReferenceSplitSet
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Personal" : name.Trim(),
            Splits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (string key in keys)
        {
            set.Splits[key] = string.Empty;
        }

        return set;
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
        RemoveKeysExcept(settings.PersonalBestTimes, conditionRowKeys);

        foreach (ReferenceSplitSet set in settings.ReferenceSplitSets)
        {
            RemoveKeysExcept(set.Splits, conditionRowKeys);
        }
    }

    private static void RemoveUnknownRouteGroupKeys(AppSettings settings)
    {
        HashSet<string> validGroupKeys = SplitRouteGroups.Build(settings)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        RemoveKeysExcept(settings.PersonalBestSegmentTimes, validGroupKeys);
        RemoveKeysExcept(settings.SplitCompletionSplitComparisons, validGroupKeys);
        RemoveKeysExcept(settings.SplitCompletionSegmentComparisons, validGroupKeys);
        RemoveKeysExcept(settings.SplitCompletionOutlineSplitStyles, validGroupKeys);
        RemoveKeysExcept(settings.SplitCompletionOutlineSegmentStyles, validGroupKeys);
        RemoveKeysExcept(settings.SegmentBestDeltaHighlightStyles, validGroupKeys);
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

    private static void RemoveKeysExcept<TValue>(Dictionary<string, TValue> values, HashSet<string> allowedKeys)
    {
        foreach (string key in values.Keys.ToArray())
        {
            if (!allowedKeys.Contains(key))
            {
                values.Remove(key);
            }
        }
    }
}
