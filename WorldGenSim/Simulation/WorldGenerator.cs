using System.Diagnostics;

namespace WorldGenSim.Simulation;

internal sealed class WorldGenerator
{
    private readonly List<GenPass> passes = [];
    private readonly int seed;
    private readonly GenerationProgress progress;
    private readonly WorldGenState? state;

    public WorldGenerator(int seed, GenerationProgress? progress = null, WorldGenState? state = null)
    {
        this.seed = seed;
        this.progress = progress ?? new GenerationProgress();
        this.state = state;
    }

    public WorldGenerator(WorldGenState state, GenerationProgress? progress = null)
        : this(state.Options.Seed, progress, state)
    {
    }

    public IReadOnlyList<GenPass> Passes => passes;

    public List<GenPassResult> PassResults { get; } = [];

    public void Append(GenPass pass)
    {
        passes.Add(pass);
    }

    public WorldGenerationRunResult RunUntilInclusive(string? stopAfterPassName = null)
    {
        var context = state is null ? new WorldGenContext(seed) : new WorldGenContext(state);
        double totalWeight = passes.Where(static pass => pass.Enabled).Sum(static pass => pass.Weight);
        double completedWeight = 0d;

        foreach (GenPass pass in passes)
        {
            GenPassResult result = RunPass(context, pass);
            PassResults.Add(result);
            if (pass.Enabled)
            {
                completedWeight += pass.Weight;
                progress.SetTotal(totalWeight <= 0d ? 1d : completedWeight / totalWeight);
            }

            if (string.Equals(pass.Name, stopAfterPassName, StringComparison.Ordinal))
            {
                return new WorldGenerationRunResult(StoppedEarly: true, StopPassName: pass.Name, PassResults);
            }
        }

        return new WorldGenerationRunResult(StoppedEarly: false, StopPassName: null, PassResults);
    }

    private GenPassResult RunPass(WorldGenContext context, GenPass pass)
    {
        if (!pass.Enabled)
        {
            return new GenPassResult(pass.Name, Skipped: true, DurationMs: 0, RandNext: 0);
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        context.ResetPassRandom();
        progress.Start(pass.Weight);
        pass.Apply(context, progress);
        progress.End();
        int randNext = context.Random.Next();
        if (context.State is not null && string.Equals(pass.Name, "Terrain", StringComparison.Ordinal))
        {
            context.State.TerrainProbeRandNext = randNext;
        }
        else if (context.State is not null && string.Equals(pass.Name, "Dunes", StringComparison.Ordinal))
        {
            context.State.DunesProbeRandNext = randNext;
        }
        else if (context.State is not null && string.Equals(pass.Name, "Ocean Sand", StringComparison.Ordinal))
        {
            context.State.OceanSandProbeRandNext = randNext;
        }
        else if (context.State is not null && string.Equals(pass.Name, "Sand Patches", StringComparison.Ordinal))
        {
            context.State.SandPatchesProbeRandNext = randNext;
        }

        return new GenPassResult(
            pass.Name,
            Skipped: false,
            DurationMs: (int)stopwatch.ElapsedMilliseconds,
            RandNext: randNext);
    }
}

internal readonly record struct WorldGenerationRunResult(
    bool StoppedEarly,
    string? StopPassName,
    IReadOnlyList<GenPassResult> Results)
{
    public static WorldGenerationRunResult Empty { get; } =
        new(StoppedEarly: false, StopPassName: null, Results: []);
}
