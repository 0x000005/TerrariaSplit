namespace TerrariaSplit.Tests;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (StartupMetrics.TryRun(args) || PyramidPreScreenMetrics.TryRun(args) || PyramidPreScreenTrace.TryRun(args))
        {
            return 0;
        }

        Console.Error.WriteLine("Expected startup-metrics, pyramid-metrics or pyramid-trace.");
        return 2;
    }
}
