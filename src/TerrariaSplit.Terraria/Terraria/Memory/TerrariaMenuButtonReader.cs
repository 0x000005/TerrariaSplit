using System.Drawing;
using TerrariaSplit.MemoryBridge.Protocol;

namespace TerrariaSplit.Terraria.Memory;

// Reads completed game layout, never configuration files or reconstructed layout formulas.
internal sealed class TerrariaMenuButtonReader
{
    private const int MaximumElements = 2048;

    public bool TryRead(IProcessMemoryReader memory, MenuAutomationLayoutDto layout, TerrariaMenuSnapshot menu,
        TerrariaMenuTarget target, out TerrariaMenuButtonPosition position, out string detail)
    {
        position = default;
        detail = $"Button {target} is not available on {menu.Page}.";
        if (memory.Is64Bit || !menu.Ready || menu.ScreenSize.Width <= 0 || menu.ScreenSize.Height <= 0 ||
            !float.IsFinite(menu.UiScale) || menu.UiScale <= 0) return false;
        IntPtr Static(string name) => new(layout.Statics.GetValueOrDefault(name));
        bool Pointer(IntPtr obj, string key, out IntPtr value)
        {
            value = IntPtr.Zero;
            return obj != IntPtr.Zero && layout.Fields.TryGetValue(key, out int offset) && offset >= 0 &&
                memory.TryReadPointerValue(IntPtr.Add(obj, offset), out value);
        }
        int? Number(IntPtr obj, string key) => obj != IntPtr.Zero && layout.Fields.TryGetValue(key, out int offset) && offset >= 0 &&
            memory.TryReadInt32(IntPtr.Add(obj, offset), out int value) ? value : null;
        string? Text(IntPtr obj, string key) => Pointer(obj, key, out IntPtr text) && text != IntPtr.Zero &&
            ManagedObjectMemoryReader.TryReadManagedString(memory, text, out string? value, 128) ? value : null;
        bool References(IntPtr list, out IntPtr[] values)
        {
            values = [];
            int? count = Number(list, "ReferenceList._size");
            if (count is null or < 0 or > MaximumElements || !Pointer(list, "ReferenceList._items", out IntPtr array) ||
                array == IntPtr.Zero || !memory.TryReadInt32(IntPtr.Add(array, 4), out int length) || length < count) return false;
            byte[]? bytes = [];
            if (count > 0 && !memory.TryReadBytes(IntPtr.Add(array, 8), count.Value * 4, out bytes)) return false;
            // Terraria pointers are unsigned x86 addresses, including the upper 2 GiB.
            values = Enumerable.Range(0, count.Value).Select(i => new IntPtr((long)BitConverter.ToUInt32(bytes!, i * 4))).ToArray();
            return values.All(p => p != IntPtr.Zero) && Number(list, "ReferenceList._size") == count &&
                Pointer(list, "ReferenceList._items", out IntPtr finalArray) && finalArray == array;
        }
        IntPtr Field(string key) => Pointer(menu.State, key, out IntPtr value) ? value : IntPtr.Zero;
        IntPtr ArrayElement(string key, int index)
        {
            IntPtr array = Field(key);
            return index >= 0 && array != IntPtr.Zero && memory.TryReadInt32(IntPtr.Add(array, 4), out int count) &&
                count is > 0 and <= MaximumElements && index < count &&
                memory.TryReadPointerValue(IntPtr.Add(array, 8 + index * 4), out IntPtr value) ? value : IntPtr.Zero;
        }
        IntPtr FindSnap(string name, int id)
        {
            var pending = new Queue<IntPtr>();
            var seen = new HashSet<IntPtr>();
            pending.Enqueue(menu.State);
            while (pending.Count > 0 && seen.Count < MaximumElements)
            {
                IntPtr element = pending.Dequeue();
                if (element == IntPtr.Zero || !seen.Add(element)) return IntPtr.Zero;
                if (!Pointer(element, "Element._snapPoint", out IntPtr snap)) return IntPtr.Zero;
                if (snap != IntPtr.Zero && Number(snap, "Snap.<Id>k__BackingField") == id && Text(snap, "Snap.Name") == name)
                    return element;
                if (!Pointer(element, "Element.Elements", out IntPtr children) || !References(children, out IntPtr[] items))
                    return IntPtr.Zero;
                foreach (IntPtr item in items) pending.Enqueue(item);
            }
            return IntPtr.Zero;
        }
        string keyboardLayout = string.Empty;
        IntPtr KeyboardSubmit()
        {
            // Both button objects survive a keyboard/mouse vs. gamepad layout change. Only the
            // currently attached outer layer is recalculated and displayed by UIVirtualKeyboard.
            if (!Pointer(menu.State, "Element.Elements", out IntPtr rootChildren) || !References(rootChildren, out IntPtr[] layers))
                return IntPtr.Zero;
            IntPtr full = Field("Keyboard.outerLayer1"), compact = Field("Keyboard.outerLayer2");
            bool fullAttached = full != IntPtr.Zero && layers.Contains(full);
            bool compactAttached = compact != IntPtr.Zero && layers.Contains(compact);
            if (fullAttached == compactAttached) return IntPtr.Zero;
            IntPtr layer = fullAttached ? full : compact;
            keyboardLayout = fullAttached ? "Keyboard._submitButton" : "Keyboard._submitButton2";
            IntPtr button = Field(keyboardLayout);
            return button != IntPtr.Zero && Pointer(layer, "Element.Elements", out IntPtr children) &&
                References(children, out IntPtr[] buttons) && buttons.Contains(button) ? button : IntPtr.Zero;
        }

        PointF uiPoint;
        string source;
        if (target.Button == TerrariaMenuButton.SinglePlayer && menu.Page == TerrariaMenuPage.Main)
        {
            // DrawMenu publishes these layout positions for the next frame's gamepad navigation.
            // They are populated even when no gamepad is connected.
            if (!memory.TryReadInt32(Static("MainMenuLastDrew"), out int drew) || drew != 0 ||
                !memory.TryReadPointerValue(Static("MainMenuPositions"), out IntPtr list)) return false;
            int? itemCount = Number(list, "ReferenceList._size");
            if (itemCount is not (> 0 and <= MaximumElements) ||
                !Pointer(list, "ReferenceList._items", out IntPtr array) || array == IntPtr.Zero ||
                !memory.TryReadInt32(IntPtr.Add(array, 4), out int count) || count < itemCount ||
                !memory.TryReadBytes(IntPtr.Add(array, 8), 8, out byte[]? bytes)) return false;
            uiPoint = new(BitConverter.ToSingle(bytes, 0), BitConverter.ToSingle(bytes, 4));
            if (!memory.TryReadPointerValue(Static("MainMenuPositions"), out IntPtr finalList) || finalList != list ||
                Number(list, "ReferenceList._size") != itemCount ||
                !Pointer(list, "ReferenceList._items", out IntPtr finalArray) || finalArray != array ||
                !memory.TryReadBytes(IntPtr.Add(array, 8), 8, out byte[]? after) || !bytes.SequenceEqual(after)) return false;
            source = "DrawMenu.MenuItemPositions[0]";
        }
        else
        {
            IntPtr LocateElement() => (menu.Page, target.Button) switch
            {
                (TerrariaMenuPage.PlayerSelect, TerrariaMenuButton.NewPlayer) => Field("PlayerSelect._newPanel"),
                (TerrariaMenuPage.WorldSelect, TerrariaMenuButton.NewWorld) => Field("WorldSelect._newPanel"),
                (TerrariaMenuPage.Character, TerrariaMenuButton.CharacterInfo) => Field("Character._charInfoCategoryButton"),
                (TerrariaMenuPage.Character, TerrariaMenuButton.CharacterClothing) => Field("Character._clothingStylesCategoryButton"),
                (TerrariaMenuPage.Character, TerrariaMenuButton.PasteTemplate) => Field("Character._pasteTemplateButton"),
                (TerrariaMenuPage.Character, TerrariaMenuButton.PlayerDifficulty) when target.Index is >= 0 and <= 3 =>
                    FindSnap("Middle", target.Index == 3 ? 1 : target.Index + 2),
                (TerrariaMenuPage.Character, TerrariaMenuButton.CreatePlayer) => FindSnap("Create", 0),
                (TerrariaMenuPage.Keyboard, TerrariaMenuButton.Submit) => KeyboardSubmit(),
                (TerrariaMenuPage.PlayerSelect, TerrariaMenuButton.PlayPlayer) => FindSnap("Play", target.Index),
                (TerrariaMenuPage.World, TerrariaMenuButton.WorldSize) => ArrayElement("World._sizeButtons", target.Index - 1),
                (TerrariaMenuPage.World, TerrariaMenuButton.WorldDifficulty) when target.Index is >= 0 and <= 3 =>
                    ArrayElement("World._difficultyButtons", target.Index == 3 ? 0 : target.Index + 1),
                (TerrariaMenuPage.World, TerrariaMenuButton.WorldEvil) => ArrayElement("World._evilButtons", target.Index + 1),
                (TerrariaMenuPage.World, TerrariaMenuButton.WorldSeed) => Field("World._seedPlate"),
                (TerrariaMenuPage.World, TerrariaMenuButton.AdvancedMenu) => Field("World._advancedSeedButton"),
                (TerrariaMenuPage.Advanced, TerrariaMenuButton.RandomSeed) => Field("Advanced._randomButton"),
                (TerrariaMenuPage.Advanced, TerrariaMenuButton.AdvancedSeed) => Field("Advanced._seedPlate"),
                (TerrariaMenuPage.Advanced, TerrariaMenuButton.SpecialSeed) => ArrayElement("Advanced._seedButtons", target.Index),
                (TerrariaMenuPage.Advanced, TerrariaMenuButton.ApplySeed) => Field("Advanced._backButton"),
                (TerrariaMenuPage.World, TerrariaMenuButton.CreateWorld) => FindSnap("Create", 0),
                _ => IntPtr.Zero
            };
            IntPtr element = LocateElement();
            if (element == IntPtr.Zero || !layout.Fields.TryGetValue("Element._dimensions", out int offset) || offset < 0 ||
                !memory.TryReadBytes(IntPtr.Add(element, offset), 16, out byte[]? bounds)) return false;
            float x = BitConverter.ToSingle(bounds, 0), y = BitConverter.ToSingle(bounds, 4);
            float width = BitConverter.ToSingle(bounds, 8), height = BitConverter.ToSingle(bounds, 12);
            if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0)
            { detail = $"Button {target} has no completed game layout."; return false; }
            uiPoint = new(x + width / 2, y + height / 2);
            source = $"{keyboardLayout} UIElement._dimensions@0x{element.ToInt64():X}; bounds={x},{y},{width},{height}";
            if (!memory.TryReadBytes(IntPtr.Add(element, offset), 16, out byte[]? after) || !bounds.SequenceEqual(after))
            { detail = "Button layout changed while reading."; return false; }
            if (LocateElement() != element)
            { detail = "Button object changed while reading."; return false; }
        }
        if (!float.IsFinite(uiPoint.X) || !float.IsFinite(uiPoint.Y)) return false;
        double px = uiPoint.X * menu.UiScale, py = uiPoint.Y * menu.UiScale;
        if (px < 0 || py < 0 || px >= menu.ScreenSize.Width || py >= menu.ScreenSize.Height)
        { detail = $"Button {target} is outside the game screen {menu.ScreenSize}; {source}; uiPoint={uiPoint}; uiScale={menu.UiScale}; point={px},{py}."; return false; }
        if (!memory.TryReadInt32(Static("menuMode"), out int mode) || (menu.Page == TerrariaMenuPage.Main ? mode != 0 : mode != 888) ||
            !memory.TryReadPointerValue(Static("MenuUI"), out IntPtr currentUi) ||
            !Pointer(currentUi, "UI._currentState", out IntPtr currentState) || currentState != menu.State ||
            !memory.TryReadInt32(Static("_uiScaleUsed"), out int scaleBits) || BitConverter.Int32BitsToSingle(scaleBits) != menu.UiScale ||
            !memory.TryReadInt32(Static("ScreenWidth"), out int screenWidth) || screenWidth != menu.ScreenSize.Width ||
            !memory.TryReadInt32(Static("ScreenHeight"), out int screenHeight) || screenHeight != menu.ScreenSize.Height)
        { detail = "Menu, game dimensions, or scale changed while reading the button."; return false; }
        Point point = new((int)Math.Round(px), (int)Math.Round(py));
        if (point.X >= menu.ScreenSize.Width || point.Y >= menu.ScreenSize.Height) return false;
        position = new(point, $"{source}; uiPoint={uiPoint}; uiScale={menu.UiScale}; gameScreen={menu.ScreenSize}; point={point}");
        detail = position.Detail;
        return true;
    }
}
