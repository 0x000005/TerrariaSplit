using System;

namespace TerrariaSplit.Compatibility
{
    [Flags]
    public enum TerrariaVersionGroup
    {
        Unknown = 0,
        Version144 = 1,
        Version145 = 2,
        Version146Plus = 4,
        All = Version144 | Version145 | Version146Plus
    }

    public enum TerrariaFeatureCategory
    {
        WorldCreation,
        Race,
        SplitDetection,
        UiScale
    }

    public enum TerrariaFeature
    {
        ForegroundWorldCreation,
        SeedFiltering,
        BackgroundWorldCreation,
        RacePreparation,
        RaceRandomControl,
        RaceBossPenalty,
        RaceInGameUi,
        GameStateDetection,
        BossAndEventDetection,
        ItemDetection,
        BiomeDetection,
        NpcDetection,
        UiScale
    }

    public enum TerrariaImplementation
    {
        None,
        LegacyMenu,
        StateDrivenMenu,
        ResourceJudge1458,
        ResourceJudge1461,
        LegacyServer,
        ModernServer,
        Race1461,
        SharedMemoryReader,
        UiScale145,
        UiScale146
    }

    public enum TerrariaFeatureSupport
    {
        Supported,
        AllowedToTry,
        PendingAdaptation,
        Unsupported,
        UnknownVersion
    }
}
