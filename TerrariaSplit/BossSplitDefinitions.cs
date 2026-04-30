namespace TerrariaSplit;

internal static class BossSplitDefinitions
{
    public static readonly IReadOnlyList<BossSplitDefinition> All = new[]
    {
        new BossSplitDefinition(
            BossSplitName.Skeletron,
            "Skeletron",
            new[] { BossFlag.Skeletron }),
        new BossSplitDefinition(
            BossSplitName.WallOfFlesh,
            "Wall of Flesh",
            new[] { BossFlag.WallOfFlesh }),
        new BossSplitDefinition(
            BossSplitName.Destroyer,
            "The Destroyer",
            new[] { BossFlag.Destroyer }),
        new BossSplitDefinition(
            BossSplitName.RemainingMechs,
            "Twins + Prime",
            new[] { BossFlag.Twins, BossFlag.SkeletronPrime }),
        new BossSplitDefinition(
            BossSplitName.Plantera,
            "Plantera",
            new[] { BossFlag.Plantera }),
        new BossSplitDefinition(
            BossSplitName.Golem,
            "Golem",
            new[] { BossFlag.Golem }),
        new BossSplitDefinition(
            BossSplitName.LunaticCultist,
            "Lunatic Cultist",
            new[] { BossFlag.LunaticCultist }),
        new BossSplitDefinition(
            BossSplitName.CelestialPillars,
            "Celestial Pillars",
            new[] { BossFlag.SolarPillar, BossFlag.VortexPillar, BossFlag.NebulaPillar, BossFlag.StardustPillar }),
        new BossSplitDefinition(
            BossSplitName.MoonLord,
            "Moon Lord",
            new[] { BossFlag.MoonLord })
    };
}
