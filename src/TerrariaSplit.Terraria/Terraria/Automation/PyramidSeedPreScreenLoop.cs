namespace TerrariaSplit.Terraria.Automation;

internal interface IPyramidSeedRandomizer
{
    Task<bool> RandomizeVisibleSeedAsync(int attempt, CancellationToken cancellationToken);
}

internal interface IPyramidVisibleSeedReader
{
    string? ReadCurrentSeed();

    bool TryPredictNextSeedBatch(
        int count,
        out IReadOnlyList<string> seedTexts,
        out string detail);

    Task<PyramidVisibleSeedReadResult> WaitForSeedAfterRandomizeAsync(
        string? previousSeedText,
        CancellationToken cancellationToken);
}

internal sealed class PyramidSeedPreScreenLoop
{
    private const int MaxConsecutiveSeedReadFailures = 3;

    private readonly WorldSeedFilterEvaluator evaluator;
    private readonly Action<string> logInfo;

    public PyramidSeedPreScreenLoop(
        WorldSeedFilterEvaluator evaluator,
        Action<string> logInfo)
    {
        this.evaluator = evaluator;
        this.logInfo = logInfo;
    }

    public async Task<PyramidSeedPreScreenLoopResult> RunAsync(
        AutoCreateWorldSettings settings,
        TerrariaMenuProfile menuProfile,
        IPyramidSeedRandomizer randomizer,
        IPyramidVisibleSeedReader seedReader,
        CancellationToken cancellationToken)
    {
        TerrariaWorldGenerationVersion worldGenerationVersion =
            PyramidSeedPreScreenEvaluator.WorldGenerationVersionFromMenuProfile(menuProfile);
        if (worldGenerationVersion != TerrariaWorldGenerationVersion.Modern1456)
        {
            return await RunSerialAsync(
                settings,
                worldGenerationVersion,
                randomizer,
                seedReader,
                cancellationToken);
        }

        int batchSize = WorldSeedFilterEvaluator.CalculateParallelism(
            Environment.ProcessorCount);
        int attempt = 0;
        int consecutiveCandidateFailures = 0;
        int consecutiveSeedReadFailures = 0;
        string? lastVisibleSeed = seedReader.ReadCurrentSeed();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!seedReader.TryPredictNextSeedBatch(
                    batchSize,
                    out IReadOnlyList<string> predictedSeeds,
                    out string predictionDetail) ||
                predictedSeeds.Count != batchSize)
            {
                logInfo(
                    "World seed pre-screen could not predict a Terraria RNG batch; " +
                    $"falling back to serial click/read filtering: {predictionDetail}");
                return await RunSerialAsync(
                    settings,
                    worldGenerationVersion,
                    randomizer,
                    seedReader,
                    cancellationToken,
                    attempt,
                    lastVisibleSeed,
                    consecutiveCandidateFailures);
            }

            logInfo(
                $"World seed pre-screen predicted {batchSize} consecutive Terraria UI seeds " +
                $"using the 80% CPU policy: {predictionDetail}.");
            IReadOnlyList<WorldSeedFilterPrediction> predictions =
                await evaluator.EvaluateBatchAsync(
                    settings,
                    predictedSeeds,
                    worldGenerationVersion,
                    cancellationToken);

            int decisionIndex = -1;
            for (int index = 0; index < predictions.Count; index++)
            {
                WorldSeedFilterPrediction candidatePrediction = predictions[index];
                consecutiveCandidateFailures =
                    WorldSeedFilterFailurePolicy.Advance(
                        consecutiveCandidateFailures,
                        candidatePrediction);
                if (candidatePrediction.IsCandidateFailure)
                {
                    logInfo(
                        $"World seed pre-screen skipped predicted candidate " +
                        $"{predictedSeeds[index]} after a generation failure: " +
                        candidatePrediction.Detail);
                    if (WorldSeedFilterFailurePolicy.ShouldStop(
                            consecutiveCandidateFailures))
                    {
                        return new PyramidSeedPreScreenLoopResult(
                            PyramidSeedPreScreenLoopStatus.CandidateFailuresExceeded,
                            attempt,
                            AcceptedSeed: null,
                            WorldSeedFilterFailurePolicy.FormatLimitReached(
                                consecutiveCandidateFailures,
                                candidatePrediction));
                    }
                }

                if (!candidatePrediction.CanUsePrediction || candidatePrediction.AcceptSeed)
                {
                    decisionIndex = index;
                    break;
                }
            }

            int clickCount = decisionIndex >= 0
                ? decisionIndex + 1
                : predictedSeeds.Count;
            string expectedSeed = predictedSeeds[clickCount - 1];
            string? seedBeforeClicks = seedReader.ReadCurrentSeed() ?? lastVisibleSeed;
            bool clickFailed = false;
            for (int clickIndex = 0; clickIndex < clickCount; clickIndex++)
            {
                attempt++;
                if (await randomizer.RandomizeVisibleSeedAsync(attempt, cancellationToken))
                {
                    continue;
                }

                clickFailed = true;
                break;
            }

            if (clickFailed)
            {
                return new PyramidSeedPreScreenLoopResult(
                    PyramidSeedPreScreenLoopStatus.RandomizeFailed,
                    attempt,
                    AcceptedSeed: null,
                    Detail: "Visible seed randomize click failed.");
            }

            PyramidVisibleSeedReadResult readResult =
                await seedReader.WaitForSeedAfterRandomizeAsync(
                    seedBeforeClicks,
                    cancellationToken);
            if (!readResult.Success)
            {
                consecutiveSeedReadFailures++;
                string detail =
                    $"No new visible seed was observed after {clickCount} predicted randomize clicks. " +
                    $"previousSeed={seedBeforeClicks ?? "unknown"}, expectedSeed={expectedSeed}, " +
                    $"lastSeed={readResult.LastSeedText}, lastStatus={readResult.LastStatus}, readAttempts={readResult.ReadAttempts}, " +
                    $"consecutiveFailures={consecutiveSeedReadFailures}.";
                if (consecutiveSeedReadFailures >= MaxConsecutiveSeedReadFailures)
                {
                    logInfo("Pyramid seed pre-screen will continue without prediction: " + detail);
                    return new PyramidSeedPreScreenLoopResult(
                        PyramidSeedPreScreenLoopStatus.SeedReadFailed,
                        attempt,
                        AcceptedSeed: null,
                        detail);
                }

                logInfo("Pyramid seed pre-screen will retry: " + detail);
                continue;
            }

            consecutiveSeedReadFailures = 0;
            lastVisibleSeed = readResult.SeedText;
            if (!string.Equals(
                    readResult.SeedText,
                    expectedSeed,
                    StringComparison.Ordinal))
            {
                logInfo(
                    $"World seed pre-screen discarded a drifted RNG batch: " +
                    $"expected={expectedSeed}, actual={readResult.SeedText}, " +
                    $"clicks={clickCount}, attempts={attempt}. Replanning from Terraria state.");
                continue;
            }

            if (decisionIndex < 0)
            {
                logInfo(
                    $"World seed pre-screen rejected the complete {batchSize}-seed batch; " +
                    $"advanced to verified tail seed {readResult.SeedText}, attempts={attempt}.");
                continue;
            }

            WorldSeedFilterPrediction prediction = predictions[decisionIndex];
            if (!prediction.CanUsePrediction)
            {
                string detail =
                    $"Seed {readResult.SeedText} could not be predicted: {prediction.Detail}.";
                logInfo(
                    prediction.CanContinueWithoutPrediction
                        ? "World seed pre-screen will continue with pyramid post-verification: " + detail
                        : "World seed pre-screen failed closed: " + detail);
                return new PyramidSeedPreScreenLoopResult(
                    prediction.CanContinueWithoutPrediction
                        ? PyramidSeedPreScreenLoopStatus.PredictionUnavailable
                        : PyramidSeedPreScreenLoopStatus.RequiredPredictionUnavailable,
                    attempt,
                    AcceptedSeed: null,
                    detail);
            }

            if (prediction.AcceptSeed)
            {
                logInfo(
                    $"World seed pre-screen accepted seed {readResult.SeedText}: " +
                    $"attempts={attempt}, readAttempts={readResult.ReadAttempts}, " +
                    $"detail={prediction.Detail}.");
                return new PyramidSeedPreScreenLoopResult(
                    PyramidSeedPreScreenLoopStatus.Accepted,
                    attempt,
                    readResult.SeedText,
                    "accepted");
            }

            logInfo(
                $"World seed pre-screen rejected seed {readResult.SeedText}: " +
                $"attempt={attempt}, batchIndex={decisionIndex}, readAttempts={readResult.ReadAttempts}, " +
                $"detail={prediction.Detail}.");
        }
    }

    private async Task<PyramidSeedPreScreenLoopResult> RunSerialAsync(
        AutoCreateWorldSettings settings,
        TerrariaWorldGenerationVersion worldGenerationVersion,
        IPyramidSeedRandomizer randomizer,
        IPyramidVisibleSeedReader seedReader,
        CancellationToken cancellationToken,
        int initialAttempt = 0,
        string? initialVisibleSeed = null,
        int initialConsecutiveCandidateFailures = 0)
    {
        int attempt = initialAttempt;
        int consecutiveCandidateFailures =
            initialConsecutiveCandidateFailures;
        int consecutiveSeedReadFailures = 0;
        string? lastVisibleSeed = initialVisibleSeed ?? seedReader.ReadCurrentSeed();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempt++;
            string? seedBeforeRandomize = seedReader.ReadCurrentSeed() ?? lastVisibleSeed;

            if (!await randomizer.RandomizeVisibleSeedAsync(attempt, cancellationToken))
            {
                return new PyramidSeedPreScreenLoopResult(
                    PyramidSeedPreScreenLoopStatus.RandomizeFailed,
                    attempt,
                    AcceptedSeed: null,
                    Detail: "Visible seed randomize click failed.");
            }

            PyramidVisibleSeedReadResult readResult =
                await seedReader.WaitForSeedAfterRandomizeAsync(
                    seedBeforeRandomize,
                    cancellationToken);
            if (!readResult.Success)
            {
                consecutiveSeedReadFailures++;
                string detail =
                    $"No new visible seed was observed after randomize. previousSeed={seedBeforeRandomize ?? "unknown"}, " +
                    $"lastSeed={readResult.LastSeedText}, lastStatus={readResult.LastStatus}, readAttempts={readResult.ReadAttempts}, " +
                    $"consecutiveFailures={consecutiveSeedReadFailures}.";
                if (consecutiveSeedReadFailures >= MaxConsecutiveSeedReadFailures)
                {
                    logInfo("Pyramid seed pre-screen will continue without prediction: " + detail);
                    return new PyramidSeedPreScreenLoopResult(
                        PyramidSeedPreScreenLoopStatus.SeedReadFailed,
                        attempt,
                        AcceptedSeed: null,
                        detail);
                }

                logInfo("Pyramid seed pre-screen will retry: " + detail);
                continue;
            }

            consecutiveSeedReadFailures = 0;
            lastVisibleSeed = readResult.SeedText;
            WorldSeedFilterPrediction prediction = await evaluator.EvaluateAsync(
                settings,
                readResult.SeedText,
                worldGenerationVersion,
                cancellationToken);
            consecutiveCandidateFailures =
                WorldSeedFilterFailurePolicy.Advance(
                    consecutiveCandidateFailures,
                    prediction);
            if (prediction.IsCandidateFailure)
            {
                logInfo(
                    $"World seed pre-screen skipped seed {readResult.SeedText} " +
                    $"after a generation failure: {prediction.Detail}");
                if (WorldSeedFilterFailurePolicy.ShouldStop(
                        consecutiveCandidateFailures))
                {
                    return new PyramidSeedPreScreenLoopResult(
                        PyramidSeedPreScreenLoopStatus.CandidateFailuresExceeded,
                        attempt,
                        AcceptedSeed: null,
                        WorldSeedFilterFailurePolicy.FormatLimitReached(
                            consecutiveCandidateFailures,
                            prediction));
                }

                continue;
            }

            if (!prediction.CanUsePrediction)
            {
                string detail =
                    $"Seed {readResult.SeedText} could not be predicted: {prediction.Detail}.";
                logInfo(
                    prediction.CanContinueWithoutPrediction
                        ? "World seed pre-screen will continue with pyramid post-verification: " + detail
                        : "World seed pre-screen failed closed: " + detail);
                return new PyramidSeedPreScreenLoopResult(
                    prediction.CanContinueWithoutPrediction
                        ? PyramidSeedPreScreenLoopStatus.PredictionUnavailable
                        : PyramidSeedPreScreenLoopStatus.RequiredPredictionUnavailable,
                    attempt,
                    AcceptedSeed: null,
                    detail);
            }

            if (prediction.AcceptSeed)
            {
                logInfo(
                    $"World seed pre-screen accepted seed {readResult.SeedText}: " +
                    $"attempts={attempt}, readAttempts={readResult.ReadAttempts}, " +
                    $"detail={prediction.Detail}.");
                return new PyramidSeedPreScreenLoopResult(
                    PyramidSeedPreScreenLoopStatus.Accepted,
                    attempt,
                    readResult.SeedText,
                    "accepted");
            }

            logInfo(
                $"World seed pre-screen rejected seed {readResult.SeedText}: " +
                $"attempt={attempt}, readAttempts={readResult.ReadAttempts}, " +
                $"detail={prediction.Detail}.");
        }
    }
}

internal enum PyramidSeedPreScreenLoopStatus
{
    Accepted,
    RandomizeFailed,
    SeedReadFailed,
    PredictionUnavailable,
    RequiredPredictionUnavailable,
    CandidateFailuresExceeded,
}

internal readonly record struct PyramidSeedPreScreenLoopResult(
    PyramidSeedPreScreenLoopStatus Status,
    int Attempts,
    string? AcceptedSeed,
    string Detail)
{
    public bool Accepted => Status == PyramidSeedPreScreenLoopStatus.Accepted;
}

internal readonly record struct PyramidVisibleSeedReadResult(
    bool Success,
    string SeedText,
    TerrariaWorldCreationSeedStatus LastStatus,
    int ReadAttempts,
    string LastSeedText)
{
    public static PyramidVisibleSeedReadResult FromSeed(string seedText, int readAttempts) =>
        new(true, seedText, TerrariaWorldCreationSeedStatus.Seed, readAttempts, seedText);

    public static PyramidVisibleSeedReadResult Failed(
        TerrariaWorldCreationSeedStatus lastStatus,
        int readAttempts,
        string lastSeedText) =>
        new(false, string.Empty, lastStatus, readAttempts, lastSeedText);
}
