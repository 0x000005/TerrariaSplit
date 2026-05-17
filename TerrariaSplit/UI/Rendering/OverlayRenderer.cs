using System.Drawing;

namespace TerrariaSplit;

internal static class OverlayRenderer
{
    public static OverlayRenderResult Render(
        Graphics graphics,
        OverlayRenderContext context,
        OverlayRenderResources resources)
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

        SplitListRenderer.Render(graphics, context, resources, listOpacity);

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

        TimerRenderer.Render(graphics, context, resources);

        return new OverlayRenderResult(animationActive);
    }
}
