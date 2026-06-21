using System.Text.Json.Serialization;

namespace TerrariaSplit.Configuration;

internal sealed class AppSettings
{
    public const string PersonalBestReferenceSetName = "PB";

    public GeneralSettings General { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
    public RouteSettings Route { get; set; } = new();
    public ComparisonSettings Comparison { get; set; } = new();
    public OverlaySettings Overlay { get; set; } = new();
    public AutomationSettings Automation { get; set; } = new();
    public PracticeWorldSettings PracticeWorlds { get; set; } = new();
    public AdvancedSettings Advanced { get; set; } = new();

    [JsonIgnore]
    public string PauseResumeKey
    {
        get => (Hotkeys ??= new HotkeySettings()).PauseResumeKey;
        set => (Hotkeys ??= new HotkeySettings()).PauseResumeKey = value;
    }

    [JsonIgnore]
    public string ResetKey
    {
        get => (Hotkeys ??= new HotkeySettings()).ResetKey;
        set => (Hotkeys ??= new HotkeySettings()).ResetKey = value;
    }

    [JsonIgnore]
    public string MouseClickThroughKey
    {
        get => (Hotkeys ??= new HotkeySettings()).MouseClickThroughKey;
        set => (Hotkeys ??= new HotkeySettings()).MouseClickThroughKey = value;
    }

    [JsonIgnore]
    public string CreateWorldKey
    {
        get => (Hotkeys ??= new HotkeySettings()).CreateWorldKey;
        set => (Hotkeys ??= new HotkeySettings()).CreateWorldKey = value;
    }

    [JsonIgnore]
    public string PracticeWorldKey
    {
        get => (Hotkeys ??= new HotkeySettings()).PracticeWorldKey;
        set => (Hotkeys ??= new HotkeySettings()).PracticeWorldKey = value;
    }

    [JsonIgnore]
    public bool ShowMouseClickThroughIndicator
    {
        get => (General ??= new GeneralSettings()).ShowMouseClickThroughIndicator;
        set => (General ??= new GeneralSettings()).ShowMouseClickThroughIndicator = value;
    }

    [JsonIgnore]
    public string Language
    {
        get => (General ??= new GeneralSettings()).Language;
        set => (General ??= new GeneralSettings()).Language = value;
    }

    [JsonIgnore]
    public bool AlwaysOnTop
    {
        get => (General ??= new GeneralSettings()).AlwaysOnTop;
        set => (General ??= new GeneralSettings()).AlwaysOnTop = value;
    }

    [JsonIgnore]
    public bool PracticeMode
    {
        get => (General ??= new GeneralSettings()).PracticeMode;
        set => (General ??= new GeneralSettings()).PracticeMode = value;
    }

    [JsonIgnore]
    public List<SplitRouteEntry> SplitRoute
    {
        get => (Route ??= new RouteSettings()).SplitRoute;
        set => (Route ??= new RouteSettings()).SplitRoute = value;
    }

    [JsonIgnore]
    public bool ExpandSplitDetails
    {
        get => (Route ??= new RouteSettings()).ExpandSplitDetails;
        set => (Route ??= new RouteSettings()).ExpandSplitDetails = value;
    }

    [JsonIgnore]
    public bool CollapseSplitDetailsOnCompletion
    {
        get => (Route ??= new RouteSettings()).CollapseSplitDetailsOnCompletion;
        set => (Route ??= new RouteSettings()).CollapseSplitDetailsOnCompletion = value;
    }

    [JsonIgnore]
    public bool AutoHideAttachedGroups
    {
        get => (Route ??= new RouteSettings()).AutoHideAttachedGroups;
        set => (Route ??= new RouteSettings()).AutoHideAttachedGroups = value;
    }

    [JsonIgnore]
    public bool AttachedGroupsAffectTimerComparison
    {
        get => (Route ??= new RouteSettings()).AttachedGroupsAffectTimerComparison;
        set => (Route ??= new RouteSettings()).AttachedGroupsAffectTimerComparison = value;
    }

    [JsonIgnore]
    public List<ReferenceSplitSet> ReferenceSplitSets
    {
        get => (Comparison ??= new ComparisonSettings()).ReferenceSplitSets;
        set => (Comparison ??= new ComparisonSettings()).ReferenceSplitSets = value;
    }

    [JsonIgnore]
    public string ActiveReferenceSplitSet
    {
        get => (Comparison ??= new ComparisonSettings()).ActiveReferenceSplitSet;
        set => (Comparison ??= new ComparisonSettings()).ActiveReferenceSplitSet = value;
    }

    [JsonIgnore]
    public bool UsePersonalBestAsReferenceTime
    {
        get => (Comparison ??= new ComparisonSettings()).UsePersonalBestAsReferenceTime;
        set => (Comparison ??= new ComparisonSettings()).UsePersonalBestAsReferenceTime = value;
    }

    [JsonIgnore]
    public List<ReferenceSplitSet> PersonalBestTimeSets
    {
        get => (Comparison ??= new ComparisonSettings()).PersonalBestTimeSets;
        set => (Comparison ??= new ComparisonSettings()).PersonalBestTimeSets = value;
    }

    [JsonIgnore]
    public string ActivePersonalBestTimeSet
    {
        get => (Comparison ??= new ComparisonSettings()).ActivePersonalBestTimeSet;
        set => (Comparison ??= new ComparisonSettings()).ActivePersonalBestTimeSet = value;
    }

    [JsonIgnore]
    public List<ReferenceSplitSet> PersonalBestSegmentSets
    {
        get => (Comparison ??= new ComparisonSettings()).PersonalBestSegmentSets;
        set => (Comparison ??= new ComparisonSettings()).PersonalBestSegmentSets = value;
    }

    [JsonIgnore]
    public string ActivePersonalBestSegmentSet
    {
        get => (Comparison ??= new ComparisonSettings()).ActivePersonalBestSegmentSet;
        set => (Comparison ??= new ComparisonSettings()).ActivePersonalBestSegmentSet = value;
    }

    [JsonIgnore]
    public Dictionary<string, string> PersonalBestTimes
    {
        get => (Comparison ??= new ComparisonSettings()).PersonalBestTimes;
        set => (Comparison ??= new ComparisonSettings()).PersonalBestTimes = value;
    }

    [JsonIgnore]
    public Dictionary<string, string> PersonalBestSegmentTimes
    {
        get => (Comparison ??= new ComparisonSettings()).PersonalBestSegmentTimes;
        set => (Comparison ??= new ComparisonSettings()).PersonalBestSegmentTimes = value;
    }

    [JsonIgnore]
    public bool AutoUpdatePersonalBestData
    {
        get => (Comparison ??= new ComparisonSettings()).AutoUpdatePersonalBestData;
        set => (Comparison ??= new ComparisonSettings()).AutoUpdatePersonalBestData = value;
    }

    [JsonIgnore]
    public bool AskBeforeUpdatingPersonalBestData
    {
        get => (Comparison ??= new ComparisonSettings()).AskBeforeUpdatingPersonalBestData;
        set => (Comparison ??= new ComparisonSettings()).AskBeforeUpdatingPersonalBestData = value;
    }

    [JsonIgnore]
    public bool ShowSplitCompletionAnimation
    {
        get => (Overlay ??= new OverlaySettings()).ShowSplitCompletionAnimation;
        set => (Overlay ??= new OverlaySettings()).ShowSplitCompletionAnimation = value;
    }

    [JsonIgnore]
    public float SplitCompletionAnimationDurationSeconds
    {
        get => (Overlay ??= new OverlaySettings()).SplitCompletionAnimationDurationSeconds;
        set => (Overlay ??= new OverlaySettings()).SplitCompletionAnimationDurationSeconds = value;
    }

    [JsonIgnore]
    public int SplitCompletionOutlineThicknessPercent
    {
        get => (Overlay ??= new OverlaySettings()).SplitCompletionOutlineThicknessPercent;
        set => (Overlay ??= new OverlaySettings()).SplitCompletionOutlineThicknessPercent = value;
    }

    [JsonIgnore]
    public Dictionary<string, bool> SplitCompletionSplitComparisons
    {
        get => (Overlay ??= new OverlaySettings()).SplitCompletionSplitComparisons;
        set => (Overlay ??= new OverlaySettings()).SplitCompletionSplitComparisons = value;
    }

    [JsonIgnore]
    public Dictionary<string, bool> SplitCompletionSegmentComparisons
    {
        get => (Overlay ??= new OverlaySettings()).SplitCompletionSegmentComparisons;
        set => (Overlay ??= new OverlaySettings()).SplitCompletionSegmentComparisons = value;
    }

    [JsonIgnore]
    public Dictionary<string, string> SplitCompletionOutlineSplitStyles
    {
        get => (Overlay ??= new OverlaySettings()).SplitCompletionOutlineSplitStyles;
        set => (Overlay ??= new OverlaySettings()).SplitCompletionOutlineSplitStyles = value;
    }

    [JsonIgnore]
    public Dictionary<string, string> SplitCompletionOutlineSegmentStyles
    {
        get => (Overlay ??= new OverlaySettings()).SplitCompletionOutlineSegmentStyles;
        set => (Overlay ??= new OverlaySettings()).SplitCompletionOutlineSegmentStyles = value;
    }

    [JsonIgnore]
    public bool ShowCurrentSplitHighlight
    {
        get => (Overlay ??= new OverlaySettings()).ShowCurrentSplitHighlight;
        set => (Overlay ??= new OverlaySettings()).ShowCurrentSplitHighlight = value;
    }

    [JsonIgnore]
    public int CurrentSplitHighlightScalePercent
    {
        get => (Overlay ??= new OverlaySettings()).CurrentSplitHighlightScalePercent;
        set => (Overlay ??= new OverlaySettings()).CurrentSplitHighlightScalePercent = value;
    }

    [JsonIgnore]
    public int CurrentSplitDepthStrengthPercent
    {
        get => (Overlay ??= new OverlaySettings()).CurrentSplitDepthStrengthPercent;
        set => (Overlay ??= new OverlaySettings()).CurrentSplitDepthStrengthPercent = value;
    }

    [JsonIgnore]
    public bool ShowEarlyDeltaTime
    {
        get => (Overlay ??= new OverlaySettings()).ShowEarlyDeltaTime;
        set => (Overlay ??= new OverlaySettings()).ShowEarlyDeltaTime = value;
    }

    [JsonIgnore]
    public int EarlyDeltaTimeSeconds
    {
        get => (Overlay ??= new OverlaySettings()).EarlyDeltaTimeSeconds;
        set => (Overlay ??= new OverlaySettings()).EarlyDeltaTimeSeconds = value;
    }

    [JsonIgnore]
    public bool EnableDynamicDeltaTimeUnits
    {
        get => (Overlay ??= new OverlaySettings()).EnableDynamicDeltaTimeUnits;
        set => (Overlay ??= new OverlaySettings()).EnableDynamicDeltaTimeUnits = value;
    }

    [JsonIgnore]
    public bool EnableDeltaGradientColor
    {
        get => (Overlay ??= new OverlaySettings()).EnableDeltaGradientColor;
        set => (Overlay ??= new OverlaySettings()).EnableDeltaGradientColor = value;
    }

    [JsonIgnore]
    public bool EnableCurrentDeltaGradientColor
    {
        get => (Overlay ??= new OverlaySettings()).EnableCurrentDeltaGradientColor;
        set => (Overlay ??= new OverlaySettings()).EnableCurrentDeltaGradientColor = value;
    }

    [JsonIgnore]
    public bool EnableTimerGradientColor
    {
        get => (Overlay ??= new OverlaySettings()).EnableTimerGradientColor;
        set => (Overlay ??= new OverlaySettings()).EnableTimerGradientColor = value;
    }

    [JsonIgnore]
    public int DeltaGradientThresholdSeconds
    {
        get => (Overlay ??= new OverlaySettings()).DeltaGradientThresholdSeconds;
        set => (Overlay ??= new OverlaySettings()).DeltaGradientThresholdSeconds = value;
    }

    [JsonIgnore]
    public string DeltaGradientCurve
    {
        get => (Overlay ??= new OverlaySettings()).DeltaGradientCurve;
        set => (Overlay ??= new OverlaySettings()).DeltaGradientCurve = value;
    }

    [JsonIgnore]
    public bool ShowSegmentBestDeltaHighlight
    {
        get => (Overlay ??= new OverlaySettings()).ShowSegmentBestDeltaHighlight;
        set => (Overlay ??= new OverlaySettings()).ShowSegmentBestDeltaHighlight = value;
    }

    [JsonIgnore]
    public Dictionary<string, string> SegmentBestDeltaHighlightStyles
    {
        get => (Overlay ??= new OverlaySettings()).SegmentBestDeltaHighlightStyles;
        set => (Overlay ??= new OverlaySettings()).SegmentBestDeltaHighlightStyles = value;
    }

    [JsonIgnore]
    public UiColorSettings Colors
    {
        get => (Overlay ??= new OverlaySettings()).Colors;
        set => (Overlay ??= new OverlaySettings()).Colors = value;
    }

    [JsonIgnore]
    public UiSoundSettings Sounds
    {
        get => (Overlay ??= new OverlaySettings()).Sounds;
        set => (Overlay ??= new OverlaySettings()).Sounds = value;
    }

    [JsonIgnore]
    public UiColumnLayoutSettings Columns
    {
        get => (Overlay ??= new OverlaySettings()).Columns;
        set => (Overlay ??= new OverlaySettings()).Columns = value;
    }

    [JsonIgnore]
    public UiTextEffectSettings TextEffects
    {
        get => (Overlay ??= new OverlaySettings()).TextEffects;
        set => (Overlay ??= new OverlaySettings()).TextEffects = value;
    }

    [JsonIgnore]
    public AutoCreateWorldSettings AutoCreate
    {
        get => (Automation ??= new AutomationSettings()).AutoCreate;
        set => (Automation ??= new AutomationSettings()).AutoCreate = value;
    }

    [JsonIgnore]
    public bool EnableDefeatedBossIconLighting
    {
        get => (Overlay ??= new OverlaySettings()).EnableDefeatedBossIconLighting;
        set => (Overlay ??= new OverlaySettings()).EnableDefeatedBossIconLighting = value;
    }

    [JsonIgnore]
    public int UndefeatedIconGrayscalePercent
    {
        get => (Overlay ??= new OverlaySettings()).UndefeatedIconGrayscalePercent;
        set => (Overlay ??= new OverlaySettings()).UndefeatedIconGrayscalePercent = value;
    }

    [JsonIgnore]
    public int UndefeatedIconBrightnessPercent
    {
        get => (Overlay ??= new OverlaySettings()).UndefeatedIconBrightnessPercent;
        set => (Overlay ??= new OverlaySettings()).UndefeatedIconBrightnessPercent = value;
    }

    [JsonIgnore]
    public int CurrentBossIconGrayscaleWeakenPercent
    {
        get => (Overlay ??= new OverlaySettings()).CurrentBossIconGrayscaleWeakenPercent;
        set => (Overlay ??= new OverlaySettings()).CurrentBossIconGrayscaleWeakenPercent = value;
    }

    [JsonIgnore]
    public int CurrentBossIconBrightnessBoostPercent
    {
        get => (Overlay ??= new OverlaySettings()).CurrentBossIconBrightnessBoostPercent;
        set => (Overlay ??= new OverlaySettings()).CurrentBossIconBrightnessBoostPercent = value;
    }

    public bool TryGetReferenceSplit(SplitDefinition definition, out TimeSpan split)
    {
        return SplitConditionDataRows.TryGetSplitTime(this, GetActiveReferenceSet().Splits, definition, out split);
    }

    public string GetReferenceText(string name)
    {
        return GetActiveReferenceSet().Splits.TryGetValue(name, out string? value) ? value : string.Empty;
    }

    public string GetPersonalBestTimeText(string name)
    {
        return PersonalBestTimes.TryGetValue(name, out string? value) ? value : string.Empty;
    }

    public string GetPersonalBestSegmentText(string name)
    {
        return PersonalBestSegmentTimes.TryGetValue(name, out string? value) ? value : string.Empty;
    }

    public void SetReferenceText(string name, string value)
    {
        if (UsePersonalBestAsReferenceTime)
        {
            return;
        }

        GetActiveReferenceSet().Splits[name] = value;
    }

    public void SetPersonalBestTimeText(string name, string value)
    {
        PersonalBestTimes[name] = value;
    }

    public void SetPersonalBestSegmentText(string name, string value)
    {
        PersonalBestSegmentTimes[name] = value;
    }

    public ReferenceSplitSet GetActiveReferenceSet()
    {
        if (UsePersonalBestAsReferenceTime)
        {
            return CreatePersonalBestReferenceSet();
        }

        ReferenceSplitSet? activeSet = ReferenceSplitSets.FirstOrDefault(
            set => string.Equals(set.Name, ActiveReferenceSplitSet, StringComparison.OrdinalIgnoreCase));
        if (activeSet is not null)
        {
            return activeSet;
        }

        if (ReferenceSplitSets.Count == 0)
        {
            ReferenceSplitSets.Add(CreateReferenceSet("WR", keys: SplitConditionDataRows.Build(this).Select(row => row.Key)));
        }

        ActiveReferenceSplitSet = ReferenceSplitSets[0].Name;
        return ReferenceSplitSets[0];
    }

    public ReferenceSplitSet CreatePersonalBestReferenceSet()
    {
        return CreateReferenceSet(PersonalBestReferenceSetName, PersonalBestTimes, SplitConditionDataRows.Build(this).Select(row => row.Key));
    }

    public ReferenceSplitSet GetActivePersonalBestTimeSet()
    {
        ReferenceSplitSet set = GetActivePersonalSet(
            PersonalBestTimeSets,
            ActivePersonalBestTimeSet,
            "Personal",
            PersonalBestTimes,
            out string activeName);
        ActivePersonalBestTimeSet = activeName;
        return set;
    }

    public ReferenceSplitSet GetActivePersonalBestSegmentSet()
    {
        ReferenceSplitSet set = GetActivePersonalSet(
            PersonalBestSegmentSets,
            ActivePersonalBestSegmentSet,
            "Personal",
            PersonalBestSegmentTimes,
            out string activeName);
        ActivePersonalBestSegmentSet = activeName;
        return set;
    }

    public void SyncPersonalBestTimesFromActiveSet()
    {
        PersonalBestTimes = new Dictionary<string, string>(
            GetActivePersonalBestTimeSet().Splits,
            StringComparer.OrdinalIgnoreCase);
    }

    public void SyncPersonalBestSegmentsFromActiveSet()
    {
        PersonalBestSegmentTimes = new Dictionary<string, string>(
            GetActivePersonalBestSegmentSet().Splits,
            StringComparer.OrdinalIgnoreCase);
    }

    public void SyncActivePersonalBestTimeSetFromDictionary()
    {
        GetActivePersonalBestTimeSet().Splits = new Dictionary<string, string>(
            PersonalBestTimes,
            StringComparer.OrdinalIgnoreCase);
    }

    public void SyncActivePersonalBestSegmentSetFromDictionary()
    {
        GetActivePersonalBestSegmentSet().Splits = new Dictionary<string, string>(
            PersonalBestSegmentTimes,
            StringComparer.OrdinalIgnoreCase);
    }

    private static ReferenceSplitSet GetActivePersonalSet(
        List<ReferenceSplitSet> sets,
        string activeName,
        string fallbackName,
        Dictionary<string, string> fallbackValues,
        out string normalizedActiveName)
    {
        ReferenceSplitSet? activeSet = sets.FirstOrDefault(
            set => string.Equals(set.Name, activeName, StringComparison.OrdinalIgnoreCase));
        if (activeSet is not null)
        {
            normalizedActiveName = activeSet.Name;
            return activeSet;
        }

        if (sets.Count == 0)
        {
            sets.Add(new ReferenceSplitSet
            {
                Name = fallbackName,
                Splits = new Dictionary<string, string>(fallbackValues, StringComparer.OrdinalIgnoreCase)
            });
        }

        normalizedActiveName = sets[0].Name;
        return sets[0];
    }

    public static ReferenceSplitSet CreateReferenceSet(
        string name,
        Dictionary<string, string>? values = null,
        IEnumerable<string>? keys = null)
    {
        var set = new ReferenceSplitSet
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Reference" : name.Trim()
        };

        IEnumerable<string> splitKeys = keys ?? SplitConditionDataRows.Build(SplitCatalog.CreateDefaultRoute()).Select(row => row.Key);
        foreach (string key in splitKeys.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string value = values is not null && values.TryGetValue(key, out string? existingValue)
                ? existingValue
                : string.Empty;
            set.Splits[key] = value;
        }

        return set;
    }
}

internal sealed class GeneralSettings
{
    public bool ShowMouseClickThroughIndicator { get; set; }
    public string Language { get; set; } = "English";
    public bool AlwaysOnTop { get; set; }
    public bool PracticeMode { get; set; } = true;
}

internal sealed class HotkeySettings
{
    public string PauseResumeKey { get; set; } = "F12";
    public string ResetKey { get; set; } = "F6";
    public string MouseClickThroughKey { get; set; } = "F9";
    public string CreateWorldKey { get; set; } = "F7";
    public string PracticeWorldKey { get; set; } = "F8";
}

internal sealed class RouteSettings
{
    public List<SplitRouteEntry> SplitRoute { get; set; } = new();
    public bool ExpandSplitDetails { get; set; }
    public bool CollapseSplitDetailsOnCompletion { get; set; } = true;
    public bool AutoHideAttachedGroups { get; set; } = true;
    public bool AttachedGroupsAffectTimerComparison { get; set; } = true;
}

internal sealed class ComparisonSettings
{
    public List<ReferenceSplitSet> ReferenceSplitSets { get; set; } = new();
    public string ActiveReferenceSplitSet { get; set; } = "WR";
    public bool UsePersonalBestAsReferenceTime { get; set; }
    public List<ReferenceSplitSet> PersonalBestTimeSets { get; set; } = new();
    public string ActivePersonalBestTimeSet { get; set; } = "Personal";
    public List<ReferenceSplitSet> PersonalBestSegmentSets { get; set; } = new();
    public string ActivePersonalBestSegmentSet { get; set; } = "Personal";
    public Dictionary<string, string> PersonalBestTimes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> PersonalBestSegmentTimes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool AutoUpdatePersonalBestData { get; set; }
    public bool AskBeforeUpdatingPersonalBestData { get; set; }
}

internal sealed class OverlaySettings
{
    public bool ShowSplitCompletionAnimation { get; set; } = true;
    public float SplitCompletionAnimationDurationSeconds { get; set; } = 4.2f;
    public int SplitCompletionOutlineThicknessPercent { get; set; } = 30;
    public Dictionary<string, bool> SplitCompletionSplitComparisons { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, bool> SplitCompletionSegmentComparisons { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> SplitCompletionOutlineSplitStyles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> SplitCompletionOutlineSegmentStyles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool ShowCurrentSplitHighlight { get; set; } = true;
    public int CurrentSplitHighlightScalePercent { get; set; } = 112;
    public int CurrentSplitDepthStrengthPercent { get; set; } = 45;
    public bool ShowEarlyDeltaTime { get; set; } = true;
    public int EarlyDeltaTimeSeconds { get; set; } = 60;
    public bool EnableDynamicDeltaTimeUnits { get; set; } = true;
    public bool EnableDeltaGradientColor { get; set; } = true;
    public bool EnableCurrentDeltaGradientColor { get; set; } = true;
    public bool EnableTimerGradientColor { get; set; } = true;
    public int DeltaGradientThresholdSeconds { get; set; } = 120;
    public string DeltaGradientCurve { get; set; } = DeltaGradientCurves.SoftStep;
    public bool ShowSegmentBestDeltaHighlight { get; set; } = true;
    public Dictionary<string, string> SegmentBestDeltaHighlightStyles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public UiColorSettings Colors { get; set; } = new();
    public UiSoundSettings Sounds { get; set; } = new();
    public UiColumnLayoutSettings Columns { get; set; } = new();
    public UiTextEffectSettings TextEffects { get; set; } = new();
    public bool EnableDefeatedBossIconLighting { get; set; } = true;
    public int UndefeatedIconGrayscalePercent { get; set; } = 80;
    public int UndefeatedIconBrightnessPercent { get; set; } = 40;
    public int CurrentBossIconGrayscaleWeakenPercent { get; set; } = 40;
    public int CurrentBossIconBrightnessBoostPercent { get; set; } = 35;
}

internal sealed class AutomationSettings
{
    public AutoCreateWorldSettings AutoCreate { get; set; } = new();
}

internal sealed class AdvancedSettings
{
    public bool EnableTerrariaUiScalePatch { get; set; }
    public int ReadyWatcherPollHz { get; set; }
    public int ReadyUiControlHz { get; set; }
    public int RunningStatusPaintHz { get; set; }
    public int TimerOverlayRefreshHz { get; set; }
}

internal sealed class AutoCreateWorldSettings
{
    public string PlayerName { get; set; } = string.Empty;
    public string PlayerTemplateCode { get; set; } = string.Empty;
    public string PlayerDifficulty { get; set; } = AutoCreatePlayerDifficulty.Softcore;
    public string WorldSize { get; set; } = AutoCreateWorldSize.Small;
    public string WorldDifficulty { get; set; } = AutoCreateWorldDifficulty.Classic;
    public string WorldEvil { get; set; } = AutoCreateWorldEvil.Crimson;
    public string SpecialSeeds { get; set; } = string.Empty;
    public string SecretSeeds { get; set; } = string.Empty;
    public bool EnableZenithStarCatch { get; set; }
    public string ZenithStarCatchStopStage { get; set; } = AutoCreateZenithStarCatchStage.Default;
    public int ZenithStarCatchSpeedSliderValue { get; set; } = AutoCreateZenithStarCatchSpeed.DefaultSliderValue;
    public bool EnablePyramidFilter { get; set; }
    public int PyramidFilterItemMask { get; set; } = AutoCreatePyramidFilterItem.SandstormInABottleMask | AutoCreatePyramidFilterItem.FlyingCarpetMask;
    public bool ReturnToMainMenuOnFilterFailure { get; set; }
    public bool EnableWorldPool { get; set; }
    public int WorldPoolTargetCount { get; set; } = 10;
    public int ShortActionDelayMilliseconds { get; set; }
    public int MenuActionDelayMilliseconds { get; set; }
    public int PyramidFilterPostDelayMilliseconds { get; set; } = 50;
    public int WindowActivationDelayMilliseconds { get; set; }
    public int ClickFocusDelayMilliseconds { get; set; }
    public int InputPressDurationMilliseconds { get; set; }
}

internal sealed class PracticeWorldSettings
{
    public const int SlotCount = 10;

    public List<PracticeWorldSlot> Slots { get; set; } = CreateDefaultSlots();

    public static List<PracticeWorldSlot> CreateDefaultSlots()
    {
        return Enumerable.Range(0, SlotCount)
            .Select(_ => new PracticeWorldSlot())
            .ToList();
    }
}

internal sealed class PracticeWorldSlot
{
    public string Name { get; set; } = string.Empty;
    public string PlayerFilePath { get; set; } = string.Empty;
    public string WorldFilePath { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Name);
}

internal static class AutoCreatePlayerDifficulty
{
    public const string Softcore = "Softcore";
    public const string Mediumcore = "Mediumcore";
    public const string Hardcore = "Hardcore";
    public const string Journey = "Journey";

    public static readonly string[] All = { Softcore, Mediumcore, Hardcore, Journey };

    public static string Normalize(string? value)
    {
        return All.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)) ?? Softcore;
    }
}

internal static class AutoCreateWorldSize
{
    public const string Small = "Small";
    public const string Medium = "Medium";
    public const string Large = "Large";

    public static readonly string[] All = { Small, Medium, Large };

    public static string Normalize(string? value)
    {
        return All.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)) ?? Small;
    }
}

internal static class AutoCreateWorldDifficulty
{
    public const string Journey = "Journey";
    public const string Classic = "Classic";
    public const string Expert = "Expert";
    public const string Master = "Master";
    public const string Normal = "Normal";

    public static readonly string[] All = { Journey, Classic, Expert, Master };

    public static string Normalize(string? value)
    {
        if (string.Equals(value, Normal, StringComparison.OrdinalIgnoreCase))
        {
            return Classic;
        }

        return All.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)) ?? Classic;
    }
}

internal static class AutoCreateWorldEvil
{
    public const string Random = "Random";
    public const string Corruption = "Corruption";
    public const string Crimson = "Crimson";

    public static readonly string[] All = { Random, Corruption, Crimson };

    public static string Normalize(string? value)
    {
        return All.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)) ?? Crimson;
    }
}

internal static class AutoCreateSpecialWorldSeed
{
    public const string NotTheBees = "Not the Bees";
    public const string Drunk = "Drunk";
    public const string Celebration = "Celebration Mk 10";
    public const string TheConstant = "The Constant";
    public const string ForTheWorthy = "For the Worthy";
    public const string NoTraps = "No Traps";
    public const string Remix = "Remix";
    public const string Zenith = "Zenith";
    public const string Skyblock = "Skyblock";

    public static readonly string[] All =
    {
        NotTheBees,
        Drunk,
        Celebration,
        TheConstant,
        ForTheWorthy,
        NoTraps,
        Remix,
        Zenith,
        Skyblock
    };

    private static readonly HashSet<string> ZenithDependencies = new(StringComparer.OrdinalIgnoreCase)
    {
        NotTheBees,
        Drunk,
        Celebration,
        TheConstant,
        ForTheWorthy,
        NoTraps,
        Remix
    };

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "not the bees", NotTheBees },
        { "notthebees", NotTheBees },
        { "drunk", Drunk },
        { "drunk world", Drunk },
        { "drunkworld", Drunk },
        { "5162020", Drunk },
        { "celebration", Celebration },
        { "celebration mk 10", Celebration },
        { "celebrationmk10", Celebration },
        { "5162011", Celebration },
        { "5162021", Celebration },
        { "constant", TheConstant },
        { "the constant", TheConstant },
        { "theconstant", TheConstant },
        { "eye 4 an eye", TheConstant },
        { "eye4aneye", TheConstant },
        { "eye for an eye", TheConstant },
        { "eyeforaneye", TheConstant },
        { "for the worthy", ForTheWorthy },
        { "fortheworthy", ForTheWorthy },
        { "no traps", NoTraps },
        { "notraps", NoTraps },
        { "remix", Remix },
        { "don't dig up", Remix },
        { "dont dig up", Remix },
        { "dontdigup", Remix },
        { "zenith", Zenith },
        { "everything", Zenith },
        { "get fixed boi", Zenith },
        { "getfixedboi", Zenith },
        { "skyblock", Skyblock }
    };

    public static bool TryNormalize(string? value, out string seed)
    {
        seed = string.Empty;
        string normalized = SettingsTokenParser.NormalizeAliasKey(value);
        if (string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, "normal", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Aliases.TryGetValue(normalized, out seed!);
    }

    public static IReadOnlyList<string> ParseList(string? value)
    {
        List<string> seeds = new();
        foreach (string token in SettingsTokenParser.SplitList(value))
        {
            if (TryNormalize(token, out string seed) && !seeds.Contains(seed, StringComparer.OrdinalIgnoreCase))
            {
                seeds.Add(seed);
            }
        }

        if (seeds.Contains(Zenith, StringComparer.OrdinalIgnoreCase))
        {
            seeds.RemoveAll(seed =>
                !string.Equals(seed, Zenith, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(seed, Skyblock, StringComparison.OrdinalIgnoreCase));
        }

        return seeds;
    }

    public static int MenuIndex(string seed)
    {
        return Normalize(seed) switch
        {
            NotTheBees => 1,
            Drunk => 2,
            Celebration => 3,
            TheConstant => 4,
            ForTheWorthy => 5,
            NoTraps => 6,
            Remix => 7,
            Zenith => 8,
            Skyblock => 9,
            _ => throw new ArgumentOutOfRangeException(nameof(seed), seed, "Unknown special world seed.")
        };
    }

    public static bool IsZenithDependency(string seed)
    {
        return ZenithDependencies.Contains(seed);
    }

    private static string Normalize(string value)
    {
        return All.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)) ?? value;
    }
}

internal static class AutoCreatePyramidFilterItem
{
    public const string SandstormInABottle = "Sandstorm in a Bottle";
    public const string FlyingCarpet = "Flying Carpet";
    public const string PharaohSet = "Pharaoh set";
    public const int SandstormInABottleMask = 1;
    public const int FlyingCarpetMask = 2;
    public const int PharaohSetMask = 4;
    public const int AllMask = SandstormInABottleMask | FlyingCarpetMask | PharaohSetMask;

    public static readonly string[] All =
    {
        SandstormInABottle,
        FlyingCarpet,
        PharaohSet
    };

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "sandstorm in a bottle", SandstormInABottle },
        { "sandstorminabottle", SandstormInABottle },
        { "sandstorm bottle", SandstormInABottle },
        { "sandstormbottle", SandstormInABottle },
        { "sandstorm", SandstormInABottle },
        { "\u6C99\u66B4\u74F6", SandstormInABottle },
        { "flying carpet", FlyingCarpet },
        { "flyingcarpet", FlyingCarpet },
        { "carpet", FlyingCarpet },
        { "\u98DE\u6BEF", FlyingCarpet },
        { "pharaoh set", PharaohSet },
        { "pharaohset", PharaohSet },
        { "pharaohs set", PharaohSet },
        { "pharaohsset", PharaohSet },
        { "pharaoh", PharaohSet },
        { "\u6CD5\u8001\u5957", PharaohSet },
        { "\u6CD5\u8001\u5957\u88C5", PharaohSet }
    };

    public static bool TryNormalize(string? value, out string item)
    {
        item = string.Empty;
        string normalized = SettingsTokenParser.NormalizeAliasKey(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return Aliases.TryGetValue(normalized, out item!);
    }

    public static IReadOnlyList<string> ParseList(string? value)
    {
        List<string> items = new();
        foreach (string token in SettingsTokenParser.SplitList(value))
        {
            if (TryNormalize(token, out string item) && !items.Contains(item, StringComparer.OrdinalIgnoreCase))
            {
                items.Add(item);
            }
        }

        return items;
    }

    public static int NormalizeMask(int mask)
    {
        return mask & AllMask;
    }

    public static int NormalizeMaskOrAll(int mask)
    {
        int normalized = NormalizeMask(mask);
        return normalized == 0 ? AllMask : normalized;
    }

    public static int Mask(string item)
    {
        return item switch
        {
            SandstormInABottle => SandstormInABottleMask,
            FlyingCarpet => FlyingCarpetMask,
            PharaohSet => PharaohSetMask,
            _ => 0
        };
    }

    public static IReadOnlyList<string> FromMask(int mask)
    {
        int normalized = NormalizeMask(mask);
        return All
            .Where(item => (normalized & Mask(item)) != 0)
            .ToList();
    }

    public static int ToMask(IEnumerable<string> items)
    {
        int mask = 0;
        foreach (string item in items)
        {
            mask |= Mask(item);
        }

        return NormalizeMask(mask);
    }
}

internal static class AutoCreateSeedList
{
    public static IReadOnlyList<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return SettingsTokenParser.SplitList(value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
