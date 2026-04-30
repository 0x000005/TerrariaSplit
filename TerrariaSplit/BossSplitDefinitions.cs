namespace TerrariaSplit;

internal static class BossSplitDefinitions
{
    public static readonly IReadOnlyList<BossSplitDefinition> All = new[]
    {
        new BossSplitDefinition(
            BossSplitName.Skeletron,
            "Skeletron",
            new[] { BossFlag.Skeletron },
            new[] { "skeletron.png" }),
        new BossSplitDefinition(
            BossSplitName.WallOfFlesh,
            "Wall of Flesh",
            new[] { BossFlag.WallOfFlesh },
            new[] { "wof.png" }),
        new BossSplitDefinition(
            BossSplitName.Destroyer,
            "The Destroyer",
            new[] { BossFlag.Destroyer },
            new[] { "destroyer.png" }),
        new BossSplitDefinition(
            BossSplitName.RemainingMechs,
            "Twins + Prime",
            new[] { BossFlag.Twins, BossFlag.SkeletronPrime },
            new[] { "twins.png", "prime.png" }),
        new BossSplitDefinition(
            BossSplitName.Plantera,
            "Plantera",
            new[] { BossFlag.Plantera },
            new[] { "plantera.png" }),
        new BossSplitDefinition(
            BossSplitName.Golem,
            "Golem",
            new[] { BossFlag.Golem },
            new[] { "golem.png" }),
        new BossSplitDefinition(
            BossSplitName.LunaticCultist,
            "Lunatic Cultist",
            new[] { BossFlag.LunaticCultist },
            new[] { "cultist.png" }),
        new BossSplitDefinition(
            BossSplitName.CelestialPillars,
            "Celestial Pillars",
            new[] { BossFlag.SolarPillar, BossFlag.VortexPillar, BossFlag.NebulaPillar, BossFlag.StardustPillar },
            new[] { "pillars.png" }),
        new BossSplitDefinition(
            BossSplitName.MoonLord,
            "Moon Lord",
            new[] { BossFlag.MoonLord },
            new[] { "moonlord.png" })
    };
}
