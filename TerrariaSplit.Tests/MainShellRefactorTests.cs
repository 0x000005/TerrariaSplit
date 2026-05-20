using System.Drawing;
using System.Windows.Forms;
using TerrariaSplit;

namespace TerrariaSplit.Tests;

internal static class MainShellRefactorTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("TerrariaMonitorCoordinator preserves watcher interval policy", TerrariaMonitorCoordinatorPreservesWatcherIntervalPolicy);
        yield return ("TerrariaMonitorCoordinator polls watcher without UI ticks", TerrariaMonitorCoordinatorPollsWatcherWithoutUiTicks);
        yield return ("TerrariaMonitorCoordinator produces runtime state without UI ticks", TerrariaMonitorCoordinatorProducesRuntimeStateWithoutUiTicks);
        yield return ("TerrariaMonitorCoordinator clears queued hotkeys before processing", TerrariaMonitorCoordinatorClearsQueuedHotkeysBeforeProcessing);
        yield return ("TerrariaMonitorCoordinator does not duplicate in-flight polls", TerrariaMonitorCoordinatorDoesNotDuplicateInflightPolls);
        yield return ("TerrariaMonitorCoordinator deduplicates repeated patch logs", TerrariaMonitorCoordinatorDeduplicatesRepeatedPatchLogs);
        yield return ("TerrariaMonitorCoordinator reset clears applied patch state", TerrariaMonitorCoordinatorResetClearsAppliedPatchState);
        yield return ("OverlayWindowController queues render once while pending", OverlayWindowControllerQueuesRenderOnceWhilePending);
        yield return ("OverlayWindowController click-through style preserves unrelated bits", OverlayWindowControllerPreservesUnrelatedStyleBits);
        yield return ("OverlayWindowController strips non-client border style", OverlayWindowControllerStripsNonClientBorderStyle);
        yield return ("SettingsUiFactory keeps two-column editor column fixed width", SettingsUiFactoryKeepsTwoColumnEditorColumnFixedWidth);
        yield return ("SettingsUiFactory row labels ellipsize clipped text", SettingsUiFactoryRowLabelsEllipsizeClippedText);
    }

    private static void TerrariaMonitorCoordinatorPreservesWatcherIntervalPolicy()
    {
        TestAssert.Equal(
            TimeSpan.FromSeconds(1),
            TerrariaMonitorCoordinator.GetNextWatcherPollInterval(
                new TerrariaWatchSnapshot(false, null, false, null, TerrariaBossStates.Unknown, TerrariaWorldGenerationState.Unknown, false, "waiting"),
                SplitTimerPhase.NotStarted));
        TestAssert.Equal(
            TimeSpan.FromMilliseconds(250),
            TerrariaMonitorCoordinator.GetNextWatcherPollInterval(
                new TerrariaWatchSnapshot(true, 123, false, true, TerrariaBossStates.Unknown, TerrariaWorldGenerationState.Unknown, false, "not ready"),
                SplitTimerPhase.NotStarted));
        TestAssert.Equal(
            TimeSpan.FromMilliseconds(5),
            TerrariaMonitorCoordinator.GetNextWatcherPollInterval(TestSnapshots.Terraria(isGameMenu: false), SplitTimerPhase.Running));
        TestAssert.Equal(
            TimeSpan.FromMilliseconds(5),
            TerrariaMonitorCoordinator.GetNextWatcherPollInterval(TestSnapshots.Terraria(isGameMenu: true), SplitTimerPhase.Paused));
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
                bossStates: TerrariaBossStates.Unknown,
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
            if (notification.RuntimeTickResult.CompletedSplitIndex == 0)
            {
                completion = notification;
            }
        };

        coordinator.Tick(SplitTimerPhase.NotStarted, patchEnabled: false);

        TestAssert.Equal(true, SpinWait.SpinUntil(() => completion.HasValue, 1000));
        TestAssert.Equal(true, completion!.Value.RuntimeState.SplitTrackerState.Statuses[0].Time.HasValue);
    }

    private static void TerrariaMonitorCoordinatorClearsQueuedHotkeysBeforeProcessing()
    {
        var watcher = new BlockingWatcher(TestSnapshots.Terraria(isGameMenu: true));
        var patch = new FakePatchApplier(TerrariaUiScalePatchResult.NoProcess());
        var requestedActions = new List<MenuHotkeyActionKind>();
        using var coordinator = new TerrariaMonitorCoordinator(
            watcher,
            patch,
            action => action(),
            utcNowProvider: () => DateTime.UtcNow);
        coordinator.WatcherPollCompleted += notification =>
        {
            if (notification.RuntimeTickResult.RequestedMenuAction is MenuHotkeyActionKind action)
            {
                requestedActions.Add(action);
            }
        };

        coordinator.Tick(SplitTimerPhase.NotStarted, patchEnabled: false);
        SpinWait.SpinUntil(() => watcher.PollCount > 0, 1000);
        coordinator.Tick(
            SplitTimerPhase.NotStarted,
            patchEnabled: false,
            [new TimerHotkeyRequest(TimerHotkeyAction.CreateWorld, DateTime.UtcNow)]);
        _ = coordinator.ClearPendingHotkeys();
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
            TerrariaBossStates.Unknown,
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
            TerrariaBossStates.Unknown,
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

    private static IReadOnlyList<BossSplitDefinition> CreateSingleBossDefinitions()
    {
        return
        [
            new BossSplitDefinition(
                BossSplitDefinitions.Skeletron,
                "Skeletron",
                [BossFlag.Skeletron],
                Array.Empty<string>(),
                Array.Empty<string>(),
                [BossSplitDefinitions.Skeletron])
        ];
    }

    private static TerrariaBossStates CreateSkeletronState(bool defeated)
    {
        return new TerrariaBossStates(
            defeated,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }
}
