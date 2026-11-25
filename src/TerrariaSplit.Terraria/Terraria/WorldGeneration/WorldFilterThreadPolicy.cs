namespace TerrariaSplit.Terraria.WorldGeneration;

internal static class WorldFilterThreadPolicy
{
    public const int MaximumThreads = 8;
    public const int MaximumExecutionThreads = 6;

    public static int Automatic(int availablePhysicalCores) =>
        Math.Max(1, (int)Math.Min(MaximumExecutionThreads, 3L * Math.Max(1, availablePhysicalCores) / 4));

    public static int Select(int requestedThreads, int availablePhysicalCores)
    {
        if (requestedThreads is < 0 or > MaximumThreads)
            throw new ArgumentOutOfRangeException(nameof(requestedThreads));
        return Math.Min(Math.Min(MaximumExecutionThreads, requestedThreads == 0 ? Automatic(availablePhysicalCores) : requestedThreads),
            Math.Max(1, availablePhysicalCores));
    }
}
