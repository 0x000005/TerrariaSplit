namespace WorldGenSim.Simulation;

internal static class WorldInterestArea
{
    public static (int LeftInclusive, int RightExclusive) CenterSixtyXRange(WorldDimensions dimensions)
    {
        int left = (int)Math.Floor(dimensions.Width * 0.2);
        int right = (int)Math.Ceiling(dimensions.Width * 0.8);
        return (Math.Max(1, left), Math.Min(dimensions.Width - 1, right));
    }

    public static bool IsInCenterSixty(WorldDimensions dimensions, int x)
    {
        (int left, int right) = CenterSixtyXRange(dimensions);
        return x >= left && x < right;
    }
}
