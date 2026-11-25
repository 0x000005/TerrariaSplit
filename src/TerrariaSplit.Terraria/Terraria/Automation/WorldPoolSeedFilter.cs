using System.Globalization;
using TerrariaSplit.Compatibility;
using TerrariaSplit.Terraria.WorldGeneration;

namespace TerrariaSplit.Terraria.Automation;

// Only seed evaluation is parallel. Accepted seeds are generated into world files serially.
internal sealed class WorldPoolSeedFilter : IDisposable
{
    private readonly WorldSeedFilterEvaluator evaluator;
    private readonly Func<string> nextSeed;
    private readonly Func<int> nextEvilCode;
    private int consecutiveCandidateFailures;

    public WorldPoolSeedFilter(int logicalProcessorCount, JungleSeedJudgeNativeClient? nativeClient = null,
        Func<string>? nextSeed = null, Func<int>? nextEvilCode = null)
    {
        Concurrency = CalculateConcurrency(logicalProcessorCount);
        evaluator = new WorldSeedFilterEvaluator(nativeClient, parallelCandidates: true);
        this.nextSeed = nextSeed ?? (() => TerrariaSeedRandom.NextShared().ToString(CultureInfo.InvariantCulture));
        this.nextEvilCode = nextEvilCode ?? (() => TerrariaSeedRandom.NextShared(2) + 1);
    }

    public int Concurrency { get; }

    public static int CalculateConcurrency(int logicalProcessorCount) =>
        (int)((Math.Max(1L, logicalProcessorCount) + 4) / 5);

    public void Reset() => consecutiveCandidateFailures = 0;

    public async Task<WorldPoolSeedFilterResult> FilterBatchAsync(AutoCreateWorldSettings settings,
        TerrariaResourceJudgeVersion version, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var candidates = Enumerable.Range(0, Concurrency).Select(_ =>
            (Seed: nextSeed(), Settings: TerrariaWorldSeedOptions.ResolveRandomEvil(settings, nextEvilCode))).ToArray();
        WorldSeedFilterPrediction[] predictions = await Task.WhenAll(candidates.Select(candidate => Task.Run(
            () => evaluator.EvaluateAsync(candidate.Settings, candidate.Seed, version, cancellationToken), cancellationToken))).ConfigureAwait(false);
        var accepted = new List<WorldPoolSeedCandidate>();
        for (int index = 0; index < predictions.Length; index++)
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
            if (prediction.AcceptSeed) accepted.Add(new(candidates[index].Seed,
                AutoCreateAdvancedFilterEligibility.IsCrimsonEligible(candidates[index].Settings.WorldEvil)));
        }
        return new WorldPoolSeedFilterResult(accepted, null);
    }

    public void Dispose() => evaluator.Dispose();
}

internal readonly record struct WorldPoolSeedCandidate(string SeedText, bool HasCrimson)
{
    public AutoCreateWorldSettings GenerationSettings(AutoCreateWorldSettings settings)
    {
        int evilCode = HasCrimson ? TerrariaWorldSeedOptions.CrimsonEvilCode : TerrariaWorldSeedOptions.CorruptionEvilCode;
        return TerrariaWorldSeedOptions.ResolveRandomEvil(settings, () => evilCode);
    }
}

internal readonly record struct WorldPoolSeedFilterResult(IReadOnlyList<WorldPoolSeedCandidate> AcceptedSeeds, string? Failure);
