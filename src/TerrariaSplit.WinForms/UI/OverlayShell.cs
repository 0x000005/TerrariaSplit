using System.Drawing;

namespace TerrariaSplit.UI;

internal sealed class OverlayShell : IDisposable
{
    private int appliedReservedRowCount = -1;
    private int appliedVisibleRowCount = -1;
    private OverlayWindowController? windowController;
    private OverlayBoundsController? boundsController;
    private TimerOverlayWindowHost? timerOverlayHost;
    private OverlayRenderResources? renderResources;
    private OverlayAnimationController? animations;

    public AppSettings TimerOverlaySettingsSnapshot { get; private set; } = new();

    public long TimerOverlaySettingsRevision { get; private set; }

    public UiPalette Palette { get; private set; }

    public bool StatusOverlayContentDirty { get; private set; } = true;

    public StatusOverlayDynamicKey? LastStatusOverlayDynamicKey { get; private set; }

    public Rectangle? StatusOverlayPartialClipBounds { get; private set; }

    public OverlayWindowController WindowController =>
        windowController ?? throw new InvalidOperationException("Overlay window controller has not been attached.");

    public OverlayBoundsController BoundsController =>
        boundsController ?? throw new InvalidOperationException("Overlay bounds controller has not been attached.");

    public TimerOverlayWindowHost TimerOverlayHost =>
        timerOverlayHost ?? throw new InvalidOperationException("Timer overlay host has not been attached.");

    public OverlayRenderResources RenderResources =>
        renderResources ?? throw new InvalidOperationException("Overlay render resources have not been attached.");

    public OverlayAnimationController Animations =>
        animations ?? throw new InvalidOperationException("Overlay animations have not been attached.");

    public bool MouseClickThrough { get; private set; }

    public bool WindowsInitialized { get; private set; }

    public bool WindowInitializationInProgress { get; private set; }

    public bool StatusBoundsFeedbackEnabled { get; private set; }

    public bool SuppressStatusBoundsFeedback { get; private set; }

    public Rectangle? PendingInitialCompositeBounds { get; set; }

    public void AttachRuntimeComponents(
        OverlayWindowController windowController,
        OverlayBoundsController boundsController,
        TimerOverlayWindowHost timerOverlayHost,
        OverlayRenderResources renderResources,
        OverlayAnimationController animations)
    {
        this.windowController = windowController;
        this.boundsController = boundsController;
        this.timerOverlayHost = timerOverlayHost;
        this.renderResources = renderResources;
        this.animations = animations;
    }

    public void SetMouseClickThrough(bool enabled)
    {
        MouseClickThrough = enabled;
    }

    public void RefreshPalette(AppSettings settings)
    {
        Palette = UiPalette.From(settings.Overlay.Colors);
    }

    public void RefreshTimerOverlaySettingsSnapshot(AppSettings snapshot)
    {
        TimerOverlaySettingsRevision++;
        TimerOverlaySettingsSnapshot = snapshot;
    }

    public void MarkStatusOverlayStaticContentDirty()
    {
        StatusOverlayContentDirty = true;
        LastStatusOverlayDynamicKey = null;
    }

    public bool CanSkipRunningStatusOverlayFrame(
        bool highlightsActive,
        StatusOverlayDynamicKey currentKey)
    {
        return !StatusOverlayContentDirty &&
            !highlightsActive &&
            LastStatusOverlayDynamicKey is StatusOverlayDynamicKey previousKey &&
            currentKey == previousKey;
    }

    public void RecordStatusOverlayRender(StatusOverlayDynamicKey dynamicKey)
    {
        LastStatusOverlayDynamicKey = dynamicKey;
        if (StatusOverlayPartialClipBounds is null)
        {
            StatusOverlayContentDirty = false;
        }
    }

    public void BeginStatusOverlayPartialClip(Rectangle bounds)
    {
        StatusOverlayPartialClipBounds = bounds;
    }

    public void EndStatusOverlayPartialClip()
    {
        StatusOverlayPartialClipBounds = null;
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

    public void Dispose()
    {
        timerOverlayHost?.Dispose();
        windowController?.Dispose();
        renderResources?.Dispose();
    }
}
