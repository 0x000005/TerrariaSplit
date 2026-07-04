namespace TerrariaSplit.Terraria.Memory;

internal sealed class TerrariaWorldCreationSeedReader
{
    private const int MaxWorldNameLength = 128;
    private const int MaxSeedTextLength = 128;

    private TerrariaWorldCreationSeedSnapshot lastSnapshot = TerrariaWorldCreationSeedSnapshot.Unknown;

    public TerrariaWorldCreationSeedSnapshot Read(IProcessMemoryReader memory)
    {
        _ = memory;
        Reset();
        return lastSnapshot;
    }

    public TerrariaWorldCreationSeedSnapshot Read(
        IProcessMemoryReader memory,
        TerrariaWorldCreationSeedMemoryLayout? layout)
    {
        if (memory.Is64Bit || layout is null)
        {
            Reset();
            return lastSnapshot;
        }

        if (!TryReadMenuUiObject(memory, layout, out IntPtr menuUiObjectAddress))
        {
            Reset();
            return lastSnapshot;
        }

        lastSnapshot = TryReadFromMenuUiObject(memory, layout, menuUiObjectAddress, out TerrariaWorldCreationSeedSnapshot snapshot)
            ? snapshot
            : TerrariaWorldCreationSeedSnapshot.NotOnWorldCreationPage;
        return lastSnapshot;
    }

    public void Reset()
    {
        lastSnapshot = TerrariaWorldCreationSeedSnapshot.Unknown;
    }

    private static bool TryReadMenuUiObject(
        IProcessMemoryReader memory,
        TerrariaWorldCreationSeedMemoryLayout layout,
        out IntPtr menuUiObjectAddress)
    {
        return memory.TryReadPointerValue(layout.MenuUiStaticFieldAddress, out menuUiObjectAddress) &&
            LooksLikeManagedPointer(menuUiObjectAddress);
    }

    private static bool TryReadFromMenuUiObject(
        IProcessMemoryReader memory,
        TerrariaWorldCreationSeedMemoryLayout layout,
        IntPtr menuUiObjectAddress,
        out TerrariaWorldCreationSeedSnapshot snapshot)
    {
        snapshot = TerrariaWorldCreationSeedSnapshot.NotOnWorldCreationPage;
        if (memory.TryReadPointerValue(
                IntPtr.Add(menuUiObjectAddress, layout.UserInterfaceCurrentStateFieldOffset),
                out IntPtr currentStateObjectAddress) &&
            LooksLikeManagedPointer(currentStateObjectAddress) &&
            TryReadFromUiState(memory, layout, currentStateObjectAddress, out snapshot))
        {
            return true;
        }

        return false;
    }

    private static bool TryReadFromUiState(
        IProcessMemoryReader memory,
        TerrariaWorldCreationSeedMemoryLayout layout,
        IntPtr stateObjectAddress,
        out TerrariaWorldCreationSeedSnapshot snapshot)
    {
        if (TryReadKnownWorldCreationState(memory, layout, stateObjectAddress, out snapshot))
        {
            return true;
        }

        for (int offset = layout.UiStateNestedReferenceScanStart;
             offset <= layout.UiStateNestedReferenceScanEnd;
             offset += layout.ObjectReferenceSize)
        {
            if (!memory.TryReadPointerValue(IntPtr.Add(stateObjectAddress, offset), out IntPtr candidateWorldCreationAddress) ||
                !LooksLikeManagedPointer(candidateWorldCreationAddress) ||
                !TryReadWorldCreationSeed(memory, layout, candidateWorldCreationAddress, out snapshot, out IntPtr seedObjectAddress) ||
                !StateContainsSeedPlate(memory, layout, stateObjectAddress, seedObjectAddress))
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
        TerrariaWorldCreationSeedMemoryLayout layout,
        IntPtr stateObjectAddress,
        out TerrariaWorldCreationSeedSnapshot snapshot)
    {
        if (TryReadWorldCreationSeed(memory, layout, stateObjectAddress, out snapshot, out _))
        {
            return true;
        }

        if (layout.HasAdvancedState &&
            memory.TryReadPointerValue(
                IntPtr.Add(stateObjectAddress, layout.WorldCreationAdvancedCreationStateFieldOffset),
                out IntPtr advancedCreationStateAddress) &&
            LooksLikeManagedPointer(advancedCreationStateAddress) &&
            TryReadWorldCreationSeed(memory, layout, advancedCreationStateAddress, out snapshot, out _))
        {
            return true;
        }

        if (TryReadAdvancedWorldCreationSeed(memory, layout, stateObjectAddress, out snapshot))
        {
            return true;
        }

        snapshot = TerrariaWorldCreationSeedSnapshot.NotOnWorldCreationPage;
        return false;
    }

    private static bool TryReadAdvancedWorldCreationSeed(
        IProcessMemoryReader memory,
        TerrariaWorldCreationSeedMemoryLayout layout,
        IntPtr advancedStateObjectAddress,
        out TerrariaWorldCreationSeedSnapshot snapshot)
    {
        snapshot = TerrariaWorldCreationSeedSnapshot.NotOnWorldCreationPage;
        if (!layout.HasAdvancedState ||
            !memory.TryReadPointerValue(
                IntPtr.Add(advancedStateObjectAddress, layout.WorldCreationAdvancedCreationStateFieldOffset),
                out IntPtr creationStateAddress) ||
            !LooksLikeManagedPointer(creationStateAddress) ||
            !memory.TryReadPointerValue(
                IntPtr.Add(advancedStateObjectAddress, layout.WorldCreationAdvancedSeedPlateFieldOffset),
                out IntPtr seedPlateAddress) ||
            !LooksLikeManagedPointer(seedPlateAddress))
        {
            return false;
        }

        return TryReadWorldCreationSeedFromSeedPlate(
            memory,
            layout,
            creationStateAddress,
            seedPlateAddress,
            out snapshot,
            out _);
    }

    private static bool TryReadWorldCreationSeed(
        IProcessMemoryReader memory,
        TerrariaWorldCreationSeedMemoryLayout layout,
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

        if (!memory.TryReadPointerValue(IntPtr.Add(worldCreationAddress, layout.WorldNameFieldOffset), out IntPtr worldNameObject) ||
            !memory.TryReadPointerValue(IntPtr.Add(worldCreationAddress, layout.SeedFieldOffset), out seedObjectAddress) ||
            !memory.TryReadPointerValue(IntPtr.Add(worldCreationAddress, layout.NamePlateFieldOffset), out IntPtr namePlateObject) ||
            !memory.TryReadPointerValue(IntPtr.Add(worldCreationAddress, layout.SeedPlateFieldOffset), out IntPtr seedPlateObject) ||
            !LooksLikeManagedPointer(worldNameObject) ||
            !LooksLikeManagedPointer(seedObjectAddress) ||
            !LooksLikeManagedPointer(namePlateObject) ||
            !LooksLikeManagedPointer(seedPlateObject) ||
            !TryReadActualContentsPointer(memory, layout, namePlateObject, out IntPtr namePlateContents) ||
            !TryReadActualContentsPointer(memory, layout, seedPlateObject, out IntPtr seedPlateContents) ||
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
        TerrariaWorldCreationSeedMemoryLayout layout,
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

        if (!memory.TryReadPointerValue(IntPtr.Add(worldCreationAddress, layout.WorldNameFieldOffset), out IntPtr worldNameObject) ||
            !memory.TryReadPointerValue(IntPtr.Add(worldCreationAddress, layout.SeedFieldOffset), out seedObjectAddress) ||
            !LooksLikeManagedPointer(worldNameObject) ||
            !LooksLikeManagedPointer(seedObjectAddress) ||
            !TryReadActualContentsPointer(memory, layout, seedPlateAddress, out IntPtr seedPlateContents) ||
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
        TerrariaWorldCreationSeedMemoryLayout layout,
        IntPtr stateObjectAddress,
        IntPtr seedObjectAddress)
    {
        for (int offset = layout.UiStateNestedReferenceScanStart;
             offset <= layout.UiStateNestedReferenceScanEnd;
             offset += layout.ObjectReferenceSize)
        {
            if (memory.TryReadPointerValue(IntPtr.Add(stateObjectAddress, offset), out IntPtr candidatePlateAddress) &&
                LooksLikeManagedPointer(candidatePlateAddress) &&
                TryReadActualContentsPointer(memory, layout, candidatePlateAddress, out IntPtr actualContents) &&
                actualContents == seedObjectAddress)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadActualContentsPointer(
        IProcessMemoryReader memory,
        TerrariaWorldCreationSeedMemoryLayout layout,
        IntPtr uiCharacterNameButtonObject,
        out IntPtr actualContents)
    {
        return memory.TryReadPointerValue(
            IntPtr.Add(uiCharacterNameButtonObject, layout.CharacterNameButtonActualContentsOffset),
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
