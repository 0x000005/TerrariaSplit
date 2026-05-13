namespace TerrariaSplit;

internal static class Terraria1456Memory
{
    public static TerrariaMemoryProfile Profile { get; } = new(
        "Terraria",
        "1.4.5.x",
        "UpdateTime x86-style signature with menu-state and boss progression fallbacks",
        "Private executable pages, then image executable pages",
        UpdateTimeSignature,
        GameMenuFallbackSignature,
        BossProgressionFallbackSignature,
        GameMenuPointerOffset,
        GameMenuFallbackGameMenuInlineAddressOffset,
        BossProgressionFallbackSkeletronInlineAddressOffset,
        BossProgressionFallbackHardmodeInlineAddressOffset,
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

    private const int GameMenuPointerOffset = 0x90;
    private const int GameMenuFallbackGameMenuInlineAddressOffset = 0xB;
    private const int BossProgressionFallbackSkeletronInlineAddressOffset = 0x2;
    private const int BossProgressionFallbackHardmodeInlineAddressOffset = 0x17;
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
