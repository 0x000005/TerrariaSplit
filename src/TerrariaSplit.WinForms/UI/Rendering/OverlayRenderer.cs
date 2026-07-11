using System.Drawing;

namespace TerrariaSplit.UI.Rendering;

internal static class OverlayRenderer
{
    public static OverlayRenderResult RenderStatus(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources,
        Rectangle? clipBounds = null)
    {
        bool animationActive = SplitCompletionAnimationRenderer.TryGetActiveAnimation(
            context.Settings,
            context.SplitCompletionAnimation,
            context.NowUtc,
            out TimeSpan animationElapsed,
            out float animationOpacity);
        bool animationVisible = animationActive &&
            animationOpacity > 0.01f &&
            context.SplitCompletionAnimation is not null;
        float listOpacity = animationVisible ? 1f - animationOpacity : 1f;

        if (!animationActive)
        {
            resources.SplitCompletionAnimationText.Clear();
        }

        resources.BossIcons.BeginRenderFrame();
        if (ShouldRenderSplitList(listOpacity))
        {
            SplitListRenderer.Render(graphics, context, resources, listOpacity, clipBounds);
        }

        if (animationVisible && context.SplitCompletionAnimation is not null)
        {
            SplitCompletionAnimationRenderer.Render(
                graphics,
                context,
                resources,
                context.SplitCompletionAnimation,
                animationElapsed,
                animationOpacity);
        }

        return new OverlayRenderResult(animationActive, resources.BossIcons.AnimatedIconUsedInCurrentFrame);
    }

    internal static bool ShouldRenderSplitList(float opacity)
    {
        return opacity > 0f;
    }

    public static void RenderTimer(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources)
    {
        TimerRenderer.Render(graphics, context, resources);
    }
}
