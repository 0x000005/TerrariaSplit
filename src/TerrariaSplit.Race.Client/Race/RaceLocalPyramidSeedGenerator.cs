using TerrariaSplit.Race.Contracts;
using TerrariaSplit.WorldGeneration;

namespace TerrariaSplit.Race.Client;

public sealed class RaceLocalPyramidSeedGenerator
{
    private const int DefaultMaxAttempts = 250_000;
    private const string SeedGenerationFailedErrorCode = "seed_generation_failed";

    public RaceLocalPyramidSeedAttempt TryNext(RaceWorldSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.SecretSeeds))
        {
            return RaceLocalPyramidSeedAttempt.Fatal("Pyramid seed pre-screen does not support secret/fixed seed text.");
        }

        string seedText = Random.Shared.Next(0, int.MaxValue).ToString(System.Globalization.CultureInfo.InvariantCulture);
        PublicPyramidSeedPreScreenResult result = PyramidSeedPreScreenFacade.Evaluate(
            seedText,
            settings.SizeCode,
            settings.DifficultyCode,
            settings.HasCrimson,
            settings.SpecialSeedMask,
            settings.RequiredPyramidItemMask,
            settings.TerrariaVersion);

        if (result.Status == PublicPyramidSeedPreScreenStatus.UnsupportedScope)
        {
            return RaceLocalPyramidSeedAttempt.Fatal(result.Detail);
        }

        if (result.Status != PublicPyramidSeedPreScreenStatus.Complete ||
            !result.HasTargetPyramid ||
            !result.MatchesRequiredItems)
        {
            return RaceLocalPyramidSeedAttempt.Miss();
        }

        string detail = $"{result.TargetClass}: {result.LootSummary}";
        return RaceLocalPyramidSeedAttempt.Match(
            new RaceSeedAssignment(seedText, RaceSeedSource.HostGenerated, detail));
    }

    public RaceOperationResult<RaceSeedAssignment> Generate(RaceWorldSettings settings, int maxAttempts = DefaultMaxAttempts)
    {
        int attempts = Math.Clamp(maxAttempts <= 0 ? DefaultMaxAttempts : maxAttempts, 1, 5_000_000);
        for (int i = 1; i <= attempts; i++)
        {
            RaceLocalPyramidSeedAttempt attempt = TryNext(settings);
            if (attempt.Status == RaceLocalPyramidSeedAttemptStatus.Fatal)
            {
                return RaceOperationResult<RaceSeedAssignment>.Failure(
                    SeedGenerationFailedErrorCode,
                    attempt.Message);
            }

            if (attempt.Status == RaceLocalPyramidSeedAttemptStatus.Match && attempt.Seed is RaceSeedAssignment seed)
            {
                string detail = string.IsNullOrWhiteSpace(seed.Detail)
                    ? $"Matched after {i} attempts."
                    : $"Matched after {i} attempts. {seed.Detail}";
                return RaceOperationResult<RaceSeedAssignment>.Success(
                    seed with { Detail = detail });
            }
        }

        return RaceOperationResult<RaceSeedAssignment>.Failure(
            SeedGenerationFailedErrorCode,
            $"No matching pyramid seed found after {attempts} attempts.");
    }
}

public enum RaceLocalPyramidSeedAttemptStatus
{
    Miss,
    Match,
    Fatal
}

public readonly record struct RaceLocalPyramidSeedAttempt(
    RaceLocalPyramidSeedAttemptStatus Status,
    RaceSeedAssignment? Seed,
    string Message)
{
    public static RaceLocalPyramidSeedAttempt Miss()
    {
        return new RaceLocalPyramidSeedAttempt(RaceLocalPyramidSeedAttemptStatus.Miss, null, string.Empty);
    }

    public static RaceLocalPyramidSeedAttempt Match(RaceSeedAssignment seed)
    {
        return new RaceLocalPyramidSeedAttempt(RaceLocalPyramidSeedAttemptStatus.Match, seed, string.Empty);
    }

    public static RaceLocalPyramidSeedAttempt Fatal(string message)
    {
        return new RaceLocalPyramidSeedAttempt(RaceLocalPyramidSeedAttemptStatus.Fatal, null, message);
    }
}
