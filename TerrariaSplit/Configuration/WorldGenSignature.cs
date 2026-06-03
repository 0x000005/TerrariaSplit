namespace TerrariaSplit;

// A stable fingerprint of the world-generation inputs that affect terrain.
// A pooled world file is only valid for compatible settings, so the seed pool is keyed
// by this and cleared whenever it changes.
internal static class WorldGenSignature
{
    // Terraria's world generator is version-sensitive. Keep this as a visible pool
    // signature component instead of an opaque "v1" format marker.
    private const string TerrariaVersion = "1.4.5.6";

    public static string From(AutoCreateWorldSettings autoCreate)
    {
        string size = AutoCreateWorldSize.Normalize(autoCreate.WorldSize);
        string difficulty = AutoCreateWorldDifficulty.Normalize(autoCreate.WorldDifficulty);
        string evil = AutoCreateWorldEvil.Normalize(autoCreate.WorldEvil);
        string specialSeeds = string.Join(",", AutoCreateSpecialWorldSeed.ParseList(autoCreate.SpecialSeeds));
        return string.Join("|", TerrariaVersion, size, difficulty, evil, specialSeeds);
    }
}

// Headless generation cannot reproduce every world-creation option, so pooling is
// scoped to the cases the dedicated server can faithfully reproduce.
internal static class SeedPoolSupport
{
    public static bool IsSupported(AutoCreateWorldSettings autoCreate)
    {
        // A user-typed visible seed pins the world, so there is nothing to randomize or pool.
        if (!string.IsNullOrWhiteSpace(autoCreate.SecretSeeds))
        {
            return false;
        }

        // The dedicated server config exposes no Skyblock toggle, so a Skyblock world
        // cannot be produced headlessly for the pool.
        return !AutoCreateSpecialWorldSeed.ParseList(autoCreate.SpecialSeeds)
            .Contains(AutoCreateSpecialWorldSeed.Skyblock, StringComparer.OrdinalIgnoreCase);
    }
}
