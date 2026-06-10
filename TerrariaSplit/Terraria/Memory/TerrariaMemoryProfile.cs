namespace TerrariaSplit;

internal sealed class TerrariaMemoryProfile
{
    public TerrariaMemoryProfile(
        string processName,
        string supportedVersionLabel,
        string signatureProfileLabel,
        string signatureScanScopeLabel,
        string updateTimeSignature,
        string gameMenuFallbackSignature,
        string bossProgressionFallbackSignature,
        string currentControllerSignature,
        string currentGenerationProgressSignature,
        int gameMenuPointerOffset,
        int gameMenuFallbackGameMenuInlineAddressOffset,
        int bossProgressionFallbackGameMenuFromHardmodeOffset,
        int bossProgressionFallbackSkeletronInlineAddressOffset,
        int bossProgressionFallbackHardmodeInlineAddressOffset,
        int currentControllerInlineAddressOffset,
        int currentGenerationProgressInlineAddressOffset,
        int bossFlagsPointerOffset,
        int hardmodePointerOffset,
        int skeletronDefeatedFlagOffset,
        int planteraDefeatedFlagOffset,
        int golemDefeatedFlagOffset,
        int lunaticCultistDefeatedFlagOffset,
        int moonLordDefeatedFlagOffset,
        int destroyerDefeatedFlagOffset,
        int twinsDefeatedFlagOffset,
        int skeletronPrimeDefeatedFlagOffset)
    {
        ProcessName = processName;
        SupportedVersionLabel = supportedVersionLabel;
        SignatureProfileLabel = signatureProfileLabel;
        SignatureScanScopeLabel = signatureScanScopeLabel;
        UpdateTimeSignature = SignaturePattern.Parse(updateTimeSignature);
        GameMenuFallbackSignature = SignaturePattern.Parse(gameMenuFallbackSignature);
        BossProgressionFallbackSignature = SignaturePattern.Parse(bossProgressionFallbackSignature);
        CurrentControllerSignature = SignaturePattern.Parse(currentControllerSignature);
        CurrentGenerationProgressSignature = SignaturePattern.Parse(currentGenerationProgressSignature);
        GameMenuPointerOffset = gameMenuPointerOffset;
        GameMenuFallbackGameMenuInlineAddressOffset = gameMenuFallbackGameMenuInlineAddressOffset;
        BossProgressionFallbackGameMenuFromHardmodeOffset = bossProgressionFallbackGameMenuFromHardmodeOffset;
        BossProgressionFallbackSkeletronInlineAddressOffset = bossProgressionFallbackSkeletronInlineAddressOffset;
        BossProgressionFallbackHardmodeInlineAddressOffset = bossProgressionFallbackHardmodeInlineAddressOffset;
        CurrentControllerInlineAddressOffset = currentControllerInlineAddressOffset;
        CurrentGenerationProgressInlineAddressOffset = currentGenerationProgressInlineAddressOffset;
        BossFlagsPointerOffset = bossFlagsPointerOffset;
        HardmodePointerOffset = hardmodePointerOffset;
        SkeletronDefeatedFlagOffset = skeletronDefeatedFlagOffset;
        PlanteraDefeatedFlagOffset = planteraDefeatedFlagOffset;
        GolemDefeatedFlagOffset = golemDefeatedFlagOffset;
        LunaticCultistDefeatedFlagOffset = lunaticCultistDefeatedFlagOffset;
        MoonLordDefeatedFlagOffset = moonLordDefeatedFlagOffset;
        DestroyerDefeatedFlagOffset = destroyerDefeatedFlagOffset;
        TwinsDefeatedFlagOffset = twinsDefeatedFlagOffset;
        SkeletronPrimeDefeatedFlagOffset = skeletronPrimeDefeatedFlagOffset;
    }

    public string ProcessName { get; }

    public string SupportedVersionLabel { get; }

    public string SignatureProfileLabel { get; }

    public string SignatureScanScopeLabel { get; }

    public SignaturePattern UpdateTimeSignature { get; }

    public SignaturePattern GameMenuFallbackSignature { get; }

    public SignaturePattern BossProgressionFallbackSignature { get; }

    public SignaturePattern CurrentControllerSignature { get; }

    public SignaturePattern CurrentGenerationProgressSignature { get; }

    public int GameMenuPointerOffset { get; }

    public int GameMenuFallbackGameMenuInlineAddressOffset { get; }

    public int BossProgressionFallbackGameMenuFromHardmodeOffset { get; }

    public int BossProgressionFallbackSkeletronInlineAddressOffset { get; }

    public int BossProgressionFallbackHardmodeInlineAddressOffset { get; }

    public int CurrentControllerInlineAddressOffset { get; }

    public int CurrentGenerationProgressInlineAddressOffset { get; }

    public int BossFlagsPointerOffset { get; }

    public int HardmodePointerOffset { get; }

    public int SkeletronDefeatedFlagOffset { get; }

    public int PlanteraDefeatedFlagOffset { get; }

    public int GolemDefeatedFlagOffset { get; }

    public int LunaticCultistDefeatedFlagOffset { get; }

    public int MoonLordDefeatedFlagOffset { get; }

    public int DestroyerDefeatedFlagOffset { get; }

    public int TwinsDefeatedFlagOffset { get; }

    public int SkeletronPrimeDefeatedFlagOffset { get; }
}
