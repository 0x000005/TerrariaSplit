namespace TerrariaSplit;

internal static class Terraria1456Memory
{
    public const string ProcessName = "Terraria";
    public const string SupportedVersionLabel = "1.4.5.x";
    public const string SignatureProfileLabel = "UpdateTime x86-style signature with menu-state and boss progression fallbacks";
    public const string SignatureScanScopeLabel = "Private executable pages, then image executable pages";

    public const string UpdateTimeSignature =
        "55 8B EC 57 56 83 EC ?? 8D 7D ?? B9 ???????? 33 C0 F3 AB 80 3D ???????? 00 75 ?? 0FB6";
    public const string GameMenuFallbackSignature =
        "83 3D ???????? 01 74 ?? 80 3D ???????? 00 74 ?? 83 3D ???????? 02 0F 85";
    public const string BossProgressionFallbackSignature =
        "80 3D ???????? 00 74 ?? 8B CE BA 2B 00 00 00 E8 ???????? " +
        "80 3D ???????? 00 74 ?? 8B CE BA 2C 00 00 00 E8 ???????? " +
        "80 3D ???????? 00 74 ?? 8B CE BA 90 00 00 00 E8 ???????? " +
        "80 3D ???????? 00 74 ?? 8B CE BA 2D 00 00 00";

    public const int GameMenuPointerOffset = 0x90;
    public const int GameMenuFallbackMenuModeInlineAddressOffset = 0x2;
    public const int GameMenuFallbackGameMenuInlineAddressOffset = 0xB;
    public const int GameMenuFallbackSecondMenuModeInlineAddressOffset = 0x14;
    public const int BossProgressionFallbackSkeletronInlineAddressOffset = 0x2;
    public const int BossProgressionFallbackHardmodeInlineAddressOffset = 0x17;
    public const int BossFlagsPointerOffset = 0x46B;
    public const int HardmodePointerOffset = 0x498;

    public const int SkeletronDefeatedFlagOffset = -0x2;
    public const int PlanteraDefeatedFlagOffset = 0x5;
    public const int GolemDefeatedFlagOffset = 0x6;
    public const int LunaticCultistDefeatedFlagOffset = 0xE;
    public const int MoonLordDefeatedFlagOffset = 0xF;
    public const int DestroyerDefeatedFlagOffset = 0x1D;
    public const int TwinsDefeatedFlagOffset = 0x1E;
    public const int SkeletronPrimeDefeatedFlagOffset = 0x1F;
}
