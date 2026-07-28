using System.Diagnostics;
using System.Globalization;
using TerrariaSplit.Terraria.WorldGeneration;

namespace TerrariaSplit.Terraria.Automation;

internal sealed class WorldSeedFilterEvaluator : IDisposable
{
    private const int CpuUsagePercent = 80;
    private readonly PyramidSeedPreScreenEvaluator pyramidEvaluator;
    private readonly Lazy<JungleSeedJudgeNativeClient> nativeClient;
    private bool disposed;

    public WorldSeedFilterEvaluator(
        PyramidSeedPreScreenEvaluator? pyramidEvaluator = null,
        JungleSeedJudgeNativeClient? nativeClient = null)
    {
        this.pyramidEvaluator = pyramidEvaluator ?? new PyramidSeedPreScreenEvaluator();
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
        bool judgeEnabled = IsJudgeFilterEnabled(settings);
        PyramidSeedPreScreenPrediction? pyramid = null;

        if (pyramidEnabled)
        {
            pyramid = pyramidEvaluator.Evaluate(
                settings,
                seedText,
                worldGenerationVersion);
            if (!pyramid.Value.CanUsePrediction)
            {
                return WorldSeedFilterPrediction.Unavailable(
                    pyramid.Value.RejectReason,
                    canContinueWithoutPrediction: !judgeEnabled,
                    pyramid);
            }
            if (!pyramid.Value.AcceptSeed)
            {
                return WorldSeedFilterPrediction.Rejected(
                    "pyramid: " + pyramid.Value.RejectReason,
                    pyramid,
                    judge: null);
            }
        }

        if (!judgeEnabled)
        {
            return WorldSeedFilterPrediction.Accepted(
                pyramidEnabled ? "pyramid accepted" : "filters disabled",
                pyramid,
                judge: null);
        }

        string? unsupported = UnsupportedJudgeScope(settings, worldGenerationVersion);
        if (unsupported is not null)
        {
            return WorldSeedFilterPrediction.Unavailable(
                unsupported,
                canContinueWithoutPrediction: false,
                pyramid);
        }

        JungleSeedJudgeResult judge;
        Stopwatch stopwatch = Stopwatch.StartNew();
        StaticAppLogger.Instance.Info($"World seed judge starting seed {seedText}.");
        try
        {
            judge = await nativeClient.Value.AnalyzeAsync(
                seedText,
                ResolveGameMode(settings.WorldDifficulty),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
            when (ex is TimeoutException ||
                ex is IOException and not FileNotFoundException)
        {
            return WorldSeedFilterPrediction.Rejected(
                "seed judge transient failure; skip seed: " + ex.Message,
                pyramid,
                judge: null);
        }
        catch (Exception ex)
            when (ex is FileNotFoundException or InvalidDataException or
                InvalidOperationException)
        {
            return WorldSeedFilterPrediction.Unavailable(
                "seed judge unavailable: " + ex.Message,
                canContinueWithoutPrediction: false,
                pyramid);
        }
        finally
        {
            StaticAppLogger.Instance.Info(
                $"World seed judge completed seed {seedText}; elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F0}.");
        }

        if (!judge.Complete)
        {
            string detail = $"seed judge status {judge.Status}: {judge.Detail}";
            return IsCandidateRejection(judge.Status)
                ? WorldSeedFilterPrediction.Rejected(
                    "seed judge skipped candidate; " + detail,
                    pyramid,
                    judge)
                : WorldSeedFilterPrediction.Unavailable(
                    detail,
                    canContinueWithoutPrediction: false,
                    pyramid,
                    judge);
        }

        JungleSeedFilterMatch match = JungleSeedFilterMatcher.Match(settings, judge);
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

    private static string? UnsupportedJudgeScope(
        AutoCreateWorldSettings settings,
        TerrariaWorldGenerationVersion worldGenerationVersion)
    {
        if (worldGenerationVersion != TerrariaWorldGenerationVersion.Modern1456)
        {
            return "seed judge supports Terraria 1.4.5.6 only";
        }
        if (AutoCreateWorldSize.Normalize(settings.WorldSize) != AutoCreateWorldSize.Small)
        {
            return "seed judge supports Small worlds only";
        }
        if (AutoCreateWorldEvil.Normalize(settings.WorldEvil) != AutoCreateWorldEvil.Crimson)
        {
            return "seed judge supports Crimson worlds only";
        }
        if (AutoCreateSpecialWorldSeed.ParseList(settings.SpecialSeeds).Any() ||
            AutoCreateSeedList.Parse(settings.SecretSeeds).Any())
        {
            return "seed judge does not support special or secret seeds";
        }
        return null;
    }

    internal static bool IsCandidateRejection(JungleSeedJudgeStatus status)
    {
        return status is JungleSeedJudgeStatus.InvalidSeed or
            JungleSeedJudgeStatus.SpecialSeedUnsupported or
            JungleSeedJudgeStatus.GenerationFailed;
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

internal readonly record struct WorldSeedFilterPrediction(
    bool CanUsePrediction,
    bool CanContinueWithoutPrediction,
    bool AcceptSeed,
    string Detail,
    PyramidSeedPreScreenPrediction? Pyramid,
    JungleSeedJudgeResult? Judge)
{
    public static WorldSeedFilterPrediction Accepted(
        string detail,
        PyramidSeedPreScreenPrediction? pyramid,
        JungleSeedJudgeResult? judge) =>
        new(true, false, true, detail, pyramid, judge);

    public static WorldSeedFilterPrediction Rejected(
        string detail,
        PyramidSeedPreScreenPrediction? pyramid,
        JungleSeedJudgeResult? judge) =>
        new(true, false, false, detail, pyramid, judge);

    public static WorldSeedFilterPrediction Unavailable(
        string detail,
        bool canContinueWithoutPrediction,
        PyramidSeedPreScreenPrediction? pyramid,
        JungleSeedJudgeResult? judge = null) =>
        new(false, canContinueWithoutPrediction, false, detail, pyramid, judge);
}

internal readonly record struct JungleSeedFilterMatch(bool Matches, string Detail);

internal static class JungleSeedFilterMatcher
{
    private const int SmallWorldWidth = 4200;

    public static JungleSeedFilterMatch Match(
        AutoCreateWorldSettings settings,
        JungleSeedJudgeResult result)
    {
        JungleSeedAnalysis jungle = result.Jungle ??
            throw new ArgumentException("Jungle analysis is required.", nameof(result));
        IReadOnlyList<CrimsonCorridorVertex> vertices = result.CrimsonVertices ??
            throw new ArgumentException("Crimson vertices are required.", nameof(result));

        if (settings.RequireCrimsonBetweenDungeonAndSpawn &&
            !MatchesCrimsonDistance(jungle.Side, vertices, settings.CrimsonDistance))
        {
            return new JungleSeedFilterMatch(
                false,
                $"crimson vertices outside {AutoCreateCrimsonDistance.Normalize(settings.CrimsonDistance)} corridor");
        }

        int minimumDepth = AutoCreateJungleRouteDepth.MinimumY(settings.JungleRouteDepth);
        if (jungle.Route.DeepestY < minimumDepth)
        {
            return new JungleSeedFilterMatch(
                false,
                $"jungle route depth {jungle.Route.DeepestY} < {minimumDepth}; " +
                $"routeStatus={jungle.Route.Status}");
        }

        Dictionary<string, int> counts = jungle.Resources
            .GroupBy(resource => resource.Category, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(resource => Math.Max(1, resource.Units)),
                StringComparer.Ordinal);
        int mask = AutoCreateResourceFilterItem.NormalizeMask(
            settings.ResourceFilterItemMask);
        if (!HasRequiredItem(mask, AutoCreateResourceFilterItem.BoomstickMask, "Boomstick", counts) ||
            !HasRequiredItem(mask, AutoCreateResourceFilterItem.FeralClawsMask, "FeralClaws", counts) ||
            !HasRequiredItem(mask, AutoCreateResourceFilterItem.AnkletOfTheWindMask, "Anklet", counts))
        {
            return new JungleSeedFilterMatch(false, "required jungle-route item missing");
        }

        if (!HasMinimum("LifeCrystal", settings.ResourceFilterLifeCrystalMinimum, counts) ||
            !HasMinimum("SpelunkerPotion", settings.ResourceFilterSpelunkerPotionMinimum, counts) ||
            !HasMinimum("FeatherfallPotion", settings.ResourceFilterFeatherfallPotionMinimum, counts))
        {
            return new JungleSeedFilterMatch(false, "required jungle-route resource count missing");
        }

        string summary = string.Join(
            ",",
            counts.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + "=" + pair.Value.ToString(CultureInfo.InvariantCulture)));
        return new JungleSeedFilterMatch(
            true,
            $"judge accepted; routeStatus={jungle.Route.Status}; " +
            $"jungleDepth={jungle.Route.DeepestY}; resources=[{summary}]");
    }

    private static bool MatchesCrimsonDistance(
        string jungleSide,
        IReadOnlyList<CrimsonCorridorVertex> vertices,
        string crimsonDistance)
    {
        int spawnX = SmallWorldWidth / 2;
        int maximumDistance =
            AutoCreateCrimsonDistance.MaximumDistanceTiles(SmallWorldWidth, crimsonDistance);
        bool dungeonOnLeft = string.Equals(jungleSide, "Right", StringComparison.Ordinal);
        return vertices.Any(vertex =>
            dungeonOnLeft
                ? vertex.X < spawnX && vertex.X >= spawnX - maximumDistance
                : vertex.X > spawnX && vertex.X <= spawnX + maximumDistance);
    }

    private static bool HasRequiredItem(
        int selectedMask,
        int itemMask,
        string category,
        IReadOnlyDictionary<string, int> counts)
    {
        return (selectedMask & itemMask) == 0 || counts.GetValueOrDefault(category) > 0;
    }

    private static bool HasMinimum(
        string category,
        int minimum,
        IReadOnlyDictionary<string, int> counts)
    {
        return counts.GetValueOrDefault(category) >= Math.Max(0, minimum);
    }
}
