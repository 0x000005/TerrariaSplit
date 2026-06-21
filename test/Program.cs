using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using System.Windows.Forms;
using TerrariaSplit;
using TerrariaSplit.Tests;
using TerrariaSplit.Terraria.WorldGeneration.Simulation;
using ScannerPyramidChestItem = TerrariaSplit.Terraria.Automation.PyramidChestItem;
using ScannerPyramidChestItemNames = TerrariaSplit.Terraria.Automation.PyramidChestItemNames;

if (PyramidPreScreenMetrics.TryRun(args))
{
    return;
}

if (PyramidPreScreenTrace.TryRun(args))
{
    return;
}

var legacyTests = new (string Name, Action Test)[]
{
    ("SignaturePattern matches wildcard bytes", TestSignaturePatternWildcard),
    ("SplitTimerFormatter formats minute and hour values", TestSplitTimerFormatter),
    ("Rolling performance counter keeps a bounded window", TestRollingPerformanceCounter),
    ("Runtime performance tracker separates paint ticks from completed paints", TestRuntimePerformancePaintDiagnostics),
    ("RunEventProcessor suppresses attached split completion animation", TestRunEventProcessorSuppressesAttachedSplitCompletionAnimation),
    ("SplitTimer clamps practice time at zero", TestSplitTimerPracticeClamp),
    ("SplitRouteGroups builds enabled main split entries", TestSplitRouteGroups),
    ("RunFinalizer applies simplified personal best eligibility", TestRunFinalizerSimplifiedPersonalBestEligibility),
    ("SplitConditionText parses ALL and ATLEAST syntax", TestSplitConditionTextParsesAllAndAtLeastSyntax),
    ("SplitCatalog preserves nested split conditions", TestSplitCatalogPreservesNestedSplitConditions),
    ("SplitConditionDataRows expands route conditions", TestSplitConditionDataRows),
    ("SplitConditionDataRows calculates nested reference completion time", TestSplitConditionDataRowsCalculatesNestedReferenceCompletionTime),
    ("SplitConditionDataRows aggregates AtLeast cumulative times", TestSplitConditionDataRowsAggregatesAtLeastTimes),
    ("SplitCatalog maps reference target icons", TestSplitCatalogReferenceTargetIcons),
    ("SplitCatalog builds split icon overrides", TestSplitCatalogBuildsSplitIconOverrides),
    ("Statistics table expands condition rows and keeps route segments", TestStatisticsTableExpandsConditionRowsAndKeepsRouteSegments),
    ("TerrariaMenuGeometry maps 900p menu coordinates", TestTerrariaMenuGeometry),
    ("Localizer returns English fallback and Chinese Crimson", TestLocalizer),
    ("JsonFileStore writes settings atomically", TestJsonFileStoreWritesAtomically),
    ("Default settings template covers serializable settings", TestDefaultSettingsTemplateCoversSerializableSettings),
    ("Legacy flat settings JSON migrates to sections", TestLegacyFlatSettingsJsonMigratesToSections),
    ("Default attached split display matches primary display", TestDefaultAttachedSplitDisplayMatchesPrimaryDisplay),
    ("Default reference times match default settings route", TestDefaultReferenceTimesMatchDefaultSettingsRoute),
    ("AppSettingsStore writes embedded defaults when settings file is invalid", TestAppSettingsStoreWritesEmbeddedDefaultsWhenSettingsFileIsInvalid),
    ("SplitTimeSetStore writes embedded WR when reference files are invalid", TestSplitTimeSetStoreWritesEmbeddedWrWhenReferenceFilesAreInvalid),
    ("AppSettingsStore save does not mutate source settings", TestAppSettingsStoreSaveDoesNotMutateSourceSettings),
    ("Runtime data paths use final directory layout", TestRuntimeDataPathsUseFinalDirectoryLayout),
    ("OperationResult preserves user message and exception", TestOperationResultPreservesFailureDetail),
    ("AppLogger is disabled by default", TestAppLoggerIsDisabledByDefault),
    ("Main publish is single file", TestMainPublishIsSingleFile),
    ("MemoryProbe publish is self contained", TestMemoryProbePublishIsSelfContained),
    ("World pool signature starts with Terraria version", TestWorldPoolSignatureStartsWithTerrariaVersion),
    ("World pool file names use TerrariaSplit timestamp", TestWorldPoolFileNameUsesTerrariaSplitTimestamp),
    ("Terraria seed random matches UnifiedRandom sequence", TestTerrariaSeedRandomMatchesUnifiedRandomSequence),
    ("Terraria copied seed builder formats options", TestTerrariaCopiedSeedBuilderFormatsOptions),
    ("Terraria world name generator follows GUI rules", TestTerrariaWorldNameGeneratorFollowsGuiRules),
    ("SettingsNormalizer clamps auto-create timings", TestSettingsNormalize),
    ("SettingsNormalizer normalizes timer overlay refresh settings", TestSettingsNormalizeTimerOverlayRefresh),
    ("SettingsNormalizer normalizes practice world slots", TestSettingsNormalizePracticeWorlds),
    ("SettingsNormalizer clamps text effects", TestSettingsNormalizeTextEffects),
    ("SettingsNormalizer derives split icons from conditions", TestSettingsNormalizeDerivesSplitIconsFromConditions),
    ("SettingsNormalizer assigns internal split ids", TestSettingsNormalizerAssignsInternalSplitIds),
    ("SettingsNormalizer normalizes UI font families", TestSettingsNormalizeUiFontFamilies),
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
    ("Settings form applies attached split display settings", TestSettingsFormAppliesAttachedSplitDisplaySettings),
    ("Settings form applies UI font families", TestSettingsFormAppliesUiFontFamilies),
    ("Settings form applies practice world slots", TestSettingsFormAppliesPracticeWorldSlots),
    ("Settings form preserves advanced split route", TestSettingsFormPreservesAdvancedSplitRoute),
    ("Settings form keeps advanced condition mode per group", TestSettingsFormKeepsAdvancedConditionModePerGroup),
    ("Settings form warns and blocks lossy advanced condition downgrade", TestSettingsFormBlocksLossyAdvancedConditionDowngrade),
    ("Settings form allows empty advanced condition downgrade", TestSettingsFormAllowsEmptyAdvancedConditionDowngrade),
    ("Settings form switches split conditions without overwrite", TestSettingsFormSwitchesSplitConditionsWithoutOverwrite),
    ("Settings form saves attached route flags", TestSettingsFormSavesAttachedRouteFlags),
    ("Settings form saves split icon override", TestSettingsFormSavesSplitIconOverride),
    ("Settings form saves localized split icon override", TestSettingsFormSavesLocalizedSplitIconOverride),
    ("Settings form warns and rejects invalid split route apply", TestSettingsFormRejectsInvalidSplitRouteApply),
    ("Settings form edits match mode from dropdown", TestSettingsFormEditsMatchModeFromDropdown),
    ("Settings form decrements match mode when deleting condition", TestSettingsFormDecrementsMatchModeWhenDeletingCondition),
    ("Settings form edits item quantity from selected condition", TestSettingsFormEditsItemQuantityFromSelectedCondition),
    ("Settings form searches item targets by name", TestSettingsFormSearchesItemTargetsByName),
    ("Settings form searches NPC targets by name", TestSettingsFormSearchesNpcTargetsByName),
    ("Settings form adds selected target to new group", TestSettingsFormAddsSelectedTargetToNewGroup),
    ("Settings form localizes target library and conditions", TestSettingsFormLocalizesTargetLibraryAndConditions),
    ("Settings form updates effects route rows dynamically", TestSettingsFormUpdatesEffectsRouteRowsDynamically),
    ("Settings hotkey box captures modifier chords", TestSettingsHotkeyBoxCapturesModifierChords),
    ("Settings form collapses zenith special seed dependencies", TestSettingsFormCollapsesZenithSpecialSeedDependencies),
    ("Settings form saves The Constant special seed", TestSettingsFormSavesTheConstantSpecialSeed),
    ("Settings form saves pyramid item filter", TestSettingsFormSavesPyramidItemFilter),
    ("Settings form applies Zenith star catch options", TestSettingsFormAppliesZenithStarCatchOptions),
    ("Settings form gates Zenith star catch behind Zenith seed", TestSettingsFormGatesZenithStarCatchBehindZenithSeed),
    ("Settings form keeps world pool independent from pyramid filter", TestSettingsFormKeepsWorldPoolIndependentFromPyramidFilter),
    ("Debug sequence uses pooled world path when pool has a world", TestDebugSequenceUsesPooledWorldPath),
    ("Settings form applies timer start sound", TestSettingsFormAppliesTimerStartSound),
    ("Settings form applies resume sound", TestSettingsFormAppliesResumeSound),
    ("Settings form applies Moon Lord split sound", TestSettingsFormAppliesMoonLordSplitSound),
    ("Settings form locks reference controls when PB reference is enabled", TestSettingsFormLocksReferenceControlsForPersonalBestReference),
    ("Settings form applies text outline and shadow colors", TestSettingsFormAppliesTextOutlineAndShadowColors),
    ("Main form preserves size when applying non-layout settings", TestMainFormPreservesSizeWhenApplyingNonLayoutSettings),
    ("Main form settings apply redraws static status overlay content", TestMainFormSettingsApplyRedrawsStaticStatusOverlayContent),
    ("Main form settings apply reloads definitions and records current run", TestMainFormSettingsApplyReloadsDefinitionsAndRecordsCurrentRun),
    ("Window layer defers modal state updates", TestWindowLayerDefersModalStateUpdates),
    ("Main form initializes overlay layout with current split count", TestMainFormInitializesOverlayLayoutWithCurrentSplitCount),
    ("Main form overlay client size matches status layout", TestMainFormOverlayClientSizeMatchesStatusLayout),
    ("Main form scales size when global scale changes", TestMainFormScalesSizeWhenGlobalScaleChanges),
    ("Main form adjusts width when split columns change", TestMainFormAdjustsWidthWhenSplitColumnsChange),
    ("Main form grows height when split route grows", TestMainFormGrowsHeightWhenSplitRouteGrows),
    ("Settings form applies current delta gradient option", TestSettingsFormAppliesCurrentDeltaGradientOption),
    ("Settings form applies advanced UI scale patch option", TestSettingsFormAppliesAdvancedUiScalePatchOption),
    ("Settings form applies timer overlay refresh settings", TestSettingsFormAppliesTimerOverlayRefreshSettings),
    ("Settings form keeps uncreated animation fields unchanged", TestSettingsFormKeepsUncreatedAnimationFieldsUnchanged),
    ("Color settings labels follow requested order", TestColorSettingsLabelsFollowRequestedOrder),
    ("Terraria UI scale patch rewrites target IL constants", TestTerrariaUiScalePatchPlan),
    ("Zenith star catch stop stages follow world generation order", TestZenithStarCatchStageStopRules),
    ("Zenith star catch speed uses logarithmic stepped range", TestZenithStarCatchSpeedRange),
    ("Pyramid scanner reads world metadata", TestPyramidFilterWorldFileScanner),
    ("Pyramid scanner reads chest contents", TestPyramidScannerReadsChestContents),
    ("Pyramid filter fast-opens after world generation state ends", TestPyramidFilterFastOpensAfterGenerationStateEnds),
    ("Pyramid filter falls back to stable file wait without generation state", TestPyramidFilterFallsBackWithoutGenerationState),
    ("Pyramid filter treats empty item mask as all candidate items", TestPyramidFilterTreatsEmptyItemMaskAsAllCandidateItems),
    ("Pyramid seed pre-screen only enables supported scope", TestPyramidSeedPreScreenScope),
    ("Pyramid seed pre-screen loop accepts after rejected seed", TestPyramidSeedPreScreenLoopAcceptsAfterRejectedSeed),
    ("Pyramid seed pre-screen loop stops first rejection without local retry", TestPyramidSeedPreScreenLoopStopsFirstRejectionWithoutLocalRetry),
    ("Pyramid seed pre-screen loop retries transient seed read failure", TestPyramidSeedPreScreenLoopRetriesTransientSeedReadFailure),
    ("Pyramid seed pre-screen loop does not retry seed read failure without local retry", TestPyramidSeedPreScreenLoopDoesNotRetrySeedReadFailureWithoutLocalRetry),
    ("Pyramid seed pre-screen loop stops after repeated seed read failures", TestPyramidSeedPreScreenLoopStopsAfterRepeatedSeedReadFailures),
    ("Pyramid seed pre-screen marks only dungeon-side target boundary uncertainty", TestPyramidSeedPreScreenDungeonBoundaryRisk),
    ("Pyramid seed pre-screen rejects known official no-tower false positives", TestPyramidSeedPreScreenRejectsKnownOfficialNoTowerFalsePositives),
    ("Pyramid seed pre-screen predicts known pyramid seed", TestPyramidSeedPreScreenPredictsKnownPyramidSeed),
    ("Pyramid seed pre-screen evaluator requires selected item", TestPyramidSeedPreScreenEvaluatorRequiresSelectedItem),
    ("Pyramid seed pre-screen keeps first pyramid chest", TestPyramidSeedPreScreenKeepsFirstPyramidChest),
    ("World seed metadata matches world options", TestWorldSeedMetadataMatchesWorldOptions),
    ("Overlay composite layout derives status and timer windows from shared bounds", TestOverlayCompositeLayoutCalculator)
};
var tests = legacyTests
    .Concat(HotkeyTests.All())
    .Concat(AutomationRunnerTests.All())
    .Concat(LoadWorldValidationTests.All())
    .Concat(HighPrecisionSchedulerTests.All())
    .Concat(MainShellRefactorTests.All())
    .Concat(ArchitectureDependencyTests.All())
    .Concat(RenderingTests.All())
    .Concat(WorldGenerationMemoryTests.All())
    .Concat(TerrariaMemoryResolverTests.All())
    .ToArray();
string? testFilter = Environment.GetEnvironmentVariable("TERRARIA_SPLIT_TEST_FILTER");
if (!string.IsNullOrWhiteSpace(testFilter))
{
    tests = tests
        .Where(test => test.Name.Contains(testFilter, StringComparison.OrdinalIgnoreCase))
        .ToArray();
}

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

static void TestRunEventProcessorSuppressesAttachedSplitCompletionAnimation()
{
    var settings = new AppSettings { Overlay = { ShowSplitCompletionAnimation = true } };
    var normal = new SplitDefinition(
        "split:normal",
        "Normal",
        SplitCondition.Fact("fact:normal"),
        [],
        [],
        []);
    var attached = new SplitDefinition(
        "split:attached",
        "Attached",
        SplitCondition.Fact("fact:attached"),
        [],
        [],
        [],
        IsAttached: true);
    var statuses = new[]
    {
        new SplitStatusSnapshot(normal, TimeSpan.FromSeconds(5), IsSkipped: false, CompletedFactKeys: []),
        new SplitStatusSnapshot(attached, TimeSpan.FromSeconds(7), IsSkipped: false, CompletedFactKeys: [])
    };
    var viewState = new ApplicationViewState(
        settings,
        RuntimeRunSnapshot.Empty,
        statuses,
        CurrentSplitIndex: 2,
        new SplitTimerState(SplitTimerPhase.Running, TimeSpan.FromSeconds(7), 0),
        StatusHash: 0,
        HasRuntimeSnapshot: true);

    IReadOnlyList<ApplicationEffect> attachedEffects = RunEventProcessor.Process(
        [new RunEvent(RunEventKind.SplitCompleted, SplitIndex: 1)],
        settings,
        viewState,
        new RunLifecycleController(),
        _ => []);
    AssertEqual(false, attachedEffects.Any(effect => effect is StartSplitCompletionAnimationEffect));
    AssertEqual(false, attachedEffects.Any(effect => effect is ClearSplitCompletionAnimationEffect));
    AssertEqual(true, attachedEffects.Any(effect => effect is TrackSegmentBestDeltaHighlightEffect));

    IReadOnlyList<ApplicationEffect> normalEffects = RunEventProcessor.Process(
        [new RunEvent(RunEventKind.SplitCompleted, SplitIndex: 0)],
        settings,
        viewState,
        new RunLifecycleController(),
        _ => []);
    AssertEqual(true, normalEffects.Any(effect => effect is StartSplitCompletionAnimationEffect));
}

static void TestSplitTimerPracticeClamp()
{
    var timer = new SplitTimer();
    timer.SetPracticeElapsed(TimeSpan.FromSeconds(-5));
    AssertEqual(TimeSpan.Zero, timer.Elapsed);
}

static void TestSplitRouteGroups()
{
    var settings = new AppSettings
    {
        Route =
        {
            SplitRoute =
            [
                new SplitRouteEntry
                {
                    Id = "split:skeletron",
                    DisplayName = "Skeletron",
                    Enabled = true,
                    Condition = SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                    IconTargetIds = [SplitCatalog.Skeletron]
                },
                new SplitRouteEntry
                {
                    Id = "split:wall-of-flesh",
                    DisplayName = "Wall of Flesh",
                    Enabled = false,
                    Condition = SplitCatalog.CreateBossFactCondition(SplitCatalog.WallOfFlesh),
                    IconTargetIds = [SplitCatalog.WallOfFlesh]
                },
                new SplitRouteEntry
                {
                    Id = "split:mechanical",
                    DisplayName = "Mechanical",
                    Enabled = true,
                    Condition = SplitCondition.Any(
                    [
                        SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer),
                        SplitCatalog.CreateBossFactCondition(SplitCatalog.Twins)
                    ]),
                    IconTargetIds = [SplitCatalog.Destroyer, SplitCatalog.Twins]
                },
                new SplitRouteEntry
                {
                    Id = "split:attached",
                    DisplayName = "Attached",
                    Enabled = true,
                    IsAttached = true,
                    Condition = SplitCatalog.CreateItemEverOwnedCondition(50, 1),
                    IconTargetIds = [SplitCatalog.CreateItemTargetId(50)]
                }
            ]
        }
    };

    List<RouteGroup> groups = SplitRouteGroups.Build(settings);
    AssertEqual(2, groups.Count);
    AssertEqual("split:skeletron", groups[0].Key);
    AssertEqual("split:mechanical", groups[1].Key);
}

static void TestRunFinalizerSimplifiedPersonalBestEligibility()
{
    var settings = new AppSettings { Route = { SplitRoute = [
            CreateBossRouteEntryForTest("split:skeletron", "Skeletron", SplitCatalog.Skeletron),
            CreateBossRouteEntryForTest("split:wall-of-flesh", "Wall of Flesh", SplitCatalog.WallOfFlesh),
            CreateBossRouteEntryForTest("split:moon-lord", "Moon Lord", SplitCatalog.MoonLord)
        ] } };
    SettingsNormalizer.Normalize(settings);
    SplitDefinition[] definitions = SplitCatalog.Build(settings).ToArray();

    var skippedBeforeLast = BuildPendingPersonalBestUpdatesForTest(
        settings,
        [
            new SplitStatusSnapshot(definitions[0], null, IsSkipped: true, CompletedFactKeys: []),
            new SplitStatusSnapshot(definitions[1], TimeSpan.FromSeconds(20), IsSkipped: false, CompletedFactKeys: []),
            new SplitStatusSnapshot(definitions[2], null, IsSkipped: false, CompletedFactKeys: [])
        ]);
    AssertEqual(false, skippedBeforeLast.HasUpdates);

    var laterUnfinished = BuildPendingPersonalBestUpdatesForTest(
        settings,
        [
            new SplitStatusSnapshot(definitions[0], TimeSpan.FromSeconds(10), IsSkipped: false, CompletedFactKeys: []),
            new SplitStatusSnapshot(definitions[1], TimeSpan.FromSeconds(20), IsSkipped: false, CompletedFactKeys: []),
            new SplitStatusSnapshot(definitions[2], null, IsSkipped: false, CompletedFactKeys: [])
        ]);
    AssertEqual(true, laterUnfinished.HasUpdates);
    AssertEqual(2, laterUnfinished.SegmentUpdateCount);
    AssertEqual(false, laterUnfinished.HasTimeUpdate);

    SplitCondition attachedItem50 = SplitCatalog.CreateItemEverOwnedCondition(50, 1);
    SplitCondition attachedItem51 = SplitCatalog.CreateItemEverOwnedCondition(51, 1);
    var attachedSettings = new AppSettings
    {
        Route =
        {
            SplitRoute =
            [
                CreateBossRouteEntryForTest("split:skeletron", "Skeletron", SplitCatalog.Skeletron),
                new SplitRouteEntry
                {
                    Id = "split:attached-item",
                    DisplayName = "Attached",
                    Enabled = true,
                    IsAttached = true,
                    Condition = SplitCondition.AtLeast([attachedItem50, attachedItem51], 1),
                    IconTargetIds = [SplitCatalog.CreateItemTargetId(50), SplitCatalog.CreateItemTargetId(51)]
                },
                CreateBossRouteEntryForTest("split:moon-lord", "Moon Lord", SplitCatalog.MoonLord)
            ]
        }
    };
    SettingsNormalizer.Normalize(attachedSettings);
    SplitDefinition[] attachedDefinitions = SplitCatalog.Build(attachedSettings).ToArray();

    var attachedSkipped = BuildPendingPersonalBestUpdatesForTest(
        attachedSettings,
        [
            new SplitStatusSnapshot(attachedDefinitions[0], TimeSpan.FromSeconds(10), IsSkipped: false, CompletedFactKeys: []),
            new SplitStatusSnapshot(attachedDefinitions[1], null, IsSkipped: true, CompletedFactKeys: []),
            new SplitStatusSnapshot(attachedDefinitions[2], TimeSpan.FromSeconds(20), IsSkipped: false, CompletedFactKeys: [])
        ]);
    AssertEqual(true, attachedSkipped.HasTimeUpdate);
    AssertEqual(false, attachedSkipped.TimeUpdateSplits.Keys.Any(key =>
        key.Contains("split:attached-item", StringComparison.OrdinalIgnoreCase)));

    var attachedCompleted = BuildPendingPersonalBestUpdatesForTest(
        attachedSettings,
        [
            new SplitStatusSnapshot(attachedDefinitions[0], TimeSpan.FromSeconds(10), IsSkipped: false, CompletedFactKeys: []),
            new SplitStatusSnapshot(
                attachedDefinitions[1],
                TimeSpan.FromSeconds(12),
                IsSkipped: false,
                CompletedFactKeys: [attachedItem51.FactKey],
                FactCompletionTimes: new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
                {
                    [attachedItem51.FactKey] = TimeSpan.FromSeconds(11)
                }),
            new SplitStatusSnapshot(attachedDefinitions[2], TimeSpan.FromSeconds(20), IsSkipped: false, CompletedFactKeys: [])
        ]);
    AssertEqual(true, attachedCompleted.HasTimeUpdate);
    List<KeyValuePair<string, string>> attachedCumulativeRows = attachedCompleted.TimeUpdateSplits
        .Where(pair => pair.Key.Contains("split:attached-item", StringComparison.OrdinalIgnoreCase))
        .ToList();
    AssertEqual(1, attachedCumulativeRows.Count);
    AssertEqual("0:12.00", attachedCumulativeRows[0].Value);
    AssertEqual("condition:split:attached-item:complete", attachedCumulativeRows[0].Key);
}

static SplitRouteEntry CreateBossRouteEntryForTest(string id, string displayName, string bossTargetId)
{
    return new SplitRouteEntry
    {
        Id = id,
        DisplayName = displayName,
        Enabled = true,
        Condition = SplitCatalog.CreateBossFactCondition(bossTargetId),
        IconTargetIds = [bossTargetId]
    };
}

static (bool HasUpdates, int SegmentUpdateCount, bool HasTimeUpdate, Dictionary<string, string> TimeUpdateSplits) BuildPendingPersonalBestUpdatesForTest(
    AppSettings settings,
    IReadOnlyList<SplitStatusSnapshot> statuses)
{
    MethodInfo method = typeof(RunFinalizer).GetMethod(
            "BuildPendingPersonalBestUpdates",
            BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Missing RunFinalizer.BuildPendingPersonalBestUpdates.");
    object updates = method.Invoke(null, [settings, settings, statuses])
        ?? throw new InvalidOperationException("RunFinalizer returned null pending updates.");
    Type updateType = updates.GetType();
    bool hasUpdates = (bool)(updateType.GetProperty("HasUpdates")?.GetValue(updates)
        ?? throw new InvalidOperationException("Missing pending update HasUpdates property."));
    object segmentUpdates = updateType.GetProperty("SegmentUpdates")?.GetValue(updates)
        ?? throw new InvalidOperationException("Missing pending segment updates.");
    int segmentUpdateCount = ((System.Collections.ICollection)segmentUpdates).Count;
    object? timeUpdate = updateType.GetProperty("TimeUpdate")?.GetValue(updates);
    var timeUpdateSplits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (timeUpdate is not null)
    {
        object splits = timeUpdate.GetType().GetProperty("Splits")?.GetValue(timeUpdate)
            ?? throw new InvalidOperationException("Missing pending time update splits.");
        foreach (System.Collections.DictionaryEntry entry in (System.Collections.IDictionary)splits)
        {
            string key = entry.Key as string
                ?? throw new InvalidOperationException("Pending time update split key is not a string.");
            timeUpdateSplits[key] = entry.Value as string ?? string.Empty;
        }
    }

    return (
        hasUpdates,
        segmentUpdateCount,
        timeUpdate is not null,
        timeUpdateSplits);
}

static void TestSplitConditionTextParsesAllAndAtLeastSyntax()
{
    string text = "ALL(\n  Boss:skeletron,\n  ATLEAST(2, Boss:destroyer, Boss:twins, Boss:skeletron-prime),\n  Item:50 >= 2\n)";
    if (!SplitConditionText.TryParse(text, LanguageNames.Chinese, out SplitCondition condition, out string error))
    {
        throw new InvalidOperationException(error);
    }
    AssertEqual(SplitConditionKind.All, SplitConditionKind.Normalize(condition.Kind));
    AssertEqual(3, condition.Children.Count);
    AssertEqual(SplitConditionKind.AtLeast, SplitConditionKind.Normalize(condition.Children[1].Kind));
    AssertEqual(2, condition.Children[1].Value);
    AssertEqual(SplitCatalog.CreateItemEverOwnedFactKey(50), condition.Children[2].FactKey);
    AssertEqual(SplitFactComparison.AtLeast, SplitFactComparison.Normalize(condition.Children[2].Comparison));
    AssertEqual(2, condition.Children[2].Value);

    TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
    builder.SetBoolean(SplitCatalog.BossFacts.Single(boss => boss.TargetId == SplitCatalog.Skeletron).FactKey, true);
    builder.SetBoolean(SplitCatalog.BossFacts.Single(boss => boss.TargetId == SplitCatalog.Destroyer).FactKey, true);
    builder.SetBoolean(SplitCatalog.BossFacts.Single(boss => boss.TargetId == SplitCatalog.Twins).FactKey, false);
    builder.SetBoolean(SplitCatalog.BossFacts.Single(boss => boss.TargetId == SplitCatalog.SkeletronPrime).FactKey, true);
    builder.SetInteger(SplitCatalog.CreateItemEverOwnedFactKey(50), 2);
    AssertEqual(SplitConditionResult.True, condition.Evaluate(builder.Build()));

    string formatted = SplitConditionText.Format(condition, LanguageNames.Chinese);
    AssertEqual(true, formatted.Contains("Boss:skeletron", StringComparison.Ordinal));
    AssertEqual(true, formatted.Contains("Item:50 >= 2", StringComparison.Ordinal));
    AssertEqual(true, SplitConditionText.TryParse(formatted, LanguageNames.Chinese, out SplitCondition reparsed, out _));
    AssertEqual(condition.GetFactKeys().Count(), reparsed.GetFactKeys().Count());

    string emptyAtLeast = SplitConditionText.Format(SplitCondition.AtLeast([], 1), LanguageNames.Chinese);
    AssertEqual("ATLEAST(1)", emptyAtLeast);
    AssertEqual(true, SplitConditionText.TryParse(emptyAtLeast, LanguageNames.Chinese, out SplitCondition emptyReparsed, out _));
    AssertEqual(SplitConditionKind.AtLeast, SplitConditionKind.Normalize(emptyReparsed.Kind));
    AssertEqual(0, emptyReparsed.Children.Count);
    AssertEqual(1, emptyReparsed.Value);
}

static void TestSplitCatalogPreservesNestedSplitConditions()
{
    SplitCondition condition = SplitCondition.All(
    [
        SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
        SplitCondition.AtLeast(
        [
            SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer),
            SplitCatalog.CreateBossFactCondition(SplitCatalog.Twins),
            SplitCatalog.CreateBossFactCondition(SplitCatalog.SkeletronPrime)
        ], 2)
    ]);
    var settings = new AppSettings
    {
        Route =
        {
            SplitRoute =
            [
                new SplitRouteEntry
                {
                    Id = "split:nested",
                    DisplayName = "Nested",
                    Enabled = true,
                    Condition = condition,
                    IconTargetIds = [SplitCatalog.Skeletron, SplitCatalog.Destroyer, SplitCatalog.Twins, SplitCatalog.SkeletronPrime]
                }
            ]
        }
    };

    SplitDefinition definition = SplitCatalog.Build(settings).Single();

    AssertEqual(SplitConditionKind.All, SplitConditionKind.Normalize(definition.Condition.Kind));
    AssertEqual(SplitConditionKind.AtLeast, SplitConditionKind.Normalize(definition.Condition.Children[1].Kind));
    AssertEqual(2, definition.Condition.Children[1].Value);
}

static void TestSplitConditionDataRows()
{
    var settings = new AppSettings { Route = { SplitRoute = SplitCatalog.CreateDefaultRoute() } };
    SettingsNormalizer.Normalize(settings);

    IReadOnlyList<SplitConditionDataRow> rows = SplitConditionDataRows.Build(settings);

    AssertEqual(12, rows.Count);
    AssertEqual("split:item-857", rows[0].SplitId);
    AssertEqual(SplitCatalog.CreateItemEverOwnedFactKey(857), rows[0].Condition.FactKey);
    AssertEqual("split:item-857", rows[1].SplitId);
    AssertEqual(SplitCatalog.CreateItemEverOwnedFactKey(934), rows[1].Condition.FactKey);
    AssertEqual("split:boss-skeletron", rows[2].SplitId);
    AssertEqual("split:boss-wall-of-flesh", rows[3].SplitId);
    AssertEqual("split:item-525", rows[4].SplitId);
    AssertEqual(true, rows[4].IsAttached);
    AssertEqual("condition:split:item-525:complete", rows[4].Key);
    AssertEqual(1, rows.Count(row => string.Equals(row.SplitId, "split:item-525", StringComparison.OrdinalIgnoreCase)));
    AssertEqual("split:boss-destroyer", rows[5].SplitId);
    AssertEqual(SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer).FactKey, rows[5].Condition.FactKey);
    AssertEqual(SplitCatalog.CreateBossFactCondition(SplitCatalog.Twins).FactKey, rows[6].Condition.FactKey);
    AssertEqual(SplitCatalog.CreateBossFactCondition(SplitCatalog.SkeletronPrime).FactKey, rows[7].Condition.FactKey);
    AssertEqual(false, rows.Any(row => string.Equals(row.Key, row.SplitId, StringComparison.OrdinalIgnoreCase)));

    SplitDefinition attachedDefinition = SplitCatalog.Build(settings)
        .Single(definition => string.Equals(definition.Id, "split:item-525", StringComparison.OrdinalIgnoreCase));
    var attachedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [rows[4].Key] = "0:22.00"
    };
    AssertEqual(true, SplitConditionDataRows.TryGetSplitTime(settings, attachedValues, attachedDefinition, out TimeSpan attachedTime));
    AssertEqual(TimeSpan.FromSeconds(22), attachedTime);
}

static void TestSplitConditionDataRowsCalculatesNestedReferenceCompletionTime()
{
    SplitCondition skeletron = SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron);
    SplitCondition destroyer = SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer);
    SplitCondition twins = SplitCatalog.CreateBossFactCondition(SplitCatalog.Twins);
    SplitCondition prime = SplitCatalog.CreateBossFactCondition(SplitCatalog.SkeletronPrime);
    SplitCondition condition = SplitCondition.All(
    [
        skeletron,
        SplitCondition.AtLeast([destroyer, twins, prime], 2)
    ]);
    var settings = new AppSettings
    {
        Route =
        {
            SplitRoute =
            [
                new SplitRouteEntry
                {
                    Id = "split:nested",
                    DisplayName = "Nested",
                    Enabled = true,
                    Condition = condition,
                    IconTargetIds = [SplitCatalog.Skeletron, SplitCatalog.Destroyer, SplitCatalog.Twins, SplitCatalog.SkeletronPrime]
                }
            ]
        }
    };
    IReadOnlyList<SplitConditionDataRow> rows = SplitConditionDataRows.Build(settings);
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (SplitConditionDataRow row in rows)
    {
        values[row.Key] = row.Condition.FactKey switch
        {
            var key when string.Equals(key, skeletron.FactKey, StringComparison.OrdinalIgnoreCase) => "0:10.00",
            var key when string.Equals(key, destroyer.FactKey, StringComparison.OrdinalIgnoreCase) => "0:20.00",
            var key when string.Equals(key, twins.FactKey, StringComparison.OrdinalIgnoreCase) => "0:30.00",
            var key when string.Equals(key, prime.FactKey, StringComparison.OrdinalIgnoreCase) => "0:40.00",
            _ => string.Empty
        };
    }

    var definition = new SplitDefinition("split:nested", "Nested", condition, [], [], []);

    AssertEqual(true, SplitConditionDataRows.TryGetSplitTime(settings, values, definition, out TimeSpan split));
    AssertEqual(TimeSpan.FromSeconds(30), split);
}

static void TestSplitConditionDataRowsAggregatesAtLeastTimes()
{
    var settings = new AppSettings
    {
        Route =
        {
            SplitRoute =
            [
                new SplitRouteEntry
                {
                    Id = "split:all",
                    DisplayName = "All",
                    Enabled = true,
                    Condition = SplitCondition.All(
                    [
                        SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                        SplitCatalog.CreateBossFactCondition(SplitCatalog.WallOfFlesh)
                    ]),
                    IconTargetIds = [SplitCatalog.Skeletron, SplitCatalog.WallOfFlesh]
                },
                new SplitRouteEntry
                {
                    Id = "split:any",
                    DisplayName = "Any",
                    Enabled = true,
                    Condition = SplitCondition.Any(
                    [
                        SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer),
                        SplitCatalog.CreateBossFactCondition(SplitCatalog.Twins)
                    ]),
                    IconTargetIds = [SplitCatalog.Destroyer, SplitCatalog.Twins]
                },
                new SplitRouteEntry
                {
                    Id = "split:at-least",
                    DisplayName = "AtLeast",
                    Enabled = true,
                    Condition = SplitCondition.AtLeast(
                    [
                        SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                        SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer),
                        SplitCatalog.CreateBossFactCondition(SplitCatalog.Twins)
                    ], 2),
                    IconTargetIds = [SplitCatalog.Skeletron, SplitCatalog.Destroyer, SplitCatalog.Twins]
                }
            ]
        }
    };
    SettingsNormalizer.Normalize(settings);
    IReadOnlyList<SplitConditionDataRow> rows = SplitConditionDataRows.Build(settings);
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [CumulativeKey(rows, "split:all", SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron).FactKey)] = "0:10.00",
        [CumulativeKey(rows, "split:all", SplitCatalog.CreateBossFactCondition(SplitCatalog.WallOfFlesh).FactKey)] = "0:12.00",
        [CumulativeKey(rows, "split:any", SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer).FactKey)] = "0:30.00",
        [CumulativeKey(rows, "split:any", SplitCatalog.CreateBossFactCondition(SplitCatalog.Twins).FactKey)] = "0:25.00",
        [CumulativeKey(rows, "split:at-least", SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron).FactKey)] = "0:14.00",
        [CumulativeKey(rows, "split:at-least", SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer).FactKey)] = "0:18.00",
        [CumulativeKey(rows, "split:at-least", SplitCatalog.CreateBossFactCondition(SplitCatalog.Twins).FactKey)] = "0:16.00"
    };

    SplitDefinition all = SplitCatalog.Build(settings).Single(definition => definition.Id == "split:all");
    SplitDefinition any = SplitCatalog.Build(settings).Single(definition => definition.Id == "split:any");
    SplitDefinition atLeast = SplitCatalog.Build(settings).Single(definition => definition.Id == "split:at-least");

    AssertEqual(true, SplitConditionDataRows.TryGetSplitTime(settings, values, all, out TimeSpan allTime));
    AssertEqual(TimeSpan.FromSeconds(12), allTime);
    AssertEqual(true, SplitConditionDataRows.TryGetSplitTime(settings, values, any, out TimeSpan anyTime));
    AssertEqual(TimeSpan.FromSeconds(25), anyTime);
    AssertEqual(true, SplitConditionDataRows.TryGetSplitTime(settings, values, atLeast, out TimeSpan atLeastTime));
    AssertEqual(TimeSpan.FromSeconds(16), atLeastTime);
}

static void TestSplitCatalogReferenceTargetIcons()
{
    AssertEqual(true, SplitCatalog.TryGetReferenceIconFileName(SplitCatalog.CreateItemTargetId(50), out string itemIcon));
    AssertEqual("Item_50.png", itemIcon);
    AssertEqual(true, File.Exists(Path.Combine("TerrariaSplit", "Assets", "Icons", "Items", itemIcon)));

    AssertEqual(true, SplitCatalog.TryGetReferenceIconFileName("boss:king-slime", out string bossIcon));
    AssertEqual("king-slime.png", bossIcon);
    AssertEqual(true, File.Exists(Path.Combine("TerrariaSplit", "Assets", "Icons", "Bosses", bossIcon)));

    foreach (BossFactDescriptor boss in SplitCatalog.BossFacts)
    {
        AssertEqual(true, File.Exists(Path.Combine("TerrariaSplit", "Assets", "Icons", "Bosses", boss.IconFileName)));
    }

    AssertEqual(true, SplitCatalog.TryGetReferenceIconFileName(SplitCatalog.CreateNpcTargetId(17), out string npcIcon));
    AssertEqual("NPC_Head_2.png", npcIcon);
    AssertEqual(true, File.Exists(Path.Combine("TerrariaSplit", "Assets", "Icons", "NPCs", npcIcon)));

    AssertEqual(true, SplitCatalog.TryGetReferenceIconFileName(SplitCatalog.CreateBiomeTargetId("aether"), out string biomeIcon));
    AssertEqual("biome-aether.png", biomeIcon);
    AssertEqual(true, File.Exists(Path.Combine("TerrariaSplit", "Assets", "Icons", "Biomes", biomeIcon)));
}

static void TestSplitCatalogBuildsSplitIconOverrides()
{
    var settings = new AppSettings
    {
        Route =
        {
            SplitRoute =
            [
                new SplitRouteEntry
                {
                    Id = "split:any-boss",
                    DisplayName = "Any Boss",
                    Enabled = true,
                    Condition = SplitCondition.Any(
                    [
                        SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                        SplitCatalog.CreateBossFactCondition(SplitCatalog.WallOfFlesh)
                    ]),
                    IconTargetIds = [SplitCatalog.Skeletron, SplitCatalog.WallOfFlesh],
                    IconOverride = new SplitIconOverride
                    {
                        Source = SplitIconOverrideSource.Target,
                        TargetId = SplitCatalog.WallOfFlesh
                    }
                }
            ]
        }
    };

    SettingsNormalizer.Normalize(settings);
    SplitDefinition definition = SplitCatalog.Build(settings).Single();

    AssertEqual(1, definition.IconFileNames.Count);
    AssertEqual("wof.png", definition.IconFileNames.Single());
    AssertEqual(1, definition.IconKeys.Count);
    AssertEqual(SplitCatalog.WallOfFlesh, definition.IconKeys.Single());
    AssertEqual(2, definition.TargetIds.Count);
    AssertEqual(1, definition.IconLightingConditions.Count);
    AssertEqual(SplitConditionKind.AtLeast, definition.IconLightingConditions.Single().Kind);
    AssertEqual(1, definition.IconLightingConditions.Single().Value);

    var completedWithOtherTarget = new SplitStatusSnapshot(
        definition,
        TimeSpan.FromSeconds(10),
        IsSkipped: false,
        CompletedFactKeys: [SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron).FactKey]);
    SplitDefinition display = SplitRenderData.GetDisplayDefinition(completedWithOtherTarget);

    AssertEqual(1, display.IconKeys.Count);
    AssertEqual(SplitCatalog.WallOfFlesh, display.IconKeys.Single());
}

static void TestStatisticsTableExpandsConditionRowsAndKeepsRouteSegments()
{
    SplitCondition wallOfFlesh = SplitCatalog.CreateBossFactCondition(SplitCatalog.WallOfFlesh);
    SplitCondition destroyer = SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer);
    var settings = new AppSettings
    {
        Route =
        {
            SplitRoute =
            [
                CreateTestRouteEntry("split:a", "A", SplitCatalog.Skeletron),
                new SplitRouteEntry
                {
                    Id = "split:attached",
                    DisplayName = "Attached",
                    Enabled = true,
                    IsAttached = true,
                    Condition = SplitCondition.AtLeast(
                    [
                        SplitCatalog.CreateItemEverOwnedCondition(50, 1),
                        SplitCatalog.CreateItemEverOwnedCondition(51, 1)
                    ], 1),
                    IconTargetIds = [SplitCatalog.CreateItemTargetId(50), SplitCatalog.CreateItemTargetId(51)]
                },
                new SplitRouteEntry
                {
                    Id = "split:b",
                    DisplayName = "B",
                    Enabled = true,
                    Condition = SplitCondition.All([wallOfFlesh, destroyer]),
                    IconTargetIds = [SplitCatalog.WallOfFlesh, SplitCatalog.Destroyer]
                },
                CreateTestRouteEntry("split:c", "C", SplitCatalog.MoonLord)
            ]
        },
        Comparison =
        {
            PersonalBestSegmentTimes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["split:a"] = "0:09.00",
                ["split:b"] = "0:05.00",
                ["split:c"] = "0:05.00"
            }
        }
    };
    IReadOnlyList<SplitConditionDataRow> conditionRows = SplitConditionDataRows.Build(settings);
    string aCumulativeKey = CumulativeKey(conditionRows, "split:a", SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron).FactKey);
    string attachedCumulativeKey = SingleCumulativeKey(settings, "split:attached");
    string bWallOfFleshKey = CumulativeKey(conditionRows, "split:b", wallOfFlesh.FactKey);
    string bDestroyerKey = CumulativeKey(conditionRows, "split:b", destroyer.FactKey);
    string cCumulativeKey = CumulativeKey(conditionRows, "split:c", SplitCatalog.CreateBossFactCondition(SplitCatalog.MoonLord).FactKey);
    settings.Comparison.PersonalBestTimes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [aCumulativeKey] = "0:09.00",
        [attachedCumulativeKey] = "0:10.00",
        [bWallOfFleshKey] = "0:11.00",
        [bDestroyerKey] = "0:14.00",
        [cCumulativeKey] = "0:19.00"
    };
    var reference = new ReferenceSplitSet
    {
        Name = "Test",
        Splits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [aCumulativeKey] = "0:10.00",
            [attachedCumulativeKey] = "0:11.00",
            [bWallOfFleshKey] = "0:12.00",
            [bDestroyerKey] = "0:15.00",
            [cCumulativeKey] = "0:20.00"
        }
    };
    var personalSplits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [aCumulativeKey] = "0:09.50",
        [attachedCumulativeKey] = "0:10.50",
        [bWallOfFleshKey] = "0:11.50",
        [bDestroyerKey] = "0:14.50",
        [cCumulativeKey] = "0:19.50"
    };

    List<StatisticsTableRow> rows = StatisticsTableBuilder.Build(settings, reference, personalSplits);

    AssertEqual(5, rows.Count);
    AssertEqual(aCumulativeKey, rows[0].ConditionRow.Key);
    AssertEqual(attachedCumulativeKey, rows[1].ConditionRow.Key);
    AssertEqual(bWallOfFleshKey, rows[2].ConditionRow.Key);
    AssertEqual(bDestroyerKey, rows[3].ConditionRow.Key);
    AssertEqual(cCumulativeKey, rows[4].ConditionRow.Key);
    AssertEqual("0:10.00", rows[0].ReferenceTimeText);
    AssertEqual("0:11.00", rows[1].ReferenceTimeText);
    AssertEqual("0:12.00", rows[2].ReferenceTimeText);
    AssertEqual("0:15.00", rows[3].ReferenceTimeText);
    AssertEqual("0:10.00", rows[0].ReferenceSegmentText);
    AssertEqual("--", rows[1].ReferenceSegmentText);
    AssertEqual("0:05.00", rows[2].ReferenceSegmentText);
    AssertEqual("0:05.00", rows[3].ReferenceSegmentText);
    AssertEqual("0:05.00", rows[4].ReferenceSegmentText);
    AssertEqual("0:09.50", rows[0].PersonalSegmentText);
    AssertEqual("--", rows[1].PersonalSegmentText);
    AssertEqual("0:05.00", rows[2].PersonalSegmentText);
    AssertEqual("0:10.00", rows[1].PersonalBestText);
    AssertEqual("--", rows[1].PersonalBestSegmentText);
    AssertEqual("0:09.00", rows[0].PersonalBestSegmentText);
    AssertEqual("0:05.00", rows[2].PersonalBestSegmentText);
    AssertEqual("0:05.00", rows[3].PersonalBestSegmentText);
    AssertEqual("0:05.00", rows[4].PersonalBestSegmentText);
    AssertEqual(1, rows[0].GroupRowCount);
    AssertEqual(1, rows[1].GroupRowCount);
    AssertEqual(2, rows[2].GroupRowCount);
    AssertEqual(0, rows[2].GroupOffset);
    AssertEqual(1, rows[3].GroupOffset);
}

static SplitRouteEntry CreateTestRouteEntry(string id, string displayName, string bossTargetId)
{
    return new SplitRouteEntry
    {
        Id = id,
        DisplayName = displayName,
        Enabled = true,
        Condition = SplitCatalog.CreateBossFactCondition(bossTargetId),
        IconTargetIds = [bossTargetId]
    };
}

static string SingleCumulativeKey(AppSettings settings, string splitId)
{
    return SplitConditionDataRows.Build(settings)
        .Single(row => string.Equals(row.SplitId, splitId, StringComparison.OrdinalIgnoreCase))
        .Key;
}

static string CumulativeKey(IReadOnlyList<SplitConditionDataRow> rows, string splitId, string factKey)
{
    return rows.Single(row =>
        string.Equals(row.SplitId, splitId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(row.Condition.FactKey, factKey, StringComparison.OrdinalIgnoreCase)).Key;
}

static void TestTerrariaMenuGeometry()
{
    TerrariaMenuGeometry geometry = TerrariaMenuGeometry.From(new Size(900, 900));
    AssertEqual(new Point(450, 245), geometry.MainMenuSinglePlayer());
    AssertEqual(new Point(282, 830), geometry.SelectMenuBackButton());
    AssertEqual(new Point(580, 534), geometry.CreatePlayerButton());
    AssertEqual(new Point(320, 534), geometry.CreateWorldBackButton());
    AssertEqual(new Point(450, 230), geometry.AdvancedSeedTextButton());
    AssertEqual(new Point(342, 287), geometry.AdvancedSpecialSeedButton(AutoCreateSpecialWorldSeed.NotTheBees));
}

static void TestLocalizer()
{
    AssertEqual("Crimson", Localizer.Get("Crimson", new AppSettings { General = { Language = "English" } }));
    AssertEqual("\u7329\u7EA2", Localizer.Get("Crimson", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u7D2F\u79EF", Localizer.Get("Cumulative", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u5206\u6BB5", Localizer.Get("Segment", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u4E0D\u900F\u660E\u5EA6 %", Localizer.Get("Opacity %", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u542F\u7528", Localizer.Get("Enabled", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u7B49\u5F85\u9644\u52A0\u5185\u5B58", Localizer.Get("Waiting for attached memory", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u7B49\u5F85\u8BA1\u65F6\u5F00\u59CB", Localizer.Get("Waiting for timer start", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u5355\u6BB5\u65F6\u95F4", Localizer.Get("Segment time", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u7D2F\u79EF\u65F6\u95F4", Localizer.Get("Cumulative time", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u53C2\u8003\u65F6\u95F4\uFF08\u672A\u6765\u9636\u6BB5\uFF09", Localizer.Get("Reference time (future stage)", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u53C2\u8003\u65F6\u95F4\uFF08\u5F53\u524D\u9636\u6BB5\uFF09", Localizer.Get("Reference time (current stage)", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u7D2F\u79EF\u65F6\u95F4\uFF08\u5DF2\u5B8C\u6210\u9636\u6BB5\uFF09", Localizer.Get("Cumulative time (completed stage)", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u4E3B\u8BA1\u65F6\u5668\uFF08\u603B\u6210\u7EE9\u5FEB\uFF09", Localizer.Get("Main timer (total fast)", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u5355\u6BB5\u65F6\u95F4\u63D0\u793A\u6587\u672C", Localizer.Get("Segment time hint text", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u7D2F\u79EF\u65F6\u95F4\u63D0\u793A\u6587\u672C", Localizer.Get("Cumulative time hint text", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u5206\u6BB5\u70B9\uFF1A\u7D2F\u79EF\u65F6\u95F4\u5FEB\u4E8E\u53C2\u8003\uFF0C\u5355\u6BB5\u65F6\u95F4\u5FEB\u4E8E PB", Localizer.Get("Stage reached: cumulative faster, segment faster", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u4EBA\u7269\u9009\u9879", Localizer.Get("Player options", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u4E16\u754C\u9009\u9879", Localizer.Get("World options", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u5929\u9876\u63A5\u661F", Localizer.Get("Zenith star catch", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u7B5B\u9009\u91D1\u5B57\u5854", Localizer.Get("Pyramid filter", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u6307\u5B9A\u7269\u54C1", Localizer.Get("Required pyramid items", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u7B5B\u9009\u5931\u8D25\u8FD4\u56DE\u4E3B\u9875\u91CD\u65B0\u521B\u5EFA", Localizer.Get("Return to main menu on filter failure", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u6C99\u66B4\u74F6", Localizer.Get("Sandstorm in a Bottle", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u98DE\u6BEF", Localizer.Get("Flying Carpet", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u6CD5\u8001\u5957", Localizer.Get("Pharaoh set", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u540E\u53F0\u5EFA\u56FE", Localizer.Get("Background world generation", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u540E\u53F0\u9884\u5EFA\u4E16\u754C\u6C60", Localizer.Get("Background world pool", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u4E16\u754C\u6C60\u4E2A\u6570", Localizer.Get("World pool size", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u5B89\u88C5\u4E16\u754C\u6C60\u4E16\u754C", Localizer.Get("Install pooled world", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u505C\u5728\u4E16\u754C\u9009\u62E9\u754C\u9762", Localizer.Get("Stop at world select", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u53C2\u8003\u65F6\u95F4", Localizer.Get("Reference Data", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u4F7F\u7528 PB \u4F5C\u4E3A\u53C2\u8003\u65F6\u95F4", Localizer.Get("Use PB as reference time", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u5F53\u524D\u53C2\u8003\u7EC4", Localizer.Get("Active group", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u4E2A\u4EBA\u6700\u4F73\u66F4\u65B0", Localizer.Get("Personal Data", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u4E2A\u4EBA\u6700\u4F73\u7D2F\u8BA1", Localizer.Get("Personal Cumulative Best", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u4E2A\u4EBA\u6700\u4F73\u5355\u6BB5", Localizer.Get("Personal segment best", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u5F53\u524D\u6570\u636E\u6587\u4EF6", Localizer.Get("Active file", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("m:ss \u6216 h:mm:ss", Localizer.Get("m:ss or h:mm:ss", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u975E\u9644\u5C5E\u7EC4", Localizer.Get("Main groups", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u9644\u5C5E\u7EC4", Localizer.Get("Attached groups", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u9644\u5C5E\u7EC4", Localizer.Get("Attached group marker", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u81EA\u52A8\u9690\u85CF\u9644\u5C5E\u7EC4", Localizer.Get("Auto hide attached groups", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u9644\u5C5E\u7EC4\u53C2\u4E0E\u4E3B\u8BA1\u65F6\u5668\u5FEB\u6162\u5224\u5B9A", Localizer.Get("Attached groups affect main timer comparison", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u5B8C\u6210\u5F53\u524D\u9636\u6BB5\u65F6\u70B9\u4EAE\u56FE\u6807", Localizer.Get("Light icons when current stage completed", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u4E3B\u9636\u6BB5\u5B8C\u6210\u52A8\u753B", Localizer.Get("Main stage completion animation", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u7EC4", Localizer.Get("BOSS Group", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u5F53\u524D\u9636\u6BB5\u56FE\u6807\u7070\u5EA6\u989D\u5916\u524A\u5F31 %", Localizer.Get("Current stage icon grayscale weaken %", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
    AssertEqual("\u5F53\u524D\u9636\u6BB5\u56FE\u6807\u4EAE\u5EA6\u989D\u5916\u589E\u5F3A %", Localizer.Get("Current stage icon brightness boost %", new AppSettings { General = { Language = "\u4E2D\u6587" } }));
}

static void TestJsonFileStoreWritesAtomically()
{
    string directory = GetPublishOutputDirectory("test-output", "json-store-tests");
    string settingsPath = Path.Combine(directory, "settings.json");
    string activeProfilePath = Path.Combine(directory, "active-settings.txt");

    try
    {
        var settings = new AppSettings { General = { Language = "\u4E2D\u6587" } };
        AssertEqual(true, JsonFileStore.Write(settingsPath, settings, "test settings"));

        AppSettings? loaded = JsonFileStore.Read<AppSettings>(settingsPath, "test settings");
        AssertEqual("\u4E2D\u6587", loaded?.General.Language);

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
    using JsonDocument document = JsonDocument.Parse(AppSettingsDefaults.TemplateJson);

    AssertJsonCoversType(typeof(AppSettings), document.RootElement, "settings");
}

static void TestLegacyFlatSettingsJsonMigratesToSections()
{
    const string json = """
    {
      "Language": "English",
      "AlwaysOnTop": true,
      "PauseResumeKey": "F10",
      "SplitRoute": [],
      "UsePersonalBestAsReferenceTime": true,
      "ShowSplitCompletionAnimation": false,
      "AutoCreate": {
        "EnablePyramidFilter": true
      }
    }
    """;

    AppSettings settings = SettingsSerializer.ReadSettingsFromJson(json, "legacy flat settings")
        ?? throw new InvalidOperationException("Legacy flat settings could not be read.");

    AssertEqual("English", settings.General.Language);
    AssertEqual(true, settings.General.AlwaysOnTop);
    AssertEqual("F10", settings.Hotkeys.PauseResumeKey);
    AssertEqual(0, settings.Route.SplitRoute.Count);
    AssertEqual(true, settings.Comparison.UsePersonalBestAsReferenceTime);
    AssertEqual(false, settings.Overlay.ShowSplitCompletionAnimation);
    AssertEqual(true, settings.Automation.AutoCreate.EnablePyramidFilter);

    string persisted = JsonSerializer.Serialize(settings, JsonFileStore.JsonOptions);
    using JsonDocument document = JsonDocument.Parse(persisted);
    JsonElement root = document.RootElement;

    AssertEqual(true, root.TryGetProperty(nameof(AppSettings.General), out _));
    AssertEqual(true, root.TryGetProperty(nameof(AppSettings.Hotkeys), out _));
    AssertEqual(true, root.TryGetProperty(nameof(AppSettings.Route), out _));
    AssertEqual(true, root.TryGetProperty(nameof(AppSettings.Comparison), out _));
    AssertEqual(true, root.TryGetProperty(nameof(AppSettings.Overlay), out _));
    AssertEqual(true, root.TryGetProperty(nameof(AppSettings.Automation), out _));
    AssertEqual(false, root.TryGetProperty("Language", out _));
    AssertEqual(false, root.TryGetProperty("PauseResumeKey", out _));
    AssertEqual(false, root.TryGetProperty("SplitRoute", out _));
    AssertEqual(false, root.TryGetProperty("AutoCreate", out _));
}

static void TestDefaultAttachedSplitDisplayMatchesPrimaryDisplay()
{
    AppSettings settings = AppSettingsDefaults.Create();

    AssertColumnMatches(settings.Overlay.Columns.Icon, settings.Overlay.Columns.AttachedIcon);
    AssertColumnMatches(settings.Overlay.Columns.Time, settings.Overlay.Columns.AttachedTime);
    AssertColumnMatches(settings.Overlay.Columns.Delta, settings.Overlay.Columns.AttachedDelta);
}

static void AssertColumnMatches(UiColumnSettings expected, UiColumnSettings actual)
{
    AssertEqual(expected.Show, actual.Show);
    AssertEqual(expected.Width, actual.Width);
    AssertEqual(expected.FontFamily, actual.FontFamily);
    AssertEqual(expected.FontSize, actual.FontSize);
    AssertEqual(expected.Bold, actual.Bold);
}

static void TestDefaultReferenceTimesMatchDefaultSettingsRoute()
{
    AppSettings settings = AppSettingsDefaults.Create();
    SettingsNormalizer.Normalize(settings);
    HashSet<string> routeKeys = SplitConditionDataRows.Build(settings)
        .Select(row => row.Key)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    ReferenceSplitSet referenceSet = System.Text.Json.JsonSerializer.Deserialize<ReferenceSplitSet>(
        EmbeddedDefaults.ReferenceTimesWrJson,
        JsonFileStore.JsonOptions)
        ?? throw new InvalidOperationException("Default WR reference times could not be read.");

    foreach (string key in referenceSet.Splits.Keys)
    {
        if (!routeKeys.Contains(key))
        {
            throw new InvalidOperationException($"Default WR reference key is not in the default route: {key}");
        }
    }

    AssertEqual("0:51.00", referenceSet.Splits["condition:split:item-857:complete"]);
    AssertEqual("6:50.00", referenceSet.Splits["condition:split:item-167:complete"]);
}

static void TestAppSettingsStoreWritesEmbeddedDefaultsWhenSettingsFileIsInvalid()
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

        Directory.CreateDirectory(settingsDirectory);
        string settingsPath = Path.Combine(settingsDirectory, "settings.json");
        File.WriteAllText(settingsPath, "{ invalid json");

        AppSettings loaded = AppSettingsStore.Load("settings.json");
        AppSettings defaults = AppSettingsDefaults.Create();

        AssertEqual(defaults.General.Language, loaded.General.Language);
        AssertEqual(defaults.Route.SplitRoute.Count, loaded.Route.SplitRoute.Count);
        AssertEqual(true, File.Exists(settingsPath));

        AppSettings? saved = SettingsSerializer.ReadSettings(settingsPath, "saved settings");
        AssertEqual(defaults.General.Language, saved?.General.Language);
        AssertEqual(defaults.Route.SplitRoute.Count, saved?.Route.SplitRoute.Count);
    }
    finally
    {
        RestoreDirectory(settingsDirectory, settingsSnapshot);
        RestoreDirectory(referenceDirectory, referenceSnapshot);
        RestoreDirectory(personalBestTimeDirectory, personalBestTimeSnapshot);
        RestoreDirectory(personalBestSegmentDirectory, personalBestSegmentSnapshot);
    }
}

static void TestSplitTimeSetStoreWritesEmbeddedWrWhenReferenceFilesAreInvalid()
{
    string referenceDirectory = SplitTimeSetStore.ReferenceDirectory;
    DirectorySnapshot referenceSnapshot = SnapshotDirectory(referenceDirectory);

    try
    {
        DeleteDirectoryIfExists(referenceDirectory);
        Directory.CreateDirectory(referenceDirectory);
        File.WriteAllText(Path.Combine(referenceDirectory, "WR.json"), "{ invalid json");

        List<ReferenceSplitSet> sets = SplitTimeSetStore.LoadReferenceSets();
        ReferenceSplitSet embeddedWr = System.Text.Json.JsonSerializer.Deserialize<ReferenceSplitSet>(
            EmbeddedDefaults.ReferenceTimesWrJson,
            JsonFileStore.JsonOptions)
            ?? throw new InvalidOperationException("Embedded WR reference times could not be read.");

        AssertEqual(1, sets.Count);
        AssertEqual("WR", sets[0].Name);
        AssertEqual(embeddedWr.Splits.Count, sets[0].Splits.Count);
        AssertEqual(true, File.Exists(Path.Combine(referenceDirectory, "WR.json")));

        ReferenceSplitSet saved = JsonFileStore.Read<ReferenceSplitSet>(
            Path.Combine(referenceDirectory, "WR.json"),
            "saved WR reference times")
            ?? throw new InvalidOperationException("Saved WR reference times could not be read.");
        AssertEqual(embeddedWr.Splits.Count, saved.Splits.Count);
    }
    finally
    {
        RestoreDirectory(referenceDirectory, referenceSnapshot);
    }
}

static void TestAppSettingsStoreSaveDoesNotMutateSourceSettings()
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

        string profileName = "save-mutation-test.json";
        _ = AppSettingsStore.Load(profileName);

        AppSettings settings = AppSettingsDefaults.Create();
        SettingsNormalizer.Normalize(settings);
        settings.Comparison.ReferenceSplitSets =
        [
            new ReferenceSplitSet
            {
                Name = "Custom Reference",
                Splits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["condition:test-reference"] = "00:01"
                }
            }
        ];
        settings.Comparison.PersonalBestTimeSets =
        [
            new ReferenceSplitSet
            {
                Name = "Race PB",
                Splits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["condition:test-pb"] = "00:02"
                }
            }
        ];
        settings.Comparison.PersonalBestSegmentSets =
        [
            new ReferenceSplitSet
            {
                Name = "Race Segments",
                Splits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["split:test-segment"] = "00:03"
                }
            }
        ];
        settings.Comparison.ActiveReferenceSplitSet = "Custom Reference";
        settings.Comparison.ActivePersonalBestTimeSet = "Race PB";
        settings.Comparison.ActivePersonalBestSegmentSet = "Race Segments";

        List<ReferenceSplitSet> referenceSets = settings.Comparison.ReferenceSplitSets;
        List<ReferenceSplitSet> personalBestTimeSets = settings.Comparison.PersonalBestTimeSets;
        List<ReferenceSplitSet> personalBestSegmentSets = settings.Comparison.PersonalBestSegmentSets;
        string beforeJson = JsonSerializer.Serialize(settings, JsonFileStore.JsonOptions);

        AppSettingsStore.Save(settings);

        string afterJson = JsonSerializer.Serialize(settings, JsonFileStore.JsonOptions);
        AssertEqual(beforeJson, afterJson);
        AssertEqual(true, ReferenceEquals(referenceSets, settings.Comparison.ReferenceSplitSets));
        AssertEqual(true, ReferenceEquals(personalBestTimeSets, settings.Comparison.PersonalBestTimeSets));
        AssertEqual(true, ReferenceEquals(personalBestSegmentSets, settings.Comparison.PersonalBestSegmentSets));

        AppSettings persisted = SettingsSerializer.ReadSettings(
            Path.Combine(settingsDirectory, profileName),
            "saved mutation test settings")
            ?? throw new InvalidOperationException("Saved mutation test settings could not be read.");
        AssertEqual(0, persisted.Comparison.ReferenceSplitSets.Count);
        AssertEqual(0, persisted.Comparison.PersonalBestTimeSets.Count);
        AssertEqual(0, persisted.Comparison.PersonalBestSegmentSets.Count);
    }
    finally
    {
        RestoreDirectory(settingsDirectory, settingsSnapshot);
        RestoreDirectory(referenceDirectory, referenceSnapshot);
        RestoreDirectory(personalBestTimeDirectory, personalBestTimeSnapshot);
        RestoreDirectory(personalBestSegmentDirectory, personalBestSegmentSnapshot);
    }
}

static void TestRuntimeDataPathsUseFinalDirectoryLayout()
{
    string dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");

    AssertEqual(dataDirectory, RuntimeDataPaths.DataDirectory);
    AssertEqual(Path.Combine(AppContext.BaseDirectory, "Settings"), AppSettingsStore.SettingsDirectory);
    AssertEqual(Path.Combine(dataDirectory, "reference-times"), SplitTimeSetStore.ReferenceDirectory);
    AssertEqual(Path.Combine(dataDirectory, "last-times"), SplitTimeSetStore.LastRunDirectory);
    AssertEqual(Path.Combine(dataDirectory, "personal-best-times"), SplitTimeSetStore.PersonalBestTimeDirectory);
    AssertEqual(Path.Combine(dataDirectory, "personal-best-segments"), SplitTimeSetStore.PersonalBestSegmentDirectory);
    AssertEqual(Path.Combine(AppContext.BaseDirectory, "Worlds"), RuntimeDataPaths.WorldPoolDirectory);
    AssertEqual(Path.Combine(AppContext.BaseDirectory, "Worlds", "scratch"), RuntimeDataPaths.WorldPoolScratchDirectory);

    SplitTimeSetStore.EnsureDirectories();
    AssertEqual(true, Directory.Exists(SplitTimeSetStore.ReferenceDirectory));
    AssertEqual(true, Directory.Exists(SplitTimeSetStore.LastRunDirectory));
    AssertEqual(true, Directory.Exists(SplitTimeSetStore.PersonalBestTimeDirectory));
    AssertEqual(true, Directory.Exists(SplitTimeSetStore.PersonalBestSegmentDirectory));
}

static void TestOperationResultPreservesFailureDetail()
{
    var exception = new IOException("disk is full");
    OperationResult result = OperationResult.Failure("Could not save settings.", exception);

    AssertEqual(false, result.Succeeded);
    AssertEqual(true, result.Failed);
    AssertEqual("Could not save settings.", result.UserMessage);
    AssertEqual("Could not save settings.", result.Message);
    AssertEqual(true, ReferenceEquals(exception, result.Exception));
    AssertEqual(string.Empty, OperationResult.Success().Message);
}

static void TestAppLoggerIsDisabledByDefault()
{
    string? previous = Environment.GetEnvironmentVariable(AppLogger.EnableLogEnvironmentVariable);
    try
    {
        Environment.SetEnvironmentVariable(AppLogger.EnableLogEnvironmentVariable, null);
        AssertEqual(false, AppLogger.IsEnabled);
    }
    finally
    {
        Environment.SetEnvironmentVariable(AppLogger.EnableLogEnvironmentVariable, previous);
    }
}

static void TestMainPublishIsSingleFile()
{
    string sourceRoot = FindSourceRoot();
    XDocument project = XDocument.Load(Path.Combine(sourceRoot, "TerrariaSplit", "TerrariaSplit.csproj"));
    XElement releaseProperties = project
        .Descendants()
        .Single(element =>
            element.Name.LocalName == "PropertyGroup" &&
            string.Equals(
                element.Attribute("Condition")?.Value,
                "'$(Configuration)' == 'Release'",
                StringComparison.Ordinal));

    AssertEqual("win-x64", GetRequiredElementValue(releaseProperties, "RuntimeIdentifier"));
    AssertEqual("true", GetRequiredElementValue(releaseProperties, "SelfContained"));
    AssertEqual("true", GetRequiredElementValue(releaseProperties, "PublishSelfContained"));
    AssertEqual("true", GetRequiredElementValue(releaseProperties, "PublishSingleFile"));
    AssertEqual("true", GetRequiredElementValue(releaseProperties, "IncludeNativeLibrariesForSelfExtract"));
    AssertEqual("false", GetRequiredElementValue(releaseProperties, "PublishTrimmed"));
    AssertEqual("none", GetRequiredElementValue(releaseProperties, "DebugType"));
    AssertEqual("false", GetRequiredElementValue(releaseProperties, "DebugSymbols"));
}

static void TestMemoryProbePublishIsSelfContained()
{
    string sourceRoot = FindSourceRoot();
    XDocument targets = XDocument.Load(Path.Combine(sourceRoot, "TerrariaSplit", "Build", "MemoryProbe.targets"));
    string commonProperties = targets
        .Descendants()
        .Single(element => element.Name.LocalName == "TerrariaSplitMemoryProbeCommonProperties")
        .Value;
    string publishProperties = targets
        .Descendants()
        .Single(element => element.Name.LocalName == "TerrariaSplitMemoryProbePublishProperties")
        .Value;

    AssertEqual(true, commonProperties.Contains("RuntimeIdentifier=$(TerrariaSplitMemoryProbeRuntimeIdentifier)", StringComparison.Ordinal));
    AssertEqual(true, commonProperties.Contains("PlatformTarget=$(TerrariaSplitMemoryProbePlatformTarget)", StringComparison.Ordinal));
    AssertEqual(true, publishProperties.Contains("$(TerrariaSplitMemoryProbeCommonProperties)", StringComparison.Ordinal));
    AssertEqual(true, publishProperties.Contains("SelfContained=true", StringComparison.Ordinal));
    AssertEqual(true, publishProperties.Contains("PublishSelfContained=true", StringComparison.Ordinal));
    AssertEqual(true, publishProperties.Contains("PublishSingleFile=true", StringComparison.Ordinal));
    AssertEqual(true, publishProperties.Contains("IncludeNativeLibrariesForSelfExtract=true", StringComparison.Ordinal));
    AssertEqual(true, publishProperties.Contains("PublishTrimmed=false", StringComparison.Ordinal));
    AssertEqual(true, publishProperties.Contains("DebugType=none", StringComparison.Ordinal));
    AssertEqual(true, publishProperties.Contains("DebugSymbols=false", StringComparison.Ordinal));

    string relativePath = targets
        .Descendants()
        .Where(element => element.Name.LocalName == "RelativePath")
        .Single(element => element.Value.Contains("%(Filename)%(Extension)", StringComparison.Ordinal))
        .Value;
    AssertEqual("%(Filename)%(Extension)", relativePath);
    AssertEqual(false, relativePath.Contains("TerrariaSplitMemoryProbeOutputSubdirectory", StringComparison.Ordinal));
}

static string GetRequiredElementValue(XElement parent, string localName)
{
    return parent.Elements().Single(element => element.Name.LocalName == localName).Value;
}

static void TestWorldPoolSignatureStartsWithTerrariaVersion()
{
    var settings = new AppSettings
    {
        General =
        {
            Language = LanguageNames.Chinese
        },
        Automation =
        {
            AutoCreate = new AutoCreateWorldSettings
            {
                WorldSize = AutoCreateWorldSize.Small,
                WorldDifficulty = AutoCreateWorldDifficulty.Expert,
                WorldEvil = AutoCreateWorldEvil.Crimson,
                SpecialSeeds = "For the Worthy|No Traps",
                SecretSeeds = "mole people|waterpark",
                EnablePyramidFilter = true,
                PyramidFilterItemMask = AutoCreatePyramidFilterItem.SandstormInABottleMask | AutoCreatePyramidFilterItem.PharaohSetMask
            }
        }
    };

    string signature = WorldPoolSignature.From(settings);

    AssertEqual("1.4.5.6|Small|Expert|Crimson|For the Worthy,No Traps|mole people,waterpark|pyramid=1|pyramidItems=5|name=zh-Hans", signature);

    settings.General.Language = LanguageNames.English;
    AssertEqual(
        "1.4.5.6|Small|Expert|Crimson|For the Worthy,No Traps|mole people,waterpark|pyramid=1|pyramidItems=5|name=en-US",
        WorldPoolSignature.From(settings));

    settings.Automation.AutoCreate.PyramidFilterItemMask = 0;
    AssertEqual(
        "1.4.5.6|Small|Expert|Crimson|For the Worthy,No Traps|mole people,waterpark|pyramid=1|pyramidItems=7|name=en-US",
        WorldPoolSignature.From(settings));
}

static void TestWorldPoolFileNameUsesTerrariaSplitTimestamp()
{
    MethodInfo method = typeof(WorldPoolStore).GetMethod(
            "CreateWorldFileName",
            BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Missing world pool file name builder.");
    string fileName = (string)(method.Invoke(null, [])
        ?? throw new InvalidOperationException("World pool file name builder returned null."));

    AssertEqual(true, fileName.StartsWith("TerrariaSplit_", StringComparison.Ordinal));
    AssertEqual(true, fileName.EndsWith(".wld", StringComparison.Ordinal));
    AssertEqual(false, fileName.Contains('-', StringComparison.Ordinal));
    AssertEqual(true, fileName.Length >= "TerrariaSplit_yyyyMMdd_HHmmss_fff.wld".Length);
}

static void TestTerrariaSeedRandomMatchesUnifiedRandomSequence()
{
    var random = new TerrariaSeedRandom(12345);

    AssertEqual(143337951, random.Next());
    AssertEqual(150666398, random.Next());
    AssertEqual(1663795458, random.Next());
    AssertEqual(1097663221, random.Next());
    AssertEqual(1712597933, random.Next());
}

static void TestTerrariaCopiedSeedBuilderFormatsOptions()
{
    var settings = new AutoCreateWorldSettings
    {
        WorldSize = AutoCreateWorldSize.Medium,
        WorldDifficulty = AutoCreateWorldDifficulty.Expert,
        WorldEvil = AutoCreateWorldEvil.Random,
        SpecialSeeds = "Zenith|Skyblock",
        SecretSeeds = "mole people | waterpark"
    };

    TerrariaCopiedSeed copiedSeed = TerrariaCopiedSeedBuilder.Create(settings, "123456789", TerrariaWorldSeedOptions.CrimsonEvilCode);

    AssertEqual("2.2.2.511.mole people|waterpark|123456789", copiedSeed.Text);
    AssertEqual(new TerrariaWorldSeedMetadata("mole people|waterpark|123456789", 2, 2, true, 511), copiedSeed.Metadata);

    settings.SpecialSeeds = string.Empty;
    settings.SecretSeeds = string.Empty;
    copiedSeed = TerrariaCopiedSeedBuilder.Create(settings, "42", TerrariaWorldSeedOptions.CorruptionEvilCode);

    AssertEqual("2.2.1.0.42", copiedSeed.Text);
    AssertEqual(new TerrariaWorldSeedMetadata("42", 2, 2, false, 0), copiedSeed.Metadata);
}

static void TestTerrariaWorldNameGeneratorFollowsGuiRules()
{
    AssertEqual(TerrariaLanguageCodes.English, TerrariaLanguageCodes.FromAppLanguage(LanguageNames.English));
    AssertEqual(TerrariaLanguageCodes.ChineseSimplified, TerrariaLanguageCodes.FromAppLanguage(LanguageNames.Chinese));

    var data = new TerrariaWorldNameData
    {
        Composition = ["first {Adjective} {Location} {Noun}", "last {Adjective}{Location}{Noun}"],
        Adjective = ["A1", "A2"],
        Location = ["L1", "L2"],
        Noun = ["N1", "N2"]
    };

    AssertEqual("last A2L2N2", TerrariaWorldNameGenerator.Create(data, _ => 0));

    data = new TerrariaWorldNameData
    {
        Composition = ["{Adjective} {Location} of {Noun}", "{Adjective}{Location}{Noun}"],
        Adjective = ["ExtremelyLongAdjective", "A"],
        Location = ["VeryLongLocation", "B"],
        Noun = ["VeryLongNoun", "C"]
    };

    int calls = 0;
    AssertEqual("ABC", TerrariaWorldNameGenerator.Create(data, max => ++calls <= 4 ? max - 1 : 0));
}

static void TestSettingsNormalize()
{
    var settings = new AppSettings
    {
        Automation =
        {
            AutoCreate = new AutoCreateWorldSettings
            {
                ShortActionDelayMilliseconds = -1,
                MenuActionDelayMilliseconds = 6000,
                PyramidFilterPostDelayMilliseconds = 6000,
                WindowActivationDelayMilliseconds = 6000,
                ClickFocusDelayMilliseconds = -10,
                InputPressDurationMilliseconds = 0,
                SpecialSeeds = "  for the worthy | get fixed boi | skyblock  ",
                SecretSeeds = "  mole people | waterpark  ",
                EnableZenithStarCatch = true,
                ZenithStarCatchStopStage = "not a real stage",
                ZenithStarCatchSpeedSliderValue = 9999,
                EnablePyramidFilter = true,
                PyramidFilterItemMask = AutoCreatePyramidFilterItem.AllMask | 8
            }
        }
    };

    SettingsNormalizer.Normalize(settings);
    AssertEqual(0, settings.Automation.AutoCreate.ShortActionDelayMilliseconds);
    AssertEqual(5000, settings.Automation.AutoCreate.MenuActionDelayMilliseconds);
    AssertEqual(5000, settings.Automation.AutoCreate.PyramidFilterPostDelayMilliseconds);
    AssertEqual(5000, settings.Automation.AutoCreate.WindowActivationDelayMilliseconds);
    AssertEqual(0, settings.Automation.AutoCreate.ClickFocusDelayMilliseconds);
    AssertEqual(1, settings.Automation.AutoCreate.InputPressDurationMilliseconds);
    AssertEqual("Zenith|Skyblock", settings.Automation.AutoCreate.SpecialSeeds);
    AssertEqual("mole people | waterpark", settings.Automation.AutoCreate.SecretSeeds);
    AssertEqual(true, settings.Automation.AutoCreate.EnableZenithStarCatch);
    AssertEqual(AutoCreateZenithStarCatchStage.Pots, settings.Automation.AutoCreate.ZenithStarCatchStopStage);
    AssertEqual(AutoCreateZenithStarCatchSpeed.MaximumSliderValue, settings.Automation.AutoCreate.ZenithStarCatchSpeedSliderValue);
    AssertEqual(true, settings.Automation.AutoCreate.EnablePyramidFilter);
    AssertEqual(AutoCreatePyramidFilterItem.AllMask, settings.Automation.AutoCreate.PyramidFilterItemMask);
    AssertEqual(true, settings.Overlay.SplitCompletionOutlineSplitStyles.Values.All(style =>
        style == SplitCompletionOutlineStyles.Rainbow));
    AssertEqual(true, settings.Overlay.SplitCompletionOutlineSegmentStyles.Values.All(style =>
        style == SplitCompletionOutlineStyles.Aurora));
    AssertEqual(
        AutoCreatePyramidFilterItem.FlyingCarpetMask | AutoCreatePyramidFilterItem.SandstormInABottleMask,
        AutoCreatePyramidFilterItem.ToMask(AutoCreatePyramidFilterItem.ParseList(" \u98DE\u6BEF | sandstorm | unknown ")));
    AssertEqual(10, AppSettingsDefaults.AutoCreate.WorldPoolTargetCount);
    AssertEqual(false, AppSettingsDefaults.AutoCreate.ReturnToMainMenuOnFilterFailure);

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
        Overlay =
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
        }
    };

    SettingsNormalizer.Normalize(settings);
    AssertEqual(0, settings.Overlay.TextEffects.IconOpacityPercent);
    AssertEqual(100, settings.Overlay.TextEffects.TimeOpacityPercent);
    AssertEqual(0, settings.Overlay.TextEffects.TimeShadowPercent);
    AssertEqual(101, settings.Overlay.TextEffects.TimeOutlineThicknessPercent);
    AssertEqual(100, settings.Overlay.TextEffects.DeltaOpacityPercent);
    AssertEqual(0, settings.Overlay.TextEffects.DeltaShadowPercent);
    AssertEqual(200, settings.Overlay.TextEffects.DeltaOutlineThicknessPercent);
    AssertEqual(0, settings.Overlay.TextEffects.TimerOpacityPercent);
    AssertEqual(0, settings.Overlay.TextEffects.TimerShadowPercent);
    AssertEqual(101, settings.Overlay.TextEffects.TimerOutlineThicknessPercent);
    AssertEqual(100, settings.Overlay.TextEffects.TimerMillisecondsOpacityPercent);
    AssertEqual(42, settings.Overlay.TextEffects.TimerMillisecondsShadowPercent);
    AssertEqual(77, settings.Overlay.TextEffects.TimerMillisecondsOutlineThicknessPercent);

    settings.Overlay.TextEffects = null!;
    SettingsNormalizer.Normalize(settings);
    AssertEqual(100, settings.Overlay.TextEffects.TimeOpacityPercent);
    AssertEqual(0, settings.Overlay.TextEffects.TimerShadowPercent);
}

static void TestSettingsNormalizeDerivesSplitIconsFromConditions()
{
    var settings = new AppSettings
    {
        Route =
        {
            SplitRoute =
            [
                new SplitRouteEntry
                {
                    Id = "split:stale-icon",
                    DisplayName = "Stale Icon",
                    Enabled = true,
                    Condition = SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                    IconTargetIds = [SplitCatalog.MoonLord]
                }
            ]
        }
    };

    SettingsNormalizer.Normalize(settings);
    SplitDefinition definition = SplitCatalog.Build(settings).Single();

    AssertEqual(SplitCatalog.Skeletron, settings.Route.SplitRoute.Single().IconTargetIds.Single());
    AssertEqual(SplitCatalog.Skeletron, definition.IconKeys.Single());
    AssertEqual(SplitCatalog.Skeletron, definition.TargetIds.Single());
}

static void TestSettingsNormalizeUiFontFamilies()
{
    var settings = new AppSettings();
    settings.Overlay.Columns.Time.FontFamily = $"  {UiFontSettings.DefaultFamilyName.ToUpperInvariant()}  ";
    settings.Overlay.Columns.Delta.FontFamily = "Definitely Missing TerrariaSplit Font";
    settings.Overlay.Columns.Timer.FontFamily = string.Empty;

    SettingsNormalizer.Normalize(settings);

    AssertEqual(UiFontSettings.DefaultFamilyName, settings.Overlay.Columns.Time.FontFamily);
    AssertEqual(UiFontSettings.DefaultFamilyName, settings.Overlay.Columns.Delta.FontFamily);
    AssertEqual(UiFontSettings.DefaultFamilyName, settings.Overlay.Columns.Timer.FontFamily);
    AssertEqual(true, UiFontSettings.GetInstalledFamilyNames().Count > 0);
}

static void TestSettingsNormalizerAssignsInternalSplitIds()
{
    var settings = new AppSettings
    {
        Route =
        {
            SplitRoute =
            [
                new SplitRouteEntry
                {
                    DisplayName = string.Empty,
                    Enabled = true,
                    Condition = SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                    IconTargetIds = [SplitCatalog.Skeletron]
                },
                new SplitRouteEntry
                {
                    DisplayName = string.Empty,
                    Enabled = true,
                    Condition = SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                    IconTargetIds = [SplitCatalog.Skeletron]
                },
                new SplitRouteEntry
                {
                    Id = "split:fixed",
                    DisplayName = "Fixed",
                    Enabled = true,
                    Condition = SplitCatalog.CreateBossFactCondition(SplitCatalog.WallOfFlesh),
                    IconTargetIds = [SplitCatalog.WallOfFlesh]
                },
                new SplitRouteEntry
                {
                    Id = "split:fixed",
                    DisplayName = "Duplicate",
                    Enabled = true,
                    Condition = SplitCatalog.CreateBossFactCondition(SplitCatalog.MoonLord),
                    IconTargetIds = [SplitCatalog.MoonLord]
                }
            ]
        }
    };

    SettingsNormalizer.Normalize(settings);

    AssertEqual(4, settings.Route.SplitRoute.Count);
    AssertEqual(
        settings.Route.SplitRoute.Count,
        settings.Route.SplitRoute.Select(entry => entry.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    AssertEqual("Skeletron", settings.Route.SplitRoute[0].DisplayName);
    AssertEqual("Skeletron", settings.Route.SplitRoute[1].DisplayName);
    AssertEqual("split:fixed", settings.Route.SplitRoute[2].Id);
    AssertEqual(false, settings.Route.SplitRoute.Any(entry => string.IsNullOrWhiteSpace(entry.Id)));
    AssertEqual(false, settings.Route.SplitRoute.Any(entry => entry.DisplayName.StartsWith("split:", StringComparison.OrdinalIgnoreCase)));
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
    var settings = new AppSettings { Hotkeys = { PauseResumeKey = Keys.ControlKey.ToString(),
        ResetKey = Keys.LShiftKey.ToString(),
        MouseClickThroughKey = Keys.RMenu.ToString(),
        CreateWorldKey = Keys.LWin.ToString(),
        PracticeWorldKey = Keys.LWin.ToString() } };

    AssertEqual(Keys.F12, settings.GetPauseResumeKeys());
    AssertEqual(Keys.F6, settings.GetResetKeys());
    AssertEqual(Keys.F9, settings.GetMouseClickThroughKeys());
    AssertEqual(Keys.F7, settings.GetCreateWorldKeys());
    AssertEqual(Keys.F8, settings.GetPracticeWorldKeys());
}

static void TestAppSettingsParsesModifierHotkeys()
{
    var settings = new AppSettings { Hotkeys = { PauseResumeKey = (Keys.Control | Keys.F12).ToString(),
        ResetKey = (Keys.Alt | Keys.F6).ToString(),
        MouseClickThroughKey = (Keys.Shift | Keys.F9).ToString(),
        CreateWorldKey = (Keys.Control | Keys.Alt | Keys.F7).ToString(),
        PracticeWorldKey = (Keys.Control | Keys.Shift | Keys.F8).ToString() } };

    AssertEqual(Keys.Control | Keys.F12, settings.GetPauseResumeKeys());
    AssertEqual(Keys.Alt | Keys.F6, settings.GetResetKeys());
    AssertEqual(Keys.Shift | Keys.F9, settings.GetMouseClickThroughKeys());
    AssertEqual(Keys.Control | Keys.Alt | Keys.F7, settings.GetCreateWorldKeys());
    AssertEqual(Keys.Control | Keys.Shift | Keys.F8, settings.GetPracticeWorldKeys());
}

static void TestAppSettingsUsesPersonalBestAsReferenceTime()
{
    var settings = new AppSettings { Comparison = { UsePersonalBestAsReferenceTime = true } };
    SettingsNormalizer.Normalize(settings);
    const string skeletronSplitId = "split:boss-skeletron";
    string skeletronKey = SingleCumulativeKey(settings, skeletronSplitId);
    settings.Comparison.ReferenceSplitSets =
    [
        AppSettings.CreateReferenceSet("WR", new Dictionary<string, string>
        {
            [skeletronKey] = "01:00"
        }, SplitConditionDataRows.Build(settings).Select(row => row.Key))
    ];
    settings.Comparison.PersonalBestTimes[skeletronKey] = "00:30";

    SplitDefinition definition = SplitCatalog.Build(settings).Single(item => item.Id == skeletronSplitId);

    AssertEqual(AppSettings.PersonalBestReferenceSetName, settings.GetActiveReferenceSet().Name);
    AssertEqual("00:30", settings.GetReferenceText(skeletronKey));
    AssertEqual(true, settings.TryGetReferenceSplit(definition, out TimeSpan split));
    AssertEqual(TimeSpan.FromSeconds(30), split);

    settings.SetReferenceText(skeletronKey, "05:00");
    AssertEqual("00:30", settings.GetReferenceText(skeletronKey));
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

        var routeSettings = new AppSettings { Route = { SplitRoute = SplitCatalog.CreateDefaultRoute() } };
        SettingsNormalizer.Normalize(routeSettings);
        IEnumerable<string> cumulativeKeys = SplitConditionDataRows.Build(routeSettings).Select(row => row.Key);
        SplitTimeSetStore.SaveReferenceSets(
        [
            CreateSplitSet("WR", cumulativeKeys),
            CreateSplitSet("Custom Reference", cumulativeKeys, "00:30")
        ]);

        SplitTimeSetStore.SavePersonalBestTimeSets(
        [
            CreateSplitSet("Personal", cumulativeKeys),
            CreateSplitSet("Race PB", cumulativeKeys)
        ]);

        IEnumerable<string> segmentKeys = SplitRouteGroups.Build(routeSettings).Select(group => group.Key);
        SplitTimeSetStore.SavePersonalBestSegmentSets(
        [
            CreateSplitSet("Personal", segmentKeys),
            CreateSplitSet("Race Segments", segmentKeys)
        ]);

        string profileName = "active-external-sets.json";
        string settingsPath = Path.Combine(settingsDirectory, profileName);
        Directory.CreateDirectory(settingsDirectory);
        SettingsSerializer.WriteSettings(settingsPath, new AppSettings { Comparison = { ActiveReferenceSplitSet = "Custom Reference",
            ActivePersonalBestTimeSet = "Race PB",
            ActivePersonalBestSegmentSet = "Race Segments" } });

        AppSettings loaded = AppSettingsStore.Load(profileName);

        AssertEqual("Custom Reference", loaded.Comparison.ActiveReferenceSplitSet);
        AssertEqual("Race PB", loaded.Comparison.ActivePersonalBestTimeSet);
        AssertEqual("Race Segments", loaded.Comparison.ActivePersonalBestSegmentSet);
    }
    finally
    {
        RestoreDirectory(settingsDirectory, settingsSnapshot);
        RestoreDirectory(referenceDirectory, referenceSnapshot);
        RestoreDirectory(personalBestTimeDirectory, personalBestTimeSnapshot);
        RestoreDirectory(personalBestSegmentDirectory, personalBestSegmentSnapshot);
    }
}

static ReferenceSplitSet CreateSplitSet(string name, IEnumerable<string> keys, string skeletronValue = "")
{
    var set = new ReferenceSplitSet
    {
        Name = name,
        Splits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    };

    foreach (string key in keys)
    {
        set.Splits[key] = key.Contains("boss-skeletron-defeated", StringComparison.OrdinalIgnoreCase)
            ? skeletronValue
            : string.Empty;
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

        AssertEqual(175, form.Result.Overlay.Columns.ScalePercent);
    });
}

static void TestSettingsFormOrdersMovedPages()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        List<string> labels = form.PageHost.Pages.Select(page => page.Nav.Text).ToList();

        AssertEqual(
            "General|Route|Data|UI|Effects|Automation|Sounds|Colors|Advanced|Debug",
            string.Join('|', labels));
    });
}

static void TestSettingsFormAppliesDynamicDeltaUnitsFromUiPage()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings { Overlay = { EnableDynamicDeltaTimeUnits = true } });
        UiSettingsPage page = form.PageHost.GetOrCreatePage<UiSettingsPage>(SettingsPageId.Ui);
        page.EnableDynamicDeltaTimeUnitsBox.Checked = false;

        form.ApplyForTests();

        AssertEqual(false, form.Result.Overlay.EnableDynamicDeltaTimeUnits);
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

        AssertEqual(55, form.Result.Overlay.TextEffects.IconOpacityPercent);
        AssertEqual(65, form.Result.Overlay.TextEffects.TimeOpacityPercent);
        AssertEqual(25, form.Result.Overlay.TextEffects.TimeShadowPercent);
        AssertEqual(30, form.Result.Overlay.TextEffects.TimeOutlineThicknessPercent);
        AssertEqual(75, form.Result.Overlay.TextEffects.DeltaOpacityPercent);
        AssertEqual(35, form.Result.Overlay.TextEffects.DeltaShadowPercent);
        AssertEqual(40, form.Result.Overlay.TextEffects.DeltaOutlineThicknessPercent);
        AssertEqual(85, form.Result.Overlay.TextEffects.TimerOpacityPercent);
        AssertEqual(25, form.Result.Overlay.TextEffects.TimerShadowPercent);
        AssertEqual(30, form.Result.Overlay.TextEffects.TimerOutlineThicknessPercent);
        AssertEqual(95, form.Result.Overlay.TextEffects.TimerMillisecondsOpacityPercent);
        AssertEqual(45, form.Result.Overlay.TextEffects.TimerMillisecondsShadowPercent);
        AssertEqual(50, form.Result.Overlay.TextEffects.TimerMillisecondsOutlineThicknessPercent);
    });
}

static void TestSettingsFormAppliesAttachedSplitDisplaySettings()
{
    RunSta(() =>
    {
        AppSettings settings = AppSettingsDefaults.Create();
        using var form = new SettingsForm(settings);
        UiSettingsPage page = form.PageHost.GetOrCreatePage<UiSettingsPage>(SettingsPageId.Ui);
        string selectedFamily = SelectInstalledFontFamilyForTest(settings.Overlay.Columns.AttachedTime.FontFamily);

        page.GetColumnWidthBoxForTests("AttachedIcon").Text = "212";
        page.GetColumnFontSizeBoxForTests("AttachedIcon").Text = "41";
        page.AttachedIconOpacityBox.Text = "44";

        page.GetColumnWidthBoxForTests("AttachedTime").Text = "122";
        SetFontFamilySelectorValue(page.GetFontFamilySelectorForTests("AttachedTime"), selectedFamily);
        page.GetColumnFontSizeBoxForTests("AttachedTime").Text = "18.5";
        (page.GetColumnBoldBoxForTests("AttachedTime") ?? throw new InvalidOperationException("Missing attached time bold box.")).Checked = true;
        page.AttachedTimeOpacityBox.Text = "64";
        page.AttachedTimeShadowBox.Text = "24";
        page.AttachedTimeOutlineThicknessBox.Text = "34";

        page.GetColumnWidthBoxForTests("AttachedDelta").Text = "132";
        SetFontFamilySelectorValue(page.GetFontFamilySelectorForTests("AttachedDelta"), selectedFamily);
        page.GetColumnFontSizeBoxForTests("AttachedDelta").Text = "19.5";
        (page.GetColumnBoldBoxForTests("AttachedDelta") ?? throw new InvalidOperationException("Missing attached delta bold box.")).Checked = false;
        page.AttachedDeltaOpacityBox.Text = "74";
        page.AttachedDeltaShadowBox.Text = "32";
        page.AttachedDeltaOutlineThicknessBox.Text = "42";

        form.ApplyForTests();

        AssertEqual(212, form.Result.Overlay.Columns.AttachedIcon.Width);
        AssertEqual(41f, form.Result.Overlay.Columns.AttachedIcon.FontSize);
        AssertEqual(false, form.Result.Overlay.Columns.AttachedIcon.Bold);
        AssertEqual(44, form.Result.Overlay.TextEffects.AttachedIconOpacityPercent);
        AssertEqual(122, form.Result.Overlay.Columns.AttachedTime.Width);
        AssertEqual(UiFontSettings.NormalizeFamilyName(selectedFamily), form.Result.Overlay.Columns.AttachedTime.FontFamily);
        AssertEqual(18.5f, form.Result.Overlay.Columns.AttachedTime.FontSize);
        AssertEqual(true, form.Result.Overlay.Columns.AttachedTime.Bold);
        AssertEqual(64, form.Result.Overlay.TextEffects.AttachedTimeOpacityPercent);
        AssertEqual(24, form.Result.Overlay.TextEffects.AttachedTimeShadowPercent);
        AssertEqual(34, form.Result.Overlay.TextEffects.AttachedTimeOutlineThicknessPercent);
        AssertEqual(132, form.Result.Overlay.Columns.AttachedDelta.Width);
        AssertEqual(UiFontSettings.NormalizeFamilyName(selectedFamily), form.Result.Overlay.Columns.AttachedDelta.FontFamily);
        AssertEqual(19.5f, form.Result.Overlay.Columns.AttachedDelta.FontSize);
        AssertEqual(false, form.Result.Overlay.Columns.AttachedDelta.Bold);
        AssertEqual(74, form.Result.Overlay.TextEffects.AttachedDeltaOpacityPercent);
        AssertEqual(32, form.Result.Overlay.TextEffects.AttachedDeltaShadowPercent);
        AssertEqual(42, form.Result.Overlay.TextEffects.AttachedDeltaOutlineThicknessPercent);
    });
}

static void TestSettingsFormAppliesUiFontFamilies()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        UiSettingsPage page = form.PageHost.GetOrCreatePage<UiSettingsPage>(SettingsPageId.Ui);
        string selectedFamily = SelectInstalledFontFamilyForTest(form.Result.Overlay.Columns.Time.FontFamily);

        SetFontFamilySelectorValue(page.GetFontFamilySelectorForTests("Time"), selectedFamily);
        SetFontFamilySelectorValue(page.GetFontFamilySelectorForTests("Timer"), selectedFamily);

        form.ApplyForTests();

        AssertEqual(UiFontSettings.NormalizeFamilyName(selectedFamily), form.Result.Overlay.Columns.Time.FontFamily);
        AssertEqual(UiFontSettings.NormalizeFamilyName(selectedFamily), form.Result.Overlay.Columns.Timer.FontFamily);
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

        AssertEqual(Keys.F10.ToString(), form.Result.Hotkeys.PracticeWorldKey);
        AssertEqual(AutoCreateSpecialWorldSeed.ForTheWorthy, form.Result.Automation.AutoCreate.SpecialSeeds);
        AssertEqual("mole people", form.Result.Automation.AutoCreate.SecretSeeds);
        AssertEqual(PracticeWorldSettings.SlotCount, form.Result.PracticeWorlds.Slots.Count);
        AssertEqual("Plantera", form.Result.PracticeWorlds.Slots[0].Name);
        AssertEqual("C:\\practice\\player.plr", form.Result.PracticeWorlds.Slots[0].PlayerFilePath);
        AssertEqual("C:\\practice\\world.wld", form.Result.PracticeWorlds.Slots[0].WorldFilePath);
    });
}

static void TestSettingsFormPreservesAdvancedSplitRoute()
{
    RunSta(() =>
    {
        var settings = new AppSettings
        {
            Route =
            {
                SplitRoute =
                [
                    new SplitRouteEntry
                    {
                        Id = "split:custom-composite",
                        DisplayName = "Custom Composite",
                        Enabled = true,
                        Condition = SplitCondition.All(
                        [
                            SplitCondition.Any(
                            [
                                SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer),
                                SplitCatalog.CreateBossFactCondition(SplitCatalog.Twins)
                            ]),
                            SplitCondition.All(
                            [
                                SplitCatalog.CreateBossFactCondition(SplitCatalog.SkeletronPrime),
                                SplitCatalog.CreateItemEverOwnedCondition(50, 3),
                                SplitCatalog.CreateItemEverOwnedCondition(70, 1)
                            ])
                        ]),
                        IconTargetIds =
                        [
                            SplitCatalog.Destroyer,
                            SplitCatalog.Twins,
                            SplitCatalog.SkeletronPrime,
                            SplitCatalog.CreateItemTargetId(50),
                            SplitCatalog.CreateItemTargetId(70)
                        ]
                    }
                ]
            }
        };

        using var form = new SettingsForm(settings);
        form.PageHost.GetOrCreatePage<SplitSettingsPage>(SettingsPageId.Splits);

        form.ApplyForTests();

        SplitRouteEntry entry = form.Result.Route.SplitRoute.Single();
        AssertEqual("split:custom-composite", entry.Id);
        AssertEqual(5, entry.IconTargetIds.Count);
        AssertEqual(true, entry.UseAdvancedConditionEditor);

        SplitCondition root = entry.Condition;
        AssertEqual(SplitConditionKind.All, SplitConditionKind.Normalize(root.Kind));
        AssertEqual(2, root.Children.Count);

        SplitCondition firstGroup = root.Children[0];
        AssertEqual(SplitConditionKind.AtLeast, SplitConditionKind.Normalize(firstGroup.Kind));
        AssertEqual(1, firstGroup.Value);
        AssertEqual(2, firstGroup.Children.Count);
        AssertEqual(true, firstGroup.Children.All(child => SplitConditionKind.Normalize(child.Kind) == SplitConditionKind.Fact));

        SplitCondition secondGroup = root.Children[1];
        AssertEqual(SplitConditionKind.All, SplitConditionKind.Normalize(secondGroup.Kind));
        AssertEqual(3, secondGroup.Children.Count);
        AssertEqual(true, secondGroup.Children.Any(child => string.Equals(child.FactKey, SplitCatalog.CreateItemEverOwnedFactKey(70), StringComparison.OrdinalIgnoreCase)));
        SplitCondition itemCondition = secondGroup.Children.Single(child =>
            string.Equals(child.FactKey, SplitCatalog.CreateItemEverOwnedFactKey(50), StringComparison.OrdinalIgnoreCase));
        AssertEqual(SplitFactComparison.AtLeast, SplitFactComparison.Normalize(itemCondition.Comparison));
        AssertEqual(3, itemCondition.Value);
    });
}

static void TestSettingsFormKeepsAdvancedConditionModePerGroup()
{
    RunSta(() =>
    {
        var settings = new AppSettings
        {
            Route =
            {
                SplitRoute =
                [
                    new SplitRouteEntry
                    {
                        Id = "split:advanced",
                        DisplayName = "Advanced",
                        Enabled = true,
                        UseAdvancedConditionEditor = true,
                        Condition = SplitCondition.All(
                        [
                            SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                            SplitCondition.AtLeast(
                            [
                                SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer),
                                SplitCatalog.CreateBossFactCondition(SplitCatalog.Twins)
                            ], 1)
                        ]),
                        IconTargetIds = [SplitCatalog.Skeletron, SplitCatalog.Destroyer, SplitCatalog.Twins]
                    },
                    new SplitRouteEntry
                    {
                        Id = "split:basic",
                        DisplayName = "Basic",
                        Enabled = true,
                        UseAdvancedConditionEditor = false,
                        Condition = SplitCondition.AtLeast(
                        [
                            SplitCatalog.CreateBossFactCondition(SplitCatalog.MoonLord)
                        ], 1),
                        IconTargetIds = [SplitCatalog.MoonLord]
                    }
                ]
            }
        };

        using var form = new SettingsForm(settings);
        SplitSettingsPage page = form.PageHost.GetOrCreatePage<SplitSettingsPage>(SettingsPageId.Splits);

        AssertEqual(true, page.AdvancedConditionModeForTests);
        AssertEqual(true, page.AddTargetToSelectedGroupButtonForTests.Enabled);
        AssertEqual("Copy ID", page.AddTargetToSelectedGroupButtonForTests.Text);
        AssertEqual(true, page.AddTargetToNewGroupButtonForTests.Enabled);
        AssertEqual(true, page.TargetKindBoxForTests.Enabled);
        AssertEqual(true, page.TargetSearchBoxForTests.Enabled);
        AssertEqual(true, page.TargetListForTests.Enabled);
        page.AdvancedConditionBoxForTests.Text = """
ALL(
  Boss:moon-lord,
  ATLEAST(1,
    Boss:skeletron,
    Boss:wall-of-flesh
  )
)
""";

        page.RouteListForTests.SelectedIndex = 1;
        AssertEqual(false, page.AdvancedConditionModeForTests);
        AssertEqual(true, page.AddTargetToSelectedGroupButtonForTests.Enabled);
        AssertEqual("Add to selected group", page.AddTargetToSelectedGroupButtonForTests.Text);

        page.RouteListForTests.SelectedIndex = 0;
        AssertEqual(true, page.AdvancedConditionModeForTests);
        AssertEqual("Copy ID", page.AddTargetToSelectedGroupButtonForTests.Text);
        AssertEqual(true, page.AdvancedConditionBoxForTests.Text.Contains("Boss:moon-lord", StringComparison.Ordinal));

        form.ApplyForTests();

        SplitRouteEntry advanced = form.Result.Route.SplitRoute[0];
        AssertEqual(true, advanced.UseAdvancedConditionEditor);
        AssertEqual(SplitConditionKind.All, SplitConditionKind.Normalize(advanced.Condition.Kind));
        AssertEqual(SplitCatalog.CreateBossFactCondition(SplitCatalog.MoonLord).FactKey, advanced.Condition.Children[0].FactKey);
        AssertEqual(SplitConditionKind.AtLeast, SplitConditionKind.Normalize(advanced.Condition.Children[1].Kind));
        AssertEqual(false, form.Result.Route.SplitRoute[1].UseAdvancedConditionEditor);
    });
}

static void TestSettingsFormBlocksLossyAdvancedConditionDowngrade()
{
    RunSta(() =>
    {
        var settings = new AppSettings
        {
            Route =
            {
                SplitRoute =
                [
                    new SplitRouteEntry
                    {
                        Id = "split:advanced",
                        DisplayName = "Advanced",
                        Enabled = true,
                        UseAdvancedConditionEditor = true,
                        Condition = SplitCondition.All(
                        [
                            SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                            SplitCondition.AtLeast(
                            [
                                SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer),
                                SplitCatalog.CreateBossFactCondition(SplitCatalog.Twins)
                            ], 1)
                        ]),
                        IconTargetIds = [SplitCatalog.Skeletron, SplitCatalog.Destroyer, SplitCatalog.Twins]
                    }
                ]
            }
        };

        var warnings = new List<(string Message, string Title, MessageBoxButtons Buttons, MessageBoxIcon Icon)>();
        using var form = new SettingsForm(
            settings,
            messageBoxPresenter: (_, message, title, buttons, icon) =>
            {
                warnings.Add((message, title, buttons, icon));
                return DialogResult.OK;
            });
        SplitSettingsPage page = form.PageHost.GetOrCreatePage<SplitSettingsPage>(SettingsPageId.Splits);

        AssertEqual(true, page.AdvancedConditionModeForTests);
        InvokePrivate(page, "ToggleAdvancedConditionMode");
        AssertEqual(true, page.AdvancedConditionModeForTests);
        AssertEqual(1, warnings.Count);
        AssertEqual("Advanced condition cannot be converted to basic editor without losing structure.", warnings[0].Message);
        AssertEqual("TerrariaSplit Settings", warnings[0].Title);
        AssertEqual(MessageBoxButtons.OK, warnings[0].Buttons);
        AssertEqual(MessageBoxIcon.Warning, warnings[0].Icon);

        form.ApplyForTests();
        AssertEqual(true, form.Result.Route.SplitRoute.Single().UseAdvancedConditionEditor);
        AssertEqual(SplitConditionKind.All, SplitConditionKind.Normalize(form.Result.Route.SplitRoute.Single().Condition.Kind));
    });
}

static void TestSettingsFormAllowsEmptyAdvancedConditionDowngrade()
{
    RunSta(() =>
    {
        var settings = new AppSettings
        {
            Route =
            {
                SplitRoute =
                [
                    new SplitRouteEntry
                    {
                        Id = "split:advanced",
                        DisplayName = "Advanced",
                        Enabled = true,
                        UseAdvancedConditionEditor = true,
                        Condition = SplitCondition.AtLeast(
                        [
                            SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron)
                        ], 1),
                        IconTargetIds = [SplitCatalog.Skeletron]
                    }
                ]
            }
        };

        using var form = new SettingsForm(settings);
        SplitSettingsPage page = form.PageHost.GetOrCreatePage<SplitSettingsPage>(SettingsPageId.Splits);

        AssertEqual(true, page.AdvancedConditionModeForTests);
        page.AdvancedConditionBoxForTests.Text = string.Empty;
        InvokePrivate(page, "ToggleAdvancedConditionMode");

        AssertEqual(false, page.AdvancedConditionModeForTests);
        AssertEqual(0, page.ConditionListForTests.Items.Count);
        AssertEqual("Add to selected group", page.AddTargetToSelectedGroupButtonForTests.Text);
    });
}

static void TestSettingsFormSwitchesSplitConditionsWithoutOverwrite()
{
    RunSta(() =>
    {
        var settings = new AppSettings
        {
            Route =
            {
                SplitRoute =
                [
                    new SplitRouteEntry
                    {
                        Id = "split:first",
                        DisplayName = "First",
                        Enabled = false,
                        Condition = SplitCondition.All([SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron)]),
                        IconTargetIds = [SplitCatalog.Skeletron]
                    },
                    new SplitRouteEntry
                    {
                        Id = "split:second",
                        DisplayName = "Second",
                        Enabled = true,
                        Condition = SplitCondition.All([SplitCatalog.CreateBossFactCondition(SplitCatalog.MoonLord)]),
                        IconTargetIds = [SplitCatalog.MoonLord]
                    }
                ]
            }
        };

        using var form = new SettingsForm(settings);
        SplitSettingsPage page = form.PageHost.GetOrCreatePage<SplitSettingsPage>(SettingsPageId.Splits);

        AssertEqual("First", page.SplitNameBoxForTests.Text);
        AssertEqual(false, page.SplitEnabledBoxForTests.Checked);

        page.RouteListForTests.SelectedIndex = 1;
        AssertEqual("Second", page.SplitNameBoxForTests.Text);
        AssertEqual(true, page.SplitEnabledBoxForTests.Checked);
        AssertEqual(true, page.ConditionListForTests.Items.Cast<object>().Any(item =>
            (item.ToString() ?? string.Empty).Contains("Moon Lord", StringComparison.Ordinal)));

        form.ApplyForTests();

        AssertEqual(
            SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron).FactKey,
            form.Result.Route.SplitRoute[0].Condition.GetFactConditions().Single().FactKey);
        AssertEqual(
            SplitCatalog.CreateBossFactCondition(SplitCatalog.MoonLord).FactKey,
            form.Result.Route.SplitRoute[1].Condition.GetFactConditions().Single().FactKey);
    });
}

static void TestSettingsFormSavesAttachedRouteFlags()
{
    RunSta(() =>
    {
        var settings = new AppSettings
        {
            Route =
            {
                SplitRoute =
                [
                    new SplitRouteEntry
                    {
                        Id = "split:first",
                        DisplayName = "First",
                        Enabled = true,
                        Condition = SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                        IconTargetIds = [SplitCatalog.Skeletron]
                    },
                    new SplitRouteEntry
                    {
                        Id = "split:second",
                        DisplayName = "Second",
                        Enabled = true,
                        Condition = SplitCatalog.CreateBossFactCondition(SplitCatalog.MoonLord),
                        IconTargetIds = [SplitCatalog.MoonLord]
                    }
                ]
            }
        };

        using var form = new SettingsForm(settings);
        SplitSettingsPage page = form.PageHost.GetOrCreatePage<SplitSettingsPage>(SettingsPageId.Splits);

        AssertEqual(true, page.SplitAttachedBoxForTests.Enabled);
        page.SplitAttachedBoxForTests.Checked = true;

        page.RouteListForTests.SelectedIndex = 1;
        AssertEqual(false, page.SplitAttachedBoxForTests.Enabled);
        AssertEqual(false, page.SplitAttachedBoxForTests.Checked);

        form.ApplyForTests();

        AssertEqual(true, form.Result.Route.SplitRoute[0].IsAttached);
        AssertEqual(false, form.Result.Route.SplitRoute[1].IsAttached);
    });
}

static void TestSettingsFormSavesSplitIconOverride()
{
    RunSta(() =>
    {
        var settings = new AppSettings
        {
            Route =
            {
                SplitRoute =
                [
                    new SplitRouteEntry
                    {
                        Id = "split:any-boss",
                        DisplayName = "Any Boss",
                        Enabled = true,
                        Condition = SplitCondition.Any(
                        [
                            SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                            SplitCatalog.CreateBossFactCondition(SplitCatalog.WallOfFlesh)
                        ]),
                        IconTargetIds = [SplitCatalog.Skeletron, SplitCatalog.WallOfFlesh]
                    },
                    new SplitRouteEntry
                    {
                        Id = "split:custom-icon",
                        DisplayName = "Custom Icon",
                        Enabled = true,
                        Condition = SplitCatalog.CreateBossFactCondition(SplitCatalog.MoonLord),
                        IconTargetIds = [SplitCatalog.MoonLord]
                    }
                ]
            }
        };

        using var form = new SettingsForm(settings);
        SplitSettingsPage page = form.PageHost.GetOrCreatePage<SplitSettingsPage>(SettingsPageId.Splits);
        SelectComboBoxItem(page.IconOverrideBoxForTests, "Wall of Flesh");

        page.RouteListForTests.SelectedIndex = 1;
        SelectComboBoxItem(page.IconOverrideBoxForTests, "Custom image");
        page.IconOverrideFileBoxForTests.Text = "icons\\custom.png";

        form.ApplyForTests();

        SplitIconOverride targetOverride = form.Result.Route.SplitRoute[0].IconOverride;
        AssertEqual(SplitIconOverrideSource.Target, targetOverride.Source);
        AssertEqual(SplitCatalog.WallOfFlesh, targetOverride.TargetId);
        AssertEqual(string.Empty, targetOverride.FilePath);

        SplitIconOverride customOverride = form.Result.Route.SplitRoute[1].IconOverride;
        AssertEqual(SplitIconOverrideSource.CustomFile, customOverride.Source);
        AssertEqual(string.Empty, customOverride.TargetId);
        AssertEqual("icons\\custom.png", customOverride.FilePath);

        using var reopened = new SettingsForm(form.Result);
        SplitSettingsPage reopenedPage = reopened.PageHost.GetOrCreatePage<SplitSettingsPage>(SettingsPageId.Splits);
        AssertEqual("Wall of Flesh (Boss:wall-of-flesh)", reopenedPage.IconOverrideBoxForTests.SelectedItem?.ToString());
        reopened.ApplyForTests();

        SplitIconOverride reopenedTargetOverride = reopened.Result.Route.SplitRoute[0].IconOverride;
        AssertEqual(SplitIconOverrideSource.Target, reopenedTargetOverride.Source);
        AssertEqual(SplitCatalog.WallOfFlesh, reopenedTargetOverride.TargetId);

        reopenedPage.RouteListForTests.SelectedIndex = 1;
        AssertEqual("Custom image", reopenedPage.IconOverrideBoxForTests.SelectedItem?.ToString());
        reopened.ApplyForTests();

        SplitIconOverride reopenedCustomOverride = reopened.Result.Route.SplitRoute[1].IconOverride;
        AssertEqual(SplitIconOverrideSource.CustomFile, reopenedCustomOverride.Source);
        AssertEqual("icons\\custom.png", reopenedCustomOverride.FilePath);
    });
}

static void TestSettingsFormSavesLocalizedSplitIconOverride()
{
    RunSta(() =>
    {
        var settings = new AppSettings
        {
            General =
            {
                Language = LanguageNames.Chinese
            },
            Route =
            {
                SplitRoute =
                [
                    new SplitRouteEntry
                    {
                        Id = "split:any-boss",
                        DisplayName = "Any Boss",
                        Enabled = true,
                        Condition = SplitCondition.Any(
                        [
                            SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                            SplitCatalog.CreateBossFactCondition(SplitCatalog.WallOfFlesh)
                        ]),
                        IconTargetIds = [SplitCatalog.Skeletron, SplitCatalog.WallOfFlesh]
                    }
                ]
            }
        };

        using var form = new SettingsForm(settings);
        SplitSettingsPage page = form.PageHost.GetOrCreatePage<SplitSettingsPage>(SettingsPageId.Splits);
        SelectComboBoxItem(page.IconOverrideBoxForTests, "血肉墙");

        form.ApplyForTests();

        SplitIconOverride iconOverride = form.Result.Route.SplitRoute.Single().IconOverride;
        AssertEqual(SplitIconOverrideSource.Target, iconOverride.Source);
        AssertEqual(SplitCatalog.WallOfFlesh, iconOverride.TargetId);

        using var reopened = new SettingsForm(form.Result);
        SplitSettingsPage reopenedPage = reopened.PageHost.GetOrCreatePage<SplitSettingsPage>(SettingsPageId.Splits);
        AssertEqual("血肉墙 (Boss:wall-of-flesh)", reopenedPage.IconOverrideBoxForTests.SelectedItem?.ToString());
    });
}

static void TestSettingsFormRejectsInvalidSplitRouteApply()
{
    RunSta(() =>
    {
        var settings = new AppSettings { Route = { SplitRoute = [
                CreateTestRouteEntry("split:skeletron", "Skeletron", SplitCatalog.Skeletron)
            ] } };

        var warnings = new List<(string Message, string Title, MessageBoxButtons Buttons, MessageBoxIcon Icon)>();
        using var form = new SettingsForm(
            settings,
            messageBoxPresenter: (_, message, title, buttons, icon) =>
            {
                warnings.Add((message, title, buttons, icon));
                return DialogResult.OK;
            });
        SplitSettingsPage page = form.PageHost.GetOrCreatePage<SplitSettingsPage>(SettingsPageId.Splits);
        page.ConditionListForTests.SelectedIndex = 0;
        InvokePrivate(page, "RemoveSelectedFact");

        AssertEqual(false, form.TryApplyForTests(out string message));
        AssertEqual(false, string.IsNullOrWhiteSpace(message));
        AssertEqual(0, warnings.Count);

        InvokePrivate(form, "ApplyAndNotify");
        AssertEqual(1, warnings.Count);
        AssertEqual(message, warnings[0].Message);
        AssertEqual("TerrariaSplit Settings", warnings[0].Title);
        AssertEqual(MessageBoxButtons.OK, warnings[0].Buttons);
        AssertEqual(MessageBoxIcon.Warning, warnings[0].Icon);
        AssertEqual("split:skeletron", form.Result.Route.SplitRoute.Single().Id);
        AssertEqual(SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron).FactKey, form.Result.Route.SplitRoute.Single().Condition.GetFactConditions().Single().FactKey);
    });
}

static void TestSettingsFormEditsMatchModeFromDropdown()
{
    RunSta(() =>
    {
        var settings = new AppSettings
        {
            Route =
            {
                SplitRoute =
                [
                    new SplitRouteEntry
                    {
                        Id = "split:mechanical",
                        DisplayName = "Mechanical",
                        Enabled = true,
                        Condition = SplitCondition.All(
                        [
                            SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer),
                            SplitCatalog.CreateBossFactCondition(SplitCatalog.Twins),
                            SplitCatalog.CreateBossFactCondition(SplitCatalog.SkeletronPrime)
                        ]),
                        IconTargetIds = [SplitCatalog.Destroyer, SplitCatalog.Twins, SplitCatalog.SkeletronPrime]
                    }
                ]
            }
        };

        using var form = new SettingsForm(settings);
        SplitSettingsPage page = form.PageHost.GetOrCreatePage<SplitSettingsPage>(SettingsPageId.Splits);

        AssertEqual(3, page.ConditionMatchModeBoxForTests.Items.Count);
        AssertEqual("All", page.ConditionMatchModeBoxForTests.SelectedItem?.ToString());
        AssertEqual("All", page.ConditionMatchModeBoxForTests.Text);
        SelectComboBoxItem(page.ConditionMatchModeBoxForTests, "At least 2");
        AssertEqual("2", page.ConditionMatchModeBoxForTests.Text);

        form.ApplyForTests();

        SplitCondition condition = form.Result.Route.SplitRoute.Single().Condition;
        AssertEqual(SplitConditionKind.AtLeast, SplitConditionKind.Normalize(condition.Kind));
        AssertEqual(2, condition.Value);
        AssertEqual(3, condition.Children.Count);
    });
}

static void TestSettingsFormDecrementsMatchModeWhenDeletingCondition()
{
    RunSta(() =>
    {
        var settings = new AppSettings
        {
            Route =
            {
                SplitRoute =
                [
                    new SplitRouteEntry
                    {
                        Id = "split:bosses",
                        DisplayName = "Bosses",
                        Enabled = true,
                        Condition = SplitCondition.AtLeast(
                        [
                            SplitCatalog.CreateBossFactCondition("boss:king-slime"),
                            SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                            SplitCatalog.CreateBossFactCondition(SplitCatalog.WallOfFlesh),
                            SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer)
                        ], 3),
                        IconTargetIds = ["boss:king-slime", SplitCatalog.Skeletron, SplitCatalog.WallOfFlesh, SplitCatalog.Destroyer]
                    }
                ]
            }
        };

        using var form = new SettingsForm(settings);
        SplitSettingsPage page = form.PageHost.GetOrCreatePage<SplitSettingsPage>(SettingsPageId.Splits);

        AssertEqual("At least 3", page.ConditionMatchModeBoxForTests.SelectedItem?.ToString());
        AssertEqual("3", page.ConditionMatchModeBoxForTests.Text);
        page.ConditionListForTests.SelectedIndex = 0;
        InvokePrivate(page, "RemoveSelectedFact");

        form.ApplyForTests();

        SplitCondition condition = form.Result.Route.SplitRoute.Single().Condition;
        AssertEqual(SplitConditionKind.AtLeast, SplitConditionKind.Normalize(condition.Kind));
        AssertEqual(2, condition.Value);
        AssertEqual(3, condition.Children.Count);
    });
}

static void TestSettingsFormEditsItemQuantityFromSelectedCondition()
{
    RunSta(() =>
    {
        var settings = new AppSettings
        {
            Route =
            {
                SplitRoute =
                [
                    new SplitRouteEntry
                    {
                        Id = "split:mixed",
                        DisplayName = "Mixed",
                        Enabled = true,
                        Condition = SplitCondition.All(
                        [
                            SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                            SplitCatalog.CreateItemEverOwnedCondition(50, 2)
                        ]),
                        IconTargetIds = [SplitCatalog.Skeletron, SplitCatalog.CreateItemTargetId(50)]
                    }
                ]
            }
        };

        using var form = new SettingsForm(settings);
        SplitSettingsPage page = form.PageHost.GetOrCreatePage<SplitSettingsPage>(SettingsPageId.Splits);

        page.ConditionListForTests.SelectedIndex = 0;
        AssertEqual(false, page.ItemQuantityBoxForTests.Enabled);
        page.TargetKindBoxForTests.SelectedIndex = 0;
        AssertEqual(false, page.ItemQuantityBoxForTests.Enabled);

        page.ConditionListForTests.SelectedIndex = 1;
        AssertEqual(true, page.ItemQuantityBoxForTests.Enabled);
        AssertEqual("2", page.ItemQuantityBoxForTests.Text);
        page.TargetKindBoxForTests.SelectedIndex = 1;
        AssertEqual(true, page.ItemQuantityBoxForTests.Enabled);

        page.ItemQuantityBoxForTests.Text = "5";
        form.ApplyForTests();

        SplitCondition itemCondition = form.Result.Route.SplitRoute.Single()
            .Condition
            .GetFactConditions()
            .Single(condition => string.Equals(
                condition.FactKey,
                SplitCatalog.CreateItemEverOwnedFactKey(50),
                StringComparison.OrdinalIgnoreCase));
        AssertEqual(5, itemCondition.Value);
    });
}

static void TestSettingsFormSearchesItemTargetsByName()
{
    RunSta(() =>
    {
        AssertEqual(true, TerrariaItemCatalog.ById.TryGetValue(50, out TerrariaItemDefinition magicMirror));
        AssertEqual("Magic Mirror", magicMirror.DisplayName);
        AssertEqual("魔镜", magicMirror.ChineseName);

        using var form = new SettingsForm(new AppSettings());
        SplitSettingsPage page = form.PageHost.GetOrCreatePage<SplitSettingsPage>(SettingsPageId.Splits);

        AssertEqual("Boss", page.TargetKindBoxForTests.SelectedItem?.ToString());
        page.TargetKindBoxForTests.SelectedIndex = 1;
        AssertEqual(1, page.TargetListForTests.Items.Count);
        AssertEqual("Too many results", page.TargetListForTests.Items[0]?.ToString());

        page.TargetSearchBoxForTests.Text = "a";
        AssertEqual(true, page.TargetListForTests.Items.Count > 0);

        page.TargetSearchBoxForTests.Text = "Magic Mirror";
        AssertEqual(true, ContainsTargetListItem(page.TargetListForTests, "Magic Mirror", "(Item:50)"));

        page.TargetSearchBoxForTests.Text = "MagicMirror";
        AssertEqual(true, ContainsTargetListItem(page.TargetListForTests, "Magic Mirror", "(Item:50)"));

        page.TargetSearchBoxForTests.Text = "魔镜";
        AssertEqual(true, ContainsTargetListItem(page.TargetListForTests, "Magic Mirror", "(Item:50)"));
    });
}

static void TestSettingsFormSearchesNpcTargetsByName()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        SplitSettingsPage page = form.PageHost.GetOrCreatePage<SplitSettingsPage>(SettingsPageId.Splits);

        page.TargetKindBoxForTests.SelectedIndex = 2;
        page.TargetSearchBoxForTests.Text = "Merchant";
        AssertEqual(true, ContainsTargetListItem(page.TargetListForTests, "Merchant", "(NPC:17)"));

        page.TargetSearchBoxForTests.Text = "商人";
        AssertEqual(true, ContainsTargetListItem(page.TargetListForTests, "Merchant", "(NPC:17)"));

        SelectTargetListItem(page.TargetListForTests, "Merchant", "(NPC:17)");
        InvokePrivate(page, "AddTargetToNewGroup");
        form.ApplyForTests();

        SplitRouteEntry added = form.Result.Route.SplitRoute.Last();
        AssertEqual(SplitCatalog.CreateNpcTargetId(17), added.IconTargetIds.Single());
        SplitCondition addedCondition = added.Condition.GetFactConditions().Single();
        AssertEqual(SplitCatalog.CreateNpcPresentFactKey(17), addedCondition.FactKey);
    });
}

static void TestSettingsFormAddsSelectedTargetToNewGroup()
{
    RunSta(() =>
    {
        var settings = new AppSettings { Route = { SplitRoute = [
                CreateTestRouteEntry("split:skeletron", "Skeletron", SplitCatalog.Skeletron)
            ] } };

        using var form = new SettingsForm(settings);
        SplitSettingsPage page = form.PageHost.GetOrCreatePage<SplitSettingsPage>(SettingsPageId.Splits);

        page.TargetKindBoxForTests.SelectedIndex = 1;
        page.TargetSearchBoxForTests.Text = "Magic Mirror";
        SelectTargetListItem(page.TargetListForTests, "Magic Mirror", "(Item:50)");
        int initialRouteCount = page.RouteListForTests.Items.Count;

        AssertEqual("Add to new group", page.AddTargetToNewGroupButtonForTests.Text);
        InvokePrivate(page, "AddTargetToNewGroup");

        AssertEqual(initialRouteCount + 1, page.RouteListForTests.Items.Count);
        AssertEqual("Magic Mirror", page.SplitNameBoxForTests.Text);
        AssertEqual(true, page.SplitEnabledBoxForTests.Checked);
        AssertEqual(true, page.ConditionListForTests.Items.Cast<object>().Any(item =>
            string.Equals(item.ToString(), "Magic Mirror >= 1", StringComparison.Ordinal)));

        form.ApplyForTests();

        SplitRouteEntry added = form.Result.Route.SplitRoute.Last();
        AssertEqual("Magic Mirror", added.DisplayName);
        AssertEqual(true, added.Enabled);
        AssertEqual(SplitCatalog.CreateItemTargetId(50), added.IconTargetIds.Single());
        SplitCondition addedCondition = added.Condition.GetFactConditions().Single();
        AssertEqual(SplitCatalog.CreateItemEverOwnedFactKey(50), addedCondition.FactKey);
        AssertEqual(SplitFactComparison.AtLeast, SplitFactComparison.Normalize(addedCondition.Comparison));
        AssertEqual(1, addedCondition.Value);
    });
}

static void TestSettingsFormLocalizesTargetLibraryAndConditions()
{
    RunSta(() =>
    {
        var settings = new AppSettings
        {
            General =
            {
                Language = LanguageNames.Chinese
            },
            Route =
            {
                SplitRoute =
                [
                    new SplitRouteEntry
                    {
                        Id = "split:custom-skeletron",
                        DisplayName = "Skeletron",
                        Enabled = true,
                        Condition = SplitCondition.All(
                        [
                            SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                            SplitCatalog.CreateItemEverOwnedCondition(50, 2)
                        ]),
                        IconTargetIds = [SplitCatalog.Skeletron, SplitCatalog.CreateItemTargetId(50)]
                    }
                ]
            }
        };

        using var form = new SettingsForm(settings);
        SplitSettingsPage page = form.PageHost.GetOrCreatePage<SplitSettingsPage>(SettingsPageId.Splits);

        AssertEqual("Skeletron", page.RouteListForTests.Items[0]?.ToString());
        AssertEqual("Skeletron", page.SplitNameBoxForTests.Text);
        AssertEqual(true, page.ConditionListForTests.Items.Cast<object>().Any(item =>
            string.Equals(item.ToString(), "骷髅王", StringComparison.Ordinal)));
        AssertEqual(true, page.ConditionListForTests.Items.Cast<object>().Any(item =>
            string.Equals(item.ToString(), "魔镜 >= 2", StringComparison.Ordinal)));

        page.TargetKindBoxForTests.SelectedIndex = 1;
        page.TargetSearchBoxForTests.Text = "Magic Mirror";
        AssertEqual(true, ContainsTargetListItem(page.TargetListForTests, "魔镜", "(Item:50)"));
        AssertEqual(false, ContainsTargetListItem(page.TargetListForTests, "Magic Mirror"));

        page.TargetSearchBoxForTests.Text = "魔镜";
        AssertEqual(true, ContainsTargetListItem(page.TargetListForTests, "魔镜", "(Item:50)"));

        page.TargetKindBoxForTests.SelectedIndex = 0;
        page.TargetSearchBoxForTests.Text = "Skeletron";
        AssertEqual(true, ContainsTargetListItem(page.TargetListForTests, "骷髅王", "(Boss:skeletron)"));
        AssertEqual(false, ContainsTargetListItem(page.TargetListForTests, "Skeletron"));

        page.TargetSearchBoxForTests.Text = "骷髅王";
        AssertEqual(true, ContainsTargetListItem(page.TargetListForTests, "骷髅王", "(Boss:skeletron)"));

        IReadOnlyList<SplitConditionDataRow> rows = SplitConditionDataRows.Build(settings);
        AssertEqual(true, rows.Any(row => string.Equals(row.DisplayName, "Skeletron：骷髅王", StringComparison.Ordinal)));
        AssertEqual(true, rows.Any(row => string.Equals(row.DisplayName, "Skeletron：魔镜 >= 2", StringComparison.Ordinal)));
    });
}

static void TestSettingsFormUpdatesEffectsRouteRowsDynamically()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings { Route = { SplitRoute = [
                CreateTestRouteEntry("split:skeletron", "Skeletron", SplitCatalog.Skeletron)
            ] } });
        AnimationSettingsPage page = form.PageHost.GetOrCreatePage<AnimationSettingsPage>(SettingsPageId.Effects);

        AssertEqual(true, page.AnimationOutlineKeysForTests.Contains("split:skeletron", StringComparer.OrdinalIgnoreCase));
        AssertEqual(true, page.SegmentBestDeltaHighlightKeysForTests.Contains("split:skeletron", StringComparer.OrdinalIgnoreCase));

        form.Result.Route.SplitRoute =
        [
            CreateTestRouteEntry("split:moon-lord", "Moon Lord", SplitCatalog.MoonLord)
        ];
        AppSettingsStore.Normalize(form.Result);
        form.PageHost.NotifyModelChanged(SettingsModelChange.RouteChanged);

        AssertEqual(false, page.AnimationOutlineKeysForTests.Contains("split:skeletron", StringComparer.OrdinalIgnoreCase));
        AssertEqual(false, page.SegmentBestDeltaHighlightKeysForTests.Contains("split:skeletron", StringComparer.OrdinalIgnoreCase));
        AssertEqual(true, page.AnimationOutlineKeysForTests.Contains("split:moon-lord", StringComparer.OrdinalIgnoreCase));
        AssertEqual(true, page.SegmentBestDeltaHighlightKeysForTests.Contains("split:moon-lord", StringComparer.OrdinalIgnoreCase));
    });
}

static bool ContainsTargetListItem(ListBox listBox, params string[] fragments)
{
    return listBox.Items.Cast<object>().Any(item =>
    {
        string text = item.ToString() ?? string.Empty;
        return fragments.All(fragment => text.Contains(fragment, StringComparison.Ordinal));
    });
}

static void SelectTargetListItem(ListBox listBox, params string[] fragments)
{
    for (int i = 0; i < listBox.Items.Count; i++)
    {
        string text = listBox.Items[i]?.ToString() ?? string.Empty;
        if (fragments.All(fragment => text.Contains(fragment, StringComparison.Ordinal)))
        {
            listBox.SelectedIndex = i;
            return;
        }
    }

    throw new InvalidOperationException($"Target list item not found: {string.Join(", ", fragments)}");
}

static void SelectComboBoxItem(ThemedDropDownList comboBox, params string[] fragments)
{
    for (int i = 0; i < comboBox.Items.Count; i++)
    {
        string text = comboBox.Items[i]?.ToString() ?? string.Empty;
        if (fragments.All(fragment => text.Contains(fragment, StringComparison.Ordinal)))
        {
            comboBox.SelectedIndex = i;
            return;
        }
    }

    throw new InvalidOperationException($"Combo box item not found: {string.Join(", ", fragments)}");
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

        AssertEqual("Zenith|Skyblock", form.Result.Automation.AutoCreate.SpecialSeeds);
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

        AssertEqual("Drunk|The Constant", form.Result.Automation.AutoCreate.SpecialSeeds);
    });
}

static void TestSettingsFormSavesPyramidItemFilter()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings
        {
            Automation =
            {
                AutoCreate = new AutoCreateWorldSettings
                {
                    EnablePyramidFilter = false,
                    PyramidFilterItemMask = AutoCreatePyramidFilterItem.FlyingCarpetMask,
                    ReturnToMainMenuOnFilterFailure = false
                }
            }
        });
        AutomationSettingsPage page = form.PageHost.GetOrCreatePage<AutomationSettingsPage>(SettingsPageId.Automation);

        AssertEqual(false, page.AutoCreatePyramidItemBoxes[AutoCreatePyramidFilterItem.FlyingCarpet].Enabled);
        AssertEqual(false, page.AutoCreateReturnToMainMenuOnFilterFailureBox.Enabled);

        page.AutoCreatePyramidFilterBox.Checked = true;
        page.AutoCreateReturnToMainMenuOnFilterFailureBox.Checked = true;
        page.AutoCreatePyramidItemBoxes[AutoCreatePyramidFilterItem.SandstormInABottle].Checked = true;
        page.AutoCreatePyramidItemBoxes[AutoCreatePyramidFilterItem.FlyingCarpet].Checked = false;
        page.AutoCreatePyramidItemBoxes[AutoCreatePyramidFilterItem.PharaohSet].Checked = true;

        AssertEqual(true, page.AutoCreatePyramidItemBoxes[AutoCreatePyramidFilterItem.SandstormInABottle].Enabled);
        AssertEqual(true, page.AutoCreateReturnToMainMenuOnFilterFailureBox.Enabled);

        form.ApplyForTests();

        AssertEqual(true, form.Result.Automation.AutoCreate.EnablePyramidFilter);
        AssertEqual(true, form.Result.Automation.AutoCreate.ReturnToMainMenuOnFilterFailure);
        AssertEqual(
            AutoCreatePyramidFilterItem.SandstormInABottleMask | AutoCreatePyramidFilterItem.PharaohSetMask,
            form.Result.Automation.AutoCreate.PyramidFilterItemMask);
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

        AssertEqual(true, form.Result.Automation.AutoCreate.EnableZenithStarCatch);
        AssertEqual(AutoCreateZenithStarCatchStage.GemCaves, form.Result.Automation.AutoCreate.ZenithStarCatchStopStage);
        AssertEqual(500, form.Result.Automation.AutoCreate.ZenithStarCatchSpeedSliderValue);
        AssertEqual(true, form.Result.Automation.AutoCreate.EnablePyramidFilter);
    });
}

static void TestSettingsFormGatesZenithStarCatchBehindZenithSeed()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings
        {
            Automation =
            {
                AutoCreate = new AutoCreateWorldSettings
                {
                    EnableZenithStarCatch = true,
                    ZenithStarCatchStopStage = AutoCreateZenithStarCatchStage.Pots
                }
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

static void TestSettingsFormKeepsWorldPoolIndependentFromPyramidFilter()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings
        {
            Automation =
            {
                AutoCreate = new AutoCreateWorldSettings
                {
                    EnablePyramidFilter = false,
                    EnableWorldPool = true,
                    WorldPoolTargetCount = 10
                }
            }
        });
        AutomationSettingsPage page = form.PageHost.GetOrCreatePage<AutomationSettingsPage>(SettingsPageId.Automation);

        AssertEqual(true, page.AutoCreateWorldPoolBox.Enabled);
        AssertEqual(true, page.AutoCreateWorldPoolTargetBox.Enabled);
        AssertEqual(true, page.AutoCreateWorldPoolBox.Checked);
        AssertEqual("10", page.AutoCreateWorldPoolTargetBox.Text);

        page.AutoCreatePyramidFilterBox.Checked = true;

        AssertEqual(true, page.AutoCreateWorldPoolBox.Enabled);
        AssertEqual(true, page.AutoCreateWorldPoolTargetBox.Enabled);

        page.AutoCreateWorldPoolBox.Checked = false;

        AssertEqual(true, page.AutoCreateWorldPoolBox.Enabled);
        AssertEqual(false, page.AutoCreateWorldPoolTargetBox.Enabled);
    });
}

static void TestDebugSequenceUsesPooledWorldPath()
{
    var settings = new AppSettings
    {
        Automation =
        {
            AutoCreate = new AutoCreateWorldSettings
            {
                WorldSize = AutoCreateWorldSize.Small,
                WorldDifficulty = AutoCreateWorldDifficulty.Expert,
                WorldEvil = AutoCreateWorldEvil.Crimson,
                SpecialSeeds = AutoCreateSpecialWorldSeed.ForTheWorthy,
                EnablePyramidFilter = false,
                EnableWorldPool = true
            }
        }
    };
    var window = new TerrariaWindowSnapshot(
        HasProcess: false,
        ProcessId: null,
        ProcessStartTime: null,
        IsResponding: false,
        HasWindow: false,
        WindowHandle: IntPtr.Zero,
        WindowTitle: string.Empty,
        IsVisible: false,
        IsMinimized: false,
        IsMaximized: false,
        IsForeground: false,
        WindowBounds: null,
        ClientSize: new Size(900, 900),
        Status: string.Empty);
    DebugSettingsSnapshot snapshot = DebugSettingsSnapshotBuilder.Build(
        window,
        RuntimeDebugSnapshot.Empty,
        new TerrariaSaveInventorySnapshot(0, 0, 0, 0),
        settings.Automation.AutoCreate,
        settings.Advanced,
        static () => 1,
        static key => key);
    string sequence = snapshot.Automation.AutoCreateSequence;

    AssertEqual(true, sequence.Contains("Install pooled world", StringComparison.Ordinal));
    AssertEqual(true, sequence.Contains("Stop at world select", StringComparison.Ordinal));
    AssertEqual(false, sequence.Contains("New World", StringComparison.Ordinal));
    AssertEqual(false, sequence.Contains("Advanced Seed", StringComparison.Ordinal));
    AssertEqual(false, sequence.Contains("Create World", StringComparison.Ordinal));
    AssertEqual(false, sequence.Contains("World size", StringComparison.Ordinal));
    AssertEqual(false, sequence.Contains("World difficulty", StringComparison.Ordinal));
    AssertEqual(false, sequence.Contains("World evil", StringComparison.Ordinal));
    AssertEqual(false, sequence.Contains("Special seeds", StringComparison.Ordinal));
    AssertEqual(false, sequence.Contains("Randomize Visible Seed", StringComparison.Ordinal));
    AssertEqual(false, sequence.Contains("Filter pyramid", StringComparison.Ordinal));
}

static void TestSettingsFormAppliesTimerStartSound()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings());
        SoundSettingsPage page = form.PageHost.GetOrCreatePage<SoundSettingsPage>(SettingsPageId.Sounds);
        page.SoundTextBoxes[nameof(UiSoundSettings.EnterWorld)].Text = "sounds\\timer-start.wav";

        form.ApplyForTests();

        AssertEqual("sounds\\timer-start.wav", form.Result.Overlay.Sounds.EnterWorld);
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
        AssertEqual(new Rectangle(1344, 180, 1512, 241), corridor);

        string emptyWorld = Path.Combine(directory, "empty.wld");
        WriteSyntheticWorldFile(emptyWorld, dimensions);
        AssertEqual(true, scanner.TryReadWorldSeedAndEvil(emptyWorld, out string seedText, out bool hasCrimson, out _));
        AssertEqual("server-picked-seed", seedText);
        AssertEqual(true, hasCrimson);
        AssertEqual(true, scanner.TryReadWorldSeedMetadata(emptyWorld, out TerrariaWorldSeedMetadata metadata, out _));
        AssertEqual("size=1, difficulty=1, evil=2, special=0", metadata.FormatWorldOptions());
    }
    finally
    {
        DeleteDirectoryIfExists(directory);
    }
}

static void TestPyramidScannerReadsChestContents()
{
    string directory = GetPublishOutputDirectory("test-output", "pyramid-chest-tests");
    Directory.CreateDirectory(directory);
    try
    {
        var scanner = new TerrariaWorldFilePyramidScanner();
        TerrariaWorldDimensions dimensions = TerrariaWorldDimensions.FromWorldSize(AutoCreateWorldSize.Small);
        Rectangle corridor = TerrariaWorldFilePyramidScanner.BuildSpeedrunCorridorBounds(dimensions);
        int chestX = corridor.Left + corridor.Width / 2 + 12;
        int chestY = corridor.Top + 35;

        string chestWorld = Path.Combine(directory, "candidate-chest.wld");
        WriteSyntheticWorldFile(
            chestWorld,
            dimensions,
            chests:
            [
                new SyntheticChest(
                    chestX,
                    chestY,
                    [
                        new SyntheticChestItem(0, 857),
                        new SyntheticChestItem(1, 279, 250)
                    ]),
                new SyntheticChest(
                    12,
                    chestY,
                    [
                        new SyntheticChestItem(0, ScannerPyramidChestItemNames.FlyingCarpet)
                    ]),
                new SyntheticChest(
                    corridor.Left + 4,
                    chestY,
                    [
                        new SyntheticChestItem(0, 8, 12)
                    ])
            ]);

        bool scanned = scanner.TryScanCandidateItemChests(
            chestWorld,
            AutoCreateWorldSize.Small,
            AutoCreatePyramidFilterItem.SandstormInABottleMask,
            out PyramidChestScanResult result,
            out Rectangle candidateBounds,
            out string detail);
        if (!scanned)
        {
            throw new InvalidOperationException(detail);
        }

        AssertEqual(string.Empty, detail);
        AssertEqual(corridor, candidateBounds);
        AssertEqual(1, result.Chests.Count);
        PyramidChestInfo chest = result.Chests[0];
        AssertEqual(chestX, chest.X);
        AssertEqual(chestY, chest.Y);
        AssertEqual(true, chest.ContainsItem(857));
        AssertEqual(false, chest.ContainsItem(934));
        AssertEqual(true, result.ContainsItem(ScannerPyramidChestItemNames.SandstormInABottle));
        AssertEqual(false, result.ContainsItem(ScannerPyramidChestItemNames.FlyingCarpet));
        AssertEqual(true, PyramidFilterItemMatcher.Matches(result, AutoCreatePyramidFilterItem.SandstormInABottleMask));
        AssertEqual(false, PyramidFilterItemMatcher.Matches(result, AutoCreatePyramidFilterItem.FlyingCarpetMask));
        AssertEqual(false, PyramidFilterItemMatcher.Matches(result, AutoCreatePyramidFilterItem.PharaohSetMask));
        AssertEqual(true, result.FormatSummary().Contains("Sandstorm in a Bottle", StringComparison.Ordinal));
        AssertEqual(true, result.FormatSummary().Contains("#279 x250", StringComparison.Ordinal));
        AssertEqual("none", default(PyramidChestScanResult).FormatSummary());
        AssertEqual(false, default(PyramidChestScanResult).ContainsItem(ScannerPyramidChestItemNames.SandstormInABottle));
        AssertEqual("(0,0): empty", default(PyramidChestInfo).FormatSummary());

        AssertEqual(true, scanner.TryScanCandidateItemChests(
            chestWorld,
            AutoCreateWorldSize.Small,
            0,
            out PyramidChestScanResult emptyMaskResult,
            out _,
            out _));
        AssertEqual(1, emptyMaskResult.Chests.Count);

        AssertEqual(true, scanner.TryScanCandidateItemChests(
            chestWorld,
            AutoCreateWorldSize.Small,
            AutoCreatePyramidFilterItem.FlyingCarpetMask,
            out PyramidChestScanResult outOfRangeResult,
            out _,
            out _));
        AssertEqual(0, outOfRangeResult.Chests.Count);

        AssertEqual(true, scanner.TryScanCandidateItemChests(
            chestWorld,
            AutoCreateWorldSize.Small,
            AutoCreatePyramidFilterItem.PharaohSetMask,
            out PyramidChestScanResult missingPharaohResult,
            out _,
            out _));
        AssertEqual(0, missingPharaohResult.Chests.Count);

        var pharaohResult = new PyramidChestScanResult(
        [
            new PyramidChestInfo(
                chestX,
                chestY,
                [
                    new ScannerPyramidChestItem(0, ScannerPyramidChestItemNames.PharaohMask, 1, 0),
                    new ScannerPyramidChestItem(1, ScannerPyramidChestItemNames.PharaohRobe, 1, 0)
                ])
        ]);
        var partialPharaohResult = new PyramidChestScanResult(
        [
            new PyramidChestInfo(
                chestX,
                chestY,
                [
                    new ScannerPyramidChestItem(0, ScannerPyramidChestItemNames.PharaohMask, 1, 0)
                ])
        ]);
        AssertEqual(true, PyramidFilterItemMatcher.Matches(pharaohResult, AutoCreatePyramidFilterItem.PharaohSetMask));
        AssertEqual(false, PyramidFilterItemMatcher.Matches(partialPharaohResult, AutoCreatePyramidFilterItem.PharaohSetMask));
        AssertEqual(false, PyramidFilterItemMatcher.Matches(partialPharaohResult, 0));
    }
    finally
    {
        DeleteDirectoryIfExists(directory);
    }
}

static void TestPyramidFilterFastOpensAfterGenerationStateEnds()
{
    string directory = GetPublishOutputDirectory("test-output", "pyramid-fast-open-tests");
    string worldsDirectory = Path.Combine(directory, "Worlds");
    Directory.CreateDirectory(worldsDirectory);
    try
    {
        TerrariaWorldDimensions dimensions = TerrariaWorldDimensions.FromWorldSize(AutoCreateWorldSize.Small);
        Rectangle corridor = TerrariaWorldFilePyramidScanner.BuildSpeedrunCorridorBounds(dimensions);
        string worldPath = Path.Combine(worldsDirectory, "fast-open.wld");
        WriteSyntheticWorldFile(
            worldPath,
            dimensions,
            chests:
            [
                new SyntheticChest(
                    corridor.Left + 8,
                    corridor.Top + 8,
                    [
                        new SyntheticChestItem(0, ScannerPyramidChestItemNames.FlyingCarpet)
                    ])
            ]);

        var filter = new PyramidFilterAutomation(
            new TerrariaAutomationContext("test pyramid filter"),
            watcherFactory: () => new SequenceGenerationWatcher(
                GenerationSnapshot(hasGeneration: true),
                GenerationSnapshot(hasGeneration: false)),
            worldsDirectoryProvider: () => worldsDirectory,
            waitTimings: new PyramidFilterWaitTimings(
                WorldFileTimeout: TimeSpan.FromSeconds(2),
                LegacyPollInterval: TimeSpan.FromMilliseconds(10),
                LegacyStableFileDuration: TimeSpan.FromSeconds(5),
                GenerationPollInterval: TimeSpan.FromMilliseconds(1),
                FastOpenTimeout: TimeSpan.FromMilliseconds(1000)));

        PyramidFilterOutcome outcome = filter.RunAsync(
            new AutoCreateWorldSettings
            {
                EnablePyramidFilter = true,
                WorldSize = AutoCreateWorldSize.Small,
                PyramidFilterItemMask = 0
            },
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase),
            CancellationToken.None).GetAwaiter().GetResult();

        AssertEqual(PyramidFilterOutcome.Kept, outcome);
    }
    finally
    {
        DeleteDirectoryIfExists(directory);
    }
}

static void TestPyramidFilterFallsBackWithoutGenerationState()
{
    string directory = GetPublishOutputDirectory("test-output", "pyramid-fallback-tests");
    string worldsDirectory = Path.Combine(directory, "Worlds");
    Directory.CreateDirectory(worldsDirectory);
    try
    {
        TerrariaWorldDimensions dimensions = TerrariaWorldDimensions.FromWorldSize(AutoCreateWorldSize.Small);
        Rectangle corridor = TerrariaWorldFilePyramidScanner.BuildSpeedrunCorridorBounds(dimensions);
        string worldPath = Path.Combine(worldsDirectory, "fallback.wld");
        WriteSyntheticWorldFile(
            worldPath,
            dimensions,
            chests:
            [
                new SyntheticChest(
                    corridor.Left + 8,
                    corridor.Top + 8,
                    [
                        new SyntheticChestItem(0, ScannerPyramidChestItemNames.SandstormInABottle)
                    ])
            ]);

        var filter = new PyramidFilterAutomation(
            new TerrariaAutomationContext("test pyramid filter"),
            watcherFactory: () => new SequenceGenerationWatcher(GenerationSnapshot(hasGeneration: false)),
            worldsDirectoryProvider: () => worldsDirectory,
            waitTimings: new PyramidFilterWaitTimings(
                WorldFileTimeout: TimeSpan.FromSeconds(1),
                LegacyPollInterval: TimeSpan.FromMilliseconds(5),
                LegacyStableFileDuration: TimeSpan.FromMilliseconds(20),
                GenerationPollInterval: TimeSpan.FromMilliseconds(5),
                FastOpenTimeout: TimeSpan.FromMilliseconds(50)));

        PyramidFilterOutcome outcome = filter.RunAsync(
            new AutoCreateWorldSettings
            {
                EnablePyramidFilter = true,
                WorldSize = AutoCreateWorldSize.Small,
                PyramidFilterItemMask = 0
            },
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase),
            CancellationToken.None).GetAwaiter().GetResult();

        AssertEqual(PyramidFilterOutcome.Kept, outcome);
    }
    finally
    {
        DeleteDirectoryIfExists(directory);
    }
}

static void TestPyramidFilterTreatsEmptyItemMaskAsAllCandidateItems()
{
    string directory = GetPublishOutputDirectory("test-output", "pyramid-empty-mask-tests");
    string worldsDirectory = Path.Combine(directory, "Worlds");
    Directory.CreateDirectory(worldsDirectory);
    try
    {
        TerrariaWorldDimensions dimensions = TerrariaWorldDimensions.FromWorldSize(AutoCreateWorldSize.Small);
        Rectangle corridor = TerrariaWorldFilePyramidScanner.BuildSpeedrunCorridorBounds(dimensions);
        string worldPath = Path.Combine(worldsDirectory, "no-candidate-item.wld");
        WriteSyntheticWorldFile(worldPath, dimensions);

        var filter = new PyramidFilterAutomation(
            new TerrariaAutomationContext("test pyramid filter"),
            watcherFactory: () => new SequenceGenerationWatcher(GenerationSnapshot(hasGeneration: false)),
            worldsDirectoryProvider: () => worldsDirectory,
            waitTimings: new PyramidFilterWaitTimings(
                WorldFileTimeout: TimeSpan.FromSeconds(1),
                LegacyPollInterval: TimeSpan.FromMilliseconds(5),
                LegacyStableFileDuration: TimeSpan.FromMilliseconds(20),
                GenerationPollInterval: TimeSpan.FromMilliseconds(5),
                FastOpenTimeout: TimeSpan.FromMilliseconds(50)));

        PyramidFilterOutcome outcome = filter.RunAsync(
            new AutoCreateWorldSettings
            {
                EnablePyramidFilter = true,
                WorldSize = AutoCreateWorldSize.Small,
                PyramidFilterItemMask = 0
            },
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase),
            CancellationToken.None).GetAwaiter().GetResult();

        AssertEqual(PyramidFilterOutcome.Rejected, outcome);
    }
    finally
    {
        DeleteDirectoryIfExists(directory);
    }
}

static void TestPyramidSeedPreScreenScope()
{
    var settings = new AutoCreateWorldSettings
    {
        EnablePyramidFilter = true,
        WorldSize = AutoCreateWorldSize.Small,
        WorldEvil = AutoCreateWorldEvil.Crimson,
        SpecialSeeds = string.Empty,
        SecretSeeds = string.Empty
    };

    AssertEqual(true, PyramidSeedPreScreenAutomation.IsEnabledFor(settings));

    settings.WorldSize = AutoCreateWorldSize.Medium;
    AssertEqual(false, PyramidSeedPreScreenAutomation.IsEnabledFor(settings));

    settings.WorldSize = AutoCreateWorldSize.Small;
    settings.WorldEvil = AutoCreateWorldEvil.Corruption;
    AssertEqual(false, PyramidSeedPreScreenAutomation.IsEnabledFor(settings));

    settings.WorldEvil = AutoCreateWorldEvil.Crimson;
    settings.SpecialSeeds = AutoCreateSpecialWorldSeed.ForTheWorthy;
    AssertEqual(false, PyramidSeedPreScreenAutomation.IsEnabledFor(settings));

    settings.SpecialSeeds = string.Empty;
    settings.SecretSeeds = "mole people";
    AssertEqual(false, PyramidSeedPreScreenAutomation.IsEnabledFor(settings));

    settings.SecretSeeds = string.Empty;
    settings.EnablePyramidFilter = false;
    AssertEqual(false, PyramidSeedPreScreenAutomation.IsEnabledFor(settings));
}

static void TestPyramidSeedPreScreenLoopAcceptsAfterRejectedSeed()
{
    var randomizer = new FakePyramidSeedRandomizer();
    var reader = new FakePyramidVisibleSeedReader("100", [
        PyramidVisibleSeedReadResult.FromSeed("101", 1),
        PyramidVisibleSeedReadResult.FromSeed("102", 1)
    ]);
    var evaluator = new FakePyramidSeedPreScreenEvaluator(seedText => new PyramidSeedPreScreenPrediction(
        default,
        "all",
        CanUsePrediction: true,
        AcceptSeed: seedText == "102",
        RejectReason: seedText == "102" ? string.Empty : "no pyramid"));
    var loop = new PyramidSeedPreScreenLoop(evaluator, _ => { });

    PyramidSeedPreScreenLoopResult result = loop.RunAsync(
        new AutoCreateWorldSettings(),
        randomizer,
        reader,
        CancellationToken.None).GetAwaiter().GetResult();

    AssertEqual(PyramidSeedPreScreenLoopStatus.Accepted, result.Status);
    AssertEqual("102", result.AcceptedSeed);
    AssertEqual(2, result.Attempts);
    AssertEqual(2, randomizer.Attempts);
    AssertEqual("100|101", string.Join("|", reader.PreviousSeedsSeen));
}

static void TestPyramidSeedPreScreenLoopStopsFirstRejectionWithoutLocalRetry()
{
    var randomizer = new FakePyramidSeedRandomizer();
    var reader = new FakePyramidVisibleSeedReader("100", [
        PyramidVisibleSeedReadResult.FromSeed("101", 1),
        PyramidVisibleSeedReadResult.FromSeed("102", 1)
    ]);
    var evaluator = new FakePyramidSeedPreScreenEvaluator(seedText => new PyramidSeedPreScreenPrediction(
        default,
        "all",
        CanUsePrediction: true,
        AcceptSeed: false,
        RejectReason: "no pyramid"));
    var loop = new PyramidSeedPreScreenLoop(evaluator, _ => { });

    PyramidSeedPreScreenLoopResult result = loop.RunAsync(
        new AutoCreateWorldSettings
        {
            ReturnToMainMenuOnFilterFailure = true
        },
        randomizer,
        reader,
        CancellationToken.None).GetAwaiter().GetResult();

    AssertEqual(PyramidSeedPreScreenLoopStatus.RejectedSeed, result.Status);
    AssertEqual<string?>(null, result.AcceptedSeed);
    AssertEqual(1, result.Attempts);
    AssertEqual(1, randomizer.Attempts);
    AssertEqual("100", string.Join("|", reader.PreviousSeedsSeen));
}

static void TestPyramidSeedPreScreenLoopRetriesTransientSeedReadFailure()
{
    var randomizer = new FakePyramidSeedRandomizer();
    var reader = new FakePyramidVisibleSeedReader("200", [
        PyramidVisibleSeedReadResult.Failed(TerrariaWorldCreationSeedStatus.NotOnWorldCreationPage, 40, "200"),
        PyramidVisibleSeedReadResult.FromSeed("201", 1)
    ]);
    var evaluator = new FakePyramidSeedPreScreenEvaluator(seedText => new PyramidSeedPreScreenPrediction(
        default,
        "all",
        CanUsePrediction: true,
        AcceptSeed: seedText == "201",
        RejectReason: seedText == "201" ? string.Empty : "no pyramid"));
    var loop = new PyramidSeedPreScreenLoop(evaluator, _ => { });

    PyramidSeedPreScreenLoopResult result = loop.RunAsync(
        new AutoCreateWorldSettings(),
        randomizer,
        reader,
        CancellationToken.None).GetAwaiter().GetResult();

    AssertEqual(PyramidSeedPreScreenLoopStatus.Accepted, result.Status);
    AssertEqual("201", result.AcceptedSeed);
    AssertEqual(2, result.Attempts);
    AssertEqual(2, randomizer.Attempts);
    AssertEqual("200|200", string.Join("|", reader.PreviousSeedsSeen));
}

static void TestPyramidSeedPreScreenLoopDoesNotRetrySeedReadFailureWithoutLocalRetry()
{
    var randomizer = new FakePyramidSeedRandomizer();
    var reader = new FakePyramidVisibleSeedReader("200", [
        PyramidVisibleSeedReadResult.Failed(TerrariaWorldCreationSeedStatus.NotOnWorldCreationPage, 40, "200"),
        PyramidVisibleSeedReadResult.FromSeed("201", 1)
    ]);
    var evaluator = new FakePyramidSeedPreScreenEvaluator(_ => new PyramidSeedPreScreenPrediction(
        default,
        "all",
        CanUsePrediction: true,
        AcceptSeed: true,
        RejectReason: string.Empty));
    var loop = new PyramidSeedPreScreenLoop(evaluator, _ => { });

    PyramidSeedPreScreenLoopResult result = loop.RunAsync(
        new AutoCreateWorldSettings
        {
            ReturnToMainMenuOnFilterFailure = true
        },
        randomizer,
        reader,
        CancellationToken.None).GetAwaiter().GetResult();

    AssertEqual(PyramidSeedPreScreenLoopStatus.SeedReadFailed, result.Status);
    AssertEqual<string?>(null, result.AcceptedSeed);
    AssertEqual(1, result.Attempts);
    AssertEqual(1, randomizer.Attempts);
    AssertEqual("200", string.Join("|", reader.PreviousSeedsSeen));
}

static void TestPyramidSeedPreScreenLoopStopsAfterRepeatedSeedReadFailures()
{
    var randomizer = new FakePyramidSeedRandomizer();
    var reader = new FakePyramidVisibleSeedReader("300", [
        PyramidVisibleSeedReadResult.Failed(TerrariaWorldCreationSeedStatus.NotOnWorldCreationPage, 40, "300"),
        PyramidVisibleSeedReadResult.Failed(TerrariaWorldCreationSeedStatus.NotOnWorldCreationPage, 40, "300"),
        PyramidVisibleSeedReadResult.Failed(TerrariaWorldCreationSeedStatus.NotOnWorldCreationPage, 40, "300")
    ]);
    var evaluator = new FakePyramidSeedPreScreenEvaluator(_ => new PyramidSeedPreScreenPrediction(
        default,
        "all",
        CanUsePrediction: true,
        AcceptSeed: true,
        RejectReason: string.Empty));
    var loop = new PyramidSeedPreScreenLoop(evaluator, _ => { });

    PyramidSeedPreScreenLoopResult result = loop.RunAsync(
        new AutoCreateWorldSettings(),
        randomizer,
        reader,
        CancellationToken.None).GetAwaiter().GetResult();

    AssertEqual(PyramidSeedPreScreenLoopStatus.SeedReadFailed, result.Status);
    AssertEqual<string?>(null, result.AcceptedSeed);
    AssertEqual(3, result.Attempts);
    AssertEqual(3, randomizer.Attempts);
    AssertEqual("300|300|300", string.Join("|", reader.PreviousSeedsSeen));
}

static void TestPyramidSeedPreScreenDungeonBoundaryRisk()
{
    var leftDungeon = new WorldGenState(new WorldOptions(1, WorldDimensions.Small, 1, true, 0))
    {
        DungeonSide = -1
    };
    AssertEqual(false, WorldInterestArea.IsInSkippedDungeonBoundaryUncertaintyBand(leftDungeon, 1343));
    AssertEqual(true, WorldInterestArea.IsInSkippedDungeonBoundaryUncertaintyBand(leftDungeon, 1344));
    AssertEqual(true, WorldInterestArea.IsInSkippedDungeonBoundaryUncertaintyBand(leftDungeon, 1469));
    AssertEqual(false, WorldInterestArea.IsInSkippedDungeonBoundaryUncertaintyBand(leftDungeon, 1470));
    AssertEqual(false, WorldInterestArea.IsInSkippedDungeonBoundaryUncertaintyBand(leftDungeon, 2100));

    var rightDungeon = new WorldGenState(new WorldOptions(1, WorldDimensions.Small, 1, true, 0))
    {
        DungeonSide = 1
    };
    AssertEqual(false, WorldInterestArea.IsInSkippedDungeonBoundaryUncertaintyBand(rightDungeon, 2100));
    AssertEqual(false, WorldInterestArea.IsInSkippedDungeonBoundaryUncertaintyBand(rightDungeon, 2729));
    AssertEqual(true, WorldInterestArea.IsInSkippedDungeonBoundaryUncertaintyBand(rightDungeon, 2730));
    AssertEqual(true, WorldInterestArea.IsInSkippedDungeonBoundaryUncertaintyBand(rightDungeon, 2855));
    AssertEqual(false, WorldInterestArea.IsInSkippedDungeonBoundaryUncertaintyBand(rightDungeon, 2856));
}

static void TestPyramidSeedPreScreenRejectsKnownOfficialNoTowerFalsePositives()
{
    foreach (string seed in new[] { "702683177", "349049665", "1944096670" })
    {
        var result = TerrariaSplit.Terraria.WorldGeneration.PyramidSeedPreScreen.EvaluateSmallCrimson(
            seed,
            difficultyCode: 1,
            requiredItemMask: 0);

        AssertEqual(false, result.HasTargetPyramid);
    }
}

static void TestPyramidSeedPreScreenPredictsKnownPyramidSeed()
{
    var result = TerrariaSplit.Terraria.WorldGeneration.PyramidSeedPreScreen.EvaluateSmallCrimson(
        "540278984",
        difficultyCode: 1,
        requiredItemMask: AutoCreatePyramidFilterItem.SandstormInABottleMask);

    AssertEqual(TerrariaSplit.Terraria.WorldGeneration.PyramidSeedPreScreenStatus.Complete, result.Status);
    AssertEqual(true, result.HasTargetPyramid);
    AssertEqual(true, result.MatchesRequiredItems);
    AssertEqual(true, result.LootSummary.Contains("Sandstorm in a Bottle", StringComparison.Ordinal));
}

static void TestPyramidSeedPreScreenEvaluatorRequiresSelectedItem()
{
    var evaluator = new PyramidSeedPreScreenEvaluator();
    var settings = new AutoCreateWorldSettings
    {
        EnablePyramidFilter = true,
        WorldSize = AutoCreateWorldSize.Small,
        WorldDifficulty = AutoCreateWorldDifficulty.Classic,
        WorldEvil = AutoCreateWorldEvil.Crimson,
        SpecialSeeds = string.Empty,
        SecretSeeds = string.Empty,
        PyramidFilterItemMask = AutoCreatePyramidFilterItem.FlyingCarpetMask
    };

    PyramidSeedPreScreenPrediction prediction = evaluator.Evaluate(settings, "540278984");

    AssertEqual(true, prediction.CanUsePrediction);
    AssertEqual(true, prediction.Result.HasTargetPyramid);
    AssertEqual(false, prediction.Result.MatchesRequiredItems);
    AssertEqual(false, prediction.AcceptSeed);
    AssertEqual("item mismatch", prediction.RejectReason);

    settings.PyramidFilterItemMask = AutoCreatePyramidFilterItem.SandstormInABottleMask;
    PyramidSeedPreScreenPrediction matchingPrediction = evaluator.Evaluate(settings, "540278984");

    AssertEqual(true, matchingPrediction.Result.MatchesRequiredItems);
    AssertEqual(true, matchingPrediction.AcceptSeed);
    AssertEqual(string.Empty, matchingPrediction.RejectReason);
}

static void TestPyramidSeedPreScreenKeepsFirstPyramidChest()
{
    var carpetResult = TerrariaSplit.Terraria.WorldGeneration.PyramidSeedPreScreen.EvaluateSmallCrimson(
        "1092653535",
        difficultyCode: 1,
        requiredItemMask: AutoCreatePyramidFilterItem.FlyingCarpetMask);

    AssertEqual(TerrariaSplit.Terraria.WorldGeneration.PyramidSeedPreScreenStatus.Complete, carpetResult.Status);
    AssertEqual(true, carpetResult.HasTargetPyramid);
    AssertEqual(false, carpetResult.MatchesRequiredItems);
    AssertEqual(false, carpetResult.LootSummary.Contains("Flying Carpet", StringComparison.Ordinal));
    AssertEqual(true, carpetResult.LootSummary.Contains("Pharaoh's Mask", StringComparison.Ordinal));

    var pharaohResult = TerrariaSplit.Terraria.WorldGeneration.PyramidSeedPreScreen.EvaluateSmallCrimson(
        "1092653535",
        difficultyCode: 1,
        requiredItemMask: AutoCreatePyramidFilterItem.PharaohSetMask);

    AssertEqual(true, pharaohResult.MatchesRequiredItems);
    AssertEqual("other", pharaohResult.TargetClass);
}

static void TestOverlayCompositeLayoutCalculator()
{
    var settings = new AppSettings();
    settings.Overlay.Columns.TimerOffsetY = -180;
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

    AssertEqual(true, OverlayCompositeLayoutCalculator.TryCreate(
        compositeBounds,
        settings,
        statusCount: 9,
        visibleStatusCount: 5,
        baseRowGap: 9,
        out OverlayCompositeLayout bottomAlignedLayout));
    int visibleRows = Math.Max(5, SplitCompletionAnimationRenderer.ReservedRowCount);
    int rowOffset = 9 - visibleRows;
    AssertEqual(layout.Layout.GetRowRect(rowOffset).Y, bottomAlignedLayout.Layout.GetRowRect(0).Y);
    AssertEqual(layout.TimerLocalBounds.Top, layout.MapTimerPointToComposite(Point.Empty).Y);

    AssertEqual(true, OverlayCompositeLayoutCalculator.TryCreate(
        compositeBounds,
        settings,
        statusCount: 3,
        visibleStatusCount: 3,
        baseRowGap: 9,
        out OverlayCompositeLayout animationReservedLayout));
    Rectangle animationBounds = SplitCompletionAnimationRenderer.GetAnimationBounds(new OverlayRenderContext(
        settings,
        UiPalette.From(settings.Overlay.Colors),
        TestSnapshots.Terraria(isGameMenu: false),
        [],
        CurrentSplitIndex: -1,
        SplitTimerPhase.NotStarted,
        TimeSpan.Zero,
        animationReservedLayout.Layout,
        SplitCompletionAnimationRenderer.ReservedRowCount,
        MouseClickThrough: false,
        SplitCompletionAnimation: null,
        SegmentBestDeltaHighlights: new Dictionary<int, SegmentBestDeltaHighlight>(),
        NowUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    AssertEqual(true, animationReservedLayout.StatusLocalBounds.Contains(animationBounds));

    AssertEqual(false, SplitLayoutCalculator.TryCreate(
        new Rectangle(0, 0, 480, 700),
        statusCount: 15,
        baseRowGap: 9,
        value => OverlayRenderContext.ScaleInt(settings, value),
        out _));

    var denseController = new OverlayBoundsController(baseRowGap: 9, settings, statusCount: 15, visibleStatusCount: 15);
    denseController.Initialize(new Rectangle(100, 200, 480, 700));
    AssertEqual(true, denseController.CompositeBounds.Height > 700);
    AssertEqual(true, denseController.CurrentLayout.TimerLocalBounds.Height > 1);
    AssertEqual(true, denseController.CurrentLayout.TimerLocalBounds.Height > denseController.CurrentLayout.Layout.TimerRect.Height);
    AssertEqual(true, denseController.CurrentLayout.TimerLocalBounds.Contains(denseController.CurrentLayout.Layout.TimerRect));
    AssertEqual(true, denseController.CurrentLayout.TimerLocalBounds.Contains(TimerRenderer.GetTimerTextBounds(
        settings,
        denseController.CurrentLayout.Layout.TimerRect)));

    var controller = new OverlayBoundsController(baseRowGap: 9, settings, statusCount: 9, visibleStatusCount: 9);
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

static void TestWorldSeedMetadataMatchesWorldOptions()
{
    var metadata = new TerrariaWorldSeedMetadata("server-picked.seed", 1, 1, true, 4);

    var settings = new AutoCreateWorldSettings
    {
        WorldSize = AutoCreateWorldSize.Small,
        WorldDifficulty = AutoCreateWorldDifficulty.Classic,
        WorldEvil = AutoCreateWorldEvil.Crimson,
        SpecialSeeds = AutoCreateSpecialWorldSeed.ForTheWorthy
    };
    AssertEqual(true, metadata.MatchesWorldOptions(settings));

    settings.WorldDifficulty = AutoCreateWorldDifficulty.Expert;
    AssertEqual(false, metadata.MatchesWorldOptions(settings));

    settings.WorldDifficulty = AutoCreateWorldDifficulty.Classic;
    settings.WorldEvil = AutoCreateWorldEvil.Corruption;
    AssertEqual(false, metadata.MatchesWorldOptions(settings));

    settings.WorldEvil = AutoCreateWorldEvil.Random;
    AssertEqual(true, metadata.MatchesWorldOptions(settings));

    settings.SpecialSeeds = string.Empty;
    AssertEqual(false, metadata.MatchesWorldOptions(settings));

    var zenithMetadata = new TerrariaWorldSeedMetadata("server-picked.seed", 2, 2, false, 255);
    settings.WorldSize = AutoCreateWorldSize.Medium;
    settings.WorldDifficulty = AutoCreateWorldDifficulty.Expert;
    settings.WorldEvil = AutoCreateWorldEvil.Corruption;
    settings.SpecialSeeds = AutoCreateSpecialWorldSeed.Zenith;
    AssertEqual(true, zenithMetadata.MatchesWorldOptions(settings));

    var partialZenithMetadata = new TerrariaWorldSeedMetadata("server-picked.seed", 2, 2, false, 128);
    AssertEqual(false, partialZenithMetadata.MatchesWorldOptions(settings));
}

static void WriteSyntheticWorldFile(
    string path,
    TerrariaWorldDimensions dimensions,
    string seedText = "server-picked-seed",
    bool crimson = true,
    IReadOnlyList<SyntheticChest>? chests = null)
{
    chests ??= [];
    using FileStream stream = File.Create(path);
    using BinaryWriter writer = new(stream);
    writer.Write(279);
    writer.Write(0x026369676F6C6572UL);
    writer.Write((uint)0);
    writer.Write((ulong)0);
    writer.Write((short)3);
    long sectionPointersOffset = stream.Position;
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);
    writer.Write((short)256);
    for (int byteIndex = 0; byteIndex < 32; byteIndex++)
    {
        byte bits = 0;
        if (byteIndex == 2)
        {
            bits |= 1 << 5; // Tile 21, basic chests, is frame-important.
        }

        writer.Write(bits);
    }

    int headerSectionOffset = (int)stream.Position;
    WriteSyntheticWorldHeader(writer, dimensions, seedText, crimson);
    int tileSectionOffset = (int)stream.Position;
    stream.Position = sectionPointersOffset;
    writer.Write(headerSectionOffset);
    writer.Write(tileSectionOffset);
    long chestPointerOffset = stream.Position;
    writer.Write(0);
    stream.Position = tileSectionOffset;

    for (int x = 0; x < dimensions.Width; x++)
    {
        WriteSyntheticTileColumn(writer, dimensions.Height);
    }

    int chestSectionOffset = (int)stream.Position;
    stream.Position = chestPointerOffset;
    writer.Write(chestSectionOffset);
    stream.Position = chestSectionOffset;
    WriteSyntheticChests(writer, chests, version: 279);
}

static void WriteSyntheticWorldHeader(
    BinaryWriter writer,
    TerrariaWorldDimensions dimensions,
    string seedText,
    bool crimson)
{
    writer.Write("synthetic");
    writer.Write(seedText);
    writer.Write((ulong)279);
    writer.Write(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee").ToByteArray());
    writer.Write(12345);
    writer.Write(0);
    writer.Write(dimensions.Width * 16);
    writer.Write(0);
    writer.Write(dimensions.Height * 16);
    writer.Write(dimensions.Height);
    writer.Write(dimensions.Width);
    writer.Write(0);
    writer.Write(false); // drunk world
    writer.Write(false); // for the worthy
    writer.Write(false); // celebration
    writer.Write(false); // the constant
    writer.Write(false); // not the bees
    writer.Write(false); // remix
    writer.Write(false); // no traps
    writer.Write(false); // zenith
    writer.Write(DateTime.UtcNow.ToBinary());
    writer.Write((byte)0);
    for (int i = 0; i < 19; i++)
    {
        writer.Write(0);
    }

    writer.Write(0d);
    writer.Write(0d);
    writer.Write(0d);
    writer.Write(true);
    writer.Write(0);
    writer.Write(false);
    writer.Write(false);
    writer.Write(0);
    writer.Write(0);
    writer.Write(crimson);
}

static void WriteSyntheticEmptyTileRun(BinaryWriter writer, int runLength)
{
    int run = Math.Max(0, runLength - 1);
    byte flags = 0;
    if (run > 0)
    {
        flags |= run <= byte.MaxValue ? (byte)0x40 : (byte)0x80;
    }

    writer.Write(flags);
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

static void WriteSyntheticTileColumn(
    BinaryWriter writer,
    int height)
{
    WriteSyntheticEmptyTileRun(writer, height);
}

static void WriteSyntheticChests(BinaryWriter writer, IReadOnlyList<SyntheticChest> chests, int version)
{
    writer.Write((short)chests.Count);
    if (version < 294)
    {
        writer.Write((short)40);
    }

    foreach (SyntheticChest chest in chests)
    {
        writer.Write(chest.X);
        writer.Write(chest.Y);
        writer.Write(string.Empty);
        if (version >= 294)
        {
            writer.Write(40);
        }

        Dictionary<int, SyntheticChestItem> items = chest.Items.ToDictionary(item => item.Slot);
        for (int slot = 0; slot < 40; slot++)
        {
            if (!items.TryGetValue(slot, out SyntheticChestItem item) || item.Stack <= 0)
            {
                writer.Write((short)0);
                continue;
            }

            writer.Write((short)item.Stack);
            writer.Write(item.Type);
            writer.Write(item.Prefix);
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

        AssertEqual("sounds\\resume.wav", form.Result.Overlay.Sounds.Resume);
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

        AssertEqual("sounds\\moonlord-best.wav", form.Result.Overlay.Sounds.MoonLordAheadReferenceAheadSegment);
    });
}

static void TestSettingsFormLocksReferenceControlsForPersonalBestReference()
{
    RunSta(() =>
    {
        var settings = new AppSettings { Comparison = { UsePersonalBestAsReferenceTime = true } };
        SettingsNormalizer.Normalize(settings);
        string skeletronKey = SingleCumulativeKey(settings, "split:boss-skeletron");
        settings.Comparison.PersonalBestTimes[skeletronKey] = "00:30";

        using var form = new SettingsForm(settings);
        DataSettingsPage page = form.PageHost.GetOrCreatePage<DataSettingsPage>(SettingsPageId.Data);

        AssertEqual(true, page.UsePersonalBestAsReferenceTimeBox.Checked);
        AssertEqual(false, page.ReferenceSetBox.Enabled);
        AssertEqual(false, page.NewReferenceSetNameBox.Enabled);
        AssertEqual("00:30", page.SplitTextBoxes[skeletronKey].Text);
        AssertEqual(true, page.SplitTextBoxes[skeletronKey].ReadOnly);
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
        colorTextBoxes[nameof(UiColorSettings.SplitCompletionSegmentLabelText)].Text = "#DDEEFF";
        colorTextBoxes[nameof(UiColorSettings.SplitCompletionLabelText)].Text = "#FEDCBA";
        colorTextBoxes[nameof(UiColorSettings.SplitCompletionSegmentTimeText)].Text = "#123ABC";
        colorTextBoxes[nameof(UiColorSettings.SplitCompletionTimeText)].Text = "#456DEF";

        form.ApplyForTests();

        AssertEqual("#112233", form.Result.Overlay.Colors.ReferenceTextOutline);
        AssertEqual("#445566", form.Result.Overlay.Colors.ReferenceTextShadow);
        AssertEqual("#778899", form.Result.Overlay.Colors.TimerPausedTextOutline);
        AssertEqual("#AABBCC", form.Result.Overlay.Colors.TimerPausedTextShadow);
        AssertEqual("#DDEEFF", form.Result.Overlay.Colors.SplitCompletionSegmentLabelText);
        AssertEqual("#FEDCBA", form.Result.Overlay.Colors.SplitCompletionLabelText);
        AssertEqual("#123ABC", form.Result.Overlay.Colors.SplitCompletionSegmentTimeText);
        AssertEqual("#456DEF", form.Result.Overlay.Colors.SplitCompletionTimeText);
    });
}

static void TestColorSettingsLabelsFollowRequestedOrder()
{
    string[] labels =
    [
        .. SettingsDescriptors.TextColors.Select(descriptor => descriptor.Label),
        .. SettingsDescriptors.AnimationColors.Select(descriptor => descriptor.Label)
    ];

    string[] expected =
    [
        "Reference time (future stage)",
        "Reference time (current stage)",
        "Cumulative time (completed stage)",
        "Delta (fast)",
        "Delta (slow)",
        "Main timer (not timing)",
        "Main timer (fast)",
        "Main timer (slow)",
        "Main timer (total fast)",
        "Main timer (total slow)",
        "Main timer (paused)",
        "Segment time hint text",
        "Cumulative time hint text",
        "Segment time",
        "Cumulative time"
    ];

    AssertEqual(string.Join("|", expected), string.Join("|", labels));
}

static void TestMainFormPreservesSizeWhenApplyingNonLayoutSettings()
{
    RunSta(() =>
    {
        using var form = new MainForm(registerGlobalHotkeys: false);
        _ = form.Handle;
        OverlayBoundsController boundsController = GetPrivateField<OverlayBoundsController>(form, "overlayBoundsController");
        AppSettings previousSettings = GetMainFormSettings(form);
        SplitStatusSnapshot[] statuses = SplitCatalog.Build(previousSettings)
            .Select(SplitStatusSnapshot.FromDefinition)
            .ToArray();
        int rowCount = SplitDisplayRows.GetReservedRowCount(previousSettings, statuses);
        int visibleRowCount = SplitDisplayRows.GetRequiredRowCount(previousSettings, statuses);
        int fittingHeight = OverlayCompositeLayoutCalculator.GetFittingHeight(
            width: 1000,
            initialHeight: 900,
            previousSettings,
            rowCount,
            visibleRowCount,
            baseRowGap: 9);
        Rectangle initialCompositeBounds = new(120, 160, 1000, fittingHeight);
        boundsController.ApplyCompositeBounds(initialCompositeBounds);

        var settings = AppSettingsStore.Clone(previousSettings);
        settings.Overlay.Colors.TimerText = "#123456";
        SetMainFormSettings(form, settings);

        InvokePrivate(form, "ApplyLoadedSettings", previousSettings, -1);

        AssertEqual(initialCompositeBounds.Size, boundsController.CompositeBounds.Size);
    });
}

static void TestMainFormSettingsApplyRedrawsStaticStatusOverlayContent()
{
    RunSta(() =>
    {
        using var form = new MainForm(registerGlobalHotkeys: false);
        _ = form.Handle;

        SetPrivateField(form, "statusOverlayContentDirty", false);
        SetPrivateField(form, "lastStatusOverlayDynamicKey", new StatusOverlayDynamicKey(0, string.Empty, 0));

        AppSettings nextSettings = GetMainFormSettings(form);
        nextSettings.Route.SplitRoute[0].Condition = SplitCondition.Any(
        [
            SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
            SplitCatalog.CreateBossFactCondition(SplitCatalog.WallOfFlesh)
        ]);

        InvokePrivate(form, "ApplySettings", nextSettings);

        AssertEqual(true, GetPrivateField<bool>(form, "statusOverlayContentDirty"));
        object? dynamicKey = form.GetType()
            .GetField("lastStatusOverlayDynamicKey", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(form);
        AssertEqual(null, dynamicKey);
    });
}

static void TestMainFormSettingsApplyReloadsDefinitionsAndRecordsCurrentRun()
{
    RunSta(() =>
    {
        using var form = new MainForm(registerGlobalHotkeys: false);
        _ = form.Handle;

        AppSettings previousSettings = GetMainFormSettings(form);
        var nextSettings = AppSettingsStore.Clone(previousSettings);
        nextSettings.Comparison.AutoUpdatePersonalBestData = false;
        nextSettings.Comparison.AskBeforeUpdatingPersonalBestData = false;
        nextSettings.General.AlwaysOnTop = !nextSettings.General.AlwaysOnTop;

        ApplicationController applicationController = GetPrivateField<ApplicationController>(form, "applicationController");
        var tracker = new SplitTracker();
        tracker.SetDefinitions(applicationController.Definitions);
        SplitStatus skeletronStatus = tracker.Statuses.First(status =>
            status.Definition.ContainsTarget(SplitCatalog.Skeletron));
        string skeletronSplitId = skeletronStatus.Definition.Id;
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
            AssertEqual(TimeText.FormatRecord(expectedTime), lastRun[skeletronSplitId]);
        }
        finally
        {
            RestoreDirectory(lastRunDirectory, lastRunSnapshot);
        }
    });
}

static void TestWindowLayerDefersModalStateUpdates()
{
    RunSta(() =>
    {
        using var form = new Form();
        _ = form.Handle;
        var blockedStates = new List<bool>();
        var controller = new WindowLayerController(
            form,
            blocked => blockedStates.Add(blocked),
            () => IntPtr.Zero);

        IDisposable registration;
        using (controller.DeferWindowStateUpdates())
        {
            registration = controller.RegisterModalWindow(() => IntPtr.Zero);
            controller.ApplyWindowState();

            AssertEqual(0, blockedStates.Count);
        }

        AssertEqual("True", string.Join('|', blockedStates));

        registration.Dispose();

        AssertEqual("True|False", string.Join('|', blockedStates));
    });
}

static void TestMainFormInitializesOverlayLayoutWithCurrentSplitCount()
{
    RunSta(() =>
    {
        using var form = new MainForm(registerGlobalHotkeys: false);
        _ = form.Handle;

        OverlayBoundsController boundsController = GetPrivateField<OverlayBoundsController>(form, "overlayBoundsController");
        ApplicationController applicationController = GetPrivateField<ApplicationController>(form, "applicationController");
        Rectangle compositeBounds = boundsController.CompositeBounds;
        AppSettings settings = GetMainFormSettings(form);

        int reservedRowCount = SplitDisplayRows.GetReservedRowCount(settings, applicationController.ViewState.DisplayStatuses);
        int visibleRowCount = SplitDisplayRows.GetRequiredRowCount(settings, applicationController.ViewState.DisplayStatuses);
        AssertEqual(true, OverlayCompositeLayoutCalculator.TryCreate(
            compositeBounds,
            settings,
            reservedRowCount,
            visibleRowCount,
            9,
            out OverlayCompositeLayout expectedLayout));
        AssertEqual(expectedLayout.Layout.FirstRowRect, boundsController.CurrentLayout.Layout.FirstRowRect);
        AssertEqual(expectedLayout.Layout.TimerRect, boundsController.CurrentLayout.Layout.TimerRect);
    });
}

static void TestMainFormOverlayClientSizeMatchesStatusLayout()
{
    RunSta(() =>
    {
        using var form = new MainForm(registerGlobalHotkeys: false);
        _ = form.Handle;
        form.Show();
        Application.DoEvents();

        OverlayBoundsController boundsController = GetPrivateField<OverlayBoundsController>(form, "overlayBoundsController");

        AssertEqual(boundsController.CurrentLayout.StatusScreenBounds.Size, form.ClientSize);
    });
}

static void TestMainFormScalesSizeWhenGlobalScaleChanges()
{
    RunSta(() =>
    {
        using var form = new MainForm(registerGlobalHotkeys: false);
        _ = form.Handle;
        OverlayBoundsController boundsController = GetPrivateField<OverlayBoundsController>(form, "overlayBoundsController");
        var previousSettings = new AppSettings();
        previousSettings.Overlay.Columns.ScalePercent = 100;
        SetMainFormSettings(form, previousSettings);
        InvokePrivate(form, "ApplyLoadedSettings", (object?)null, -1);
        boundsController.ApplyCompositeBounds(new Rectangle(80, 90, 600, 500));
        Size previousSize = boundsController.CompositeBounds.Size;
        int previousRowHeight = boundsController.CurrentLayout.Layout.FirstRowRect.Height;

        var settings = AppSettingsStore.Clone(previousSettings);
        settings.Overlay.Columns.ScalePercent = 150;
        SetMainFormSettings(form, settings);

        InvokePrivate(form, "ApplyLoadedSettings", previousSettings, -1);

        AssertEqual((int)Math.Round(previousSize.Width * 1.5f, MidpointRounding.AwayFromZero), boundsController.CompositeBounds.Width);
        AssertEqual(true, boundsController.CompositeBounds.Height >= (int)Math.Round(previousSize.Height * 1.5f, MidpointRounding.AwayFromZero));
        AssertEqual(true, boundsController.CurrentLayout.Layout.FirstRowRect.Height >= previousRowHeight);
    });
}

static void TestMainFormAdjustsWidthWhenSplitColumnsChange()
{
    RunSta(() =>
    {
        using var form = new MainForm(registerGlobalHotkeys: false);
        _ = form.Handle;
        OverlayBoundsController boundsController = GetPrivateField<OverlayBoundsController>(form, "overlayBoundsController");
        var previousSettings = new AppSettings();
        previousSettings.Overlay.Columns.ScalePercent = 100;
        SetMainFormSettings(form, previousSettings);
        InvokePrivate(form, "ApplyLoadedSettings", (object?)null, -1);

        boundsController.ApplyCompositeBounds(new Rectangle(80, 90, 600, 500));
        int previousRowHeight = boundsController.CurrentLayout.Layout.FirstRowRect.Height;
        var settings = AppSettingsStore.Clone(previousSettings);
        settings.Overlay.Columns.Time.Width += 100;
        SetMainFormSettings(form, settings);

        InvokePrivate(form, "ApplyLoadedSettings", previousSettings, -1);

        AssertEqual(700, boundsController.CompositeBounds.Width);
        AssertEqual(true, boundsController.CompositeBounds.Height >= 500);
        AssertEqual(true, boundsController.CurrentLayout.Layout.FirstRowRect.Height >= previousRowHeight);
    });
}

static void TestMainFormGrowsHeightWhenSplitRouteGrows()
{
    RunSta(() =>
    {
        using var form = new MainForm(registerGlobalHotkeys: false);
        _ = form.Handle;
        OverlayBoundsController boundsController = GetPrivateField<OverlayBoundsController>(form, "overlayBoundsController");
        var previousSettings = new AppSettings { Route = { SplitRoute = SplitCatalog.CreateDefaultRoute() } };
        previousSettings.Overlay.Columns.ScalePercent = 100;
        SetMainFormSettings(form, previousSettings);
        InvokePrivate(form, "ApplyLoadedSettings", (object?)null, -1);

        boundsController.ApplyCompositeBounds(new Rectangle(80, 90, 600, 720));
        int previousHeight = boundsController.CompositeBounds.Height;
        int previousRowHeight = boundsController.CurrentLayout.Layout.FirstRowRect.Height;

        var settings = AppSettingsStore.Clone(previousSettings);
        settings.Route.SplitRoute.Add(CreateTestRouteEntry("split:extra-a", "Extra A", SplitCatalog.Skeletron));
        settings.Route.SplitRoute.Add(CreateTestRouteEntry("split:extra-b", "Extra B", SplitCatalog.MoonLord));
        settings.Route.SplitRoute.Add(CreateTestRouteEntry("split:extra-c", "Extra C", SplitCatalog.WallOfFlesh));
        settings.Route.SplitRoute.Add(CreateTestRouteEntry("split:extra-d", "Extra D", SplitCatalog.Destroyer));
        settings.Route.SplitRoute.Add(CreateTestRouteEntry("split:extra-e", "Extra E", SplitCatalog.Twins));
        SetMainFormSettings(form, settings);

        InvokePrivate(form, "ApplyLoadedSettings", previousSettings, SplitCatalog.Build(settings).Count);

        AssertEqual(true, boundsController.CompositeBounds.Height > previousHeight);
        AssertEqual(previousRowHeight, boundsController.CurrentLayout.Layout.FirstRowRect.Height);
    });
}

static void TestSettingsFormAppliesCurrentDeltaGradientOption()
{
    RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings { Overlay = { EnableDeltaGradientColor = false,
            EnableCurrentDeltaGradientColor = true,
            EnableTimerGradientColor = false } });
        AnimationSettingsPage page = form.PageHost.GetOrCreatePage<AnimationSettingsPage>(SettingsPageId.Effects);
        page.EnableCurrentDeltaGradientColorBox.Checked = false;

        form.ApplyForTests();

        AssertEqual(false, form.Result.Overlay.EnableDeltaGradientColor);
        AssertEqual(false, form.Result.Overlay.EnableCurrentDeltaGradientColor);
        AssertEqual(false, form.Result.Overlay.EnableTimerGradientColor);
    });
}

static void TestSettingsFormKeepsUncreatedAnimationFieldsUnchanged()
{
    RunSta(() =>
    {
        var settings = new AppSettings { Overlay = { UndefeatedIconGrayscalePercent = 22,
            UndefeatedIconBrightnessPercent = 73,
            CurrentBossIconGrayscaleWeakenPercent = 11,
            CurrentBossIconBrightnessBoostPercent = 64 } };
        using var form = new SettingsForm(settings);
        form.PageHost.GetOrCreatePage<UiSettingsPage>(SettingsPageId.Ui);

        form.ApplyForTests();

        AssertEqual(22, form.Result.Overlay.UndefeatedIconGrayscalePercent);
        AssertEqual(73, form.Result.Overlay.UndefeatedIconBrightnessPercent);
        AssertEqual(11, form.Result.Overlay.CurrentBossIconGrayscaleWeakenPercent);
        AssertEqual(64, form.Result.Overlay.CurrentBossIconBrightnessBoostPercent);
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

static TerrariaWatchSnapshot GenerationSnapshot(bool hasGeneration)
{
    TerrariaWorldGenerationState generation = hasGeneration
        ? new TerrariaWorldGenerationState("Final Cleanup", "Saving world", 1d, 1d)
        : TerrariaWorldGenerationState.Unknown;
    return new TerrariaWatchSnapshot(
        true,
        123,
        true,
        true,
        TerrariaGameFacts.Unknown,
        generation,
        false,
        "test watcher");
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

static void SetFontFamilySelectorValue(FontFamilySelector selector, string value)
{
    if (!UiFontSettings.GetInstalledFamilyNames().Contains(value, StringComparer.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Font selector does not contain '{value}'.");
    }

    selector.SetSelectedFontFamily(value);
}

static string SelectInstalledFontFamilyForTest(string currentFamily)
{
    return UiFontSettings.GetInstalledFamilyNames()
        .FirstOrDefault(name => !string.Equals(name, currentFamily, StringComparison.OrdinalIgnoreCase))
        ?? UiFontSettings.NormalizeFamilyName(currentFamily);
}

static void SetMainFormSettings(MainForm form, AppSettings settings)
{
    ApplicationController controller = GetPrivateField<ApplicationController>(form, "applicationController");
    AppSettings clonedSettings = AppSettingsStore.Clone(settings);
    IReadOnlyList<SplitDefinition> definitions = SplitCatalog.Build(clonedSettings);
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

static IEnumerable<Control> EnumerateControls(Control root)
{
    yield return root;
    foreach (Control child in root.Controls)
    {
        foreach (Control descendant in EnumerateControls(child))
        {
            yield return descendant;
        }
    }
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

        string siblingSourceRoot = Path.Combine(directory, "TerrariaSplit");
        if (File.Exists(Path.Combine(siblingSourceRoot, "TerrariaSplit.slnx")))
        {
            return siblingSourceRoot;
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
        type == typeof(GeneralSettings) ||
        type == typeof(HotkeySettings) ||
        type == typeof(RouteSettings) ||
        type == typeof(ComparisonSettings) ||
        type == typeof(OverlaySettings) ||
        type == typeof(AutomationSettings) ||
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
        ClearReadOnlyAttributes(path);
        Directory.Delete(path, true);
    }
}

static void ClearReadOnlyAttributes(string path)
{
    var directory = new DirectoryInfo(path);
    if (!directory.Exists)
    {
        return;
    }

    foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
    {
        entry.Attributes &= ~FileAttributes.ReadOnly;
    }

    directory.Attributes &= ~FileAttributes.ReadOnly;
}

static void Nearly(double expected, double actual, double tolerance)
{
    if (Math.Abs(expected - actual) > tolerance)
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

readonly record struct SyntheticChest(int X, int Y, IReadOnlyList<SyntheticChestItem> Items);

readonly record struct SyntheticChestItem(int Slot, int Type, int Stack = 1, byte Prefix = 0);

readonly record struct DirectorySnapshot(bool Exists, Dictionary<string, byte[]> Files);

sealed class FakePyramidSeedRandomizer : IPyramidSeedRandomizer
{
    public int Attempts { get; private set; }

    public Task<bool> RandomizeVisibleSeedAsync(int attempt, CancellationToken cancellationToken)
    {
        Attempts++;
        return Task.FromResult(true);
    }
}

sealed class FakePyramidVisibleSeedReader : IPyramidVisibleSeedReader
{
    private readonly Queue<PyramidVisibleSeedReadResult> readResults;

    public FakePyramidVisibleSeedReader(string? currentSeed, IEnumerable<PyramidVisibleSeedReadResult> readResults)
    {
        CurrentSeed = currentSeed;
        this.readResults = new Queue<PyramidVisibleSeedReadResult>(readResults);
    }

    public string? CurrentSeed { get; private set; }

    public List<string?> PreviousSeedsSeen { get; } = [];

    public string? ReadCurrentSeed()
    {
        return CurrentSeed;
    }

    public Task<PyramidVisibleSeedReadResult> WaitForSeedAfterRandomizeAsync(
        string? previousSeedText,
        CancellationToken cancellationToken)
    {
        PreviousSeedsSeen.Add(previousSeedText);
        if (readResults.Count == 0)
        {
            return Task.FromResult(PyramidVisibleSeedReadResult.Failed(
                TerrariaWorldCreationSeedStatus.Unknown,
                0,
                CurrentSeed ?? string.Empty));
        }

        PyramidVisibleSeedReadResult result = readResults.Dequeue();
        if (result.Success)
        {
            CurrentSeed = result.SeedText;
        }

        return Task.FromResult(result);
    }
}

sealed class FakePyramidSeedPreScreenEvaluator : IPyramidSeedPreScreenEvaluator
{
    private readonly Func<string, PyramidSeedPreScreenPrediction> evaluate;

    public FakePyramidSeedPreScreenEvaluator(Func<string, PyramidSeedPreScreenPrediction> evaluate)
    {
        this.evaluate = evaluate;
    }

    public PyramidSeedPreScreenPrediction Evaluate(AutoCreateWorldSettings settings, string seedText)
    {
        return evaluate(seedText);
    }
}

sealed class SequenceGenerationWatcher : ITerrariaWorldWatcher
{
    private readonly TerrariaWatchSnapshot[] snapshots;
    private int index;

    public SequenceGenerationWatcher(params TerrariaWatchSnapshot[] snapshots)
    {
        this.snapshots = snapshots.Length == 0
            ? [new TerrariaWatchSnapshot(
                true,
                123,
                true,
                true,
                TerrariaGameFacts.Unknown,
                TerrariaWorldGenerationState.Unknown,
                false,
                "test watcher")]
            : snapshots;
    }

    public TerrariaWatchSnapshot Poll()
    {
        if (index < snapshots.Length)
        {
            return snapshots[index++];
        }

        return snapshots[^1];
    }

    public TerrariaWatcherDiagnostics GetDiagnostics()
    {
        return TerrariaWatcherDiagnosticsDefaults.Empty;
    }

    public void Dispose()
    {
    }
}
