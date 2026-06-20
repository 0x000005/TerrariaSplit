namespace TerrariaSplit;

internal interface TerrariaLocalPlayerMemoryLayout
{
    IntPtr PlayerArrayStaticFieldAddress { get; }

    IntPtr MyPlayerStaticFieldAddress { get; }

    int ManagedArrayLengthOffset { get; }

    int ManagedArrayFirstElementOffset { get; }

    int ObjectReferenceSize { get; }
}

internal static class TerrariaLocalPlayerResolver
{
    public static bool TryResolve(
        IProcessMemoryReader memory,
        TerrariaLocalPlayerMemoryLayout layout,
        out IntPtr localPlayerAddress)
    {
        localPlayerAddress = IntPtr.Zero;
        if (!memory.TryReadInt32(layout.MyPlayerStaticFieldAddress, out int playerIndex) ||
            playerIndex < 0 ||
            playerIndex > 255 ||
            !memory.TryReadPointerValue(layout.PlayerArrayStaticFieldAddress, out IntPtr playerArrayAddress) ||
            playerArrayAddress == IntPtr.Zero ||
            !memory.TryReadInt32(IntPtr.Add(playerArrayAddress, layout.ManagedArrayLengthOffset), out int playerCount) ||
            playerIndex >= playerCount ||
            playerCount <= 0 ||
            playerCount > 256)
        {
            return false;
        }

        IntPtr playerElementAddress = IntPtr.Add(
            playerArrayAddress,
            layout.ManagedArrayFirstElementOffset + playerIndex * layout.ObjectReferenceSize);
        return memory.TryReadPointerValue(playerElementAddress, out localPlayerAddress) &&
            localPlayerAddress != IntPtr.Zero;
    }
}
