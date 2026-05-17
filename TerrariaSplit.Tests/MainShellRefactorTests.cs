using System.Drawing;
using System.Windows.Forms;
using TerrariaSplit;

namespace TerrariaSplit.Tests;

internal static class MainShellRefactorTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("TerrariaMonitorCoordinator preserves watcher interval policy", TerrariaMonitorCoordinatorPreservesWatcherIntervalPolicy);
        yield return ("TerrariaMonitorCoordinator does not duplicate in-flight polls", TerrariaMonitorCoordinatorDoesNotDuplicateInflightPolls);
        yield return ("TerrariaMonitorCoordinator deduplicates repeated patch logs", TerrariaMonitorCoordinatorDeduplicatesRepeatedPatchLogs);
        yield return ("TerrariaMonitorCoordinator reset clears applied patch state", TerrariaMonitorCoordinatorResetClearsAppliedPatchState);
        yield return ("OverlayWindowController queues render once while pending", OverlayWindowControllerQueuesRenderOnceWhilePending);
        yield return ("OverlayWindowController click-through style preserves unrelated bits", OverlayWindowControllerPreservesUnrelatedStyleBits);
        yield return ("SettingsUiFactory keeps two-column editor column fixed width", SettingsUiFactoryKeepsTwoColumnEditorColumnFixedWidth);
    }

    private static void TerrariaMonitorCoordinatorPreservesWatcherIntervalPolicy()
    {
        TestAssert.Equal(
            TimeSpan.FromSeconds(1),
            TerrariaMonitorCoordinator.GetNextWatcherPollInterval(
                new TerrariaWatchSnapshot(false, null, false, null, TerrariaBossStates.Unknown, false, "waiting"),
                SplitTimerPhase.NotStarted));
        TestAssert.Equal(
            TimeSpan.FromMilliseconds(250),
            TerrariaMonitorCoordinator.GetNextWatcherPollInterval(
                new TerrariaWatchSnapshot(true, 123, false, true, TerrariaBossStates.Unknown, false, "not ready"),
                SplitTimerPhase.NotStarted));
        TestAssert.Equal(
            TimeSpan.FromMilliseconds(50),
            TerrariaMonitorCoordinator.GetNextWatcherPollInterval(TestSnapshots.Terraria(isGameMenu: false), SplitTimerPhase.Running));
        TestAssert.Equal(
            TimeSpan.FromMilliseconds(100),
            TerrariaMonitorCoordinator.GetNextWatcherPollInterval(TestSnapshots.Terraria(isGameMenu: true), SplitTimerPhase.Paused));
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

        public FakeWatcher(TerrariaWatchSnapshot snapshot)
        {
            this.snapshot = snapshot;
        }

        public TerrariaWatchSnapshot Poll()
        {
            PollCount++;
            return snapshot;
        }

        public int PollCount { get; private set; }

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
}
