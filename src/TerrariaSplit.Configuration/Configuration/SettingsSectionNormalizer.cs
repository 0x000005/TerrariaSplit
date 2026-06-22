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
        advanced.ReadyWatcherPollHz = RefreshRateSettings.NormalizeReadyWatcherPollHz(advanced.ReadyWatcherPollHz);
        advanced.ReadyUiControlHz = RefreshRateSettings.NormalizeReadyUiControlHz(advanced.ReadyUiControlHz);
        advanced.RunningStatusPaintHz = RefreshRateSettings.NormalizeRunningStatusPaintHz(advanced.RunningStatusPaintHz);
        advanced.TimerOverlayRefreshHz = RefreshRateSettings.NormalizeTimerOverlayRefreshHz(advanced.TimerOverlayRefreshHz);
    }

    public static void NormalizeColumnSettings(UiColumnLayoutSettings columns, UiColumnLayoutSettings defaults)
    {
        columns.ScalePercent = Math.Clamp(columns.ScalePercent, 25, 300);

        foreach (UiColumnDescriptor descriptor in UiColumnDescriptors.All)
        {
            UiColumnSettings defaultColumn = descriptor.GetValue(defaults) ?? new UiColumnSettings();
            UiColumnSettings column = descriptor.GetValue(columns) ?? defaultColumn;
            descriptor.SetValue(columns, column);
            NormalizeColumn(column, defaultColumn);
        }
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
            descriptor.SetOpacity(effects, ClampPercent(descriptor.GetOpacity(effects)));
            if (descriptor.GetShadow is not null && descriptor.SetShadow is not null)
            {
                descriptor.SetShadow(effects, ClampPercent(descriptor.GetShadow(effects)));
            }

            if (descriptor.GetOutline is not null && descriptor.SetOutline is not null)
            {
                descriptor.SetOutline(effects, ClampOutlinePercent(descriptor.GetOutline(effects)));
            }
        }
    }

    private static int ClampPercent(int value)
    {
        return Math.Clamp(value, 0, 100);
    }

    private static int ClampOutlinePercent(int value)
    {
        return Math.Clamp(value, 0, 200);
    }
}
