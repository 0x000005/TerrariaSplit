using System.Diagnostics.CodeAnalysis;

namespace TerrariaSplit.Tests;

internal static class TerrariaMemoryResolverTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("TerrariaMemoryResolver keeps primary UpdateTime menu address when readable", KeepsPrimaryUpdateTimeMenuAddressWhenReadable);
        yield return ("TerrariaMemoryResolver keeps fallback menu address before boss progression route", KeepsFallbackMenuAddressBeforeBossProgressionRoute);
        yield return ("TerrariaMemoryResolver infers menu address from boss progression route", InfersMenuAddressFromBossProgressionRoute);
    }

    private static void KeepsPrimaryUpdateTimeMenuAddressWhenReadable()
    {
        var resolver = new TerrariaMemoryResolver(Terraria1456Memory.Profile);
        var memory = new FakeProcessMemoryReader(is64Bit: false);

        memory.WriteExecutablePage(new IntPtr(0x2000), BuildUpdateTimeCode(new IntPtr(0x4000)));
        memory.WriteBool(new IntPtr(0x4000), true);

        TerrariaMemoryResolveResult result = resolver.Resolve(memory);
        TerrariaMemoryResolution resolution = resolver.Resolution;

        TestAssert.Equal(true, result.ObservedGameMenu);
        TestAssert.Equal(new IntPtr(0x4000), resolution.GameMenuAddress);
        TestAssert.Equal(IntPtr.Zero, resolution.GameMenuSecondaryAddress);
        TestAssert.Equal(false, resolution.UsingBossProgressionMenuFallback);
        TestAssert.Equal(false, resolution.UsingGameMenuFallback);
    }

    private static void KeepsFallbackMenuAddressBeforeBossProgressionRoute()
    {
        var resolver = new TerrariaMemoryResolver(Terraria1456Memory.Profile);
        var memory = new FakeProcessMemoryReader(is64Bit: false);

        byte[] code = new byte[0x100];
        WriteGameMenuFallbackSignature(code, 0x00, menuModeAddress: new IntPtr(0x3000), gameMenuAddress: new IntPtr(0x4000));
        WriteBossProgressionSignature(code, 0x40, skeletronAddress: new IntPtr(0x5000), hardmodeAddress: new IntPtr(0x6000));

        memory.WriteExecutablePage(new IntPtr(0x2000), code);
        memory.WriteBool(new IntPtr(0x4000), false);
        memory.WriteBool(new IntPtr(0x5000), true);
        memory.WriteBool(new IntPtr(0x5011), true);
        memory.WriteBool(new IntPtr(0x6000), true);
        memory.WriteBool(new IntPtr(0x604E), true);

        TerrariaMemoryResolveResult result = resolver.Resolve(memory);
        TerrariaMemoryResolution resolution = resolver.Resolution;

        TestAssert.Equal(false, result.ObservedGameMenu);
        TestAssert.Equal(new IntPtr(0x4000), resolution.GameMenuAddress);
        TestAssert.Equal(false, resolution.UsingBossProgressionMenuFallback);
        TestAssert.Equal(true, resolution.UsingGameMenuFallback);
    }

    private static void InfersMenuAddressFromBossProgressionRoute()
    {
        var resolver = new TerrariaMemoryResolver(Terraria1456Memory.Profile);
        var memory = new FakeProcessMemoryReader(is64Bit: false);

        memory.WriteExecutablePage(
            new IntPtr(0x2000),
            BuildBossProgressionCode(skeletronAddress: new IntPtr(0x5000), hardmodeAddress: new IntPtr(0x6000)));
        memory.WriteBool(new IntPtr(0x5000), true);
        memory.WriteBool(new IntPtr(0x5011), true);
        memory.WriteBool(new IntPtr(0x6000), true);
        memory.WriteBool(new IntPtr(0x604E), false);

        TerrariaMemoryResolveResult result = resolver.Resolve(memory);
        TerrariaMemoryResolution resolution = resolver.Resolution;

        TestAssert.Equal(false, result.ObservedGameMenu);
        TestAssert.Equal(new IntPtr(0x604E), resolution.GameMenuAddress);
        TestAssert.Equal(new IntPtr(0x5002), resolution.BossFlagsBaseAddress);
        TestAssert.Equal(new IntPtr(0x6000), resolution.HardmodeAddress);
        TestAssert.Equal(true, resolution.UsingBossProgressionMenuFallback);
        TestAssert.Equal(true, resolution.UsingBossProgressionFallback);
        TestAssert.Equal(false, resolution.UsingGameMenuFallback);
    }

    private static byte[] BuildUpdateTimeCode(IntPtr gameMenuAddress)
    {
        byte[] bytes = new byte[0xA0];
        byte[] signaturePrefix =
        [
            0x55, 0x8B, 0xEC, 0x57, 0x56, 0x83, 0xEC, 0x78,
            0x8D, 0x7D, 0x94, 0xB9, 0x19, 0x00, 0x00, 0x00,
            0x33, 0xC0, 0xF3, 0xAB, 0x80, 0x3D
        ];
        Array.Copy(signaturePrefix, bytes, signaturePrefix.Length);
        WritePointer(bytes, 0x16, new IntPtr(0x7000));
        bytes[0x1A] = 0x00;
        bytes[0x1B] = 0x75;
        bytes[0x1C] = 0x09;
        bytes[0x1D] = 0x0F;
        bytes[0x1E] = 0xB6;
        bytes[0x1F] = 0x05;
        WritePointer(bytes, 0x20, new IntPtr(0x7001));
        WritePointer(bytes, 0x90, gameMenuAddress);
        return bytes;
    }

    private static byte[] BuildBossProgressionCode(IntPtr skeletronAddress, IntPtr hardmodeAddress)
    {
        byte[] bytes = new byte[0x80];
        WriteBossProgressionSignature(bytes, 0, skeletronAddress, hardmodeAddress);
        return bytes;
    }

    private static void WritePointer(byte[] bytes, int offset, IntPtr value)
    {
        Array.Copy(BitConverter.GetBytes(value.ToInt32()), 0, bytes, offset, 4);
    }

    private static void WriteInt32(byte[] bytes, int offset, int value)
    {
        Array.Copy(BitConverter.GetBytes(value), 0, bytes, offset, 4);
    }

    private static void WriteGameMenuFallbackSignature(
        byte[] bytes,
        int offset,
        IntPtr menuModeAddress,
        IntPtr gameMenuAddress)
    {
        bytes[offset] = 0x83;
        bytes[offset + 1] = 0x3D;
        WritePointer(bytes, offset + 2, menuModeAddress);
        bytes[offset + 6] = 0x01;
        bytes[offset + 7] = 0x74;
        bytes[offset + 8] = 0x09;
        bytes[offset + 9] = 0x80;
        bytes[offset + 10] = 0x3D;
        WritePointer(bytes, offset + 11, gameMenuAddress);
        bytes[offset + 15] = 0x00;
        bytes[offset + 16] = 0x74;
        bytes[offset + 17] = 0x0D;
        bytes[offset + 18] = 0x83;
        bytes[offset + 19] = 0x3D;
        WritePointer(bytes, offset + 20, menuModeAddress);
        bytes[offset + 24] = 0x02;
        bytes[offset + 25] = 0x0F;
        bytes[offset + 26] = 0x85;
    }

    private static void WriteBossProgressionSignature(
        byte[] bytes,
        int offset,
        IntPtr skeletronAddress,
        IntPtr hardmodeAddress)
    {
        int index = offset;
        WriteBossProgressionCheck(bytes, ref index, skeletronAddress, 0x2B, includeCall: true);
        WriteBossProgressionCheck(bytes, ref index, hardmodeAddress, 0x2C, includeCall: true);
        WriteBossProgressionCheck(bytes, ref index, new IntPtr(0x5010), 0x90, includeCall: true);
        WriteBossProgressionCheck(bytes, ref index, new IntPtr(0x5012), 0x2D, includeCall: false);
    }

    private static void WriteBossProgressionCheck(
        byte[] bytes,
        ref int index,
        IntPtr flagAddress,
        int npcId,
        bool includeCall)
    {
        bytes[index++] = 0x80;
        bytes[index++] = 0x3D;
        WritePointer(bytes, index, flagAddress);
        index += 4;
        bytes[index++] = 0x00;
        bytes[index++] = 0x74;
        bytes[index++] = 0x0C;
        bytes[index++] = 0x8B;
        bytes[index++] = 0xCE;
        bytes[index++] = 0xBA;
        WriteInt32(bytes, index, npcId);
        index += 4;
        if (includeCall)
        {
            bytes[index++] = 0xE8;
            WriteInt32(bytes, index, 0);
            index += 4;
        }
    }

    private sealed class FakeProcessMemoryReader : IProcessMemoryReader
    {
        private readonly Dictionary<long, byte> bytes = new();
        private readonly List<MemoryPage> executablePages = new();

        public FakeProcessMemoryReader(bool is64Bit)
        {
            Is64Bit = is64Bit;
        }

        public bool Is64Bit { get; }

        public IEnumerable<MemoryPage> ExecutablePages() => executablePages;

        public IEnumerable<MemoryPage> ExecutablePrivatePages() =>
            executablePages.Where(page => page.Type == MemoryPageType.Private);

        public bool TryReadBool(IntPtr address, out bool value)
        {
            value = false;
            if (!TryReadBytes(address, 1, out byte[]? buffer))
            {
                return false;
            }

            value = buffer[0] != 0;
            return true;
        }

        public bool TryReadInt32(IntPtr address, out int value)
        {
            value = 0;
            if (!TryReadBytes(address, 4, out byte[]? buffer))
            {
                return false;
            }

            value = BitConverter.ToInt32(buffer, 0);
            return true;
        }

        public bool TryReadDouble(IntPtr address, out double value)
        {
            value = 0d;
            if (!TryReadBytes(address, 8, out byte[]? buffer))
            {
                return false;
            }

            value = BitConverter.ToDouble(buffer, 0);
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
                ? new IntPtr(BitConverter.ToInt64(buffer, 0))
                : new IntPtr(BitConverter.ToInt32(buffer, 0));
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

        public void WriteExecutablePage(IntPtr address, byte[] value)
        {
            WriteBytes(address, value);
            executablePages.Add(new MemoryPage(
                address,
                value.Length,
                MemoryPageProtect.PageExecuteReadWrite,
                MemoryPageType.Private));
        }

        public void WriteBool(IntPtr address, bool value)
        {
            WriteBytes(address, [value ? (byte)1 : (byte)0]);
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
