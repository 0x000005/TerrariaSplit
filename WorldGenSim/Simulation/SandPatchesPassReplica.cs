namespace WorldGenSim.Simulation;

internal static class SandPatchesPassReplica
{
    public static void Apply(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Sand Patches replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        int patchCount = (int)(width * 0.013);

        for (int i = 0; i < patchCount; i++)
        {
            progress.Set((float)i / patchCount);
            int x = random.Next(0, width);
            int y = random.Next((int)state.MainWorldSurface, (int)state.MainRockLayer);
            while (x > width * 0.46 && x < width * 0.54 && y < state.MainWorldSurface + 150.0)
            {
                x = random.Next(0, width);
                y = random.Next((int)state.MainWorldSurface, (int)state.MainRockLayer);
            }

            int strength = random.Next(15, 70);
            int steps = random.Next(20, 130);
            WorldGenTileRunner.RunSandPatch(state, random, x, y, strength, steps);
        }

        state.SandPatchesApplied = true;
    }
}
