using System.Drawing;

namespace TerrariaSplit.UI;

internal sealed class OverlayBoundsController
{
    private readonly int baseRowGap;
    private AppSettings settings;
    private int statusCount;
    private int visibleStatusCount;
    private Rectangle compositeBounds;
    private OverlayCompositeLayout currentLayout;

    public OverlayBoundsController(int baseRowGap, AppSettings settings, int statusCount, int visibleStatusCount)
    {
        this.baseRowGap = baseRowGap;
        this.settings = settings;
        this.statusCount = statusCount;
        this.visibleStatusCount = visibleStatusCount;
    }

    public bool IsInitialized { get; private set; }

    public Rectangle CompositeBounds => compositeBounds;

    public OverlayCompositeLayout CurrentLayout => currentLayout;

    public event Action<OverlayCompositeLayout>? LayoutChanged;

    public void Initialize(Rectangle initialCompositeBounds)
    {
        ApplyCompositeBounds(initialCompositeBounds);
    }

    public void UpdateContext(AppSettings settings, int statusCount, int visibleStatusCount)
    {
        this.settings = settings;
        this.statusCount = statusCount;
        this.visibleStatusCount = visibleStatusCount;
        if (IsInitialized)
        {
            ApplyCompositeBounds(compositeBounds);
        }
    }

    public void MoveBy(Point delta)
    {
        if (!IsInitialized)
        {
            return;
        }

        ApplyCompositeBounds(new Rectangle(
            compositeBounds.X + delta.X,
            compositeBounds.Y + delta.Y,
            compositeBounds.Width,
            compositeBounds.Height));
    }

    public void ApplyCompositeBounds(Rectangle bounds)
    {
        Rectangle normalizedBounds = Normalize(bounds);
        if (!OverlayCompositeLayoutCalculator.TryCreate(
                normalizedBounds,
                settings,
                statusCount,
                visibleStatusCount,
                baseRowGap,
                out OverlayCompositeLayout layout))
        {
            return;
        }

        compositeBounds = normalizedBounds;
        currentLayout = layout;
        IsInitialized = true;
        LayoutChanged?.Invoke(layout);
    }

    public void HandleStatusResize(Rectangle statusScreenBounds)
    {
        if (!IsInitialized)
        {
            return;
        }

        ApplyCompositeBounds(ToCompositeBounds(statusScreenBounds, currentLayout.StatusScreenBounds));
    }

    public void HandleTimerResize(Rectangle timerScreenBounds)
    {
        if (!IsInitialized)
        {
            return;
        }

        ApplyCompositeBounds(ToCompositeBounds(timerScreenBounds, currentLayout.TimerScreenBounds));
    }

    private Rectangle Normalize(Rectangle bounds)
    {
        Size minimum = SplitLayoutCalculator.GetMinimumWindowSize(settings);
        int rowMinimumHeight = SplitLayoutCalculator.GetMinimumWindowHeightForRows(
            settings,
            Math.Max(statusCount, visibleStatusCount),
            baseRowGap);
        int width = Math.Max(bounds.Width, minimum.Width);
        int height = Math.Max(bounds.Height, Math.Max(minimum.Height, rowMinimumHeight));
        height = OverlayCompositeLayoutCalculator.GetFittingHeight(
            width,
            height,
            settings,
            statusCount,
            visibleStatusCount,
            baseRowGap);
        return new Rectangle(bounds.X, bounds.Y, width, height);
    }

    private Rectangle ToCompositeBounds(Rectangle updatedScreenBounds, Rectangle previousScreenBounds)
    {
        int leftDelta = updatedScreenBounds.Left - previousScreenBounds.Left;
        int topDelta = updatedScreenBounds.Top - previousScreenBounds.Top;
        int rightDelta = updatedScreenBounds.Right - previousScreenBounds.Right;
        int bottomDelta = updatedScreenBounds.Bottom - previousScreenBounds.Bottom;

        int left = compositeBounds.Left + leftDelta;
        int top = compositeBounds.Top + topDelta;
        int right = compositeBounds.Right + rightDelta;
        int bottom = compositeBounds.Bottom + bottomDelta;
        return Rectangle.FromLTRB(left, top, right, bottom);
    }
}
