using System.Text.Json.Serialization;

namespace TerrariaSplit.Configuration;

public sealed class AppSettings
{
    [Obsolete("Use ReferenceSplitSetService.PersonalBestReferenceSetName instead.")]
    public const string PersonalBestReferenceSetName = ReferenceSplitSetService.PersonalBestReferenceSetName;

    public GeneralSettings General { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
    public RouteSettings Route { get; set; } = new();
    public ComparisonSettings Comparison { get; set; } = new();
    public OverlaySettings Overlay { get; set; } = new();
    public AutomationSettings Automation { get; set; } = new();
    public PracticeWorldSettings PracticeWorlds { get; set; } = new();
    public AdvancedSettings Advanced { get; set; } = new();

    [Obsolete("Use ReferenceSplitSetService.TryGetReferenceSplit instead.")]
    public bool TryGetReferenceSplit(SplitDefinition definition, out TimeSpan split)
    {
        return ReferenceSplitSetService.TryGetReferenceSplit(this, definition, out split);
    }

    [Obsolete("Use ReferenceSplitSetService.GetReferenceText instead.")]
    public string GetReferenceText(string name)
    {
        return ReferenceSplitSetService.GetReferenceText(this, name);
    }

    [Obsolete("Use PersonalBestSetService.GetPersonalBestTimeText instead.")]
    public string GetPersonalBestTimeText(string name)
    {
        return PersonalBestSetService.GetPersonalBestTimeText(this, name);
    }

    [Obsolete("Use PersonalBestSetService.GetPersonalBestSegmentText instead.")]
    public string GetPersonalBestSegmentText(string name)
    {
        return PersonalBestSetService.GetPersonalBestSegmentText(this, name);
    }

    [Obsolete("Use ReferenceSplitSetService.SetReferenceText instead.")]
    public void SetReferenceText(string name, string value)
    {
        ReferenceSplitSetService.SetReferenceText(this, name, value);
    }

    [Obsolete("Use PersonalBestSetService.SetPersonalBestTimeText instead.")]
    public void SetPersonalBestTimeText(string name, string value)
    {
        PersonalBestSetService.SetPersonalBestTimeText(this, name, value);
    }

    [Obsolete("Use PersonalBestSetService.SetPersonalBestSegmentText instead.")]
    public void SetPersonalBestSegmentText(string name, string value)
    {
        PersonalBestSetService.SetPersonalBestSegmentText(this, name, value);
    }

    [Obsolete("Use ReferenceSplitSetService.GetActiveReferenceSet instead.")]
    public ReferenceSplitSet GetActiveReferenceSet()
    {
        return ReferenceSplitSetService.GetActiveReferenceSet(this);
    }

    [Obsolete("Use ReferenceSplitSetService.CreatePersonalBestReferenceSet instead.")]
    public ReferenceSplitSet CreatePersonalBestReferenceSet()
    {
        return ReferenceSplitSetService.CreatePersonalBestReferenceSet(this);
    }

    [Obsolete("Use PersonalBestSetService.GetActivePersonalBestTimeSet instead.")]
    public ReferenceSplitSet GetActivePersonalBestTimeSet()
    {
        return PersonalBestSetService.GetActivePersonalBestTimeSet(this);
    }

    [Obsolete("Use PersonalBestSetService.GetActivePersonalBestSegmentSet instead.")]
    public ReferenceSplitSet GetActivePersonalBestSegmentSet()
    {
        return PersonalBestSetService.GetActivePersonalBestSegmentSet(this);
    }

    [Obsolete("Use PersonalBestSetService.SyncPersonalBestTimesFromActiveSet instead.")]
    public void SyncPersonalBestTimesFromActiveSet()
    {
        PersonalBestSetService.SyncPersonalBestTimesFromActiveSet(this);
    }

    [Obsolete("Use PersonalBestSetService.SyncPersonalBestSegmentsFromActiveSet instead.")]
    public void SyncPersonalBestSegmentsFromActiveSet()
    {
        PersonalBestSetService.SyncPersonalBestSegmentsFromActiveSet(this);
    }

    [Obsolete("Use PersonalBestSetService.SyncActivePersonalBestTimeSetFromDictionary instead.")]
    public void SyncActivePersonalBestTimeSetFromDictionary()
    {
        PersonalBestSetService.SyncActivePersonalBestTimeSetFromDictionary(this);
    }

    [Obsolete("Use PersonalBestSetService.SyncActivePersonalBestSegmentSetFromDictionary instead.")]
    public void SyncActivePersonalBestSegmentSetFromDictionary()
    {
        PersonalBestSetService.SyncActivePersonalBestSegmentSetFromDictionary(this);
    }

    [Obsolete("Use ReferenceSplitSetService.CreateReferenceSet instead.")]
    public static ReferenceSplitSet CreateReferenceSet(
        string name,
        Dictionary<string, string>? values = null,
        IEnumerable<string>? keys = null)
    {
        return ReferenceSplitSetService.CreateReferenceSet(name, values, keys);
    }
}

public sealed class GeneralSettings
{
    public bool ShowMouseClickThroughIndicator { get; set; }
    public string Language { get; set; } = "English";
    public bool AlwaysOnTop { get; set; }
    public bool PracticeMode { get; set; } = true;
}

public sealed class HotkeySettings
{
    public string PauseResumeKey { get; set; } = "F12";
    public string ResetKey { get; set; } = "F6";
    public string MouseClickThroughKey { get; set; } = "F9";
    public string CreateWorldKey { get; set; } = "F7";
    public string PracticeWorldKey { get; set; } = "F8";
}

public sealed class RouteSettings
{
    public List<SplitRouteEntry> SplitRoute { get; set; } = new();
    public bool ExpandSplitDetails { get; set; }
    public bool CollapseSplitDetailsOnCompletion { get; set; } = true;
    public bool EnableVisibleGroupCountLimit { get; set; }
    public int VisibleGroupCountLimit { get; set; } = 5;
    public int CurrentGroupPosition { get; set; } = 3;
    public bool ShowFinalGroup { get; set; }
    public bool AutoHideAttachedGroups { get; set; } = true;
}

public sealed class ComparisonSettings
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

public sealed class OverlaySettings
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

public sealed class AutomationSettings
{
    public AutoCreateWorldSettings AutoCreate { get; set; } = new();
}

public sealed class AdvancedSettings
{
    public bool EnableTerrariaUiScalePatch { get; set; }
    public int ReadyWatcherPollHz { get; set; }
    public int ReadyUiControlHz { get; set; }
    public int RunningStatusPaintHz { get; set; }
    public int TimerOverlayRefreshHz { get; set; }
}

public sealed class AutoCreateWorldSettings
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

public sealed class PracticeWorldSettings
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

public sealed class PracticeWorldSlot
{
    public string Name { get; set; } = string.Empty;
    public string PlayerFilePath { get; set; } = string.Empty;
    public string WorldFilePath { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Name);
}

public static class AutoCreatePlayerDifficulty
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

public static class AutoCreateWorldSize
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

public static class AutoCreateWorldDifficulty
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

public static class AutoCreateWorldEvil
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

public static class AutoCreateSpecialWorldSeed
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

public static class AutoCreatePyramidFilterItem
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

public static class AutoCreateSeedList
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
