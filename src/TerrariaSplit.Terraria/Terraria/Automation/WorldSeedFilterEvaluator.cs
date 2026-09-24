using System.Diagnostics;
using System.Globalization;
using TerrariaSplit.Terraria.WorldGeneration;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class WorldSeedFilterEvaluator : IDisposable
{
    private const int CpuUsagePercent = 80;
    private readonly Lazy<JungleSeedJudgeNativeClient> nativeClient;
    private readonly bool raceParallelism;
    private bool disposed;

    public WorldSeedFilterEvaluator(
        JungleSeedJudgeNativeClient? nativeClient = null,
        bool raceParallelism = false)
    {
        this.raceParallelism = raceParallelism;
        this.nativeClient = new Lazy<JungleSeedJudgeNativeClient>(
            nativeClient is null
                ? () => JungleSeedJudgeNativeClient.CreateDefault()
                : () => nativeClient,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public static bool IsEnabledFor(AutoCreateWorldSettings settings)
    {
        return PyramidSeedPreScreenEvaluator.IsEnabledFor(settings) ||
            IsJudgeFilterEnabled(settings);
    }

    public static bool IsJudgeFilterEnabled(AutoCreateWorldSettings settings)
    {
        return settings.EnableCheats &&
            AutoCreateAdvancedFilterEligibility.IsEligible(settings) &&
            (settings.RequireCrimsonBetweenDungeonAndSpawn ||
             AutoCreateResourceFilter.HasRequirements(settings));
    }

    public static int CalculateParallelism(int logicalProcessorCount)
    {
        int normalizedLogicalProcessorCount = Math.Max(1, logicalProcessorCount);
        return Math.Max(
            1,
            (int)((long)normalizedLogicalProcessorCount *
                CpuUsagePercent / 100));
    }

    public async Task<IReadOnlyList<WorldSeedFilterPrediction>> EvaluateBatchAsync(
        AutoCreateWorldSettings settings,
        IReadOnlyList<string> seedTexts,
        TerrariaWorldGenerationVersion worldGenerationVersion,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(seedTexts);

        if (!raceParallelism)
        {
            var results = new List<WorldSeedFilterPrediction>(seedTexts.Count);
            foreach (string seed in seedTexts)
                results.Add(await EvaluateAsync(settings, seed, worldGenerationVersion, cancellationToken).ConfigureAwait(false));
            return results;
        }

        var tasks = new Task<WorldSeedFilterPrediction>[seedTexts.Count];
        for (int index = 0; index < seedTexts.Count; index++)
        {
            string seedText = seedTexts[index];
            tasks[index] = Task.Run(
                () => EvaluateAsync(
                    settings,
                    seedText,
                    worldGenerationVersion,
                    cancellationToken),
                cancellationToken);
        }

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public async Task<WorldSeedFilterPrediction> EvaluateAsync(
        AutoCreateWorldSettings settings,
        string seedText,
        TerrariaWorldGenerationVersion worldGenerationVersion,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        bool pyramidEnabled = PyramidSeedPreScreenEvaluator.IsEnabledFor(settings);
        // Copied seeds prepend secret tokens; only the visible numeric seed is simulated.
        seedText = seedText[(seedText.LastIndexOf('|') + 1)..].Trim();
        bool judgeEnabled = IsJudgeFilterEnabled(settings);
        PyramidSeedPreScreenPrediction? pyramid = null;

        if (!pyramidEnabled && !judgeEnabled)
        {
            return WorldSeedFilterPrediction.Accepted(
                "filters disabled",
                pyramid,
                judge: null);
        }

        string? unsupported = UnsupportedJudgeScope(settings);
        if (unsupported is not null)
        {
            return WorldSeedFilterPrediction.Unavailable(
                unsupported,
                pyramid);
        }

        JungleSeedJudgeGameMode gameMode = ResolveGameMode(settings.WorldDifficulty);
        JungleSeedJudgeResult judge;
        Stopwatch stopwatch = Stopwatch.StartNew();
        FileAppLogger.Instance.Info(
            $"World seed judge starting seed {seedText}; mode={gameMode}.");
        try
        {
            judge = await nativeClient.Value.AnalyzeAsync(
                seedText,
                gameMode,
                cancellationToken,
                RequestedRequirements(settings),
                threads: raceParallelism ? 1 : 0).ConfigureAwait(false);
        }
        catch (Exception ex)
            when (ex is TimeoutException ||
                ex is IOException and not FileNotFoundException)
        {
            return WorldSeedFilterPrediction.Rejected(
                $"seed judge transient failure; skip seed; seed={seedText}, " +
                $"mode={gameMode}: {ex.Message}",
                pyramid,
                judge: null);
        }
        catch (Exception ex)
            when (ex is FileNotFoundException or InvalidDataException or
                InvalidOperationException)
        {
            return WorldSeedFilterPrediction.Unavailable(
                $"seed judge unavailable; seed={seedText}, mode={gameMode}: " +
                ex.Message,
                pyramid);
        }
        finally
        {
            FileAppLogger.Instance.Info(
                $"World seed judge completed seed {seedText}; mode={gameMode}; " +
                $"elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F0}.");
        }

        if (!judge.Complete)
        {
            string detail =
                $"seed judge status {judge.Status}; seed={seedText}, mode={gameMode}: " +
                judge.Detail;
            FileAppLogger.Instance.Info(detail);
            if (IsCandidateFailure(judge.Status))
            {
                return WorldSeedFilterPrediction.CandidateFailure(
                    detail,
                    pyramid,
                    judge);
            }

            return IsCandidateRejection(judge.Status)
                ? WorldSeedFilterPrediction.Rejected(
                    "seed judge skipped candidate; " + detail,
                    pyramid,
                    judge)
                : WorldSeedFilterPrediction.Unavailable(
                    detail,
                    pyramid,
                    judge);
        }

        return judge.Decision == JungleSeedJudgeDecision.Accepted
            ? WorldSeedFilterPrediction.Accepted(judge.Reason!, pyramid, judge)
            : WorldSeedFilterPrediction.Rejected(judge.Reason!, pyramid, judge);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
    }

    internal static ResourceJudgeRequirements RequestedRequirements(AutoCreateWorldSettings settings)
    {
        const int smallWorldWidth = 4200;
        bool pyramid = PyramidSeedPreScreenEvaluator.IsEnabledFor(settings);
        return new ResourceJudgeRequirements(
            PyramidItemMask: pyramid ? AutoCreatePyramidFilterItem.NormalizeMaskOrAll(settings.PyramidFilterItemMask) : 0,
            PyramidGoldMinimum: pyramid ? AutoCreatePyramidCoinPileMinimum.Normalize(settings.PyramidFilterCoinPileMinimum) : 0,
            PyramidMaximumDepth: pyramid ? AutoCreatePyramidFilterDepth.Normalize(settings.PyramidMaximumDepth) : 0,
            CrimsonMaximumDistance: settings.RequireCrimsonBetweenDungeonAndSpawn ? AutoCreateCrimsonDistance.MaximumDistanceTiles(smallWorldWidth, settings.CrimsonDistance) : 0,
            JungleMinimumY: AutoCreateJungleRouteDepth.MinimumY(settings.JungleRouteDepth),
            JungleItemMask: AutoCreateResourceFilterItem.NormalizeMask(settings.ResourceFilterItemMask),
            LifeCrystalMinimum: Math.Max(0, settings.ResourceFilterLifeCrystalMinimum),
            SpelunkerPotionMinimum: Math.Max(0, settings.ResourceFilterSpelunkerPotionMinimum),
            FeatherfallPotionMinimum: Math.Max(0, settings.ResourceFilterFeatherfallPotionMinimum),
            StarfuryMaximumDistance: AutoCreateItemDistance.NormalizeStarfury(settings.StarfuryMaximumDistance),
            FinchStaffMaximumDistance: AutoCreateItemDistance.NormalizeFinchStaff(settings.FinchStaffMaximumDistance));
    }

    private static string? UnsupportedJudgeScope(AutoCreateWorldSettings settings)
    {
        if (AutoCreateWorldSize.Normalize(settings.WorldSize) != AutoCreateWorldSize.Small)
        {
            return "seed judge supports Small worlds only";
        }
        if (AutoCreateWorldEvil.Normalize(settings.WorldEvil) != AutoCreateWorldEvil.Crimson)
        {
            return "seed judge supports Crimson worlds only";
        }
        return null;
    }

    internal static bool IsCandidateRejection(JungleSeedJudgeStatus status)
    {
        return status is JungleSeedJudgeStatus.InvalidSeed or
            JungleSeedJudgeStatus.SpecialSeedUnsupported;
    }

    internal static bool IsCandidateFailure(JungleSeedJudgeStatus status)
    {
        return status == JungleSeedJudgeStatus.GenerationFailed;
    }

    private static JungleSeedJudgeGameMode ResolveGameMode(string? difficulty)
    {
        return AutoCreateWorldDifficulty.Normalize(difficulty) switch
        {
            AutoCreateWorldDifficulty.Expert => JungleSeedJudgeGameMode.Expert,
            AutoCreateWorldDifficulty.Master => JungleSeedJudgeGameMode.Master,
            AutoCreateWorldDifficulty.Journey => JungleSeedJudgeGameMode.Journey,
            _ => JungleSeedJudgeGameMode.Classic
        };
    }
}

internal enum WorldSeedFilterPredictionKind
{
    Accepted,
    Rejected,
    CandidateFailure,
    Unavailable
}

internal readonly record struct WorldSeedFilterPrediction(
    WorldSeedFilterPredictionKind Kind,
    string Detail,
    PyramidSeedPreScreenPrediction? Pyramid,
    JungleSeedJudgeResult? Judge)
{
    public bool CanUsePrediction =>
        Kind != WorldSeedFilterPredictionKind.Unavailable;

    public bool AcceptSeed => Kind == WorldSeedFilterPredictionKind.Accepted;

    public bool IsCandidateFailure =>
        Kind == WorldSeedFilterPredictionKind.CandidateFailure;

    public bool IsFatal => Kind == WorldSeedFilterPredictionKind.Unavailable;

    public static WorldSeedFilterPrediction Accepted(
        string detail,
        PyramidSeedPreScreenPrediction? pyramid,
        JungleSeedJudgeResult? judge) =>
        new(
            WorldSeedFilterPredictionKind.Accepted,
            detail,
            pyramid,
            judge);

    public static WorldSeedFilterPrediction Rejected(
        string detail,
        PyramidSeedPreScreenPrediction? pyramid,
        JungleSeedJudgeResult? judge) =>
        new(
            WorldSeedFilterPredictionKind.Rejected,
            detail,
            pyramid,
            judge);

    public static WorldSeedFilterPrediction CandidateFailure(
        string detail,
        PyramidSeedPreScreenPrediction? pyramid,
        JungleSeedJudgeResult? judge) =>
        new(
            WorldSeedFilterPredictionKind.CandidateFailure,
            detail,
            pyramid,
            judge);

    public static WorldSeedFilterPrediction Unavailable(
        string detail,
        PyramidSeedPreScreenPrediction? pyramid,
        JungleSeedJudgeResult? judge = null) =>
        new(
            WorldSeedFilterPredictionKind.Unavailable,
            detail,
            pyramid,
            judge);
}

internal static class WorldSeedFilterFailurePolicy
{
    public const int MaximumConsecutiveCandidateFailures = 3;

    public static int Advance(
        int consecutiveCandidateFailures,
        WorldSeedFilterPrediction prediction)
    {
        return prediction.IsCandidateFailure
            ? consecutiveCandidateFailures + 1
            : 0;
    }

    public static bool ShouldStop(int consecutiveCandidateFailures)
    {
        return consecutiveCandidateFailures >=
            MaximumConsecutiveCandidateFailures;
    }

    public static string FormatLimitReached(
        int consecutiveCandidateFailures,
        WorldSeedFilterPrediction prediction)
    {
        return
            $"World seed filtering stopped after {consecutiveCandidateFailures} " +
            $"consecutive candidate generation failures. Last failure: " +
            prediction.Detail;
    }
}
