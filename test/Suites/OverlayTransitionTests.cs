using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using TerrariaSplit.UI.Rendering;

namespace TerrariaSplit.Tests;

internal static class OverlayTransitionTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("completion transition separates outgoing list boss and incoming list phases", TestSuite.Core, TransitionTimeline);
        yield return TestCase.Sync("completion transition preserves frozen presentation across replacement and cancellation", TestSuite.Core, PresentationLifecycle);
        yield return TestCase.Async("completion transition holds animation screen geometry during layout changes and dragging", TestSuite.Windows, GeometryJourney);
        yield return TestCase.Async("completion transition fades the old expanded rows before revealing collapsed rows", TestSuite.Windows, FrozenRowsJourney);
        yield return TestCase.Async("completion transition freezes attached visibility scrolling and final-run expansion", TestSuite.Windows, VisibleGroupsJourney);
        yield return TestCase.Async("overlay prepares its layout before allocating the layered frame surface", TestSuite.Windows, SurfacePreparationJourney);
    }

    private static void TransitionTimeline()
    {
        foreach (int seconds in new[] { 2, 5, 20 })
        {
            TimeSpan duration = TimeSpan.FromSeconds(seconds);
            Check.Equal(1f, SplitCompletionAnimationRenderer.GetListOpacity(TimeSpan.Zero, duration));
            Check.Equal(0f, SplitCompletionAnimationRenderer.GetAnimationOpacity(TimeSpan.Zero, duration));
            Check.Equal(0f, SplitCompletionAnimationRenderer.GetListOpacity(TimeSpan.FromMilliseconds(180), duration));
            Check.Equal(0f, SplitCompletionAnimationRenderer.GetAnimationOpacity(TimeSpan.FromMilliseconds(180), duration));
            Check.Equal(1f, SplitCompletionAnimationRenderer.GetAnimationOpacity(TimeSpan.FromMilliseconds(450), duration));
            Check.Equal(0f, SplitCompletionAnimationRenderer.GetAnimationOpacity(duration - TimeSpan.FromMilliseconds(180), duration));
            Check.Equal(0f, SplitCompletionAnimationRenderer.GetListOpacity(duration - TimeSpan.FromMilliseconds(180), duration));
            Check.Equal(1f, SplitCompletionAnimationRenderer.GetListOpacity(duration, duration));
            for (int milliseconds = 0; milliseconds <= seconds * 1000; milliseconds += 5)
            {
                TimeSpan elapsed = TimeSpan.FromMilliseconds(milliseconds);
                float list = SplitCompletionAnimationRenderer.GetListOpacity(elapsed, duration);
                float boss = SplitCompletionAnimationRenderer.GetAnimationOpacity(elapsed, duration);
                Check.True(list is >= 0f and <= 1f && boss is >= 0f and <= 1f);
                Check.True(list * boss == 0f);
            }
        }
    }

    private static void PresentationLifecycle()
    {
        AppSettings settings = CreateSettings(3);
        settings.Overlay.SplitCompletionAnimationDurationSeconds = 5;
        SplitStatusSnapshot[] oldStatuses = SplitCatalog.Build(settings).Select(SplitStatusSnapshot.FromDefinition).ToArray();
        SplitStatusSnapshot[] completed = Complete(oldStatuses);
        OverlayCompositeLayout layout = CreateBounds(settings, 7).CurrentLayout;
        OverlayRenderContext oldContext = Context(settings, oldStatuses, 0, layout, DateTime.UnixEpoch);
        var geometry = new SplitCompletionAnimationGeometry(new Rectangle(20, 50, 600, 400), 300);
        var controller = new OverlayAnimationController();
        controller.RecordListPresentation(oldContext, geometry, layout);
        controller.StartSplitCompletionAnimation(settings, completed, 0, DateTime.UnixEpoch);
        Check.True(controller.OutgoingSplitList is not null);
        Check.True(controller.OutgoingSplitList!.Statuses[0].Time is null);
        Check.Equal(0, controller.OutgoingSplitList.CurrentSplitIndex);
        int capturedScale = controller.OutgoingSplitList.Settings.Overlay.Columns.ScalePercent;
        settings.Overlay.Columns.ScalePercent = capturedScale + 10;
        Check.Equal(capturedScale, controller.OutgoingSplitList.Settings.Overlay.Columns.ScalePercent);
        settings.Overlay.Columns.ScalePercent = capturedScale;
        Check.False(controller.CanApplyCompletionLayout(settings, DateTime.UnixEpoch.AddMilliseconds(179)));
        Check.True(controller.CanApplyCompletionLayout(settings, DateTime.UnixEpoch.AddMilliseconds(250)));
        controller.MarkCompletionLayoutApplied();
        Check.False(controller.CanApplyCompletionLayout(settings, DateTime.UnixEpoch.AddSeconds(1)));
        Check.True(controller.CanApplyCompletionLayout(settings, DateTime.UnixEpoch.AddMilliseconds(4820)));

        OverlayRenderContext frozen = controller.OutgoingSplitList;
        controller.UpdateAfterRender(new OverlayRenderResult(true, ListOpacity: 0.5f, OutgoingListRendered: true));
        controller.StartSplitCompletionAnimation(settings, completed, 1, DateTime.UnixEpoch.AddMilliseconds(100));
        Check.Equal(0.5f, controller.OutgoingListOpacity);
        Check.True(ReferenceEquals(frozen, controller.OutgoingSplitList));
        controller.UpdateAfterRender(new OverlayRenderResult(true, ListOpacity: 0f));
        controller.StartSplitCompletionAnimation(settings, completed, 2, DateTime.UnixEpoch.AddSeconds(1));
        Check.Equal(0f, controller.OutgoingListOpacity);
        controller.StartSplitCompletionAnimation(settings, completed, 0, DateTime.UnixEpoch.AddSeconds(20));
        Check.Equal(0f, controller.OutgoingListOpacity);
        Check.True(controller.CanApplyCompletionLayout(settings, DateTime.UnixEpoch.AddSeconds(30)));
        controller.UpdateAfterRender(new OverlayRenderResult(false));
        Check.True(controller.SplitCompletionAnimation is null && controller.OutgoingSplitList is null);
        Check.True(controller.ResolveGeometry(layout) is null);
        Check.True(controller.GetRequiredStatusBounds(settings, DateTime.UnixEpoch.AddSeconds(30)) is null);

        controller.Clear();
        controller.StartSplitCompletionAnimation(settings, oldStatuses, 0, DateTime.UnixEpoch);
        Check.True(controller.SplitCompletionAnimation is null);
        Check.True(controller.CanApplyCompletionLayout(settings, DateTime.UnixEpoch));
    }

    private static Task GeometryJourney(CancellationToken token) => StaTestHost.RunAsync(() =>
    {
        foreach (int scale in new[] { 75, 100, 200 })
        {
            AppSettings settings = CreateSettings(20);
            settings.Overlay.Columns.ScalePercent = scale;
            settings.Overlay.SplitCompletionAnimationDurationSeconds = 5;
            SplitStatusSnapshot[] statuses = SplitCatalog.Build(settings).Select(SplitStatusSnapshot.FromDefinition).ToArray();
            OverlayBoundsController bounds = CreateBounds(settings, 20);
            OverlayCompositeLayout original = bounds.CurrentLayout;
            OverlayRenderContext context = Context(settings, statuses, 0, original, DateTime.UnixEpoch);
            using var bitmap = new Bitmap(64, 64);
            using Graphics graphics = Graphics.FromImage(bitmap);
            using var resources = new OverlayRenderResources();
            SplitCompletionAnimationGeometry natural = SplitCompletionAnimationRenderer.CaptureGeometry(graphics, context, resources);
            var animations = new OverlayAnimationController();
            animations.RecordListPresentation(context, natural, original);
            animations.StartSplitCompletionAnimation(settings, Complete(statuses), 0, DateTime.UnixEpoch);
            Rectangle expectedScreen = natural.Bounds;
            expectedScreen.Offset(original.StatusScreenBounds.Location);
            float expectedCenter = natural.CenterX + original.StatusScreenBounds.Left;
            Point timer = original.TimerScreenBounds.Location;
            Rectangle? required = animations.GetRequiredStatusBounds(settings, DateTime.UnixEpoch.AddMilliseconds(250));
            Check.True(required is not null);
            foreach (int rows in new[] { 7, 32, 9 })
            {
                bounds.UpdateContext(settings, rows, rows, required);
                SplitCompletionAnimationGeometry actual = animations.ResolveGeometry(bounds.CurrentLayout)!.Value;
                Rectangle actualScreen = actual.Bounds;
                actualScreen.Offset(bounds.CurrentLayout.StatusScreenBounds.Location);
                Check.Equal(expectedScreen, actualScreen);
                Check.Equal(expectedCenter, actual.CenterX + bounds.CurrentLayout.StatusScreenBounds.Left);
                Check.Equal(timer, bounds.CurrentLayout.TimerScreenBounds.Location);
                Check.True(bounds.CurrentLayout.StatusScreenBounds.Contains(actualScreen));
            }

            var move = new Point(37, -21);
            bounds.MoveBy(move);
            Rectangle moved = animations.ResolveGeometry(bounds.CurrentLayout)!.Value.Bounds;
            moved.Offset(bounds.CurrentLayout.StatusScreenBounds.Location);
            expectedScreen.Offset(move);
            Check.Equal(expectedScreen, moved);
            Check.True(animations.GetRequiredStatusBounds(settings, DateTime.UnixEpoch.AddMilliseconds(4820)) is null);
            bounds.UpdateContext(settings, 9, 9);
            Check.Equal(new Point(timer.X + move.X, timer.Y + move.Y), bounds.CurrentLayout.TimerScreenBounds.Location);
        }
    }, token);

    private static Task FrozenRowsJourney(CancellationToken token) => StaTestHost.RunAsync(() =>
    {
        AppSettings settings = CreateSettings(2);
        settings.Route.ExpandSplitDetails = true;
        settings.Route.CollapseSplitDetailsOnCompletion = true;
        settings.Route.ShowAllMultiConditionMainGroupsAfterFinalGroup = false;
        settings.Route.SplitRoute[0].Condition = SplitCondition.All(Enumerable.Range(0, 12)
            .Select(index => SplitCondition.Fact("event:expanded-" + index)).ToList());
        SplitStatusSnapshot[] before = SplitCatalog.Build(settings).Select(SplitStatusSnapshot.FromDefinition).ToArray();
        string[] completedFacts = before[0].Definition.Condition.GetFactConditions().Take(11).Select(fact => fact.FactKey).ToArray();
        before[0] = before[0] with
        {
            CompletedFactKeys = completedFacts,
            FactCompletionTimes = completedFacts.Select((fact, index) => (fact, time: TimeSpan.FromSeconds(index + 1)))
                .ToDictionary(item => item.fact, item => item.time)
        };
        SplitStatusSnapshot[] after = Complete(before);
        after[1] = before[1];
        int oldRows = SplitDisplayRows.GetRequiredRowCount(settings, before, 0);
        int newRows = SplitDisplayRows.GetRequiredRowCount(settings, after, 1);
        Check.True(oldRows > newRows);
        OverlayBoundsController bounds = CreateBounds(settings, Math.Max(7, oldRows));
        OverlayCompositeLayout layout = bounds.CurrentLayout;
        OverlayRenderContext oldContext = Context(settings, before, 0, layout, DateTime.UnixEpoch);
        var animations = new OverlayAnimationController();
        using var oldBitmap = new Bitmap(layout.StatusScreenBounds.Width, layout.StatusScreenBounds.Height, PixelFormat.Format32bppPArgb);
        using var newBitmap = new Bitmap(oldBitmap.Width, oldBitmap.Height, PixelFormat.Format32bppPArgb);
        using var resources = new OverlayRenderResources();
        using (Graphics graphics = Graphics.FromImage(oldBitmap))
        {
            Configure(graphics);
            OverlayRenderer.RenderStatus(graphics, oldContext, resources);
            animations.RecordListPresentation(oldContext,
                SplitCompletionAnimationRenderer.CaptureGeometry(graphics, oldContext, resources), layout);
        }

        animations.StartSplitCompletionAnimation(settings, after, 0, DateTime.UnixEpoch);
        OverlayRenderContext current = Context(settings, after, 1, layout, DateTime.UnixEpoch) with
        {
            SplitCompletionAnimation = animations.SplitCompletionAnimation,
            OutgoingSplitList = animations.OutgoingSplitList,
            OutgoingListOpacity = animations.OutgoingListOpacity,
            CompletionGeometry = animations.ResolveGeometry(layout)
        };
        using (Graphics graphics = Graphics.FromImage(newBitmap))
        {
            Configure(graphics);
            OverlayRenderResult result = OverlayRenderer.RenderStatus(graphics, current, resources);
            Check.True(result.OutgoingListRendered);
            Check.Equal(1f, result.ListOpacity);
        }

        Check.True(Pixels(oldBitmap).AsSpan().SequenceEqual(Pixels(newBitmap)));
        TimeSpan duration = SplitCompletionAnimationRenderer.GetAnimationDuration(settings);
        current = current with { NowUtc = DateTime.UnixEpoch + duration - TimeSpan.FromMilliseconds(90), TimerPhase = SplitTimerPhase.Paused };
        using (Graphics graphics = Graphics.FromImage(newBitmap))
        {
            graphics.Clear(Color.Transparent);
            Configure(graphics);
            OverlayRenderResult result = OverlayRenderer.RenderStatus(graphics, current, resources);
            Check.True(result.SplitCompletionAnimationActive && !result.OutgoingListRendered);
            Check.True(result.ListOpacity > 0f && result.ListOpacity < 1f);
            animations.UpdateAfterRender(result);
            Check.True(animations.SplitCompletionAnimation is not null);
        }

        current = current with { NowUtc = DateTime.UnixEpoch.AddSeconds(10), TimerPhase = SplitTimerPhase.Paused };
        using (Graphics graphics = Graphics.FromImage(newBitmap))
        {
            graphics.Clear(Color.Transparent);
            Configure(graphics);
            OverlayRenderResult result = OverlayRenderer.RenderStatus(graphics, current, resources);
            Check.False(result.SplitCompletionAnimationActive || result.OutgoingListRendered);
            Check.Equal(1f, result.ListOpacity);
            animations.UpdateAfterRender(result);
            Check.True(animations.SplitCompletionAnimation is null);
        }
        Check.False(Pixels(oldBitmap).AsSpan().SequenceEqual(Pixels(newBitmap)));
    }, token);

    private static Task VisibleGroupsJourney(CancellationToken token) => StaTestHost.RunAsync(() =>
    {
        void CheckFrozen(AppSettings settings, SplitStatusSnapshot[] before, SplitStatusSnapshot[] after,
            int oldCurrent, int newCurrent, int completedIndex, bool expandFinal)
        {
            OverlayCompositeLayout layout = CreateBounds(settings, 20).CurrentLayout;
            OverlayRenderContext context = Context(settings, before, oldCurrent, layout, DateTime.UnixEpoch);
            IReadOnlyList<SplitDisplayRow> oldRows = SplitDisplayRows.Build(settings, before, oldCurrent, context.VisibleStatusRowCount);
            IReadOnlyList<SplitDisplayRow> newRows = SplitDisplayRows.Build(settings, after, newCurrent,
                Math.Max(7, SplitDisplayRows.GetRequiredRowCount(settings, after, newCurrent, expandFinal)), expandFinal);
            Check.False(oldRows.SequenceEqual(newRows));
            var controller = new OverlayAnimationController();
            controller.RecordListPresentation(context, new SplitCompletionAnimationGeometry(new Rectangle(10, 10, 700, 450), 350), layout);
            controller.StartSplitCompletionAnimation(settings, after, completedIndex, DateTime.UnixEpoch);
            OverlayRenderContext frozen = controller.OutgoingSplitList!;
            Check.Sequence(oldRows, SplitDisplayRows.Build(frozen.Settings, frozen.Statuses, frozen.CurrentSplitIndex,
                frozen.VisibleStatusRowCount, frozen.IgnoreVisibleGroupLimit));
        }

        AppSettings attached = CreateSettings(8);
        attached.Route.AutoHideAttachedGroups = true;
        foreach (SplitRouteEntry entry in attached.Route.SplitRoute.Skip(1).Take(6)) entry.IsAttached = true;
        SplitStatusSnapshot[] locked = SplitCatalog.Build(attached).Select(SplitStatusSnapshot.FromDefinition).ToArray();
        SplitStatusSnapshot[] unlocked = Complete(locked);
        for (int i = 1; i < unlocked.Length; i++) unlocked[i] = locked[i];
        CheckFrozen(attached, locked, unlocked, 0, 1, 0, expandFinal: false);

        AppSettings scrolling = CreateSettings(20);
        scrolling.Route.EnableVisibleGroupCountLimit = true;
        scrolling.Route.VisibleGroupCountLimit = 5;
        SplitStatusSnapshot[] definitions = SplitCatalog.Build(scrolling).Select(SplitStatusSnapshot.FromDefinition).ToArray();
        SplitStatusSnapshot[] progress = Complete(definitions);
        for (int i = 10; i < progress.Length; i++) progress[i] = definitions[i];
        SplitStatusSnapshot[] advanced = progress.ToArray();
        advanced[10] = Complete(definitions)[10];
        CheckFrozen(scrolling, progress, advanced, 10, 11, 10, expandFinal: false);
        SplitStatusSnapshot[] complete = Complete(definitions);
        SplitStatusSnapshot[] finalPending = complete.ToArray();
        finalPending[^1] = definitions[^1];
        CheckFrozen(scrolling, finalPending, complete, 19, 20, 19, expandFinal: true);
    }, token);

    private static Task SurfacePreparationJourney(CancellationToken token) => StaTestHost.RunAsync(() =>
    {
        using var form = new System.Windows.Forms.Form { ClientSize = new Size(100, 100), ShowInTaskbar = false };
        _ = form.Handle;
        int prepared = 0;
        Size published = Size.Empty;
        using var controller = new OverlayWindowController(form, graphics =>
        {
            Check.Equal(240f, graphics.VisibleClipBounds.Width);
            Check.Equal(180f, graphics.VisibleClipBounds.Height);
            return true;
        }, updateLayeredBitmap: bitmap => { published = bitmap.Size; return true; });
        controller.PreparingFrame += () => { prepared++; form.ClientSize = new Size(240, 180); };
        Check.True(controller.RenderNow());
        Check.Equal(1, prepared);
        Check.Equal(new Size(240, 180), published);
        Check.True(controller.RenderNow(static _ => true));
        Check.Equal(1, prepared);
    }, token);

    private static AppSettings CreateSettings(int count)
    {
        AppSettings settings = AppSettingsDefaults.Create();
        settings.Route.EnableVisibleGroupCountLimit = false;
        settings.Overlay.ShowCurrentSplitHighlight = false;
        settings.Route.SplitRoute = Enumerable.Range(0, count).Select(index => new SplitRouteEntry
        {
            Id = "transition-" + index, DisplayName = "Stage " + index, Enabled = true,
            Condition = SplitCondition.Fact("event:transition-" + index)
        }).ToList();
        return settings;
    }

    private static SplitStatusSnapshot[] Complete(IReadOnlyList<SplitStatusSnapshot> statuses) => statuses.Select((status, index) =>
        status with
        {
            Time = TimeSpan.FromSeconds((index + 1) * 10),
            CompletedFactKeys = status.Definition.Condition.ToFlatGroup().GetFactConditions().Select(fact => fact.FactKey).ToArray()
        }).ToArray();

    private static OverlayBoundsController CreateBounds(AppSettings settings, int rows)
    {
        var bounds = new OverlayBoundsController(9, settings, rows, rows);
        bounds.Initialize(new Rectangle(100, 200, 900, 1400));
        return bounds;
    }

    private static OverlayRenderContext Context(AppSettings settings, IReadOnlyList<SplitStatusSnapshot> statuses,
        int current, OverlayCompositeLayout layout, DateTime now) => new(settings, UiPalette.From(settings.Overlay.Colors),
        default, statuses, current, SplitTimerPhase.Running, TimeSpan.FromSeconds(5),
        new SplitLayout(layout.ToStatusLocal(layout.Layout.FirstRowRect), layout.ToStatusLocal(layout.Layout.TimerRect), layout.Layout.RowGap),
        Math.Max(7, SplitDisplayRows.GetRequiredRowCount(settings, statuses, current)), false, null,
        new Dictionary<int, SegmentBestDeltaHighlight>(), now);

    private static void Configure(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
    }

    private static byte[] Pixels(Bitmap bitmap)
    {
        BitmapData data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            byte[] bytes = new byte[data.Stride * data.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
