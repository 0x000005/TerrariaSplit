namespace TerrariaSplit;

internal static class Terraria1456Memory
{
    public const string ProcessName = "Terraria";

    public const string UpdateTimeSignature =
        "55 8B EC 57 56 83 EC ?? 8D 7D ?? B9 ???????? 33 C0 F3 AB 80 3D ???????? 00 75 ?? 0FB6";

    public const int GameMenuPointerOffset = 0x90;
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
