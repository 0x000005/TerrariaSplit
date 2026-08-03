using System.Text.Json.Serialization;

namespace TerrariaSplit.Configuration;

public sealed class AppSettings
{
    public AppSettings()
    {
        AppSettings defaults = AppSettingsDefaults.Create();
        General = defaults.General;
        Hotkeys = defaults.Hotkeys;
        Route = defaults.Route;
        Comparison = defaults.Comparison;
        Overlay = defaults.Overlay;
        Automation = defaults.Automation;
        Race = defaults.Race;
        PracticeWorlds = defaults.PracticeWorlds;
        Advanced = defaults.Advanced;
    }

    [JsonConstructor]
    public AppSettings(
        GeneralSettings? general,
        HotkeySettings? hotkeys,
        RouteSettings? route,
        ComparisonSettings? comparison,
        OverlaySettings? overlay,
        AutomationSettings? automation,
        RaceSettings? race,
        PracticeWorldSettings? practiceWorlds,
        AdvancedSettings? advanced)
    {
        General = general!;
        Hotkeys = hotkeys!;
        Route = route!;
        Comparison = comparison!;
        Overlay = overlay!;
        Automation = automation!;
        Race = race!;
        PracticeWorlds = practiceWorlds!;
        Advanced = advanced!;
    }

    public GeneralSettings General { get; set; }
    public HotkeySettings Hotkeys { get; set; }
    public RouteSettings Route { get; set; }
    public ComparisonSettings Comparison { get; set; }
    public OverlaySettings Overlay { get; set; }
    public AutomationSettings Automation { get; set; }
    public RaceSettings Race { get; set; }
    public PracticeWorldSettings PracticeWorlds { get; set; }
    public AdvancedSettings Advanced { get; set; }
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
    public string PauseResumeKey { get; set; } = "F8";
    public string ResetKey { get; set; } = "F6";
    public string MouseClickThroughKey { get; set; } = "F9";
    public string CreateWorldKey { get; set; } = "None";
    public string PracticeWorldKey { get; set; } = "None";
    public string ManualSplitKey { get; set; } = "None";
}

public sealed class RouteSettings
{
    public List<SplitRouteEntry> SplitRoute { get; set; } = new();
    public bool ExpandSplitDetails { get; set; } = true;
    public bool CollapseSplitDetailsOnCompletion { get; set; } = true;
    public bool EnableVisibleGroupCountLimit { get; set; }
    public int VisibleGroupCountLimit { get; set; } = 5;
    public int CurrentGroupPosition { get; set; } = 3;
    public bool ShowFinalGroup { get; set; }
    public bool ShowAllVisibleGroupsAfterFinalGroup { get; set; } = true;
    public bool ShowAllAttachedGroupsAfterFinalGroup { get; set; } = true;
    public bool ShowAllMultiConditionMainGroupsAfterFinalGroup { get; set; } = true;
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
    public int? WindowPositionX { get; set; }
    public int? WindowPositionY { get; set; }
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
    public int EarlyDeltaTimeSeconds { get; set; } = 120;
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

public sealed class RaceSettings
{
    public string ServerUrl { get; set; } = "http://127.0.0.1:5000";

    public string Nickname { get; set; } = string.Empty;

    public string LastRoomCode { get; set; } = string.Empty;

    public string PreferredRole { get; set; } = RacePreferredRole.Host;

    public string PreferredWorldSource { get; set; } = RacePreferredWorldSource.Random;

    public string PlayerTemplateCode { get; set; } = string.Empty;

    public RaceWorldSetupSettings WorldSetup { get; set; } = new();

    public RaceLeaderboardSettings Leaderboard { get; set; } = new();

    public RaceVoiceSettings Voice { get; set; } = new();
}

public sealed class RaceWorldSetupSettings
{
    public string Source { get; set; } = RacePreferredWorldSource.Random;

    public string SeedText { get; set; } = string.Empty;

    public string WorldSize { get; set; } = AutoCreateWorldSize.Medium;

    public string WorldDifficulty { get; set; } = AutoCreateWorldDifficulty.Classic;

    public string WorldEvil { get; set; } = AutoCreateWorldEvil.Crimson;

    public string SpecialSeeds { get; set; } = string.Empty;

    public string SecretSeeds { get; set; } = string.Empty;

    public bool RngControlEnabled { get; set; } = true;

    public bool BossFailurePenaltyEnabled { get; set; } = true;

    public bool CheatsEnabled { get; set; } = true;

    public bool PyramidEnabled { get; set; } = true;

    public int PyramidItemMask { get; set; } =
        AutoCreatePyramidFilterItem.SandstormInABottleMask |
        AutoCreatePyramidFilterItem.FlyingCarpetMask;

    public bool CrimsonEnabled { get; set; } = true;

    public string CrimsonDistance { get; set; } = AutoCreateCrimsonDistance.Default;

    public string JungleRouteDepth { get; set; } = AutoCreateJungleRouteDepth.Medium;

    public int ResourceItemMask { get; set; }

    public int LifeCrystalMinimum { get; set; }

    public int SpelunkerPotionMinimum { get; set; }

    public int FeatherfallPotionMinimum { get; set; }
}

public sealed class RaceVoiceSettings
{
    public bool Enabled { get; set; }

    public string VoiceName { get; set; } = string.Empty;

    public int SpeedPercent { get; set; } = 100;

    public int Volume { get; set; } = 100;
}

public static class RacePreferredRole
{
    public const string Host = "Host";
    public const string Member = "Member";

    public static string Normalize(string? value)
    {
        return string.Equals(value, Member, StringComparison.OrdinalIgnoreCase)
            ? Member
            : Host;
    }
}

public static class RacePreferredWorldSource
{
    public const string Random = "Random";
    public const string CustomSeed = "CustomSeed";

    public static string Normalize(string? value)
    {
        return string.Equals(value, CustomSeed, StringComparison.OrdinalIgnoreCase)
            ? CustomSeed
            : Random;
    }
}

public sealed class RaceLeaderboardSettings
{
    public bool UseRankColorForMainTimer { get; set; }

    public int? WindowPositionX { get; set; }

    public int? WindowPositionY { get; set; }

    public int RankPlayerGap { get; set; }

    public int PlayerIconGap { get; set; }

    public int IconTimeGap { get; set; }

    public string RankAlignment { get; set; } = UiColumnAlignment.Right;

    public string PlayerAlignment { get; set; } = UiColumnAlignment.Right;

    public string IconAlignment { get; set; } = UiColumnAlignment.Right;

    public string TimeAlignment { get; set; } = UiColumnAlignment.Right;

    public UiColumnSettings Rank { get; set; } = new()
    {
        Show = true,
        Width = 78,
        FontSize = 13f,
        Bold = true
    };

    public UiColumnSettings Player { get; set; } = new()
    {
        Show = true,
        Width = 180,
        FontSize = 13f,
        Bold = true
    };

    public UiColumnSettings Icon { get; set; } = new()
    {
        Show = true,
        Width = 76,
        FontSize = 32f,
        Bold = false
    };

    public UiColumnSettings Time { get; set; } = new()
    {
        Show = true,
        Width = 138,
        FontSize = 14f,
        Bold = true
    };

    public RaceLeaderboardTextEffectSettings TextEffects { get; set; } = new();

    public RaceLeaderboardColorSettings Colors { get; set; } = new();
}

public sealed class RaceLeaderboardTextEffectSettings
{
    public RaceLeaderboardColumnEffectSettings Rank { get; set; } = new();

    public RaceLeaderboardColumnEffectSettings Player { get; set; } = new();

    public RaceLeaderboardColumnEffectSettings Icon { get; set; } = new()
    {
        ShadowPercent = 20,
        OutlineThicknessPercent = 0
    };

    public RaceLeaderboardColumnEffectSettings Time { get; set; } = new();
}

public sealed class RaceLeaderboardColumnEffectSettings
{
    public int OpacityPercent { get; set; } = 100;

    public int ShadowPercent { get; set; } = 40;

    public int OutlineThicknessPercent { get; set; } = 30;
}

public sealed class RaceLeaderboardColorSettings
{
    public RaceLeaderboardRankGradientColorSettings RankGradient { get; set; } = new();

    public RaceLeaderboardColumnColorSettings Rank { get; set; } = new()
    {
        Text = "#F0A040"
    };

    public RaceLeaderboardColumnColorSettings Player { get; set; } = new()
    {
        Text = "#FFFFFF"
    };

    public RaceLeaderboardColumnColorSettings PlayerSelf { get; set; } = new()
    {
        Text = "#FFFFFF"
    };

    public RaceLeaderboardColumnColorSettings PlayerOther { get; set; } = new()
    {
        Text = "#FFFFFF"
    };

    public RaceLeaderboardColumnColorSettings Icon { get; set; } = new()
    {
        Text = "#FFFFFF"
    };

    public RaceLeaderboardColumnColorSettings Time { get; set; } = new()
    {
        Text = "#F0A040"
    };
}

public sealed class RaceLeaderboardRankGradientColorSettings
{
    public string Start { get; set; } = "#FFD35A";

    public string Middle { get; set; } = "#FFFFFF";

    public string End { get; set; } = "#FF3030";
}

public sealed class RaceLeaderboardColumnColorSettings
{
    public string Text { get; set; } = "#FFFFFF";

    public string Outline { get; set; } = "#101010";

    public string Shadow { get; set; } = "#000000";
}

public sealed class AdvancedSettings
{
    public bool EnableManualSplit { get; set; }
    public bool EnableTerrariaUiScalePatch { get; set; }
    public bool EnableRtssOverlay { get; set; }
    public string RtssExecutablePath { get; set; } = string.Empty;
    public int RtssOverlayX { get; set; } = 10;
    public int RtssOverlayY { get; set; } = 10;
    public int RtssOverlayZoom { get; set; } = 1;
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
    public bool PreserveExistingSaves { get; set; }
    public string WorldSize { get; set; } = AutoCreateWorldSize.Small;
    public string WorldDifficulty { get; set; } = AutoCreateWorldDifficulty.Classic;
    public string WorldEvil { get; set; } = AutoCreateWorldEvil.Crimson;
    public string SpecialSeeds { get; set; } = string.Empty;
    public string SecretSeeds { get; set; } = string.Empty;
    public bool EnableZenithStarCatch { get; set; }
    public string ZenithStarCatchStopStage { get; set; } = AutoCreateZenithStarCatchStage.Default;
    public int ZenithStarCatchSpeedSliderValue { get; set; } = AutoCreateZenithStarCatchSpeed.DefaultSliderValue;
    public bool EnableCheats { get; set; } = true;
    public bool EnablePyramidFilter { get; set; } = true;
    public int PyramidFilterItemMask { get; set; } = AutoCreatePyramidFilterItem.SandstormInABottleMask | AutoCreatePyramidFilterItem.FlyingCarpetMask;
    public bool RequireCrimsonBetweenDungeonAndSpawn { get; set; } = true;
    public string CrimsonDistance { get; set; } = AutoCreateCrimsonDistance.Default;
    public string JungleRouteDepth { get; set; } = AutoCreateJungleRouteDepth.Medium;
    public int ResourceFilterItemMask { get; set; }
    public int ResourceFilterLifeCrystalMinimum { get; set; }
    public int ResourceFilterSpelunkerPotionMinimum { get; set; }
    public int ResourceFilterFeatherfallPotionMinimum { get; set; }
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

public static class AutoCreateCrimsonDistance
{
    public const string Near = "Near";
    public const string Medium = "Medium";
    public const string Far = "Far";
    public const string Default = Far;

    public static readonly string[] All = { Near, Medium, Far };

    public static string Normalize(string? value)
    {
        return All.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)) ?? Default;
    }

    public static bool Includes(string selectedDistance, string distance)
    {
        int selectedIndex = Array.IndexOf(All, Normalize(selectedDistance));
        int distanceIndex = Array.IndexOf(All, Normalize(distance));
        return distanceIndex <= selectedIndex;
    }

    public static int MaximumDistanceTiles(int worldWidth, string? distance)
    {
        int halfWorldWidth = Math.Max(0, worldWidth / 2);
        return Normalize(distance) switch
        {
            Near => halfWorldWidth / 4,
            Medium => halfWorldWidth * 9 / 20,
            _ => halfWorldWidth
        };
    }
}

public static class AutoCreateJungleRouteDepth
{
    public const string None = "0";
    public const string Medium = "Medium";
    public const string Deep = "Deep";
    public const string VeryDeep = "Very deep";

    public static readonly string[] All = [Medium, Deep, VeryDeep];

    public static string Normalize(string? value) =>
        All.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)) ?? None;

    public static bool Includes(string selectedDepth, string candidate)
    {
        int selectedIndex = Array.IndexOf(All, Normalize(selectedDepth));
        int candidateIndex = Array.IndexOf(All, Normalize(candidate));
        return selectedIndex >= 0 && candidateIndex >= selectedIndex;
    }

    public static int MinimumY(string? depth) => Normalize(depth) switch
    {
        Medium => 550,
        Deep => 650,
        VeryDeep => 750,
        _ => 0
    };
}

public static class AutoCreateResourceFilterItem
{
    public const string Boomstick = "Boomstick";
    public const string FeralClaws = "Feral Claws";
    public const string AnkletOfTheWind = "Anklet of the Wind";
    public const int BoomstickMask = 1;
    public const int FeralClawsMask = 2;
    public const int AnkletOfTheWindMask = 8;
    public const int AllMask = BoomstickMask | FeralClawsMask | AnkletOfTheWindMask;

    public static readonly string[] All = [Boomstick, FeralClaws, AnkletOfTheWind];

    public static int NormalizeMask(int mask) => mask & AllMask;

    public static int Mask(string item) => item switch
    {
        Boomstick => BoomstickMask,
        FeralClaws => FeralClawsMask,
        AnkletOfTheWind => AnkletOfTheWindMask,
        _ => 0
    };

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

public static class AutoCreateResourceFilter
{
    public static bool HasRequirements(AutoCreateWorldSettings settings) =>
        AutoCreateJungleRouteDepth.Normalize(settings.JungleRouteDepth) != AutoCreateJungleRouteDepth.None ||
        AutoCreateResourceFilterItem.NormalizeMask(settings.ResourceFilterItemMask) != 0 ||
        AutoCreateResourceMinimum.NormalizeLifeCrystals(settings.ResourceFilterLifeCrystalMinimum) > 0 ||
        AutoCreateResourceMinimum.NormalizePotions(settings.ResourceFilterSpelunkerPotionMinimum) > 0 ||
        AutoCreateResourceMinimum.NormalizePotions(settings.ResourceFilterFeatherfallPotionMinimum) > 0;
}

public static class AutoCreateResourceMinimum
{
    public static readonly int[] LifeCrystals = [0, 1, 2, 3, 4, 5, 6];
    public static readonly int[] Potions = [0, 1, 2, 3];

    public static int NormalizeLifeCrystals(int value) =>
        value <= 0 ? 0 : Math.Min(value, LifeCrystals[^1]);

    public static int NormalizePotions(int value) => Normalize(value, Potions);

    private static int Normalize(int value, IReadOnlyList<int> values) =>
        values.Contains(value) ? value : 0;
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
