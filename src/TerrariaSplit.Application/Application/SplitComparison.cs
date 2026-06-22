namespace TerrariaSplit.Application;

internal readonly record struct SplitComparison(TimeSpan? Delta, bool ShowDelta)
{
    public static SplitComparison Empty => new(null, false);
}
