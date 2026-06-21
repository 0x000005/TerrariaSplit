using System.Drawing;

namespace TerrariaSplit.UI;

internal sealed class OverlayShell
{
    private int appliedReservedRowCount = -1;
    private int appliedVisibleRowCount = -1;

    public bool MouseClickThrough { get; private set; }

    public bool WindowsInitialized { get; private set; }

    public bool WindowInitializationInProgress { get; private set; }

    public bool StatusBoundsFeedbackEnabled { get; private set; }

    public bool SuppressStatusBoundsFeedback { get; private set; }

    public Rectangle? PendingInitialCompositeBounds { get; set; }

    public void SetMouseClickThrough(bool enabled)
    {
        MouseClickThrough = enabled;
    }

    public bool BeginWindowInitialization()
    {
        if (WindowsInitialized)
        {
            return false;
        }

        WindowInitializationInProgress = true;
        return true;
    }

    public Rectangle CompleteWindowInitialization(Rectangle fallbackBounds)
    {
        WindowsInitialized = true;
        Rectangle initialCompositeBounds = PendingInitialCompositeBounds ?? fallbackBounds;
        PendingInitialCompositeBounds = null;
        return initialCompositeBounds;
    }

    public void EndWindowInitialization()
    {
        WindowInitializationInProgress = false;
    }

    public void EnableStatusBoundsFeedback()
    {
        StatusBoundsFeedbackEnabled = true;
    }

    public void BeginSuppressStatusBoundsFeedback()
    {
        SuppressStatusBoundsFeedback = true;
    }

    public void EndSuppressStatusBoundsFeedback()
    {
        SuppressStatusBoundsFeedback = false;
    }

    public bool ApplyLayoutRowCounts(int reservedRowCount, int visibleRowCount, bool force)
    {
        if (!force &&
            reservedRowCount == appliedReservedRowCount &&
            visibleRowCount == appliedVisibleRowCount)
        {
            return false;
        }

        appliedReservedRowCount = reservedRowCount;
        appliedVisibleRowCount = visibleRowCount;
        return true;
    }
}
