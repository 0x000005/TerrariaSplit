namespace TerrariaSplit.Configuration;

internal static class AutoCreateZenithStarCatchStage
{
    public const string LifeCrystals = "Life Crystals";
    public const string Statues = "Statues";
    public const string BuriedChests = "Buried Chests";
    public const string GemCaves = "Gem Caves";
    public const string Pots = "Pots";
    public const string Traps = "Traps";
    public const string Default = Pots;

    public static readonly string[] All =
    [
        LifeCrystals,
        Statues,
        BuriedChests,
        GemCaves,
        Pots,
        Traps
    ];

    private static readonly string[] TrackedPassOrder =
    [
        LifeCrystals,
        Statues,
        BuriedChests,
        "Surface Chests",
        "Jungle Chests Placement",
        "Water Chests",
        "Spider Caves",
        GemCaves,
        "Moss",
        "Temple",
        "Cave Walls",
        "Jungle Trees",
        "Floating Island Houses",
        "Quick Cleanup",
        Pots,
        "Hellforge",
        "Spreading Grass",
        "Surface Ore and Stone",
        "Place Fallen Log",
        Traps,
        "Piles"
    ];

    public static string Normalize(string? value)
    {
        return All.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)) ?? Default;
    }

    public static bool TryGetSelectionIndex(string? value, out int index)
    {
        string normalized = Normalize(value);
        index = Array.FindIndex(All, option => string.Equals(option, normalized, StringComparison.OrdinalIgnoreCase));
        return index >= 0;
    }

    public static bool Includes(string? selectedStopStage, string stage)
    {
        return TryGetSelectionIndex(selectedStopStage, out int selectedIndex) &&
            TryGetSelectionIndex(stage, out int stageIndex) &&
            stageIndex <= selectedIndex;
    }

    public static bool TryGetTrackedPassIndex(string? currentPassName, out int index)
    {
        index = -1;
        if (string.IsNullOrWhiteSpace(currentPassName))
        {
            return false;
        }

        index = Array.FindIndex(
            TrackedPassOrder,
            pass => string.Equals(pass, currentPassName, StringComparison.OrdinalIgnoreCase));
        return index >= 0;
    }

    public static bool ShouldStopAtPass(string? selectedStopStage, string? currentPassName)
    {
        if (!TryGetTrackedPassIndex(currentPassName, out int currentIndex))
        {
            return false;
        }

        if (!TryGetTrackedPassIndex(Normalize(selectedStopStage), out int stopIndex))
        {
            return false;
        }

        return currentIndex > stopIndex;
    }
}
