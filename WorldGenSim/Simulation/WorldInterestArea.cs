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

    public static bool HasPotentialTargetPyramidCandidate(WorldGenState state)
    {
        (int left, int right) = TargetPyramidXRange(state.Options.Dimensions);
        left -= PyramidCandidateTargetPadding;
        right += PyramidCandidateTargetPadding;
        IReadOnlyList<PyramidCandidate> candidates = state.PyramidCandidates;
        for (int i = 0; i < candidates.Count; i++)
        {
            PyramidCandidate candidate = candidates[i];
            if (candidate.X < left || candidate.X >= right)
            {
                continue;
            }

            if (!IsPyramidCandidateInBuildableBand(state, candidate.X))
            {
                continue;
            }

            int minDistance = state.Options.Dimensions.Width;
            for (int previous = 0; previous < i; previous++)
            {
                minDistance = Math.Min(minDistance, Math.Abs(candidate.X - candidates[previous].X));
            }

            if (minDistance >= 220)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsPyramidCandidateInBuildableBand(WorldGenState state, int x)
    {
        int width = state.Options.Dimensions.Width;
        if (x <= 300 || x >= width - 300)
        {
            return false;
        }

        double dungeonShadow = width * 0.15;
        if (state.DungeonSide <= -1 && x < state.DungeonLocation + dungeonShadow)
        {
            return false;
        }

        if (state.DungeonSide >= 1 && x > state.DungeonLocation - dungeonShadow)
        {
            return false;
        }

        return true;
    }
}
