namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

internal sealed class StageOneReplicaSimulator
{
    public StageOneReplicaResult Generate(WorldSeedMetadata metadata)
    {
        WorldOptions options = WorldOptions.FromMetadata(metadata);
        var state = new WorldGenState(options);
        state.ClearWorld();

        ResetSimulationResult reset = StageOneReset.Apply(state);
        if (!reset.IsSupported)
        {
            return new StageOneReplicaResult(state, WorldGenerationRunResult.Empty, IsComplete: false, reset.Detail);
        }

        var generator = new WorldGenerator(state);
        OfficialPassPlan.AppendToPyramids(generator);
        WorldGenerationRunResult run = generator.RunUntilInclusive(OfficialPassPlan.StopPassName);

        return new StageOneReplicaResult(
            state,
            run,
            IsComplete: run.StoppedEarly && string.Equals(run.StopPassName, OfficialPassPlan.StopPassName, StringComparison.Ordinal),
            Detail: $"{reset.Detail} {OfficialPassPlan.ImplementedPassCount}/{OfficialPassPlan.PassCount} pass bodies implemented; " +
                $"{OfficialPassPlan.ExplicitlySkippedPassCount} audited pass bodies skipped; " +
                $"{OfficialPassPlan.StubPassCount} still stubbed.");
    }
}

internal readonly record struct StageOneReplicaResult(
    WorldGenState State,
    WorldGenerationRunResult Run,
    bool IsComplete,
    string Detail);
