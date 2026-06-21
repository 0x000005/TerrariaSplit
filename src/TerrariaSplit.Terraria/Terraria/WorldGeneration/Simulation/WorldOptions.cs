namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

internal readonly record struct WorldOptions(
    int Seed,
    WorldDimensions Dimensions,
    int DifficultyCode,
    bool HasCrimson,
    int SpecialSeedMask)
{
    public static WorldOptions FromMetadata(WorldSeedMetadata metadata)
    {
        return new WorldOptions(
            int.Parse(metadata.SeedText, System.Globalization.CultureInfo.InvariantCulture),
            WorldDimensions.FromSizeCode(metadata.SizeCode),
            metadata.DifficultyCode,
            metadata.HasCrimson,
            metadata.SpecialSeedMask);
    }

    public bool IsTargetScope => Dimensions == WorldDimensions.Small && HasCrimson && SpecialSeedMask == 0;

    public string TargetScopeDetail()
    {
        if (Dimensions != WorldDimensions.Small)
        {
            return "Pyramid pre-screen stage-1 target scope is small worlds only.";
        }

        if (!HasCrimson)
        {
            return "Pyramid pre-screen stage-1 target scope is crimson worlds only.";
        }

        if (SpecialSeedMask != 0)
        {
            return "Pyramid pre-screen stage-1 target scope excludes special/secret seeds.";
        }

        return "Pyramid pre-screen stage-1 target scope: small crimson non-special world.";
    }
}
