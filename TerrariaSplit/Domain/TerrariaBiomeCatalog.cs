namespace TerrariaSplit;

internal sealed record TerrariaBiomeDefinition(
    string Id,
    string DisplayName,
    string ChineseName,
    TerrariaBiomeRule Rule,
    string IconFileName);

internal sealed record TerrariaBiomeRule(
    IReadOnlyList<TerrariaBiomeZoneBit> Required,
    IReadOnlyList<TerrariaBiomeZoneBit> AnyOf,
    IReadOnlyList<TerrariaBiomeZoneBit> Excluded)
{
    public IEnumerable<TerrariaBiomeZoneBit> ZoneBits => Required.Concat(AnyOf).Concat(Excluded);
}

internal readonly record struct TerrariaBiomeZoneBit(string ZoneFieldName, int BitIndex);

internal static class TerrariaBiomeCatalog
{
    private static readonly TerrariaBiomeZoneBit[] ForestExclusions =
    [
        Zone.Dungeon,
        Zone.Corruption,
        Zone.Hallow,
        Zone.Meteor,
        Zone.Jungle,
        Zone.Snow,
        Zone.Crimson,
        Zone.Desert,
        Zone.Glowshroom,
        Zone.UndergroundDesert,
        Zone.Ocean,
        Zone.LihzhardTemple,
        Zone.Graveyard,
        Zone.Shimmer
    ];

    public static readonly IReadOnlyList<TerrariaBiomeDefinition> Items =
    [
        new("forest", "Forest", "森林", Rule([Zone.Surface], [], ForestExclusions), "biome-forest.png"),
        new("underground", "Underground", "地下", Require(Zone.Underground), "biome-underground.png"),
        new("cavern", "Cavern", "洞穴", Require(Zone.Cavern), "biome-cavern.png"),
        new("underworld", "Underworld", "地狱", Require(Zone.Underworld), "biome-underworld.png"),
        new("space", "Space", "太空", Require(Zone.Space), "biome-space.png"),
        new("snow", "Snow", "雪原", Require(Zone.Snow, Zone.Surface), "biome-snow.png"),
        new("underground-ice", "Underground Ice", "地下冰雪", Rule([Zone.Snow], [Zone.Underground, Zone.Cavern], []), "biome-underground-ice.png"),
        new("desert", "Desert", "沙漠", Require(Zone.Desert, Zone.Surface), "biome-desert.png"),
        new("underground-desert", "Underground Desert", "地下沙漠", Require(Zone.UndergroundDesert), "biome-underground-desert.png"),
        new("ocean", "Ocean", "海洋", Require(Zone.Ocean), "biome-ocean.png"),
        new("jungle", "Jungle", "丛林", Require(Zone.Jungle, Zone.Surface), "biome-jungle.png"),
        new("underground-jungle", "Underground Jungle", "地下丛林", Rule([Zone.Jungle], [Zone.Underground, Zone.Cavern], []), "biome-underground-jungle.png"),
        new("lihzhard-temple", "Lihzahrd Temple", "丛林神庙", Require(Zone.LihzhardTemple), "biome-lihzhard-temple.png"),
        new("glowing-mushroom", "Glowing Mushroom", "发光蘑菇", Require(Zone.Glowshroom), "biome-glowing-mushroom.png"),
        new("corruption", "Corruption", "腐化", Require(Zone.Corruption, Zone.Surface), "biome-corruption.png"),
        new("underground-corruption", "Underground Corruption", "地下腐化", Rule([Zone.Corruption], [Zone.Underground, Zone.Cavern], []), "biome-underground-corruption.png"),
        new("crimson", "Crimson", "猩红", Require(Zone.Crimson, Zone.Surface), "biome-crimson.png"),
        new("underground-crimson", "Underground Crimson", "地下猩红", Rule([Zone.Crimson], [Zone.Underground, Zone.Cavern], []), "biome-underground-crimson.png"),
        new("hallow", "Hallow", "神圣", Require(Zone.Hallow, Zone.Surface), "biome-hallow.png"),
        new("underground-hallow", "Underground Hallow", "地下神圣", Rule([Zone.Hallow], [Zone.Underground, Zone.Cavern], []), "biome-underground-hallow.png"),
        new("dungeon", "Dungeon", "地牢", Require(Zone.Dungeon), "biome-dungeon.png"),
        new("aether", "Aether", "以太", Require(Zone.Shimmer), "biome-aether.png")
    ];

    public static readonly IReadOnlyDictionary<string, TerrariaBiomeDefinition> ById =
        Items.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyList<string> RequiredZoneFieldNames = Items
        .SelectMany(item => item.Rule.ZoneBits)
        .Select(bit => bit.ZoneFieldName)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static TerrariaBiomeRule Require(params TerrariaBiomeZoneBit[] required)
    {
        return Rule(required, [], []);
    }

    private static TerrariaBiomeRule Rule(
        IReadOnlyList<TerrariaBiomeZoneBit> required,
        IReadOnlyList<TerrariaBiomeZoneBit> anyOf,
        IReadOnlyList<TerrariaBiomeZoneBit> excluded)
    {
        return new TerrariaBiomeRule(required, anyOf, excluded);
    }

    private static class Zone
    {
        public static readonly TerrariaBiomeZoneBit Dungeon = new("zone1", 0);
        public static readonly TerrariaBiomeZoneBit Corruption = new("zone1", 1);
        public static readonly TerrariaBiomeZoneBit Hallow = new("zone1", 2);
        public static readonly TerrariaBiomeZoneBit Meteor = new("zone1", 3);
        public static readonly TerrariaBiomeZoneBit Jungle = new("zone1", 4);
        public static readonly TerrariaBiomeZoneBit Snow = new("zone1", 5);
        public static readonly TerrariaBiomeZoneBit Crimson = new("zone1", 6);
        public static readonly TerrariaBiomeZoneBit Desert = new("zone2", 5);
        public static readonly TerrariaBiomeZoneBit Glowshroom = new("zone2", 6);
        public static readonly TerrariaBiomeZoneBit UndergroundDesert = new("zone2", 7);
        public static readonly TerrariaBiomeZoneBit Space = new("zone3", 0);
        public static readonly TerrariaBiomeZoneBit Surface = new("zone3", 1);
        public static readonly TerrariaBiomeZoneBit Underground = new("zone3", 2);
        public static readonly TerrariaBiomeZoneBit Cavern = new("zone3", 3);
        public static readonly TerrariaBiomeZoneBit Underworld = new("zone3", 4);
        public static readonly TerrariaBiomeZoneBit Ocean = new("zone3", 5);
        public static readonly TerrariaBiomeZoneBit LihzhardTemple = new("zone4", 5);
        public static readonly TerrariaBiomeZoneBit Graveyard = new("zone4", 6);
        public static readonly TerrariaBiomeZoneBit Shimmer = new("zone5", 0);
    }
}
