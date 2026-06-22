using System.Drawing;

namespace TerrariaSplit.Terraria;

public readonly record struct TerrariaWindowSnapshot(
    bool HasProcess,
    int? ProcessId,
    DateTime? ProcessStartTime,
    bool IsResponding,
    bool HasWindow,
    IntPtr WindowHandle,
    string WindowTitle,
    bool IsVisible,
    bool IsMinimized,
    bool IsMaximized,
    bool IsForeground,
    Rectangle? WindowBounds,
    Size? ClientSize,
    string Status);
