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

        if (pyramidEnabled)
        {
            pyramid = new PyramidSeedPreScreenEvaluator().Evaluate(settings, seedText, worldGenerationVersion);
            if (pyramid.Value.Result.Status == PyramidSeedPreScreenStatus.Error)
                return WorldSeedFilterPrediction.CandidateFailure(pyramid.Value.RejectReason, pyramid, null);
            if (pyramid.Value.CanUsePrediction && !pyramid.Value.AcceptSeed)
                return WorldSeedFilterPrediction.Rejected("pyramid pre-screen: " + pyramid.Value.RejectReason, pyramid, null);
            // A pre-screen pass never authorizes a seed; ResourceJudge makes the final decision.
        }

        if (!pyramidEnabled && !judgeEnabled)
        {
            return WorldSeedFilterPrediction.Accepted(
                "filters disabled",
                pyramid,
                judge: null);
        }

        string? unsupported = UnsupportedJudgeScope(settings, worldGenerationVersion);
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
                RequestedAnalysis(settings),
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

        JungleSeedFilterMatch match = JungleSeedFilterMatcher.Match(settings, judge);
        if (match.IsUncertain)
        {
            return WorldSeedFilterPrediction.CandidateFailure(
                "Resource analysis uncertain: " + match.Detail, pyramid, judge);
        }
        return match.Matches
            ? WorldSeedFilterPrediction.Accepted(match.Detail, pyramid, judge)
            : WorldSeedFilterPrediction.Rejected(match.Detail, pyramid, judge);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
    }

    internal static int RequestedAnalysis(AutoCreateWorldSettings settings)
    {
        int mask = 0;
        if (PyramidSeedPreScreenEvaluator.IsEnabledFor(settings))
        {
            mask |= ResourceJudgeAnalysis.PyramidItems;
            if (settings.PyramidFilterCoinPileMinimum > 0) mask |= ResourceJudgeAnalysis.PyramidGold;
            if (AutoCreatePyramidFilterDepth.Normalize(settings.PyramidMaximumDepth) > 0)
                mask |= ResourceJudgeAnalysis.PyramidDepth;
        }
        if (settings.RequireCrimsonBetweenDungeonAndSpawn) mask |= ResourceJudgeAnalysis.Crimson;
        if (AutoCreateJungleRouteDepth.MinimumY(settings.JungleRouteDepth) > 0) mask |= ResourceJudgeAnalysis.JungleRoute;
        if (AutoCreateResourceFilterItem.NormalizeMask(settings.ResourceFilterItemMask) != 0) mask |= ResourceJudgeAnalysis.JungleItems;
        if (settings.ResourceFilterLifeCrystalMinimum > 0) mask |= ResourceJudgeAnalysis.LifeCrystals;
        if (settings.ResourceFilterSpelunkerPotionMinimum > 0) mask |= ResourceJudgeAnalysis.SpelunkerPotions;
        if (settings.ResourceFilterFeatherfallPotionMinimum > 0) mask |= ResourceJudgeAnalysis.FeatherfallPotions;
        return mask;
    }

    private static string? UnsupportedJudgeScope(
        AutoCreateWorldSettings settings,
        TerrariaWorldGenerationVersion worldGenerationVersion)
    {
        if (worldGenerationVersion != TerrariaWorldGenerationVersion.Modern1458)
        {
            return "seed judge supports Terraria 1.4.5.8 only";
        }
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

internal readonly record struct JungleSeedFilterMatch(bool Matches, string Detail, bool IsUncertain = false);

internal static class JungleSeedFilterMatcher
{
    private const int SmallWorldWidth = 4200;

    public static JungleSeedFilterMatch Match(
        AutoCreateWorldSettings settings,
        JungleSeedJudgeResult result)
    {
        ResourceJudgeMetrics metrics = result.Metrics ??
            throw new ArgumentException("Requested measurements are required.", nameof(result));
        JungleSeedAnalysis? jungle = result.Jungle;
        bool uncertain = jungle is not null && (jungle.AnalysisStatus == JungleSeedAnalysisStatus.Uncertain ||
            jungle.Route.Status != JungleRouteStatus.Complete);

        int requestedAnalysis = WorldSeedFilterEvaluator.RequestedAnalysis(settings);
        if ((requestedAnalysis & (ResourceJudgeAnalysis.PyramidItems | ResourceJudgeAnalysis.PyramidGold | ResourceJudgeAnalysis.PyramidDepth)) != 0)
        {
            int items = AutoCreatePyramidFilterItem.NormalizeMaskOrAll(settings.PyramidFilterItemMask);
            int gold = AutoCreatePyramidCoinPileMinimum.Normalize(settings.PyramidFilterCoinPileMinimum);
            bool checkItems = (requestedAnalysis & ResourceJudgeAnalysis.PyramidItems) != 0;
            bool checkGold = (requestedAnalysis & ResourceJudgeAnalysis.PyramidGold) != 0;
            bool checkDepth = (requestedAnalysis & ResourceJudgeAnalysis.PyramidDepth) != 0;
            if (result.Pyramids is null || !result.Pyramids.Any(p =>
                (!checkItems || (p.ItemMask.GetValueOrDefault() & items) != 0) &&
                (!checkGold || gold == 0 || p.GoldCoinPileCount >= gold) &&
                (!checkDepth || p.TunnelSurfaceDistance is { } distance && AutoCreatePyramidFilterDepth.Matches(distance, settings.PyramidMaximumDepth))))
                return new JungleSeedFilterMatch(false, "pyramid item, gold pile or depth requirements not met by one pyramid");
        }

        if (settings.RequireCrimsonBetweenDungeonAndSpawn &&
            (metrics.NearestDungeonSideCrimsonDistance is not { } crimsonDistance ||
             crimsonDistance > AutoCreateCrimsonDistance.MaximumDistanceTiles(SmallWorldWidth, settings.CrimsonDistance)))
        {
            return new JungleSeedFilterMatch(
                false,
                $"crimson vertices outside {AutoCreateCrimsonDistance.Normalize(settings.CrimsonDistance)} corridor");
        }

        int minimumDepth = AutoCreateJungleRouteDepth.MinimumY(settings.JungleRouteDepth);
        if (minimumDepth > 0 && (metrics.JungleRouteDeepestY is not { } depth || depth < minimumDepth))
        {
            return new JungleSeedFilterMatch(
                false,
                $"jungle route depth {metrics.JungleRouteDeepestY} < {minimumDepth}; " +
                $"routeStatus={jungle?.Route.Status}", IsUncertain: uncertain);
        }

        int mask = AutoCreateResourceFilterItem.NormalizeMask(
            settings.ResourceFilterItemMask);
        if ((metrics.JungleItemMask.GetValueOrDefault() & mask) != mask)
        {
            return new JungleSeedFilterMatch(false, "required jungle-route item missing",
                IsUncertain: uncertain);
        }

        if (metrics.LifeCrystalCount.GetValueOrDefault() < Math.Max(0, settings.ResourceFilterLifeCrystalMinimum) ||
            metrics.SpelunkerPotionCount.GetValueOrDefault() < Math.Max(0, settings.ResourceFilterSpelunkerPotionMinimum) ||
            metrics.FeatherfallPotionCount.GetValueOrDefault() < Math.Max(0, settings.ResourceFilterFeatherfallPotionMinimum))
        {
            return new JungleSeedFilterMatch(false, "required jungle-route resource count missing",
                IsUncertain: uncertain);
        }

        return new JungleSeedFilterMatch(
            true,
            $"judge accepted; endPass={result.CheckpointPassIndex}; routeStatus={jungle?.Route.Status}; " +
            $"jungleDepth={metrics.JungleRouteDeepestY}; itemMask={metrics.JungleItemMask}; " +
            $"lifeCrystals={metrics.LifeCrystalCount}; spelunker={metrics.SpelunkerPotionCount}; featherfall={metrics.FeatherfallPotionCount}");
    }
}
