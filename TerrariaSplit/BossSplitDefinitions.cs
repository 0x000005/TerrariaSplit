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
            BossSplitName.RemainingMechs,
            "Mechanical Bosses",
            new[] { BossFlag.Destroyer, BossFlag.SkeletronPrime, BossFlag.Twins },
            new[] { "destroyer.png", "prime.png", "twins.png" }),
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
            BossSplitName.MoonLord,
            "Moon Lord",
            new[] { BossFlag.MoonLord },
            new[] { "moonlord.png" })
    };
}
