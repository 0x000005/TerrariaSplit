using System.Globalization;
using TerrariaSplit.Terraria.WorldGeneration;
using TerrariaSplit.Terraria.WorldGeneration.Simulation;

namespace TerrariaSplit.Diagnostics;

internal static class PyramidPreScreenTrace
{
    private static readonly string[] DefaultStops =
    [
        "Dunes",
        "Ocean Sand",
        "Full Desert",
        "Corruption",
        "Clean Up Dirt",
        "Pyramids"
    ];

    public static bool TryRun(string[] args)
    {
        if (args.Length == 0 ||
            !string.Equals(args[0], "pyramid-trace", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (args.Length < 2 || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
        {
            Console.Error.WriteLine("Usage: pyramid-trace <seed> [stop-pass ...]");
            Environment.ExitCode = 2;
            return true;
        }

        string[] stops = args.Length > 2 ? args[2..] : DefaultStops;
        foreach (string stop in stops)
        {
            TraceStop(seed, stop);
        }

        return true;
    }

    private static void TraceStop(int seed, string stopPass)
    {
        WorldSeedMetadata metadata = new(seed.ToString(CultureInfo.InvariantCulture), SizeCode: 1, DifficultyCode: 0, HasCrimson: true, SpecialSeedMask: 0);
        WorldOptions options = WorldOptions.FromMetadata(metadata);
        var state = new WorldGenState(options);
        state.ClearWorld();
        state.EnableCrimsonDiagnostics = true;
        state.EnableFullDesertDiagnostics = true;

        ResetSimulationResult reset = StageOneReset.Apply(state);
        if (!reset.IsSupported)
        {
            Console.WriteLine("stop," + Escape(stopPass) + ",unsupported," + Escape(reset.Detail));
            return;
        }

        var generator = new WorldGenerator(state);
        OfficialPassPlan.AppendToPyramids(generator);
        WorldGenerationRunResult run = generator.RunUntilInclusive(stopPass);
        Console.WriteLine("stop," + Escape(stopPass) + "," + (run.StoppedEarly ? "stopped" : "end") + "," + Escape(run.StopPassName ?? string.Empty));
        Console.WriteLine("state,worldSurface," + state.MainWorldSurface.ToString("0.###", CultureInfo.InvariantCulture));
        Console.WriteLine("state,dungeonSide," + state.DungeonSide.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("state,dungeonLocation," + state.DungeonLocation.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("state,undergroundDesert," + Rect(state.UndergroundDesertLocation));
        Console.WriteLine("state,undergroundDesertHive," + Rect(state.UndergroundDesertHiveLocation));
        foreach (CrimsonBiomeRange range in state.CrimsonBiomeRangesForDiagnostics)
        {
            Console.WriteLine(string.Join(
                ',',
                "crimsonRange",
                range.Center.ToString(CultureInfo.InvariantCulture),
                range.LeftInclusive.ToString(CultureInfo.InvariantCulture),
                range.RightExclusive.ToString(CultureInfo.InvariantCulture)));
        }

        foreach (CrimsonRangeAttemptDiagnostic attempt in state.CrimsonRangeAttemptDiagnostics)
        {
            Console.WriteLine(string.Join(
                ',',
                "crimsonAttempt",
                attempt.BiomeIndex.ToString(CultureInfo.InvariantCulture),
                attempt.AttemptIndex.ToString(CultureInfo.InvariantCulture),
                attempt.Center.ToString(CultureInfo.InvariantCulture),
                attempt.LeftInclusive.ToString(CultureInfo.InvariantCulture),
                attempt.RightExclusive.ToString(CultureInfo.InvariantCulture),
                attempt.JungleLeft.ToString(CultureInfo.InvariantCulture),
                attempt.JungleRight.ToString(CultureInfo.InvariantCulture),
                attempt.SnowLeft.ToString(CultureInfo.InvariantCulture),
                attempt.SnowRight.ToString(CultureInfo.InvariantCulture),
                Escape(attempt.RejectReason.ToString())));
        }

        foreach (FullDesertCandidateDiagnostic diagnostic in state.FullDesertCandidateDiagnostics)
        {
            Console.WriteLine(string.Join(
                ',',
                "fullDesertStep",
                Escape(diagnostic.Step),
                diagnostic.EntranceKind.ToString(CultureInfo.InvariantCulture),
                diagnostic.CandidateIndex.ToString(CultureInfo.InvariantCulture),
                diagnostic.X.ToString(CultureInfo.InvariantCulture),
                diagnostic.StartY.ToString(CultureInfo.InvariantCulture),
                diagnostic.Found ? "true" : "false",
                diagnostic.ScanY.ToString(CultureInfo.InvariantCulture),
                diagnostic.TileType.ToString(CultureInfo.InvariantCulture)));
        }

        Console.WriteLine("candidate,index,x,startY,source,buildable,scanY,active,type,minDistance,fate,risk,sandDepth,sandSpan,activeDepth");

        IReadOnlyList<PyramidCandidateAnalysis> analyses = PyramidsPassReplica.AnalyzeCandidates(state);
        for (int i = 0; i < analyses.Count; i++)
        {
            PyramidCandidateAnalysis analysis = analyses[i];
            PyramidCandidate candidate = analysis.Candidate;
            Console.WriteLine(string.Join(
                ',',
                "candidate",
                analysis.Index.ToString(CultureInfo.InvariantCulture),
                candidate.X.ToString(CultureInfo.InvariantCulture),
                candidate.Y.ToString(CultureInfo.InvariantCulture),
                candidate.SourceIndex.ToString(CultureInfo.InvariantCulture),
                analysis.BuildableBand ? "true" : "false",
                analysis.ScanY.ToString(CultureInfo.InvariantCulture),
                analysis.ScanTileActive ? "true" : "false",
                analysis.ScanTileType.ToString(CultureInfo.InvariantCulture),
                analysis.MinPreviousDistance.ToString(CultureInfo.InvariantCulture),
                Escape(analysis.Fate),
                Escape(state.GetPyramidCandidateRisk(i).ToString()),
                analysis.SandDepth.ToString(CultureInfo.InvariantCulture),
                analysis.SandSpan.ToString(CultureInfo.InvariantCulture),
                analysis.ActiveDepth.ToString(CultureInfo.InvariantCulture)));
        }

        Console.WriteLine("chest,index,x,y,candidateIndex,source,scanY,depth,tunnelTopX,tunnelTopY,tunnelOpeningSide,tunnelSurfaceDistance,copperPiles,silverPiles,goldPiles,totalPiles,loot");
        IReadOnlyList<PyramidChest> chests = state.PyramidChestsForDiagnostics;
        for (int i = 0; i < chests.Count; i++)
        {
            PyramidChest chest = chests[i];
            PyramidCoinPileCounts counts = chest.CoinPileCounts;
            Console.WriteLine(string.Join(
                ',',
                "chest",
                i.ToString(CultureInfo.InvariantCulture),
                chest.X.ToString(CultureInfo.InvariantCulture),
                chest.Y.ToString(CultureInfo.InvariantCulture),
                chest.CandidateIndex.ToString(CultureInfo.InvariantCulture),
                chest.CandidateSourceIndex.ToString(CultureInfo.InvariantCulture),
                chest.CandidateScanY.ToString(CultureInfo.InvariantCulture),
                chest.DepthFromSurface.ToString(CultureInfo.InvariantCulture),
                chest.TunnelTopX.ToString(CultureInfo.InvariantCulture),
                chest.TunnelTopY.ToString(CultureInfo.InvariantCulture),
                chest.TunnelOpeningSide.ToString(CultureInfo.InvariantCulture),
                chest.TunnelSurfaceDistance.ToString(CultureInfo.InvariantCulture),
                counts.Copper.ToString(CultureInfo.InvariantCulture),
                counts.Silver.ToString(CultureInfo.InvariantCulture),
                counts.Gold.ToString(CultureInfo.InvariantCulture),
                counts.Total.ToString(CultureInfo.InvariantCulture),
                Escape(chest.FormatLootSummary())));

            foreach (PyramidCoinPile pile in chest.CoinPiles)
            {
                Console.WriteLine(string.Join(
                    ',',
                    "coinPile",
                    i.ToString(CultureInfo.InvariantCulture),
                    pile.X.ToString(CultureInfo.InvariantCulture),
                    pile.Y.ToString(CultureInfo.InvariantCulture),
                    Escape(pile.Kind.ToString())));
            }
        }
    }

    private static string Rect(WorldRect rect)
    {
        return string.Join(
            ';',
            rect.X.ToString(CultureInfo.InvariantCulture),
            rect.Y.ToString(CultureInfo.InvariantCulture),
            rect.Width.ToString(CultureInfo.InvariantCulture),
            rect.Height.ToString(CultureInfo.InvariantCulture));
    }

    private static string Escape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
