namespace TerrariaSplit.Tests;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (StartupMetrics.TryRun(args) ||
            PyramidPreScreenMetrics.TryRun(args) ||
            PyramidPreScreenTrace.TryRun(args) ||
            JungleTunnelTrace.TryRun(args))
        {
            return 0;
        }

        Console.Error.WriteLine("Expected startup-metrics, pyramid-metrics, pyramid-trace or jungle-trace.");
        return 2;
    }
}
