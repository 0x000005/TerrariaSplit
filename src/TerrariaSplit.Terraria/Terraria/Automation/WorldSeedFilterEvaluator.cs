using System.Diagnostics;
using System.Globalization;
using TerrariaSplit.Compatibility;
using TerrariaSplit.Terraria.WorldGeneration;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class WorldSeedFilterEvaluator : IDisposable
{
    private const int CpuUsagePercent = 80;
    private readonly Lazy<JungleSeedJudgeNativeClient> nativeClient;
    private readonly bool parallelCandidates;
    private bool disposed;

    public WorldSeedFilterEvaluator(
        JungleSeedJudgeNativeClient? nativeClient = null,
        bool parallelCandidates = false)
    {
        this.parallelCandidates = parallelCandidates;
        this.nativeClient = new Lazy<JungleSeedJudgeNativeClient>(
            nativeClient is null
                ? () => JungleSeedJudgeNativeClient.CreateDefault()
                : () => nativeClient,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public static bool IsEnabledFor(AutoCreateWorldSettings settings)
    {
        return IsJudgeFilterEnabled(settings);
    }

    public static bool IsJudgeFilterEnabled(AutoCreateWorldSettings settings)
    {
        return settings.EnableCheats &&
            AutoCreateAdvancedFilterEligibility.IsEligible(settings) &&
            (settings.EnablePyramidFilter || (settings.RequireCrimsonBetweenDungeonAndSpawn && AutoCreateAdvancedFilterEligibility.IsCrimsonEligible(settings.WorldEvil)) ||
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
        TerrariaResourceJudgeVersion judgeVersion,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(seedTexts);

        if (!parallelCandidates)
        {
            var results = new List<WorldSeedFilterPrediction>(seedTexts.Count);
            foreach (string seed in seedTexts)
                results.Add(await EvaluateAsync(settings, seed, judgeVersion, cancellationToken).ConfigureAwait(false));
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
                    judgeVersion,
                    cancellationToken),
                cancellationToken);
        }

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public async Task<WorldSeedFilterPrediction> EvaluateAsync(
        AutoCreateWorldSettings settings, string seedText,
        TerrariaResourceJudgeVersion judgeVersion, CancellationToken cancellationToken)
    {
        string traceId = Guid.NewGuid().ToString("N");
        var clock = Stopwatch.StartNew();
        WorldFilterTrace.Write("candidate.start", new { traceId, seedText, judgeVersion,
            settings.WorldDifficulty, settings.WorldSize, settings.EnableCheats, parallelCandidates,
            requirements = RequestedRequirements(settings, judgeVersion), requestedThreads = parallelCandidates ? 1 : 0 });
        try
        {
            var result = await EvaluateCoreAsync(settings, seedText, judgeVersion, cancellationToken).ConfigureAwait(false);
            WorldFilterTrace.Write("candidate.end", new { traceId, seedText, elapsedMs = clock.Elapsed.TotalMilliseconds,
                result.Kind, result.Detail, result.AcceptSeed, result.CanUsePrediction, result.IsCandidateFailure,
                hasNativeResult = result.Judge is not null, judge = result.Judge });
            return result;
        }
        catch (Exception ex)
        {
            WorldFilterTrace.Write("candidate.error", new { traceId, seedText,
                elapsedMs = clock.Elapsed.TotalMilliseconds, cancelled = cancellationToken.IsCancellationRequested,
                error = ex.ToString() });
            throw;
        }
    }

    private async Task<WorldSeedFilterPrediction> EvaluateCoreAsync(
        AutoCreateWorldSettings settings,
        string seedText,
        TerrariaResourceJudgeVersion judgeVersion,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        // Copied seeds prepend secret tokens; only the visible numeric seed is simulated.
        seedText = seedText[(seedText.LastIndexOf('|') + 1)..].Trim();
        bool judgeEnabled = IsJudgeFilterEnabled(settings);
        cancellationToken.ThrowIfCancellationRequested();
        if (!judgeEnabled)
        {
            return WorldSeedFilterPrediction.Accepted(
                "filters disabled",
                judge: null);
        }

        string? unsupported = UnsupportedJudgeScope(settings);
        if (unsupported is not null)
        {
            return WorldSeedFilterPrediction.Unavailable(
                unsupported);
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
                RequestedRequirements(settings, judgeVersion),
                threads: parallelCandidates ? 1 : 0).ConfigureAwait(false);
        }
        catch (Exception ex)
            when (ex is TimeoutException ||
                ex is IOException and not FileNotFoundException)
        {
            return WorldSeedFilterPrediction.Rejected(
                $"seed judge transient failure; skip seed; seed={seedText}, " +
                $"mode={gameMode}: {ex.Message}",
                judge: null);
        }
        catch (Exception ex)
            when (ex is FileNotFoundException or InvalidDataException or
                InvalidOperationException)
        {
            return WorldSeedFilterPrediction.Unavailable(
                $"seed judge unavailable; seed={seedText}, mode={gameMode}: " +
                ex.Message);
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
                    judge);
            }

            return IsCandidateRejection(judge.Status)
                ? WorldSeedFilterPrediction.Rejected(
                    "seed judge skipped candidate; " + detail,
                    judge)
                : WorldSeedFilterPrediction.Unavailable(
                    detail,
                    judge);
        }

        return judge.Decision == JungleSeedJudgeDecision.Accepted
            ? WorldSeedFilterPrediction.Accepted(judge.Reason!, judge)
            : WorldSeedFilterPrediction.Rejected(judge.Reason!, judge);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
    }

    internal static ResourceJudgeRequirements RequestedRequirements(AutoCreateWorldSettings settings,
        TerrariaResourceJudgeVersion judgeVersion = TerrariaResourceJudgeVersion.Version1461)
    {
        int worldWidth = AutoCreateWorldSize.Width(settings.WorldSize);
        int worldHeight = AutoCreateWorldSize.Height(settings.WorldSize);
        bool pyramid = settings.EnableCheats && settings.EnablePyramidFilter && AutoCreateAdvancedFilterEligibility.IsEligible(settings);
        return new ResourceJudgeRequirements(
            PyramidItemMask: pyramid ? AutoCreatePyramidFilterItem.NormalizeMaskOrAll(settings.PyramidFilterItemMask) : 0,
            PyramidGoldMinimum: pyramid ? AutoCreatePyramidCoinPileMinimum.Normalize(settings.PyramidFilterCoinPileMinimum) : 0,
            PyramidMaximumDepth: pyramid ? AutoCreatePyramidFilterDepth.Normalize(settings.PyramidMaximumDepth) : 0,
            CrimsonMaximumDistance: settings.RequireCrimsonBetweenDungeonAndSpawn && AutoCreateAdvancedFilterEligibility.IsCrimsonEligible(settings.WorldEvil) ? AutoCreateCrimsonDistance.MaximumDistanceTiles(worldWidth, settings.CrimsonDistance) : 0,
            JungleMinimumY: AutoCreateJungleRouteDepth.MinimumY(settings.JungleRouteDepth, worldHeight),
            JungleItemMask: AutoCreateResourceFilterItem.NormalizeMask(settings.ResourceFilterItemMask),
            LifeCrystalMinimum: Math.Max(0, settings.ResourceFilterLifeCrystalMinimum),
            SpelunkerPotionMinimum: Math.Max(0, settings.ResourceFilterSpelunkerPotionMinimum),
            FeatherfallPotionMinimum: Math.Max(0, settings.ResourceFilterFeatherfallPotionMinimum),
            StarfuryMaximumDistance: AutoCreateItemDistance.StarfuryMaximum(settings.StarfuryMaximumDistance, worldWidth),
            FinchStaffMaximumDistance: AutoCreateItemDistance.FinchStaffMaximum(settings.FinchStaffMaximumDistance, worldWidth),
            WorldSize: TerrariaWorldSeedOptions.SizeCode(settings.WorldSize),
            Evil: AutoCreateWorldEvil.Normalize(settings.WorldEvil) switch
            {
                AutoCreateWorldEvil.Crimson => TerrariaWorldSeedOptions.CrimsonEvilCode,
                AutoCreateWorldEvil.Corruption => TerrariaWorldSeedOptions.CorruptionEvilCode,
                _ => throw new InvalidOperationException("Random world evil must be resolved before seed evaluation.")
            },
            TerrariaVersion: TerrariaResourceJudgeBuilds.ForVersion(judgeVersion).Version);
    }

    private static string? UnsupportedJudgeScope(AutoCreateWorldSettings settings)
    {
        if (AutoCreateWorldEvil.Normalize(settings.WorldEvil) == AutoCreateWorldEvil.Random)
        {
            return "Random world evil must be resolved before seed evaluation.";
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
        JungleSeedJudgeResult? judge) =>
        new(
            WorldSeedFilterPredictionKind.Accepted,
            detail,
            judge);

    public static WorldSeedFilterPrediction Rejected(
        string detail,
        JungleSeedJudgeResult? judge) =>
        new(
            WorldSeedFilterPredictionKind.Rejected,
            detail,
            judge);

    public static WorldSeedFilterPrediction CandidateFailure(
        string detail,
        JungleSeedJudgeResult? judge) =>
        new(
            WorldSeedFilterPredictionKind.CandidateFailure,
            detail,
            judge);

    public static WorldSeedFilterPrediction Unavailable(
        string detail,
        JungleSeedJudgeResult? judge = null) =>
        new(
            WorldSeedFilterPredictionKind.Unavailable,
            detail,
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
