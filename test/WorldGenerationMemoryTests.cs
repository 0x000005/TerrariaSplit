using System.Reflection;
using System.Diagnostics.CodeAnalysis;

namespace TerrariaSplit.Tests;

internal static class WorldGenerationMemoryTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("TerrariaMemoryResolver reads world generation progress and pass name", TerrariaMemoryResolverReadsWorldGenerationProgressAndPassName);
        yield return ("TerrariaMemoryResolver ignores progress slots without a valid message string", TerrariaMemoryResolverIgnoresProgressWithoutMessageString);
    }

    private static void TerrariaMemoryResolverReadsWorldGenerationProgressAndPassName()
    {
        var resolver = new TerrariaMemoryResolver(Terraria1456Memory.Profile);
        SetPrivateField(resolver, "currentGenerationProgressAddress", new IntPtr(0x1000));
        SetPrivateField(resolver, "currentControllerAddress", new IntPtr(0x1004));

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
        var resolver = new TerrariaMemoryResolver(Terraria1456Memory.Profile);
        SetPrivateField(resolver, "currentGenerationProgressAddress", new IntPtr(0x1000));

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

    private static void SetPrivateField(object target, string fieldName, IntPtr value)
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
                : new IntPtr(BitConverter.ToInt32(buffer!, 0));
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
                WriteBytes(address, BitConverter.GetBytes(value.ToInt32()));
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
