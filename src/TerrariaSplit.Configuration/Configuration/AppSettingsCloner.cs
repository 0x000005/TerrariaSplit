namespace TerrariaSplit.Configuration;

public static class AppSettingsCloner
{
    public static AppSettings Clone(AppSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new AppSettings
        {
            General = Clone(source.General),
            Hotkeys = Clone(source.Hotkeys),
            Route = Clone(source.Route),
            Comparison = Clone(source.Comparison),
            Overlay = Clone(source.Overlay),
            Automation = Clone(source.Automation),
            Race = Clone(source.Race),
            PracticeWorlds = Clone(source.PracticeWorlds),
            Advanced = Clone(source.Advanced)
        };
    }

    private static GeneralSettings Clone(GeneralSettings? source)
    {
        source ??= new GeneralSettings();
        return new GeneralSettings
        {
            ShowMouseClickThroughIndicator = source.ShowMouseClickThroughIndicator,
            Language = source.Language,
            AlwaysOnTop = source.AlwaysOnTop,
            PracticeMode = source.PracticeMode
        };
    }

    private static HotkeySettings Clone(HotkeySettings? source)
    {
        source ??= new HotkeySettings();
        return new HotkeySettings
        {
            PauseResumeKey = source.PauseResumeKey,
            ResetKey = source.ResetKey,
            MouseClickThroughKey = source.MouseClickThroughKey,
            CreateWorldKey = source.CreateWorldKey,
            PracticeWorldKey = source.PracticeWorldKey
        };
    }

    private static RouteSettings Clone(RouteSettings? source)
    {
        source ??= new RouteSettings();
        return new RouteSettings
        {
            SplitRoute = (source.SplitRoute ?? []).Select(Clone).ToList(),
            ExpandSplitDetails = source.ExpandSplitDetails,
            CollapseSplitDetailsOnCompletion = source.CollapseSplitDetailsOnCompletion,
            EnableVisibleGroupCountLimit = source.EnableVisibleGroupCountLimit,
            VisibleGroupCountLimit = source.VisibleGroupCountLimit,
            CurrentGroupPosition = source.CurrentGroupPosition,
            ShowFinalGroup = source.ShowFinalGroup,
            ShowAllVisibleGroupsAfterFinalGroup = source.ShowAllVisibleGroupsAfterFinalGroup,
            ShowAllAttachedGroupsAfterFinalGroup = source.ShowAllAttachedGroupsAfterFinalGroup,
            ShowAllMultiConditionMainGroupsAfterFinalGroup = source.ShowAllMultiConditionMainGroupsAfterFinalGroup,
            AutoHideAttachedGroups = source.AutoHideAttachedGroups
        };
    }

    private static SplitRouteEntry Clone(SplitRouteEntry source)
    {
        return new SplitRouteEntry
        {
            Id = source.Id,
            Enabled = source.Enabled,
            DisplayName = source.DisplayName,
            Condition = source.Condition?.Clone() ?? SplitCondition.Fact(string.Empty),
            IconTargetIds = [.. source.IconTargetIds ?? []],
            IconOverride = new SplitIconOverride
            {
                Source = source.IconOverride?.Source ?? SplitIconOverrideSource.All,
                TargetId = source.IconOverride?.TargetId ?? string.Empty,
                FilePath = source.IconOverride?.FilePath ?? string.Empty
            },
            IsAttached = source.IsAttached,
            UseAdvancedConditionEditor = source.UseAdvancedConditionEditor,
            ExpandDetails = source.ExpandDetails
        };
    }

    private static ComparisonSettings Clone(ComparisonSettings? source)
    {
        source ??= new ComparisonSettings();
        return new ComparisonSettings
        {
            ReferenceSplitSets = (source.ReferenceSplitSets ?? []).Select(Clone).ToList(),
            ActiveReferenceSplitSet = source.ActiveReferenceSplitSet,
            UsePersonalBestAsReferenceTime = source.UsePersonalBestAsReferenceTime,
            PersonalBestTimeSets = (source.PersonalBestTimeSets ?? []).Select(Clone).ToList(),
            ActivePersonalBestTimeSet = source.ActivePersonalBestTimeSet,
            PersonalBestSegmentSets = (source.PersonalBestSegmentSets ?? []).Select(Clone).ToList(),
            ActivePersonalBestSegmentSet = source.ActivePersonalBestSegmentSet,
            PersonalBestTimes = new Dictionary<string, string>(
                source.PersonalBestTimes ?? [],
                StringComparer.OrdinalIgnoreCase),
            PersonalBestSegmentTimes = new Dictionary<string, string>(
                source.PersonalBestSegmentTimes ?? [],
                StringComparer.OrdinalIgnoreCase),
            AutoUpdatePersonalBestData = source.AutoUpdatePersonalBestData,
            AskBeforeUpdatingPersonalBestData = source.AskBeforeUpdatingPersonalBestData
        };
    }

    private static ReferenceSplitSet Clone(ReferenceSplitSet source)
    {
        return new ReferenceSplitSet
        {
            Name = source.Name,
            Splits = new Dictionary<string, string>(source.Splits ?? [], StringComparer.OrdinalIgnoreCase)
        };
    }

    private static OverlaySettings Clone(OverlaySettings? source)
    {
        source ??= new OverlaySettings();
        return new OverlaySettings
        {
            WindowPositionX = source.WindowPositionX,
            WindowPositionY = source.WindowPositionY,
            ShowSplitCompletionAnimation = source.ShowSplitCompletionAnimation,
            SplitCompletionAnimationDurationSeconds = source.SplitCompletionAnimationDurationSeconds,
            SplitCompletionOutlineThicknessPercent = source.SplitCompletionOutlineThicknessPercent,
            SplitCompletionSplitComparisons = new Dictionary<string, bool>(
                source.SplitCompletionSplitComparisons ?? [],
                StringComparer.OrdinalIgnoreCase),
            SplitCompletionSegmentComparisons = new Dictionary<string, bool>(
                source.SplitCompletionSegmentComparisons ?? [],
                StringComparer.OrdinalIgnoreCase),
            SplitCompletionOutlineSplitStyles = new Dictionary<string, string>(
                source.SplitCompletionOutlineSplitStyles ?? [],
                StringComparer.OrdinalIgnoreCase),
            SplitCompletionOutlineSegmentStyles = new Dictionary<string, string>(
                source.SplitCompletionOutlineSegmentStyles ?? [],
                StringComparer.OrdinalIgnoreCase),
            ShowCurrentSplitHighlight = source.ShowCurrentSplitHighlight,
            CurrentSplitHighlightScalePercent = source.CurrentSplitHighlightScalePercent,
            CurrentSplitDepthStrengthPercent = source.CurrentSplitDepthStrengthPercent,
            ShowEarlyDeltaTime = source.ShowEarlyDeltaTime,
            EarlyDeltaTimeSeconds = source.EarlyDeltaTimeSeconds,
            EnableDynamicDeltaTimeUnits = source.EnableDynamicDeltaTimeUnits,
            EnableDeltaGradientColor = source.EnableDeltaGradientColor,
            EnableCurrentDeltaGradientColor = source.EnableCurrentDeltaGradientColor,
            EnableTimerGradientColor = source.EnableTimerGradientColor,
            DeltaGradientThresholdSeconds = source.DeltaGradientThresholdSeconds,
            DeltaGradientCurve = source.DeltaGradientCurve,
            ShowSegmentBestDeltaHighlight = source.ShowSegmentBestDeltaHighlight,
            SegmentBestDeltaHighlightStyles = new Dictionary<string, string>(
                source.SegmentBestDeltaHighlightStyles ?? [],
                StringComparer.OrdinalIgnoreCase),
            Colors = Clone(source.Colors),
            Sounds = Clone(source.Sounds),
            Columns = Clone(source.Columns),
            TextEffects = Clone(source.TextEffects),
            EnableDefeatedBossIconLighting = source.EnableDefeatedBossIconLighting,
            UndefeatedIconGrayscalePercent = source.UndefeatedIconGrayscalePercent,
            UndefeatedIconBrightnessPercent = source.UndefeatedIconBrightnessPercent,
            CurrentBossIconGrayscaleWeakenPercent = source.CurrentBossIconGrayscaleWeakenPercent,
            CurrentBossIconBrightnessBoostPercent = source.CurrentBossIconBrightnessBoostPercent
        };
    }

    private static UiColorSettings Clone(UiColorSettings? source)
    {
        source ??= new UiColorSettings();
        return new UiColorSettings
        {
            ReferenceText = source.ReferenceText,
            ReferenceTextOutline = source.ReferenceTextOutline,
            ReferenceTextShadow = source.ReferenceTextShadow,
            ActiveReferenceText = source.ActiveReferenceText,
            ActiveReferenceTextOutline = source.ActiveReferenceTextOutline,
            ActiveReferenceTextShadow = source.ActiveReferenceTextShadow,
            SplitText = source.SplitText,
            SplitTextOutline = source.SplitTextOutline,
            SplitTextShadow = source.SplitTextShadow,
            IconOutline = source.IconOutline,
            IconShadow = source.IconShadow,
            DeltaAheadText = source.DeltaAheadText,
            DeltaAheadTextOutline = source.DeltaAheadTextOutline,
            DeltaAheadTextShadow = source.DeltaAheadTextShadow,
            DeltaBehindText = source.DeltaBehindText,
            DeltaBehindTextOutline = source.DeltaBehindTextOutline,
            DeltaBehindTextShadow = source.DeltaBehindTextShadow,
            TimerText = source.TimerText,
            TimerTextOutline = source.TimerTextOutline,
            TimerTextShadow = source.TimerTextShadow,
            TimerAheadText = source.TimerAheadText,
            TimerAheadTextOutline = source.TimerAheadTextOutline,
            TimerAheadTextShadow = source.TimerAheadTextShadow,
            TimerBehindText = source.TimerBehindText,
            TimerBehindTextOutline = source.TimerBehindTextOutline,
            TimerBehindTextShadow = source.TimerBehindTextShadow,
            TimerRecordText = source.TimerRecordText,
            TimerRecordTextOutline = source.TimerRecordTextOutline,
            TimerRecordTextShadow = source.TimerRecordTextShadow,
            TimerNoRecordText = source.TimerNoRecordText,
            TimerNoRecordTextOutline = source.TimerNoRecordTextOutline,
            TimerNoRecordTextShadow = source.TimerNoRecordTextShadow,
            TimerPausedText = source.TimerPausedText,
            TimerPausedTextOutline = source.TimerPausedTextOutline,
            TimerPausedTextShadow = source.TimerPausedTextShadow,
            SplitCompletionSegmentLabelText = source.SplitCompletionSegmentLabelText,
            SplitCompletionLabelText = source.SplitCompletionLabelText,
            SplitCompletionSegmentTimeText = source.SplitCompletionSegmentTimeText,
            SplitCompletionTimeText = source.SplitCompletionTimeText
        };
    }

    private static UiSoundSettings Clone(UiSoundSettings? source)
    {
        source ??= new UiSoundSettings();
        return new UiSoundSettings
        {
            Pause = source.Pause,
            Resume = source.Resume,
            Reset = source.Reset,
            EnterWorld = source.EnterWorld,
            SplitBehindReferenceBehindSegment = source.SplitBehindReferenceBehindSegment,
            SplitBehindReferenceAheadSegment = source.SplitBehindReferenceAheadSegment,
            SplitAheadReferenceBehindSegment = source.SplitAheadReferenceBehindSegment,
            SplitAheadReferenceAheadSegment = source.SplitAheadReferenceAheadSegment,
            FinalGroupBehindReferenceBehindSegment = source.FinalGroupBehindReferenceBehindSegment,
            FinalGroupBehindReferenceAheadSegment = source.FinalGroupBehindReferenceAheadSegment,
            FinalGroupAheadReferenceBehindSegment = source.FinalGroupAheadReferenceBehindSegment,
            FinalGroupAheadReferenceAheadSegment = source.FinalGroupAheadReferenceAheadSegment
        };
    }

    private static UiColumnLayoutSettings Clone(UiColumnLayoutSettings? source)
    {
        source ??= new UiColumnLayoutSettings();
        return new UiColumnLayoutSettings
        {
            ScalePercent = source.ScalePercent,
            Icon = Clone(source.Icon),
            Time = Clone(source.Time),
            Delta = Clone(source.Delta),
            AttachedIcon = Clone(source.AttachedIcon),
            AttachedTime = Clone(source.AttachedTime),
            AttachedDelta = Clone(source.AttachedDelta),
            Timer = Clone(source.Timer),
            TimerMilliseconds = Clone(source.TimerMilliseconds),
            TimerOffsetX = source.TimerOffsetX,
            TimerOffsetY = source.TimerOffsetY
        };
    }

    private static UiColumnSettings Clone(UiColumnSettings? source)
    {
        source ??= new UiColumnSettings();
        return new UiColumnSettings
        {
            Show = source.Show,
            Width = source.Width,
            FontFamily = source.FontFamily,
            FontSize = source.FontSize,
            Bold = source.Bold
        };
    }

    private static UiTextEffectSettings Clone(UiTextEffectSettings? source)
    {
        source ??= new UiTextEffectSettings();
        return new UiTextEffectSettings
        {
            IconOpacityPercent = source.IconOpacityPercent,
            IconShadowPercent = source.IconShadowPercent,
            IconOutlineThicknessPercent = source.IconOutlineThicknessPercent,
            TimeOpacityPercent = source.TimeOpacityPercent,
            TimeShadowPercent = source.TimeShadowPercent,
            TimeOutlineThicknessPercent = source.TimeOutlineThicknessPercent,
            DeltaOpacityPercent = source.DeltaOpacityPercent,
            DeltaShadowPercent = source.DeltaShadowPercent,
            DeltaOutlineThicknessPercent = source.DeltaOutlineThicknessPercent,
            AttachedIconOpacityPercent = source.AttachedIconOpacityPercent,
            AttachedIconShadowPercent = source.AttachedIconShadowPercent,
            AttachedIconOutlineThicknessPercent = source.AttachedIconOutlineThicknessPercent,
            AttachedTimeOpacityPercent = source.AttachedTimeOpacityPercent,
            AttachedTimeShadowPercent = source.AttachedTimeShadowPercent,
            AttachedTimeOutlineThicknessPercent = source.AttachedTimeOutlineThicknessPercent,
            AttachedDeltaOpacityPercent = source.AttachedDeltaOpacityPercent,
            AttachedDeltaShadowPercent = source.AttachedDeltaShadowPercent,
            AttachedDeltaOutlineThicknessPercent = source.AttachedDeltaOutlineThicknessPercent,
            TimerOpacityPercent = source.TimerOpacityPercent,
            TimerShadowPercent = source.TimerShadowPercent,
            TimerOutlineThicknessPercent = source.TimerOutlineThicknessPercent,
            TimerMillisecondsOpacityPercent = source.TimerMillisecondsOpacityPercent,
            TimerMillisecondsShadowPercent = source.TimerMillisecondsShadowPercent,
            TimerMillisecondsOutlineThicknessPercent = source.TimerMillisecondsOutlineThicknessPercent
        };
    }

    private static AutomationSettings Clone(AutomationSettings? source)
    {
        source ??= new AutomationSettings();
        return new AutomationSettings
        {
            AutoCreate = Clone(source.AutoCreate)
        };
    }

    private static AutoCreateWorldSettings Clone(AutoCreateWorldSettings? source)
    {
        source ??= new AutoCreateWorldSettings();
        return new AutoCreateWorldSettings
        {
            PlayerName = source.PlayerName,
            PlayerTemplateCode = source.PlayerTemplateCode,
            PlayerDifficulty = source.PlayerDifficulty,
            PreserveExistingSaves = source.PreserveExistingSaves,
            WorldSize = source.WorldSize,
            WorldDifficulty = source.WorldDifficulty,
            WorldEvil = source.WorldEvil,
            SpecialSeeds = source.SpecialSeeds,
            SecretSeeds = source.SecretSeeds,
            EnableZenithStarCatch = source.EnableZenithStarCatch,
            ZenithStarCatchStopStage = source.ZenithStarCatchStopStage,
            ZenithStarCatchSpeedSliderValue = source.ZenithStarCatchSpeedSliderValue,
            EnablePyramidFilter = source.EnablePyramidFilter,
            PyramidFilterItemMask = source.PyramidFilterItemMask,
            RequireCrimsonBetweenDungeonAndSpawn = source.RequireCrimsonBetweenDungeonAndSpawn,
            ReturnToMainMenuOnFilterFailure = source.ReturnToMainMenuOnFilterFailure,
            EnableWorldPool = source.EnableWorldPool,
            WorldPoolTargetCount = source.WorldPoolTargetCount,
            ShortActionDelayMilliseconds = source.ShortActionDelayMilliseconds,
            MenuActionDelayMilliseconds = source.MenuActionDelayMilliseconds,
            PyramidFilterPostDelayMilliseconds = source.PyramidFilterPostDelayMilliseconds,
            WindowActivationDelayMilliseconds = source.WindowActivationDelayMilliseconds,
            ClickFocusDelayMilliseconds = source.ClickFocusDelayMilliseconds,
            InputPressDurationMilliseconds = source.InputPressDurationMilliseconds
        };
    }

    private static RaceSettings Clone(RaceSettings? source)
    {
        source ??= new RaceSettings();
        return new RaceSettings
        {
            ServerUrl = source.ServerUrl,
            Nickname = source.Nickname,
            PreferredRole = source.PreferredRole,
            PreferredWorldSource = source.PreferredWorldSource,
            Leaderboard = Clone(source.Leaderboard)
        };
    }

    private static RaceLeaderboardSettings Clone(RaceLeaderboardSettings? source)
    {
        source ??= new RaceLeaderboardSettings();
        return new RaceLeaderboardSettings
        {
            UseRankColorForMainTimer = source.UseRankColorForMainTimer,
            Rank = Clone(source.Rank),
            Player = Clone(source.Player),
            Icon = Clone(source.Icon),
            Time = Clone(source.Time),
            TextEffects = Clone(source.TextEffects),
            Colors = Clone(source.Colors)
        };
    }

    private static RaceLeaderboardTextEffectSettings Clone(RaceLeaderboardTextEffectSettings? source)
    {
        source ??= new RaceLeaderboardTextEffectSettings();
        return new RaceLeaderboardTextEffectSettings
        {
            Rank = Clone(source.Rank),
            Player = Clone(source.Player),
            Icon = Clone(source.Icon),
            Time = Clone(source.Time)
        };
    }

    private static RaceLeaderboardColumnEffectSettings Clone(RaceLeaderboardColumnEffectSettings? source)
    {
        source ??= new RaceLeaderboardColumnEffectSettings();
        return new RaceLeaderboardColumnEffectSettings
        {
            OpacityPercent = source.OpacityPercent,
            ShadowPercent = source.ShadowPercent,
            OutlineThicknessPercent = source.OutlineThicknessPercent
        };
    }

    private static RaceLeaderboardColorSettings Clone(RaceLeaderboardColorSettings? source)
    {
        source ??= new RaceLeaderboardColorSettings();
        return new RaceLeaderboardColorSettings
        {
            RankGradient = Clone(source.RankGradient),
            Rank = Clone(source.Rank),
            Player = Clone(source.Player),
            PlayerSelf = Clone(source.PlayerSelf),
            PlayerOther = Clone(source.PlayerOther),
            Icon = Clone(source.Icon),
            Time = Clone(source.Time)
        };
    }

    private static RaceLeaderboardRankGradientColorSettings Clone(RaceLeaderboardRankGradientColorSettings? source)
    {
        source ??= new RaceLeaderboardRankGradientColorSettings();
        return new RaceLeaderboardRankGradientColorSettings
        {
            Start = source.Start,
            Middle = source.Middle,
            End = source.End
        };
    }

    private static RaceLeaderboardColumnColorSettings Clone(RaceLeaderboardColumnColorSettings? source)
    {
        source ??= new RaceLeaderboardColumnColorSettings();
        return new RaceLeaderboardColumnColorSettings
        {
            Text = source.Text,
            Outline = source.Outline,
            Shadow = source.Shadow
        };
    }

    private static PracticeWorldSettings Clone(PracticeWorldSettings? source)
    {
        source ??= new PracticeWorldSettings();
        return new PracticeWorldSettings
        {
            Slots = (source.Slots ?? []).Select(slot => new PracticeWorldSlot
            {
                Name = slot.Name,
                PlayerFilePath = slot.PlayerFilePath,
                WorldFilePath = slot.WorldFilePath
            }).ToList()
        };
    }

    private static AdvancedSettings Clone(AdvancedSettings? source)
    {
        source ??= new AdvancedSettings();
        return new AdvancedSettings
        {
            EnableTerrariaUiScalePatch = source.EnableTerrariaUiScalePatch,
            EnableRtssOverlay = source.EnableRtssOverlay,
            RtssExecutablePath = source.RtssExecutablePath,
            RtssOverlayX = source.RtssOverlayX,
            RtssOverlayY = source.RtssOverlayY,
            RtssOverlayZoom = source.RtssOverlayZoom,
            ReadyWatcherPollHz = source.ReadyWatcherPollHz,
            ReadyUiControlHz = source.ReadyUiControlHz,
            RunningStatusPaintHz = source.RunningStatusPaintHz,
            TimerOverlayRefreshHz = source.TimerOverlayRefreshHz
        };
    }
}
