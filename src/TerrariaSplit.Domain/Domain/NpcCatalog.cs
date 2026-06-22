namespace TerrariaSplit.Domain;

public sealed record TerrariaNpcDefinition(
    int Id,
    string InternalName,
    string DisplayName,
    string ChineseName,
    int DefaultHeadIndex);

public static class TerrariaNpcCatalog
{
    public const int MaxNpcId = 687;

    public static readonly IReadOnlyList<TerrariaNpcDefinition> Items =
    [
        new(17, "Merchant", "Merchant", "商人", 2),
        new(18, "Nurse", "Nurse", "护士", 3),
        new(19, "ArmsDealer", "Arms Dealer", "军火商", 6),
        new(20, "Dryad", "Dryad", "树妖", 5),
        new(22, "Guide", "Guide", "向导", 1),
        new(37, "OldMan", "Old Man", "老人", -1),
        new(38, "Demolitionist", "Demolitionist", "爆破专家", 4),
        new(54, "Clothier", "Clothier", "服装商", 7),
        new(107, "GoblinTinkerer", "Goblin Tinkerer", "哥布林工匠", 9),
        new(108, "Wizard", "Wizard", "巫师", 10),
        new(124, "Mechanic", "Mechanic", "机械师", 8),
        new(142, "SantaClaus", "Santa Claus", "圣诞老人", 11),
        new(160, "Truffle", "Truffle", "松露人", 12),
        new(178, "Steampunker", "Steampunker", "蒸汽朋克人", 13),
        new(207, "DyeTrader", "Dye Trader", "染料商", 14),
        new(208, "PartyGirl", "Party Girl", "派对女孩", 15),
        new(209, "Cyborg", "Cyborg", "机器侠", 16),
        new(227, "Painter", "Painter", "油漆工", 17),
        new(228, "WitchDoctor", "Witch Doctor", "巫医", 18),
        new(229, "Pirate", "Pirate", "海盗", 19),
        new(353, "Stylist", "Stylist", "发型师", 20),
        new(368, "TravellingMerchant", "Traveling Merchant", "旅商", 21),
        new(369, "Angler", "Angler", "渔夫", 22),
        new(441, "TaxCollector", "Tax Collector", "税收官", 23),
        new(453, "SkeletonMerchant", "Skeleton Merchant", "骷髅商人", -1),
        new(550, "DD2Bartender", "Tavernkeep", "酒馆老板", 24),
        new(588, "Golfer", "Golfer", "高尔夫球手", 25),
        new(633, "BestiaryGirl", "Zoologist", "动物学家", 26),
        new(663, "Princess", "Princess", "公主", 45),
        new(670, "TownSlimeBlue", "Nerdy Slime", "书呆子史莱姆", 46),
        new(678, "TownSlimeGreen", "Cool Slime", "酷酷史莱姆", 47),
        new(679, "TownSlimeOld", "Elder Slime", "长者史莱姆", 48),
        new(680, "TownSlimePurple", "Clumsy Slime", "笨拙史莱姆", 49),
        new(681, "TownSlimeRainbow", "Diva Slime", "天后史莱姆", 50),
        new(682, "TownSlimeRed", "Surly Slime", "暴躁史莱姆", 51),
        new(683, "TownSlimeYellow", "Mystic Slime", "神秘史莱姆", 52),
        new(684, "TownSlimeCopper", "Squire Slime", "侍卫史莱姆", 53)
    ];

    public static readonly IReadOnlyDictionary<int, TerrariaNpcDefinition> ById =
        Items.ToDictionary(item => item.Id);
}
