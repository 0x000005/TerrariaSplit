using System.Reflection;
using System.Diagnostics.CodeAnalysis;

namespace TerrariaSplit.Tests;

internal static class WorldGenerationMemoryTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("TerrariaMemoryResolver reads world generation progress and pass name", TerrariaMemoryResolverReadsWorldGenerationProgressAndPassName);
        yield return ("TerrariaMemoryResolver ignores progress slots without a valid message string", TerrariaMemoryResolverIgnoresProgressWithoutMessageString);
        yield return ("TerrariaMemoryResolver reads world generation statusText fallback", TerrariaMemoryResolverReadsWorldGenerationStatusTextFallback);
        yield return ("TerrariaWorldCreationSeedReader reads advanced seed page through visible seed plate", TerrariaWorldCreationSeedReaderReadsAdvancedSeedPage);
        yield return ("TerrariaWorldCreationSeedReader accepts high x86 managed object pointers", TerrariaWorldCreationSeedReaderAcceptsHighX86ManagedObjectPointers);
        yield return ("TerrariaWorldCreationSeedReader reads current state while randomize button is hovered", TerrariaWorldCreationSeedReaderReadsCurrentStateWhileRandomizeButtonIsHovered);
        yield return ("TerrariaWorldCreationSeedReader does not reuse stale world creation object off page", TerrariaWorldCreationSeedReaderDoesNotReuseStaleWorldCreationObjectOffPage);
    }

    private static void TerrariaMemoryResolverReadsWorldGenerationProgressAndPassName()
    {
        var resolver = new TerrariaMemoryResolver();
        resolver.SetRuntimeLayoutForTests(CreateWorldGenerationRuntimeLayout(
            statusTextAddress: IntPtr.Zero,
            progressAddress: new IntPtr(0x1000),
            controllerAddress: new IntPtr(0x1004)));

        var memory = new FakeProcessMemoryReader(is64Bit: false);
        memory.WritePointer(new IntPtr(0x1000), new IntPtr(0x2000));
        memory.WritePointer(new IntPtr(0x1004), new IntPtr(0x3000));

        memory.WriteDouble(new IntPtr(0x2004), 0.25d);
        memory.WriteDouble(new IntPtr(0x200C), 0.5d);
        memory.WriteDouble(new IntPtr(0x2014), 2d);
        memory.WriteDouble(new IntPtr(0x201C), 1d);
        memory.WritePointer(new IntPtr(0x2024), new IntPtr(0x7000));
        memory.WriteManagedString(new IntPtr(0x7000), "Step {0:0.0%}");

        memory.WritePointer(new IntPtr(0x3010), new IntPtr(0x4000));
        memory.WritePointer(new IntPtr(0x4018), new IntPtr(0x5000));
        memory.WritePointer(new IntPtr(0x500C), new IntPtr(0x6000));
        memory.WriteManagedString(new IntPtr(0x6000), "Reset");

        TerrariaWorldGenerationState state = resolver.ReadWorldGenerationState(memory);

        TestAssert.Equal("Step 25.0%", state.ProgressMessage);
        TestAssert.Equal(0.25d, state.CurrentProgress!.Value);
        TestAssert.Equal(0.375d, state.TotalProgress!.Value);
        TestAssert.Equal("Reset", state.CurrentPassName);
    }

    private static void TerrariaMemoryResolverIgnoresProgressWithoutMessageString()
    {
        var resolver = new TerrariaMemoryResolver();
        resolver.SetRuntimeLayoutForTests(CreateWorldGenerationRuntimeLayout(
            statusTextAddress: IntPtr.Zero,
            progressAddress: new IntPtr(0x1000),
            controllerAddress: IntPtr.Zero));

        var memory = new FakeProcessMemoryReader(is64Bit: false);
        memory.WritePointer(new IntPtr(0x1000), new IntPtr(0x2000));
        memory.WriteDouble(new IntPtr(0x2004), 0.25d);
        memory.WriteDouble(new IntPtr(0x200C), 0.5d);
        memory.WriteDouble(new IntPtr(0x2014), 2d);
        memory.WriteDouble(new IntPtr(0x201C), 1d);
        memory.WritePointer(new IntPtr(0x2024), IntPtr.Zero);

        TerrariaWorldGenerationState state = resolver.ReadWorldGenerationState(memory);

        TestAssert.Equal<string?>(null, state.ProgressMessage);
        TestAssert.Equal<double?>(null, state.CurrentProgress);
        TestAssert.Equal<double?>(null, state.TotalProgress);
    }

    private static void TerrariaMemoryResolverReadsWorldGenerationStatusTextFallback()
    {
        var resolver = new TerrariaMemoryResolver();
        resolver.SetRuntimeLayoutForTests(CreateWorldGenerationRuntimeLayout(
            statusTextAddress: new IntPtr(0x1000),
            progressAddress: IntPtr.Zero,
            controllerAddress: IntPtr.Zero));

        var memory = new FakeProcessMemoryReader(is64Bit: false);
        memory.WritePointer(new IntPtr(0x1000), new IntPtr(0x7000));
        memory.WriteManagedString(new IntPtr(0x7000), "37.5% - Pyramids - 50.0%");

        TerrariaWorldGenerationState state = resolver.ReadWorldGenerationState(memory);

        TestAssert.Equal("Pyramids", state.ProgressMessage);
        TestAssert.Equal(0.5d, state.CurrentProgress!.Value);
        TestAssert.Equal(0.375d, state.TotalProgress!.Value);
    }

    private static void TerrariaWorldCreationSeedReaderReadsAdvancedSeedPage()
    {
        const int advancedCreationStateOffset = 0xF0;
        const int advancedSeedPlateOffset = 0xFC;
        const int worldNameOffset = 0xF0;
        const int seedOffset = 0xF4;
        const int actualContentsOffset = 0xFC;

        var memory = new FakeProcessMemoryReader(is64Bit: false);
        IntPtr advancedState = new(0x100000);
        IntPtr creationState = new(0x110000);
        IntPtr seedPlate = new(0x120000);
        IntPtr worldName = new(0x130000);
        IntPtr seedText = new(0x140000);

        memory.WritePointer(IntPtr.Add(advancedState, advancedCreationStateOffset), creationState);
        memory.WritePointer(IntPtr.Add(advancedState, advancedSeedPlateOffset), seedPlate);
        memory.WritePointer(IntPtr.Add(creationState, worldNameOffset), worldName);
        memory.WritePointer(IntPtr.Add(creationState, seedOffset), seedText);
        memory.WritePointer(IntPtr.Add(seedPlate, actualContentsOffset), seedText);
        memory.WriteManagedString(worldName, "The Test World");
        memory.WriteManagedString(seedText, "26045689");

        TerrariaWorldCreationSeedSnapshot snapshot = ReadWorldCreationSeedFromUiState(memory, advancedState);

        TestAssert.Equal(TerrariaWorldCreationSeedStatus.Seed, snapshot.Status);
        TestAssert.Equal("26045689", snapshot.SeedText);
        TestAssert.Equal(creationState, snapshot.WorldCreationAddress);
    }

    private static void TerrariaWorldCreationSeedReaderAcceptsHighX86ManagedObjectPointers()
    {
        const int advancedCreationStateOffset = 0xF0;
        const int advancedSeedPlateOffset = 0xFC;
        const int worldNameOffset = 0xF0;
        const int seedOffset = 0xF4;
        const int actualContentsOffset = 0xFC;

        var memory = new FakeProcessMemoryReader(is64Bit: false);
        IntPtr advancedState = new(0x895B17D4L);
        IntPtr creationState = new(0x895A874CL);
        IntPtr seedPlate = new(0x895B34F0L);
        IntPtr worldName = new(0x89C20200L);
        IntPtr seedText = new(0x89C21C08L);

        memory.WritePointer(IntPtr.Add(advancedState, advancedCreationStateOffset), creationState);
        memory.WritePointer(IntPtr.Add(advancedState, advancedSeedPlateOffset), seedPlate);
        memory.WritePointer(IntPtr.Add(creationState, worldNameOffset), worldName);
        memory.WritePointer(IntPtr.Add(creationState, seedOffset), seedText);
        memory.WritePointer(IntPtr.Add(seedPlate, actualContentsOffset), seedText);
        memory.WriteManagedString(worldName, "High Heap World");
        memory.WriteManagedString(seedText, "2018947530");

        TerrariaWorldCreationSeedSnapshot snapshot = ReadWorldCreationSeedFromUiState(memory, advancedState);

        TestAssert.Equal(TerrariaWorldCreationSeedStatus.Seed, snapshot.Status);
        TestAssert.Equal("2018947530", snapshot.SeedText);
        TestAssert.Equal(creationState, snapshot.WorldCreationAddress);
    }

    private static void TerrariaWorldCreationSeedReaderReadsCurrentStateWhileRandomizeButtonIsHovered()
    {
        const int currentStateOffset = 0x1C;
        const int lastElementHoverOffset = 0x58;
        const int advancedCreationStateOffset = 0xF0;
        const int advancedSeedPlateOffset = 0xFC;
        const int worldNameOffset = 0xF0;
        const int seedOffset = 0xF4;
        const int actualContentsOffset = 0xFC;

        var reader = new TerrariaWorldCreationSeedReader();
        var memory = new FakeProcessMemoryReader(is64Bit: false);
        IntPtr menuUiSlot = new(0x100000);
        TerrariaWorldCreationSeedMemoryLayout layout = CreateTestSeedLayout(menuUiSlot);
        IntPtr menuUiObject = new(0x110000);
        IntPtr advancedState = new(0x895B17D4L);
        IntPtr randomizeButton = new(0x895B1F9CL);
        IntPtr creationState = new(0x895A874CL);
        IntPtr seedPlate = new(0x895B463CL);
        IntPtr worldName = new(0x89C20200L);
        IntPtr seedText = new(0x895F3568L);

        memory.WritePointer(menuUiSlot, menuUiObject);
        memory.WritePointer(IntPtr.Add(menuUiObject, currentStateOffset), advancedState);
        memory.WritePointer(IntPtr.Add(menuUiObject, lastElementHoverOffset), randomizeButton);
        memory.WritePointer(IntPtr.Add(advancedState, advancedCreationStateOffset), creationState);
        memory.WritePointer(IntPtr.Add(advancedState, advancedSeedPlateOffset), seedPlate);
        memory.WritePointer(IntPtr.Add(creationState, worldNameOffset), worldName);
        memory.WritePointer(IntPtr.Add(creationState, seedOffset), seedText);
        memory.WritePointer(IntPtr.Add(seedPlate, actualContentsOffset), seedText);
        memory.WriteManagedString(worldName, "Hovered Randomize World");
        memory.WriteManagedString(seedText, "2033256499");

        TerrariaWorldCreationSeedSnapshot snapshot = reader.Read(memory, layout);

        TestAssert.Equal(TerrariaWorldCreationSeedStatus.Seed, snapshot.Status);
        TestAssert.Equal("2033256499", snapshot.SeedText);
        TestAssert.Equal(creationState, snapshot.WorldCreationAddress);
    }

    private static void TerrariaWorldCreationSeedReaderDoesNotReuseStaleWorldCreationObjectOffPage()
    {
        const int currentStateOffset = 0x1C;
        const int worldNameOffset = 0xF0;
        const int seedOffset = 0xF4;
        const int namePlateOffset = 0x10C;
        const int seedPlateOffset = 0x110;
        const int actualContentsOffset = 0xFC;

        var reader = new TerrariaWorldCreationSeedReader();
        var memory = new FakeProcessMemoryReader(is64Bit: false);
        IntPtr menuUiSlot = new(0x100000);
        TerrariaWorldCreationSeedMemoryLayout layout = CreateTestSeedLayout(menuUiSlot);
        IntPtr menuUiObject = new(0x110000);
        IntPtr unrelatedCurrentState = new(0x120000);
        IntPtr oldCreationState = new(0x130000);
        IntPtr oldWorldName = new(0x140000);
        IntPtr oldSeedText = new(0x150000);
        IntPtr oldNamePlate = new(0x160000);
        IntPtr oldSeedPlate = new(0x170000);

        SetPrivateField(
            reader,
            "lastSnapshot",
            TerrariaWorldCreationSeedSnapshot.FromSeed("old-seed", oldCreationState));

        memory.WritePointer(menuUiSlot, menuUiObject);
        memory.WritePointer(IntPtr.Add(menuUiObject, currentStateOffset), unrelatedCurrentState);
        memory.WritePointer(IntPtr.Add(oldCreationState, worldNameOffset), oldWorldName);
        memory.WritePointer(IntPtr.Add(oldCreationState, seedOffset), oldSeedText);
        memory.WritePointer(IntPtr.Add(oldCreationState, namePlateOffset), oldNamePlate);
        memory.WritePointer(IntPtr.Add(oldCreationState, seedPlateOffset), oldSeedPlate);
        memory.WritePointer(IntPtr.Add(oldNamePlate, actualContentsOffset), oldWorldName);
        memory.WritePointer(IntPtr.Add(oldSeedPlate, actualContentsOffset), oldSeedText);
        memory.WriteManagedString(oldWorldName, "Old World");
        memory.WriteManagedString(oldSeedText, "old-seed");

        TerrariaWorldCreationSeedSnapshot snapshot = reader.Read(memory, layout);

        TestAssert.Equal(TerrariaWorldCreationSeedStatus.NotOnWorldCreationPage, snapshot.Status);
        TestAssert.Equal<string?>(null, snapshot.SeedText);
        TestAssert.Equal(IntPtr.Zero, snapshot.WorldCreationAddress);
    }

    private static TerrariaWorldCreationSeedSnapshot ReadWorldCreationSeedFromUiState(
        IProcessMemoryReader memory,
        IntPtr stateObjectAddress)
    {
        TerrariaWorldCreationSeedSnapshot snapshot = TryReadWorldCreationSeedFromUiState(memory, stateObjectAddress);
        if (snapshot.Status == TerrariaWorldCreationSeedStatus.NotOnWorldCreationPage)
        {
            throw new InvalidOperationException("Expected world creation seed to be readable from UI state.");
        }

        return snapshot;
    }

    private static TerrariaWorldCreationSeedSnapshot TryReadWorldCreationSeedFromUiState(
        IProcessMemoryReader memory,
        IntPtr stateObjectAddress)
    {
        MethodInfo method = typeof(TerrariaWorldCreationSeedReader).GetMethod(
                "TryReadFromUiState",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing TryReadFromUiState.");
        object?[] args = [memory, CreateTestSeedLayout(new IntPtr(0x100000)), stateObjectAddress, null];
        _ = (bool)(method.Invoke(null, args)
            ?? throw new InvalidOperationException("TryReadFromUiState returned null."));

        return (TerrariaWorldCreationSeedSnapshot)(args[3]
            ?? throw new InvalidOperationException("TryReadFromUiState did not set snapshot."));
    }

    private static TerrariaRuntimeMemoryLayout CreateWorldGenerationRuntimeLayout(
        IntPtr statusTextAddress,
        IntPtr progressAddress,
        IntPtr controllerAddress)
    {
        return new TerrariaRuntimeMemoryLayout(
            TerrariaVersion: "test",
            new TerrariaCoreMemoryLayout(
                GameMenuStaticFieldAddress: new IntPtr(0x4000),
                StatusTextStaticFieldAddress: statusTextAddress,
                MenuUiStaticFieldAddress: IntPtr.Zero),
            new TerrariaBossMemoryLayout(new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase)),
            Item: null,
            Npc: null,
            Biome: null,
            SeedUi: null,
            WorldGeneration: new TerrariaWorldGenerationMemoryLayout(
                statusTextAddress,
                progressAddress,
                controllerAddress,
                GenerationProgressMessageFieldOffset: 0x24,
                GenerationProgressValueFieldOffset: 0x4,
                GenerationProgressTotalWeightedProgressFieldOffset: 0xC,
                GenerationProgressTotalWeightFieldOffset: 0x14,
                GenerationProgressCurrentPassWeightFieldOffset: 0x1C,
                ControllerGeneratorFieldOffset: 0x10,
                WorldGeneratorCurrentPassFieldOffset: 0x18,
                GenPassNameFieldOffset: 0xC),
            ResolvedFieldCount: 1);
    }

    private static TerrariaWorldCreationSeedMemoryLayout CreateTestSeedLayout(IntPtr menuUiSlot)
    {
        return new TerrariaWorldCreationSeedMemoryLayout(
            menuUiSlot,
            UserInterfaceCurrentStateFieldOffset: 0x1C,
            UiStateNestedReferenceScanStart: 0x8,
            UiStateNestedReferenceScanEnd: 0x300,
            WorldCreationAdvancedCreationStateFieldOffset: 0xF0,
            WorldCreationAdvancedSeedPlateFieldOffset: 0xFC,
            WorldNameFieldOffset: 0xF0,
            SeedFieldOffset: 0xF4,
            NamePlateFieldOffset: 0x10C,
            SeedPlateFieldOffset: 0x110,
            CharacterNameButtonActualContentsOffset: 0xFC,
            ObjectReferenceSize: 4);
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing field " + fieldName);
        field.SetValue(target, value);
    }

    private sealed class FakeProcessMemoryReader : IProcessMemoryReader
    {
        private readonly Dictionary<long, byte> bytes = new();

        public FakeProcessMemoryReader(bool is64Bit)
        {
            Is64Bit = is64Bit;
        }

        public bool Is64Bit { get; }

        public IEnumerable<MemoryPage> ExecutablePages()
        {
            yield break;
        }

        public IEnumerable<MemoryPage> ExecutablePrivatePages()
        {
            yield break;
        }

        public bool TryReadBool(IntPtr address, out bool value)
        {
            value = false;
            if (!TryReadBytes(address, 1, out byte[]? buffer))
            {
                return false;
            }

            value = buffer![0] != 0;
            return true;
        }

        public bool TryReadInt32(IntPtr address, out int value)
        {
            value = 0;
            if (!TryReadBytes(address, 4, out byte[]? buffer))
            {
                return false;
            }

            value = BitConverter.ToInt32(buffer!, 0);
            return true;
        }

        public bool TryReadDouble(IntPtr address, out double value)
        {
            value = 0d;
            if (!TryReadBytes(address, 8, out byte[]? buffer))
            {
                return false;
            }

            value = BitConverter.ToDouble(buffer!, 0);
            return true;
        }

        public bool TryReadPointer(IntPtr address, out IntPtr value)
        {
            bool success = TryReadPointerValue(address, out value);
            return success && value != IntPtr.Zero;
        }

        public bool TryReadPointerValue(IntPtr address, out IntPtr value)
        {
            value = IntPtr.Zero;
            int size = Is64Bit ? 8 : 4;
            if (!TryReadBytes(address, size, out byte[]? buffer))
            {
                return false;
            }

            value = Is64Bit
                ? new IntPtr(BitConverter.ToInt64(buffer!, 0))
                : new IntPtr(BitConverter.ToUInt32(buffer!, 0));
            return true;
        }

        public bool TryReadBytes(IntPtr address, int count, [NotNullWhen(true)] out byte[]? result)
        {
            result = new byte[count];
            long start = address.ToInt64();
            for (int index = 0; index < count; index++)
            {
                if (!bytes.TryGetValue(start + index, out byte value))
                {
                    result = null;
                    return false;
                }

                result[index] = value;
            }

            return true;
        }

        public void WritePointer(IntPtr address, IntPtr value)
        {
            if (Is64Bit)
            {
                WriteBytes(address, BitConverter.GetBytes(value.ToInt64()));
            }
            else
            {
                WriteBytes(address, BitConverter.GetBytes(unchecked((uint)value.ToInt64())));
            }
        }

        public void WriteDouble(IntPtr address, double value)
        {
            WriteBytes(address, BitConverter.GetBytes(value));
        }

        public void WriteManagedString(IntPtr address, string value)
        {
            WriteInt32(IntPtr.Add(address, 4), value.Length);
            WriteBytes(IntPtr.Add(address, 8), System.Text.Encoding.Unicode.GetBytes(value));
        }

        private void WriteInt32(IntPtr address, int value)
        {
            WriteBytes(address, BitConverter.GetBytes(value));
        }

        private void WriteBytes(IntPtr address, byte[] value)
        {
            long start = address.ToInt64();
            for (int index = 0; index < value.Length; index++)
            {
                bytes[start + index] = value[index];
            }
        }
    }
}
