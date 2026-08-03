using System.Diagnostics;
using System.Globalization;

internal static class WorldFilterStress
{
    private const int DefaultCount = 32;

    public static bool TryRun(string[] args)
    {
        if (args.Length == 0 ||
            !string.Equals(
                args[0],
                "world-filter-stress",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            WorldFilterStressOptions options =
                WorldFilterStressOptions.Parse(args);
            RunAsync(options).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Environment.ExitCode = 2;
        }

        return true;
    }

    private static async Task RunAsync(WorldFilterStressOptions options)
    {
        string[] seeds = Enumerable.Range(0, options.Count)
            .Select(index => CreateSeed(options.StartIndex + index))
            .ToArray();
        WorldFilterStressResult[] serial = await RunPhaseAsync(
            "serial",
            seeds,
            options,
            parallelism: 1);
        WorldFilterStressResult[] parallel = await RunPhaseAsync(
            "parallel",
            seeds,
            options,
            options.Parallelism);

        int mismatches = 0;
        for (int index = 0; index < seeds.Length; index++)
        {
            if (serial[index].EquivalentTo(parallel[index]))
            {
                continue;
            }

            mismatches++;
            Console.WriteLine(string.Join(
                ',',
                "mismatch",
                index.ToString(CultureInfo.InvariantCulture),
                seeds[index],
                Escape(serial[index].Status),
                Escape(parallel[index].Status),
                Escape(serial[index].Detail),
                Escape(parallel[index].Detail)));
        }

        int failures = serial.Count(result => result.Failed) +
            parallel.Count(result => result.Failed);
        Console.WriteLine(
            $"summary,count={seeds.Length},parallelism={options.Parallelism}," +
            $"failures={failures},mismatches={mismatches}," +
            $"startIndex={options.StartIndex},mode={options.GameMode}," +
            $"library={Escape(options.LibraryPath)}");
        if (failures > 0 || mismatches > 0)
        {
            Environment.ExitCode = 1;
        }
    }

    private static async Task<WorldFilterStressResult[]> RunPhaseAsync(
        string phase,
        IReadOnlyList<string> seeds,
        WorldFilterStressOptions options,
        int parallelism)
    {
        var results = new WorldFilterStressResult[seeds.Count];
        Stopwatch stopwatch = Stopwatch.StartNew();
        await Parallel.ForEachAsync(
            Enumerable.Range(0, seeds.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = parallelism
            },
            async (index, cancellationToken) =>
            {
                string seed = seeds[index];
                Stopwatch requestStopwatch = Stopwatch.StartNew();
                try
                {
                    var client = new JungleSeedJudgeNativeClient(
                        options.LibraryPath,
                        TimeSpan.FromSeconds(15));
                    JungleSeedJudgeResult result = await client.AnalyzeAsync(
                        seed,
                        options.GameMode,
                        cancellationToken);
                    results[index] = new WorldFilterStressResult(
                        result.Status.ToString(),
                        result.Detail,
                        requestStopwatch.ElapsedMilliseconds,
                        result.Status == JungleSeedJudgeStatus.GenerationFailed);
                }
                catch (Exception ex)
                {
                    results[index] = new WorldFilterStressResult(
                        ex.GetType().Name,
                        ex.Message,
                        requestStopwatch.ElapsedMilliseconds,
                        Failed: true);
                }

                if (results[index].Failed)
                {
                    Console.WriteLine(string.Join(
                        ',',
                        phase,
                        index.ToString(CultureInfo.InvariantCulture),
                        seed,
                        Escape(results[index].Status),
                        results[index].ElapsedMilliseconds.ToString(
                            CultureInfo.InvariantCulture),
                        Escape(results[index].Detail)));
                }
            });
        Console.WriteLine(
            $"phase,{phase},count={seeds.Count},parallelism={parallelism}," +
            $"elapsedMs={stopwatch.ElapsedMilliseconds}");
        return results;
    }

    private static string CreateSeed(int index)
    {
        uint value = unchecked((uint)index * 747_796_405u + 2_891_336_453u);
        return (value & 0x7fff_ffffu).ToString(CultureInfo.InvariantCulture);
    }

    private static string Escape(string value)
    {
        string normalized = value ?? string.Empty;
        return '"' + normalized.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }

    private sealed record WorldFilterStressOptions(
        int Count,
        int StartIndex,
        int Parallelism,
        JungleSeedJudgeGameMode GameMode,
        string LibraryPath)
    {
        public static WorldFilterStressOptions Parse(string[] args)
        {
            int count = ParsePositive(args, 1, DefaultCount, "count");
            int startIndex = ParseNonNegative(args, 2, 0, "startIndex");
            int defaultParallelism =
                WorldSeedFilterEvaluator.CalculateParallelism(
                    Environment.ProcessorCount);
            int parallelism = ParsePositive(
                args,
                3,
                defaultParallelism,
                "parallelism");
            JungleSeedJudgeGameMode gameMode = args.Length > 4
                ? Enum.Parse<JungleSeedJudgeGameMode>(args[4], ignoreCase: true)
                : JungleSeedJudgeGameMode.Classic;
            string libraryPath = args.Length > 5
                ? Path.GetFullPath(args[5])
                : JungleSeedJudgeNativeLibraryLocator.ResolvePath();
            return new WorldFilterStressOptions(
                count,
                startIndex,
                parallelism,
                gameMode,
                libraryPath);
        }

        private static int ParsePositive(
            string[] args,
            int index,
            int fallback,
            string name)
        {
            int value = ParseNonNegative(args, index, fallback, name);
            return value > 0
                ? value
                : throw new ArgumentOutOfRangeException(
                    name,
                    value,
                    name + " must be positive.");
        }

        private static int ParseNonNegative(
            string[] args,
            int index,
            int fallback,
            string name)
        {
            if (args.Length <= index)
            {
                return fallback;
            }

            if (int.TryParse(
                    args[index],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int value) &&
                value >= 0)
            {
                return value;
            }

            throw new ArgumentException(
                $"{name} must be a non-negative integer.",
                name);
        }
    }

    private sealed record WorldFilterStressResult(
        string Status,
        string Detail,
        long ElapsedMilliseconds,
        bool Failed)
    {
        public bool EquivalentTo(WorldFilterStressResult other)
        {
            return string.Equals(Status, other.Status, StringComparison.Ordinal) &&
                string.Equals(Detail, other.Detail, StringComparison.Ordinal);
        }
    }
}
