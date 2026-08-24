namespace TerrariaSplit.Race.Determinism
{
    public static class RaceDeterminismProtocol
    {
        public const int CurrentVersion = 5;
        public const int CurrentChancePolicyVersion = 3;
        public const int EntropySeedLength = 32;
        public const string TerrariaVersion = "1.4.5.8";
        public const string TerrariaCompatibilityId =
            "terraria-1.4.5.8-win-x86-mvid-2c29f6c34bd94add9c58da159804e083";

        public const int WorldLockCapability = 1 << 0;
        public const int NpcDirectDropsCapability = 1 << 1;
        public const int PlayerTriggeredResultsCapability = 1 << 2;
        public const int AlchemyAndLuckCapability = 1 << 3;
        public const int WorldTransitionsCapability = 1 << 4;
        public const int StardustTownAndNaturalEventsCapability = 1 << 5;

        public const int KnownCapabilities =
            WorldLockCapability |
            NpcDirectDropsCapability |
            PlayerTriggeredResultsCapability |
            AlchemyAndLuckCapability |
            WorldTransitionsCapability |
            StardustTownAndNaturalEventsCapability;
    }
}
