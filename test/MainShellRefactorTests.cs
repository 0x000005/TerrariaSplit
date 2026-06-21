using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using TerrariaSplit;

namespace TerrariaSplit.Tests;

internal static class MainShellRefactorTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("TerrariaMonitorCoordinator preserves watcher interval policy", TerrariaMonitorCoordinatorPreservesWatcherIntervalPolicy);
        yield return ("TerrariaMonitorCoordinator publishes only changed or heartbeat completions", TerrariaMonitorCoordinatorPublishesOnlyChangedOrHeartbeatCompletions);
        yield return ("TerrariaMonitorCoordinator polls watcher without UI ticks", TerrariaMonitorCoordinatorPollsWatcherWithoutUiTicks);
        yield return ("TerrariaMonitorCoordinator produces runtime state without UI ticks", TerrariaMonitorCoordinatorProducesRuntimeStateWithoutUiTicks);
        yield return ("TerrariaMonitorCoordinator clears queued menu actions before processing", TerrariaMonitorCoordinatorClearsQueuedMenuActionsBeforeProcessing);
        yield return ("TerrariaMonitorCoordinator does not duplicate in-flight polls", TerrariaMonitorCoordinatorDoesNotDuplicateInflightPolls);
        yield return ("TerrariaMonitorCoordinator deduplicates repeated patch logs", TerrariaMonitorCoordinatorDeduplicatesRepeatedPatchLogs);
        yield return ("TerrariaMonitorCoordinator reset clears applied patch state", TerrariaMonitorCoordinatorResetClearsAppliedPatchState);
        yield return ("OverlayWindowController queues render once while pending", OverlayWindowControllerQueuesRenderOnceWhilePending);
        yield return ("OverlayWindowController click-through style preserves unrelated bits", OverlayWindowControllerPreservesUnrelatedStyleBits);
        yield return ("OverlayWindowController strips non-client border style", OverlayWindowControllerStripsNonClientBorderStyle);
        yield return ("WindowLayerController applies always-on-top setting without blocking input", WindowLayerControllerAppliesAlwaysOnTopSettingWithoutBlockingInput);
        yield return ("WindowLayerController blocks main windows while modal is registered", WindowLayerControllerBlocksMainWindowsWhileModalIsRegistered);
        yield return ("WindowLayerController ignores modal activation when no modal is registered", WindowLayerControllerIgnoresModalActivationWhenNoModalIsRegistered);
        yield return ("ProgramModalWindowCoordinator registers modal forms through one gateway", ProgramModalWindowCoordinatorRegistersModalFormsThroughOneGateway);
        yield return ("ProgramModalWindowCoordinator enables only the current nested modal", ProgramModalWindowCoordinatorEnablesOnlyCurrentNestedModal);
        yield return ("MainWindowModalInputRouter redirects blocked main window activation", MainWindowModalInputRouterRedirectsBlockedMainWindowActivation);
        yield return ("MainFormContextMenuBuilder exposes pyramid filter toggle", MainFormContextMenuBuilderExposesPyramidFilterToggle);
        yield return ("PracticeWorldSelectorForm uses save selector text", PracticeWorldSelectorFormUsesSaveSelectorText);
        yield return ("PracticeWorldSelectorForm scales layout with display context", PracticeWorldSelectorFormScalesLayoutWithDisplayContext);
        yield return ("HotkeyWarningDialog uses plain dialog content", HotkeyWarningDialogUsesPlainDialogContent);
        yield return ("SettingsMessageDialog uses themed dialog chrome", SettingsMessageDialogUsesThemedDialogChrome);
        yield return ("Settings form title bar uses icon window buttons", SettingsFormTitleBarUsesIconWindowButtons);
        yield return ("SettingsUiFactory keeps two-column editor column fixed width", SettingsUiFactoryKeepsTwoColumnEditorColumnFixedWidth);
        yield return ("SettingsUiFactory row labels ellipsize clipped text", SettingsUiFactoryRowLabelsEllipsizeClippedText);
        yield return ("SettingsUiFactory hides native multiline scrollbars", SettingsUiFactoryHidesNativeMultilineScrollbars);
        yield return ("ThemedScrollPanel routes list wheel to inner list until boundary", ThemedScrollPanelRoutesListWheelToInnerListUntilBoundary);
        yield return ("FontFamilySelector uses themed drop-down list", FontFamilySelectorUsesThemedDropDownList);
        yield return ("Settings form uses themed drop-down lists", SettingsFormUsesThemedDropDownLists);
    }

    private static void TerrariaMonitorCoordinatorPreservesWatcherIntervalPolicy()
    {
        TestAssert.Equal(
            TimeSpan.FromSeconds(1),
            TerrariaMonitorCoordinator.GetNextWatcherPollInterval(
                new TerrariaWatchSnapshot(false, null, false, null, TerrariaGameFacts.Unknown, TerrariaWorldGenerationState.Unknown, false, "waiting"),
                SplitTimerPhase.NotStarted));
        TestAssert.Equal(
            TimeSpan.FromMilliseconds(250),
            TerrariaMonitorCoordinator.GetNextWatcherPollInterval(
                new TerrariaWatchSnapshot(true, 123, false, true, TerrariaGameFacts.Unknown, TerrariaWorldGenerationState.Unknown, false, "not ready"),
                SplitTimerPhase.NotStarted));
        TestAssert.Equal(
            TimeSpan.FromMilliseconds(5),
            TerrariaMonitorCoordinator.GetNextWatcherPollInterval(TestSnapshots.Terraria(isGameMenu: false), SplitTimerPhase.Running));
        TestAssert.Equal(
            TimeSpan.FromMilliseconds(5),
            TerrariaMonitorCoordinator.GetNextWatcherPollInterval(TestSnapshots.Terraria(isGameMenu: true), SplitTimerPhase.Paused));
    }

    private static void TerrariaMonitorCoordinatorPublishesOnlyChangedOrHeartbeatCompletions()
    {
        TimeSpan heartbeat = TimeSpan.FromMilliseconds(250);
        long heartbeatTicks = (long)(heartbeat.TotalSeconds * Stopwatch.Frequency);
        TerrariaWatchSnapshot snapshot = TestSnapshots.Terraria(isGameMenu: true);
        RuntimeRunSnapshot runtimeSnapshot = RuntimeRunSnapshot.Empty;
        TerrariaWatcherDiagnostics diagnostics = TerrariaWatcherDiagnosticsDefaults.Empty;

        WatcherPollCompletion MakeCompletion(
            long completedTimestamp,
            TerrariaWatchSnapshot? snapshotOverride = null,
            RuntimeRunSnapshot? runtimeOverride = null,
            IReadOnlyList<RunEvent>? events = null,
            long commandSequence = 0)
        {
            return new WatcherPollCompletion(
                snapshotOverride ?? snapshot,
                diagnostics,
                runtimeOverride ?? runtimeSnapshot,
                events ?? [],
                commandSequence,
                TimeSpan.FromMilliseconds(1),
                completedTimestamp,
                TimeSpan.FromMilliseconds(5),
                TimeSpan.FromMilliseconds(5),
                null);
        }

        // First completion always publishes.
        WatcherPollCompletion first = MakeCompletion(1_000);
        TestAssert.Equal(true, TerrariaMonitorCoordinator.ShouldPublishWatcherCompletion(
            first, WatcherPublishState.Empty, heartbeat));
        WatcherPublishState published = WatcherPublishState.FromCompletion(first);

        // Unchanged state inside the heartbeat window stays silent.
        TestAssert.Equal(false, TerrariaMonitorCoordinator.ShouldPublishWatcherCompletion(
            MakeCompletion(1_000 + heartbeatTicks / 2), published, heartbeat));

        // Heartbeat republishes unchanged state.
        TestAssert.Equal(true, TerrariaMonitorCoordinator.ShouldPublishWatcherCompletion(
            MakeCompletion(1_000 + heartbeatTicks), published, heartbeat));

        // Snapshot value change publishes immediately.
        TestAssert.Equal(true, TerrariaMonitorCoordinator.ShouldPublishWatcherCompletion(
            MakeCompletion(1_001, snapshotOverride: TestSnapshots.Terraria(isGameMenu: false)),
            published,
            heartbeat));

        // Runtime snapshot instance change publishes immediately.
        var changedRuntime = RuntimeRunSnapshot.Empty with { CurrentSplitIndex = 1 };
        TestAssert.Equal(true, TerrariaMonitorCoordinator.ShouldPublishWatcherCompletion(
            MakeCompletion(1_001, runtimeOverride: changedRuntime), published, heartbeat));

        // Run events publish immediately.
        TestAssert.Equal(true, TerrariaMonitorCoordinator.ShouldPublishWatcherCompletion(
            MakeCompletion(1_001, events: [new RunEvent(RunEventKind.RunStarted)]), published, heartbeat));

        // Newly applied runtime commands publish immediately.
        TestAssert.Equal(true, TerrariaMonitorCoordinator.ShouldPublishWatcherCompletion(
            MakeCompletion(1_001, commandSequence: 7), published, heartbeat));
    }

    private static void TerrariaMonitorCoordinatorPollsWatcherWithoutUiTicks()
    {
        var watcher = new FakeWatcher(TestSnapshots.Terraria(isGameMenu: true));
        var patch = new FakePatchApplier(TerrariaUiScalePatchResult.NoProcess());
        using var coordinator = new TerrariaMonitorCoordinator(
            watcher,
            patch,
            action => action(),
            utcNowProvider: () => DateTime.UtcNow);

        coordinator.Tick(SplitTimerPhase.NotStarted, patchEnabled: false);

        TestAssert.Equal(true, SpinWait.SpinUntil(() => watcher.PollCount >= 2, 1000));
    }

    private static void TerrariaMonitorCoordinatorProducesRuntimeStateWithoutUiTicks()
    {
        var watcher = new SequenceWatcher(
        [
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: TerrariaGameFacts.Unknown,
                enteredWorld: true),
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateSkeletronState(false)),
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateSkeletronState(true))
        ]);
        var patch = new FakePatchApplier(TerrariaUiScalePatchResult.NoProcess());
        WatcherPollNotification? completion = null;
        using var coordinator = new TerrariaMonitorCoordinator(
            watcher,
            patch,
            action => action(),
            utcNowProvider: () => DateTime.UtcNow);
        _ = coordinator.SetRuntimeDefinitions(CreateSingleBossDefinitions());
        coordinator.WatcherPollCompleted += notification =>
        {
            if (notification.RunEvents.Any(runEvent =>
                    runEvent.Kind == RunEventKind.SplitCompleted &&
                    runEvent.SplitIndex == 0))
            {
                completion = notification;
            }
        };

        coordinator.Tick(SplitTimerPhase.NotStarted, patchEnabled: false);

        TestAssert.Equal(true, SpinWait.SpinUntil(() => completion.HasValue, 1000));
        TestAssert.Equal(true, completion!.Value.RuntimeSnapshot.Statuses[0].Time.HasValue);
    }

    private static void TerrariaMonitorCoordinatorClearsQueuedMenuActionsBeforeProcessing()
    {
        var watcher = new BlockingWatcher(TestSnapshots.Terraria(isGameMenu: true));
        var patch = new FakePatchApplier(TerrariaUiScalePatchResult.NoProcess());
        var requestedActions = new List<MenuActionKind>();
        using var coordinator = new TerrariaMonitorCoordinator(
            watcher,
            patch,
            action => action(),
            utcNowProvider: () => DateTime.UtcNow);
        coordinator.WatcherPollCompleted += notification =>
        {
            foreach (RunEvent runEvent in notification.RunEvents)
            {
                if (runEvent.Kind == RunEventKind.MenuActionRequested &&
                    runEvent.MenuAction is MenuActionKind action)
                {
                    requestedActions.Add(action);
                }
            }
        };

        coordinator.Tick(SplitTimerPhase.NotStarted, patchEnabled: false);
        SpinWait.SpinUntil(() => watcher.PollCount > 0, 1000);
        _ = coordinator.SubmitRuntimeCommand(
            RuntimeCommand.QueueMenuAction(MenuActionKind.CreateWorld, DateTime.UtcNow));
        _ = coordinator.ClearPendingMenuActions();
        watcher.Release();

        SpinWait.SpinUntil(() => watcher.CompletedCount >= 2, 1000);

        TestAssert.Equal(0, requestedActions.Count);
    }

    private static void TerrariaMonitorCoordinatorDoesNotDuplicateInflightPolls()
    {
        var watcher = new BlockingWatcher(TestSnapshots.Terraria(isGameMenu: true));
        var patch = new FakePatchApplier(TerrariaUiScalePatchResult.NoProcess());
        using var coordinator = new TerrariaMonitorCoordinator(
            watcher,
            patch,
            action => action(),
            utcNowProvider: () => DateTime.UtcNow);

        coordinator.Tick(SplitTimerPhase.NotStarted, patchEnabled: false);
        SpinWait.SpinUntil(() => watcher.PollCount > 0, 1000);
        coordinator.Tick(SplitTimerPhase.NotStarted, patchEnabled: false);
        TestAssert.Equal(1, watcher.PollCount);

        watcher.Release();
        SpinWait.SpinUntil(() => watcher.CompletedCount > 0, 1000);
    }

    private static void TerrariaMonitorCoordinatorDeduplicatesRepeatedPatchLogs()
    {
        DateTime now = DateTime.UtcNow;
        var logs = new List<string>();
        var watcher = new FakeWatcher(new TerrariaWatchSnapshot(
            false,
            null,
            false,
            null,
            TerrariaGameFacts.Unknown,
            TerrariaWorldGenerationState.Unknown,
            false,
            "waiting"));
        var patch = new FakePatchApplier(TerrariaUiScalePatchResult.Applied(321, "patched"));
        using var coordinator = new TerrariaMonitorCoordinator(
            watcher,
            patch,
            action => action(),
            logInfo: logs.Add,
            utcNowProvider: () => now,
            isProcessStillRunning: _ => false);

        coordinator.Tick(SplitTimerPhase.NotStarted, patchEnabled: true);
        SpinWait.SpinUntil(() => patch.CallCount == 1, 1000);
        SpinWait.SpinUntil(() => logs.Count == 1, 1000);

        now += TimeSpan.FromSeconds(3);
        coordinator.Tick(SplitTimerPhase.NotStarted, patchEnabled: true);
        SpinWait.SpinUntil(() => patch.CallCount == 2, 1000);
        Thread.Sleep(50);

        TestAssert.Equal(1, logs.Count);
    }

    private static void TerrariaMonitorCoordinatorResetClearsAppliedPatchState()
    {
        DateTime now = DateTime.UtcNow;
        var watcher = new FakeWatcher(new TerrariaWatchSnapshot(
            false,
            null,
            false,
            null,
            TerrariaGameFacts.Unknown,
            TerrariaWorldGenerationState.Unknown,
            false,
            "waiting"));
        var patch = new FakePatchApplier(TerrariaUiScalePatchResult.Applied(111, "patched"));
        using var coordinator = new TerrariaMonitorCoordinator(
            watcher,
            patch,
            action => action(),
            utcNowProvider: () => now,
            isProcessStillRunning: _ => true);

        coordinator.Tick(SplitTimerPhase.NotStarted, patchEnabled: true);
        SpinWait.SpinUntil(() => patch.CallCount == 1, 1000);

        now += TimeSpan.FromSeconds(3);
        coordinator.Tick(SplitTimerPhase.NotStarted, patchEnabled: true);
        TestAssert.Equal(1, patch.CallCount);

        coordinator.ResetUiScalePatchState();
        now += TimeSpan.FromSeconds(3);
        coordinator.Tick(SplitTimerPhase.NotStarted, patchEnabled: true);
        SpinWait.SpinUntil(() => patch.CallCount == 2, 1000);
    }

    private static void OverlayWindowControllerQueuesRenderOnceWhilePending()
    {
        RunSta(() =>
        {
            using var form = new Form { Size = new Size(200, 100) };
            IntPtr _ = form.Handle;
            var queue = new List<Action>();
            int draws = 0;
            using var controller = new OverlayWindowController(
                form,
                graphics =>
                {
                    draws++;
                    return true;
                },
                _ => { },
                dispatch: queue.Add,
                updateLayeredBitmap: _ => true);

            controller.QueueRender();
            controller.QueueRender();

            TestAssert.Equal(1, queue.Count);
            queue[0]();
            TestAssert.Equal(1, draws);
        });
    }

    private static void OverlayWindowControllerPreservesUnrelatedStyleBits()
    {
        const int sentinel = 0x1000;
        int clickThrough = OverlayWindowController.ComposeExtendedStyle(sentinel, mouseClickThrough: true);
        int normal = OverlayWindowController.ComposeExtendedStyle(sentinel, mouseClickThrough: false);

        TestAssert.Equal(true, (clickThrough & sentinel) == sentinel);
        TestAssert.Equal(true, (normal & sentinel) == sentinel);
        TestAssert.Equal(true, (clickThrough & 0x20) == 0x20);
        TestAssert.Equal(false, (normal & 0x20) == 0x20);
    }

    private static void OverlayWindowControllerStripsNonClientBorderStyle()
    {
        const int sentinel = 0x1000;
        const int nonClient = 0x00C00000 | 0x00800000 | 0x00400000 | 0x00040000;
        int style = OverlayWindowController.ComposeBorderlessStyle(sentinel | nonClient);

        TestAssert.Equal(true, (style & sentinel) == sentinel);
        TestAssert.Equal(0, style & nonClient);
    }

    private static void WindowLayerControllerAppliesAlwaysOnTopSettingWithoutBlockingInput()
    {
        RunSta(() =>
        {
            bool? timerBlocked = null;
            using var form = new Form();
            _ = form.Handle;
            var controller = new WindowLayerController(
                form,
                value => timerBlocked = value,
                () => IntPtr.Zero);

            controller.SetAlwaysOnTop(true);
            TestAssert.Equal(true, controller.AlwaysOnTop);
            TestAssert.Equal(true, NativeMethods.IsWindowEnabled(form.Handle));
            TestAssert.Equal(null, timerBlocked);

            controller.SetAlwaysOnTop(false);
            TestAssert.Equal(false, controller.AlwaysOnTop);
            TestAssert.Equal(true, NativeMethods.IsWindowEnabled(form.Handle));
            TestAssert.Equal(null, timerBlocked);
        });
    }

    private static void WindowLayerControllerBlocksMainWindowsWhileModalIsRegistered()
    {
        RunSta(() =>
        {
            bool? timerBlocked = null;
            using var form = new Form();
            using var modal = new Form();
            _ = form.Handle;
            _ = modal.Handle;
            var controller = new WindowLayerController(
                form,
                value => timerBlocked = value,
                () => IntPtr.Zero);

            using (controller.RegisterModalWindow(() => modal.Handle))
            {
                TestAssert.Equal(false, NativeMethods.IsWindowEnabled(form.Handle));
                TestAssert.Equal(true, timerBlocked);
                TestAssert.Equal(true, controller.HasModalWindow);
                TestAssert.Equal(true, controller.RedirectMainWindowInputToModal());
            }

            TestAssert.Equal(true, NativeMethods.IsWindowEnabled(form.Handle));
            TestAssert.Equal(false, timerBlocked);
            TestAssert.Equal(false, controller.HasModalWindow);
        });
    }

    private static void WindowLayerControllerIgnoresModalActivationWhenNoModalIsRegistered()
    {
        RunSta(() =>
        {
            bool? timerBlocked = null;
            using var form = new Form();
            _ = form.Handle;
            var controller = new WindowLayerController(
                form,
                value => timerBlocked = value,
                () => IntPtr.Zero);

            TestAssert.Equal(false, controller.HasModalWindow);
            TestAssert.Equal(false, controller.RedirectMainWindowInputToModal());
            TestAssert.Equal(null, timerBlocked);
            TestAssert.Equal(true, NativeMethods.IsWindowEnabled(form.Handle));
        });
    }

    private static void ProgramModalWindowCoordinatorRegistersModalFormsThroughOneGateway()
    {
        RunSta(() =>
        {
            bool? timerBlocked = null;
            using var mainForm = new Form();
            using var modalForm = new Form();
            _ = mainForm.Handle;
            var coordinator = new ProgramModalWindowCoordinator(
                mainForm,
                value => timerBlocked = value,
                () => IntPtr.Zero);

            using (coordinator.RegisterModalForm(modalForm))
            {
                TestAssert.Equal(true, coordinator.HasModalWindow);
                TestAssert.Equal(false, NativeMethods.IsWindowEnabled(mainForm.Handle));
                TestAssert.Equal(true, timerBlocked);
            }

            TestAssert.Equal(false, coordinator.HasModalWindow);
            TestAssert.Equal(true, NativeMethods.IsWindowEnabled(mainForm.Handle));
            TestAssert.Equal(false, timerBlocked);
        });
    }

    private static void ProgramModalWindowCoordinatorEnablesOnlyCurrentNestedModal()
    {
        RunSta(() =>
        {
            using var mainForm = new Form();
            using var firstModal = new Form();
            using var secondModal = new Form();
            _ = mainForm.Handle;
            var coordinator = new ProgramModalWindowCoordinator(
                mainForm,
                _ => { },
                () => IntPtr.Zero);

            using (coordinator.RegisterModalForm(firstModal))
            {
                TestAssert.Equal(true, NativeMethods.IsWindowEnabled(firstModal.Handle));

                using (coordinator.RegisterModalForm(secondModal))
                {
                    TestAssert.Equal(false, NativeMethods.IsWindowEnabled(firstModal.Handle));
                    TestAssert.Equal(true, NativeMethods.IsWindowEnabled(secondModal.Handle));
                }

                TestAssert.Equal(true, NativeMethods.IsWindowEnabled(firstModal.Handle));
            }
        });
    }

    private static void MainWindowModalInputRouterRedirectsBlockedMainWindowActivation()
    {
        RunSta(() =>
        {
            bool stoppedMainInteraction = false;
            using var mainForm = new Form();
            using var modalForm = new Form();
            using var contextMenu = new ContextMenuStrip();
            _ = mainForm.Handle;
            var coordinator = new ProgramModalWindowCoordinator(
                mainForm,
                _ => { },
                () => IntPtr.Zero);
            var router = new MainWindowModalInputRouter(
                coordinator,
                contextMenu,
                () => stoppedMainInteraction = true);

            using (coordinator.RegisterModalForm(modalForm))
            {
                Message message = Message.Create(
                    mainForm.Handle,
                    MainWindowModalInputRouter.WmMouseActivate,
                    IntPtr.Zero,
                    IntPtr.Zero);

                TestAssert.Equal(true, router.TryHandleWindowMessage(ref message));
                TestAssert.Equal(true, stoppedMainInteraction);
                TestAssert.Equal((IntPtr)MainWindowModalInputRouter.MaNoActivateAndEat, message.Result);
            }
        });
    }

    private static void MainFormContextMenuBuilderExposesPyramidFilterToggle()
    {
        RunSta(() =>
        {
            int toggleCount = 0;
            using var menu = new ContextMenuStrip();
            var settings = new AppSettings
            {
                Language = "\u4E2D\u6587",
                AutoCreate = new AutoCreateWorldSettings
                {
                    EnablePyramidFilter = true
                }
            };

            new MainFormContextMenuBuilder().Rebuild(
                menu,
                settings,
                () => { },
                () => { },
                () => toggleCount++,
                _ => { },
                () => { });

            ToolStripMenuItem item = menu.Items
                .OfType<ToolStripMenuItem>()
                .Single(menuItem => menuItem.Name == MainFormContextMenuBuilder.PyramidFilterToggleItemName);

            TestAssert.Equal("\u7B5B\u5854", item.Text);
            TestAssert.Equal(true, item.Checked);

            item.PerformClick();

            TestAssert.Equal(1, toggleCount);
        });
    }

    private static void PracticeWorldSelectorFormUsesSaveSelectorText()
    {
        RunSta(() =>
        {
            using var englishForm = new PracticeWorldSelectorForm(new AppSettings { Language = LanguageNames.English });
            TestAssert.Equal("Save Selector", englishForm.Text);
            TestAssert.Equal(
                true,
                EnumerateControls(englishForm).OfType<Label>().Any(label => label.Text == "Press ESC to exit"));

            using var chineseForm = new PracticeWorldSelectorForm(new AppSettings { Language = "\u4E2D\u6587" });
            TestAssert.Equal("\u5B58\u6863\u9009\u62E9", chineseForm.Text);
            TestAssert.Equal(
                true,
                EnumerateControls(chineseForm).OfType<Label>().Any(label => label.Text == "\u6309ESC\u9000\u51FA"));
        });
    }

    private static void PracticeWorldSelectorFormScalesLayoutWithDisplayContext()
    {
        PracticeWorldSelectorLayoutMetrics baseline = PracticeWorldSelectorForm.CalculateLayoutMetrics(
            new Rectangle(0, 0, 1920, 1080),
            1f);
        PracticeWorldSelectorLayoutMetrics highDpi = PracticeWorldSelectorForm.CalculateLayoutMetrics(
            new Rectangle(0, 0, 2560, 1440),
            1.5f);
        PracticeWorldSelectorLayoutMetrics smallScreen = PracticeWorldSelectorForm.CalculateLayoutMetrics(
            new Rectangle(0, 0, 800, 600),
            1f);

        TestAssert.Equal(true, highDpi.ClientSize.Width > baseline.ClientSize.Width);
        TestAssert.Equal(true, highDpi.SlotHeight > baseline.SlotHeight);
        TestAssert.Equal(true, highDpi.TitleFontSize > baseline.TitleFontSize);
        TestAssert.Equal(true, smallScreen.ClientSize.Height <= 600);
        TestAssert.Equal(true, smallScreen.ClientSize.Width <= 800);
    }

    private static void HotkeyWarningDialogUsesPlainDialogContent()
    {
        RunSta(() =>
        {
            using var dialog = new HotkeyWarningDialog("Hotkey warning", "Ctrl + F10 registration failed.");
            Control[] controls = EnumerateControls(dialog).ToArray();

            TestAssert.Equal(false, controls.OfType<TextBox>().Any());
            TestAssert.Equal(
                true,
                controls.OfType<Label>().Any(label => label.Text.Contains("Ctrl + F10", StringComparison.Ordinal)));
        });
    }

    private static void SettingsMessageDialogUsesThemedDialogChrome()
    {
        RunSta(() =>
        {
            using var dialog = new SettingsMessageDialog(
                "Settings",
                "Advanced condition cannot be converted.",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning,
                static key => key);
            Control[] controls = EnumerateControls(dialog).ToArray();

            TestAssert.Equal(FormBorderStyle.None, dialog.FormBorderStyle);
            TestAssert.Equal(UiTheme.Window, dialog.BackColor);
            TestAssert.Equal(true, controls.OfType<Button>().Any(button => button.Text == "OK"));
            TestAssert.Equal(
                true,
                controls.OfType<Label>().Any(label => label.Text.Contains("Advanced condition", StringComparison.Ordinal)));
            TestAssert.Equal(false, controls.OfType<TextBox>().Any());
            TestAssert.Equal(
                false,
                controls.OfType<Panel>().Any(panel => panel.BackColor == Color.FromArgb(196, 143, 58)));

            using var oneLine = new SettingsMessageDialog(
                "Settings",
                "One line.",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning,
                static key => key);
            using var tenLines = new SettingsMessageDialog(
                "Settings",
                string.Join(Environment.NewLine, Enumerable.Range(1, 10).Select(index => $"Line {index}")),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning,
                static key => key);
            using var twelveLines = new SettingsMessageDialog(
                "Settings",
                string.Join(Environment.NewLine, Enumerable.Range(1, 12).Select(index => $"Line {index}")),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning,
                static key => key);

            TestAssert.Equal(true, tenLines.ClientSize.Height > oneLine.ClientSize.Height);
            TestAssert.Equal(tenLines.ClientSize.Height, twelveLines.ClientSize.Height);
        });
    }

    private static void SettingsFormTitleBarUsesIconWindowButtons()
    {
        RunSta(() =>
        {
            using var form = new SettingsForm(new AppSettings());
            form.Show();
            Application.DoEvents();
            Control[] controls = EnumerateControls(form).ToArray();

            Button minimize = controls.OfType<Button>().Single(button => button.AccessibleName == "Minimize");
            Button maximize = controls.OfType<Button>().Single(button => button.AccessibleName == "Maximize");
            Button close = controls.OfType<Button>().Single(button => button.AccessibleName == "Close");

            TestAssert.Equal(string.Empty, minimize.Text);
            TestAssert.Equal(string.Empty, maximize.Text);
            TestAssert.Equal(string.Empty, close.Text);
            TestAssert.Equal(true, form.MinimizeBox);
            TestAssert.Equal(true, form.MaximizeBox);

            maximize.PerformClick();
            TestAssert.Equal(FormWindowState.Maximized, form.WindowState);
            TestAssert.Equal("Restore", maximize.AccessibleName);

            maximize.PerformClick();
            TestAssert.Equal(FormWindowState.Normal, form.WindowState);
            TestAssert.Equal("Maximize", maximize.AccessibleName);

            minimize.PerformClick();
            TestAssert.Equal(FormWindowState.Minimized, form.WindowState);
        });
    }

    private static void SettingsUiFactoryKeepsTwoColumnEditorColumnFixedWidth()
    {
        RunSta(() =>
        {
            var factory = new SettingsUiFactory(static key => key);
            using TableLayoutPanel grid = factory.CreateTwoColumnGrid(280f);

            TestAssert.Equal(2, grid.ColumnStyles.Count);
            TestAssert.Equal(SizeType.Percent, grid.ColumnStyles[0].SizeType);
            TestAssert.Equal(100f, grid.ColumnStyles[0].Width);
            TestAssert.Equal(SizeType.Absolute, grid.ColumnStyles[1].SizeType);
            TestAssert.Equal(280f, grid.ColumnStyles[1].Width);
        });
    }

    private static void SettingsUiFactoryRowLabelsEllipsizeClippedText()
    {
        RunSta(() =>
        {
            var factory = new SettingsUiFactory(static key => key);
            using Label label = factory.CreateRowLabel("Moon Lord: cumulative not faster, segment not faster");

            TestAssert.Equal(true, label.AutoEllipsis);
            TestAssert.Equal(false, label.AutoSize);
        });
    }

    private static void SettingsUiFactoryHidesNativeMultilineScrollbars()
    {
        RunSta(() =>
        {
            var factory = new SettingsUiFactory(static key => key);
            using TextBox textBox = factory.CreateMultilineValueBox(120);

            TestAssert.Equal(true, textBox.Multiline);
            TestAssert.Equal(ScrollBars.None, textBox.ScrollBars);
        });
    }

    private static void ThemedScrollPanelRoutesListWheelToInnerListUntilBoundary()
    {
        RunSta(() =>
        {
            using var panel = new ThemedScrollPanel
            {
                Size = new Size(260, 180),
                Padding = new Padding(8)
            };
            using var content = new Panel
            {
                Size = new Size(220, 360)
            };
            using var listBox = new ListBox
            {
                Height = 80,
                IntegralHeight = false,
                ItemHeight = 16,
                Width = 180
            };
            for (int i = 0; i < 20; i++)
            {
                listBox.Items.Add(i.ToString());
            }

            content.Controls.Add(listBox);
            panel.Controls.Add(content);
            _ = panel.Handle;
            _ = listBox.Handle;

            int initialTopIndex = listBox.TopIndex;
            NativeMethods.SendMessage(listBox.Handle, 0x020A, MakeMouseWheelWParam(-120), IntPtr.Zero);
            TestAssert.Equal(true, listBox.TopIndex > initialTopIndex);

            int visibleItemCount = Math.Max(1, listBox.ClientSize.Height / Math.Max(1, listBox.ItemHeight));
            int maxTopIndex = Math.Max(0, listBox.Items.Count - visibleItemCount);
            listBox.TopIndex = maxTopIndex;
            int bottomTopIndex = listBox.TopIndex;
            NativeMethods.SendMessage(listBox.Handle, 0x020A, MakeMouseWheelWParam(-120), IntPtr.Zero);
            TestAssert.Equal(bottomTopIndex, listBox.TopIndex);
        });
    }

    private static IntPtr MakeMouseWheelWParam(int delta)
    {
        return new IntPtr(unchecked((int)((uint)(ushort)delta << 16)));
    }

    private static void FontFamilySelectorUsesThemedDropDownList()
    {
        RunSta(() =>
        {
            var factory = new SettingsUiFactory(static key => key);
            using ThemedDropDownList dropDown = factory.CreateDropDownList();
            using var selector = new FontFamilySelector();

            dropDown.Items.Add("Soft");
            dropDown.Items.Add("Sharp");
            dropDown.SelectedIndex = 1;
            Control dropDownControl = dropDown;
            Control selectorControl = selector;

            TestAssert.Equal("Sharp", dropDown.SelectedItem);
            TestAssert.Equal(false, dropDownControl is ComboBox);
            TestAssert.Equal(true, selectorControl is ThemedDropDownList);
            TestAssert.Equal(false, selectorControl is ComboBox);
            TestAssert.Equal(true, selector.Items.Count > 0);
        });
    }

    private static void SettingsFormUsesThemedDropDownLists()
    {
        RunSta(() =>
        {
            using var form = new SettingsForm(new AppSettings());
            foreach (SettingsPageHost.PageEntry page in form.PageHost.Pages)
            {
                form.PageHost.Select(page.Id);
            }

            Control[] controls = EnumerateControls(form).ToArray();
            TestAssert.Equal(false, controls.OfType<ComboBox>().Any());
            TestAssert.Equal(true, controls.OfType<ThemedDropDownList>().Any());
        });
    }

    private static void RunSta(Action action)
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

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control descendant in EnumerateControls(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed class FakeWatcher : ITerrariaWorldWatcher
    {
        private readonly TerrariaWatchSnapshot snapshot;
        private int pollCount;

        public FakeWatcher(TerrariaWatchSnapshot snapshot)
        {
            this.snapshot = snapshot;
        }

        public TerrariaWatchSnapshot Poll()
        {
            Interlocked.Increment(ref pollCount);
            return snapshot;
        }

        public TerrariaWatcherDiagnostics GetDiagnostics()
        {
            return TerrariaWatcherDiagnosticsDefaults.Empty;
        }

        public int PollCount => Volatile.Read(ref pollCount);

        public void Dispose()
        {
        }
    }

    private sealed class BlockingWatcher : ITerrariaWorldWatcher
    {
        private readonly TerrariaWatchSnapshot snapshot;
        private readonly ManualResetEventSlim gate = new(false);

        public BlockingWatcher(TerrariaWatchSnapshot snapshot)
        {
            this.snapshot = snapshot;
        }

        public int PollCount => pollCount;

        public int CompletedCount => completedCount;

        public TerrariaWatchSnapshot Poll()
        {
            Interlocked.Increment(ref pollCount);
            gate.Wait();
            Interlocked.Increment(ref completedCount);
            return snapshot;
        }

        public TerrariaWatcherDiagnostics GetDiagnostics()
        {
            return TerrariaWatcherDiagnosticsDefaults.Empty;
        }

        public void Release()
        {
            gate.Set();
        }

        public void Dispose()
        {
            gate.Dispose();
        }

        private int pollCount;
        private int completedCount;
    }

    private sealed class SequenceWatcher : ITerrariaWorldWatcher
    {
        private readonly TerrariaWatchSnapshot[] snapshots;
        private int index;

        public SequenceWatcher(TerrariaWatchSnapshot[] snapshots)
        {
            this.snapshots = snapshots;
        }

        public TerrariaWatchSnapshot Poll()
        {
            int currentIndex = Interlocked.Increment(ref index) - 1;
            if (currentIndex < snapshots.Length)
            {
                return snapshots[currentIndex];
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

    private sealed class FakePatchApplier : ITerrariaUiScalePatchApplier
    {
        private readonly TerrariaUiScalePatchResult result;

        public FakePatchApplier(TerrariaUiScalePatchResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public TerrariaUiScalePatchResult TryApply()
        {
            CallCount++;
            return result;
        }
    }

    private static IReadOnlyList<SplitDefinition> CreateSingleBossDefinitions()
    {
        return
        [
            new SplitDefinition(
                "split:skeletron",
                "Skeletron",
                SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                Array.Empty<string>(),
                Array.Empty<string>(),
                [SplitCatalog.Skeletron])
        ];
    }

    private static TerrariaGameFacts CreateSkeletronState(bool defeated)
    {
        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        builder.SetBoolean(
            SplitCatalog.BossFacts.First(boss => boss.TargetId == SplitCatalog.Skeletron).FactKey,
            defeated);
        return builder.Build();
    }
}
