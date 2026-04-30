namespace TerrariaSplit;

internal static class BossSplitDefinitions
{
    public const string Skeletron = "Skeletron";
    public const string WallOfFlesh = "WallOfFlesh";
    public const string Destroyer = "Destroyer";
    public const string SkeletronPrime = "SkeletronPrime";
    public const string Twins = "Twins";
    public const string Plantera = "Plantera";
    public const string Golem = "Golem";
    public const string LunaticCultist = "LunaticCultist";
    public const string CelestialPillars = "CelestialPillars";
    public const string MoonLord = "MoonLord";
    public const string RemainingMechs = "RemainingMechs";

    public static readonly IReadOnlyList<BossUnitDefinition> Units = new[]
    {
        new BossUnitDefinition(
            Skeletron,
            "Skeletron",
            new[] { BossFlag.Skeletron },
            new[] { "skeletron.png" }),
        new BossUnitDefinition(
            WallOfFlesh,
            "Wall of Flesh",
            new[] { BossFlag.WallOfFlesh },
            new[] { "wof.png" }),
        new BossUnitDefinition(
            Destroyer,
            "Destroyer",
            new[] { BossFlag.Destroyer },
            new[] { "destroyer.png" }),
        new BossUnitDefinition(
            SkeletronPrime,
            "Skeletron Prime",
            new[] { BossFlag.SkeletronPrime },
            new[] { "prime.png" }),
        new BossUnitDefinition(
            Twins,
            "The Twins",
            new[] { BossFlag.Twins },
            new[] { "twins.png" }),
        new BossUnitDefinition(
            Plantera,
            "Plantera",
            new[] { BossFlag.Plantera },
            new[] { "plantera.png" }),
        new BossUnitDefinition(
            Golem,
            "Golem",
            new[] { BossFlag.Golem },
            new[] { "golem.png" }),
        new BossUnitDefinition(
            LunaticCultist,
            "Lunatic Cultist",
            new[] { BossFlag.LunaticCultist },
            new[] { "cultist.png" }),
        new BossUnitDefinition(
            CelestialPillars,
            "Celestial Pillars",
            new[] { BossFlag.SolarPillar, BossFlag.VortexPillar, BossFlag.NebulaPillar, BossFlag.StardustPillar },
            new[] { "pillars.png" }),
        new BossUnitDefinition(
            MoonLord,
            "Moon Lord",
            new[] { BossFlag.MoonLord },
            new[] { "moonlord.png" })
    };

    public static IReadOnlyList<BossSplitDefinition> Build(AppSettings settings)
    {
        IReadOnlyDictionary<string, BossUnitDefinition> units = Units.ToDictionary(unit => unit.Id, StringComparer.OrdinalIgnoreCase);
        return settings.Route
            .Where(entry => entry.Enabled && units.ContainsKey(entry.BossId))
            .OrderBy(entry => entry.Segment)
            .ThenBy(entry => GetUnitOrder(entry.BossId))
            .GroupBy(entry => Math.Max(1, (int)Math.Truncate(entry.Segment)))
            .Select(group => BuildSplit(group.Select(entry => units[entry.BossId]).ToList()))
            .ToList();
    }

    public static List<BossRouteEntry> CreateDefaultRoute()
    {
        return new List<BossRouteEntry>
        {
            new() { BossId = Skeletron, Enabled = true, Segment = 1m },
            new() { BossId = WallOfFlesh, Enabled = true, Segment = 2m },
            new() { BossId = Destroyer, Enabled = true, Segment = 3m },
            new() { BossId = SkeletronPrime, Enabled = true, Segment = 3m },
            new() { BossId = Twins, Enabled = true, Segment = 3m },
            new() { BossId = Plantera, Enabled = true, Segment = 4m },
            new() { BossId = Golem, Enabled = true, Segment = 5m },
            new() { BossId = LunaticCultist, Enabled = true, Segment = 6m },
            new() { BossId = CelestialPillars, Enabled = false, Segment = 7m },
            new() { BossId = MoonLord, Enabled = true, Segment = 8m }
        };
    }

    public static bool TryGetUnit(string id, out BossUnitDefinition unit)
    {
        unit = Units.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase))!;
        return unit is not null;
    }

    private static BossSplitDefinition BuildSplit(IReadOnlyList<BossUnitDefinition> units)
    {
        string id = GetSplitId(units);
        string displayName = GetSplitDisplayName(units);
        return new BossSplitDefinition(
            id,
            displayName,
            units.SelectMany(unit => unit.RequiredFlags).ToArray(),
            units.SelectMany(unit => unit.IconFileNames).ToArray(),
            units.SelectMany(unit => unit.IconFileNames.Select(_ => unit.Id)).ToArray(),
            units.Select(unit => unit.Id).ToArray());
    }

    private static string GetSplitId(IReadOnlyList<BossUnitDefinition> units)
    {
        if (units.Count == 1)
        {
            return units[0].Id;
        }

        if (units.Select(unit => unit.Id).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).SequenceEqual(
            new[] { Destroyer, SkeletronPrime, Twins }.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)))
        {
            return RemainingMechs;
        }

        return string.Join("+", units.Select(unit => unit.Id));
    }

    private static string GetSplitDisplayName(IReadOnlyList<BossUnitDefinition> units)
    {
        return units.Count == 1
            ? units[0].DisplayName
            : GetSplitId(units) == RemainingMechs
                ? "Mechanical Bosses"
                : string.Join(" / ", units.Select(unit => unit.DisplayName));
    }

    private static int GetUnitOrder(string bossId)
    {
        for (int i = 0; i < Units.Count; i++)
        {
            if (string.Equals(Units[i].Id, bossId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return int.MaxValue;
    }
}
