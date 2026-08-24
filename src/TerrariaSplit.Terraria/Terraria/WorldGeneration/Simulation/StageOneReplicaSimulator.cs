namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

internal sealed class StageOneReplicaSimulator
{
    public StageOneReplicaResult Generate(
        WorldSeedMetadata metadata,
        TerrariaWorldGenerationVersion version = TerrariaWorldGenerationVersion.Modern1458)
    {
        WorldOptions options = WorldOptions.FromMetadata(metadata);
        var state = new WorldGenState(options);

        ResetSimulationResult reset = StageOneReset.Apply(state);
        if (!reset.IsSupported)
        {
            return new StageOneReplicaResult(state, WorldGenerationRunResult.Empty, IsComplete: false, reset.Detail);
        }

        var generator = new WorldGenerator(state);
        OfficialPassPlan.AppendToPyramids(generator, version);
        WorldGenerationRunResult run = generator.RunUntilInclusive(OfficialPassPlan.StopPassName);

        return new StageOneReplicaResult(
            state,
            run,
            IsComplete: run.StoppedEarly && string.Equals(run.StopPassName, OfficialPassPlan.StopPassName, StringComparison.Ordinal),
            Detail: $"{reset.Detail} version={version}; " +
                $"{OfficialPassPlan.ImplementedPassCountFor(version)}/{OfficialPassPlan.PassCountFor(version)} pass bodies implemented; " +
                $"{OfficialPassPlan.ExplicitlySkippedPassCountFor(version)} audited pass bodies skipped; " +
                $"{OfficialPassPlan.StubPassCountFor(version)} still stubbed.");
    }
}

internal readonly record struct StageOneReplicaResult(
    WorldGenState State,
    WorldGenerationRunResult Run,
    bool IsComplete,
    string Detail);
