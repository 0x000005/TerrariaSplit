namespace TerrariaSplit;

internal sealed class TerrariaWorldCreationSeedReader
{
    private const int X86PointerSize = 4;
    private const int X86UserInterfaceCurrentStateFieldOffset = 0x1C;
    private const int X86UiStateNestedReferenceScanStart = 0x8;
    private const int X86UiStateNestedReferenceScanEnd = 0x300;
    private const int X86WorldCreationAdvancedCreationStateFieldOffset = 0xF0;
    private const int X86WorldCreationAdvancedSeedPlateFieldOffset = 0xFC;
    private const int X86WorldNameFieldOffset = 0xF0;
    private const int X86SeedFieldOffset = 0xF4;
    private const int X86NamePlateFieldOffset = 0x10C;
    private const int X86SeedPlateFieldOffset = 0x110;
    private const int X86CharacterNameButtonActualContentsOffset = 0xFC;
    private const int MenuUiSetNullInlineAddressOffset = 23;
    private const int MaxWorldNameLength = 128;
    private const int MaxSeedTextLength = 128;
    private static readonly TimeSpan StaticSlotScanInterval = TimeSpan.FromSeconds(1);
    private static readonly SignaturePattern MenuUiSetNullSignature = SignaturePattern.Parse(
        "80 3D ???????? 00 74 0C 81 3D ???????? 78 03 00 00 74 10 8B 0D ???????? 33 D2 39 09 FF 15");

    private IntPtr menuUiSlotAddress;
    private DateTime nextStaticSlotScanUtc = DateTime.MinValue;
    private TerrariaWorldCreationSeedSnapshot lastSnapshot = TerrariaWorldCreationSeedSnapshot.Unknown;

    public TerrariaWorldCreationSeedSnapshot Read(IProcessMemoryReader memory)
    {
        if (memory.Is64Bit)
        {
            Reset();
            lastSnapshot = TerrariaWorldCreationSeedSnapshot.Unknown;
            return lastSnapshot;
        }

        if (menuUiSlotAddress == IntPtr.Zero)
        {
            DateTime now = DateTime.UtcNow;
            if (now < nextStaticSlotScanUtc)
            {
                return lastSnapshot;
            }

            nextStaticSlotScanUtc = now + StaticSlotScanInterval;
            if (!TryResolveMenuUiSlot(memory, out menuUiSlotAddress))
            {
                lastSnapshot = TerrariaWorldCreationSeedSnapshot.Unknown;
                return lastSnapshot;
            }
        }

        if (!TryReadMenuUiObject(memory, menuUiSlotAddress, out IntPtr menuUiObjectAddress))
        {
            Reset();
            lastSnapshot = TerrariaWorldCreationSeedSnapshot.Unknown;
            return lastSnapshot;
        }

        if (TryReadFromMenuUiObject(memory, menuUiObjectAddress, out TerrariaWorldCreationSeedSnapshot snapshot))
        {
            lastSnapshot = snapshot;
        }
        else
        {
            lastSnapshot = TerrariaWorldCreationSeedSnapshot.NotOnWorldCreationPage;
        }

        return lastSnapshot;
    }

    public void Reset()
    {
        menuUiSlotAddress = IntPtr.Zero;
        nextStaticSlotScanUtc = DateTime.MinValue;
        lastSnapshot = TerrariaWorldCreationSeedSnapshot.Unknown;
    }

    private static bool TryResolveMenuUiSlot(IProcessMemoryReader memory, out IntPtr resolvedMenuUiSlotAddress)
    {
        resolvedMenuUiSlotAddress = IntPtr.Zero;
        IntPtr anchorAddress = SignatureScanner.Scan(
            memory,
            MenuUiSetNullSignature,
            "MenuUI SetState(null) anchor",
            out _);
        if (anchorAddress == IntPtr.Zero)
        {
            return false;
        }

        IntPtr inlineAddressLocation = IntPtr.Add(anchorAddress, MenuUiSetNullInlineAddressOffset);
        if (!memory.TryReadPointer(inlineAddressLocation, out IntPtr candidateSlotAddress) ||
            !TryReadMenuUiObject(memory, candidateSlotAddress, out _))
        {
            return false;
        }

        resolvedMenuUiSlotAddress = candidateSlotAddress;
        return true;
    }

    private static bool TryReadMenuUiObject(
        IProcessMemoryReader memory,
        IntPtr menuUiSlotAddress,
        out IntPtr menuUiObjectAddress)
    {
        return memory.TryReadPointerValue(menuUiSlotAddress, out menuUiObjectAddress) &&
            LooksLikeManagedPointer(menuUiObjectAddress);
    }

    private static bool TryReadFromMenuUiObject(
        IProcessMemoryReader memory,
        IntPtr menuUiObjectAddress,
        out TerrariaWorldCreationSeedSnapshot snapshot)
    {
        snapshot = TerrariaWorldCreationSeedSnapshot.NotOnWorldCreationPage;
        if (memory.TryReadPointerValue(
                IntPtr.Add(menuUiObjectAddress, X86UserInterfaceCurrentStateFieldOffset),
                out IntPtr currentStateObjectAddress) &&
            LooksLikeManagedPointer(currentStateObjectAddress) &&
            TryReadFromUiState(memory, currentStateObjectAddress, out snapshot))
        {
            return true;
        }

        return false;
    }

    private static bool TryReadFromUiState(
        IProcessMemoryReader memory,
        IntPtr stateObjectAddress,
        out TerrariaWorldCreationSeedSnapshot snapshot)
    {
        if (TryReadKnownWorldCreationState(memory, stateObjectAddress, out snapshot))
        {
            return true;
        }

        for (int offset = X86UiStateNestedReferenceScanStart;
             offset <= X86UiStateNestedReferenceScanEnd;
             offset += X86PointerSize)
        {
            if (!memory.TryReadPointerValue(IntPtr.Add(stateObjectAddress, offset), out IntPtr candidateWorldCreationAddress) ||
                !LooksLikeManagedPointer(candidateWorldCreationAddress) ||
                !TryReadWorldCreationSeed(memory, candidateWorldCreationAddress, out snapshot, out IntPtr seedObjectAddress) ||
                !StateContainsSeedPlate(memory, stateObjectAddress, seedObjectAddress))
            {
                continue;
            }

            return true;
        }

        snapshot = TerrariaWorldCreationSeedSnapshot.NotOnWorldCreationPage;
        return false;
    }

    private static bool TryReadKnownWorldCreationState(
        IProcessMemoryReader memory,
        IntPtr stateObjectAddress,
        out TerrariaWorldCreationSeedSnapshot snapshot)
    {
        if (TryReadWorldCreationSeed(memory, stateObjectAddress, out snapshot, out _))
        {
            return true;
        }

        if (memory.TryReadPointerValue(
            IntPtr.Add(stateObjectAddress, X86WorldCreationAdvancedCreationStateFieldOffset),
            out IntPtr advancedCreationStateAddress) &&
            LooksLikeManagedPointer(advancedCreationStateAddress) &&
            TryReadWorldCreationSeed(memory, advancedCreationStateAddress, out snapshot, out _))
        {
            return true;
        }

        if (TryReadAdvancedWorldCreationSeed(memory, stateObjectAddress, out snapshot))
        {
            return true;
        }

        snapshot = TerrariaWorldCreationSeedSnapshot.NotOnWorldCreationPage;
        return false;
    }

    private static bool TryReadAdvancedWorldCreationSeed(
        IProcessMemoryReader memory,
        IntPtr advancedStateObjectAddress,
        out TerrariaWorldCreationSeedSnapshot snapshot)
    {
        snapshot = TerrariaWorldCreationSeedSnapshot.NotOnWorldCreationPage;
        if (!memory.TryReadPointerValue(
                IntPtr.Add(advancedStateObjectAddress, X86WorldCreationAdvancedCreationStateFieldOffset),
                out IntPtr creationStateAddress) ||
            !LooksLikeManagedPointer(creationStateAddress) ||
            !memory.TryReadPointerValue(
                IntPtr.Add(advancedStateObjectAddress, X86WorldCreationAdvancedSeedPlateFieldOffset),
                out IntPtr seedPlateAddress) ||
            !LooksLikeManagedPointer(seedPlateAddress))
        {
            return false;
        }

        return TryReadWorldCreationSeedFromSeedPlate(
            memory,
            creationStateAddress,
            seedPlateAddress,
            out snapshot,
            out _);
    }

    private static bool TryReadWorldCreationSeed(
        IProcessMemoryReader memory,
        IntPtr worldCreationAddress,
        out TerrariaWorldCreationSeedSnapshot snapshot,
        out IntPtr seedObjectAddress)
    {
        snapshot = TerrariaWorldCreationSeedSnapshot.NotOnWorldCreationPage;
        seedObjectAddress = IntPtr.Zero;
        if (worldCreationAddress == IntPtr.Zero || memory.Is64Bit)
        {
            return false;
        }

        if (!memory.TryReadPointerValue(IntPtr.Add(worldCreationAddress, X86WorldNameFieldOffset), out IntPtr worldNameObject) ||
            !memory.TryReadPointerValue(IntPtr.Add(worldCreationAddress, X86SeedFieldOffset), out seedObjectAddress) ||
            !memory.TryReadPointerValue(IntPtr.Add(worldCreationAddress, X86NamePlateFieldOffset), out IntPtr namePlateObject) ||
            !memory.TryReadPointerValue(IntPtr.Add(worldCreationAddress, X86SeedPlateFieldOffset), out IntPtr seedPlateObject) ||
            !LooksLikeManagedPointer(worldNameObject) ||
            !LooksLikeManagedPointer(seedObjectAddress) ||
            !LooksLikeManagedPointer(namePlateObject) ||
            !LooksLikeManagedPointer(seedPlateObject) ||
            !TryReadActualContentsPointer(memory, namePlateObject, out IntPtr namePlateContents) ||
            !TryReadActualContentsPointer(memory, seedPlateObject, out IntPtr seedPlateContents) ||
            namePlateContents != worldNameObject ||
            seedPlateContents != seedObjectAddress)
        {
            return false;
        }

        if (!ManagedObjectMemoryReader.TryReadManagedString(memory, worldNameObject, out string? worldName) ||
            !IsValidWorldName(worldName) ||
            !ManagedObjectMemoryReader.TryReadManagedString(memory, seedObjectAddress, out string? seedText) ||
            seedText is null ||
            !IsValidSeedText(seedText))
        {
            return false;
        }

        snapshot = seedText.Length == 0
            ? TerrariaWorldCreationSeedSnapshot.EmptySeed(worldCreationAddress)
            : TerrariaWorldCreationSeedSnapshot.FromSeed(seedText, worldCreationAddress);
        return true;
    }

    private static bool TryReadWorldCreationSeedFromSeedPlate(
        IProcessMemoryReader memory,
        IntPtr worldCreationAddress,
        IntPtr seedPlateAddress,
        out TerrariaWorldCreationSeedSnapshot snapshot,
        out IntPtr seedObjectAddress)
    {
        snapshot = TerrariaWorldCreationSeedSnapshot.NotOnWorldCreationPage;
        seedObjectAddress = IntPtr.Zero;
        if (worldCreationAddress == IntPtr.Zero ||
            seedPlateAddress == IntPtr.Zero ||
            memory.Is64Bit)
        {
            return false;
        }

        if (!memory.TryReadPointerValue(IntPtr.Add(worldCreationAddress, X86WorldNameFieldOffset), out IntPtr worldNameObject) ||
            !memory.TryReadPointerValue(IntPtr.Add(worldCreationAddress, X86SeedFieldOffset), out seedObjectAddress) ||
            !LooksLikeManagedPointer(worldNameObject) ||
            !LooksLikeManagedPointer(seedObjectAddress) ||
            !TryReadActualContentsPointer(memory, seedPlateAddress, out IntPtr seedPlateContents) ||
            seedPlateContents != seedObjectAddress)
        {
            return false;
        }

        if (!ManagedObjectMemoryReader.TryReadManagedString(memory, worldNameObject, out string? worldName) ||
            !IsValidWorldName(worldName) ||
            !ManagedObjectMemoryReader.TryReadManagedString(memory, seedObjectAddress, out string? seedText) ||
            seedText is null ||
            !IsValidSeedText(seedText))
        {
            return false;
        }

        snapshot = seedText.Length == 0
            ? TerrariaWorldCreationSeedSnapshot.EmptySeed(worldCreationAddress)
            : TerrariaWorldCreationSeedSnapshot.FromSeed(seedText, worldCreationAddress);
        return true;
    }

    private static bool StateContainsSeedPlate(
        IProcessMemoryReader memory,
        IntPtr stateObjectAddress,
        IntPtr seedObjectAddress)
    {
        for (int offset = X86UiStateNestedReferenceScanStart;
             offset <= X86UiStateNestedReferenceScanEnd;
             offset += X86PointerSize)
        {
            if (memory.TryReadPointerValue(IntPtr.Add(stateObjectAddress, offset), out IntPtr candidatePlateAddress) &&
                LooksLikeManagedPointer(candidatePlateAddress) &&
                TryReadActualContentsPointer(memory, candidatePlateAddress, out IntPtr actualContents) &&
                actualContents == seedObjectAddress)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadActualContentsPointer(
        IProcessMemoryReader memory,
        IntPtr uiCharacterNameButtonObject,
        out IntPtr actualContents)
    {
        return memory.TryReadPointerValue(
            IntPtr.Add(uiCharacterNameButtonObject, X86CharacterNameButtonActualContentsOffset),
            out actualContents);
    }

    private static bool LooksLikeManagedPointer(IntPtr pointer)
    {
        long value = pointer.ToInt64();
        return value >= 0x10000L && value <= 0xFFFF0000L && (value & 0x3L) == 0;
    }

    private static bool IsValidWorldName(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.Length <= MaxWorldNameLength &&
            !ContainsInvalidControlCharacter(value);
    }

    private static bool IsValidSeedText(string value)
    {
        return value.Length <= MaxSeedTextLength &&
            !ContainsInvalidControlCharacter(value);
    }

    private static bool ContainsInvalidControlCharacter(string value)
    {
        foreach (char c in value)
        {
            if (char.IsControl(c))
            {
                return true;
            }
        }

        return false;
    }
}
