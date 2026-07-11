using TerrariaSplit.Terraria.WorldGeneration;

namespace TerrariaSplit.Terraria.Automation;

internal interface IPyramidSeedRandomizer
{
    Task<bool> RandomizeVisibleSeedAsync(int attempt, CancellationToken cancellationToken);
}

internal interface IPyramidVisibleSeedReader
{
    string? ReadCurrentSeed();

    Task<PyramidVisibleSeedReadResult> WaitForSeedAfterRandomizeAsync(
        string? previousSeedText,
        CancellationToken cancellationToken);
}

internal sealed class PyramidSeedPreScreenLoop
{
    private const int MaxConsecutiveSeedReadFailures = 3;

    private readonly IPyramidSeedPreScreenEvaluator evaluator;
    private readonly Action<string> logInfo;

    public PyramidSeedPreScreenLoop(
        IPyramidSeedPreScreenEvaluator evaluator,
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
        int attempt = 0;
        int consecutiveSeedReadFailures = 0;
        string? lastVisibleSeed = seedReader.ReadCurrentSeed();

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
                await seedReader.WaitForSeedAfterRandomizeAsync(seedBeforeRandomize, cancellationToken);
            if (!readResult.Success)
            {
                consecutiveSeedReadFailures++;
                string detail =
                    $"No new visible seed was observed after randomize. previousSeed={seedBeforeRandomize ?? "unknown"}, " +
                    $"lastSeed={readResult.LastSeedText}, lastStatus={readResult.LastStatus}, readAttempts={readResult.ReadAttempts}, " +
                    $"consecutiveFailures={consecutiveSeedReadFailures}.";
                if (settings.ReturnToMainMenuOnFilterFailure ||
                    consecutiveSeedReadFailures >= MaxConsecutiveSeedReadFailures)
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
            PyramidSeedPreScreenPrediction prediction = evaluator.Evaluate(
                settings,
                readResult.SeedText,
                worldGenerationVersion);
            if (!prediction.CanUsePrediction)
            {
                string detail =
                    $"Seed {readResult.SeedText} could not be predicted: {prediction.RejectReason}; " +
                    $"detail={prediction.Result.Detail}, scanMs={prediction.Result.DurationMilliseconds}.";
                logInfo("Pyramid seed pre-screen will continue without prediction: " + detail);
                return new PyramidSeedPreScreenLoopResult(
                    PyramidSeedPreScreenLoopStatus.PredictionUnavailable,
                    attempt,
                    AcceptedSeed: null,
                    detail);
            }

            if (prediction.AcceptSeed)
            {
                logInfo(
                    $"Pyramid seed pre-screen accepted seed {readResult.SeedText}: " +
                    $"requiredItems={prediction.RequiredItems}, itemMatch={prediction.Result.MatchesRequiredItems}, " +
                    $"class={prediction.Result.TargetClass}, loot={prediction.Result.LootSummary}, " +
                    $"attempts={attempt}, scanMs={prediction.Result.DurationMilliseconds}, " +
                    $"readAttempts={readResult.ReadAttempts}.");
                return new PyramidSeedPreScreenLoopResult(
                    PyramidSeedPreScreenLoopStatus.Accepted,
                    attempt,
                    readResult.SeedText,
                    "accepted");
            }

            logInfo(
                $"Pyramid seed pre-screen rejected seed {readResult.SeedText}: {prediction.RejectReason}, " +
                $"requiredItems={prediction.RequiredItems}, itemMatch={prediction.Result.MatchesRequiredItems}, " +
                $"class={prediction.Result.TargetClass}, loot={prediction.Result.LootSummary}, " +
                $"attempt={attempt}, scanMs={prediction.Result.DurationMilliseconds}, " +
                $"readAttempts={readResult.ReadAttempts}.");
            if (settings.ReturnToMainMenuOnFilterFailure)
            {
                return new PyramidSeedPreScreenLoopResult(
                    PyramidSeedPreScreenLoopStatus.RejectedSeed,
                    attempt,
                    AcceptedSeed: null,
                    prediction.RejectReason);
            }
        }
    }
}

internal enum PyramidSeedPreScreenLoopStatus
{
    Accepted,
    RejectedSeed,
    RandomizeFailed,
    SeedReadFailed,
    PredictionUnavailable,
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
