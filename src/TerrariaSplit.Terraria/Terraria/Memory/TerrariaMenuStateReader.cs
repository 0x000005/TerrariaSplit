using TerrariaSplit.MemoryBridge.Protocol;
using System.Drawing;

namespace TerrariaSplit.Terraria.Memory;

internal enum TerrariaMenuPage { Unknown, Main, PlayerSelect, Character, Keyboard, WorldSelect, World, Advanced, Generating }

internal sealed record TerrariaMenuSnapshot(
    bool Valid, TerrariaMenuPage Page, IntPtr State, bool LayoutDirty, double ClickDisabledMs,
    bool MouseDown, bool UiMouseDown, IntPtr Hover, IntPtr LastDown,
    int WorldWidth, int GameMode, int Evil, int? PlayerDifficulty, int? CharacterCategory,
    string? Text, string? Seed, bool CanSubmit, bool[]? SpecialSeeds,
    IReadOnlyDictionary<string, int> PlayerValues, int[] KeyboardWords, string Detail, PointF? MousePosition = null, float UiScale = 1f,
    int? TextMaximumLength = null, IReadOnlyList<string>? SecretSeeds = null, string? SpecialSeedLayoutError = null,
    string? ActivePlayerPath = null)
{
    public bool Ready => Valid && Page != TerrariaMenuPage.Unknown && !LayoutDirty && ClickDisabledMs <= 0 &&
        !MouseDown && (!UsesMenuUi || !UiMouseDown);
    public bool UsesMenuUi => Page is not (TerrariaMenuPage.Main or TerrariaMenuPage.Generating);
    public bool KeyDown(int key) => KeyboardWords.Length == 8 && (KeyboardWords[key / 32] & (1 << (key % 32))) != 0;
    // UserInterface caches Main.MouseScreen after PlayerInput.SetZoom_UI, in UI units.
    public bool CursorAt(Point clientPoint) => MousePosition is { } position && UiScale > 0 &&
        Math.Abs(position.X - clientPoint.X / UiScale) <= 1 && Math.Abs(position.Y - clientPoint.Y / UiScale) <= 1;
    public static TerrariaMenuSnapshot Unknown(string detail) => new(false, TerrariaMenuPage.Unknown, IntPtr.Zero, false, 0,
        false, false, IntPtr.Zero, IntPtr.Zero, 0, 0, 0, null, null, null, null, false, null,
        new Dictionary<string, int>(), [], detail);
}

internal sealed class TerrariaMenuStateReader
{
    // A copied seed can contain many secret-seed names, unlike short monitor/status strings.
    internal const int MaximumMenuTextLength = 16 * 1024;
    private const int SpecialSeedOptionCount = 10;
    private const int MaximumListLength = 1024;

    public TerrariaMenuSnapshot Read(IProcessMemoryReader memory, MenuAutomationLayoutDto layout)
    {
        IntPtr Address(string name) => new(layout.Statics.GetValueOrDefault(name));
        bool Field(IntPtr obj, string key, out IntPtr address)
        {
            address = IntPtr.Zero;
            if (obj == IntPtr.Zero || !layout.Fields.TryGetValue(key, out int offset) || offset < 0) return false;
            address = IntPtr.Add(obj, offset); return true;
        }
        bool Pointer(IntPtr obj, string key, out IntPtr value)
        {
            value = IntPtr.Zero;
            return Field(obj, key, out IntPtr at) && memory.TryReadPointerValue(at, out value);
        }
        int? Number(IntPtr obj, string key) => Field(obj, key, out IntPtr at) && memory.TryReadInt32(at, out int v) ? v : null;
        string? Text(IntPtr obj, string key) => Pointer(obj, key, out IntPtr value) && value != IntPtr.Zero &&
            ManagedObjectMemoryReader.TryReadManagedString(memory, value, out string? s, MaximumMenuTextLength) ? s : null;
        bool Bool(IntPtr obj, string key, out bool value)
        {
            value = false; return Field(obj, key, out IntPtr at) && memory.TryReadBool(at, out value);
        }
        IntPtr[]? ReferenceList(IntPtr list)
        {
            int? count = Number(list, "ReferenceList._size");
            if (count is null or < 0 or > MaximumListLength || !Pointer(list, "ReferenceList._items", out IntPtr items) ||
                items == IntPtr.Zero || !memory.TryReadInt32(IntPtr.Add(items, 4), out int capacity) || capacity < count)
                return null;
            var values = new IntPtr[count.Value];
            for (int i = 0; i < values.Length; i++)
                if (!memory.TryReadPointerValue(IntPtr.Add(items, 8 + i * 4), out values[i]) || values[i] == IntPtr.Zero) return null;
            return Number(list, "ReferenceList._size") == count &&
                Pointer(list, "ReferenceList._items", out IntPtr finalItems) && finalItems == items ? values : null;
        }
        if (memory.Is64Bit || !memory.TryReadInt32(Address("menuMode"), out int mode) ||
            !memory.TryReadBool(Address("mouseLeft"), out bool down) ||
            !memory.TryReadInt32(Address("maxTilesX"), out int width) ||
            !memory.TryReadInt32(Address("Evil"), out int evil)) return TerrariaMenuSnapshot.Unknown("Menu static fields unreadable.");
        if (!memory.TryReadBool(Address("gameMenu"), out bool gameMenu) || !gameMenu)
            return TerrariaMenuSnapshot.Unknown("Terraria is not in a game menu.");
        if (!memory.TryReadPointerValue(Address("ActiveWorldFileData"), out IntPtr worldData) ||
            Number(worldData, "WorldData.GameMode") is not { } gameMode) return TerrariaMenuSnapshot.Unknown("World mode unreadable.");
        if (!memory.TryReadPointerValue(Address("MenuUI"), out IntPtr ui) ||
            !Pointer(ui, "UI._currentState", out IntPtr state)) return TerrariaMenuSnapshot.Unknown("MenuUI unavailable.");
        if (!memory.TryReadBool(Address("generatingWorld"), out bool generating))
            return TerrariaMenuSnapshot.Unknown("World generation state unreadable.");
        TerrariaMenuPage page = generating ? TerrariaMenuPage.Generating : mode == 0 ? TerrariaMenuPage.Main : TerrariaMenuPage.Unknown;
        if (!generating && mode == 888 && state != IntPtr.Zero && memory.TryReadPointerValue(state, out IntPtr methodTable))
        {
            foreach (TerrariaMenuPage candidate in new[] { TerrariaMenuPage.PlayerSelect, TerrariaMenuPage.Character,
                TerrariaMenuPage.Keyboard, TerrariaMenuPage.WorldSelect, TerrariaMenuPage.World, TerrariaMenuPage.Advanced })
                if (layout.Types.GetValueOrDefault(candidate.ToString()) == methodTable.ToInt64()) { page = candidate; break; }
        }
        bool dirty = false, wasDown = false, canSubmit = false;
        double disabled = 0;
        IntPtr hover = IntPtr.Zero, lastDown = IntPtr.Zero;
        PointF? mousePosition = null;
        float uiScale = 1f;
        if (page is not (TerrariaMenuPage.Main or TerrariaMenuPage.Generating))
        {
            if (!memory.TryReadInt32(Address("_uiScaleUsed"), out int scaleBits) ||
                !float.IsFinite(uiScale = BitConverter.Int32BitsToSingle(scaleBits)) || uiScale <= 0)
                return TerrariaMenuSnapshot.Unknown("UI scale unreadable or invalid.");
            if (!Bool(ui, "UI._isStateDirty", out dirty) || !Field(ui, "UI._clickDisabledTimeRemaining", out IntPtr timer) ||
                !memory.TryReadDouble(timer, out disabled) || !double.IsFinite(disabled) ||
                !Pointer(ui, "UI._lastElementHover", out hover) || !Pointer(ui, "UI.LeftMouse", out IntPtr pointer) ||
                !Bool(pointer, "Pointer.WasDown", out wasDown) || !Pointer(pointer, "Pointer.LastDown", out lastDown))
                return TerrariaMenuSnapshot.Unknown("Menu input fields unreadable.");
            if (!Field(ui, "UI.MousePosition", out IntPtr mouseAt) || !memory.TryReadBytes(mouseAt, 8, out byte[]? mouseBytes))
                return TerrariaMenuSnapshot.Unknown("UI cursor position unreadable.");
            mousePosition = new PointF(BitConverter.ToSingle(mouseBytes, 0), BitConverter.ToSingle(mouseBytes, 4));
            if (!float.IsFinite(mousePosition.Value.X) || !float.IsFinite(mousePosition.Value.Y))
                return TerrariaMenuSnapshot.Unknown("UI cursor position invalid.");
        }
        IntPtr creation = page == TerrariaMenuPage.World ? state : IntPtr.Zero;
        if (page == TerrariaMenuPage.Advanced) Pointer(state, "Advanced._creationState", out creation);
        string? seed = creation != IntPtr.Zero ? Text(creation, "World._optionSeed") : null;
        string? text = null;
        int? textMaximumLength = null;
        if (page == TerrariaMenuPage.Keyboard)
        {
            if (!Pointer(state, "Keyboard._textBox", out IntPtr box) || (text = Text(box, "TextBox._text")) is null ||
                !Bool(state, "Keyboard._canSubmit", out canSubmit) ||
                (textMaximumLength = Number(box, "TextBox._maxLength")) is null or < 0)
                return TerrariaMenuSnapshot.Unknown("Keyboard content or TextBox._maxLength unreadable.");
        }
        int? difficulty = null, category = null;
        var playerValues = new Dictionary<string, int>();
        if (page == TerrariaMenuPage.Character)
        {
            if (!Pointer(state, "Character._player", out IntPtr player) ||
                !Field(player, "Player.difficulty", out IntPtr difficultyAt) || !memory.TryReadBytes(difficultyAt, 1, out byte[]? bytes))
                return TerrariaMenuSnapshot.Unknown("Creation player unreadable.");
            difficulty = bytes[0]; category = Number(state, "Character._selectedPicker");
            foreach (string name in new[] { "hair", "skinVariant", "voiceVariant", "voicePitchOffset", "hairColor", "skinColor", "shirtColor", "underShirtColor", "pantsColor", "shoeColor", "eyeColor" })
                if (Number(player, "Player." + name) is { } value) playerValues[name] = value;
        }
        bool[]? seeds = null;
        string? specialSeedLayoutError = null;
        IReadOnlyList<string>? secretSeeds = null;
        if (page is TerrariaMenuPage.World or TerrariaMenuPage.Advanced)
        {
            string[] requiredFields = ["ReferenceList._items", "ReferenceList._size", "Option._enabled", "World._secretSeedTextsEntered"];
            string[] missingFields = requiredFields.Where(key => !layout.Fields.TryGetValue(key, out int offset) || offset < 0).ToArray();
            if (missingFields.Length > 0 || Address("WorldOptions") == IntPtr.Zero ||
                Enumerable.Range(0, SpecialSeedOptionCount).Any(i => layout.Types.GetValueOrDefault("SeedOption." + i) == 0))
                specialSeedLayoutError = "Special seed layout unavailable: WorldOptions, SeedOption types, or fields " + string.Join(", ", missingFields);
            else if (memory.TryReadPointerValue(Address("WorldOptions"), out IntPtr optionsRoot) &&
                ReferenceList(optionsRoot) is { Length: SpecialSeedOptionCount } options)
            {
                seeds = new bool[SpecialSeedOptionCount];
                for (int i = 0; i < seeds.Length; i++)
                    if (!memory.TryReadPointerValue(options[i], out IntPtr optionType) ||
                        optionType.ToInt64() != layout.Types["SeedOption." + i] || !Bool(options[i], "Option._enabled", out seeds[i]))
                    { seeds = null; break; }
                if (!memory.TryReadPointerValue(Address("WorldOptions"), out IntPtr finalOptions) || finalOptions != optionsRoot) seeds = null;
            }
            if (Pointer(creation, "World._secretSeedTextsEntered", out IntPtr secretList) && ReferenceList(secretList) is { } entries)
            {
                var names = new List<string>(entries.Length);
                foreach (IntPtr entry in entries)
                    if (ManagedObjectMemoryReader.TryReadManagedString(memory, entry, out string? name, MaximumMenuTextLength)) names.Add(name!);
                    else break;
                if (names.Count == entries.Length && Pointer(creation, "World._secretSeedTextsEntered", out IntPtr finalSecretList) &&
                    finalSecretList == secretList) secretSeeds = names;
            }
        }
        string? activePlayerPath = page == TerrariaMenuPage.WorldSelect &&
            memory.TryReadPointerValue(Address("ActivePlayerFileData"), out IntPtr activePlayer) ? Text(activePlayer, "FileData._path") : null;
        int[] keys = new int[layout.KeyboardWordOffsets.Length];
        if (keys.Length != 8) return TerrariaMenuSnapshot.Unknown("Keyboard-state layout unavailable.");
        // CLR static structs are boxed. Resolve the slot on every sample; GC can move the box.
        if (!memory.TryReadPointerValue(Address("inputText"), out IntPtr keyboard) || keyboard == IntPtr.Zero ||
            !memory.TryReadPointerValue(keyboard, out IntPtr keyboardType) ||
            keyboardType.ToInt64() != layout.Types.GetValueOrDefault("KeyboardState"))
            return TerrariaMenuSnapshot.Unknown("Keyboard-state object unavailable.");
        for (int i = 0; i < keys.Length; i++)
            if (layout.KeyboardWordOffsets[i] < 0 ||
                !memory.TryReadInt32(IntPtr.Add(keyboard, 4 + layout.KeyboardWordOffsets[i]), out keys[i]))
                return TerrariaMenuSnapshot.Unknown("Keyboard-state fields unreadable.");
        if (!Pointer(ui, "UI._currentState", out IntPtr finalState) || finalState != state ||
            !memory.TryReadInt32(Address("menuMode"), out int finalMode) || finalMode != mode ||
            !memory.TryReadPointerValue(Address("inputText"), out IntPtr finalKeyboard) || finalKeyboard != keyboard ||
            !memory.TryReadPointerValue(Address("MenuUI"), out IntPtr finalUi) || finalUi != ui ||
            page is not (TerrariaMenuPage.Main or TerrariaMenuPage.Generating) &&
                (!Pointer(ui, "UI._lastElementHover", out IntPtr finalHover) || finalHover != hover))
            return TerrariaMenuSnapshot.Unknown("Menu changed while reading.");
        return new(true, page, state, dirty, disabled, down, wasDown, hover, lastDown, width, gameMode, evil,
            difficulty, category, text, seed, canSubmit, seeds, playerValues, keys,
            $"mode={mode}; page={page}; state=0x{state.ToInt64():X}; dirty={dirty}; disabledMs={disabled:F0}; mouse={down}/{wasDown}; hover=0x{hover.ToInt64():X}; lastDown=0x{lastDown.ToInt64():X}; cursor={mousePosition}; uiScale={uiScale}; width={width}; gameMode={gameMode}; evil={evil}; category={category}; playerDifficulty={difficulty}; canSubmit={canSubmit}; input={text}; maxInput={textMaximumLength}; seed={seed}; specials={(seeds is null ? "unreadable" : string.Join(",", seeds))}; secrets={(secretSeeds is null ? "unreadable" : string.Join("|", secretSeeds))}; layoutError={specialSeedLayoutError}; activePlayer={activePlayerPath}",
            mousePosition, uiScale, textMaximumLength, secretSeeds, specialSeedLayoutError, activePlayerPath);
    }
}
