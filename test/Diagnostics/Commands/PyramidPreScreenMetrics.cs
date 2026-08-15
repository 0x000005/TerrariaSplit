using System.Diagnostics;
using System.Globalization;
using TerrariaSplit;
using TerrariaSplit.Terraria.WorldGeneration;
using TerrariaSplit.Terraria.WorldGeneration.Simulation;
using ScannerPyramidChestItemNames = TerrariaSplit.Terraria.Automation.PyramidChestItemNames;

internal static class PyramidPreScreenMetrics
{
    private const string DefaultWorldRoot = @"D:\OneDrive - huzhaoran\Crimson";

    public static bool TryRun(string[] args)
    {
        if (args.Length == 0 ||
            !string.Equals(args[0], "pyramid-metrics", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        PyramidMetricsOptions options = PyramidMetricsOptions.Parse(args);
        Run(options);
        return true;
    }

    private static void Run(PyramidMetricsOptions options)
    {
        if (!Directory.Exists(options.WorldRoot))
        {
            Console.Error.WriteLine("World root not found: " + options.WorldRoot);
            Environment.ExitCode = 2;
            return;
        }

        var scanner = new TerrariaWorldFilePyramidScanner();
        WarmUp(options.WarmupCount, options.WorldGenerationVersion);

        int total = 0;
        int readable = 0;
        int supported = 0;
        int unsupported = 0;
        int unreadable = 0;
        int tp = 0;
        int fp = 0;
        int tn = 0;
        int fn = 0;
        int itemMismatch = 0;
        int errors = 0;
        var durations = new List<long>();
        var rows = new List<PyramidMetricRow>();
        var diagnostics = new List<PyramidDiagnosticRow>();

        Console.WriteLine("status,truth,predicted,seed,durationMs,worldFile,detail");
        foreach (string worldPath in Directory.EnumerateFiles(options.WorldRoot, "*.wld", SearchOption.AllDirectories)
                     .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (options.Limit.HasValue && supported >= options.Limit.Value)
            {
                break;
            }

            total++;
            if (!scanner.TryReadWorldSeedMetadata(worldPath, out TerrariaWorldSeedMetadata metadata, out string metadataDetail))
            {
                unreadable++;
                Console.WriteLine(Row("unreadable", "", "", "", "", worldPath, metadataDetail));
                continue;
            }

            readable++;
            if (!IsSupported(metadata))
            {
                unsupported++;
                Console.WriteLine(Row(
                    "unsupported",
                    "",
                    "",
                    metadata.SeedText,
                    "",
                    worldPath,
                    metadata.FormatWorldOptions()));
                continue;
            }

            if (!scanner.TryScanCandidateItemChests(
                    worldPath,
                    SizeText(metadata.SizeCode),
                    AutoCreatePyramidFilterItem.AllMask,
                    out PyramidChestScanResult truthScan,
                    out _,
                    out string scanDetail))
            {
                unreadable++;
                Console.WriteLine(Row("unreadable", "", "", metadata.SeedText, "", worldPath, scanDetail));
                continue;
            }

            supported++;
            PyramidTruthClass truth = ClassifyTruth(truthScan);
            PyramidSeedPreScreenResult prediction = PyramidSeedPreScreen.Evaluate(
                metadata.SeedText,
                metadata.SizeCode,
                metadata.DifficultyCode,
                metadata.HasCrimson,
                metadata.SpecialSeedMask,
                AutoCreatePyramidFilterItem.AllMask,
                options.WorldGenerationVersion);
            durations.Add(prediction.DurationMilliseconds);

            PyramidTruthClass predicted = ClassifyPrediction(prediction);
            bool truthHas = truth != PyramidTruthClass.None;
            bool predictedHas = prediction.Status == PyramidSeedPreScreenStatus.Complete && prediction.HasTargetPyramid;
            bool classMatches = truth == predicted;
            string status;

            if (prediction.Status != PyramidSeedPreScreenStatus.Complete)
            {
                errors++;
                status = "error";
            }
            else if (truthHas && predictedHas)
            {
                tp++;
                if (!classMatches)
                {
                    itemMismatch++;
                    status = "item-mismatch";
                }
                else
                {
                    status = "tp";
                }
            }
            else if (!truthHas && predictedHas)
            {
                fp++;
                status = "fp";
            }
            else if (truthHas)
            {
                fn++;
                status = "fn";
            }
            else
            {
                tn++;
                status = "tn";
            }

            var row = new PyramidMetricRow(
                status,
                truth,
                predicted,
                metadata.SeedText,
                prediction.DurationMilliseconds,
                worldPath,
                prediction.Detail);
            rows.Add(row);
            if (options.ShouldDiagnose(status, metadata.SeedText))
            {
                diagnostics.AddRange(CreateDiagnostics(
                    status,
                    truth,
                    predicted,
                    metadata,
                    worldPath,
                    prediction,
                    options.WorldGenerationVersion));
            }

            Console.WriteLine(Row(
                status,
                FormatClass(truth),
                FormatClass(predicted),
                metadata.SeedText,
                prediction.DurationMilliseconds.ToString(CultureInfo.InvariantCulture),
                worldPath,
                prediction.Detail));
        }

        if (!string.IsNullOrWhiteSpace(options.CsvPath))
        {
            WriteCsv(options.CsvPath, rows);
        }

        if (!string.IsNullOrWhiteSpace(options.DiagnosticsCsvPath))
        {
            WriteDiagnosticsCsv(options.DiagnosticsCsvPath, diagnostics);
        }

        foreach (PyramidDiagnosticRow diagnostic in diagnostics)
        {
            Console.Error.WriteLine(diagnostic.FormatForLog());
        }

        PrintSummary(
            total,
            readable,
            supported,
            unsupported,
            unreadable,
            tp,
            fp,
            tn,
            fn,
            itemMismatch,
            errors,
            durations);
    }

    private static void WarmUp(int warmupCount, TerrariaWorldGenerationVersion worldGenerationVersion)
    {
        for (int i = 0; i < warmupCount; i++)
        {
            _ = PyramidSeedPreScreen.EvaluateSmallCrimson(
                (540278984 + i).ToString(CultureInfo.InvariantCulture),
                difficultyCode: 1,
                requiredItemMask: AutoCreatePyramidFilterItem.AllMask,
                worldGenerationVersion);
        }
    }

    private static bool IsSupported(TerrariaWorldSeedMetadata metadata)
    {
        return metadata.SizeCode == 1 &&
            metadata.HasCrimson &&
            metadata.SpecialSeedMask == 0;
    }

    private static string SizeText(int sizeCode)
    {
        return sizeCode switch
        {
            1 => AutoCreateWorldSize.Small,
            3 => AutoCreateWorldSize.Large,
            _ => AutoCreateWorldSize.Medium
        };
    }

    private static PyramidTruthClass ClassifyTruth(PyramidChestScanResult truth)
    {
        if (truth.Chests.Count == 0)
        {
            return PyramidTruthClass.None;
        }

        if (truth.ContainsItem(ScannerPyramidChestItemNames.FlyingCarpet))
        {
            return PyramidTruthClass.FlyingCarpet;
        }

        if (truth.ContainsItem(ScannerPyramidChestItemNames.SandstormInABottle))
        {
            return PyramidTruthClass.SandstormInABottle;
        }

        return PyramidTruthClass.Other;
    }

    private static PyramidTruthClass ClassifyPrediction(PyramidSeedPreScreenResult prediction)
    {
        if (prediction.Status != PyramidSeedPreScreenStatus.Complete || !prediction.HasTargetPyramid)
        {
            return PyramidTruthClass.None;
        }

        return prediction.TargetClass switch
        {
            "flying" => PyramidTruthClass.FlyingCarpet,
            "sandstorm" => PyramidTruthClass.SandstormInABottle,
            "flying+sandstorm" => PyramidTruthClass.FlyingAndSandstorm,
            "other" => PyramidTruthClass.Other,
            _ => PyramidTruthClass.None
        };
    }

    private static IReadOnlyList<PyramidDiagnosticRow> CreateDiagnostics(
        string status,
        PyramidTruthClass truth,
        PyramidTruthClass predicted,
        TerrariaWorldSeedMetadata metadata,
        string worldPath,
        PyramidSeedPreScreenResult prediction,
        TerrariaWorldGenerationVersion worldGenerationVersion)
    {
        try
        {
            var result = new StageOneReplicaSimulator().Generate(new WorldSeedMetadata(
                metadata.SeedText,
                metadata.SizeCode,
                metadata.DifficultyCode,
                metadata.HasCrimson,
                metadata.SpecialSeedMask),
                worldGenerationVersion);
            if (!result.IsComplete)
            {
                return
                [
                    PyramidDiagnosticRow.CreateSeedRow(
                        status,
                        truth,
                        predicted,
                        metadata.SeedText,
                        worldPath,
                        "simulation-incomplete",
                        result.Detail)
                ];
            }

            WorldGenState state = result.State;
            IReadOnlyList<PyramidCandidateAnalysis> analyses = PyramidsPassReplica.AnalyzeCandidates(state);
            string category = DiagnoseErrorCategory(status, prediction, state);
            var rows = new List<PyramidDiagnosticRow>
            {
                PyramidDiagnosticRow.CreateSeedRow(
                    status,
                    truth,
                    predicted,
                    metadata.SeedText,
                    worldPath,
                    category,
                    prediction.Detail)
            };

            foreach (PyramidChest chest in state.PyramidChestsForDiagnostics)
            {
                PyramidCandidate candidate = state.PyramidCandidates[chest.CandidateIndex];
                PyramidCandidateRisk risk = state.GetPyramidCandidateRisk(chest.CandidateIndex);
                rows.Add(PyramidDiagnosticRow.CreateChestRow(
                    status,
                    truth,
                    predicted,
                    metadata.SeedText,
                    worldPath,
                    category,
                    chest.CandidateIndex,
                    candidate.X,
                    candidate.Y,
                    candidate.SourceIndex,
                    chest.X,
                    chest.Y,
                    chest.CandidateScanY,
                    risk.ToString(),
                    chest.CandidateSandDepth,
                    chest.CandidateSandSpan,
                    chest.CandidateActiveDepth,
                    chest.FormatLootSummary()));
            }

            foreach (PyramidCandidateAnalysis analysis in analyses)
            {
                PyramidCandidate candidate = analysis.Candidate;
                if (!WorldInterestArea.IsInTargetPyramidXRange(state.Options.Dimensions, candidate.X) &&
                    !IsNearTargetRange(state, candidate.X))
                {
                    continue;
                }

                PyramidCandidateRisk risk = state.GetPyramidCandidateRisk(analysis.Index);
                rows.Add(PyramidDiagnosticRow.CreateCandidateRow(
                    status,
                    truth,
                    predicted,
                    metadata.SeedText,
                    worldPath,
                    category,
                    analysis.Index,
                    candidate.X,
                    candidate.Y,
                    candidate.SourceIndex,
                    analysis.ScanY,
                    analysis.ScanTileType,
                    analysis.MinPreviousDistance,
                    analysis.Fate,
                    risk.ToString(),
                    analysis.SandDepth,
                    analysis.SandSpan,
                    analysis.ActiveDepth));
            }

            return rows;
        }
        catch (Exception ex)
        {
            return
            [
                PyramidDiagnosticRow.CreateSeedRow(
                    status,
                    truth,
                    predicted,
                    metadata.SeedText,
                    worldPath,
                    "diagnostic-error",
                    ex.Message)
            ];
        }
    }

    private static bool IsNearTargetRange(WorldGenState state, int x)
    {
        (int left, int right) = WorldInterestArea.TargetPyramidXRange(state.Options.Dimensions);
        return x >= left - 300 && x < right + 300;
    }

    private static string DiagnoseErrorCategory(
        string status,
        PyramidSeedPreScreenResult prediction,
        WorldGenState state)
    {
        bool hasAnyChest = state.PyramidChestsForDiagnostics.Count > 0;
        bool hasHardRiskChest = state.PyramidChestsForDiagnostics.Any(chest =>
            (state.GetPyramidCandidateRisk(chest.CandidateIndex) & PyramidCandidateRisk.HardRejectMask) != 0);

        return status switch
        {
            "fp" when prediction.HasTargetPyramid => "simulated-built-official-none",
            "fn" when hasHardRiskChest => "hard-risk-rejected",
            "fn" when hasAnyChest => "simulated-non-target-or-unmatched-chest",
            "fn" => "simulated-no-chest",
            "item-mismatch" => "simulated-loot-mismatch",
            _ => "not-an-error"
        };
    }

    private static string FormatClass(PyramidTruthClass value)
    {
        return value switch
        {
            PyramidTruthClass.None => "none",
            PyramidTruthClass.FlyingCarpet => "flying",
            PyramidTruthClass.SandstormInABottle => "sandstorm",
            PyramidTruthClass.FlyingAndSandstorm => "flying+sandstorm",
            PyramidTruthClass.Other => "other",
            _ => "unknown"
        };
    }

    private static void PrintSummary(
        int total,
        int readable,
        int supported,
        int unsupported,
        int unreadable,
        int tp,
        int fp,
        int tn,
        int fn,
        int itemMismatch,
        int errors,
        IReadOnlyList<long> durations)
    {
        int positives = tp + fn;
        int negatives = tn + fp;
        int completed = tp + fp + tn + fn;
        Console.Error.WriteLine("summary");
        Console.Error.WriteLine("total=" + total.ToString(CultureInfo.InvariantCulture));
        Console.Error.WriteLine("readable=" + readable.ToString(CultureInfo.InvariantCulture));
        Console.Error.WriteLine("supported=" + supported.ToString(CultureInfo.InvariantCulture));
        Console.Error.WriteLine("unsupported=" + unsupported.ToString(CultureInfo.InvariantCulture));
        Console.Error.WriteLine("unreadable=" + unreadable.ToString(CultureInfo.InvariantCulture));
        Console.Error.WriteLine("completed=" + completed.ToString(CultureInfo.InvariantCulture));
        Console.Error.WriteLine("errors=" + errors.ToString(CultureInfo.InvariantCulture));
        Console.Error.WriteLine("tp=" + tp.ToString(CultureInfo.InvariantCulture));
        Console.Error.WriteLine("fp=" + fp.ToString(CultureInfo.InvariantCulture));
        Console.Error.WriteLine("tn=" + tn.ToString(CultureInfo.InvariantCulture));
        Console.Error.WriteLine("fn=" + fn.ToString(CultureInfo.InvariantCulture));
        Console.Error.WriteLine("itemMismatch=" + itemMismatch.ToString(CultureInfo.InvariantCulture));
        Console.Error.WriteLine("fpRate=" + FormatRate(fp, negatives));
        Console.Error.WriteLine("fnRate=" + FormatRate(fn, positives));
        Console.Error.WriteLine("itemMismatchRateAmongTP=" + FormatRate(itemMismatch, tp));

        if (durations.Count == 0)
        {
            return;
        }

        long[] sorted = durations.Order().ToArray();
        double average = durations.Average();
        Console.Error.WriteLine("avgMs=" + average.ToString("0.###", CultureInfo.InvariantCulture));
        Console.Error.WriteLine("p50Ms=" + Percentile(sorted, 0.50).ToString(CultureInfo.InvariantCulture));
        Console.Error.WriteLine("p95Ms=" + Percentile(sorted, 0.95).ToString(CultureInfo.InvariantCulture));
        Console.Error.WriteLine("maxMs=" + sorted[^1].ToString(CultureInfo.InvariantCulture));
    }

    private static string FormatRate(int numerator, int denominator)
    {
        return denominator == 0
            ? "n/a"
            : ((double)numerator / denominator).ToString("P2", CultureInfo.InvariantCulture);
    }

    private static long Percentile(IReadOnlyList<long> sorted, double percentile)
    {
        int index = (int)Math.Ceiling(sorted.Count * percentile) - 1;
        index = Math.Clamp(index, 0, sorted.Count - 1);
        return sorted[index];
    }

    private static string Row(
        string status,
        string truth,
        string predicted,
        string seed,
        string durationMs,
        string worldFile,
        string detail)
    {
        return string.Join(
            ',',
            Csv(status),
            Csv(truth),
            Csv(predicted),
            Csv(seed),
            Csv(durationMs),
            Csv(worldFile),
            Csv(detail));
    }

    private static void WriteCsv(string csvPath, IReadOnlyList<PyramidMetricRow> rows)
    {
        using var writer = new StreamWriter(csvPath, append: false);
        writer.WriteLine("status,truth,predicted,seed,durationMs,worldFile,detail");
        foreach (PyramidMetricRow row in rows)
        {
            writer.WriteLine(Row(
                row.Status,
                FormatClass(row.Truth),
                FormatClass(row.Predicted),
                row.Seed,
                row.DurationMilliseconds.ToString(CultureInfo.InvariantCulture),
                row.WorldFile,
                row.Detail));
        }
    }

    private static void WriteDiagnosticsCsv(string csvPath, IReadOnlyList<PyramidDiagnosticRow> rows)
    {
        using var writer = new StreamWriter(csvPath, append: false);
        writer.WriteLine(PyramidDiagnosticRow.Header);
        foreach (PyramidDiagnosticRow row in rows)
        {
            writer.WriteLine(row.FormatCsv());
        }
    }

    private static string Csv(string value)
    {
        return value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private enum PyramidTruthClass
    {
        None,
        FlyingCarpet,
        SandstormInABottle,
        FlyingAndSandstorm,
        Other
    }

    private readonly record struct PyramidMetricRow(
        string Status,
        PyramidTruthClass Truth,
        PyramidTruthClass Predicted,
        string Seed,
        long DurationMilliseconds,
        string WorldFile,
        string Detail);

    private readonly record struct PyramidDiagnosticRow(
        string Status,
        PyramidTruthClass Truth,
        PyramidTruthClass Predicted,
        string Seed,
        string WorldFile,
        string Category,
        string RowKind,
        int CandidateIndex,
        int CandidateX,
        int CandidateY,
        int CandidateSource,
        int ChestX,
        int ChestY,
        int ScanY,
        int ScanTileType,
        int MinPreviousDistance,
        string Fate,
        string Risk,
        int SandDepth,
        int SandSpan,
        int ActiveDepth,
        string ChestLoot,
        string Detail)
    {
        public const string Header =
            "status,truth,predicted,seed,worldFile,category,rowKind,candidateIndex,candidateX,candidateY,candidateSource,chestX,chestY,scanY,scanTileType,minPreviousDistance,fate,risk,sandDepth,sandSpan,activeDepth,chestLoot,detail";

        public static PyramidDiagnosticRow CreateSeedRow(
            string status,
            PyramidTruthClass truth,
            PyramidTruthClass predicted,
            string seed,
            string worldFile,
            string category,
            string detail)
        {
            return new PyramidDiagnosticRow(
                status,
                truth,
                predicted,
                seed,
                worldFile,
                category,
                "seed",
                -1,
                -1,
                -1,
                -1,
                -1,
                -1,
                -1,
                -1,
                -1,
                string.Empty,
                string.Empty,
                0,
                0,
                0,
                string.Empty,
                detail);
        }

        public static PyramidDiagnosticRow CreateChestRow(
            string status,
            PyramidTruthClass truth,
            PyramidTruthClass predicted,
            string seed,
            string worldFile,
            string category,
            int candidateIndex,
            int candidateX,
            int candidateY,
            int candidateSource,
            int chestX,
            int chestY,
            int scanY,
            string risk,
            int sandDepth,
            int sandSpan,
            int activeDepth,
            string chestLoot)
        {
            return new PyramidDiagnosticRow(
                status,
                truth,
                predicted,
                seed,
                worldFile,
                category,
                "chest",
                candidateIndex,
                candidateX,
                candidateY,
                candidateSource,
                chestX,
                chestY,
                scanY,
                -1,
                -1,
                string.Empty,
                risk,
                sandDepth,
                sandSpan,
                activeDepth,
                chestLoot,
                string.Empty);
        }

        public static PyramidDiagnosticRow CreateCandidateRow(
            string status,
            PyramidTruthClass truth,
            PyramidTruthClass predicted,
            string seed,
            string worldFile,
            string category,
            int candidateIndex,
            int candidateX,
            int candidateY,
            int candidateSource,
            int scanY,
            int scanTileType,
            int minPreviousDistance,
            string fate,
            string risk,
            int sandDepth,
            int sandSpan,
            int activeDepth)
        {
            return new PyramidDiagnosticRow(
                status,
                truth,
                predicted,
                seed,
                worldFile,
                category,
                "candidate",
                candidateIndex,
                candidateX,
                candidateY,
                candidateSource,
                -1,
                -1,
                scanY,
                scanTileType,
                minPreviousDistance,
                fate,
                risk,
                sandDepth,
                sandSpan,
                activeDepth,
                string.Empty,
                string.Empty);
        }

        public string FormatCsv()
        {
            return string.Join(
                ',',
                Csv(Status),
                Csv(FormatClass(Truth)),
                Csv(FormatClass(Predicted)),
                Csv(Seed),
                Csv(WorldFile),
                Csv(Category),
                Csv(RowKind),
                CandidateIndex.ToString(CultureInfo.InvariantCulture),
                CandidateX.ToString(CultureInfo.InvariantCulture),
                CandidateY.ToString(CultureInfo.InvariantCulture),
                CandidateSource.ToString(CultureInfo.InvariantCulture),
                ChestX.ToString(CultureInfo.InvariantCulture),
                ChestY.ToString(CultureInfo.InvariantCulture),
                ScanY.ToString(CultureInfo.InvariantCulture),
                ScanTileType.ToString(CultureInfo.InvariantCulture),
                MinPreviousDistance.ToString(CultureInfo.InvariantCulture),
                Csv(Fate),
                Csv(Risk),
                SandDepth.ToString(CultureInfo.InvariantCulture),
                SandSpan.ToString(CultureInfo.InvariantCulture),
                ActiveDepth.ToString(CultureInfo.InvariantCulture),
                Csv(ChestLoot),
                Csv(Detail));
        }

        public string FormatForLog()
        {
            return "diagnostic " + FormatCsv();
        }
    }

    private sealed record PyramidMetricsOptions(
        string WorldRoot,
        int? Limit,
        int WarmupCount,
        string CsvPath,
        string DiagnosticsCsvPath,
        TerrariaWorldGenerationVersion WorldGenerationVersion,
        bool DiagnoseErrors,
        bool DiagnoseAll,
        HashSet<string> DiagnoseSeeds)
    {
        public static PyramidMetricsOptions Parse(string[] args)
        {
            string worldRoot = DefaultWorldRoot;
            int? limit = null;
            int warmupCount = 3;
            string csvPath = string.Empty;
            string diagnosticsCsvPath = string.Empty;
            TerrariaWorldGenerationVersion worldGenerationVersion = TerrariaWorldGenerationVersion.Modern1456;
            bool diagnoseErrors = false;
            bool diagnoseAll = false;
            var diagnoseSeeds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                switch (arg)
                {
                    case "--root":
                        worldRoot = RequireValue(args, ref i, arg);
                        break;
                    case "--limit":
                        limit = int.Parse(RequireValue(args, ref i, arg), CultureInfo.InvariantCulture);
                        break;
                    case "--warmup":
                        warmupCount = int.Parse(RequireValue(args, ref i, arg), CultureInfo.InvariantCulture);
                        break;
                    case "--csv":
                        csvPath = RequireValue(args, ref i, arg);
                        break;
                    case "--diagnose-errors":
                        diagnoseErrors = true;
                        break;
                    case "--diagnose-all":
                        diagnoseAll = true;
                        break;
                    case "--diagnostics-csv":
                        diagnosticsCsvPath = RequireValue(args, ref i, arg);
                        break;
                    case "--terraria-version":
                        worldGenerationVersion = ParseWorldGenerationVersion(RequireValue(args, ref i, arg));
                        break;
                    case "--worldgen-version":
                        worldGenerationVersion = ParseWorldGenerationVersion(RequireValue(args, ref i, arg));
                        break;
                    case "--seeds":
                        foreach (string seed in RequireValue(args, ref i, arg).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            diagnoseSeeds.Add(seed);
                        }

                        break;
                    default:
                        if (arg.StartsWith("-", StringComparison.Ordinal))
                        {
                            throw new ArgumentException("Unknown pyramid-metrics option: " + arg);
                        }

                        worldRoot = arg;
                        break;
                }
            }

            return new PyramidMetricsOptions(
                worldRoot,
                limit,
                Math.Max(0, warmupCount),
                csvPath,
                diagnosticsCsvPath,
                worldGenerationVersion,
                diagnoseErrors,
                diagnoseAll,
                diagnoseSeeds);
        }

        public bool ShouldDiagnose(string status, string seed)
        {
            return DiagnoseAll ||
                DiagnoseSeeds.Contains(seed) ||
                (DiagnoseErrors && (status is "fp" or "fn" or "item-mismatch" or "error"));
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

        private static TerrariaWorldGenerationVersion ParseWorldGenerationVersion(string value)
        {
            string normalized = value.Trim();
            if (normalized.Equals("1449", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("1.4.4.9", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("v1.4.4.9", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("legacy1449", StringComparison.OrdinalIgnoreCase))
            {
                return TerrariaWorldGenerationVersion.Legacy1449;
            }

            if (normalized.Equals("1456", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("1.4.5", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("v1.4.5", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("modern1456", StringComparison.OrdinalIgnoreCase))
            {
                return TerrariaWorldGenerationVersion.Modern1456;
            }

            throw new ArgumentException("Unknown Terraria worldgen version: " + value);
        }
    }
}
