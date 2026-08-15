using System.Diagnostics;

namespace TerrariaSplit.Tests;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        TestCase[] tests = TestCatalog.All().ToArray();
        if (args.Contains("--list", StringComparer.OrdinalIgnoreCase))
        {
            foreach (TestCase test in tests)
            {
                Console.WriteLine($"{test.Suite,-8} {test.Name}");
            }

            return 0;
        }

        string? suiteText = Environment.GetEnvironmentVariable("TERRARIA_SPLIT_TEST_SUITE");
        if (Enum.TryParse(suiteText, ignoreCase: true, out TestSuite suite))
        {
            tests = tests.Where(test => test.Suite == suite).ToArray();
        }
        else
        {
            tests = tests.Where(test => test.Suite != TestSuite.Release).ToArray();
        }

        string? filter = Environment.GetEnvironmentVariable("TERRARIA_SPLIT_TEST_FILTER");
        if (!string.IsNullOrWhiteSpace(filter))
        {
            tests = tests.Where(test => test.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        int failures = 0;
        var total = Stopwatch.StartNew();
        foreach (TestCase test in tests)
        {
            var elapsed = Stopwatch.StartNew();
            try
            {
                using var timeout = new CancellationTokenSource(test.Timeout);
                await test.Run(timeout.Token).WaitAsync(test.Timeout);
                Console.WriteLine($"PASS [{test.Suite}] {test.Name} ({elapsed.ElapsedMilliseconds} ms)");
            }
            catch (Exception ex)
            {
                failures++;
                Exception detail = ex is AggregateException aggregate ? aggregate.Flatten().InnerExceptions[0] : ex;
                Console.WriteLine($"FAIL [{test.Suite}] {test.Name} ({elapsed.ElapsedMilliseconds} ms): {detail.Message}");
            }
        }

        Console.WriteLine($"RESULT total={tests.Length} failed={failures} elapsed={total.Elapsed}");
        return failures == 0 ? 0 : 1;
    }
}

internal static class TestCatalog
{
    public static IEnumerable<TestCase> All()
    {
        return CoreAndRunTests.All()
            .Concat(InfrastructureFlowTests.All())
            .Concat(ApplicationFlowTests.All())
            .Concat(ConfigurationStorageFlowTests.All())
            .Concat(TerrariaIntegrationTests.All())
            .Concat(RaceWorldUploadFlowTests.All())
            .Concat(RaceFlowTests.All())
            .Concat(WindowsFlowTests.All())
            .Concat(UpdateFlowTests.All())
            .Concat(QualityGateTests.All());
    }
}
