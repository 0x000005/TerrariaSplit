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
        TimeSpan duration = SplitCompletionAnimationRenderer.GetAnimationDuration(context.Settings);
        bool outgoingList = animationActive && SplitCompletionAnimationRenderer.IsOutgoingListPhase(animationElapsed, duration);
        float listOpacity = animationActive
            ? SplitCompletionAnimationRenderer.GetListOpacity(animationElapsed, duration)
            : 1f;
        if (outgoingList)
        {
            listOpacity *= context.OutgoingListOpacity;
        }

        if (!animationActive)
        {
            resources.SplitCompletionAnimationText.Clear();
        }

        resources.BossIcons.BeginRenderFrame();
        if (ShouldRenderSplitList(listOpacity))
        {
            OverlayRenderContext listContext = outgoingList && context.OutgoingSplitList is not null
                ? context.OutgoingSplitList
                : context;
            SplitListRenderer.Render(graphics, listContext, resources, listOpacity, clipBounds);
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

        return new OverlayRenderResult(animationActive, resources.BossIcons.AnimatedIconUsedInCurrentFrame,
            listOpacity, outgoingList);
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
