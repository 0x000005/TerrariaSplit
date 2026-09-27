using System.Globalization;
using TerrariaSplit.Terraria.WorldGeneration;

namespace TerrariaSplit.Terraria.Automation;

// Only seed evaluation is parallel. Accepted seeds are generated into world files serially.
internal sealed class WorldPoolSeedFilter : IDisposable
{
    private readonly WorldSeedFilterEvaluator evaluator;
    private readonly Func<string> nextSeed;
    private int consecutiveCandidateFailures;

    public WorldPoolSeedFilter(int logicalProcessorCount, JungleSeedJudgeNativeClient? nativeClient = null,
        Func<string>? nextSeed = null)
    {
        Concurrency = CalculateConcurrency(logicalProcessorCount);
        evaluator = new WorldSeedFilterEvaluator(nativeClient, parallelCandidates: true);
        this.nextSeed = nextSeed ?? (() => TerrariaSeedRandom.NextShared().ToString(CultureInfo.InvariantCulture));
    }

    public int Concurrency { get; }

    public static int CalculateConcurrency(int logicalProcessorCount) =>
        (int)((Math.Max(1L, logicalProcessorCount) + 4) / 5);

    public void Reset() => consecutiveCandidateFailures = 0;

    public async Task<WorldPoolSeedFilterResult> FilterBatchAsync(AutoCreateWorldSettings settings,
        TerrariaWorldGenerationVersion version, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string[] seeds = Enumerable.Range(0, Concurrency).Select(_ => nextSeed()).ToArray();
        IReadOnlyList<WorldSeedFilterPrediction> predictions = await evaluator.EvaluateBatchAsync(
            settings, seeds, version, cancellationToken).ConfigureAwait(false);
        var accepted = new List<string>();
        for (int index = 0; index < predictions.Count; index++)
        {
            WorldSeedFilterPrediction prediction = predictions[index];
            consecutiveCandidateFailures = WorldSeedFilterFailurePolicy.Advance(consecutiveCandidateFailures, prediction);
            if (prediction.IsFatal || WorldSeedFilterFailurePolicy.ShouldStop(consecutiveCandidateFailures))
            {
                string detail = prediction.IsFatal ? prediction.Detail :
                    WorldSeedFilterFailurePolicy.FormatLimitReached(consecutiveCandidateFailures, prediction);
                Reset();
                return new WorldPoolSeedFilterResult([], detail);
            }
            if (prediction.AcceptSeed) accepted.Add(seeds[index]);
        }
        return new WorldPoolSeedFilterResult(accepted, null);
    }

    public void Dispose() => evaluator.Dispose();
}

internal readonly record struct WorldPoolSeedFilterResult(IReadOnlyList<string> AcceptedSeeds, string? Failure);
