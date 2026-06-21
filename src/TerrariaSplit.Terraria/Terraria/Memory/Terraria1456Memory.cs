namespace TerrariaSplit.Terraria.Memory;

internal static class Terraria1456Memory
{
    public static TerrariaMemoryProfile Profile { get; } = new(
        "Terraria",
        "1.4.5.x",
        "UpdateTime x86-style signature with menu-state, boss progression, and worldgen fallbacks",
        "Private executable pages, then image executable pages",
        UpdateTimeSignature,
        GameMenuFallbackSignature,
        BossProgressionFallbackSignature,
        CurrentControllerSignature,
        CurrentGenerationProgressSignature,
        GameMenuPointerOffset,
        GameMenuFallbackGameMenuInlineAddressOffset,
        BossProgressionFallbackGameMenuFromHardmodeOffset,
        BossProgressionFallbackSkeletronInlineAddressOffset,
        BossProgressionFallbackHardmodeInlineAddressOffset,
        CurrentControllerInlineAddressOffset,
        CurrentGenerationProgressInlineAddressOffset,
        BossFlagsPointerOffset,
        HardmodePointerOffset,
        SkeletronDefeatedFlagOffset,
        PlanteraDefeatedFlagOffset,
        GolemDefeatedFlagOffset,
        LunaticCultistDefeatedFlagOffset,
        MoonLordDefeatedFlagOffset,
        DestroyerDefeatedFlagOffset,
        TwinsDefeatedFlagOffset,
        SkeletronPrimeDefeatedFlagOffset);

    private const string UpdateTimeSignature =
        "55 8B EC 57 56 83 EC ?? 8D 7D ?? B9 ???????? 33 C0 F3 AB 80 3D ???????? 00 75 ?? 0FB6";
    private const string GameMenuFallbackSignature =
        "83 3D ???????? 01 74 ?? 80 3D ???????? 00 74 ?? 83 3D ???????? 02 0F 85";
    private const string BossProgressionFallbackSignature =
        "80 3D ???????? 00 74 ?? 8B CE BA 2B 00 00 00 E8 ???????? " +
        "80 3D ???????? 00 74 ?? 8B CE BA 2C 00 00 00 E8 ???????? " +
        "80 3D ???????? 00 74 ?? 8B CE BA 90 00 00 00 E8 ???????? " +
        "80 3D ???????? 00 74 ?? 8B CE BA 2D 00 00 00";
    private const string CurrentControllerSignature =
        "55 8B EC 57 56 53 50 8B F1 8B FA 83 3D ???????? 00 0F 84 ???????? 80 3D ???????? 00 74 ?? " +
        "B9 ???????? BA 4E 00 00 00 E8 ????????";
    private const string CurrentGenerationProgressSignature =
        "56 8B F2 A1 ???????? 8D 91 F8 00 00 00 E8 ???????? 83 B9 F8 00 00 00 00 75 02 5E C3";

    private const int GameMenuPointerOffset = 0x90;
    private const int GameMenuFallbackGameMenuInlineAddressOffset = 0xB;
    private const int BossProgressionFallbackGameMenuFromHardmodeOffset = 0x4E;
    private const int BossProgressionFallbackSkeletronInlineAddressOffset = 0x2;
    private const int BossProgressionFallbackHardmodeInlineAddressOffset = 0x17;
    private const int CurrentControllerInlineAddressOffset = 13;
    private const int CurrentGenerationProgressInlineAddressOffset = 4;
    private const int BossFlagsPointerOffset = 0x46B;
    private const int HardmodePointerOffset = 0x498;

    private const int SkeletronDefeatedFlagOffset = -0x2;
    private const int PlanteraDefeatedFlagOffset = 0x5;
    private const int GolemDefeatedFlagOffset = 0x6;
    private const int LunaticCultistDefeatedFlagOffset = 0xE;
    private const int MoonLordDefeatedFlagOffset = 0xF;
    private const int DestroyerDefeatedFlagOffset = 0x1D;
    private const int TwinsDefeatedFlagOffset = 0x1E;
    private const int SkeletronPrimeDefeatedFlagOffset = 0x1F;
}
