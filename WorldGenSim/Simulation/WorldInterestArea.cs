namespace WorldGenSim.Simulation;

internal static class WorldInterestArea
{
    private const int PyramidCandidateTargetPadding = 500;

    public static (int LeftInclusive, int RightExclusive) TargetPyramidXRange(WorldDimensions dimensions)
    {
        int left = (int)Math.Floor(dimensions.Width * 0.35);
        int right = (int)Math.Ceiling(dimensions.Width * 0.75);
        return (Math.Max(1, left), Math.Min(dimensions.Width - 1, right));
    }

    public static (int TopInclusive, int BottomExclusive) TargetPyramidYRange(WorldDimensions dimensions)
    {
        int top = (int)Math.Floor(dimensions.Height * 0.15);
        int bottom = (int)Math.Ceiling(dimensions.Height * 0.35);
        return (Math.Max(1, top), Math.Min(dimensions.Height - 1, bottom));
    }

    public static bool IsInTargetPyramidXRange(WorldDimensions dimensions, int x)
    {
        (int left, int right) = TargetPyramidXRange(dimensions);
        return x >= left && x < right;
    }

    public static bool IntersectsTargetPyramidArea(
        WorldDimensions dimensions,
        WorldRect area,
        int horizontalPadding = 0,
        int verticalPadding = 0)
    {
        (int left, int right) = TargetPyramidXRange(dimensions);
        (int top, int bottom) = TargetPyramidYRange(dimensions);
        return area.Left - horizontalPadding < right &&
            area.Right + horizontalPadding > left &&
            area.Top - verticalPadding < bottom &&
            area.Bottom + verticalPadding > top;
    }

    public static bool HasPyramidCandidateNearTarget(WorldGenState state)
    {
        (int left, int right) = TargetPyramidXRange(state.Options.Dimensions);
        left -= PyramidCandidateTargetPadding;
        right += PyramidCandidateTargetPadding;
        foreach (PyramidCandidate candidate in state.PyramidCandidates)
        {
            if (candidate.X >= left && candidate.X < right)
            {
                return true;
            }
        }

        return false;
    }
}
