using TerrariaSplit;
using System.Diagnostics;

namespace TerrariaSplit.Tests;

internal static class HotkeyTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("Hotkey command mapper routes hotkeys into app commands", HotkeyCommandMapperRoutesHotkeysIntoAppCommands);
        yield return ("ApplicationController maps input commands to effects", ApplicationControllerMapsInputCommandsToEffects);
        yield return ("ApplicationController apply settings preserves pending PB update", ApplicationControllerApplySettingsPreservesPendingPersonalBestUpdate);
        yield return ("TimerController consumes queued menu actions only on menu", TimerControllerConsumesQueuedMenuActionsOnlyOnMenu);
        yield return ("TimerController records automatic events at observed timestamps", TimerControllerRecordsAutomaticEventsAtObservedTimestamps);
        yield return ("SplitCondition evaluates tri-state groups", SplitConditionEvaluatesTriStateGroups);
        yield return ("SplitTracker completes AND and OR conditions", SplitTrackerCompletesCompositeConditions);
        yield return ("SplitTracker skips earlier splits when later split completes", SplitTrackerSkipsEarlierSplitsWhenLaterSplitCompletes);
        yield return ("SplitTracker keeps attached splits out of route decisions", SplitTrackerKeepsAttachedSplitsOutOfRouteDecisions);
        yield return ("SplitTracker records completion fact keys for OR display", SplitTrackerRecordsCompletionFactKeysForOrDisplay);
        yield return ("SplitTracker records partial fact completion times", SplitTrackerRecordsPartialFactCompletionTimes);
        yield return ("SplitTracker keeps historical fact keys when split completes", SplitTrackerKeepsHistoricalFactKeysWhenSplitCompletes);
        yield return ("SplitTracker remembers ever owned item counts", SplitTrackerRemembersEverOwnedItemCounts);
        yield return ("SplitTracker skips initially satisfied conditions after delayed fact resolution", SplitTrackerSkipsInitiallySatisfiedConditionAfterDelayedFactResolution);
        yield return ("SplitTracker completes condition after initial false state", SplitTrackerCompletesConditionAfterInitialFalseState);
        yield return ("WatcherRuntimeProcessor reuses snapshot instances while state is unchanged", WatcherRuntimeProcessorReusesUnchangedSnapshots);
    }

    private static void HotkeyCommandMapperRoutesHotkeysIntoAppCommands()
    {
        DateTime requestedAtUtc = DateTime.UtcNow;

        TestAssert.Equal(true, HotkeyCommandMapper.TryMap(
            HotkeyAction.PauseResume,
            requestedAtUtc,
            createWorldRunning: false,
            enterWorldRunning: false,
            out AppCommand pauseCommand));
        TestAssert.Equal(AppCommandKind.TogglePause, pauseCommand.Kind);

        TestAssert.Equal(true, HotkeyCommandMapper.TryMap(
            HotkeyAction.MouseClickThrough,
            requestedAtUtc,
            createWorldRunning: false,
            enterWorldRunning: false,
            out AppCommand clickThroughCommand));
        TestAssert.Equal(AppCommandKind.ToggleMouseClickThrough, clickThroughCommand.Kind);

        TestAssert.Equal(true, HotkeyCommandMapper.TryMap(
            HotkeyAction.Reset,
            requestedAtUtc,
            createWorldRunning: false,
            enterWorldRunning: false,
            out AppCommand resetCommand));
        TestAssert.Equal(AppCommandKind.QueueMenuAction, resetCommand.Kind);
        TestAssert.Equal(MenuActionKind.Reset, resetCommand.MenuAction);

        TestAssert.Equal(true, HotkeyCommandMapper.TryMap(
            HotkeyAction.CreateWorld,
            requestedAtUtc,
            createWorldRunning: false,
            enterWorldRunning: false,
            out AppCommand createWorldCommand));
        TestAssert.Equal(AppCommandKind.QueueMenuAction, createWorldCommand.Kind);
        TestAssert.Equal(MenuActionKind.CreateWorld, createWorldCommand.MenuAction);

        TestAssert.Equal(true, HotkeyCommandMapper.TryMap(
            HotkeyAction.PracticeWorld,
            requestedAtUtc,
            createWorldRunning: false,
            enterWorldRunning: false,
            out AppCommand practiceWorldCommand));
        TestAssert.Equal(AppCommandKind.QueueMenuAction, practiceWorldCommand.Kind);
        TestAssert.Equal(MenuActionKind.PracticeWorld, practiceWorldCommand.MenuAction);

        TestAssert.Equal(true, HotkeyCommandMapper.TryMap(
            HotkeyAction.CreateWorld,
            requestedAtUtc,
            createWorldRunning: true,
            enterWorldRunning: false,
            out AppCommand cancelCreateCommand));
        TestAssert.Equal(AppCommandKind.CancelCreateWorld, cancelCreateCommand.Kind);

        TestAssert.Equal(false, HotkeyCommandMapper.TryMap(
            HotkeyAction.Reset,
            requestedAtUtc,
            createWorldRunning: true,
            enterWorldRunning: false,
            out _));

        TestAssert.Equal(true, HotkeyCommandMapper.TryMap(
            HotkeyAction.PracticeWorld,
            requestedAtUtc,
            createWorldRunning: false,
            enterWorldRunning: true,
            out AppCommand cancelEnterCommand));
        TestAssert.Equal(AppCommandKind.CancelEnterWorld, cancelEnterCommand.Kind);
    }

    private static void ApplicationControllerMapsInputCommandsToEffects()
    {
        DateTime requestedAtUtc = DateTime.UtcNow;
        var controller = new ApplicationController(new AppSettings(), _ => true);

        ApplicationUpdate resetUpdate = controller.HandleCommand(
            AppCommand.QueueMenuAction(MenuActionKind.Reset, requestedAtUtc));
        ApplicationEffect resetEffect = resetUpdate.Effects.Single();
        TestAssert.Equal(ApplicationEffectKind.SubmitRuntimeCommand, resetEffect.Kind);
        TestAssert.Equal(RuntimeCommandKind.QueueMenuAction, resetEffect.RuntimeCommand?.Kind);
        TestAssert.Equal(MenuActionKind.Reset, resetEffect.RuntimeCommand?.MenuAction);

        ApplicationUpdate clickThroughUpdate = controller.HandleCommand(AppCommand.ToggleMouseClickThrough());
        TestAssert.Equal(ApplicationEffectKind.ToggleMouseClickThrough, clickThroughUpdate.Effects.Single().Kind);

        ApplicationUpdate pyramidFilterUpdate = controller.HandleCommand(AppCommand.TogglePyramidFilter());
        TestAssert.Equal(true, controller.Settings.AutoCreate.EnablePyramidFilter);
        TestAssert.Equal(2, pyramidFilterUpdate.Effects.Count);
        TestAssert.Equal(ApplicationEffectKind.SaveSettings, pyramidFilterUpdate.Effects[0].Kind);
        TestAssert.Equal(ApplicationEffectKind.ApplySettingsToShell, pyramidFilterUpdate.Effects[1].Kind);
        TestAssert.Equal(false, pyramidFilterUpdate.Effects.Any(effect =>
            effect.Kind == ApplicationEffectKind.SubmitRuntimeCommand));

        ApplicationUpdate cancelCreateUpdate = controller.HandleCommand(AppCommand.CancelCreateWorld());
        TestAssert.Equal(ApplicationEffectKind.CancelCreateWorldAutomation, cancelCreateUpdate.Effects.Single().Kind);

        ApplicationUpdate cancelEnterUpdate = controller.HandleCommand(AppCommand.CancelEnterWorld());
        TestAssert.Equal(ApplicationEffectKind.CancelEnterWorldAutomation, cancelEnterUpdate.Effects.Single().Kind);
    }

    private static void ApplicationControllerApplySettingsPreservesPendingPersonalBestUpdate()
    {
        var settings = new AppSettings
        {
            AutoUpdatePersonalBestData = true,
            AskBeforeUpdatingPersonalBestData = true,
            SplitRoute =
            [
                new SplitRouteEntry
                {
                    Id = "split:skeletron",
                    DisplayName = "Skeletron",
                    Condition = SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                    IconTargetIds = [SplitCatalog.Skeletron]
                }
            ]
        };
        settings.PersonalBestSegmentTimes["split:skeletron"] = "1:00.00";

        int confirmCount = 0;
        var controller = new ApplicationController(settings, _ =>
        {
            confirmCount++;
            return true;
        });
        var tracker = new SplitTracker();
        tracker.SetDefinitions(controller.Definitions);
        tracker.Statuses[0].SetTime(TimeSpan.FromSeconds(30));
        RuntimeRunSnapshot runtimeSnapshot = RuntimeRunSnapshot.FromState(
            new SplitTimerState(SplitTimerPhase.Paused, TimeSpan.FromSeconds(30), 0),
            tracker,
            Stopwatch.GetTimestamp());

        controller.HandleWatcherNotification(new WatcherPollNotification(
            TestSnapshots.Terraria(isGameMenu: false),
            TestSnapshots.Terraria(isGameMenu: false),
            TerrariaWatcherDiagnosticsDefaults.Empty,
            runtimeSnapshot,
            [],
            0,
            TimeSpan.Zero,
            Stopwatch.GetTimestamp(),
            TimeSpan.FromMilliseconds(5),
            TimeSpan.Zero,
            null));

        AppSettings nextSettings = AppSettingsStore.Clone(settings);
        nextSettings.AlwaysOnTop = !nextSettings.AlwaysOnTop;
        ApplicationUpdate update = controller.HandleCommand(AppCommand.ApplySettings(nextSettings));

        TestAssert.Equal(1, confirmCount);
        TestAssert.Equal("0:30.00", controller.Settings.PersonalBestSegmentTimes["split:skeletron"]);
        TestAssert.Equal(nextSettings.AlwaysOnTop, controller.Settings.AlwaysOnTop);
        TestAssert.Equal(true, update.Effects.Any(effect => effect.Kind == ApplicationEffectKind.SaveSettings));
        TestAssert.Equal(true, update.Effects.Any(effect => effect.Kind == ApplicationEffectKind.SubmitRuntimeCommand &&
            effect.RuntimeCommand?.Kind == RuntimeCommandKind.Reset));
    }

    private static void TimerControllerConsumesQueuedMenuActionsOnlyOnMenu()
    {
        var controller = new TimerController(
            new SplitTimer(),
            new SplitTracker(),
            new PendingMenuActionScheduler(),
            TimeSpan.FromSeconds(1));
        DateTime requestedAtUtc = DateTime.UtcNow;

        controller.QueuePendingMenuAction(MenuActionKind.CreateWorld, requestedAtUtc);

        IReadOnlyList<RunEvent> inWorldEvents = controller.Tick(TestSnapshots.Terraria(isGameMenu: false));
        TestAssert.Equal(null, GetMenuAction(inWorldEvents));

        IReadOnlyList<RunEvent> menuEvents = controller.Tick(TestSnapshots.Terraria(isGameMenu: true));
        TestAssert.Equal(MenuActionKind.CreateWorld, GetMenuAction(menuEvents));

        controller.QueuePendingMenuAction(MenuActionKind.Reset, DateTime.UtcNow);
        IReadOnlyList<RunEvent> resetEvents = controller.Tick(TestSnapshots.Terraria(isGameMenu: true));
        TestAssert.Equal(MenuActionKind.Reset, GetMenuAction(resetEvents));

        controller.QueuePendingMenuAction(MenuActionKind.PracticeWorld, DateTime.UtcNow);
        IReadOnlyList<RunEvent> enterWorldEvents = controller.Tick(TestSnapshots.Terraria(isGameMenu: true));
        TestAssert.Equal(MenuActionKind.PracticeWorld, GetMenuAction(enterWorldEvents));
    }

    private static void SplitTrackerSkipsInitiallySatisfiedConditionAfterDelayedFactResolution()
    {
        SplitTracker tracker = CreateSingleSkeletronTracker();
        var controller = new TimerController(
            new SplitTimer(),
            tracker,
            new PendingMenuActionScheduler(),
            TimeSpan.FromSeconds(1));

        IReadOnlyList<RunEvent> startEvents = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: TerrariaGameFacts.Unknown,
                enteredWorld: true));
        TestAssert.Equal(true, HasEvent(startEvents, RunEventKind.RunStarted));
        TestAssert.Equal(null, GetCompletedSplitIndex(startEvents));

        IReadOnlyList<RunEvent> resolvedEvents = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateSkeletronFacts(true)));
        TestAssert.Equal(null, GetCompletedSplitIndex(resolvedEvents));
        if (!tracker.Statuses[0].IsSkipped)
        {
            throw new InvalidOperationException("Initially satisfied split was not marked skipped.");
        }
        TestAssert.Equal(1, tracker.CurrentIndex);
    }

    private static void SplitTrackerCompletesConditionAfterInitialFalseState()
    {
        SplitTracker tracker = CreateSingleSkeletronTracker();
        var controller = new TimerController(
            new SplitTimer(),
            tracker,
            new PendingMenuActionScheduler(),
            TimeSpan.FromSeconds(1));

        _ = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: TerrariaGameFacts.Unknown,
                enteredWorld: true));
        IReadOnlyList<RunEvent> initialStateEvents = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateSkeletronFacts(false)));
        TestAssert.Equal(null, GetCompletedSplitIndex(initialStateEvents));

        IReadOnlyList<RunEvent> completedEvents = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateSkeletronFacts(true)));
        TestAssert.Equal(0, GetCompletedSplitIndex(completedEvents));
        TestAssert.Equal(false, tracker.Statuses[0].IsSkipped);
    }

    private static void TimerControllerRecordsAutomaticEventsAtObservedTimestamps()
    {
        SplitTracker tracker = CreateSingleSkeletronTracker();
        var timer = new SplitTimer();
        var controller = new TimerController(
            timer,
            tracker,
            new PendingMenuActionScheduler(),
            TimeSpan.FromSeconds(1));

        long startTimestamp = 1_000_000;
        long initialIncompleteTimestamp = startTimestamp + Stopwatch.Frequency / 10;
        long completionTimestamp = startTimestamp + Stopwatch.Frequency / 4;

        IReadOnlyList<RunEvent> startEvents = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: TerrariaGameFacts.Unknown,
                enteredWorld: true),
            startTimestamp);
        TestAssert.Equal(true, HasEvent(startEvents, RunEventKind.RunStarted));

        _ = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateSkeletronFacts(false)),
            initialIncompleteTimestamp);

        IReadOnlyList<RunEvent> completionEvents = controller.Tick(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateSkeletronFacts(true)),
            completionTimestamp);

        TestAssert.Equal(0, GetCompletedSplitIndex(completionEvents));
        TimeSpan expected = TimeSpan.FromSeconds((completionTimestamp - startTimestamp) / (double)Stopwatch.Frequency);
        TestAssert.Equal(expected, tracker.Statuses[0].Time);
    }

    private static void SplitConditionEvaluatesTriStateGroups()
    {
        TerrariaGameFacts facts = CreateFacts(("fact:a", true), ("fact:b", false));

        TestAssert.Equal(
            SplitConditionResult.Unknown,
            SplitCondition.All([SplitCondition.Fact("fact:a"), SplitCondition.Fact("fact:missing")]).Evaluate(facts));
        TestAssert.Equal(
            SplitConditionResult.False,
            SplitCondition.All([SplitCondition.Fact("fact:a"), SplitCondition.Fact("fact:b")]).Evaluate(facts));
        TestAssert.Equal(
            SplitConditionResult.Unknown,
            SplitCondition.Any([SplitCondition.Fact("fact:b"), SplitCondition.Fact("fact:missing")]).Evaluate(facts));
        TestAssert.Equal(
            SplitConditionResult.True,
            SplitCondition.All([
                SplitCondition.Fact("fact:a"),
                SplitCondition.Fact("fact:item", SplitFactComparison.AtLeast, 2)
            ]).Evaluate(CreateFacts(("fact:a", true), ("fact:item", 2))));
        TestAssert.Equal(
            SplitConditionResult.True,
            SplitCondition.AtLeast(
            [
                SplitCondition.Fact("fact:a"),
                SplitCondition.Fact("fact:c"),
                SplitCondition.Fact("fact:missing")
            ], 2).Evaluate(CreateFacts(("fact:a", true), ("fact:c", true))));
        TestAssert.Equal(
            SplitConditionResult.Unknown,
            SplitCondition.AtLeast(
            [
                SplitCondition.Fact("fact:a"),
                SplitCondition.Fact("fact:b"),
                SplitCondition.Fact("fact:missing")
            ], 2).Evaluate(facts));
        TestAssert.Equal(
            SplitConditionResult.False,
            SplitCondition.AtLeast(
            [
                SplitCondition.Fact("fact:b"),
                SplitCondition.Fact("fact:missing")
            ], 2).Evaluate(facts));
    }

    private static void SplitTrackerCompletesCompositeConditions()
    {
        var tracker = new SplitTracker();
        tracker.SetDefinitions(
        [
            new SplitDefinition("split:all", "All", SplitCondition.All([SplitCondition.Fact("fact:a"), SplitCondition.Fact("fact:b")]), [], [], []),
            new SplitDefinition("split:any", "Any", SplitCondition.Any([SplitCondition.Fact("fact:c"), SplitCondition.Fact("fact:d")]), [], [], []),
            new SplitDefinition("split:at-least", "AtLeast", SplitCondition.AtLeast(
            [
                SplitCondition.Fact("fact:e"),
                SplitCondition.Fact("fact:f"),
                SplitCondition.Fact("fact:g")
            ], 2), [], [], [])
        ]);
        tracker.OnRunStarted(
            TestSnapshots.Terraria(isGameMenu: false, bossStates: CreateFacts(
                ("fact:a", false),
                ("fact:b", false),
                ("fact:c", false),
                ("fact:d", false),
                ("fact:e", false),
                ("fact:f", false),
                ("fact:g", false))));
        TestAssert.Equal(0, tracker.CurrentIndex);

        SplitRecord? allComplete = tracker.Update(
            TestSnapshots.Terraria(isGameMenu: false, bossStates: CreateFacts(("fact:a", true), ("fact:b", true))),
            TimeSpan.FromSeconds(1));
        TestAssert.Equal("split:all", allComplete?.Name);

        SplitRecord? anyComplete = tracker.Update(
            TestSnapshots.Terraria(isGameMenu: false, bossStates: CreateFacts(("fact:c", false), ("fact:d", true))),
            TimeSpan.FromSeconds(2));
        TestAssert.Equal("split:any", anyComplete?.Name);

        SplitRecord? atLeastIncomplete = tracker.Update(
            TestSnapshots.Terraria(isGameMenu: false, bossStates: CreateFacts(("fact:e", true), ("fact:f", false), ("fact:g", false))),
            TimeSpan.FromSeconds(3));
        TestAssert.Equal(null, atLeastIncomplete);

        SplitRecord? atLeastComplete = tracker.Update(
            TestSnapshots.Terraria(isGameMenu: false, bossStates: CreateFacts(("fact:e", true), ("fact:f", false), ("fact:g", true))),
            TimeSpan.FromSeconds(4));
        TestAssert.Equal("split:at-least", atLeastComplete?.Name);
    }

    private static void SplitTrackerSkipsEarlierSplitsWhenLaterSplitCompletes()
    {
        var tracker = new SplitTracker();
        tracker.SetDefinitions(
        [
            new SplitDefinition(
                "split:earlier",
                "Earlier",
                SplitCondition.Fact("fact:earlier"),
                [],
                [],
                []),
            new SplitDefinition(
                "split:required",
                "Required",
                SplitCondition.Fact("fact:required"),
                [],
                [],
                []),
            new SplitDefinition(
                "split:gate",
                "Gate",
                SplitCondition.Fact("fact:gate"),
                [],
                [],
                [])
        ]);
        tracker.OnRunStarted(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(("fact:earlier", false), ("fact:required", false), ("fact:gate", false))));

        SplitRecord? completed = tracker.Update(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(("fact:earlier", false), ("fact:required", true), ("fact:gate", false))),
            TimeSpan.FromSeconds(5));

        TestAssert.Equal("split:required", completed?.Name);
        TestAssert.Equal(1, completed?.Index);
        TestAssert.Equal(true, tracker.Statuses[0].IsSkipped);
        TestAssert.Equal(0, tracker.Statuses[0].CompletedFactKeys.Count);
        TestAssert.Equal(TimeSpan.FromSeconds(5), tracker.Statuses[1].Time);
        TestAssert.Equal(2, tracker.CurrentIndex);

        _ = tracker.Update(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(("fact:earlier", true), ("fact:required", true), ("fact:gate", false))),
            TimeSpan.FromSeconds(6));

        TestAssert.Equal(0, tracker.Statuses[0].CompletedFactKeys.Count);

        var initiallySatisfiedLaterTracker = new SplitTracker();
        initiallySatisfiedLaterTracker.SetDefinitions(
        [
            new SplitDefinition(
                "split:earlier",
                "Earlier",
                SplitCondition.Fact("fact:earlier"),
                [],
                [],
                []),
            new SplitDefinition(
                "split:required",
                "Required",
                SplitCondition.Fact("fact:required"),
                [],
                [],
                [])
        ]);
        initiallySatisfiedLaterTracker.OnRunStarted(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(("fact:earlier", false), ("fact:required", true))));

        if (!initiallySatisfiedLaterTracker.Statuses[0].IsSkipped)
        {
            throw new InvalidOperationException("Earlier split was not skipped after later split was initially satisfied.");
        }

        if (!initiallySatisfiedLaterTracker.Statuses[1].IsSkipped)
        {
            throw new InvalidOperationException("Initially satisfied required split was not skipped.");
        }
        TestAssert.Equal(2, initiallySatisfiedLaterTracker.CurrentIndex);
    }

    private static void SplitTrackerKeepsAttachedSplitsOutOfRouteDecisions()
    {
        const string firstMainFact = "fact:first-main";
        string flyingCarpetFact = SplitCatalog.CreateItemFactKey(857);
        string sandstormBottleFact = SplitCatalog.CreateItemFactKey(934);
        SplitCondition pyramidCondition = SplitCondition.Any(
        [
            SplitCatalog.CreateItemEverOwnedCondition(857, 1),
            SplitCatalog.CreateItemEverOwnedCondition(934, 1)
        ]);

        var firstStageAttachedTracker = new SplitTracker();
        firstStageAttachedTracker.SetDefinitions(
        [
            new SplitDefinition("split:pyramid", "金字塔", pyramidCondition, [], [], [], IsAttached: true),
            new SplitDefinition("split:first-main", "First Main", SplitCondition.Fact(firstMainFact), [], [], [])
        ]);
        firstStageAttachedTracker.OnRunStarted(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreatePyramidFacts(firstMainFact, false, flyingCarpetFact, 0, sandstormBottleFact, 0)));

        SplitRecord? sandstormBottleComplete = firstStageAttachedTracker.Update(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreatePyramidFacts(firstMainFact, false, flyingCarpetFact, 0, sandstormBottleFact, 1)),
            TimeSpan.FromSeconds(4));

        TestAssert.Equal("split:pyramid", sandstormBottleComplete?.Name);
        TestAssert.Equal(TimeSpan.FromSeconds(4), firstStageAttachedTracker.Statuses[0].Time);
        TestAssert.Equal(1, firstStageAttachedTracker.CurrentIndex);

        var initiallySatisfiedFirstStageAttachedTracker = new SplitTracker();
        initiallySatisfiedFirstStageAttachedTracker.SetDefinitions(
        [
            new SplitDefinition("split:pyramid", "金字塔", pyramidCondition, [], [], [], IsAttached: true),
            new SplitDefinition("split:first-main", "First Main", SplitCondition.Fact(firstMainFact), [], [], [])
        ]);
        initiallySatisfiedFirstStageAttachedTracker.OnRunStarted(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreatePyramidFacts(firstMainFact, false, flyingCarpetFact, 0, sandstormBottleFact, 1)));

        TestAssert.Equal(true, initiallySatisfiedFirstStageAttachedTracker.Statuses[0].IsSkipped);
        TestAssert.Equal(null, initiallySatisfiedFirstStageAttachedTracker.Statuses[0].Time);
        TestAssert.Equal(1, initiallySatisfiedFirstStageAttachedTracker.CurrentIndex);

        var initiallySatisfiedAttachedTracker = new SplitTracker();
        initiallySatisfiedAttachedTracker.SetDefinitions(
        [
            new SplitDefinition("split:first", "First", SplitCondition.Fact("fact:first"), [], [], []),
            new SplitDefinition("split:attached", "Attached", SplitCondition.Fact("fact:attached"), [], [], [], IsAttached: true),
            new SplitDefinition("split:second", "Second", SplitCondition.Fact("fact:second"), [], [], [])
        ]);
        initiallySatisfiedAttachedTracker.OnRunStarted(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(("fact:first", false), ("fact:attached", true), ("fact:second", false))));

        TestAssert.Equal(false, initiallySatisfiedAttachedTracker.Statuses[0].IsSkipped);
        TestAssert.Equal(false, initiallySatisfiedAttachedTracker.Statuses[1].IsSkipped);
        TestAssert.Equal(0, initiallySatisfiedAttachedTracker.CurrentIndex);

        SplitRecord? noEarlyAttachedComplete = initiallySatisfiedAttachedTracker.Update(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(("fact:first", false), ("fact:attached", true), ("fact:second", false))),
            TimeSpan.FromSeconds(1));
        TestAssert.Equal(null, noEarlyAttachedComplete);
        TestAssert.Equal(false, initiallySatisfiedAttachedTracker.Statuses[1].IsSkipped);

        SplitRecord? firstGateComplete = initiallySatisfiedAttachedTracker.Update(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(("fact:first", true), ("fact:attached", true), ("fact:second", false))),
            TimeSpan.FromSeconds(2));
        TestAssert.Equal("split:first", firstGateComplete?.Name);

        SplitRecord? stageOpenedAttached = initiallySatisfiedAttachedTracker.Update(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(("fact:first", true), ("fact:attached", true), ("fact:second", false))),
            TimeSpan.FromSeconds(3));
        TestAssert.Equal(null, stageOpenedAttached);
        TestAssert.Equal(true, initiallySatisfiedAttachedTracker.Statuses[1].IsSkipped);

        var outOfOrderAttachedTracker = new SplitTracker();
        outOfOrderAttachedTracker.SetDefinitions(
        [
            new SplitDefinition("split:first", "First", SplitCondition.Fact("fact:first"), [], [], []),
            new SplitDefinition("split:attached-a", "Attached A", SplitCondition.Fact("fact:attached-a"), [], [], [], IsAttached: true),
            new SplitDefinition("split:attached-b", "Attached B", SplitCondition.Fact("fact:attached-b"), [], [], [], IsAttached: true),
            new SplitDefinition("split:second", "Second", SplitCondition.Fact("fact:second"), [], [], [])
        ]);
        outOfOrderAttachedTracker.OnRunStarted(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(
                    ("fact:first", false),
                    ("fact:attached-a", false),
                    ("fact:attached-b", false),
                    ("fact:second", false))));

        SplitRecord? firstComplete = outOfOrderAttachedTracker.Update(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(
                    ("fact:first", true),
                    ("fact:attached-a", false),
                    ("fact:attached-b", false),
                    ("fact:second", false))),
            TimeSpan.FromSeconds(1));
        TestAssert.Equal("split:first", firstComplete?.Name);

        SplitRecord? attachedBComplete = outOfOrderAttachedTracker.Update(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(
                    ("fact:first", true),
                    ("fact:attached-a", false),
                    ("fact:attached-b", true),
                    ("fact:second", false))),
            TimeSpan.FromSeconds(2));
        TestAssert.Equal("split:attached-b", attachedBComplete?.Name);
        TestAssert.Equal(false, outOfOrderAttachedTracker.Statuses[1].IsSkipped);
        TestAssert.Equal(TimeSpan.FromSeconds(2), outOfOrderAttachedTracker.Statuses[2].Time);
        TestAssert.Equal(1, outOfOrderAttachedTracker.CurrentIndex);

        var mainCompletionTracker = new SplitTracker();
        mainCompletionTracker.SetDefinitions(
        [
            new SplitDefinition("split:first", "First", SplitCondition.Fact("fact:first"), [], [], []),
            new SplitDefinition("split:attached", "Attached", SplitCondition.Fact("fact:attached"), [], [], [], IsAttached: true),
            new SplitDefinition("split:second", "Second", SplitCondition.Fact("fact:second"), [], [], [])
        ]);
        mainCompletionTracker.OnRunStarted(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(("fact:first", false), ("fact:attached", false), ("fact:second", false))));

        SplitRecord? secondComplete = mainCompletionTracker.Update(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(("fact:first", false), ("fact:attached", false), ("fact:second", true))),
            TimeSpan.FromSeconds(3));

        TestAssert.Equal("split:second", secondComplete?.Name);
        TestAssert.Equal(true, mainCompletionTracker.Statuses[0].IsSkipped);
        TestAssert.Equal(true, mainCompletionTracker.Statuses[1].IsSkipped);
        TestAssert.Equal(TimeSpan.FromSeconds(3), mainCompletionTracker.Statuses[2].Time);
    }

    private static TerrariaGameFacts CreatePyramidFacts(
        string mainFactKey,
        bool mainValue,
        string firstItemFactKey,
        int firstItemCount,
        string secondItemFactKey,
        int secondItemCount)
    {
        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        builder.SetBoolean(mainFactKey, mainValue);
        builder.SetInteger(firstItemFactKey, firstItemCount);
        builder.SetInteger(secondItemFactKey, secondItemCount);
        return builder.Build();
    }

    private static void SplitTrackerRecordsCompletionFactKeysForOrDisplay()
    {
        SplitCondition skeletron = SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron);
        SplitCondition wallOfFlesh = SplitCatalog.CreateBossFactCondition(SplitCatalog.WallOfFlesh);
        var tracker = new SplitTracker();
        tracker.SetDefinitions(
        [
            new SplitDefinition(
                "split:any-boss",
                "Any Boss",
                SplitCondition.Any([skeletron, wallOfFlesh]),
                ["skeletron.png", "wof.png"],
                [SplitCatalog.Skeletron, SplitCatalog.WallOfFlesh],
                [SplitCatalog.Skeletron, SplitCatalog.WallOfFlesh]),
            new SplitDefinition(
                "split:gate",
                "Gate",
                SplitCondition.Fact("fact:gate"),
                [],
                [],
                [])
        ]);

        tracker.OnRunStarted(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(
                    (skeletron.FactKey, false),
                    (wallOfFlesh.FactKey, false),
                    ("fact:gate", false))));

        SplitRecord? completed = tracker.Update(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(
                    (skeletron.FactKey, false),
                    (wallOfFlesh.FactKey, true),
                    ("fact:gate", false))),
            TimeSpan.FromSeconds(10));

        TestAssert.Equal("split:any-boss", completed?.Name);
        SplitStatus status = tracker.Statuses[0];
        TestAssert.Equal(wallOfFlesh.FactKey, status.CompletedFactKeys.Single());
        TestAssert.Equal(2, status.Definition.IconKeys.Count);

        _ = tracker.Update(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(
                    (skeletron.FactKey, true),
                    (wallOfFlesh.FactKey, true),
                    ("fact:gate", false))),
            TimeSpan.FromSeconds(11));

        TestAssert.Equal(2, status.CompletedFactKeys.Count);
        TestAssert.Equal(true, status.CompletedFactKeys.Contains(skeletron.FactKey, StringComparer.OrdinalIgnoreCase));
        TestAssert.Equal(true, status.CompletedFactKeys.Contains(wallOfFlesh.FactKey, StringComparer.OrdinalIgnoreCase));
        TestAssert.Equal(TimeSpan.FromSeconds(10), status.FactCompletionTimes[wallOfFlesh.FactKey]);
        TestAssert.Equal(TimeSpan.FromSeconds(11), status.FactCompletionTimes[skeletron.FactKey]);
    }

    private static void SplitTrackerRecordsPartialFactCompletionTimes()
    {
        var tracker = new SplitTracker();
        tracker.SetDefinitions(
        [
            new SplitDefinition(
                "split:expanded",
                "Expanded",
                SplitCondition.AtLeast(
                [
                    SplitCondition.Fact("fact:a"),
                    SplitCondition.Fact("fact:b")
                ], 2),
                [],
                [],
                [])
        ]);
        tracker.OnRunStarted(TestSnapshots.Terraria(
            isGameMenu: false,
            bossStates: CreateFacts(("fact:a", false), ("fact:b", false))));

        SplitRecord? partial = tracker.Update(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(("fact:a", true), ("fact:b", false))),
            TimeSpan.FromSeconds(5));

        TestAssert.Equal(null, partial);
        SplitStatus status = tracker.Statuses[0];
        TestAssert.Equal(null, status.Time);
        TestAssert.Equal(TimeSpan.FromSeconds(5), status.FactCompletionTimes["fact:a"]);
        TestAssert.Equal(false, status.FactCompletionTimes.ContainsKey("fact:b"));

        SplitRecord? completed = tracker.Update(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(("fact:a", true), ("fact:b", true))),
            TimeSpan.FromSeconds(9));

        TestAssert.Equal("split:expanded", completed?.Name);
        TestAssert.Equal(TimeSpan.FromSeconds(5), status.FactCompletionTimes["fact:a"]);
        TestAssert.Equal(TimeSpan.FromSeconds(9), status.FactCompletionTimes["fact:b"]);
    }

    private static void SplitTrackerKeepsHistoricalFactKeysWhenSplitCompletes()
    {
        var tracker = new SplitTracker();
        tracker.SetDefinitions(
        [
            new SplitDefinition(
                "split:expanded",
                "Expanded",
                SplitCondition.AtLeast(
                [
                    SplitCondition.Fact("fact:a"),
                    SplitCondition.Fact("fact:b"),
                    SplitCondition.Fact("fact:c")
                ], 2),
                [],
                [],
                [])
        ]);
        tracker.OnRunStarted(TestSnapshots.Terraria(
            isGameMenu: false,
            bossStates: CreateFacts(("fact:a", false), ("fact:b", false), ("fact:c", false))));

        _ = tracker.Update(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(("fact:a", true), ("fact:b", false), ("fact:c", false))),
            TimeSpan.FromSeconds(5));

        SplitRecord? completed = tracker.Update(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(("fact:a", false), ("fact:b", true), ("fact:c", true))),
            TimeSpan.FromSeconds(9));

        TestAssert.Equal("split:expanded", completed?.Name);
        SplitStatus status = tracker.Statuses[0];
        TestAssert.Equal(3, status.CompletedFactKeys.Count);
        TestAssert.Equal(true, status.CompletedFactKeys.Contains("fact:a", StringComparer.OrdinalIgnoreCase));
        TestAssert.Equal(true, status.CompletedFactKeys.Contains("fact:b", StringComparer.OrdinalIgnoreCase));
        TestAssert.Equal(true, status.CompletedFactKeys.Contains("fact:c", StringComparer.OrdinalIgnoreCase));
        TestAssert.Equal(TimeSpan.FromSeconds(5), status.FactCompletionTimes["fact:a"]);
        TestAssert.Equal(TimeSpan.FromSeconds(9), status.FactCompletionTimes["fact:b"]);
        TestAssert.Equal(TimeSpan.FromSeconds(9), status.FactCompletionTimes["fact:c"]);
    }

    private static void SplitTrackerRemembersEverOwnedItemCounts()
    {
        const string gateFact = "fact:gate";
        string currentItemFact = SplitCatalog.CreateItemFactKey(50);
        var tracker = new SplitTracker();
        tracker.SetDefinitions(
        [
            new SplitDefinition("split:gate", "Gate", SplitCondition.All([SplitCondition.Fact(gateFact)]), [], [], []),
            new SplitDefinition("split:ever-item", "Ever Item", SplitCondition.All([SplitCatalog.CreateItemEverOwnedCondition(50, 2)]), [], [], [])
        ]);
        tracker.OnRunStarted(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts((gateFact, false), (currentItemFact, 0))));

        SplitRecord? gateComplete = tracker.Update(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts((gateFact, true), (currentItemFact, 3))),
            TimeSpan.FromSeconds(1));
        TestAssert.Equal("split:gate", gateComplete?.Name);

        SplitRecord? everItemComplete = tracker.Update(
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts((gateFact, true), (currentItemFact, 0))),
            TimeSpan.FromSeconds(2));
        TestAssert.Equal("split:ever-item", everItemComplete?.Name);
    }

    private static bool HasEvent(IReadOnlyList<RunEvent> events, RunEventKind kind)
    {
        return events.Any(runEvent => runEvent.Kind == kind);
    }

    private static MenuActionKind? GetMenuAction(IReadOnlyList<RunEvent> events)
    {
        foreach (RunEvent runEvent in events)
        {
            if (runEvent.Kind == RunEventKind.MenuActionRequested)
            {
                return runEvent.MenuAction;
            }
        }

        return null;
    }

    private static int? GetCompletedSplitIndex(IReadOnlyList<RunEvent> events)
    {
        foreach (RunEvent runEvent in events)
        {
            if (runEvent.Kind == RunEventKind.SplitCompleted)
            {
                return runEvent.SplitIndex;
            }
        }

        return null;
    }

    private static SplitTracker CreateSingleSkeletronTracker()
    {
        var tracker = new SplitTracker();
        tracker.SetDefinitions([
            new SplitDefinition(
                "split:skeletron",
                "Skeletron",
                SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                Array.Empty<string>(),
                Array.Empty<string>(),
                [SplitCatalog.Skeletron])
        ]);
        return tracker;
    }

    private static void WatcherRuntimeProcessorReusesUnchangedSnapshots()
    {
        var processor = new WatcherRuntimeProcessor(TimeSpan.FromSeconds(1));
        _ = processor.ApplyCommand(
            RuntimeCommand.SetDefinitions([
                new SplitDefinition(
                    "split:skeletron",
                    "Skeletron",
                    SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    [SplitCatalog.Skeletron])
            ]),
            1_000);

        RuntimeProcessorTickResult first = processor.Tick(TestSnapshots.Terraria(isGameMenu: true), 2_000, []);
        RuntimeProcessorTickResult second = processor.Tick(TestSnapshots.Terraria(isGameMenu: true), 3_000, []);
        TestAssert.Equal(true, ReferenceEquals(first.Snapshot, second.Snapshot));
        TestAssert.Equal(0, second.Events.Count);

        RuntimeProcessorTickResult started = processor.Tick(
            TestSnapshots.Terraria(isGameMenu: false, bossStates: CreateSkeletronFacts(false), enteredWorld: true),
            4_000,
            []);
        TestAssert.Equal(false, ReferenceEquals(second.Snapshot, started.Snapshot));
        TestAssert.Equal(true, HasEvent(started.Events, RunEventKind.RunStarted));

        RuntimeProcessorTickResult running = processor.Tick(
            TestSnapshots.Terraria(isGameMenu: false, bossStates: CreateSkeletronFacts(false)),
            5_000,
            []);
        TestAssert.Equal(true, ReferenceEquals(started.Snapshot, running.Snapshot));

        RuntimeProcessorTickResult completed = processor.Tick(
            TestSnapshots.Terraria(isGameMenu: false, bossStates: CreateSkeletronFacts(true)),
            6_000,
            []);
        TestAssert.Equal(false, ReferenceEquals(running.Snapshot, completed.Snapshot));
        TestAssert.Equal(0, GetCompletedSplitIndex(completed.Events));
    }

    private static TerrariaGameFacts CreateSkeletronFacts(bool defeated)
    {
        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        builder.SetBoolean(
            SplitCatalog.BossFacts.First(boss => boss.TargetId == SplitCatalog.Skeletron).FactKey,
            defeated);
        return builder.Build();
    }

    private static TerrariaGameFacts CreateFacts(params (string Key, bool Value)[] values)
    {
        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        foreach ((string key, bool value) in values)
        {
            builder.SetBoolean(key, value);
        }

        return builder.Build();
    }

    private static TerrariaGameFacts CreateFacts(params (string Key, int Value)[] values)
    {
        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        foreach ((string key, int value) in values)
        {
            builder.SetInteger(key, value);
        }

        return builder.Build();
    }

    private static TerrariaGameFacts CreateFacts((string Key, bool Value) first, (string Key, int Value) second)
    {
        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        builder.SetBoolean(first.Key, first.Value);
        builder.SetInteger(second.Key, second.Value);
        return builder.Build();
    }
}
