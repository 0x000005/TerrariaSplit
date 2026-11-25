using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Windows.Forms;
using TerrariaSplit.MemoryBridge.Protocol;
using TerrariaSplit.Terraria.Automation;
using TerrariaSplit.Terraria.Memory;

namespace TerrariaSplit.Tests;

internal static class ModernMenuTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Async("1458 menus acknowledge down and release and wait for target readiness", TestSuite.Core, ClickJourney);
        yield return TestCase.Async("1458 menus acknowledge moved UI objects and completed mouse-down callbacks without repeating clicks", TestSuite.Core, MovedClickJourney);
        yield return TestCase.Async("1458 random clicks cannot be acknowledged by a previously nonempty seed", TestSuite.Core, RandomClickJourney);
        yield return TestCase.Async("1458 settings skip satisfied targets and retry only readable unsatisfied values", TestSuite.Core, EnsureJourney);
        yield return TestCase.Sync("1458 submitted seeds validate parsed secrets and settings instead of raw bootstrap text", TestSuite.Core, SeedSubmissionJourney);
        yield return TestCase.Async("1458 menus confirm option values and keyboard validity before advancing", TestSuite.Core, OptionJourney);
        yield return TestCase.Async("1458 menus always release input on cancellation and focus loss", TestSuite.Core, InterruptedClick);
        yield return TestCase.Async("1458 button failures allow three total attempts with 50ms retries without repeating completed actions", TestSuite.Core, RetryJourney);
        yield return TestCase.Async("1458 random seed retries require a changed seed within three total attempts", TestSuite.Core, RandomRetryJourney);
        yield return TestCase.Async("1458 Unicode input batches Chinese and surrogate pairs and acknowledges actual text", TestSuite.Core, TextJourney);
        yield return TestCase.Async("1458 Unicode input stops on cancellation focus loss and wrong text without retrying", TestSuite.Core, TextFailureJourney);
        yield return TestCase.Async("1458 menu waits diagnose unknown states instead of advancing", TestSuite.Core, UnknownJourney);
        yield return TestCase.Sync("1458 menu reader validates layouts and reads state without injection", TestSuite.Core, MemoryJourney);
        yield return TestCase.Sync("state-driven menu version boundary keeps 1449 delays", TestSuite.Core, VersionBoundary);
        yield return TestCase.Async("1458 shared runtime cache merges initialization and refreshes safely across process restarts", TestSuite.Core, LayoutCacheJourney);
        yield return TestCase.Async("1458 shared runtime cache retries failed probes without poisoning later runs", TestSuite.Core, FailedLayoutJourney);
        yield return TestCase.Async("1458 cold startup waits without caching partial layouts and refreshes invalid metadata", TestSuite.Core, ColdStartupJourney);
        yield return TestCase.Sync("1458 monitoring resolvers reuse the same runtime and menu layout", TestSuite.Core, SharedResolverJourney);
        yield return TestCase.Async("1458 seed filtering reads live seeds through the existing menu observer", TestSuite.Core, SharedSeedJourney);
    }

    private static TerrariaMenuSnapshot Snapshot(TerrariaMenuPage page) => new(true, page, new IntPtr(200), false, 0,
        false, false, new IntPtr(300), IntPtr.Zero, 4200, 0, 1, 0, 0, "", "1", true, null,
        new Dictionary<string, int>(), new int[8], "test " + page, new PointF(100, 100));

    private sealed class Observer : ITerrariaMenuObserver
    {
        public TerrariaMenuSnapshot State = Snapshot(TerrariaMenuPage.World);
        public int Initializations;
        public Point ButtonPoint = new(100, 100);
        public bool ButtonAvailable = true;
        public int ButtonReads;
        public TerrariaMenuSnapshot Read() => State;
        public bool TryReadButton(TerrariaMenuSnapshot menu, TerrariaMenuTarget target, out TerrariaMenuButtonPosition position, out string detail)
        {
            ButtonReads++;
            detail = "fake game layout " + target;
            position = new(ButtonPoint, detail);
            return ButtonAvailable;
        }
        public Task InitializeAsync(CancellationToken cancellationToken) { Initializations++; return Task.CompletedTask; }
        public void Dispose() { }
    }

    private sealed class Input(Observer observer) : IModernMenuInput
    {
        public bool IsForeground { get; set; } = true;
        public int Downs, Ups;
        public Action<bool>? OnMouse;
        public Func<bool, bool>? MouseResult;
        public Action<Keys, bool>? OnKey;
        public Action<string>? OnText;
        public readonly List<string> TextBatches = [];
        public readonly HashSet<Keys> HeldKeys = [];
        public bool Position(Point point, out string detail)
        {
            observer.State = observer.State with { MousePosition = new PointF((int)(point.X / observer.State.UiScale), (int)(point.Y / observer.State.UiScale)) };
            detail = ""; return true;
        }
        public bool Mouse(bool down)
        { if (down) Downs++; else Ups++; OnMouse?.Invoke(down); return MouseResult?.Invoke(down) ?? true; }
        public void Key(Keys key, bool down)
        { if (down) HeldKeys.Add(key); else HeldKeys.Remove(key); OnKey?.Invoke(key, down); }
        public void UnicodeText(string text) { TextBatches.Add(text); OnText?.Invoke(text); }
    }

    private static async Task ClickJourney(CancellationToken token)
    {
        foreach (bool actsOnRelease in new[] { false, true })
        foreach (float scale in new[] { 1f, 1.2f, 2f / 3f })
        {
            var observer = new Observer { State = Snapshot(TerrariaMenuPage.World) with { UiScale = scale } };
            var input = new Input(observer);
            bool released = false; int readyTicks = 0;
            input.OnMouse = down =>
            {
                if (down)
                    observer.State = observer.State with { MouseDown = true, UiMouseDown = true, LastDown = observer.State.Hover };
                else
                {
                    released = true;
                    observer.State = Snapshot(TerrariaMenuPage.Advanced) with { LayoutDirty = true, ClickDisabledMs = 200 };
                }
                if (down && !actsOnRelease)
                    observer.State = Snapshot(TerrariaMenuPage.Advanced) with { MouseDown = true, UiMouseDown = true, ClickDisabledMs = 200 };
            };
            using var menu = new ModernMenuAutomation(observer, input, (_, _) =>
            {
                if (released && ++readyTicks == 3) observer.State = Snapshot(TerrariaMenuPage.Advanced);
                return Task.CompletedTask;
            });
            await menu.ClickAsync("advanced", new(TerrariaMenuButton.AdvancedMenu),
                MenuExpectation.Page(TerrariaMenuPage.World, TerrariaMenuPage.Advanced), token);
            Check.Equal(1, input.Downs); Check.Equal(1, input.Ups); Check.Equal(3, readyTicks);
        }
    }

    private static async Task InterruptedClick(CancellationToken token)
    {
        foreach (bool cancel in new[] { false, true })
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var observer = new Observer(); var input = new Input(observer);
            input.OnMouse = down => { if (down) { if (cancel) cts.Cancel(); else input.IsForeground = false; } };
            using var menu = new ModernMenuAutomation(observer, input, (_, ct) => Task.Delay(1, ct));
            try
            {
                await menu.ClickAsync("interrupted", new(TerrariaMenuButton.AdvancedMenu),
                    MenuExpectation.Page(TerrariaMenuPage.World, TerrariaMenuPage.Advanced), cts.Token);
                throw new InvalidOperationException("Expected interrupted click.");
            }
            catch (OperationCanceledException) when (cancel) { }
            catch (InvalidOperationException ex) when (!cancel && ex.Message.Contains("foreground", StringComparison.Ordinal)) { }
            Check.Equal(1, input.Downs); Check.Equal(1, input.Ups);
        }
    }

    private static async Task MovedClickJourney(CancellationToken token)
    {
        foreach (string mode in new[] { "moved", "completed", "torn", "wrong-target", "wrong-position" })
        {
            var observer = new Observer { State = Snapshot(TerrariaMenuPage.Character) };
            var input = new Input(observer);
            int samples = 0;
            input.OnMouse = down =>
            {
                if (down)
                    observer.State = observer.State with
                    {
                        State = new IntPtr(1000), Hover = new IntPtr(1100), LastDown = new IntPtr(1100),
                        MouseDown = true, UiMouseDown = true
                    };
                else
                    observer.State = observer.State with { MouseDown = false, UiMouseDown = false,
                        LastDown = IntPtr.Zero, CharacterCategory = 1 };
                if (!down) return;
                if (mode == "completed")
                    observer.State = observer.State with { CharacterCategory = 1, MouseDown = false,
                        UiMouseDown = false, Hover = IntPtr.Zero, LastDown = IntPtr.Zero };
                if (mode == "torn" || mode == "wrong-target")
                    observer.State = observer.State with { Hover = new IntPtr(1200) };
                if (mode == "wrong-position")
                    observer.State = observer.State with { MousePosition = new PointF(200, 200) };
            };
            using var menu = new ModernMenuAutomation(observer, input, (_, _) =>
            {
                if (++samples > 2) throw new InvalidOperationException("Click acknowledgment did not follow the current UI state.");
                Check.Equal(0, input.Ups);
                observer.State = observer.State with { Hover = observer.State.LastDown, MousePosition = new PointF(100, 100) };
                return Task.CompletedTask;
            });
            await menu.ClickAsync(mode, new(TerrariaMenuButton.AdvancedMenu),
                new MenuExpectation(TerrariaMenuPage.Character, TerrariaMenuPage.Character, s => s.CharacterCategory == 1), token);
            Check.Equal(mode is "torn" or "wrong-target" or "wrong-position" ? 1 : 0, samples);
            Check.Equal(1, input.Downs); Check.Equal(1, input.Ups);
            Check.Equal(1, observer.State.CharacterCategory);
        }
    }

    private static async Task RetryJourney(CancellationToken token)
    {
        foreach (string mode in new[] { "third-success", "persistent", "already-completed", "cancel" })
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var observer = new Observer();
            var input = new Input(observer);
            int retries = 0;
            input.MouseResult = down => !down || mode == "third-success" && input.Downs == 3;
            input.OnMouse = down =>
            {
                if (down && (mode == "already-completed" || mode == "third-success" && input.Downs == 3))
                    observer.State = Snapshot(TerrariaMenuPage.Advanced);
            };
            using var menu = new ModernMenuAutomation(observer, input, (duration, ct) =>
            {
                if (duration == TimeSpan.FromMilliseconds(50))
                {
                    retries++;
                    if (mode == "cancel") cts.Cancel();
                }
                ct.ThrowIfCancellationRequested();
                return Task.Delay(1, ct);
            });
            Task click = menu.ClickAsync(mode, new(TerrariaMenuButton.AdvancedMenu),
                MenuExpectation.Page(TerrariaMenuPage.World, TerrariaMenuPage.Advanced), cts.Token);
            if (mode == "persistent") await Check.ThrowsAsync<InvalidOperationException>(() => click);
            else if (mode == "cancel") await Check.ThrowsAsync<OperationCanceledException>(() => click);
            else await click;
            Check.Equal(mode is "third-success" or "persistent" ? 3 : 1, input.Downs);
            Check.Equal(input.Downs, input.Ups);
            Check.Equal(mode is "third-success" or "persistent" ? 2 : 1, retries);
        }
    }

    private static async Task RandomRetryJourney(CancellationToken token)
    {
        foreach (bool succeeds in new[] { false, true })
        {
            var observer = new Observer { State = Snapshot(TerrariaMenuPage.Advanced) with { Seed = "old" } };
            var input = new Input(observer);
            int retries = 0;
            input.OnMouse = down => observer.State = observer.State with
            {
                MouseDown = down, UiMouseDown = down, LastDown = down ? observer.State.Hover : IntPtr.Zero,
                Seed = succeeds && input.Downs == 3 ? "new" : "old"
            };
            using var menu = new ModernMenuAutomation(observer, input, (duration, ct) =>
            {
                if (duration == TimeSpan.FromMilliseconds(50)) retries++;
                return Task.Delay(1, ct);
            });
            Task click = menu.ClickAsync("random seed", new(TerrariaMenuButton.AdvancedMenu),
                MenuExpectation.RandomSeed(), token);
            if (succeeds) await click;
            else await Check.ThrowsAsync<TimeoutException>(() => click);
            Check.Equal(3, input.Downs); Check.Equal(3, input.Ups); Check.Equal(2, retries);
            Check.Equal(succeeds ? "new" : "old", observer.State.Seed);
        }
    }

    private static async Task OptionJourney(CancellationToken token)
    {
        foreach (bool keyboard in new[] { false, true })
        {
            var observer = new Observer
            {
                State = Snapshot(keyboard ? TerrariaMenuPage.Keyboard : TerrariaMenuPage.World) with { CanSubmit = false }
            };
            var input = new Input(observer);
            bool released = false;
            int readyTicks = 0, completionTicks = 0;
            input.OnMouse = down =>
            {
                observer.State = observer.State with { MouseDown = down, UiMouseDown = down,
                    LastDown = down ? observer.State.Hover : IntPtr.Zero };
                if (!down) released = true;
            };
            using var menu = new ModernMenuAutomation(observer, input, (_, _) =>
            {
                if (!released)
                {
                    // The submit button may exist before Terraria has validated the text.
                    Check.Equal(0, input.Downs);
                    if (++readyTicks == 2) observer.State = observer.State with { CanSubmit = true };
                }
                else if (++completionTicks == 3)
                    observer.State = Snapshot(TerrariaMenuPage.World) with { WorldWidth = 6400 };
                return Task.CompletedTask;
            });
            var expected = keyboard
                ? MenuExpectation.Page(TerrariaMenuPage.Keyboard, TerrariaMenuPage.World)
                : new MenuExpectation(TerrariaMenuPage.World, TerrariaMenuPage.World, s => s.WorldWidth == 6400);
            await menu.ClickAsync("option", new(TerrariaMenuButton.AdvancedMenu), expected, token);
            Check.Equal(keyboard ? 2 : 0, readyTicks);
            Check.Equal(3, completionTicks); Check.Equal(1, input.Downs); Check.Equal(1, input.Ups);
        }
    }

    private static async Task TextJourney(CancellationToken token)
    {
        foreach (string text in new[] { "12345", "中文玩家", new string('1', 63) + "\U0001F600" + new string('2', 120),
            new string('种', 608), new string('子', 640) })
        {
            var observer = new Observer { State = Snapshot(TerrariaMenuPage.Keyboard) with { Text = "old" } };
            int[] held = new int[8]; held[(int)Keys.LControlKey / 32] = 1 << ((int)Keys.LControlKey % 32);
            observer.State = observer.State with { KeyboardWords = held };
            var input = new Input(observer);
            input.OnKey = (key, down) =>
            {
                Check.False(key == Keys.V);
                int[] words = (int[])observer.State.KeyboardWords.Clone();
                if (down) words[(int)key / 32] |= 1 << ((int)key % 32); else words[(int)key / 32] &= ~(1 << ((int)key % 32));
                observer.State = observer.State with { KeyboardWords = words };
            };
            string? pending = null;
            input.OnText = batch => { Check.Equal(0, input.HeldKeys.Count); pending = batch; };
            int ticks = 0;
            using var menu = new ModernMenuAutomation(observer, input, (_, _) =>
            {
                if (++ticks <= 2)
                {
                    Check.Equal(0, input.TextBatches.Count);
                    if (ticks == 2) observer.State = observer.State with { KeyboardWords = new int[8] };
                }
                if (input.HeldKeys.Contains(Keys.Z)) observer.State = observer.State with { Text = "" };
                if (pending is not null) { observer.State = observer.State with { Text = observer.State.Text + pending }; pending = null; }
                return Task.CompletedTask;
            });
            await menu.EnterTextAsync(text, token);
            Check.Equal(text, observer.State.Text); Check.Equal(0, input.HeldKeys.Count);
            Check.Equal(text, string.Concat(input.TextBatches));
            Check.True(input.TextBatches.All(batch => batch.Length <= 64 && !char.IsHighSurrogate(batch[^1])));
            Check.Equal((text.Length + 63) / 64, input.TextBatches.Count);
            int batches = input.TextBatches.Count;
            await menu.EnterTextAsync(text, token);
            Check.Equal(batches, input.TextBatches.Count);
            await Check.ThrowsAsync<InvalidDataException>(() => menu.EnterTextAsync("bad\ntext", token));
        }
        Check.True(ModernMenuAutomation.TemplateMatches("{\"hairStyle\":4,\"hairColor\":\"123456\"}")(
            Snapshot(TerrariaMenuPage.Character) with { PlayerValues = new Dictionary<string, int> { ["hair"] = 4, ["hairColor"] = 0x563412 } }));
    }

    private static async Task RandomClickJourney(CancellationToken token)
    {
        foreach (bool legacyPredicate in new[] { false, true })
        {
            var observer = new Observer { State = Snapshot(TerrariaMenuPage.Advanced) with { Seed = "old" } };
            var input = new Input(observer);
            bool downPending = false, upPending = false;
            int received = 0;
            input.OnMouse = down => { downPending = down; upPending = !down; };
            using var menu = new ModernMenuAutomation(observer, input, (_, _) =>
            {
                if (downPending)
                {
                    // SendInput has returned, but Terraria has not consumed the press until this sample.
                    Check.Equal(0, input.Ups);
                    downPending = false; received++;
                    observer.State = observer.State with { MouseDown = true, UiMouseDown = true, LastDown = observer.State.Hover };
                }
                if (upPending)
                {
                    Check.Equal(1, received);
                    upPending = false;
                    observer.State = observer.State with { MouseDown = false, UiMouseDown = false, LastDown = IntPtr.Zero, Seed = "new" };
                }
                return Task.CompletedTask;
            });
            MenuExpectation expected = legacyPredicate
                ? new(TerrariaMenuPage.Advanced, TerrariaMenuPage.Advanced, s => !string.IsNullOrEmpty(s.Seed))
                : MenuExpectation.Page(TerrariaMenuPage.Advanced, TerrariaMenuPage.Advanced);
            await menu.ClickAsync("random seed", new(TerrariaMenuButton.AdvancedMenu), expected, token);
            PyramidVisibleSeedReadResult result = await menu.WaitForSeedAfterRandomizeAsync("old", token);
            Check.True(result.Success); Check.Equal("new", result.SeedText);
            Check.Equal(1, input.Downs); Check.Equal(1, input.Ups);
        }
    }

    private static async Task EnsureJourney(CancellationToken token)
    {
        foreach (bool satisfied in new[] { false, true })
        {
            var observer = new Observer { State = Snapshot(TerrariaMenuPage.Advanced) with { SpecialSeeds = new bool[10] } };
            observer.State.SpecialSeeds![5] = satisfied;
            var input = new Input(observer);
            input.OnMouse = down =>
            {
                observer.State = observer.State with { MouseDown = down, UiMouseDown = down, LastDown = down ? observer.State.Hover : IntPtr.Zero };
                if (down && input.Downs == 2) observer.State.SpecialSeeds![5] = true;
            };
            using var menu = new ModernMenuAutomation(observer, input, (_, ct) => Task.Delay(1, ct));
            var ensure = MenuExpectation.Ensure(TerrariaMenuPage.Advanced, s => s.SpecialSeeds![5], "For the Worthy enabled", ModernMenuAutomation.SpecialSeedsReadable);
            await menu.ClickAsync("worthy", new(TerrariaMenuButton.AdvancedMenu), ensure, token);
            Check.Equal(satisfied ? 0 : 2, input.Downs); Check.Equal(input.Downs, input.Ups);
            observer.State = observer.State with { SpecialSeeds = null, SpecialSeedLayoutError = "Missing Option._enabled" };
            await Check.ThrowsAsync<InvalidDataException>(() => menu.ClickAsync("unreadable", new(TerrariaMenuButton.SpecialSeed, 5), ensure, token));
            Check.Equal(satisfied ? 0 : 2, input.Downs);
        }
        Check.Equal(1000.0, ModernMenuAutomation.StepTimeout.TotalMilliseconds);
        Check.Equal(2000.0, ModernMenuAutomation.ListTimeout.TotalMilliseconds);
        var keyboard = new Observer { State = Snapshot(TerrariaMenuPage.Keyboard) with { TextMaximumLength = 20 } };
        var keys = new Input(keyboard);
        using var textMenu = new ModernMenuAutomation(keyboard, keys, (_, _) => Task.CompletedTask);
        await Check.ThrowsAsync<InvalidDataException>(() => textMenu.EnterTextAsync(new string('a', 21), token));
        await Check.ThrowsAsync<InvalidDataException>(() => textMenu.EnterTextAsync(new string('a', 16 * 1024 + 1), token));
        Check.Equal(0, keys.TextBatches.Count);
    }

    private static void SeedSubmissionJourney()
    {
        TerrariaMenuSnapshot parsed = Snapshot(TerrariaMenuPage.World) with
        {
            WorldWidth = 4200, GameMode = 0, Evil = 0,
            SecretSeeds = new[] { "Abandoned manors", "Arachnophobia" }, Seed = "", SpecialSeeds = new bool[10]
        };
        parsed.SpecialSeeds![0] = true;
        string bootstrap = "1.1.1.0.Abandoned manors|Arachnophobia|";
        Check.True(WorldCreationMenuDriver.SeedSubmissionMatches(parsed, bootstrap));
        Check.False(WorldCreationMenuDriver.SeedSubmissionMatches(parsed with { SecretSeeds = new[] { "Abandoned manors" } }, bootstrap));
        Check.False(WorldCreationMenuDriver.SeedSubmissionMatches(parsed with { WorldWidth = 6400 }, bootstrap));
        Check.True(WorldCreationMenuDriver.SeedSubmissionMatches(parsed with { Seed = "123" }, bootstrap + "123"));
        Check.True(WorldCreationMenuDriver.SeedSubmissionMatches(parsed with { Seed = "fixed" }, "fixed"));
        Check.False(WorldCreationMenuDriver.SeedSubmissionMatches(parsed with { Seed = "old" }, "fixed"));
        Check.True(WorldCreationMenuDriver.SeedSubmissionMatches(parsed with
            { WorldWidth = 6400, GameMode = 1, Evil = 1, SecretSeeds = Array.Empty<string>(), Seed = "123" }, "2.2.2.123"));
        var zenith = new bool[10]; Array.Fill(zenith, true, 1, 8);
        Check.True(WorldCreationMenuDriver.SeedSubmissionMatches(parsed with
            { SpecialSeeds = zenith, SecretSeeds = Array.Empty<string>(), Seed = "123" }, "1.1.1.128.123"));
        Check.False(WorldCreationMenuDriver.SeedSubmissionMatches(parsed with
            { SecretSeeds = Array.Empty<string>(), Seed = "123" }, "1.1.1.128.123"));
    }

    private static async Task TextFailureJourney(CancellationToken token)
    {
        foreach (string mode in new[] { "cancel", "focus", "mismatch", "clear-cancel" })
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var observer = new Observer { State = Snapshot(TerrariaMenuPage.Keyboard) with { Text = mode == "clear-cancel" ? "old" : "" } };
            var input = new Input(observer);
            input.OnKey = (key, down) => { if (down && key == Keys.Z) cts.Cancel(); };
            input.OnText = _ =>
            {
                if (mode == "cancel") cts.Cancel();
                else if (mode == "focus") input.IsForeground = false;
                else observer.State = observer.State with { Text = "wrong" };
            };
            using var menu = new ModernMenuAutomation(observer, input, (_, ct) => Task.Delay(1, ct));
            if (mode is "cancel" or "clear-cancel")
                await Check.ThrowsAsync<OperationCanceledException>(() => menu.EnterTextAsync(new string('1', 40), cts.Token));
            else
                await Check.ThrowsAsync<InvalidOperationException>(() => menu.EnterTextAsync(new string('1', 40), cts.Token));
            Check.Equal(mode == "clear-cancel" ? 0 : 1, input.TextBatches.Count);
            Check.Equal(0, input.HeldKeys.Count);
        }
    }

    private static async Task UnknownJourney(CancellationToken token)
    {
        var observer = new Observer { State = TerrariaMenuSnapshot.Unknown("invalid pointer") };
        var input = new Input(observer);
        using var menu = new ModernMenuAutomation(observer, input, (_, ct) => Task.Delay(1, ct));
        await menu.InitializeAsync(token);
        TimeoutException error = await Check.ThrowsAsync<TimeoutException>(() => menu.WaitAsync("new world",
            s => s.Ready, token, TimeSpan.FromMilliseconds(650)));
        Check.True(error.Message.Contains("invalid pointer", StringComparison.Ordinal)); Check.Equal(0, input.Downs);
        Check.Equal(1, observer.Initializations);
    }

    private static void VersionBoundary()
    {
        Check.True(TerrariaMenuObserver.IsSupported("1.4.5.8"));
        Check.False(TerrariaMenuObserver.IsSupported("1.4.4.9"));
        Check.True(TerrariaMenuObserver.IsSupported("1.4.5.7"));
        Check.True(TerrariaMenuObserver.IsSupported("1.4.6"));
        Check.False(TerrariaMenuObserver.IsSupported(null));
        var context = new TerrariaAutomationContext("legacy");
        context.ConfigureTiming(new AutoCreateWorldSettings { ClickFocusDelayMilliseconds = 73, InputPressDurationMilliseconds = 89 });
        Check.True(context.ModernMenu is null); Check.Equal(73, context.Window.ClickFocusDelayMilliseconds);
        Check.Equal(89, context.Window.InputPressDurationMilliseconds);
    }

    private static RuntimeLayoutDto LayoutDto(long address, long startTime = 100) => new("1.4.5.8", startTime,
        new(address, 0, address), new(new()), null, null, null, null,
        new(0, 0, 0, -1, -1, -1, -1, -1, -1, -1, -1), 83,
        new(MenuAutomationLayoutDto.RequiredStatics.ToDictionary(key => key, _ => address),
            MenuAutomationLayoutDto.RequiredFields.ToDictionary(key => key, _ => 4),
            MenuAutomationLayoutDto.RequiredTypes.ToDictionary(key => key, _ => address),
            Enumerable.Range(0, 8).Select(i => i * 4).ToArray()), new(address, address + 1, true));

    private static TerrariaRuntimeMemoryLayout Layout(long address) => LayoutDto(address).ToRuntimeMemoryLayout();

    private static async Task LayoutCacheJourney(CancellationToken token)
    {
        var probes = new List<TaskCompletionSource<TerrariaRuntimeMemoryLayout>>();
        var cache = new TerrariaRuntimeLayoutCache(_ =>
        {
            var pending = new TaskCompletionSource<TerrariaRuntimeMemoryLayout>(TaskCreationOptions.RunContinuationsAsynchronously);
            probes.Add(pending);
            return pending.Task;
        });
        var game = new TerrariaProcessIdentity(42, 100);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task<TerrariaRuntimeMemoryLayout> first = cache.GetAsync(game, null, cancelled.Token);
        Task<TerrariaRuntimeMemoryLayout> concurrent = cache.GetAsync(game, null, token);
        Check.Equal(1, probes.Count);
        cancelled.Cancel();
        await Check.ThrowsAsync<OperationCanceledException>(async () => { _ = await first; });
        TerrariaRuntimeMemoryLayout initial = Layout(1000);
        probes[0].SetResult(initial);
        Check.True(ReferenceEquals(initial, await concurrent));
        Check.True(ReferenceEquals(initial, await cache.GetAsync(game, null, token)));
        Check.Equal(1, probes.Count);

        Task<TerrariaRuntimeMemoryLayout> refresh = cache.GetAsync(game, initial, token);
        Task<TerrariaRuntimeMemoryLayout> concurrentRefresh = cache.GetAsync(game, initial, token);
        Check.Equal(2, probes.Count);
        TerrariaRuntimeMemoryLayout updated = Layout(2000);
        probes[1].SetResult(updated);
        Check.True(ReferenceEquals(updated, await refresh));
        Check.True(ReferenceEquals(updated, await concurrentRefresh));
        Check.True(ReferenceEquals(updated, await cache.GetAsync(game, initial, token)));
        Check.Equal(2, probes.Count);

        Task<TerrariaRuntimeMemoryLayout> oldRefresh = cache.GetAsync(game, updated, token);
        // PID reuse must not reuse a layout from the previous process. A late old probe must not replace it.
        var restartedGame = new TerrariaProcessIdentity(42, 101);
        Task<TerrariaRuntimeMemoryLayout> restarted = cache.GetAsync(restartedGame, null, token);
        Check.Equal(4, probes.Count);
        probes[2].SetResult(Layout(3000));
        _ = await oldRefresh;
        TerrariaRuntimeMemoryLayout fresh = Layout(4000);
        probes[3].SetResult(fresh);
        Check.True(ReferenceEquals(fresh, await restarted));
        Check.True(ReferenceEquals(fresh, await cache.GetAsync(restartedGame, null, token)));
        Check.Equal(4, probes.Count);
        Check.True(cache.TryGet(restartedGame, out var cached));
        Check.True(ReferenceEquals(fresh, cached));
        Check.False(cache.TryGet(game, out _));
    }

    private static async Task FailedLayoutJourney(CancellationToken token)
    {
        int calls = 0;
        TerrariaRuntimeMemoryLayout expected = Layout(1000);
        var cache = new TerrariaRuntimeLayoutCache(_ => ++calls == 1
            ? Task.FromException<TerrariaRuntimeMemoryLayout>(new InvalidOperationException("probe failed"))
            : Task.FromResult(expected));
        var game = new TerrariaProcessIdentity(42, 100);
        await Check.ThrowsAsync<InvalidOperationException>(async () => { _ = await cache.GetAsync(game, null, token); });
        Check.True(ReferenceEquals(expected, await cache.GetAsync(game, null, token)));
        Check.Equal(2, calls);
        var response = new RuntimeLayoutResponse(true, null, LayoutDto(1000, game.StartTimeUtcTicks));
        string json = System.Text.Json.JsonSerializer.Serialize(response);
        RuntimeLayoutResponse decoded = System.Text.Json.JsonSerializer.Deserialize<RuntimeLayoutResponse>(json)!;
        Check.Equal(game.StartTimeUtcTicks, decoded.Layout!.ProcessStartTimeUtcTicks);
        Check.Equal(1000L, decoded.Layout.MenuAutomation!.Statics["MenuUI"]);
        RuntimeLayoutDto partial = decoded.Layout with
        {
            CapabilityErrors = new Dictionary<string, string> { ["ItemDetection"] = "Missing item field." }
        };
        RuntimeLayoutDto decodedPartial = System.Text.Json.JsonSerializer.Deserialize<RuntimeLayoutDto>(
            System.Text.Json.JsonSerializer.Serialize(partial))!;
        Check.Equal("Missing item field.", decodedPartial.ToRuntimeMemoryLayout()
            .ValidationError(TerrariaSplit.Compatibility.TerrariaFeature.ItemDetection));
    }

    private static async Task ColdStartupJourney(CancellationToken token)
    {
        var game = new TerrariaProcessIdentity(42, 100);
        TerrariaRuntimeMemoryLayout cold = Layout(1000) with { MenuAutomation = null, Startup = new(1000, 1001, false) };
        var startup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int probes = 0, waits = 0;
        TerrariaRuntimeMemoryLayout ready = Layout(2000);
        var cache = new TerrariaRuntimeLayoutCache(_ => Task.FromResult(++probes == 1 ? cold : probes == 2 ? ready : Layout(3000)),
            (_, _) => { waits++; return startup.Task; });
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task<TerrariaRuntimeMemoryLayout> first = cache.GetAsync(game, null, cancelled.Token);
        Task<TerrariaRuntimeMemoryLayout> shared = cache.GetAsync(game, null, token);
        Check.Equal(1, probes); Check.Equal(1, waits); Check.False(cache.TryGet(game, out _)); Check.False(shared.IsCompleted);
        cancelled.Cancel();
        await Check.ThrowsAsync<OperationCanceledException>(async () => { _ = await first; });
        Check.False(shared.IsCompleted);
        startup.SetResult();
        Check.True(ReferenceEquals(ready, await shared)); Check.Equal(2, probes); Check.Equal(2, cache.ProbeCount);
        Check.True(ReferenceEquals(ready, await cache.GetAsync(game, null, token)));
        // A stale/incomplete existing entry must also be rejected, rather than reused forever.
        ready.MenuAutomation!.Statics["Evil"] = 0;
        Check.False(cache.TryGet(game, out _, TerrariaSplit.Compatibility.TerrariaFeature.ForegroundWorldCreation));
        Check.True(cache.TryGet(game, out _));
        Check.Equal(3000L, (await cache.GetAsync(game, null, token, TerrariaSplit.Compatibility.TerrariaFeature.ForegroundWorldCreation)).MenuAutomation!.Statics["Evil"]);
        Check.Equal(3, probes);

        foreach (string defect in new[] { "Evil", "generatingWorld", "WorldOptions", "Keyboard", "ReferenceList._items", "startup", "words" })
        {
            TerrariaRuntimeMemoryLayout incomplete = Layout(4000);
            if (defect == "startup") incomplete = incomplete with { Startup = null };
            else if (defect == "words") incomplete = incomplete with { MenuAutomation = incomplete.MenuAutomation! with { KeyboardWordOffsets = new int[8] } };
            else if (defect == "Keyboard") incomplete.MenuAutomation!.Types.Remove(defect);
            else if (defect == "ReferenceList._items") incomplete.MenuAutomation!.Fields[defect] = -1;
            else incomplete.MenuAutomation!.Statics[defect] = 0;
            int attempts = 0;
            var recovering = new TerrariaRuntimeLayoutCache(_ => Task.FromResult(++attempts == 1 ? incomplete : Layout(5000)));
            await Check.ThrowsAsync<InvalidOperationException>(async () => { _ = await recovering.GetAsync(game, null, token,
                TerrariaSplit.Compatibility.TerrariaFeature.ForegroundWorldCreation); });
            Check.False(recovering.TryGet(game, out _, TerrariaSplit.Compatibility.TerrariaFeature.ForegroundWorldCreation));
            Check.True(recovering.TryGet(game, out _));
            Check.True((await recovering.GetAsync(game, null, token, TerrariaSplit.Compatibility.TerrariaFeature.ForegroundWorldCreation))
                .ValidationError(TerrariaSplit.Compatibility.TerrariaFeature.ForegroundWorldCreation) is null);
            Check.Equal(2, attempts);
        }
        TerrariaRuntimeMemoryLayout legacy = Layout(1) with { TerrariaVersion = "1.4.4.9", MenuAutomation = null, Startup = null };
        Check.True(legacy.CacheValidationError() is null);
    }

    private static void SharedResolverJourney()
    {
        int probes = 0;
        var cache = new TerrariaRuntimeLayoutCache(_ => Task.FromResult(Layout(++probes)));
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var first = new TerrariaClrMemoryResolver(layoutCache: cache);
        var second = new TerrariaClrMemoryResolver(layoutCache: cache);
        first.SetProcess(process); second.SetProcess(process);
        var memory = new Memory();
        Check.True(first.TryGetRuntimeLayout(memory, out var initial));
        Check.True(second.TryGetRuntimeLayout(memory, out var reused));
        Check.Equal(1, probes); Check.True(ReferenceEquals(initial, reused));
        Check.True(ReferenceEquals(initial.MenuAutomation, reused.MenuAutomation));
        first.ResetLayout();
        Check.True(first.TryGetRuntimeLayout(memory, out var refreshed));
        Check.Equal(2, probes); Check.False(ReferenceEquals(initial, refreshed));
    }

    private static async Task SharedSeedJourney(CancellationToken token)
    {
        var observer = new Observer { State = Snapshot(TerrariaMenuPage.Advanced) with { Seed = "111" } };
        var input = new Input(observer);
        using var menu = new ModernMenuAutomation(observer, input, (_, _) =>
        {
            observer.State = observer.State with { Seed = "222" };
            return Task.CompletedTask;
        });
        await menu.InitializeAsync(token);
        IPyramidVisibleSeedReader seeds = menu;
        Check.Equal("111", seeds.ReadCurrentSeed());
        PyramidVisibleSeedReadResult changed = await seeds.WaitForSeedAfterRandomizeAsync("111", token);
        Check.True(changed.Success); Check.Equal("222", changed.SeedText); Check.Equal(2, changed.ReadAttempts);
        Check.Equal("222", seeds.ReadCurrentSeed()); Check.Equal(1, observer.Initializations);
        observer.State = observer.State with { Page = TerrariaMenuPage.Keyboard };
        Check.True(seeds.ReadCurrentSeed() is null);
        Check.Equal(0, input.Downs);
    }

    private static void MemoryJourney()
    {
        var memory = new Memory();
        var layout = new MenuAutomationLayoutDto(new()
        {
            ["menuMode"] = 100, ["mouseLeft"] = 104, ["maxTilesX"] = 108, ["Evil"] = 112,
            ["ActiveWorldFileData"] = 116, ["MenuUI"] = 120, ["inputText"] = 124, ["generatingWorld"] = 156,
            ["_uiScaleUsed"] = 160, ["gameMenu"] = 164, ["ScreenWidth"] = 180, ["ScreenHeight"] = 184,
            ["mouseLeftRelease"] = 188, ["MouseX"] = 192, ["MouseY"] = 196
        }, new()
        {
            ["WorldData.GameMode"] = 4, ["UI._currentState"] = 4, ["UI._isStateDirty"] = 8,
            ["UI._clickDisabledTimeRemaining"] = 12, ["UI._lastElementHover"] = 20, ["UI.LeftMouse"] = 24,
            ["UI.MousePosition"] = 28, ["Pointer.WasDown"] = 4, ["Pointer.LastDown"] = 8, ["World._optionSeed"] = 4
        }, new() { ["World"] = 900, ["KeyboardState"] = 950 }, Enumerable.Range(0, 8).Select(i => i * 4).ToArray());
        memory.Set(100, BitConverter.GetBytes(888)); memory.Set(104, [0]);
        memory.Set(108, BitConverter.GetBytes(4200)); memory.Set(112, BitConverter.GetBytes(1));
        memory.Set(116, BitConverter.GetBytes(2000)); memory.Set(120, BitConverter.GetBytes(3000));
        memory.Set(124, BitConverter.GetBytes(8000)); memory.Set(8000, BitConverter.GetBytes(950)); memory.Set(8004, new byte[32]);
        memory.Set(156, [0]); memory.Set(2004, BitConverter.GetBytes(2));
        memory.Set(160, BitConverter.GetBytes(1.2f));
        memory.Set(164, [1]);
        memory.Set(180, BitConverter.GetBytes(1920)); memory.Set(184, BitConverter.GetBytes(1080));
        memory.Set(188, [1]); memory.Set(192, BitConverter.GetBytes(120)); memory.Set(196, BitConverter.GetBytes(294));
        memory.Set(3004, BitConverter.GetBytes(4000)); memory.Set(3008, [0]);
        memory.Set(3012, BitConverter.GetBytes(200.0)); memory.Set(3020, BitConverter.GetBytes(6000));
        memory.Set(3024, BitConverter.GetBytes(5000)); memory.Set(3028, BitConverter.GetBytes(100f).Concat(BitConverter.GetBytes(100f)).ToArray());
        memory.Set(4000, BitConverter.GetBytes(900)); memory.Set(4004, BitConverter.GetBytes(7000));
        memory.Set(5004, [0]); memory.Set(5008, BitConverter.GetBytes(0));
        memory.Set(7004, BitConverter.GetBytes(3)); memory.Set(7008, System.Text.Encoding.Unicode.GetBytes("123"));
        var reader = new TerrariaMenuStateReader();
        TerrariaMenuSnapshot first = reader.Read(memory, layout);
        Check.True(first.Valid); Check.False(first.Ready); Check.Equal(TerrariaMenuPage.World, first.Page);
        Check.Equal(2, first.GameMode); Check.Equal("123", first.Seed);
        Check.True(first.CursorAt(new Point(120, 120))); Check.False(first.CursorAt(new Point(100, 100)));
        memory.Set(3012, BitConverter.GetBytes(0.0)); Check.True(reader.Read(memory, layout).Ready);
        memory.Set(3012, BitConverter.GetBytes(double.NaN)); Check.False(reader.Read(memory, layout).Valid);
        memory.Set(156, [1]); Check.Equal(TerrariaMenuPage.Generating, reader.Read(memory, layout).Page);
        // A moved boxed KeyboardState must be followed through its static root.
        memory.Set(124, BitConverter.GetBytes(9000)); memory.Set(9000, BitConverter.GetBytes(950)); memory.Set(9004, new byte[32]);
        memory.Set(9004 + ((int)Keys.V / 32) * 4, BitConverter.GetBytes(1 << ((int)Keys.V % 32)));
        Check.True(reader.Read(memory, layout).KeyDown((int)Keys.V));
        memory.Set(164, [0]); Check.False(reader.Read(memory, layout).Valid);

        memory.Set(164, [1]); memory.Set(156, [0]); memory.Set(3012, BitConverter.GetBytes(0.0));
        var keyboardFields = new Dictionary<string, int>(layout.Fields)
        {
            ["Keyboard._textBox"] = 4, ["Keyboard._canSubmit"] = 8, ["TextBox._text"] = 4, ["TextBox._maxLength"] = 8
        };
        var keyboardTypes = new Dictionary<string, long>(layout.Types) { ["Keyboard"] = 901 };
        var keyboardLayout = new MenuAutomationLayoutDto(layout.Statics, keyboardFields, keyboardTypes, layout.KeyboardWordOffsets);
        memory.Set(4000, BitConverter.GetBytes(901)); memory.Set(4004, BitConverter.GetBytes(10000)); memory.Set(4008, [1]);
        memory.Set(10004, BitConverter.GetBytes(11000));
        memory.Set(10008, BitConverter.GetBytes(int.MaxValue));
        foreach (int length in new[] { 512, 576, 608, 640, 16 * 1024 })
        {
            string text = new('种', length);
            memory.Set(11004, BitConverter.GetBytes(length)); memory.Set(11008, System.Text.Encoding.Unicode.GetBytes(text));
            TerrariaMenuSnapshot keyboard = reader.Read(memory, keyboardLayout);
            Check.True(keyboard.Valid); Check.True(keyboard.CanSubmit); Check.Equal(text, keyboard.Text);
            if (length > 512) Check.False(ManagedObjectMemoryReader.TryReadManagedString(memory, new IntPtr(11000), out _));
        }
        foreach (int invalidLength in new[] { -1, 16 * 1024 + 1, int.MaxValue })
        {
            memory.Set(11004, BitConverter.GetBytes(invalidLength));
            Check.False(reader.Read(memory, keyboardLayout).Valid);
        }

        layout.Statics["WorldOptions"] = 168; layout.Statics["ActivePlayerFileData"] = 172;
        layout.Fields["World._secretSeedTextsEntered"] = 8;
        layout.Fields["ReferenceList._items"] = 4; layout.Fields["ReferenceList._size"] = 8;
        layout.Fields["Option._enabled"] = 4; layout.Fields["FileData._path"] = 4;
        memory.Set(4000, BitConverter.GetBytes(900)); memory.Set(4008, BitConverter.GetBytes(20000));
        memory.Set(168, BitConverter.GetBytes(21000));
        memory.Set(21004, BitConverter.GetBytes(22000)); memory.Set(21008, BitConverter.GetBytes(10));
        memory.Set(22004, BitConverter.GetBytes(10));
        for (int i = 0; i < 10; i++)
        {
            layout.Types["SeedOption." + i] = 25000 + i;
            memory.Set(22008 + i * 4, BitConverter.GetBytes(24000 + i * 32));
            memory.Set(24000 + i * 32, BitConverter.GetBytes(25000 + i));
            memory.Set(24004 + i * 32, [i == 5 ? (byte)1 : (byte)0]);
        }
        memory.Set(20004, BitConverter.GetBytes(23000)); memory.Set(20008, BitConverter.GetBytes(2));
        memory.Set(23004, BitConverter.GetBytes(2));
        foreach ((int i, string name) in new[] { (0, "Abandoned manors"), (1, "Arachnophobia") })
        {
            int address = 26000 + i * 100;
            memory.Set(23008 + i * 4, BitConverter.GetBytes(address));
            memory.Set(address + 4, BitConverter.GetBytes(name.Length)); memory.Set(address + 8, System.Text.Encoding.Unicode.GetBytes(name));
        }
        TerrariaMenuSnapshot options = reader.Read(memory, layout);
        Check.True(options.Valid); Check.True(ModernMenuAutomation.SpecialSeedsReadable(options));
        Check.True(options.SpecialSeeds![5]); Check.False(options.SpecialSeeds[4]);
        Check.Equal("Abandoned manors|Arachnophobia", string.Join("|", options.SecretSeeds!));
        // A wrong option type must not be interpreted as an enabled/disabled seed.
        memory.Set(24000 + 5 * 32, BitConverter.GetBytes(99999));
        Check.True(reader.Read(memory, layout).SpecialSeeds is null);
        layout.Fields["Option._enabled"] = -1;
        Check.Throws<InvalidDataException>(() => ModernMenuAutomation.SpecialSeedsReadable(reader.Read(memory, layout)));
        layout.Fields["Option._enabled"] = 4;
        layout.Types["WorldSelect"] = 902; memory.Set(4000, BitConverter.GetBytes(902));
        memory.Set(172, BitConverter.GetBytes(27000)); memory.Set(27004, BitConverter.GetBytes(28000));
        string path = @"D:\Players\created.plr";
        memory.Set(28004, BitConverter.GetBytes(path.Length)); memory.Set(28008, System.Text.Encoding.Unicode.GetBytes(path));
        Check.Equal(path, reader.Read(memory, layout).ActivePlayerPath);
    }

    private sealed class Memory : IProcessMemoryReader
    {
        private readonly Dictionary<long, byte> bytes = new();
        public void Set(long address, byte[] values) { for (int i = 0; i < values.Length; i++) bytes[address + i] = values[i]; }
        public bool Is64Bit => false;
        public bool TryReadBytes(IntPtr address, int count, [NotNullWhen(true)] out byte[]? value)
        {
            value = null; var result = new byte[count];
            for (int i = 0; i < count; i++) if (!bytes.TryGetValue(address.ToInt64() + i, out result[i])) return false;
            value = result; return true;
        }
        public bool TryReadInt32(IntPtr address, out int value) { bool ok = TryReadBytes(address, 4, out byte[]? b); value = ok ? BitConverter.ToInt32(b!) : 0; return ok; }
        public bool TryReadDouble(IntPtr address, out double value) { bool ok = TryReadBytes(address, 8, out byte[]? b); value = ok ? BitConverter.ToDouble(b!) : 0; return ok; }
        public bool TryReadBool(IntPtr address, out bool value) { bool ok = TryReadBytes(address, 1, out byte[]? b); value = ok && b![0] != 0; return ok; }
        public bool TryReadPointerValue(IntPtr address, out IntPtr value) { bool ok = TryReadInt32(address, out int i); value = new IntPtr(i); return ok; }
        public bool TryReadPointer(IntPtr address, out IntPtr value) => TryReadPointerValue(address, out value);
        public IEnumerable<MemoryPage> ExecutablePages() => [];
        public IEnumerable<MemoryPage> ExecutablePrivatePages() => [];
    }
}
