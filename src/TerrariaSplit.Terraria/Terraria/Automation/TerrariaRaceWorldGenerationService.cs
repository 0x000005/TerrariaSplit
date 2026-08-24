using TerrariaSplit.Terraria.Processes;

namespace TerrariaSplit.Terraria.Automation;

public sealed record TerrariaRaceWorldGenerationResult(
    bool Succeeded,
    string WorldPath,
    string Message,
    bool Retryable)
{
    public static TerrariaRaceWorldGenerationResult Success(string worldPath)
    {
        return new TerrariaRaceWorldGenerationResult(true, worldPath, string.Empty, Retryable: false);
    }

    public static TerrariaRaceWorldGenerationResult Failure(string message, bool retryable = false)
    {
        return new TerrariaRaceWorldGenerationResult(false, string.Empty, message, retryable);
    }
}

public sealed record TerrariaRaceSeedFilterCandidate(
    string SeedText,
    int BatchIndex,
    string Detail);

public sealed record TerrariaRaceSeedFilterBatchResult(
    IReadOnlyList<TerrariaRaceSeedFilterCandidate> AcceptedCandidates,
    int EvaluatedCount,
    string FatalError,
    string Detail,
    int ConsecutiveCandidateFailures)
{
    public bool HasFatalError => !string.IsNullOrWhiteSpace(FatalError);

    public static TerrariaRaceSeedFilterBatchResult Complete(
        IReadOnlyList<TerrariaRaceSeedFilterCandidate> acceptedCandidates,
        int evaluatedCount,
        string detail,
        int consecutiveCandidateFailures)
    {
        return new TerrariaRaceSeedFilterBatchResult(
            acceptedCandidates,
            evaluatedCount,
            string.Empty,
            detail,
            consecutiveCandidateFailures);
    }

    public static TerrariaRaceSeedFilterBatchResult Fatal(
        int evaluatedCount,
        string error,
        int consecutiveCandidateFailures = 0)
    {
        return new TerrariaRaceSeedFilterBatchResult(
            Array.Empty<TerrariaRaceSeedFilterCandidate>(),
            evaluatedCount,
            error,
            error,
            consecutiveCandidateFailures);
    }
}

public sealed class TerrariaRaceWorldGenerationService : IDisposable
{
    private readonly HeadlessWorldGenerator generator;
    private readonly List<WorldSeedFilterEvaluator> seedFilterEvaluators = [];

    public TerrariaRaceWorldGenerationService(IRuntimeDataPaths? paths = null)
    {
        generator = new HeadlessWorldGenerator(paths);
    }

    public async Task<TerrariaRaceWorldGenerationResult> GenerateAndInstallAsync(
        AutoCreateWorldSettings settings,
        string seedText,
        string worldName,
        string? appLanguage,
        CancellationToken cancellationToken,
        IProgress<int>? progress = null,
        int progressMaximum = 80,
        bool seedFilterAlreadyAccepted = false)
    {
        TerrariaServerTarget? serverTarget = TerrariaServerLocator.TryResolveTarget();
        if (serverTarget is null)
        {
            return TerrariaRaceWorldGenerationResult.Failure("TerrariaServer.exe was not found.");
        }

        AutoCreateWorldSettings generationSettings = CloneRaceSettings(settings);
        HeadlessWorldGenResult result = await generator.GenerateAndScanAsync(
            serverTarget.Value,
            appLanguage,
            generationSettings,
            seedText,
            worldName,
            cancellationToken,
            CreateRaceProgressMapper(progress, progressMaximum),
            skipSeedFilter: seedFilterAlreadyAccepted);
        try
        {
            if (!string.IsNullOrWhiteSpace(result.FailureDetail))
            {
                return TerrariaRaceWorldGenerationResult.Failure(
                    result.FailureDetail,
                    result.Retryable);
            }

            if (!result.Generated)
            {
                return TerrariaRaceWorldGenerationResult.Failure("World generation was skipped because another generator is running.");
            }

            if (!result.Keep || string.IsNullOrWhiteSpace(result.WorldPath) || !File.Exists(result.WorldPath))
            {
                return TerrariaRaceWorldGenerationResult.Failure(
                    "TerrariaServer.exe did not produce a matching world file.",
                    retryable: true);
            }

            string installedPath = InstallWorld(result.WorldPath, worldName);
            return TerrariaRaceWorldGenerationResult.Success(installedPath);
        }
        finally
        {
            generator.ClearScratch();
        }
    }

    public static int CalculateSeedFilterConcurrency(int logicalProcessorCount)
    {
        return WorldSeedFilterEvaluator.CalculateParallelism(logicalProcessorCount);
    }

    public async Task<TerrariaRaceSeedFilterBatchResult> FilterSeedBatchAsync(
        AutoCreateWorldSettings settings,
        IReadOnlyList<string> seedTexts,
        CancellationToken cancellationToken,
        int initialConsecutiveCandidateFailures = 0)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(seedTexts);
        if (seedTexts.Count == 0)
        {
            return TerrariaRaceSeedFilterBatchResult.Complete(
                Array.Empty<TerrariaRaceSeedFilterCandidate>(),
                0,
                "No seeds were supplied.",
                Math.Max(0, initialConsecutiveCandidateFailures));
        }

        TerrariaServerTarget? serverTarget = TerrariaServerLocator.TryResolveTarget();
        if (serverTarget is null)
        {
            return TerrariaRaceSeedFilterBatchResult.Fatal(
                seedTexts.Count,
                "TerrariaServer.exe was not found.");
        }

        AutoCreateWorldSettings filterSettings = CloneRaceSettings(settings);
        TerrariaWorldGenerationVersion worldGenerationVersion =
            serverTarget.Value.IsLegacy1449
                ? TerrariaWorldGenerationVersion.Legacy1449
                : TerrariaWorldGenerationVersion.Modern1458;
        var tasks = new Task<SeedFilterEvaluation>[seedTexts.Count];
        EnsureSeedFilterEvaluatorCount(seedTexts.Count);
        for (int index = 0; index < seedTexts.Count; index++)
        {
            tasks[index] = EvaluateSeedAsync(
                seedFilterEvaluators[index],
                filterSettings,
                seedTexts[index],
                index,
                worldGenerationVersion,
                cancellationToken);
        }

        SeedFilterEvaluation[] evaluations = await Task.WhenAll(tasks);
        return ClassifySeedFilterBatch(
            evaluations,
            initialConsecutiveCandidateFailures);
    }

    internal static TerrariaRaceSeedFilterBatchResult ClassifySeedFilterBatch(
        IReadOnlyList<SeedFilterEvaluation> evaluations,
        int initialConsecutiveCandidateFailures = 0)
    {
        ArgumentNullException.ThrowIfNull(evaluations);
        var accepted = new List<TerrariaRaceSeedFilterCandidate>();
        string lastDetail = string.Empty;
        int consecutiveCandidateFailures = Math.Max(
            0,
            initialConsecutiveCandidateFailures);
        foreach (SeedFilterEvaluation evaluation in evaluations.OrderBy(item => item.BatchIndex))
        {
            WorldSeedFilterPrediction prediction = evaluation.Prediction;
            lastDetail = prediction.Detail;
            consecutiveCandidateFailures =
                WorldSeedFilterFailurePolicy.Advance(
                    consecutiveCandidateFailures,
                    prediction);
            if (prediction.IsCandidateFailure)
            {
                if (WorldSeedFilterFailurePolicy.ShouldStop(
                        consecutiveCandidateFailures))
                {
                    return TerrariaRaceSeedFilterBatchResult.Fatal(
                        evaluations.Count,
                        WorldSeedFilterFailurePolicy.FormatLimitReached(
                            consecutiveCandidateFailures,
                            prediction),
                        consecutiveCandidateFailures);
                }

                continue;
            }

            if (prediction.IsFatal)
            {
                return TerrariaRaceSeedFilterBatchResult.Fatal(
                    evaluations.Count,
                    prediction.Detail,
                    consecutiveCandidateFailures);
            }

            if (prediction.CanUsePrediction && prediction.AcceptSeed ||
                !prediction.CanUsePrediction && prediction.CanContinueWithoutPrediction)
            {
                accepted.Add(new TerrariaRaceSeedFilterCandidate(
                    evaluation.SeedText,
                    evaluation.BatchIndex,
                    prediction.Detail));
                continue;
            }

        }

        return TerrariaRaceSeedFilterBatchResult.Complete(
            accepted,
            evaluations.Count,
            lastDetail,
            consecutiveCandidateFailures);
    }

    public void Dispose()
    {
        foreach (WorldSeedFilterEvaluator evaluator in seedFilterEvaluators)
        {
            evaluator.Dispose();
        }
        seedFilterEvaluators.Clear();
        generator.Dispose();
    }

    private static async Task<SeedFilterEvaluation> EvaluateSeedAsync(
        WorldSeedFilterEvaluator evaluator,
        AutoCreateWorldSettings settings,
        string seedText,
        int batchIndex,
        TerrariaWorldGenerationVersion worldGenerationVersion,
        CancellationToken cancellationToken)
    {
        WorldSeedFilterPrediction prediction = await evaluator.EvaluateAsync(
            settings,
            seedText,
            worldGenerationVersion,
            cancellationToken);
        return new SeedFilterEvaluation(
            seedText,
            batchIndex,
            prediction);
    }

    private void EnsureSeedFilterEvaluatorCount(int count)
    {
        while (seedFilterEvaluators.Count < count)
        {
            seedFilterEvaluators.Add(new WorldSeedFilterEvaluator());
        }
    }

    private static AutoCreateWorldSettings CloneRaceSettings(AutoCreateWorldSettings settings)
    {
        return new AutoCreateWorldSettings
        {
            WorldSize = settings.WorldSize,
            WorldDifficulty = settings.WorldDifficulty,
            WorldEvil = settings.WorldEvil,
            SpecialSeeds = settings.SpecialSeeds,
            SecretSeeds = settings.SecretSeeds,
            EnableCheats = settings.EnableCheats,
            EnablePyramidFilter = settings.EnablePyramidFilter,
            PyramidFilterItemMask = settings.PyramidFilterItemMask,
            RequireCrimsonBetweenDungeonAndSpawn = settings.RequireCrimsonBetweenDungeonAndSpawn,
            CrimsonDistance = settings.CrimsonDistance,
            JungleRouteDepth = settings.JungleRouteDepth,
            ResourceFilterItemMask = settings.ResourceFilterItemMask,
            ResourceFilterLifeCrystalMinimum = settings.ResourceFilterLifeCrystalMinimum,
            ResourceFilterSpelunkerPotionMinimum = settings.ResourceFilterSpelunkerPotionMinimum,
            ResourceFilterFeatherfallPotionMinimum = settings.ResourceFilterFeatherfallPotionMinimum,
            PreserveExistingSaves = true
        };
    }

    private static IProgress<int>? CreateRaceProgressMapper(IProgress<int>? progress, int progressMaximum)
    {
        int maximum = Math.Clamp(progressMaximum, 0, 100);
        return progress is null
            ? null
            : new Progress<int>(percent =>
            {
                int clamped = Math.Clamp(percent, 0, 100);
                progress.Report(Math.Clamp((int)Math.Round(clamped * maximum / 100d), 0, maximum));
            });
    }

    private static string InstallWorld(string sourcePath, string worldName)
    {
        string worldsDirectory = Path.Combine(TerrariaSavePaths.SaveRoot(), "Worlds");
        Directory.CreateDirectory(worldsDirectory);
        string stem = SanitizeFileStem(string.IsNullOrWhiteSpace(worldName)
            ? "TerrariaRace"
            : worldName);
        string targetPath = GetUniquePath(worldsDirectory, stem);
        File.Copy(sourcePath, targetPath, overwrite: false);
        CopyBackupIfPresent(sourcePath, targetPath);
        return targetPath;
    }

    private static string GetUniquePath(string directory, string stem)
    {
        string candidate = Path.Combine(directory, stem + ".wld");
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        for (int i = 1; i < 10_000; i++)
        {
            candidate = Path.Combine(directory, $"{stem}-{i}.wld");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{stem}-{DateTime.UtcNow:yyyyMMddHHmmssfff}.wld");
    }

    private static string SanitizeFileStem(string value)
    {
        string stem = new(value
            .Trim()
            .Select(static ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
            .ToArray());
        return string.IsNullOrWhiteSpace(stem) ? "TerrariaRace" : stem;
    }

    private static void CopyBackupIfPresent(string sourcePath, string targetPath)
    {
        string backup = sourcePath + ".bak";
        if (File.Exists(backup))
        {
            File.Copy(backup, targetPath + ".bak", overwrite: false);
        }
    }

    internal readonly record struct SeedFilterEvaluation(
        string SeedText,
        int BatchIndex,
        WorldSeedFilterPrediction Prediction);
}
