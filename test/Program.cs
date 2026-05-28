using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using TerrariaSplit;
using TerrariaSplit.Tests;

var legacyTests = new (string Name, Action Test)[]
{
    ("SignaturePattern matches wildcard bytes", TestSignaturePatternWildcard),
    ("SplitTimerFormatter formats minute and hour values", TestSplitTimerFormatter),
    ("Rolling performance counter keeps a bounded window", TestRollingPerformanceCounter),
    ("Runtime performance tracker separates paint ticks from completed paints", TestRuntimePerformancePaintDiagnostics),
    ("SplitTimer clamps practice time at zero", TestSplitTimerPracticeClamp),
    ("BossRouteGroups groups enabled entries by segment", TestBossRouteGroups),
    ("TerrariaMenuGeometry maps 900p menu coordinates", TestTerrariaMenuGeometry),
    ("Localizer returns English fallback and Chinese Crimson", TestLocalizer),
    ("JsonFileStore writes settings atomically", TestJsonFileStoreWritesAtomically),
    ("Default settings template covers serializable settings", TestDefaultSettingsTemplateCoversSerializableSettings),
    ("SettingsNormalizer clamps auto-create timings", TestSettingsNormalize),
    ("SettingsNormalizer normalizes timer overlay refresh settings", TestSettingsNormalizeTimerOverlayRefresh),
    ("SettingsNormalizer normalizes practice world slots", TestSettingsNormalizePracticeWorlds),
    ("SettingsNormalizer clamps text effects", TestSettingsNormalizeTextEffects),
    ("Hotkey validator rejects reserved keys", TestHotkeyValidatorRejectsReservedKeys),
    ("Hotkey validator accepts modifier chords", TestHotkeyValidatorAcceptsModifierChords),
    ("AppSettings falls back from invalid hotkeys", TestAppSettingsInvalidHotkeyFallback),
    ("AppSettings parses modifier hotkeys", TestAppSettingsParsesModifierHotkeys),
    ("AppSettings uses PB as reference time", TestAppSettingsUsesPersonalBestAsReferenceTime),
    ("AppSettingsStore preserves active external split set names", TestAppSettingsStorePreservesActiveExternalSplitSetNames),
    ("Input model no longer exposes runtime hotkey requests", TestInputModelStaticRegression),
    ("Settings form orders moved pages", TestSettingsFormOrdersMovedPages),
    ("Settings form applies global scale from General page", TestSettingsFormAppliesGlobalScaleFromGeneralPage),
    ("Settings form applies dynamic delta units from UI page", TestSettingsFormAppliesDynamicDeltaUnitsFromUiPage),
    ("Settings form applies text effects from UI page", TestSettingsFormAppliesTextEffectsFromUiPage),
    ("Settings form applies practice world slots", TestSettingsFormAppliesPracticeWorldSlots),
    ("Settings hotkey box captures modifier chords", TestSettingsHotkeyBoxCapturesModifierChords),
    ("Settings form collapses zenith special seed dependencies", TestSettingsFormCollapsesZenithSpecialSeedDependencies),
    ("Settings form saves The Constant special seed", TestSettingsFormSavesTheConstantSpecialSeed),
    ("Settings form applies Zenith star catch options", TestSettingsFormAppliesZenithStarCatchOptions),
    ("Settings form gates Zenith star catch behind Zenith seed", TestSettingsFormGatesZenithStarCatchBehindZenithSeed),
    ("Settings form applies timer start sound", TestSettingsFormAppliesTimerStartSound),
    ("Settings form applies resume sound", TestSettingsFormAppliesResumeSound),
    ("Settings form applies Moon Lord split sound", TestSettingsFormAppliesMoonLordSplitSound),
    ("Settings form locks reference controls when PB reference is enabled", TestSettingsFormLocksReferenceControlsForPersonalBestReference),
    ("Settings form applies text outline and shadow colors", TestSettingsFormAppliesTextOutlineAndShadowColors),
    ("Main form preserves size when applying non-layout settings", TestMainFormPreservesSizeWhenApplyingNonLayoutSettings),
    ("Main form settings apply finalizes current run before reloading definitions", TestMainFormSettingsApplyFinalizesCurrentRunBeforeReload),
    ("Main form initializes overlay layout with current split count", TestMainFormInitializesOverlayLayoutWithCurrentSplitCount),
    ("Main form overlay client size matches status layout", TestMainFormOverlayClientSizeMatchesStatusLayout),
    ("Main form scales size when global scale changes", TestMainFormScalesSizeWhenGlobalScaleChanges),
    ("Main form adjusts width when split columns change", TestMainFormAdjustsWidthWhenSplitColumnsChange),
    ("Settings form applies current delta gradient option", TestSettingsFormAppliesCurrentDeltaGradientOption),
    ("Settings form applies advanced UI scale patch option", TestSettingsFormAppliesAdvancedUiScalePatchOption),
    ("Settings form applies timer overlay refresh settings", TestSettingsFormAppliesTimerOverlayRefreshSettings),
    ("Settings form keeps uncreated animation fields unchanged", TestSettingsFormKeepsUncreatedAnimationFieldsUnchanged),
    ("Terraria UI scale patch rewrites target IL constants", TestTerrariaUiScalePatchPlan),
    ("Zenith star catch stop stages follow world generation order", TestZenithStarCatchStageStopRules),
    ("Zenith star catch speed uses logarithmic stepped range", TestZenithStarCatchSpeedRange),
    ("Pyramid filter scans world file evidence in speedrun corridor", TestPyramidFilterWorldFileScanner),
    ("Overlay composite layout derives status and timer windows from shared bounds", TestOverlayCompositeLayoutCalculator)
};
var tests = legacyTests
    .Concat(HotkeyTests.All())
    .Concat(AutomationRunnerTests.All())
    .Concat(LoadWorldValidationTests.All())
    .Concat(HighPrecisionSchedulerTests.All())
    .Concat(MainShellRefactorTests.All())
    .Concat(RenderingTests.All())
    .Concat(WorldGenerationMemoryTests.All())
    .ToArray();

int failures = 0;
foreach ((string name, Action test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

if (failures > 0)
{
    Environment.ExitCode = 1;
}

static void TestSignaturePatternWildcard()
{
    SignaturePattern pattern = SignaturePattern.Parse("AA ?? CC");
    AssertEqual(1, pattern.FindIn([0x00, 0xAA, 0xBB, 0xCC, 0xDD]));
    AssertEqual(-1, pattern.FindIn([0xAA, 0xBB, 0xCD]));
}

static void TestSplitTimerFormatter()
{
    AssertEqual("01:02.34", SplitTimerFormatter.Format(new TimeSpan(0, 0, 1, 2, 340)));
    AssertEqual("00:00.01", SplitTimerFormatter.Format(TimeSpan.FromMilliseconds(10)));
    AssertEqual("1:02:03.04", SplitTimerFormatter.Format(new TimeSpan(0, 1, 2, 3, 40)));
}

static void TestRollingPerformanceCounter()
{
    var counter = new RollingPerformanceCounter(capacity: 3);
    counter.Record(1);
    counter.Record(2);
    counter.Record(5);
    counter.Record(8);

    AssertEqual(4, counter.TotalCount);
    AssertEqual(3, counter.SampleCount);
    AssertEqual(8d, counter.LastMilliseconds);
    AssertEqual(5d, counter.AverageMilliseconds);
    AssertEqual(8d, counter.MaxMilliseconds);
}

static void TestRuntimePerformancePaintDiagnostics()
{
    var tracker = new RuntimePerformanceTracker();
    long baseTimestamp = Stopwatch.GetTimestamp();
    long tenMilliseconds = (long)Math.Round(Stopwatch.Frequency * 0.01d);

    tracker.RecordStatusPaintTick(new HighPrecisionSchedulerTick(
        baseTimestamp,
        baseTimestamp + 1,
        TimeSpan.FromMilliseconds(10),
        TimeSpan.FromMilliseconds(0.25)));
    tracker.RecordStatusPaintTick(new HighPrecisionSchedulerTick(
        baseTimestamp + tenMilliseconds,
        baseTimestamp + tenMilliseconds + 2,
        TimeSpan.FromMilliseconds(10),
        TimeSpan.FromMilliseconds(0.5)));
    tracker.RecordStatusPaintDispatchSkipped();

    tracker.RecordTimerOverlayPaintTick(new HighPrecisionSchedulerTick(
        baseTimestamp,
        baseTimestamp + 1,
        TimeSpan.FromMilliseconds(10),
        TimeSpan.FromMilliseconds(0.75)));
    tracker.RecordTimerOverlayPaintTick(new HighPrecisionSchedulerTick(
        baseTimestamp + tenMilliseconds,
        baseTimestamp + tenMilliseconds + 2,
        TimeSpan.FromMilliseconds(10),
        TimeSpan.FromMilliseconds(1)));
    tracker.RecordTimerOverlayPaintDispatchSkipped();
    tracker.RecordTimerOverlayPaintInputSkipped();

    RuntimePerformanceDiagnostics snapshot = tracker.Snapshot();
    AssertEqual(2, snapshot.StatusPaintTickCount);
    AssertEqual(2, snapshot.TimerOverlayPaintTickCount);
    AssertEqual(1, snapshot.StatusPaintDispatchSkipCount);
    AssertEqual(1, snapshot.TimerOverlayPaintDispatchSkipCount);
    AssertEqual(1, snapshot.TimerOverlayPaintInputSkipCount);
    Nearly(10d, snapshot.ActualStatusPaintTickIntervalMilliseconds, 0.1d);
    Nearly(10d, snapshot.ActualTimerOverlayPaintTickIntervalMilliseconds, 0.1d);
    Nearly(0.375d, snapshot.AverageStatusPaintTickDelayMilliseconds, 0.001d);
    Nearly(0.875d, snapshot.AverageTimerOverlayPaintTickDelayMilliseconds, 0.001d);
}

static void TestSplitTimerPracticeClamp()
{
    var timer = new SplitTimer();
    timer.SetPracticeElapsed(TimeSpan.FromSeconds(-5));
    AssertEqual(TimeSpan.Zero, timer.Elapsed);
}

static void TestBossRouteGroups()
{
    var settings = new AppSettings
    {
        Route =
        [
            new BossRouteEntry { BossId = "skeletron", Enabled = true, Segment = 1 },
            new BossRouteEntry { BossId = "wallofflesh", Enabled = false, Segment = 1 },
            new BossRouteEntry { BossId = "destroyer", Enabled = true, Segment = 2 },
            new BossRouteEntry { BossId = "twins", Enabled = true, Segment = 2 }
        ]
    };

    List<RouteGroup> groups = BossRouteGroups.Build(settings);
    AssertEqual(2, groups.Count);
    AssertEqual("skeletron", groups[0].Key);
    AssertEqual("destroyer+twins", groups[1].Key);
}

static void TestTerrariaMenuGeometry()
{
    TerrariaMenuGeometry geometry = TerrariaMenuGeometry.From(new Size(900, 900));
    AssertEqual(new Point(450, 245), geometry.MainMenuSinglePlayer());
    AssertEqual(new Point(282, 830), geometry.SelectMenuBackButton());
    AssertEqual(new Point(580, 534), geometry.CreatePlayerButton());
    AssertEqual(new Point(450, 230), geometry.AdvancedSeedTextButton());
    AssertEqual(new Point(342, 287), geometry.AdvancedSpecialSeedButton(AutoCreateSpecialWorldSeed.NotTheBees));
}

static void TestLocalizer()
{
    AssertEqual("Crimson", Localizer.Get("Crimson", new AppSettings { Language = "English" }));
    AssertEqual("\u7329\u7EA2", Localizer.Get("Crimson", new AppSettings { Language = "\u4E2D\u6587" }));
    AssertEqual("\u7D2F\u79EF", Localizer.Get("Cumulative", new AppSettings { Language = "\u4E2D\u6587" }));
    AssertEqual("\u5206\u6BB5", Localizer.Get("Segment", new AppSettings { Language = "\u4E2D\u6587" }));
    AssertEqual("\u4E0D\u900F\u660E\u5EA6 %", Localizer.Get("Opacity %", new AppSettings { Language = "\u4E2D\u6587" }));
    AssertEqual("\u81EA\u52A8\u7B5B\u9009\u91D1\u5B57\u5854", Localizer.Get("Auto filter pyramid", new AppSettings { Language = "\u4E2D\u6587" }));
    AssertEqual("\u7B49\u5F85\u9644\u52A0\u5185\u5B58", Localizer.Get("Waiting for attached memory", new AppSettings { Language = "\u4E2D\u6587" }));
    AssertEqual("\u7B49\u5F85\u8BA1\u65F6\u5F00\u59CB", Localizer.Get("Waiting for timer start", new AppSettings { Language = "\u4E2D\u6587" }));
    AssertEqual("\u5206\u6BB5\u65F6\u95F4", Localizer.Get("Segment time", new AppSettings { Language = "\u4E2D\u6587" }));
    AssertEqual("\u7D2F\u8BA1\u65F6\u95F4", Localizer.Get("Cumulative time", new AppSettings { Language = "\u4E2D\u6587" }));
}

static void TestJsonFileStoreWritesAtomically()
{
    string directory = GetPublishOutputDirectory("test-output", "json-store-tests");
    string settingsPath = Path.Combine(directory, "settings.json");
    string activeProfilePath = Path.Combine(directory, "active-settings.txt");

    try
    {
        var settings = new AppSettings { Language = "\u4E2D\u6587" };
        AssertEqual(true, JsonFileStore.Write(settingsPath, settings, "test settings"));

        AppSettings? loaded = JsonFileStore.Read<AppSettings>(settingsPath, "test settings");
        AssertEqual("\u4E2D\u6587", loaded?.Language);

        AssertEqual(true, JsonFileStore.WriteText(activeProfilePath, "profile.json", "test active settings profile"));
        AssertEqual("profile.json", File.ReadAllText(activeProfilePath));
        AssertEqual(0, Directory.EnumerateFiles(directory, "*.tmp").Count());
    }
    finally
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}

static void TestDefaultSettingsTemplateCoversSerializableSettings()
{
    string path = FindDefaultSettingsTemplate();
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));

    AssertJsonCoversType(typeof(AppSettings), document.RootElement, "settings");
}

static void TestSettingsNormalize()
{
    var settings = new AppSettings
    {
        AutoCreate = new AutoCreateWorldSettings
        {
            ShortActionDelayMilliseconds = -1,
            MenuActionDelayMilliseconds = 6000,
            WindowActivationDelayMilliseconds = 6000,
            ClickFocusDelayMilliseconds = -10,
            InputPressDurationMilliseconds = 0,
            SpecialSeeds = "  for the worthy | get fixed boi | skyblock  ",
            SecretSeeds = "  mole people | waterpark  ",
            EnableZenithStarCatch = true,
            ZenithStarCatchStopStage = "not a real stage",
            ZenithStarCatchSpeedSliderValue = 9999,
            EnablePyramidFilter = true
        }
    };

    SettingsNormalizer.Normalize(settings);
    AssertEqual(0, settings.AutoCreate.ShortActionDelayMilliseconds);
    AssertEqual(5000, settings.AutoCreate.MenuActionDelayMilliseconds);
    AssertEqual(5000, settings.AutoCreate.WindowActivationDelayMilliseconds);
    AssertEqual(0, settings.AutoCreate.ClickFocusDelayMilliseconds);
    AssertEqual(1, settings.AutoCreate.InputPressDurationMilliseconds);
    AssertEqual("Zenith|Skyblock", settings.AutoCreate.SpecialSeeds);
    AssertEqual("mole people | waterpark", settings.AutoCreate.SecretSeeds);
    AssertEqual(true, settings.AutoCreate.EnableZenithStarCatch);
    AssertEqual(AutoCreateZenithStarCatchStage.Pots, settings.AutoCreate.ZenithStarCatchStopStage);
    AssertEqual(AutoCreateZenithStarCatchSpeed.MaximumSliderValue, settings.AutoCreate.ZenithStarCatchSpeedSliderValue);
    AssertEqual(true, settings.AutoCreate.EnablePyramidFilter);

    settings.Advanced = null!;
    SettingsNormalizer.Normalize(settings);
    AssertEqual(false, settings.Advanced.EnableTerrariaUiScalePatch);
}

static void TestSettingsNormalizeTimerOverlayRefresh()
{
    var settings = new AppSettings
    {
        Advanced = new AdvancedSettings
        {
            ReadyWatcherPollHz = 999,
            ReadyUiControlHz = 1,
            RunningStatusPaintHz = 999,
            TimerOverlayRefreshHz = 999
        }
    };

    SettingsNormalizer.Normalize(settings);
    AssertEqual(960, settings.Advanced.ReadyWatcherPollHz);
    AssertEqual(60, settings.Advanced.ReadyUiControlHz);
    AssertEqual(240, settings.Advanced.RunningStatusPaintHz);
    AssertEqual(240, settings.Advanced.TimerOverlayRefreshHz);

    settings.Advanced.ReadyWatcherPollHz = 1;
    settings.Advanced.ReadyUiControlHz = 999;
    settings.Advanced.RunningStatusPaintHz = 1;
    settings.Advanced.TimerOverlayRefreshHz = 1;
    SettingsNormalizer.Normalize(settings);
    AssertEqual(120, settings.Advanced.ReadyWatcherPollHz);
    AssertEqual(240, settings.Advanced.ReadyUiControlHz);
    AssertEqual(60, settings.Advanced.RunningStatusPaintHz);
    AssertEqual(60, settings.Advanced.TimerOverlayRefreshHz);
}

static void TestSettingsNormalizePracticeWorlds()
{
    var settings = new AppSettings
    {
        PracticeWorlds = new PracticeWorldSettings
        {
            Slots =
            [
                new PracticeWorldSlot
                {
                    Name = "  Skeletron  ",
                    PlayerFilePath = "  C:\\practice\\player.plr  ",
                    WorldFilePath = "  C:\\practice\\world.wld  "
                },
                null!
            ]
        }
    };

    SettingsNormalizer.Normalize(settings);

    AssertEqual(PracticeWorldSettings.SlotCount, settings.PracticeWorlds.Slots.Count);
    AssertEqual("Skeletron", settings.PracticeWorlds.Slots[0].Name);
    AssertEqual("C:\\practice\\player.plr", settings.PracticeWorlds.Slots[0].PlayerFilePath);
    AssertEqual("C:\\practice\\world.wld", settings.PracticeWorlds.Slots[0].WorldFilePath);
    AssertEqual(string.Empty, settings.PracticeWorlds.Slots[1].Name);

    settings.PracticeWorlds = null!;
    SettingsNormalizer.Normalize(settings);
    AssertEqual(PracticeWorldSettings.SlotCount, settings.PracticeWorlds.Slots.Count);
}

static void TestSettingsNormalizeTextEffects()
{
    var settings = new AppSettings
    {
        TextEffects = new UiTextEffectSettings
        {
            IconOpacityPercent = -1,
            TimeOpacityPercent = 101,
            TimeShadowPercent = -1,
            TimeOutlineThicknessPercent = 101,
            DeltaOpacityPercent = 900,
            DeltaShadowPercent = -99,
            DeltaOutlineThicknessPercent = 900,
            TimerOpacityPercent = -1,
            TimerShadowPercent = -1,
            TimerOutlineThicknessPercent = 101,
            TimerMillisecondsOpacityPercent = 142,
            TimerMillisecondsShadowPercent = 42,
            TimerMillisecondsOutlineThicknessPercent = 77
        }
    };

    SettingsNormalizer.Normalize(settings);
    AssertEqual(0, settings.TextEffects.IconOpacityPercent);
    AssertEqual(100, settings.TextEffects.TimeOpacityPercent);
    AssertEqual(0, settings.TextEffects.TimeShadowPercent);
    AssertEqual(101, settings.TextEffects.TimeOutlineThicknessPercent);
    AssertEqual(100, settings.TextEffects.DeltaOpacityPercent);
    AssertEqual(0, settings.TextEffects.DeltaShadowPercent);
    AssertEqual(200, settings.TextEffects.DeltaOutlineThicknessPercent);
    AssertEqual(0, settings.TextEffects.TimerOpacityPercent);
    AssertEqual(0, settings.TextEffects.TimerShadowPercent);
    AssertEqual(101, settings.TextEffects.TimerOutlineThicknessPercent);
    AssertEqual(100, settings.TextEffects.TimerMillisecondsOpacityPercent);
    AssertEqual(42, settings.TextEffects.TimerMillisecondsShadowPercent);
    AssertEqual(77, settings.TextEffects.TimerMillisecondsOutlineThicknessPercent);

    settings.TextEffects = null!;
    SettingsNormalizer.Normalize(settings);
    AssertEqual(100, settings.TextEffects.TimeOpacityPercent);
    AssertEqual(0, settings.TextEffects.TimerShadowPercent);
}

static void TestHotkeyValidatorRejectsReservedKeys()
{
    AssertEqual(false, HotkeyKeyValidator.IsAllowed(Keys.ControlKey));
    AssertEqual(false, HotkeyKeyValidator.IsAllowed(Keys.LShiftKey));
    AssertEqual(false, HotkeyKeyValidator.IsAllowed(Keys.RMenu));
    AssertEqual(false, HotkeyKeyValidator.IsAllowed(Keys.LWin));
    AssertEqual(false, HotkeyKeyValidator.IsAllowed(Keys.CapsLock));
    AssertEqual(true, HotkeyKeyValidator.IsAllowed(Keys.A));
    AssertEqual(true, HotkeyKeyValidator.IsAllowed(Keys.F6));
    AssertEqual(false, HotkeyKeyValidator.IsAllowed(Keys.Control | Keys.ControlKey));
}

static void TestHotkeyValidatorAcceptsModifierChords()
{
    AssertEqual(true, HotkeyKeyValidator.IsAllowed(Keys.Control | Keys.F6));
    AssertEqual(true, HotkeyKeyValidator.IsAllowed(Keys.Alt | Keys.Shift | Keys.A));
    AssertEqual(true, HotkeyKeyValidator.TryNormalize(Keys.Control | Keys.Alt | Keys.F10, out Keys normalized));
    AssertEqual(Keys.Control | Keys.Alt | Keys.F10, normalized);
    AssertEqual("Ctrl + Alt + F10", HotkeyKeyValidator.Format(normalized));
}

static void TestAppSettingsInvalidHotkeyFallback()
{
    var settings = new AppSettings
    {
        PauseResumeKey = Keys.ControlKey.ToString(),
        ResetKey = Keys.LShiftKey.ToString(),
        MouseClickThroughKey = Keys.RMenu.ToString(),
        CreateWorldKey = Keys.LWin.ToString(),
        PracticeWorldKey = Keys.LWin.ToString()
    };

    AssertEqual(Keys.F12, settings.PauseResumeKeys);
    AssertEqual(Keys.F6, settings.ResetKeys);
    AssertEqual(Keys.F9, settings.MouseClickThroughKeys);
    AssertEqual(Keys.F7, settings.CreateWorldKeys);
    AssertEqual(Keys.F8, settings.PracticeWorldKeys);
}

static void TestAppSettingsParsesModifierHotkeys()
{
    var settings = new AppSettings
    {
        PauseResumeKey = (Keys.Control | Keys.F12).ToString(),
        ResetKey = (Keys.Alt | Keys.F6).ToString(),
        MouseClickThroughKey = (Keys.Shift | Keys.F9).ToString(),
        CreateWorldKey = (Keys.Control | Keys.Alt | Keys.F7).ToString(),
        PracticeWorldKey = (Keys.Control | Keys.Shift | Keys.F8).ToString()
    };

    AssertEqual(Keys.Control | Keys.F12, settings.PauseResumeKeys);
    AssertEqual(Keys.Alt | Keys.F6, settings.ResetKeys);
    AssertEqual(Keys.Shift | Keys.F9, settings.MouseClickThroughKeys);
    AssertEqual(Keys.Control | Keys.Alt | Keys.F7, settings.CreateWorldKeys);
    AssertEqual(Keys.Control | Keys.Shift | Keys.F8, settings.PracticeWorldKeys);
}

static void TestAppSettingsUsesPersonalBestAsReferenceTime()
{
    var settings = new AppSettings
    {
        UsePersonalBestAsReferenceTime = true,
        ReferenceSplitSets =
        [
            AppSettings.CreateReferenceSet("WR", new Dictionary<string, string>
            {
                ["Skeletron"] = "01:00"
            })
        ],
        PersonalBestTimes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Skeletron"] = "00:30"
        }
    };
    SettingsNormalizer.Normalize(settings);

    var definition = new BossSplitDefinition(
        "Skeletron",
        "Skeletron",
        Array.Empty<BossFlag>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        ["Skeletron"]);

    AssertEqual(AppSettings.PersonalBestReferenceSetName, settings.GetActiveReferenceSet().Name);
    AssertEqual("00:30", settings.GetReferenceText("Skeletron"));
    AssertEqual(true, settings.TryGetReferenceSplit(definition, out TimeSpan split));
    AssertEqual(TimeSpan.FromSeconds(30), split);

    settings.SetReferenceText("Skeletron", "05:00");
    AssertEqual("00:30", settings.GetReferenceText("Skeletron"));
}

static void TestAppSettingsStorePreservesActiveExternalSplitSetNames()
{
    string settingsDirectory = AppSettingsStore.SettingsDirectory;
    string referenceDirectory = SplitTimeSetStore.ReferenceDirectory;
    string personalBestTimeDirectory = SplitTimeSetStore.PersonalBestTimeDirectory;
    string personalBestSegmentDirectory = SplitTimeSetStore.PersonalBestSegmentDirectory;
    DirectorySnapshot settingsSnapshot = SnapshotDirectory(settingsDirectory);
    DirectorySnapshot referenceSnapshot = SnapshotDirectory(referenceDirectory);
    DirectorySnapshot personalBestTimeSnapshot = SnapshotDirectory(personalBestTimeDirectory);
    DirectorySnapshot personalBestSegmentSnapshot = SnapshotDirectory(personalBestSegmentDirectory);

    try
    {
        DeleteDirectoryIfExists(settingsDirectory);
        DeleteDirectoryIfExists(referenceDirectory);
        DeleteDirectoryIfExists(personalBestTimeDirectory);
        DeleteDirectoryIfExists(personalBestSegmentDirectory);

        SplitTimeSetStore.SaveReferenceSets(
        [
            CreateSplitSet("WR", BossSplitDefinitions.Units.Select(unit => unit.Id)),
            CreateSplitSet("Custom Reference", BossSplitDefinitions.Units.Select(unit => unit.Id))
        ]);

        SplitTimeSetStore.SavePersonalBestTimeSets(
        [
            CreateSplitSet("Personal", BossSplitDefinitions.Units.Select(unit => unit.Id)),
            CreateSplitSet("Race PB", BossSplitDefinitions.Units.Select(unit => unit.Id))
        ]);

        var routeSettings = new AppSettings { Route = BossSplitDefinitions.CreateDefaultRoute() };
        IEnumerable<string> segmentKeys = BossRouteGroups.Build(routeSettings).Select(group => group.Key);
        SplitTimeSetStore.SavePersonalBestSegmentSets(
        [
            CreateSplitSet("Personal", segmentKeys),
            CreateSplitSet("Race Segments", segmentKeys)
        ]);

        string profileName = "active-external-sets.json";
        string settingsPath = Path.Combine(settingsDirectory, profileName);
        Directory.CreateDirectory(settingsDirectory);
        SettingsSerializer.WriteSettings(settingsPath, new AppSettings
        {
            ActiveReferenceSplitSet = "Custom Reference",
            ActivePersonalBestTimeSet = "Race PB",
            ActivePersonalBestSegmentSet = "Race Segments"
        });

        AppSettings loaded = AppSettingsStore.Load(profileName);

        AssertEqual("Custom Reference", loaded.ActiveReferenceSplitSet);
        AssertEqual("Race PB", loaded.ActivePersonalBestTimeSet);
        AssertEqual("Race Segments", loaded.ActivePersonalBestSegmentSet);
    }
    finally
    {
        RestoreDirectory(settingsDirectory, settingsSnapshot);
        RestoreDirectory(referenceDirectory, referenceSnapshot);
        RestoreDirectory(personalBestTimeDirectory, personalBestTimeSnapshot);
        RestoreDirectory(personalBestSegmentDirectory, personalBestSegmentSnapshot);
    }
}

static ReferenceSplitSet CreateSplitSet(string name, IEnumerable<string> keys)
{
    var set = new ReferenceSplitSet
    {
        Name = name,
        Splits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    };

    foreach (string key in keys)
    {
        set.Splits[key] = string.Empty;
    }

    return set;
}

static void TestInputModelStaticRegression()
{
    string sourceRoot = FindSourceRoot();
    string appSourceRoot = Path.Combine(sourceRoot, "TerrariaSplit");
    foreach (string sourcePath in Directory.EnumerateFiles(appSourceRoot, "*.cs", SearchOption.AllDirectories))
    {
        string relativePath = Path.GetRelativePath(appSourceRoot, sourcePath);
        if (relativePath.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            relativePath.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        string source = File.ReadAllText(sourcePath);
        AssertEqual(false, source.Contains("Timer" + "HotkeyRequest", StringComparison.Ordinal));
    }

    string timerControllerPath = Path.Combine(appSourceRoot, "Application", "TimerController.cs");
    string timerControllerSource = File.ReadAllText(timerControllerPath);
    AssertEqual(false, timerControllerSource.Contains("HotkeyAction.", StringComparison.Ordinal));
}

static void TestSettingsFormAppliesGlobalScaleFromGeneralPage()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        GeneralSettingsPage page = form.PageHost.GetOrCreatePage<GeneralSettingsPage>(SettingsPageId.General);
        page.GlobalScaleBox.Text = "175";

        form.ApplyForTests();

        AssertEqual(175, form.Result.Columns.ScalePercent);
    });
}

static void TestSettingsFormOrdersMovedPages()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        List<string> labels = form.PageHost.Pages.Select(page => page.Nav.Text).ToList();

        AssertEqual(
            "General|BOSS|Data|UI|Effects|Automation|Sounds|Colors|Advanced|Debug",
            string.Join('|', labels));
    });
}

static void TestSettingsFormAppliesDynamicDeltaUnitsFromUiPage()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings { EnableDynamicDeltaTimeUnits = true });
        UiSettingsPage page = form.PageHost.GetOrCreatePage<UiSettingsPage>(SettingsPageId.Ui);
        page.EnableDynamicDeltaTimeUnitsBox.Checked = false;

        form.ApplyForTests();

        AssertEqual(false, form.Result.EnableDynamicDeltaTimeUnits);
    });
}

static void TestSettingsFormAppliesTextEffectsFromUiPage()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        UiSettingsPage page = form.PageHost.GetOrCreatePage<UiSettingsPage>(SettingsPageId.Ui);
        page.IconOpacityBox.Text = "55";
        page.TimeOpacityBox.Text = "65";
        page.TimeShadowBox.Text = "25";
        page.TimeOutlineThicknessBox.Text = "30";
        page.DeltaOpacityBox.Text = "75";
        page.DeltaShadowBox.Text = "35";
        page.DeltaOutlineThicknessBox.Text = "40";
        page.TimerOpacityBox.Text = "85";
        page.TimerShadowBox.Text = "25";
        page.TimerOutlineThicknessBox.Text = "30";
        page.TimerMillisecondsOpacityBox.Text = "95";
        page.TimerMillisecondsShadowBox.Text = "45";
        page.TimerMillisecondsOutlineThicknessBox.Text = "50";

        form.ApplyForTests();

        AssertEqual(55, form.Result.TextEffects.IconOpacityPercent);
        AssertEqual(65, form.Result.TextEffects.TimeOpacityPercent);
        AssertEqual(25, form.Result.TextEffects.TimeShadowPercent);
        AssertEqual(30, form.Result.TextEffects.TimeOutlineThicknessPercent);
        AssertEqual(75, form.Result.TextEffects.DeltaOpacityPercent);
        AssertEqual(35, form.Result.TextEffects.DeltaShadowPercent);
        AssertEqual(40, form.Result.TextEffects.DeltaOutlineThicknessPercent);
        AssertEqual(85, form.Result.TextEffects.TimerOpacityPercent);
        AssertEqual(25, form.Result.TextEffects.TimerShadowPercent);
        AssertEqual(30, form.Result.TextEffects.TimerOutlineThicknessPercent);
        AssertEqual(95, form.Result.TextEffects.TimerMillisecondsOpacityPercent);
        AssertEqual(45, form.Result.TextEffects.TimerMillisecondsShadowPercent);
        AssertEqual(50, form.Result.TextEffects.TimerMillisecondsOutlineThicknessPercent);
    });
}

static void TestSettingsFormAppliesPracticeWorldSlots()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        GeneralSettingsPage generalPage = form.PageHost.GetOrCreatePage<GeneralSettingsPage>(SettingsPageId.General);
        SetHotkeyBox(generalPage.PracticeWorldKeyBox, Keys.F10);
        AutomationSettingsPage automationPage = form.PageHost.GetOrCreatePage<AutomationSettingsPage>(SettingsPageId.Automation);
        automationPage.AutoCreateSpecialSeedBoxes[AutoCreateSpecialWorldSeed.ForTheWorthy].Checked = true;
        automationPage.AutoCreateSecretSeedsBox.Text = "mole people";
        AutomationSettingsPage.PracticeSlotControls firstSlot = automationPage.PracticeSlots[0];
        firstSlot.NameBox.Text = "Plantera";
        firstSlot.PlayerFilePathBox.Text = "C:\\practice\\player.plr";
        firstSlot.WorldFilePathBox.Text = "C:\\practice\\world.wld";

        form.ApplyForTests();

        AssertEqual(Keys.F10.ToString(), form.Result.PracticeWorldKey);
        AssertEqual(AutoCreateSpecialWorldSeed.ForTheWorthy, form.Result.AutoCreate.SpecialSeeds);
        AssertEqual("mole people", form.Result.AutoCreate.SecretSeeds);
        AssertEqual(PracticeWorldSettings.SlotCount, form.Result.PracticeWorlds.Slots.Count);
        AssertEqual("Plantera", form.Result.PracticeWorlds.Slots[0].Name);
        AssertEqual("C:\\practice\\player.plr", form.Result.PracticeWorlds.Slots[0].PlayerFilePath);
        AssertEqual("C:\\practice\\world.wld", form.Result.PracticeWorlds.Slots[0].WorldFilePath);
    });
}

static void TestSettingsHotkeyBoxCapturesModifierChords()
{
    RunSta(() =>
    {
        using var textBox = new SettingsHotkeyTextBox();
        PressHotkeyBoxKey(textBox, Keys.Control | Keys.F10);
        AssertEqual(Keys.Control | Keys.F10, textBox.Hotkey);
        AssertEqual("Ctrl + F10", textBox.Text);

        PressHotkeyBoxKey(textBox, Keys.Alt | Keys.Shift | Keys.A);
        AssertEqual(Keys.Alt | Keys.Shift | Keys.A, textBox.Hotkey);
        AssertEqual("Alt + Shift + A", textBox.Text);
    });
}

static void TestSettingsFormCollapsesZenithSpecialSeedDependencies()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        AutomationSettingsPage page = form.PageHost.GetOrCreatePage<AutomationSettingsPage>(SettingsPageId.Automation);
        page.AutoCreateSpecialSeedBoxes[AutoCreateSpecialWorldSeed.ForTheWorthy].Checked = true;
        page.AutoCreateSpecialSeedBoxes[AutoCreateSpecialWorldSeed.Remix].Checked = true;
        page.AutoCreateSpecialSeedBoxes[AutoCreateSpecialWorldSeed.Zenith].Checked = true;
        page.AutoCreateSpecialSeedBoxes[AutoCreateSpecialWorldSeed.Skyblock].Checked = true;

        AssertEqual(false, page.AutoCreateSpecialSeedBoxes[AutoCreateSpecialWorldSeed.ForTheWorthy].Checked);
        AssertEqual(false, page.AutoCreateSpecialSeedBoxes[AutoCreateSpecialWorldSeed.ForTheWorthy].Enabled);

        form.ApplyForTests();

        AssertEqual("Zenith|Skyblock", form.Result.AutoCreate.SpecialSeeds);
    });
}

static void TestSettingsFormSavesTheConstantSpecialSeed()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        AutomationSettingsPage page = form.PageHost.GetOrCreatePage<AutomationSettingsPage>(SettingsPageId.Automation);
        page.AutoCreateSpecialSeedBoxes[AutoCreateSpecialWorldSeed.Drunk].Checked = true;
        page.AutoCreateSpecialSeedBoxes[AutoCreateSpecialWorldSeed.TheConstant].Checked = true;

        form.ApplyForTests();

        AssertEqual("Drunk|The Constant", form.Result.AutoCreate.SpecialSeeds);
    });
}

static void TestSettingsFormAppliesZenithStarCatchOptions()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        AutomationSettingsPage page = form.PageHost.GetOrCreatePage<AutomationSettingsPage>(SettingsPageId.Automation);
        page.AutoCreateSpecialSeedBoxes[AutoCreateSpecialWorldSeed.Zenith].Checked = true;
        page.AutoCreateZenithStarCatchBox.Checked = true;
        page.AutoCreateZenithStarCatchStageBoxes[AutoCreateZenithStarCatchStage.GemCaves].Checked = false;
        page.AutoCreateZenithStarCatchSpeedBar.Value = 500;
        page.AutoCreatePyramidFilterBox.Checked = true;

        AssertEqual(true, page.AutoCreateZenithStarCatchStageBoxes[AutoCreateZenithStarCatchStage.LifeCrystals].Checked);
        AssertEqual(true, page.AutoCreateZenithStarCatchStageBoxes[AutoCreateZenithStarCatchStage.Statues].Checked);
        AssertEqual(true, page.AutoCreateZenithStarCatchStageBoxes[AutoCreateZenithStarCatchStage.BuriedChests].Checked);
        AssertEqual(true, page.AutoCreateZenithStarCatchStageBoxes[AutoCreateZenithStarCatchStage.GemCaves].Checked);
        AssertEqual(false, page.AutoCreateZenithStarCatchStageBoxes[AutoCreateZenithStarCatchStage.Pots].Checked);
        AssertEqual(false, page.AutoCreateZenithStarCatchStageBoxes[AutoCreateZenithStarCatchStage.Traps].Checked);

        form.ApplyForTests();

        AssertEqual(true, form.Result.AutoCreate.EnableZenithStarCatch);
        AssertEqual(AutoCreateZenithStarCatchStage.GemCaves, form.Result.AutoCreate.ZenithStarCatchStopStage);
        AssertEqual(500, form.Result.AutoCreate.ZenithStarCatchSpeedSliderValue);
        AssertEqual(true, form.Result.AutoCreate.EnablePyramidFilter);
    });
}

static void TestSettingsFormGatesZenithStarCatchBehindZenithSeed()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings
        {
            AutoCreate = new AutoCreateWorldSettings
            {
                EnableZenithStarCatch = true,
                ZenithStarCatchStopStage = AutoCreateZenithStarCatchStage.Pots
            }
        });
        AutomationSettingsPage page = form.PageHost.GetOrCreatePage<AutomationSettingsPage>(SettingsPageId.Automation);

        AssertEqual(false, page.AutoCreateZenithStarCatchBox.Enabled);
        AssertEqual(false, page.AutoCreateZenithStarCatchStageBoxes[AutoCreateZenithStarCatchStage.LifeCrystals].Enabled);

        page.AutoCreateSpecialSeedBoxes[AutoCreateSpecialWorldSeed.Zenith].Checked = true;

        AssertEqual(true, page.AutoCreateZenithStarCatchBox.Enabled);
        AssertEqual(true, page.AutoCreateZenithStarCatchStageBoxes[AutoCreateZenithStarCatchStage.LifeCrystals].Enabled);
    });
}

static void TestSettingsFormAppliesTimerStartSound()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        SoundSettingsPage page = form.PageHost.GetOrCreatePage<SoundSettingsPage>(SettingsPageId.Sounds);
        page.SoundTextBoxes[nameof(UiSoundSettings.EnterWorld)].Text = "sounds\\timer-start.wav";

        form.ApplyForTests();

        AssertEqual("sounds\\timer-start.wav", form.Result.Sounds.EnterWorld);
    });
}

static void TestZenithStarCatchStageStopRules()
{
    AssertEqual(false, AutoCreateZenithStarCatchStage.ShouldStopAtPass(
        AutoCreateZenithStarCatchStage.GemCaves,
        AutoCreateZenithStarCatchStage.GemCaves));
    AssertEqual(true, AutoCreateZenithStarCatchStage.ShouldStopAtPass(
        AutoCreateZenithStarCatchStage.GemCaves,
        "Moss"));
    AssertEqual(false, AutoCreateZenithStarCatchStage.ShouldStopAtPass(
        AutoCreateZenithStarCatchStage.Pots,
        "Quick Cleanup"));
    AssertEqual(true, AutoCreateZenithStarCatchStage.ShouldStopAtPass(
        AutoCreateZenithStarCatchStage.Pots,
        "Hellforge"));
    AssertEqual(true, AutoCreateZenithStarCatchStage.ShouldStopAtPass(
        AutoCreateZenithStarCatchStage.Traps,
        "Piles"));
}

static void TestZenithStarCatchSpeedRange()
{
    AssertEqual("1.0", AutoCreateZenithStarCatchSpeed.FormatMultiplier(AutoCreateZenithStarCatchSpeed.MinimumSliderValue));
    AssertEqual("50.0", AutoCreateZenithStarCatchSpeed.FormatMultiplier(AutoCreateZenithStarCatchSpeed.MaximumSliderValue));
    AssertEqual("5.0", AutoCreateZenithStarCatchSpeed.FormatMultiplier(AutoCreateZenithStarCatchSpeed.DefaultSliderValue));
    AssertEqual("1.5", AutoCreateZenithStarCatchSpeed.FormatMultiplier(100));
    AssertEqual("2.2", AutoCreateZenithStarCatchSpeed.FormatMultiplier(200));
    AssertEqual("3.2", AutoCreateZenithStarCatchSpeed.FormatMultiplier(300));
    AssertEqual("7.0", AutoCreateZenithStarCatchSpeed.FormatMultiplier(500));
    AssertEqual("12.5", AutoCreateZenithStarCatchSpeed.FormatMultiplier(650));
    AssertEqual("23.0", AutoCreateZenithStarCatchSpeed.FormatMultiplier(800));
}

static void TestPyramidFilterWorldFileScanner()
{
    string directory = GetPublishOutputDirectory("test-output", "pyramid-scanner-tests");
    Directory.CreateDirectory(directory);
    try
    {
        var scanner = new TerrariaWorldFilePyramidScanner();
        TerrariaWorldDimensions dimensions = TerrariaWorldDimensions.FromWorldSize(AutoCreateWorldSize.Small);
        Rectangle corridor = TerrariaWorldFilePyramidScanner.BuildSpeedrunCorridorBounds(dimensions);
        AssertEqual(new Rectangle(1260, 180, 1681, 241), corridor);
        int evidenceX = corridor.Left + corridor.Width / 2;
        int evidenceY = corridor.Top + 32;

        string emptyWorld = Path.Combine(directory, "empty.wld");
        WriteSyntheticWorldFile(emptyWorld, dimensions, null);
        AssertEqual(true, scanner.TryScanSpeedrunCorridor(
            emptyWorld,
            AutoCreateWorldSize.Small,
            1,
            1,
            out PyramidEvidenceScanResult emptyEvidence,
            out _,
            out _));
        AssertEqual(false, emptyEvidence.MeetsThreshold(1, 1));

        string wallWorld = Path.Combine(directory, "wall.wld");
        WriteSyntheticWorldFile(wallWorld, dimensions, new SyntheticTileEvidence(evidenceX, evidenceY, false, 0, 34));
        AssertEqual(true, scanner.TryScanSpeedrunCorridor(
            wallWorld,
            AutoCreateWorldSize.Small,
            1,
            1,
            out PyramidEvidenceScanResult wallEvidence,
            out _,
            out _));
        AssertEqual(true, wallEvidence.MeetsThreshold(1, 1));
        AssertEqual(true, wallEvidence.Wall34Count >= 1);

        string fourthHeaderWallWorld = Path.Combine(directory, "wall-fourth-header.wld");
        WriteSyntheticWorldFile(
            fourthHeaderWallWorld,
            dimensions,
            new SyntheticTileEvidence(evidenceX, evidenceY, false, 0, 34, 0x10));
        AssertEqual(true, scanner.TryScanSpeedrunCorridor(
            fourthHeaderWallWorld,
            AutoCreateWorldSize.Small,
            1,
            1,
            out PyramidEvidenceScanResult fourthHeaderWallEvidence,
            out _,
            out _));
        AssertEqual(true, fourthHeaderWallEvidence.MeetsThreshold(1, 1));
        AssertEqual(true, fourthHeaderWallEvidence.Wall34Count >= 1);

        string tileWorld = Path.Combine(directory, "tile.wld");
        WriteSyntheticWorldFile(tileWorld, dimensions, new SyntheticTileEvidence(evidenceX, evidenceY, true, 151, 0));
        AssertEqual(true, scanner.TryScanSpeedrunCorridor(
            tileWorld,
            AutoCreateWorldSize.Small,
            1,
            1,
            out PyramidEvidenceScanResult tileEvidence,
            out _,
            out _));
        AssertEqual(true, tileEvidence.MeetsThreshold(1, 1));
        AssertEqual(true, tileEvidence.ActiveTile151Count >= 1);

        string outsideWorld = Path.Combine(directory, "outside.wld");
        WriteSyntheticWorldFile(outsideWorld, dimensions, new SyntheticTileEvidence(1, evidenceY, false, 0, 34));
        AssertEqual(true, scanner.TryScanSpeedrunCorridor(
            outsideWorld,
            AutoCreateWorldSize.Small,
            1,
            1,
            out PyramidEvidenceScanResult outsideEvidence,
            out _,
            out _));
        AssertEqual(false, outsideEvidence.MeetsThreshold(1, 1));
    }
    finally
    {
        DeleteDirectoryIfExists(directory);
    }
}

static void TestOverlayCompositeLayoutCalculator()
{
    var settings = new AppSettings();
    settings.Columns.TimerOffsetY = -180;
    Rectangle compositeBounds = new(100, 200, 900, 700);

    AssertEqual(true, OverlayCompositeLayoutCalculator.TryCreate(
        compositeBounds,
        settings,
        statusCount: 9,
        baseRowGap: 9,
        out OverlayCompositeLayout layout));
    AssertEqual(compositeBounds, layout.CompositeBounds);
    AssertEqual(compositeBounds.Width, layout.StatusLocalBounds.Width);
    AssertEqual(compositeBounds.Width, layout.TimerLocalBounds.Width);
    AssertEqual(0, layout.StatusLocalBounds.X);
    AssertEqual(0, layout.TimerLocalBounds.X);
    AssertEqual(0, layout.StatusLocalBounds.Y);
    AssertEqual(true, layout.StatusLocalBounds.Height < compositeBounds.Height);
    AssertEqual(true, layout.StatusLocalBounds.Contains(layout.Layout.GetRowRect(8)));
    AssertEqual(true, layout.TimerLocalBounds.Height < compositeBounds.Height);
    AssertEqual(true, layout.TimerLocalBounds.Contains(layout.Layout.TimerRect));
    AssertEqual(true, layout.TimerLocalBounds.Contains(TimerRenderer.GetTimerTextBounds(settings, layout.Layout.TimerRect)));
    AssertEqual(compositeBounds.Left, layout.StatusScreenBounds.Left);
    AssertEqual(compositeBounds.Left, layout.TimerScreenBounds.Left);
    AssertEqual(compositeBounds.Top, layout.StatusScreenBounds.Top);
    AssertEqual(compositeBounds.Top + layout.TimerLocalBounds.Top, layout.TimerScreenBounds.Top);
    AssertEqual(layout.TimerLocalBounds.Top, layout.MapTimerPointToComposite(Point.Empty).Y);

    var controller = new OverlayBoundsController(baseRowGap: 9, settings, statusCount: 9);
    controller.Initialize(compositeBounds);
    Rectangle originalTimerScreenBounds = controller.CurrentLayout.TimerScreenBounds;
    controller.HandleTimerResize(new Rectangle(
        originalTimerScreenBounds.Left,
        originalTimerScreenBounds.Top,
        originalTimerScreenBounds.Width,
        originalTimerScreenBounds.Height + 50));
    AssertEqual(compositeBounds.Height + 50, controller.CompositeBounds.Height);

    Rectangle resizedComposite = controller.CompositeBounds;
    Rectangle originalStatusScreenBounds = controller.CurrentLayout.StatusScreenBounds;
    controller.HandleStatusResize(new Rectangle(
        originalStatusScreenBounds.Left,
        originalStatusScreenBounds.Top,
        originalStatusScreenBounds.Width + 40,
        originalStatusScreenBounds.Height));
    AssertEqual(resizedComposite.Width + 40, controller.CompositeBounds.Width);
}

static void WriteSyntheticWorldFile(
    string path,
    TerrariaWorldDimensions dimensions,
    SyntheticTileEvidence? evidence)
{
    using FileStream stream = File.Create(path);
    using BinaryWriter writer = new(stream);
    writer.Write(279);
    writer.Write(0x026369676F6C6572UL);
    writer.Write((uint)0);
    writer.Write((ulong)0);
    writer.Write((short)2);
    long sectionPointersOffset = stream.Position;
    writer.Write(0);
    writer.Write(0);
    writer.Write((short)256);
    for (int i = 0; i < 32; i++)
    {
        writer.Write((byte)0);
    }

    int tileSectionOffset = (int)stream.Position;
    stream.Position = sectionPointersOffset;
    writer.Write(tileSectionOffset);
    writer.Write(tileSectionOffset);
    stream.Position = tileSectionOffset;

    for (int x = 0; x < dimensions.Width; x++)
    {
        if (evidence is SyntheticTileEvidence tile && tile.X == x && tile.Y >= 0 && tile.Y < dimensions.Height)
        {
            if (tile.Y > 0)
            {
                WriteSyntheticTile(writer, false, 0, 0, tile.Y);
            }

            WriteSyntheticTile(writer, tile.Active, tile.Type, tile.Wall, 1, tile.QuaternaryFlags);
            int trailing = dimensions.Height - tile.Y - 1;
            if (trailing > 0)
            {
                WriteSyntheticTile(writer, false, 0, 0, trailing);
            }
        }
        else
        {
            WriteSyntheticTile(writer, false, 0, 0, dimensions.Height);
        }
    }
}

static void WriteSyntheticTile(
    BinaryWriter writer,
    bool active,
    ushort type,
    ushort wall,
    int runLength,
    byte quaternaryFlags = 0)
{
    byte flags = 0;
    byte secondaryFlags = 0;
    byte tertiaryFlags = 0;
    if (active)
    {
        flags |= 0x02;
    }

    if (wall > 0)
    {
        flags |= 0x04;
    }

    if (active && type > byte.MaxValue)
    {
        flags |= 0x20;
    }

    int run = Math.Max(0, runLength - 1);
    if (run > 0)
    {
        flags |= run <= byte.MaxValue ? (byte)0x40 : (byte)0x80;
    }

    if (quaternaryFlags != 0)
    {
        tertiaryFlags |= 0x01;
        secondaryFlags |= 0x01;
    }

    if (secondaryFlags != 0)
    {
        flags |= 0x01;
    }

    writer.Write(flags);
    if (secondaryFlags != 0)
    {
        writer.Write(secondaryFlags);
        writer.Write(tertiaryFlags);
        writer.Write(quaternaryFlags);
    }

    if (active)
    {
        writer.Write((byte)(type & 0xFF));
        if (type > byte.MaxValue)
        {
            writer.Write((byte)(type >> 8));
        }
    }

    if (wall > 0)
    {
        writer.Write((byte)(wall & 0xFF));
    }

    if (run > 0)
    {
        if (run <= byte.MaxValue)
        {
            writer.Write((byte)run);
        }
        else
        {
            writer.Write((short)run);
        }
    }
}

static void TestSettingsFormAppliesResumeSound()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        SoundSettingsPage page = form.PageHost.GetOrCreatePage<SoundSettingsPage>(SettingsPageId.Sounds);
        page.SoundTextBoxes[nameof(UiSoundSettings.Resume)].Text = "sounds\\resume.wav";

        form.ApplyForTests();

        AssertEqual("sounds\\resume.wav", form.Result.Sounds.Resume);
    });
}

static void TestSettingsFormAppliesMoonLordSplitSound()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        SoundSettingsPage page = form.PageHost.GetOrCreatePage<SoundSettingsPage>(SettingsPageId.Sounds);
        page.SoundTextBoxes[nameof(UiSoundSettings.MoonLordAheadReferenceAheadSegment)].Text = "sounds\\moonlord-best.wav";

        form.ApplyForTests();

        AssertEqual("sounds\\moonlord-best.wav", form.Result.Sounds.MoonLordAheadReferenceAheadSegment);
    });
}

static void TestSettingsFormLocksReferenceControlsForPersonalBestReference()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings
        {
            UsePersonalBestAsReferenceTime = true,
            PersonalBestTimes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Skeletron"] = "00:30"
            }
        });
        DataSettingsPage page = form.PageHost.GetOrCreatePage<DataSettingsPage>(SettingsPageId.Data);

        AssertEqual(true, page.UsePersonalBestAsReferenceTimeBox.Checked);
        AssertEqual(false, page.ReferenceSetBox.Enabled);
        AssertEqual(false, page.NewReferenceSetNameBox.Enabled);
        AssertEqual("00:30", page.SplitTextBoxes["Skeletron"].Text);
        AssertEqual(true, page.SplitTextBoxes["Skeletron"].ReadOnly);
    });
}

static void TestSettingsFormAppliesTextOutlineAndShadowColors()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        ColorSettingsPage page = form.PageHost.GetOrCreatePage<ColorSettingsPage>(SettingsPageId.Colors);
        IReadOnlyDictionary<string, TextBox> colorTextBoxes = page.ColorTextBoxes;
        colorTextBoxes[nameof(UiColorSettings.ReferenceTextOutline)].Text = "#112233";
        colorTextBoxes[nameof(UiColorSettings.ReferenceTextShadow)].Text = "#445566";
        colorTextBoxes[nameof(UiColorSettings.TimerPausedTextOutline)].Text = "#778899";
        colorTextBoxes[nameof(UiColorSettings.TimerPausedTextShadow)].Text = "#AABBCC";
        colorTextBoxes[nameof(UiColorSettings.SplitCompletionLabelText)].Text = "#DDEEFF";
        colorTextBoxes[nameof(UiColorSettings.SplitCompletionTimeText)].Text = "#FEDCBA";

        form.ApplyForTests();

        AssertEqual("#112233", form.Result.Colors.ReferenceTextOutline);
        AssertEqual("#445566", form.Result.Colors.ReferenceTextShadow);
        AssertEqual("#778899", form.Result.Colors.TimerPausedTextOutline);
        AssertEqual("#AABBCC", form.Result.Colors.TimerPausedTextShadow);
        AssertEqual("#DDEEFF", form.Result.Colors.SplitCompletionLabelText);
        AssertEqual("#FEDCBA", form.Result.Colors.SplitCompletionTimeText);
    });
}

static void TestMainFormPreservesSizeWhenApplyingNonLayoutSettings()
{
    RunSta(() =>
    {
        using var form = new MainForm();
        _ = form.Handle;
        OverlayBoundsController boundsController = GetPrivateField<OverlayBoundsController>(form, "overlayBoundsController");
        AppSettings previousSettings = GetMainFormSettings(form);
        Rectangle initialCompositeBounds = new(120, 160, 1000, 900);
        boundsController.ApplyCompositeBounds(initialCompositeBounds);

        var settings = AppSettingsStore.Clone(previousSettings);
        settings.Colors.TimerText = "#123456";
        SetMainFormSettings(form, settings);

        InvokePrivate(form, "ApplyLoadedSettings", previousSettings, -1);

        AssertEqual(initialCompositeBounds.Size, boundsController.CompositeBounds.Size);
    });
}

static void TestMainFormSettingsApplyFinalizesCurrentRunBeforeReload()
{
    RunSta(() =>
    {
        using var form = new MainForm();
        _ = form.Handle;

        AppSettings previousSettings = GetMainFormSettings(form);
        var nextSettings = AppSettingsStore.Clone(previousSettings);
        nextSettings.AutoUpdatePersonalBestData = false;
        nextSettings.AskBeforeUpdatingPersonalBestData = false;
        nextSettings.AlwaysOnTop = !nextSettings.AlwaysOnTop;

        ApplicationController applicationController = GetPrivateField<ApplicationController>(form, "applicationController");
        var tracker = new BossSplitTracker();
        tracker.SetDefinitions(applicationController.Definitions);
        BossSplitStatus skeletronStatus = tracker.Statuses.First(status =>
            status.Definition.BossIds.Any(bossId => string.Equals(
                bossId,
                BossSplitDefinitions.Skeletron,
                StringComparison.OrdinalIgnoreCase)));
        TimeSpan expectedTime = TimeSpan.FromSeconds(30);
        skeletronStatus.SetTime(expectedTime);
        RuntimeRunSnapshot runtimeSnapshot = RuntimeRunSnapshot.FromState(
            new SplitTimerState(SplitTimerPhase.Paused, expectedTime, 0),
            tracker,
            Stopwatch.GetTimestamp());
        InvokePrivate(
            form,
            "HandleWatcherPollCompleted",
            new WatcherPollNotification(
                TestSnapshots.Terraria(isGameMenu: true),
                TestSnapshots.Terraria(isGameMenu: true),
                TerrariaWatcherDiagnosticsDefaults.Empty,
                runtimeSnapshot,
                [],
                applicationController.MinimumAcceptedRuntimeCommandSequence,
                TimeSpan.Zero,
                Stopwatch.GetTimestamp(),
                TimeSpan.FromMilliseconds(5),
                TimeSpan.Zero,
                null));

        string lastRunDirectory = SplitTimeSetStore.LastRunDirectory;
        DirectorySnapshot lastRunSnapshot = SnapshotDirectory(lastRunDirectory);
        try
        {
            DeleteDirectoryIfExists(lastRunDirectory);

            InvokePrivate(form, "ApplySettings", nextSettings);

            Dictionary<string, string> lastRun = SplitTimeSetStore.LoadLatestLastRun();
            AssertEqual(TimeText.FormatRecord(expectedTime), lastRun[BossSplitDefinitions.Skeletron]);
        }
        finally
        {
            RestoreDirectory(lastRunDirectory, lastRunSnapshot);
        }
    });
}

static void TestMainFormInitializesOverlayLayoutWithCurrentSplitCount()
{
    RunSta(() =>
    {
        using var form = new MainForm();
        _ = form.Handle;

        OverlayBoundsController boundsController = GetPrivateField<OverlayBoundsController>(form, "overlayBoundsController");
        ApplicationController applicationController = GetPrivateField<ApplicationController>(form, "applicationController");
        Rectangle compositeBounds = boundsController.CompositeBounds;
        AppSettings settings = GetMainFormSettings(form);

        AssertEqual(true, SplitLayoutCalculator.TryCreate(
            new Rectangle(0, 0, compositeBounds.Width, compositeBounds.Height),
            applicationController.ViewState.DisplayStatuses.Count,
            9,
            value => OverlayRenderContext.ScaleInt(settings, value),
            out SplitLayout expectedLayout));
        AssertEqual(expectedLayout.FirstRowRect, boundsController.CurrentLayout.Layout.FirstRowRect);
        AssertEqual(expectedLayout.TimerRect, boundsController.CurrentLayout.Layout.TimerRect);
    });
}

static void TestMainFormOverlayClientSizeMatchesStatusLayout()
{
    RunSta(() =>
    {
        using var form = new MainForm();
        _ = form.Handle;

        OverlayBoundsController boundsController = GetPrivateField<OverlayBoundsController>(form, "overlayBoundsController");

        AssertEqual(form.Size, form.ClientSize);
        AssertEqual(boundsController.CurrentLayout.StatusScreenBounds.Size, form.ClientSize);
    });
}

static void TestMainFormScalesSizeWhenGlobalScaleChanges()
{
    RunSta(() =>
    {
        using var form = new MainForm();
        _ = form.Handle;
        OverlayBoundsController boundsController = GetPrivateField<OverlayBoundsController>(form, "overlayBoundsController");
        var previousSettings = new AppSettings();
        previousSettings.Columns.ScalePercent = 100;
        SetMainFormSettings(form, previousSettings);
        InvokePrivate(form, "ApplyLoadedSettings", (object?)null, -1);
        boundsController.ApplyCompositeBounds(new Rectangle(80, 90, 600, 500));
        Size previousSize = boundsController.CompositeBounds.Size;

        var settings = AppSettingsStore.Clone(previousSettings);
        settings.Columns.ScalePercent = 150;
        SetMainFormSettings(form, settings);

        InvokePrivate(form, "ApplyLoadedSettings", previousSettings, -1);

        AssertEqual(
            new Size(
                (int)Math.Round(previousSize.Width * 1.5f, MidpointRounding.AwayFromZero),
                (int)Math.Round(previousSize.Height * 1.5f, MidpointRounding.AwayFromZero)),
            boundsController.CompositeBounds.Size);
    });
}

static void TestMainFormAdjustsWidthWhenSplitColumnsChange()
{
    RunSta(() =>
    {
        using var form = new MainForm();
        _ = form.Handle;
        OverlayBoundsController boundsController = GetPrivateField<OverlayBoundsController>(form, "overlayBoundsController");
        var previousSettings = new AppSettings();
        previousSettings.Columns.ScalePercent = 100;
        SetMainFormSettings(form, previousSettings);
        InvokePrivate(form, "ApplyLoadedSettings", (object?)null, -1);

        boundsController.ApplyCompositeBounds(new Rectangle(80, 90, 600, 500));
        var settings = AppSettingsStore.Clone(previousSettings);
        settings.Columns.Time.Width += 100;
        SetMainFormSettings(form, settings);

        InvokePrivate(form, "ApplyLoadedSettings", previousSettings, -1);

        AssertEqual(new Size(700, 500), boundsController.CompositeBounds.Size);
    });
}

static void TestSettingsFormAppliesCurrentDeltaGradientOption()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings
        {
            EnableDeltaGradientColor = false,
            EnableCurrentDeltaGradientColor = true,
            EnableTimerGradientColor = false
        });
        AnimationSettingsPage page = form.PageHost.GetOrCreatePage<AnimationSettingsPage>(SettingsPageId.Effects);
        page.EnableCurrentDeltaGradientColorBox.Checked = false;

        form.ApplyForTests();

        AssertEqual(false, form.Result.EnableDeltaGradientColor);
        AssertEqual(false, form.Result.EnableCurrentDeltaGradientColor);
        AssertEqual(false, form.Result.EnableTimerGradientColor);
    });
}

static void TestSettingsFormKeepsUncreatedAnimationFieldsUnchanged()
{
    RunSta(() =>
    {
        var settings = new AppSettings
        {
            UndefeatedIconGrayscalePercent = 22,
            UndefeatedIconBrightnessPercent = 73,
            CurrentBossIconGrayscaleWeakenPercent = 11,
            CurrentBossIconBrightnessBoostPercent = 64
        };
        using var form = new SettingsForm(settings);
        form.PageHost.GetOrCreatePage<UiSettingsPage>(SettingsPageId.Ui);

        form.ApplyForTests();

        AssertEqual(22, form.Result.UndefeatedIconGrayscalePercent);
        AssertEqual(73, form.Result.UndefeatedIconBrightnessPercent);
        AssertEqual(11, form.Result.CurrentBossIconGrayscaleWeakenPercent);
        AssertEqual(64, form.Result.CurrentBossIconBrightnessBoostPercent);
    });
}

static void TestSettingsFormAppliesAdvancedUiScalePatchOption()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        AdvancedSettingsPage page = form.PageHost.GetOrCreatePage<AdvancedSettingsPage>(SettingsPageId.Advanced);
        page.EnableTerrariaUiScalePatchBox.Checked = true;

        form.ApplyForTests();

        AssertEqual(true, form.Result.Advanced.EnableTerrariaUiScalePatch);
    });
}

static void TestSettingsFormAppliesTimerOverlayRefreshSettings()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        AdvancedSettingsPage page = form.PageHost.GetOrCreatePage<AdvancedSettingsPage>(SettingsPageId.Advanced);

        page.ReadyWatcherPollHzBox.SelectedIndex = 2;
        page.ReadyUiControlHzBox.SelectedIndex = 3;
        page.RunningStatusPaintHzBox.SelectedIndex = 0;
        AssertEqual(true, page.TimerOverlayRefreshHzBox.Enabled);
        page.TimerOverlayRefreshHzBox.SelectedIndex = 1;

        form.ApplyForTests();

        AssertEqual(480, form.Result.Advanced.ReadyWatcherPollHz);
        AssertEqual(180, form.Result.Advanced.ReadyUiControlHz);
        AssertEqual(60, form.Result.Advanced.RunningStatusPaintHz);
        AssertEqual(90, form.Result.Advanced.TimerOverlayRefreshHz);
    });
}

static void TestTerrariaUiScalePatchPlan()
{
    IReadOnlyList<byte[]> originalPatterns = GetUiScalePatchPatterns("OriginalPattern");
    IReadOnlyList<byte[]> patchedPatterns = GetUiScalePatchPatterns("PatchedPattern");
    byte[] buffer = BuildPatternBuffer(originalPatterns);

    TerrariaUiScalePatchPlan plan = TerrariaUiScalePatch.CreatePlan(buffer, IntPtr.Zero);
    AssertEqual(true, plan.CanApply);
    AssertEqual(false, plan.AlreadyApplied);
    AssertEqual(originalPatterns.Count, plan.Writes.Count);

    byte[] patched = TerrariaUiScalePatch.ApplyToBufferForTest(buffer);
    TerrariaUiScalePatchPlan patchedPlan = TerrariaUiScalePatch.CreatePlan(patched, IntPtr.Zero);
    AssertEqual(true, patchedPlan.CanApply);
    AssertEqual(true, patchedPlan.AlreadyApplied);

    foreach (byte[] pattern in patchedPatterns)
    {
        AssertEqual(true, ContainsPattern(patched, pattern));
    }
}

static IReadOnlyList<byte[]> GetUiScalePatchPatterns(string propertyName)
{
    FieldInfo operationsField = typeof(TerrariaUiScalePatch).GetField(
            "Operations",
            BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Missing Terraria UI scale patch operations.");
    var operations = (Array)(operationsField.GetValue(null)
        ?? throw new InvalidOperationException("Terraria UI scale patch operations are null."));
    var patterns = new List<byte[]>();

    foreach (object operation in operations)
    {
        PropertyInfo property = operation.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing patch operation property {propertyName}.");
        patterns.Add((byte[])(property.GetValue(operation)
            ?? throw new InvalidOperationException($"Patch operation property {propertyName} is null.")));
    }

    return patterns;
}

static byte[] BuildPatternBuffer(IReadOnlyList<byte[]> patterns)
{
    var bytes = new List<byte>();
    for (int index = 0; index < patterns.Count; index++)
    {
        bytes.Add(0x41);
        bytes.Add((byte)index);
        bytes.Add(0x52);
        bytes.AddRange(patterns[index]);
        bytes.Add(0x7F);
    }

    return bytes.ToArray();
}

static bool ContainsPattern(byte[] buffer, byte[] pattern)
{
    if (pattern.Length == 0 || buffer.Length < pattern.Length)
    {
        return false;
    }

    for (int start = 0; start <= buffer.Length - pattern.Length; start++)
    {
        bool matches = true;
        for (int index = 0; index < pattern.Length; index++)
        {
            if (buffer[start + index] != pattern[index])
            {
                matches = false;
                break;
            }
        }

        if (matches)
        {
            return true;
        }
    }

    return false;
}

static void RunSta(Action action)
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure is not null)
    {
        throw failure;
    }
}

static void SetHotkeyBox(TextBox textBox, Keys key)
{
    MethodInfo method = textBox.GetType().GetMethod(
            "SetHotkey",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Missing hotkey setter.");
    method.Invoke(textBox, [key]);
}

static void PressHotkeyBoxKey(SettingsHotkeyTextBox textBox, Keys keyData)
{
    MethodInfo method = typeof(SettingsHotkeyTextBox).GetMethod(
            "OnKeyDown",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Missing hotkey keydown handler.");
    var args = new KeyEventArgs(keyData);
    method.Invoke(textBox, [args]);
}

static void SetMainFormSettings(MainForm form, AppSettings settings)
{
    ApplicationController controller = GetPrivateField<ApplicationController>(form, "applicationController");
    AppSettings clonedSettings = AppSettingsStore.Clone(settings);
    IReadOnlyList<BossSplitDefinition> definitions = BossSplitDefinitions.Build(clonedSettings);
    SetPrivateField(controller, "<Settings>k__BackingField", clonedSettings);
    SetPrivateField(controller, "<Definitions>k__BackingField", definitions);
    SetPrivateField(controller, "<ViewState>k__BackingField", ApplicationViewState.FromDefinitions(clonedSettings, definitions));
}

static AppSettings GetMainFormSettings(MainForm form)
{
    ApplicationController controller = GetPrivateField<ApplicationController>(form, "applicationController");
    return AppSettingsStore.Clone(controller.Settings);
}

static void SetPrivateField(object target, string fieldName, object? value)
{
    FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Missing field {fieldName} on {target.GetType().Name}.");
    field.SetValue(target, value);
}

static T GetPrivateField<T>(object target, params string[] fieldNames)
{
    object? current = target;
    Type? currentType = target.GetType();
    foreach (string fieldName in fieldNames)
    {
        if (current is null || currentType is null)
        {
            throw new InvalidOperationException($"Field path {string.Join('.', fieldNames)} resolved to null before {fieldName}.");
        }

        FieldInfo field = currentType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Missing field {fieldName} on {currentType.Name}.");
        current = field.GetValue(current);
        currentType = current?.GetType();
    }

    if (current is not T value)
    {
        throw new InvalidOperationException($"Field path {string.Join('.', fieldNames)} was not a {typeof(T).Name}.");
    }

    return value;
}

static object? InvokePrivate(object target, string name, params object?[] args)
{
    MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Missing private method {name}.");

    try
    {
        return method.Invoke(target, args);
    }
    catch (TargetInvocationException ex) when (ex.InnerException is not null)
    {
        throw ex.InnerException;
    }
}

static string GetPublishOutputDirectory(params string[] segments)
{
    string path = Path.Combine(
        [Directory.GetCurrentDirectory(), "publish", .. segments, Guid.NewGuid().ToString("N")]);
    Directory.CreateDirectory(path);
    return path;
}

static string FindSourceRoot()
{
    string directory = Directory.GetCurrentDirectory();
    while (!string.IsNullOrWhiteSpace(directory))
    {
        if (File.Exists(Path.Combine(directory, "TerrariaSplit.slnx")))
        {
            return directory;
        }

        string? parent = Directory.GetParent(directory)?.FullName;
        if (string.Equals(parent, directory, StringComparison.OrdinalIgnoreCase))
        {
            break;
        }

        directory = parent ?? string.Empty;
    }

    throw new DirectoryNotFoundException("TerrariaSplit source root was not found.");
}

static string FindDefaultSettingsTemplate()
{
    string outputTemplatePath = AppSettingsDefaults.TemplatePath;
    if (File.Exists(outputTemplatePath))
    {
        return outputTemplatePath;
    }

    string directory = Directory.GetCurrentDirectory();
    while (!string.IsNullOrWhiteSpace(directory))
    {
        string sourceTemplatePath = Path.Combine(directory, "TerrariaSplit", "settings", "settings.json");
        if (File.Exists(sourceTemplatePath))
        {
            return sourceTemplatePath;
        }

        string? parent = Directory.GetParent(directory)?.FullName;
        if (string.Equals(parent, directory, StringComparison.OrdinalIgnoreCase))
        {
            break;
        }

        directory = parent ?? string.Empty;
    }

    throw new FileNotFoundException("Default settings template was not found.", outputTemplatePath);
}

static void AssertJsonCoversType(Type type, JsonElement element, string path)
{
    if (element.ValueKind != JsonValueKind.Object)
    {
        throw new InvalidOperationException($"{path} must be a JSON object.");
    }

    foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
    {
        if (!property.CanWrite || property.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
        {
            continue;
        }

        if (!element.TryGetProperty(property.Name, out JsonElement child))
        {
            throw new InvalidOperationException($"{path}.{property.Name} is missing from the default settings template.");
        }

        Type? nestedType = GetTemplateNestedType(property.PropertyType);
        if (nestedType is not null)
        {
            AssertJsonCoversType(nestedType, child, $"{path}.{property.Name}");
            continue;
        }

        if (property.PropertyType == typeof(List<PracticeWorldSlot>))
        {
            if (child.ValueKind != JsonValueKind.Array || child.GetArrayLength() == 0)
            {
                throw new InvalidOperationException($"{path}.{property.Name} must contain default practice world slots.");
            }

            AssertJsonCoversType(typeof(PracticeWorldSlot), child[0], $"{path}.{property.Name}[0]");
        }
    }
}

static Type? GetTemplateNestedType(Type type)
{
    if (type == typeof(UiColorSettings) ||
        type == typeof(UiSoundSettings) ||
        type == typeof(UiColumnLayoutSettings) ||
        type == typeof(UiColumnSettings) ||
        type == typeof(UiTextEffectSettings) ||
        type == typeof(AutoCreateWorldSettings) ||
        type == typeof(PracticeWorldSettings) ||
        type == typeof(AdvancedSettings))
    {
        return type;
    }

    return null;
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

static DirectorySnapshot SnapshotDirectory(string path)
{
    if (!Directory.Exists(path))
    {
        return new DirectorySnapshot(false, new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase));
    }

    return new DirectorySnapshot(
        true,
        Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .ToDictionary(
                filePath => Path.GetRelativePath(path, filePath),
                File.ReadAllBytes,
                StringComparer.OrdinalIgnoreCase));
}

static void RestoreDirectory(string path, DirectorySnapshot snapshot)
{
    DeleteDirectoryIfExists(path);
    if (!snapshot.Exists && snapshot.Files.Count == 0)
    {
        return;
    }

    Directory.CreateDirectory(path);
    foreach ((string relativePath, byte[] content) in snapshot.Files)
    {
        string filePath = Path.Combine(path, relativePath);
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(filePath, content);
    }
}

static void DeleteDirectoryIfExists(string path)
{
    if (Directory.Exists(path))
    {
        Directory.Delete(path, true);
    }
}

static void Nearly(double expected, double actual, double tolerance)
{
    if (Math.Abs(expected - actual) > tolerance)
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

readonly record struct SyntheticTileEvidence(int X, int Y, bool Active, ushort Type, ushort Wall, byte QuaternaryFlags = 0);

readonly record struct DirectorySnapshot(bool Exists, Dictionary<string, byte[]> Files);
