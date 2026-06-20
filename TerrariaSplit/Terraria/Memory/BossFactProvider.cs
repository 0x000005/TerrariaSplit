namespace TerrariaSplit;

internal sealed class BossFactProvider
{
    public TerrariaGameFacts Read(IProcessMemoryReader memory, TerrariaMemoryContext context)
    {
        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        byte[]? flagBytes = null;
        int minimumOffset = 0;
        bool hasFlagBlock = TryReadFlagBlock(memory, context, out flagBytes, out minimumOffset);

        foreach (BossFactDescriptor boss in SplitCatalog.BossFacts)
        {
            bool? value = boss.AddressKind switch
            {
                BossFactAddressKind.Hardmode => ReadHardmode(memory, context),
                BossFactAddressKind.BossFlagBlock when hasFlagBlock => ReadFlag(flagBytes!, minimumOffset, boss.Offset),
                _ => null
            };
            builder.SetBoolean(boss.FactKey, value);
        }

        return builder.Build();
    }

    private static bool TryReadFlagBlock(
        IProcessMemoryReader memory,
        TerrariaMemoryContext context,
        out byte[]? bytes,
        out int minimumOffset)
    {
        bytes = null;
        minimumOffset = 0;
        if (context.BossFlags is null || context.BossFlags.BaseAddress == IntPtr.Zero)
        {
            return false;
        }

        int[] offsets = SplitCatalog.BossFacts
            .Where(boss => boss.AddressKind == BossFactAddressKind.BossFlagBlock)
            .Select(boss => boss.Offset)
            .ToArray();
        minimumOffset = offsets.Min();
        int maximumOffset = offsets.Max();
        int length = maximumOffset - minimumOffset + 1;
        return memory.TryReadBytes(IntPtr.Add(context.BossFlags.BaseAddress, minimumOffset), length, out bytes);
    }

    private static bool? ReadFlag(byte[] bytes, int minimumOffset, int offset)
    {
        int index = offset - minimumOffset;
        return index >= 0 && index < bytes.Length
            ? bytes[index] != 0
            : null;
    }

    private static bool? ReadHardmode(IProcessMemoryReader memory, TerrariaMemoryContext context)
    {
        if (context.HardmodeAddress == IntPtr.Zero)
        {
            return null;
        }

        return memory.TryReadBool(context.HardmodeAddress, out bool value)
            ? value
            : null;
    }
}
