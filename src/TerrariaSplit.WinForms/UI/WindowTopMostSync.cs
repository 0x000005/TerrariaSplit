namespace TerrariaSplit.UI;

internal static class WindowTopMostSync
{
    private static readonly IntPtr HwndTopMost = new(-1);
    private static readonly IntPtr HwndNoTopMost = new(-2);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZOrder = 0x0200;
    private const uint SwpAsyncWindowPos = 0x4000;

    // Race package updates can arrive while another UI thread owns one of the
    // overlay windows. A synchronous SetWindowPos then waits for that thread
    // while the package handler is holding the main UI thread.
    private const uint Flags =
        SwpNoSize |
        SwpNoMove |
        SwpNoActivate |
        SwpNoOwnerZOrder |
        SwpAsyncWindowPos;

    public static void Apply(bool topMost, params IntPtr[] handles)
    {
        IntPtr insertAfter = topMost ? HwndTopMost : HwndNoTopMost;
        foreach (IntPtr handle in handles)
        {
            if (handle == IntPtr.Zero)
            {
                continue;
            }

            NativeMethods.SetWindowPos(handle, insertAfter, 0, 0, 0, 0, Flags);
        }
    }

    public static void PlaceBehind(IntPtr insertAfter, params IntPtr[] handles)
    {
        if (insertAfter == IntPtr.Zero)
        {
            return;
        }

        foreach (IntPtr handle in handles)
        {
            if (handle == IntPtr.Zero || handle == insertAfter)
            {
                continue;
            }

            NativeMethods.SetWindowPos(handle, insertAfter, 0, 0, 0, 0, Flags);
        }
    }
}
