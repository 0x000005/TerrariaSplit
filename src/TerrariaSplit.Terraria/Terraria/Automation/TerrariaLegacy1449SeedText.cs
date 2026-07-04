namespace TerrariaSplit.Terraria.Automation;

public static class TerrariaLegacy1449SeedText
{
    private static readonly Dictionary<string, string> SpecialSeedTexts = new(StringComparer.OrdinalIgnoreCase)
    {
        { AutoCreateSpecialWorldSeed.NotTheBees, "notthebees" },
        { AutoCreateSpecialWorldSeed.Drunk, "5162020" },
        { AutoCreateSpecialWorldSeed.Celebration, "celebrationmk10" },
        { AutoCreateSpecialWorldSeed.TheConstant, "theconstant" },
        { AutoCreateSpecialWorldSeed.ForTheWorthy, "fortheworthy" },
        { AutoCreateSpecialWorldSeed.NoTraps, "notraps" },
        { AutoCreateSpecialWorldSeed.Remix, "dontdigup" },
        { AutoCreateSpecialWorldSeed.Zenith, "getfixedboi" }
    };

    public static bool TryBuild(AutoCreateWorldSettings settings, out string seedText, out string detail)
    {
        seedText = string.Empty;
        detail = string.Empty;

        List<string> seedTokens = new();
        foreach (string rawSeed in AutoCreateSeedList.Parse(settings.SpecialSeeds))
        {
            if (!AutoCreateSpecialWorldSeed.TryNormalize(rawSeed, out string specialSeed))
            {
                detail = $"Unknown Terraria special seed: {rawSeed}";
                return false;
            }

            if (string.Equals(specialSeed, AutoCreateSpecialWorldSeed.Skyblock, StringComparison.OrdinalIgnoreCase))
            {
                detail = "Skyblock is not available in Terraria 1.4.4.9.";
                return false;
            }
        }

        foreach (string specialSeed in AutoCreateSpecialWorldSeed.ParseList(settings.SpecialSeeds))
        {
            if (!TryGetSpecialSeedText(specialSeed, out string specialSeedText))
            {
                detail = $"{specialSeed} is not available in Terraria 1.4.4.9.";
                return false;
            }

            seedTokens.Add(specialSeedText);
        }

        // Terraria 1.4.4.9 accepts special seeds through the normal world seed prompt.
        // Keep the user-provided secret seed text as raw seed prompt tokens and join it
        // with converted special seed tokens.
        seedTokens.AddRange(AutoCreateSeedList.Parse(settings.SecretSeeds));
        seedText = string.Join("|", seedTokens);
        detail = seedTokens.Count == 0
            ? "Terraria will choose a random 1.4.4.9 seed."
            : $"1.4.4.9 world seed field text: {seedText}";
        return true;
    }

    public static bool TryGetSpecialSeedText(string specialSeed, out string seedText)
    {
        seedText = string.Empty;
        if (string.Equals(specialSeed, AutoCreateSpecialWorldSeed.Skyblock, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return SpecialSeedTexts.TryGetValue(specialSeed, out seedText!);
    }
}
