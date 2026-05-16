using System.Windows.Forms;
using System.Text.Json.Serialization;

namespace TerrariaSplit;

internal sealed class AppSettings
{
    public string PauseResumeKey { get; set; } = Keys.F12.ToString();
    public string ResetKey { get; set; } = Keys.F6.ToString();
    public string MouseClickThroughKey { get; set; } = Keys.F9.ToString();
    public string CreateWorldKey { get; set; } = Keys.F7.ToString();
    public bool ShowMouseClickThroughIndicator { get; set; }
    public string Language { get; set; } = "English";
    public bool AlwaysOnTop { get; set; }
    public bool PracticeMode { get; set; } = true;
    public List<BossRouteEntry> Route { get; set; } = new();
    public Dictionary<string, string> BossIconPaths { get; set; } = new();
    public List<ReferenceSplitSet> ReferenceSplitSets { get; set; } = new();
    public string ActiveReferenceSplitSet { get; set; } = "WR";
    public List<ReferenceSplitSet> PersonalBestTimeSets { get; set; } = new();
    public string ActivePersonalBestTimeSet { get; set; } = "Personal";
    public List<ReferenceSplitSet> PersonalBestSegmentSets { get; set; } = new();
    public string ActivePersonalBestSegmentSet { get; set; } = "Personal";
    public Dictionary<string, string> PersonalBestTimes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> PersonalBestSegmentTimes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool AutoUpdatePersonalBestData { get; set; }
    public bool AskBeforeUpdatingPersonalBestData { get; set; }
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
    public bool EnableTimerGradientColor { get; set; } = true;
    public int DeltaGradientThresholdSeconds { get; set; } = 120;
    public string DeltaGradientCurve { get; set; } = DeltaGradientCurves.SoftStep;
    public bool ShowSegmentBestDeltaHighlight { get; set; } = true;
    public Dictionary<string, string> SegmentBestDeltaHighlightStyles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public UiColorSettings Colors { get; set; } = new();
    public UiSoundSettings Sounds { get; set; } = new();
    public UiColumnLayoutSettings Columns { get; set; } = new();
    public AutoCreateWorldSettings AutoCreate { get; set; } = new();
    public AdvancedSettings Advanced { get; set; } = new();
    public bool EnableDefeatedBossIconLighting { get; set; } = true;
    public int UndefeatedIconGrayscalePercent { get; set; } = 80;
    public int UndefeatedIconBrightnessPercent { get; set; } = 40;
    public int CurrentBossIconGrayscaleWeakenPercent { get; set; } = 40;
    public int CurrentBossIconBrightnessBoostPercent { get; set; } = 35;

    [JsonIgnore]
    public Keys PauseResumeKeys => ParseKey(PauseResumeKey, Keys.F12);

    [JsonIgnore]
    public Keys ResetKeys => ParseKey(ResetKey, Keys.F6);

    [JsonIgnore]
    public Keys MouseClickThroughKeys => ParseKey(MouseClickThroughKey, Keys.F9);

    [JsonIgnore]
    public Keys CreateWorldKeys => ParseKey(CreateWorldKey, Keys.F7);

    public bool TryGetReferenceSplit(BossSplitDefinition definition, out TimeSpan split)
    {
        split = TimeSpan.Zero;
        bool anyFound = false;
        TimeSpan maxSplit = TimeSpan.Zero;
        var splits = GetActiveReferenceSet().Splits;

        foreach (string bossId in definition.BossIds)
        {
            if (splits.TryGetValue(bossId, out string? value) && TimeText.TryParse(value, out TimeSpan s))
            {
                if (!anyFound || s > maxSplit)
                {
                    maxSplit = s;
                }
                anyFound = true;
            }
        }

        if (anyFound)
        {
            split = maxSplit;
            return true;
        }

        return false;
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

    public string GetBossIconPath(string name)
    {
        return BossIconPaths.TryGetValue(name, out string? value) ? value : string.Empty;
    }

    public void SetBossIconPath(string name, string value)
    {
        BossIconPaths[name] = value;
    }

    public void SetReferenceText(string name, string value)
    {
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

    private static Keys ParseKey(string? value, Keys fallback)
    {
        if (Enum.TryParse(value, ignoreCase: true, out Keys key) &&
            HotkeyKeyValidator.IsAllowed(key))
        {
            return key;
        }

        return fallback;
    }

    public ReferenceSplitSet GetActiveReferenceSet()
    {
        ReferenceSplitSet? activeSet = ReferenceSplitSets.FirstOrDefault(
            set => string.Equals(set.Name, ActiveReferenceSplitSet, StringComparison.OrdinalIgnoreCase));
        if (activeSet is not null)
        {
            return activeSet;
        }

        if (ReferenceSplitSets.Count == 0)
        {
            ReferenceSplitSets.Add(CreateReferenceSet("WR"));
        }

        ActiveReferenceSplitSet = ReferenceSplitSets[0].Name;
        return ReferenceSplitSets[0];
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

    public static ReferenceSplitSet CreateReferenceSet(string name, Dictionary<string, string>? values = null)
    {
        var set = new ReferenceSplitSet
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Reference" : name.Trim()
        };

        foreach (BossUnitDefinition unit in BossSplitDefinitions.Units)
        {
            string key = unit.Id;
            string value = values is not null && values.TryGetValue(key, out string? existingValue)
                ? existingValue
                : string.Empty;
            set.Splits[key] = value;
        }

        return set;
    }
}

internal sealed class AdvancedSettings
{
    public bool EnableTerrariaUiScalePatch { get; set; }
}

internal sealed class AutoCreateWorldSettings
{
    public const int DefaultShortActionDelayMilliseconds = 70;
    public const int DefaultMenuActionDelayMilliseconds = 160;
    public const int DefaultWindowActivationDelayMilliseconds = 100;
    public const int DefaultClickFocusDelayMilliseconds = 60;
    public const int DefaultInputPressDurationMilliseconds = 150;

    public string PlayerName { get; set; } = string.Empty;
    public string PlayerTemplateCode { get; set; } = string.Empty;
    public string PlayerDifficulty { get; set; } = AutoCreatePlayerDifficulty.Softcore;
    public string WorldSize { get; set; } = AutoCreateWorldSize.Medium;
    public string WorldDifficulty { get; set; } = AutoCreateWorldDifficulty.Classic;
    public string WorldEvil { get; set; } = AutoCreateWorldEvil.Random;
    public int ShortActionDelayMilliseconds { get; set; } = DefaultShortActionDelayMilliseconds;
    public int MenuActionDelayMilliseconds { get; set; } = DefaultMenuActionDelayMilliseconds;
    public int WindowActivationDelayMilliseconds { get; set; } = DefaultWindowActivationDelayMilliseconds;
    public int ClickFocusDelayMilliseconds { get; set; } = DefaultClickFocusDelayMilliseconds;
    public int InputPressDurationMilliseconds { get; set; } = DefaultInputPressDurationMilliseconds;
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
        return All.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)) ?? Medium;
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
        return All.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)) ?? Random;
    }
}
