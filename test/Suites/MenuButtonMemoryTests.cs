using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Text;
using TerrariaSplit.MemoryBridge.Protocol;
using TerrariaSplit.Terraria.Memory;
using TerrariaSplit.Terraria.Automation;
using System.Windows.Forms;

namespace TerrariaSplit.Tests;

internal static class MenuButtonMemoryTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("1458 every automation button reads real game layout at different scales and after relocation", TestSuite.Core, LayoutJourney);
        yield return TestCase.Sync("1458 actual button reading rejects torn stale invisible and malformed game layouts", TestSuite.Core, InvalidLayoutJourney);
        yield return TestCase.Async("1458 main menu moves to the actual game button only after layout and release and confirms the game cursor", TestSuite.Core, MainClickJourney);
        yield return TestCase.Sync("1458 keyboard submit selects the attached layout and rejects inactive stale and changing layers", TestSuite.Core, KeyboardLayoutJourney);
        yield return TestCase.Async("1458 keyboard and gamepad submit both player names and seeds using the visible button", TestSuite.Core, KeyboardSubmitJourney);
    }

    private static IEnumerable<(TerrariaMenuPage Page, TerrariaMenuTarget Target, string? Field, int? ArrayIndex, string? Snap, int SnapId)> Cases()
    {
        yield return (TerrariaMenuPage.Main, new(TerrariaMenuButton.SinglePlayer), null, null, null, 0);
        yield return (TerrariaMenuPage.PlayerSelect, new(TerrariaMenuButton.NewPlayer), "PlayerSelect._newPanel", null, null, 0);
        yield return (TerrariaMenuPage.WorldSelect, new(TerrariaMenuButton.NewWorld), "WorldSelect._newPanel", null, null, 0);
        yield return (TerrariaMenuPage.Character, new(TerrariaMenuButton.CharacterInfo), "Character._charInfoCategoryButton", null, null, 0);
        yield return (TerrariaMenuPage.Character, new(TerrariaMenuButton.CharacterClothing), "Character._clothingStylesCategoryButton", null, null, 0);
        yield return (TerrariaMenuPage.Character, new(TerrariaMenuButton.PasteTemplate), "Character._pasteTemplateButton", null, null, 0);
        yield return (TerrariaMenuPage.Character, new(TerrariaMenuButton.CreatePlayer), null, null, "Create", 0);
        yield return (TerrariaMenuPage.World, new(TerrariaMenuButton.CreateWorld), null, null, "Create", 0);
        yield return (TerrariaMenuPage.Keyboard, new(TerrariaMenuButton.Submit), "Keyboard._submitButton", null, null, 0);
        foreach (int index in new[] { 0, 1, 5 })
            yield return (TerrariaMenuPage.PlayerSelect, new(TerrariaMenuButton.PlayPlayer, index), null, null, "Play", index);
        for (int index = 0; index < 4; index++)
        {
            yield return (TerrariaMenuPage.Character, new(TerrariaMenuButton.PlayerDifficulty, index), null, null, "Middle", index == 3 ? 1 : index + 2);
            yield return (TerrariaMenuPage.World, new(TerrariaMenuButton.WorldDifficulty, index), "World._difficultyButtons", index == 3 ? 0 : index + 1, null, 0);
        }
        for (int index = 0; index < 3; index++)
        {
            yield return (TerrariaMenuPage.World, new(TerrariaMenuButton.WorldSize, index + 1), "World._sizeButtons", index, null, 0);
            yield return (TerrariaMenuPage.World, new(TerrariaMenuButton.WorldEvil, index - 1), "World._evilButtons", index, null, 0);
        }
        yield return (TerrariaMenuPage.World, new(TerrariaMenuButton.WorldSeed), "World._seedPlate", null, null, 0);
        yield return (TerrariaMenuPage.World, new(TerrariaMenuButton.AdvancedMenu), "World._advancedSeedButton", null, null, 0);
        yield return (TerrariaMenuPage.Advanced, new(TerrariaMenuButton.RandomSeed), "Advanced._randomButton", null, null, 0);
        yield return (TerrariaMenuPage.Advanced, new(TerrariaMenuButton.AdvancedSeed), "Advanced._seedPlate", null, null, 0);
        yield return (TerrariaMenuPage.Advanced, new(TerrariaMenuButton.ApplySeed), "Advanced._backButton", null, null, 0);
        for (int index = 0; index < 10; index++)
            yield return (TerrariaMenuPage.Advanced, new(TerrariaMenuButton.SpecialSeed, index), "Advanced._seedButtons", index, null, 0);
    }

    private static void LayoutJourney()
    {
        foreach (long allocationStart in new[] { 10000L, 0x7FFF8000L, 0x8D5C0000L, 0xFFF00000L })
        foreach (float scale in new[] { 1f, 1.2f, 2f / 3f, 1.5f })
        foreach (var sample in Cases())
        {
            var fixture = new Fixture(sample.Page, scale, allocationStart: allocationStart);
            var reader = new TerrariaMenuButtonReader();
            IntPtr element = fixture.Element(new(110, 215, 74, 22));
            PointF expectedUi = new(147, 226);
            if (sample.Page == TerrariaMenuPage.Main)
            {
                expectedUi = new(350, 250);
                fixture.MainPosition(expectedUi);
            }
            else if (sample.Page == TerrariaMenuPage.Keyboard)
            {
                fixture.KeyboardButtons(element, fixture.Element(new(3000, 3000, 240, 60)), fullVisible: true);
            }
            else if (sample.Field is { } field)
            {
                IntPtr value = element;
                if (sample.ArrayIndex is { } index)
                {
                    IntPtr[] items = Enumerable.Range(0, 10).Select(_ => fixture.Element(new(1, 1, 2, 2))).ToArray();
                    items[index] = element;
                    value = fixture.Array(items);
                }
                fixture.SetPointer(fixture.Root, field, value);
            }
            else
            {
                fixture.Snap(element, sample.Snap!, sample.SnapId);
                IntPtr other = fixture.Element(new(1, 1, 2, 2));
                fixture.Snap(other, sample.Snap!, sample.SnapId + 1);
                IntPtr parent = fixture.Element(new(1, 1, 500, 500));
                fixture.Children(parent, other, element);
                fixture.Children(fixture.Root, fixture.Element(new(1, 1, 2, 2)), parent);
            }
            if (!reader.TryRead(fixture.Memory, fixture.Layout, fixture.Menu, sample.Target, out var found, out string detail))
                throw new InvalidOperationException($"{sample.Target} at scale {scale}, allocation 0x{allocationStart:X}: {detail}");
            Check.Equal(Scaled(expectedUi, scale), found.Point);
            Check.True(found.Detail.Contains(sample.Page == TerrariaMenuPage.Main ? "MenuItemPositions" : "_dimensions", StringComparison.Ordinal));
            // Layout positions and references are deliberately moved; a cached absolute pointer/point must not win.
            if (sample.Page == TerrariaMenuPage.Main) fixture.MainPosition(expectedUi = new(420, 310));
            else { fixture.Bounds(element, new(140, 235, 74, 22)); expectedUi = new(177, 246); }
            if (!reader.TryRead(fixture.Memory, fixture.Layout, fixture.Menu, sample.Target, out found, out detail))
                throw new InvalidOperationException($"Relocated {sample.Target} at {scale}: {detail}");
            Check.Equal(Scaled(expectedUi, scale), found.Point);
            fixture.Memory.Int(fixture.Static("ScreenWidth"), 800);
            Check.False(reader.TryRead(fixture.Memory, fixture.Layout, fixture.Menu, sample.Target, out _, out _));
        }
        var main = new Fixture(TerrariaMenuPage.Main, 1.2f);
        main.PrepareMainState();
        var stateReader = new TerrariaMenuStateReader();
        TerrariaMenuSnapshot state = stateReader.Read(main.Memory, main.Layout);
        Check.True(state.Valid); Check.Equal(TerrariaMenuPage.Main, state.Page);
        Check.Equal(1.2f, state.UiScale); Check.Equal(new Size(2000, 1200), state.ScreenSize);
        Check.True(state.CursorAt(new(960, 294))); Check.True(state.MouseReleased);
        main.Memory.Set(main.Static("mouseLeftRelease"), [0]);
        Check.False(stateReader.Read(main.Memory, main.Layout).MouseReleased);
    }

    private static void InvalidLayoutJourney()
    {
        foreach (string mode in new[] { "zero", "nan", "offscreen", "torn", "moved-object", "page", "state", "scale", "unreadable" })
        {
            var f = new Fixture(TerrariaMenuPage.World, 1.2f);
            var target = new TerrariaMenuTarget(TerrariaMenuButton.WorldSeed);
            IntPtr element = f.Element(new(110, 215, 74, 22));
            f.SetPointer(f.Root, "World._seedPlate", element);
            switch (mode)
            {
                case "zero": f.Bounds(element, new(100, 100, 0, 20)); break;
                case "nan": f.Bounds(element, new(float.NaN, 100, 20, 20)); break;
                case "offscreen": f.Bounds(element, new(4000, 100, 20, 20)); break;
                case "page": f.Memory.Int(f.Static("menuMode"), 0); break;
                case "state": f.SetPointer(f.Ui, "UI._currentState", f.Element(new(1, 1, 20, 20))); break;
                case "scale": f.Memory.Float(f.Static("_uiScaleUsed"), 1); break;
                case "unreadable": f.SetPointer(f.Root, "World._seedPlate", new IntPtr(999999)); break;
                case "moved-object":
                    f.Memory.OnRead = (address, _) =>
                    {
                        if (address == IntPtr.Add(element, f.Layout.Fields["Element._dimensions"]))
                            f.SetPointer(f.Root, "World._seedPlate", f.Element(new(140, 235, 74, 22)));
                    };
                    break;
                case "torn":
                    int reads = 0;
                    f.Memory.OnRead = (address, _) =>
                    {
                        if (address == IntPtr.Add(element, f.Layout.Fields["Element._dimensions"]) && ++reads == 2)
                            f.Bounds(element, new(140, 235, 74, 22));
                    };
                    break;
            }
            Check.False(new TerrariaMenuButtonReader().TryRead(f.Memory, f.Layout, f.Menu, target, out _, out _));
        }
        var main = new Fixture(TerrariaMenuPage.Main, 1.2f);
        main.MainPosition(new(350, 250));
        main.Memory.Int(main.Static("MainMenuLastDrew"), 1);
        Check.False(new TerrariaMenuButtonReader().TryRead(main.Memory, main.Layout, main.Menu, new(TerrariaMenuButton.SinglePlayer), out _, out _));
        var cycle = new Fixture(TerrariaMenuPage.Character, 1);
        cycle.Children(cycle.Root, cycle.Root);
        Check.False(new TerrariaMenuButtonReader().TryRead(cycle.Memory, cycle.Layout, cycle.Menu, new(TerrariaMenuButton.CreatePlayer), out _, out _));
    }

    private static Point Scaled(PointF point, float scale) => new((int)Math.Round(point.X * scale), (int)Math.Round(point.Y * scale));

    private static void KeyboardLayoutJourney()
    {
        var f = new Fixture(TerrariaMenuPage.Keyboard, 2.1333334f, new(2880, 1920));
        var target = new TerrariaMenuTarget(TerrariaMenuButton.Submit);
        var reader = new TerrariaMenuButtonReader();
        IntPtr full = f.Element(new(1700, 1500, 240, 37)); // Detached layout's stale dimensions reproduce the report.
        IntPtr compact = f.Element(new(555, 320, 240, 60));
        var layers = f.KeyboardButtons(full, compact, fullVisible: false);
        Check.True(reader.TryRead(f.Memory, f.Layout, f.Menu, target, out var point, out _));
        Check.Equal(Scaled(new(675, 350), f.Menu.UiScale), point.Point);
        Check.True(point.Detail.Contains("Keyboard._submitButton2", StringComparison.Ordinal));
        f.Children(f.Root, layers.Full);
        // The newly attached full keyboard has not been recalculated yet. Do not select the old compact button.
        Check.False(reader.TryRead(f.Memory, f.Layout, f.Menu, target, out _, out _));
        f.Bounds(full, new(600, 400, 240, 37));
        Check.True(reader.TryRead(f.Memory, f.Layout, f.Menu, target, out point, out _));
        Check.Equal(Scaled(new(720, 418.5f), f.Menu.UiScale), point.Point);
        f.Children(f.Root, layers.Compact); // Both buttons now have in-range bounds; attachment must decide.
        Check.True(reader.TryRead(f.Memory, f.Layout, f.Menu, target, out point, out _));
        Check.Equal(Scaled(new(675, 350), f.Menu.UiScale), point.Point);
        f.Children(f.Root, layers.Full, layers.Compact);
        Check.False(reader.TryRead(f.Memory, f.Layout, f.Menu, target, out _, out _));
        f.Children(f.Root);
        Check.False(reader.TryRead(f.Memory, f.Layout, f.Menu, target, out _, out _));
        f.Children(f.Root, layers.Compact);
        f.Children(layers.Compact);
        Check.False(reader.TryRead(f.Memory, f.Layout, f.Menu, target, out _, out _));
        f.Children(layers.Compact, compact);
        f.Memory.OnRead = (address, _) =>
        {
            if (address == IntPtr.Add(compact, f.Layout.Fields["Element._dimensions"])) f.Children(f.Root, layers.Full);
        };
        Check.False(reader.TryRead(f.Memory, f.Layout, f.Menu, target, out _, out _));
        f.Memory.OnRead = null;
        Check.True(reader.TryRead(f.Memory, f.Layout, f.Menu, target, out point, out _));
        Check.Equal(Scaled(new(720, 418.5f), f.Menu.UiScale), point.Point);
    }

    private static async Task KeyboardSubmitJourney(CancellationToken token)
    {
        foreach (float scale in new[] { 1f, 1.2f, 2.1333334f })
        foreach (bool gamepad in new[] { false, true })
        foreach (TerrariaMenuPage after in new[] { TerrariaMenuPage.PlayerSelect, TerrariaMenuPage.World, TerrariaMenuPage.Advanced })
        {
            var f = new Fixture(TerrariaMenuPage.Keyboard, scale);
            IntPtr full = f.Element(new(600, 400, 240, 37)), compact = f.Element(new(555, 320, 240, 60));
            f.KeyboardButtons(full, compact, gamepad);
            var observer = new Observer(f) { State = f.Menu with { Text = after == TerrariaMenuPage.PlayerSelect ? "1" : "12345" } };
            var input = new Input(observer)
            {
                ExpectedPoint = Scaled(gamepad ? new(720, 418.5f) : new(675, 350), scale), ImmediatePosition = true
            };
            input.OnMouse = down => { if (!down) observer.State = observer.State with { Page = after }; };
            using var menu = new ModernMenuAutomation(observer, input, (_, _) =>
                throw new InvalidOperationException("An immediately acknowledged visible submit button must not retry."));
            await menu.ClickAsync("submit player name or seed", new(TerrariaMenuButton.Submit),
                MenuExpectation.Page(TerrariaMenuPage.Keyboard, after), token);
            Check.Equal(1, input.Positions); Check.Equal(1, input.Downs); Check.Equal(1, input.Ups);
            Check.Equal(after, observer.State.Page);
        }
    }

    private static async Task MainClickJourney(CancellationToken token)
    {
        var f = new Fixture(TerrariaMenuPage.Main, 1.2f);
        var observer = new Observer(f) { State = f.Menu with { MouseReleased = false, MousePosition = PointF.Empty } };
        var input = new Input(observer);
        int polls = 0;
        using var menu = new ModernMenuAutomation(observer, input, (duration, _) =>
        {
            Check.Equal(TimeSpan.FromMilliseconds(10), duration);
            polls++;
            if (polls == 1)
            {
                Check.Equal(0, input.Positions);
                observer.State = observer.State with { MouseReleased = true };
            }
            else if (polls == 2)
            {
                Check.Equal(0, input.Positions);
                f.MainPosition(new(800, 245)); // The game has now published its real layout.
            }
            else if (polls == 3)
            {
                Check.Equal(1, input.Positions); Check.Equal(0, input.Downs);
                observer.State = observer.State with { MousePosition = new(800, 245) };
            }
            else if (polls == 4)
            {
                Check.Equal(1, input.Ups);
                observer.State = observer.State with { Page = TerrariaMenuPage.PlayerSelect, MouseReleased = true };
            }
            else throw new InvalidOperationException("A completed main-menu click must not be repeated.");
            return Task.CompletedTask;
        });
        await menu.ClickAsync("Single Player", new(TerrariaMenuButton.SinglePlayer),
            MenuExpectation.Page(TerrariaMenuPage.Main, TerrariaMenuPage.PlayerSelect), token);
        Check.Equal(4, polls); Check.Equal(1, input.Downs); Check.Equal(1, input.Ups);

        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(token);
        observer.State = f.Menu with { MousePosition = PointF.Empty };
        f.Memory.Int(f.Static("MainMenuPositions"), 0);
        using var unavailable = new ModernMenuAutomation(observer, input, (_, _) => { cancelled.Cancel(); return Task.CompletedTask; });
        await Check.ThrowsAsync<OperationCanceledException>(() => unavailable.ClickAsync("missing layout", new(TerrariaMenuButton.SinglePlayer),
            MenuExpectation.Page(TerrariaMenuPage.Main, TerrariaMenuPage.PlayerSelect), cancelled.Token));
        Check.Equal(1, input.Positions); Check.Equal(1, input.Downs); Check.Equal(1, input.Ups);
    }

    private sealed class Observer(Fixture fixture) : ITerrariaMenuObserver
    {
        public TerrariaMenuSnapshot State = fixture.Menu;
        private readonly TerrariaMenuButtonReader reader = new();
        public TerrariaMenuSnapshot Read() => State;
        public bool TryReadButton(TerrariaMenuSnapshot menu, TerrariaMenuTarget target, out TerrariaMenuButtonPosition position, out string detail) =>
            reader.TryRead(fixture.Memory, fixture.Layout, menu, target, out position, out detail);
        public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
        public void Dispose() { }
    }

    private sealed class Input(Observer observer) : IModernMenuInput
    {
        public int Positions, Downs, Ups;
        public Point ExpectedPoint = new(960, 294);
        public bool ImmediatePosition;
        public Action<bool>? OnMouse;
        public bool IsForeground => true;
        public bool Position(Point point, out string detail)
        {
            Check.Equal(ExpectedPoint, point); Positions++;
            if (ImmediatePosition) observer.State = observer.State with { MousePosition = new(point.X / observer.State.UiScale, point.Y / observer.State.UiScale) };
            detail = "game point received without a geometry formula";
            // The OS has moved the cursor, but the game has not sampled it yet.
            return true;
        }
        public bool Mouse(bool down)
        {
            Check.True(observer.State.CursorAt(ExpectedPoint));
            if (down) { if (observer.State.Page == TerrariaMenuPage.Main) Check.True(observer.State.MouseReleased); Downs++; } else Ups++;
            observer.State = observer.State with { MouseDown = down, UiMouseDown = down,
                LastDown = down ? observer.State.Hover : IntPtr.Zero, MouseReleased = false };
            OnMouse?.Invoke(down);
            return true;
        }
        public void Key(Keys key, bool down) => throw new InvalidOperationException("Main-menu click must not send keys.");
        public void UnicodeText(string text) => throw new InvalidOperationException("Main-menu click must not type text.");
    }

    private sealed class Fixture
    {
        public readonly Memory Memory = new();
        public readonly MenuAutomationLayoutDto Layout;
        public readonly TerrariaMenuSnapshot Menu;
        public readonly IntPtr Root, Ui;
        private long allocation;
        public Fixture(TerrariaMenuPage page, float scale, Size? screenSize = null, long allocationStart = 10000)
        {
            allocation = allocationStart;
            Size screen = screenSize ?? new Size(2000, 1200);
            Layout = new(MenuAutomationLayoutDto.RequiredStatics.Select((name, i) => (name, address: 1000L + i * 4)).ToDictionary(x => x.name, x => x.address),
                MenuAutomationLayoutDto.RequiredFields.Select((name, i) => (name, offset: 4 + i * 20)).ToDictionary(x => x.name, x => x.offset), new(), new int[8]);
            Root = Element(new(0, 0, 1000, 1000)); Ui = Allocate();
            SetPointer(Ui, "UI._currentState", Root);
            Memory.Int(Static("MenuUI"), PointerBits(Ui)); Memory.Int(Static("menuMode"), page == TerrariaMenuPage.Main ? 0 : 888);
            Memory.Float(Static("_uiScaleUsed"), scale); Memory.Int(Static("ScreenWidth"), screen.Width); Memory.Int(Static("ScreenHeight"), screen.Height);
            Menu = new(true, page, Root, false, 0, false, false, new(1), IntPtr.Zero,
                4200, 0, 1, 0, 0, "", "123", true, null, new Dictionary<string, int>(), new int[8], "fixture",
                UiScale: scale, ScreenSize: screen);
        }
        public IntPtr Static(string key) => new(Layout.Statics[key]);
        private IntPtr Allocate() { IntPtr address = new(allocation); allocation += 2048; return address; }
        private static int PointerBits(IntPtr value) => unchecked((int)value.ToInt64());
        public void SetPointer(IntPtr obj, string field, IntPtr value) => Memory.Int(IntPtr.Add(obj, Layout.Fields[field]), PointerBits(value));
        public IntPtr Element(RectangleF bounds)
        {
            IntPtr element = Allocate(); Bounds(element, bounds);
            SetPointer(element, "Element._snapPoint", IntPtr.Zero); Children(element);
            return element;
        }
        public void Bounds(IntPtr element, RectangleF bounds) => Memory.Set(IntPtr.Add(element, Layout.Fields["Element._dimensions"]),
            new[] { bounds.X, bounds.Y, bounds.Width, bounds.Height }.SelectMany(BitConverter.GetBytes).ToArray());
        public IntPtr Array(IntPtr[] values)
        {
            IntPtr array = Allocate(); Memory.Int(IntPtr.Add(array, 4), values.Length);
            Memory.Set(IntPtr.Add(array, 8), values.SelectMany(value => BitConverter.GetBytes(PointerBits(value))).ToArray()); return array;
        }
        public void Children(IntPtr parent, params IntPtr[] children)
        {
            IntPtr list = Allocate(); SetPointer(list, "ReferenceList._items", Array(children));
            Memory.Int(IntPtr.Add(list, Layout.Fields["ReferenceList._size"]), children.Length);
            SetPointer(parent, "Element.Elements", list);
        }
        public void Snap(IntPtr element, string name, int id)
        {
            IntPtr snap = Allocate(), text = Allocate(); Memory.Int(IntPtr.Add(text, 4), name.Length);
            Memory.Set(IntPtr.Add(text, 8), Encoding.Unicode.GetBytes(name)); SetPointer(snap, "Snap.Name", text);
            Memory.Int(IntPtr.Add(snap, Layout.Fields["Snap.<Id>k__BackingField"]), id);
            SetPointer(element, "Element._snapPoint", snap);
        }
        public void MainPosition(PointF point)
        {
            IntPtr list = Allocate(), array = Allocate(); Memory.Int(Static("MainMenuPositions"), PointerBits(list));
            Memory.Int(Static("MainMenuLastDrew"), 0); SetPointer(list, "ReferenceList._items", array);
            Memory.Int(IntPtr.Add(list, Layout.Fields["ReferenceList._size"]), 1); Memory.Int(IntPtr.Add(array, 4), 1);
            Memory.Set(IntPtr.Add(array, 8), BitConverter.GetBytes(point.X).Concat(BitConverter.GetBytes(point.Y)).ToArray());
        }
        public (IntPtr Full, IntPtr Compact) KeyboardButtons(IntPtr full, IntPtr compact, bool fullVisible)
        {
            IntPtr fullLayer = Element(new(0, 270, 540, 266)), compactLayer = Element(new(0, 270, 540, 266));
            SetPointer(Root, "Keyboard._submitButton", full); SetPointer(Root, "Keyboard._submitButton2", compact);
            SetPointer(Root, "Keyboard.outerLayer1", fullLayer); SetPointer(Root, "Keyboard.outerLayer2", compactLayer);
            Children(fullLayer, full); Children(compactLayer, compact);
            Children(Root, fullVisible ? fullLayer : compactLayer);
            return (fullLayer, compactLayer);
        }
        public void PrepareMainState()
        {
            Memory.Set(Static("gameMenu"), [1]); Memory.Set(Static("mouseLeft"), [0]); Memory.Set(Static("generatingWorld"), [0]);
            Memory.Int(Static("maxTilesX"), 4200); Memory.Int(Static("Evil"), -1);
            Memory.Set(Static("mouseLeftRelease"), [1]); Memory.Int(Static("MouseX"), 960); Memory.Int(Static("MouseY"), 294);
            IntPtr worldData = Allocate(), keyboard = Allocate();
            Memory.Int(Static("ActiveWorldFileData"), PointerBits(worldData)); Memory.Int(IntPtr.Add(worldData, Layout.Fields["WorldData.GameMode"]), 0);
            Memory.Int(Static("inputText"), PointerBits(keyboard)); Layout.Types["KeyboardState"] = 1234;
            Memory.Int(keyboard, 1234); Memory.Set(IntPtr.Add(keyboard, 4), new byte[32]);
        }
    }

    private sealed class Memory : IProcessMemoryReader
    {
        private readonly Dictionary<long, byte> data = new();
        public Action<IntPtr, int>? OnRead;
        public bool Is64Bit => false;
        public void Set(IntPtr address, byte[] values) { for (int i = 0; i < values.Length; i++) data[address.ToInt64() + i] = values[i]; }
        public void Int(IntPtr address, int value) => Set(address, BitConverter.GetBytes(value));
        public void Float(IntPtr address, float value) => Set(address, BitConverter.GetBytes(value));
        public bool TryReadBytes(IntPtr address, int count, [NotNullWhen(true)] out byte[]? bytes)
        {
            OnRead?.Invoke(address, count); bytes = null;
            if (count <= 0) return false;
            byte[] result = new byte[count];
            for (int i = 0; i < count; i++) if (!data.TryGetValue(address.ToInt64() + i, out result[i])) return false;
            bytes = result; return true;
        }
        public bool TryReadInt32(IntPtr address, out int value) { bool ok = TryReadBytes(address, 4, out var bytes); value = ok ? BitConverter.ToInt32(bytes!) : 0; return ok; }
        public bool TryReadBool(IntPtr address, out bool value) { bool ok = TryReadBytes(address, 1, out var bytes); value = ok && bytes![0] != 0; return ok; }
        public bool TryReadDouble(IntPtr address, out double value) { bool ok = TryReadBytes(address, 8, out var bytes); value = ok ? BitConverter.ToDouble(bytes!) : 0; return ok; }
        public bool TryReadPointerValue(IntPtr address, out IntPtr value) { bool ok = TryReadInt32(address, out int raw); value = new((long)unchecked((uint)raw)); return ok; }
        public bool TryReadPointer(IntPtr address, out IntPtr value) => TryReadPointerValue(address, out value);
        public IEnumerable<MemoryPage> ExecutablePages() => [];
        public IEnumerable<MemoryPage> ExecutablePrivatePages() => [];
    }
}
