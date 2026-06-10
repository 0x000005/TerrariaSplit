using System.Globalization;
using WorldGenSim.Simulation;

namespace WorldGenSim;

internal static class Program
{
    private const string DefaultClassifiedWorldsPath =
        @"C:\Users\HZR\Documents\My Games\Terraria\TerrariaSplitDeleted\PyramidWorlds_classified\PyramidWorlds_classified";

    private static int Main(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        return args[0].ToLowerInvariant() switch
        {
            "samples" => RunSamples(args),
            "compare" => RunCompare(args),
            "reset-smoke" => RunResetSmoke(args),
            "terrain-smoke" => RunTerrainSmoke(args),
            "dunes-smoke" => RunDunesSmoke(args),
            "pyramid-smoke" => RunPyramidSmoke(args),
            "passes-smoke" => RunPassesSmoke(args),
            "runner-smoke" => RunRunnerSmoke(args),
            _ => UnknownCommand(args[0])
        };
    }

    private static int RunSamples(string[] args)
    {
        string root = args.Length >= 2 ? args[1] : DefaultClassifiedWorldsPath;
        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine("World folder not found: " + root);
            return 2;
        }

        var reader = new WorldFileSampleReader(root);
        int total = 0;
        int readable = 0;
        int unreadable = 0;
        int skipped = 0;

        Console.WriteLine("class,seed,size,difficulty,evil,special,pyramidChests,path");
        foreach (string path in Directory.EnumerateFiles(root, "*.wld", SearchOption.AllDirectories)
                     .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
        {
            total++;
            if (reader.TryRead(path, out WorldSample sample, out string detail))
            {
                readable++;
                WorldOptions sampleOptions = WorldOptions.FromMetadata(sample.Metadata);
                if (!sampleOptions.IsTargetScope)
                {
                    skipped++;
                    continue;
                }

                Console.WriteLine(string.Join(
                    ',',
                    Csv(sample.Classification),
                    Csv(sample.Metadata.SeedText),
                    sample.Metadata.SizeCode.ToString(CultureInfo.InvariantCulture),
                    sample.Metadata.DifficultyCode.ToString(CultureInfo.InvariantCulture),
                    sample.Metadata.HasCrimson ? "crimson" : "corruption",
                    sample.Metadata.SpecialSeedMask.ToString(CultureInfo.InvariantCulture),
                    Csv(sample.PyramidChests.FormatSummary()),
                    Csv(path)));
            }
            else
            {
                unreadable++;
                Console.Error.WriteLine("Unreadable world: " + path + " :: " + detail);
            }
        }

        Console.Error.WriteLine(
            $"samples total={total.ToString(CultureInfo.InvariantCulture)} " +
            $"readable={readable.ToString(CultureInfo.InvariantCulture)} " +
            $"target={(readable - skipped).ToString(CultureInfo.InvariantCulture)} " +
            $"skipped={skipped.ToString(CultureInfo.InvariantCulture)} " +
            $"unreadable={unreadable.ToString(CultureInfo.InvariantCulture)}");

        return unreadable == 0 ? 0 : 3;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine("Unknown command: " + command);
        PrintUsage();
        return 1;
    }

    private static bool IsHelp(string value)
    {
        return value is "-h" or "--help" or "help" or "/?";
    }

    private static void PrintUsage()
    {
        Console.WriteLine("WorldGenSim");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  samples [world-folder]   Read metadata from classified .wld files.");
        Console.WriteLine("  compare [options]        Compare generated simulator results with classified .wld samples.");
        Console.WriteLine("  reset-smoke [seed]       Print non-special WorldGen.Reset replica state.");
        Console.WriteLine("  terrain-smoke [seed]     Print Reset + Terrain replica state.");
        Console.WriteLine("  dunes-smoke [seed]       Print Reset + Terrain + Dunes replica state.");
        Console.WriteLine("  pyramid-smoke [seed]     Print generated pyramid candidates and target chest loot.");
        Console.WriteLine("  passes-smoke [seed] [stop-pass]");
        Console.WriteLine("                          Print per-pass RandNext values, default stop-pass is Surface Caves.");
        Console.WriteLine("  runner-smoke [seed]      Exercise the stage-1 pass runner and RNG reset semantics.");
        Console.WriteLine();
        Console.WriteLine("Compare options:");
        Console.WriteLine("  --worlds <folder>        Classified world folder. Defaults to the configured corpus.");
        Console.WriteLine("  --limit <N>              Compare at most N samples.");
        Console.WriteLine("  --backend <replica|echo> replica = current stage-1 simulator; echo = comparer self-test.");
    }

    private static int RunResetSmoke(string[] args)
    {
        int seed = ParseSmokeSeed(args);
        int sizeCode = ParseSmokeSizeCode(args);
        var state = CreateSmokeState(seed, sizeCode, args);
        ResetSimulationResult result = StageOneReset.Apply(state);
        if (!result.IsSupported)
        {
            Console.Error.WriteLine(result.Detail);
            return 4;
        }

        Console.WriteLine("key,value");
        Console.WriteLine("seed," + seed.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("size," + sizeCode.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("evil," + (state.Crimson ? "crimson" : "corruption"));
        Console.WriteLine("resetProbeRandNext," + state.ResetProbeRandNext.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("worldId," + state.WorldId.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("dungeonSide," + state.DungeonSide.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("dungeonLocation," + state.DungeonLocation.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("jungleOriginX," + state.JungleOriginX.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("snowOriginLeft," + state.SnowOriginLeft.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("snowOriginRight," + state.SnowOriginRight.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("leftBeachEnd," + state.LeftBeachEnd.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("rightBeachStart," + state.RightBeachStart.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("hellChestItems," + FormatArray(state.HellChestItems));
        Console.WriteLine("ores," + $"{state.Copper}/{state.Iron}/{state.Silver}/{state.Gold}");
        Console.WriteLine("treeStyles," + FormatArray(state.TreeStyle));
        Console.WriteLine("caveBackStyles," + FormatArray(state.CaveBackStyle));
        Console.WriteLine("moonType," + state.MoonType.ToString(CultureInfo.InvariantCulture));
        Console.Error.WriteLine(result.Detail);
        return 0;
    }

    private static int RunTerrainSmoke(string[] args)
    {
        int seed = ParseSmokeSeed(args);
        int sizeCode = ParseSmokeSizeCode(args);
        var state = CreateSmokeState(seed, sizeCode, args);
        ResetSimulationResult reset = StageOneReset.Apply(state);
        if (!reset.IsSupported)
        {
            Console.Error.WriteLine(reset.Detail);
            return 4;
        }

        var generator = new WorldGenerator(state);
        generator.Append(new DelegateGenPass("Terrain", 1d, TerrainPassReplica.Apply));
        WorldGenerationRunResult run = generator.RunUntilInclusive();
        GenPassResult terrain = run.Results[0];

        Console.WriteLine("key,value");
        Console.WriteLine("seed," + seed.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("size," + sizeCode.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("evil," + (state.Crimson ? "crimson" : "corruption"));
        Console.WriteLine("terrainRandNext," + terrain.RandNext.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("mainWorldSurface," + state.MainWorldSurface.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("mainRockLayer," + state.MainRockLayer.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("genWorldSurfaceLow," + state.WorldSurfaceLow.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("genWorldSurface," + state.WorldSurface.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("genWorldSurfaceHigh," + state.WorldSurfaceHigh.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("genRockLayerLow," + state.RockLayerLow.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("genRockLayer," + state.RockLayer.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("genRockLayerHigh," + state.RockLayerHigh.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("waterLine," + state.WaterLine.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("lavaLine," + state.LavaLine.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("leftBeachEnd," + state.LeftBeachEnd.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("rightBeachStart," + state.RightBeachStart.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("surfaceAtSpawn," + state.TerrainSurfaceHeights[state.Options.Dimensions.Width / 2].ToString(CultureInfo.InvariantCulture));
        Console.Error.WriteLine(reset.Detail);
        return 0;
    }

    private static int RunDunesSmoke(string[] args)
    {
        int seed = ParseSmokeSeed(args);
        int sizeCode = ParseSmokeSizeCode(args);
        var state = CreateSmokeState(seed, sizeCode, args);
        ResetSimulationResult reset = StageOneReset.Apply(state);
        if (!reset.IsSupported)
        {
            Console.Error.WriteLine(reset.Detail);
            return 4;
        }

        var generator = new WorldGenerator(state);
        generator.Append(new DelegateGenPass("Terrain", 1d, TerrainPassReplica.Apply));
        generator.Append(new DelegateGenPass("Dunes", 1d, DunesPassReplica.Apply));
        WorldGenerationRunResult run = generator.RunUntilInclusive();

        Console.WriteLine("key,value");
        Console.WriteLine("seed," + seed.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("size," + sizeCode.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("evil," + (state.Crimson ? "crimson" : "corruption"));
        foreach (GenPassResult passResult in run.Results)
        {
            Console.WriteLine(passResult.Name + "RandNext," + passResult.RandNext.ToString(CultureInfo.InvariantCulture));
        }

        Console.WriteLine("candidateCount," + state.PyramidCandidates.Count.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < state.PyramidCandidates.Count; i++)
        {
            PyramidCandidate candidate = state.PyramidCandidates[i];
            Console.WriteLine(
                "candidate" + i.ToString(CultureInfo.InvariantCulture) + "," +
                candidate.X.ToString(CultureInfo.InvariantCulture) + ";" +
                candidate.Y.ToString(CultureInfo.InvariantCulture) + ";" +
                candidate.SourceIndex.ToString(CultureInfo.InvariantCulture));
        }

        Console.Error.WriteLine(reset.Detail);
        return 0;
    }

    private static int RunPassesSmoke(string[] args)
    {
        int seed = args.Length >= 2 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 540278984;
        int sizeCode = 1;
        int stopIndex = 2;
        if (args.Length >= 3 && int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedSizeCode))
        {
            sizeCode = parsedSizeCode;
            stopIndex = 3;
        }

        string stopPass = args.Length > stopIndex ?
            string.Join(' ', args.Skip(stopIndex)) :
            "Surface Caves";
        var options = new WorldOptions(
            seed,
            WorldDimensions.FromSizeCode(sizeCode),
            DifficultyCode: 1,
            HasCrimson: true,
            SpecialSeedMask: 0);
        var state = new WorldGenState(options);
        state.ClearWorld();
        ResetSimulationResult reset = StageOneReset.Apply(state);
        if (!reset.IsSupported)
        {
            Console.Error.WriteLine(reset.Detail);
            return 4;
        }

        var generator = new WorldGenerator(state);
        OfficialPassPlan.AppendToPyramids(generator);
        WorldGenerationRunResult run = generator.RunUntilInclusive(stopPass);

        Console.WriteLine("pass,randNext,durationMs");
        foreach (GenPassResult passResult in run.Results)
        {
            Console.WriteLine(string.Join(
                ',',
                Csv(passResult.Name),
                passResult.RandNext.ToString(CultureInfo.InvariantCulture),
                passResult.DurationMs.ToString(CultureInfo.InvariantCulture)));
        }

        Console.Error.WriteLine(reset.Detail);
        Console.Error.WriteLine("stopped=" + (run.StoppedEarly ? run.StopPassName : "end"));
        Console.Error.WriteLine("candidateCount=" + state.PyramidCandidates.Count.ToString(CultureInfo.InvariantCulture));
        return run.StoppedEarly ? 0 : 4;
    }

    private static int RunPyramidSmoke(string[] args)
    {
        int seed = ParseSmokeSeed(args);
        int sizeCode = ParseSmokeSizeCode(args);
        StageOneReplicaResult result = new StageOneReplicaSimulator().Generate(new WorldSeedMetadata(
            seed.ToString(CultureInfo.InvariantCulture),
            sizeCode,
            DifficultyCode: 1,
            HasCrimson: true,
            SpecialSeedMask: 0));

        Console.WriteLine("key,value");
        Console.WriteLine("seed," + seed.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("complete," + result.IsComplete.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("detail," + Csv(result.Detail));
        Console.WriteLine("candidateCount," + result.State.PyramidCandidates.Count.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < result.State.PyramidCandidates.Count; i++)
        {
            PyramidCandidate candidate = result.State.PyramidCandidates[i];
            Console.WriteLine(
                "candidate" + i.ToString(CultureInfo.InvariantCulture) + "," +
                candidate.X.ToString(CultureInfo.InvariantCulture) + ";" +
                candidate.Y.ToString(CultureInfo.InvariantCulture) + ";" +
                candidate.SourceIndex.ToString(CultureInfo.InvariantCulture));
        }

        PyramidChestSet chests = result.State.ScanTargetPyramidChests();
        Console.WriteLine("targetPyramidLoot," + Csv(chests.FormatLootSummary()));
        Console.WriteLine("targetPyramidFull," + Csv(chests.FormatSummary()));
        return result.IsComplete ? 0 : 4;
    }

    private static int ParseSmokeSeed(string[] args)
    {
        return args.Length >= 2 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 540278984;
    }

    private static int ParseSmokeSizeCode(string[] args)
    {
        return args.Length >= 3 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 1;
    }

    private static WorldGenState CreateSmokeState(int seed, int sizeCode, string[] args)
    {
        bool hasCrimson = args.Length < 4 || ParseEvil(args[3]);
        var options = new WorldOptions(
            seed,
            WorldDimensions.FromSizeCode(sizeCode),
            DifficultyCode: 1,
            hasCrimson,
            SpecialSeedMask: 0);
        var state = new WorldGenState(options);
        state.ClearWorld();
        return state;
    }

    private static bool ParseEvil(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "crimson" or "1" or "true" => true,
            "corruption" or "0" or "false" => false,
            _ => throw new ArgumentException("evil must be crimson or corruption.")
        };
    }

    private static int RunCompare(string[] args)
    {
        CompareOptions options;
        try
        {
            options = CompareOptions.Parse(args, DefaultClassifiedWorldsPath);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        if (!Directory.Exists(options.WorldsPath))
        {
            Console.Error.WriteLine("World folder not found: " + options.WorldsPath);
            return 2;
        }

        IWorldGenSimulator simulator = options.Backend switch
        {
            CompareBackend.Echo => new EchoWorldGenSimulator(),
            _ => new CurrentReplicaWorldGenSimulator()
        };

        var reader = new WorldFileSampleReader(options.WorldsPath);
        int total = 0;
        int readable = 0;
        int matched = 0;
        int mismatched = 0;
        int pending = 0;
        int skipped = 0;
        int unreadable = 0;
        int targetCompared = 0;

        Console.WriteLine("status,class,seed,expected,actual,detail,path");
        foreach (string path in Directory.EnumerateFiles(options.WorldsPath, "*.wld", SearchOption.AllDirectories)
                     .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
        {
            if (options.Limit.HasValue && targetCompared >= options.Limit.Value)
            {
                break;
            }

            total++;
            if (!reader.TryRead(path, out WorldSample sample, out string detail))
            {
                unreadable++;
                Console.WriteLine(string.Join(
                    ',',
                    "unreadable",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    Csv(detail),
                    Csv(path)));
                continue;
            }

            readable++;
            WorldOptions sampleOptions = WorldOptions.FromMetadata(sample.Metadata);
            if (!sampleOptions.IsTargetScope)
            {
                skipped++;
                Console.WriteLine(string.Join(
                    ',',
                    "skipped",
                    Csv(sample.Classification),
                    Csv(sample.Metadata.SeedText),
                    Csv(sample.PyramidChests.FormatLootSummary()),
                    string.Empty,
                    Csv(sampleOptions.TargetScopeDetail()),
                    Csv(path)));
                continue;
            }

            targetCompared++;
            SimulatedWorldResult actual = simulator.Generate(sample);
            SampleComparison comparison = CompareSample(sample, actual);
            switch (comparison.Status)
            {
                case ComparisonStatus.Match:
                    matched++;
                    break;
                case ComparisonStatus.Mismatch:
                    mismatched++;
                    break;
                default:
                    pending++;
                    break;
            }

            Console.WriteLine(string.Join(
                ',',
                comparison.Status.ToString().ToLowerInvariant(),
                Csv(sample.Classification),
                Csv(sample.Metadata.SeedText),
                Csv(sample.PyramidChests.FormatLootSummary()),
                Csv(actual.PyramidChests.FormatLootSummary()),
                Csv(comparison.Detail),
                Csv(path)));
        }

        Console.Error.WriteLine(
            $"compare backend={options.Backend.ToString().ToLowerInvariant()} " +
            $"total={total.ToString(CultureInfo.InvariantCulture)} " +
            $"readable={readable.ToString(CultureInfo.InvariantCulture)} " +
            $"target={targetCompared.ToString(CultureInfo.InvariantCulture)} " +
            $"skipped={skipped.ToString(CultureInfo.InvariantCulture)} " +
            $"matched={matched.ToString(CultureInfo.InvariantCulture)} " +
            $"mismatched={mismatched.ToString(CultureInfo.InvariantCulture)} " +
            $"pending={pending.ToString(CultureInfo.InvariantCulture)} " +
            $"unreadable={unreadable.ToString(CultureInfo.InvariantCulture)}");

        return unreadable > 0 ? 3 :
            mismatched > 0 ? 5 :
            pending > 0 ? 4 :
            0;
    }

    private static SampleComparison CompareSample(WorldSample expected, SimulatedWorldResult actual)
    {
        if (actual.Status == SimulationStatus.Pending)
        {
            return new SampleComparison(ComparisonStatus.Pending, actual.Detail);
        }

        string expectedSummary = expected.PyramidChests.FormatLootSummary();
        string actualSummary = actual.PyramidChests.FormatLootSummary();
        if (string.Equals(expectedSummary, actualSummary, StringComparison.Ordinal))
        {
            return new SampleComparison(ComparisonStatus.Match, "matched pyramid target loot");
        }

        return new SampleComparison(
            ComparisonStatus.Mismatch,
            $"expected pyramid loot '{expectedSummary}' but generated '{actualSummary}'");
    }

    private static int RunRunnerSmoke(string[] args)
    {
        int seed = args.Length >= 2 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 1;
        var generator = new WorldGenerator(seed);
        generator.Append(new DelegateGenPass("A", 1d, static (context, progress) =>
        {
            _ = context.Random.Next(10);
            _ = context.Random.Next(20);
        }));
        generator.Append(new DelegateGenPass("B", 1d, static (context, progress) =>
        {
            _ = context.Random.Next(10);
            _ = context.Random.Next(20);
        }));
        WorldGenerationRunResult result = generator.RunUntilInclusive();

        foreach (GenPassResult passResult in result.Results)
        {
            Console.WriteLine($"{passResult.Name},{passResult.RandNext}");
        }

        bool sameRandNext = result.Results.Count == 2 &&
            result.Results[0].RandNext == result.Results[1].RandNext;
        Console.Error.WriteLine("sameRandNext=" + sameRandNext.ToString(CultureInfo.InvariantCulture));
        return sameRandNext ? 0 : 4;
    }

    private static string Csv(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static string FormatArray(IReadOnlyList<int> values)
    {
        return string.Join(';', values.Select(static value => value.ToString(CultureInfo.InvariantCulture)));
    }
}

internal enum CompareBackend
{
    Replica,
    Echo
}

internal sealed record CompareOptions(string WorldsPath, int? Limit, CompareBackend Backend)
{
    public static CompareOptions Parse(string[] args, string defaultWorldsPath)
    {
        string worldsPath = defaultWorldsPath;
        int? limit = null;
        CompareBackend backend = CompareBackend.Replica;

        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--worlds":
                    worldsPath = RequireValue(args, ref i, arg);
                    break;
                case "--limit":
                    string limitText = RequireValue(args, ref i, arg);
                    if (!int.TryParse(limitText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedLimit) ||
                        parsedLimit <= 0)
                    {
                        throw new ArgumentException("--limit must be a positive integer.");
                    }

                    limit = parsedLimit;
                    break;
                case "--backend":
                    string backendText = RequireValue(args, ref i, arg);
                    backend = backendText.ToLowerInvariant() switch
                    {
                        "replica" or "pending" => CompareBackend.Replica,
                        "echo" => CompareBackend.Echo,
                        _ => throw new ArgumentException("Unknown compare backend: " + backendText)
                    };
                    break;
                default:
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        throw new ArgumentException("Unknown compare option: " + arg);
                    }

                    worldsPath = arg;
                    break;
            }
        }

        return new CompareOptions(worldsPath, limit, backend);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException(option + " requires a value.");
        }

        index++;
        return args[index];
    }
}

internal interface IWorldGenSimulator
{
    SimulatedWorldResult Generate(WorldSample sample);
}

internal sealed class CurrentReplicaWorldGenSimulator : IWorldGenSimulator
{
    private readonly StageOneReplicaSimulator simulator = new();

    public SimulatedWorldResult Generate(WorldSample sample)
    {
        StageOneReplicaResult result = simulator.Generate(sample.Metadata);
        if (!result.IsComplete)
        {
            return SimulatedWorldResult.Pending(result.Detail);
        }

        return SimulatedWorldResult.Completed(
            result.State.ScanTargetPyramidChests(),
            "stage-one replica completed");
    }
}

internal sealed class EchoWorldGenSimulator : IWorldGenSimulator
{
    public SimulatedWorldResult Generate(WorldSample sample)
    {
        return SimulatedWorldResult.Completed(sample.PyramidChests, "echo backend validates comparison plumbing only");
    }
}

internal readonly record struct SimulatedWorldResult(
    SimulationStatus Status,
    PyramidChestSet PyramidChests,
    string Detail)
{
    public static SimulatedWorldResult Pending(string detail)
    {
        return new SimulatedWorldResult(SimulationStatus.Pending, PyramidChestSet.Empty, detail);
    }

    public static SimulatedWorldResult Completed(PyramidChestSet chests, string detail)
    {
        return new SimulatedWorldResult(SimulationStatus.Completed, chests, detail);
    }
}

internal enum SimulationStatus
{
    Pending,
    Completed
}

internal readonly record struct SampleComparison(ComparisonStatus Status, string Detail);

internal enum ComparisonStatus
{
    Match,
    Mismatch,
    Pending
}
