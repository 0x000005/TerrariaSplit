namespace TerrariaSplit.Configuration;

public static class SettingsSectionNormalizer
{
    public static void NormalizeAutoCreate(AutoCreateWorldSettings autoCreate)
    {
        autoCreate.PlayerName ??= string.Empty;
        autoCreate.PlayerTemplateCode ??= string.Empty;
        autoCreate.PlayerDifficulty = AutoCreatePlayerDifficulty.Normalize(autoCreate.PlayerDifficulty);
        autoCreate.WorldSize = AutoCreateWorldSize.Normalize(autoCreate.WorldSize);
        autoCreate.WorldDifficulty = AutoCreateWorldDifficulty.Normalize(autoCreate.WorldDifficulty);
        autoCreate.WorldEvil = AutoCreateWorldEvil.Normalize(autoCreate.WorldEvil);
        autoCreate.SpecialSeeds = string.Join("|", AutoCreateSpecialWorldSeed.ParseList(autoCreate.SpecialSeeds));
        autoCreate.SecretSeeds = autoCreate.SecretSeeds?.Trim() ?? string.Empty;
        autoCreate.FixedSeed = autoCreate.FixedSeed?.Trim() ?? string.Empty;
        autoCreate.ZenithStarCatchStopStage = AutoCreateZenithStarCatchStage.Normalize(autoCreate.ZenithStarCatchStopStage);
        autoCreate.ZenithStarCatchSpeedSliderValue = AutoCreateZenithStarCatchSpeed.NormalizeSliderValue(autoCreate.ZenithStarCatchSpeedSliderValue);
        autoCreate.PyramidFilterItemMask = AutoCreatePyramidFilterItem.NormalizeMask(autoCreate.PyramidFilterItemMask);
        autoCreate.PyramidFilterDepth = AutoCreatePyramidDepth.Normalize(autoCreate.PyramidFilterDepth);
        autoCreate.PyramidFilterCoinPileMinimum = AutoCreatePyramidCoinPileMinimum.Normalize(autoCreate.PyramidFilterCoinPileMinimum);
        autoCreate.CrimsonDistance = AutoCreateCrimsonDistance.Normalize(autoCreate.CrimsonDistance);
        autoCreate.JungleRouteDepth = AutoCreateJungleRouteDepth.Normalize(autoCreate.JungleRouteDepth);
        autoCreate.ResourceFilterItemMask = AutoCreateResourceFilterItem.NormalizeMask(autoCreate.ResourceFilterItemMask);
        autoCreate.ResourceFilterLifeCrystalMinimum = AutoCreateResourceMinimum.NormalizeLifeCrystals(autoCreate.ResourceFilterLifeCrystalMinimum);
        autoCreate.ResourceFilterSpelunkerPotionMinimum = AutoCreateResourceMinimum.NormalizePotions(autoCreate.ResourceFilterSpelunkerPotionMinimum);
        autoCreate.ResourceFilterFeatherfallPotionMinimum = AutoCreateResourceMinimum.NormalizePotions(autoCreate.ResourceFilterFeatherfallPotionMinimum);
        if (autoCreate.FixedSeed.Length > 0)
        {
            autoCreate.EnablePyramidFilter = false;
        }
        AutoCreateAdvancedFilterEligibility.ClearUnsupportedFilters(autoCreate);
        autoCreate.ShortActionDelayMilliseconds = Math.Clamp(autoCreate.ShortActionDelayMilliseconds, 0, 5000);
        autoCreate.MenuActionDelayMilliseconds = Math.Clamp(autoCreate.MenuActionDelayMilliseconds, 0, 5000);
        autoCreate.PyramidFilterPostDelayMilliseconds = Math.Clamp(autoCreate.PyramidFilterPostDelayMilliseconds, 0, 5000);
        autoCreate.WindowActivationDelayMilliseconds = Math.Clamp(autoCreate.WindowActivationDelayMilliseconds, 0, 5000);
        autoCreate.ClickFocusDelayMilliseconds = Math.Clamp(autoCreate.ClickFocusDelayMilliseconds, 0, 5000);
        autoCreate.InputPressDurationMilliseconds = Math.Clamp(autoCreate.InputPressDurationMilliseconds, 1, 5000);
        autoCreate.WorldPoolTargetCount = Math.Clamp(autoCreate.WorldPoolTargetCount, 1, 50);
    }

    public static void NormalizePracticeWorlds(PracticeWorldSettings practiceWorlds)
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

    public static void NormalizeAdvanced(AdvancedSettings advanced)
    {
        advanced.RtssExecutablePath = advanced.RtssExecutablePath?.Trim() ?? string.Empty;
        advanced.RtssOverlayX = Math.Clamp(advanced.RtssOverlayX, -10000, 10000);
        advanced.RtssOverlayY = Math.Clamp(advanced.RtssOverlayY, -10000, 10000);
        advanced.RtssOverlayZoom = Math.Clamp(advanced.RtssOverlayZoom, 1, 8);
        advanced.ReadyWatcherPollHz = RefreshRateSettings.NormalizeReadyWatcherPollHz(advanced.ReadyWatcherPollHz);
        advanced.ReadyUiControlHz = RefreshRateSettings.NormalizeReadyUiControlHz(advanced.ReadyUiControlHz);
        advanced.RunningStatusPaintHz = RefreshRateSettings.NormalizeRunningStatusPaintHz(advanced.RunningStatusPaintHz);
        advanced.TimerOverlayRefreshHz = RefreshRateSettings.NormalizeTimerOverlayRefreshHz(advanced.TimerOverlayRefreshHz);
    }

    public static void NormalizeRace(RaceSettings race, RaceSettings defaults)
    {
        race.ServerUrl = string.IsNullOrWhiteSpace(race.ServerUrl)
            ? defaults.ServerUrl
            : race.ServerUrl.Trim();
        race.Nickname = race.Nickname?.Trim() ?? string.Empty;
        race.LastRoomCode = NormalizeRaceRoomCode(race.LastRoomCode);
        race.PreferredRole = RacePreferredRole.Normalize(race.PreferredRole);
        race.PreferredWorldSource = RacePreferredWorldSource.Normalize(race.PreferredWorldSource);
        race.PlayerTemplateCode = race.PlayerTemplateCode?.Trim() ?? string.Empty;
        race.WorldSetup ??= defaults.WorldSetup ?? new RaceWorldSetupSettings();
        defaults.WorldSetup ??= new RaceWorldSetupSettings();
        NormalizeRaceWorldSetup(race.WorldSetup);
        race.BossPenalty ??= defaults.BossPenalty ?? new RaceBossPenaltySettings();
        defaults.BossPenalty ??= new RaceBossPenaltySettings();
        NormalizeRaceBossPenalty(race.BossPenalty, defaults.BossPenalty);
        race.Voice ??= defaults.Voice ?? new RaceVoiceSettings();
        defaults.Voice ??= new RaceVoiceSettings();
        race.Voice.VoiceName = race.Voice.VoiceName?.Trim() ?? string.Empty;
        race.Voice.SpeedPercent = Math.Clamp(race.Voice.SpeedPercent, 50, 200);
        race.Voice.Volume = Math.Clamp(race.Voice.Volume, 0, 100);
          race.Leaderboard ??= defaults.Leaderboard;
          defaults.Leaderboard ??= new RaceLeaderboardSettings();
          race.Leaderboard.RankPlayerGap = Math.Clamp(race.Leaderboard.RankPlayerGap, 0, 1000);
          race.Leaderboard.PlayerIconGap = Math.Clamp(race.Leaderboard.PlayerIconGap, 0, 1000);
          race.Leaderboard.IconTimeGap = Math.Clamp(race.Leaderboard.IconTimeGap, 0, 1000);
          race.Leaderboard.RankAlignment = UiColumnAlignment.Normalize(race.Leaderboard.RankAlignment, UiColumnAlignment.Right);
          race.Leaderboard.PlayerAlignment = UiColumnAlignment.Normalize(race.Leaderboard.PlayerAlignment, UiColumnAlignment.Right);
          race.Leaderboard.IconAlignment = UiColumnAlignment.Normalize(race.Leaderboard.IconAlignment, UiColumnAlignment.Right);
          race.Leaderboard.TimeAlignment = UiColumnAlignment.Normalize(race.Leaderboard.TimeAlignment, UiColumnAlignment.Right);
        NormalizeColumn(race.Leaderboard.Rank ??= defaults.Leaderboard.Rank, defaults.Leaderboard.Rank);
        NormalizeColumn(race.Leaderboard.Player ??= defaults.Leaderboard.Player, defaults.Leaderboard.Player);
        NormalizeColumn(race.Leaderboard.Icon ??= defaults.Leaderboard.Icon, defaults.Leaderboard.Icon);
        NormalizeColumn(race.Leaderboard.Time ??= defaults.Leaderboard.Time, defaults.Leaderboard.Time);
        race.Leaderboard.TextEffects ??= defaults.Leaderboard.TextEffects ?? new RaceLeaderboardTextEffectSettings();
        defaults.Leaderboard.TextEffects ??= new RaceLeaderboardTextEffectSettings();
        NormalizeRaceLeaderboardEffect(race.Leaderboard.TextEffects.Rank ??= defaults.Leaderboard.TextEffects.Rank);
        NormalizeRaceLeaderboardEffect(race.Leaderboard.TextEffects.Player ??= defaults.Leaderboard.TextEffects.Player);
        NormalizeRaceLeaderboardEffect(race.Leaderboard.TextEffects.Icon ??= defaults.Leaderboard.TextEffects.Icon);
        NormalizeRaceLeaderboardEffect(race.Leaderboard.TextEffects.Time ??= defaults.Leaderboard.TextEffects.Time);
        race.Leaderboard.Colors ??= defaults.Leaderboard.Colors ?? new RaceLeaderboardColorSettings();
        defaults.Leaderboard.Colors ??= new RaceLeaderboardColorSettings();
        defaults.Leaderboard.Colors.RankGradient ??= new RaceLeaderboardRankGradientColorSettings();
        NormalizeRaceLeaderboardRankGradient(
            race.Leaderboard.Colors.RankGradient ??= defaults.Leaderboard.Colors.RankGradient,
            defaults.Leaderboard.Colors.RankGradient);
        NormalizeRaceLeaderboardColor(race.Leaderboard.Colors.Rank ??= defaults.Leaderboard.Colors.Rank, defaults.Leaderboard.Colors.Rank);
        NormalizeRaceLeaderboardColor(race.Leaderboard.Colors.Player ??= defaults.Leaderboard.Colors.Player, defaults.Leaderboard.Colors.Player);
        defaults.Leaderboard.Colors.PlayerSelf ??= CloneRaceLeaderboardColor(defaults.Leaderboard.Colors.Player);
        defaults.Leaderboard.Colors.PlayerOther ??= CloneRaceLeaderboardColor(defaults.Leaderboard.Colors.Player);
        race.Leaderboard.Colors.PlayerSelf ??= CloneRaceLeaderboardColor(race.Leaderboard.Colors.Player);
        race.Leaderboard.Colors.PlayerOther ??= CloneRaceLeaderboardColor(race.Leaderboard.Colors.Player);
        NormalizeRaceLeaderboardColor(race.Leaderboard.Colors.PlayerSelf, defaults.Leaderboard.Colors.PlayerSelf);
        NormalizeRaceLeaderboardColor(race.Leaderboard.Colors.PlayerOther, defaults.Leaderboard.Colors.PlayerOther);
        NormalizeRaceLeaderboardColor(race.Leaderboard.Colors.Icon ??= defaults.Leaderboard.Colors.Icon, defaults.Leaderboard.Colors.Icon);
        NormalizeRaceLeaderboardColor(race.Leaderboard.Colors.Time ??= defaults.Leaderboard.Colors.Time, defaults.Leaderboard.Colors.Time);
    }

    private static string NormalizeRaceRoomCode(string? value)
    {
        string roomCode = value?.Trim() ?? string.Empty;
        return roomCode.Length == 4 &&
            roomCode.All(character => character is >= '0' and <= '9')
                ? roomCode
                : string.Empty;
    }

    private static void NormalizeRaceWorldSetup(RaceWorldSetupSettings setup)
    {
        setup.Source = RacePreferredWorldSource.Normalize(setup.Source);
        setup.SeedText = setup.SeedText?.Trim() ?? string.Empty;
        setup.WorldSize = AutoCreateWorldSize.Normalize(setup.WorldSize);
        setup.WorldDifficulty = AutoCreateWorldDifficulty.Normalize(setup.WorldDifficulty);
        setup.WorldEvil = AutoCreateWorldEvil.Normalize(setup.WorldEvil);
        setup.SpecialSeeds = string.Join("|", AutoCreateSpecialWorldSeed.ParseList(setup.SpecialSeeds));
        setup.SecretSeeds = setup.SecretSeeds?.Trim() ?? string.Empty;
        setup.BossPenaltyEnabledKinds &= RaceWorldSetupSettings.AllBossPenaltyKinds;
        setup.PyramidItemMask = AutoCreatePyramidFilterItem.NormalizeMask(setup.PyramidItemMask);
        setup.PyramidDepth = AutoCreatePyramidDepth.Normalize(setup.PyramidDepth);
        setup.PyramidCoinPileMinimum = AutoCreatePyramidCoinPileMinimum.Normalize(setup.PyramidCoinPileMinimum);
        setup.CrimsonDistance = AutoCreateCrimsonDistance.Normalize(setup.CrimsonDistance);
        setup.JungleRouteDepth = AutoCreateJungleRouteDepth.Normalize(setup.JungleRouteDepth);
        setup.ResourceItemMask = AutoCreateResourceFilterItem.NormalizeMask(setup.ResourceItemMask);
        setup.LifeCrystalMinimum = AutoCreateResourceMinimum.NormalizeLifeCrystals(setup.LifeCrystalMinimum);
        setup.SpelunkerPotionMinimum = AutoCreateResourceMinimum.NormalizePotions(setup.SpelunkerPotionMinimum);
        setup.FeatherfallPotionMinimum = AutoCreateResourceMinimum.NormalizePotions(setup.FeatherfallPotionMinimum);
        AutoCreateAdvancedFilterEligibility.ClearUnsupportedFilters(setup);
    }

    private static void NormalizeRaceBossPenalty(
        RaceBossPenaltySettings settings,
        RaceBossPenaltySettings defaults)
    {
        var fallback = new RaceBossPenaltySettings();
        defaults.Skeletron ??= fallback.Skeletron;
        defaults.WallOfFlesh ??= fallback.WallOfFlesh;
        defaults.Destroyer ??= fallback.Destroyer;
        defaults.SkeletronPrime ??= fallback.SkeletronPrime;
        defaults.Twins ??= fallback.Twins;
        defaults.Plantera ??= fallback.Plantera;
        defaults.Golem ??= fallback.Golem;
        defaults.LunaticCultist ??= fallback.LunaticCultist;
        NormalizeRaceBossPenaltyBoss(settings.Skeletron ??= defaults.Skeletron);
        NormalizeRaceBossPenaltyBoss(settings.WallOfFlesh ??= defaults.WallOfFlesh);
        NormalizeRaceBossPenaltyBoss(settings.Destroyer ??= defaults.Destroyer);
        NormalizeRaceBossPenaltyBoss(settings.SkeletronPrime ??= defaults.SkeletronPrime);
        NormalizeRaceBossPenaltyBoss(settings.Twins ??= defaults.Twins);
        NormalizeRaceBossPenaltyBoss(settings.Plantera ??= defaults.Plantera);
        NormalizeRaceBossPenaltyBoss(settings.Golem ??= defaults.Golem);
        NormalizeRaceBossPenaltyBoss(settings.LunaticCultist ??= defaults.LunaticCultist);
    }

    private static void NormalizeRaceBossPenaltyBoss(RaceBossPenaltyBossSettings settings)
    {
        settings.JourneyBaseSeconds = Math.Clamp(settings.JourneyBaseSeconds, 0, RaceBossPenaltySettings.MaximumSeconds);
        settings.JourneyProportionalSeconds = Math.Clamp(settings.JourneyProportionalSeconds, 0, RaceBossPenaltySettings.MaximumSeconds);
        settings.ClassicBaseSeconds = Math.Clamp(settings.ClassicBaseSeconds, 0, RaceBossPenaltySettings.MaximumSeconds);
        settings.ClassicProportionalSeconds = Math.Clamp(settings.ClassicProportionalSeconds, 0, RaceBossPenaltySettings.MaximumSeconds);
        settings.ExpertBaseSeconds = Math.Clamp(settings.ExpertBaseSeconds, 0, RaceBossPenaltySettings.MaximumSeconds);
        settings.ExpertProportionalSeconds = Math.Clamp(settings.ExpertProportionalSeconds, 0, RaceBossPenaltySettings.MaximumSeconds);
        settings.MasterBaseSeconds = Math.Clamp(settings.MasterBaseSeconds, 0, RaceBossPenaltySettings.MaximumSeconds);
        settings.MasterProportionalSeconds = Math.Clamp(settings.MasterProportionalSeconds, 0, RaceBossPenaltySettings.MaximumSeconds);
    }

    public static void NormalizeColumnSettings(UiColumnLayoutSettings columns, UiColumnLayoutSettings defaults)
    {
        columns.ScalePercent = Math.Clamp(columns.ScalePercent, 25, 300);
        columns.IconNameGap = Math.Clamp(columns.IconNameGap, 0, 1000);
        columns.NameTimeGap = Math.Clamp(columns.NameTimeGap, 0, 1000);
        columns.TimeDeltaGap = Math.Clamp(columns.TimeDeltaGap, 0, 1000);
        columns.IconAlignment = UiColumnAlignment.Normalize(columns.IconAlignment, UiColumnAlignment.Right);
        columns.NameAlignment = UiColumnAlignment.Normalize(columns.NameAlignment, UiColumnAlignment.Center);
        columns.TimeAlignment = UiColumnAlignment.Normalize(columns.TimeAlignment, UiColumnAlignment.Right);
        columns.DeltaAlignment = UiColumnAlignment.Normalize(columns.DeltaAlignment, UiColumnAlignment.Left);

        foreach (UiColumnDescriptor descriptor in UiColumnDescriptors.All)
        {
            UiColumnSettings defaultColumn = descriptor.GetValue(defaults) ?? new UiColumnSettings();
            UiColumnSettings column = descriptor.GetValue(columns) ?? defaultColumn;
            descriptor.SetValue(columns, column);
            NormalizeColumn(column, defaultColumn);
        }

        UiColumnDescriptors.SynchronizeSharedWidths(columns);
    }

    private static void NormalizeColumn(UiColumnSettings column, UiColumnSettings defaults)
    {
        string fallbackFamily = string.IsNullOrWhiteSpace(defaults.FontFamily)
            ? UiFontDefaults.DefaultFamilyName
            : defaults.FontFamily.Trim();
        column.FontFamily = string.IsNullOrWhiteSpace(column.FontFamily)
            ? fallbackFamily
            : column.FontFamily.Trim();

        if (column.Width <= 0)
        {
            column.Width = defaults.Width;
        }

        if (column.FontSize <= 0)
        {
            column.FontSize = defaults.FontSize;
        }
    }

    public static void NormalizeTextEffects(UiTextEffectSettings effects)
    {
        foreach (UiTextEffectDescriptor descriptor in UiTextEffectDescriptors.All)
        {
            descriptor.SetOpacity(effects, ClampOpacityPercent(descriptor.GetOpacity(effects)));
            if (descriptor.GetShadow is not null && descriptor.SetShadow is not null)
            {
                descriptor.SetShadow(effects, ClampEffectPercent(descriptor.GetShadow(effects)));
            }

            if (descriptor.GetOutline is not null && descriptor.SetOutline is not null)
            {
                descriptor.SetOutline(effects, ClampEffectPercent(descriptor.GetOutline(effects)));
            }
        }
    }

    private static void NormalizeRaceLeaderboardEffect(RaceLeaderboardColumnEffectSettings effect)
    {
        effect.OpacityPercent = ClampOpacityPercent(effect.OpacityPercent);
        effect.ShadowPercent = ClampEffectPercent(effect.ShadowPercent);
        effect.OutlineThicknessPercent = ClampEffectPercent(effect.OutlineThicknessPercent);
    }

    private static void NormalizeRaceLeaderboardRankGradient(
        RaceLeaderboardRankGradientColorSettings gradient,
        RaceLeaderboardRankGradientColorSettings defaults)
    {
        gradient.Start = NormalizeColorText(gradient.Start, defaults.Start);
        gradient.Middle = NormalizeColorText(gradient.Middle, defaults.Middle);
        gradient.End = NormalizeColorText(gradient.End, defaults.End);
    }

    private static void NormalizeRaceLeaderboardColor(
        RaceLeaderboardColumnColorSettings color,
        RaceLeaderboardColumnColorSettings defaults)
    {
        color.Text = NormalizeColorText(color.Text, defaults.Text);
        color.Outline = NormalizeColorText(color.Outline, defaults.Outline);
        color.Shadow = NormalizeColorText(color.Shadow, defaults.Shadow);
    }

    private static RaceLeaderboardColumnColorSettings CloneRaceLeaderboardColor(RaceLeaderboardColumnColorSettings? source)
    {
        source ??= new RaceLeaderboardColumnColorSettings();
        return new RaceLeaderboardColumnColorSettings
        {
            Text = source.Text,
            Outline = source.Outline,
            Shadow = source.Shadow
        };
    }

    private static string NormalizeColorText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private static int ClampOpacityPercent(int value)
    {
        return Math.Clamp(value, 0, 100);
    }

    private static int ClampEffectPercent(int value)
    {
        return Math.Clamp(value, 0, 100);
    }
}
