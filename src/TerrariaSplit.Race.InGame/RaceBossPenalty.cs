using System;
using System.Globalization;

namespace TerrariaSplit.Race.InGame
{
    internal enum RaceBossPenaltyKind
    {
        Skeletron = 1,
        WallOfFlesh = 2
    }

    internal static class RaceBossPenalty
    {
        public const string ActionControlId = "race-boss-penalty";
        public const long SkeletronProportionalPenaltyMilliseconds = 5L * 60L * 1000L;
        public const long WallOfFleshBasePenaltyMilliseconds = 2L * 60L * 1000L;
        public const long WallOfFleshProportionalPenaltyMilliseconds = 3L * 60L * 1000L;

        public static long CalculateMilliseconds(
            RaceBossPenaltyKind kind,
            int currentLife,
            int maximumLife,
            int gameMode)
        {
            if (!IsSupportedKind(kind) || currentLife <= 0 || maximumLife <= 0)
            {
                return 0L;
            }

            double difficultyMultiplier;
            switch (gameMode)
            {
                case 1:
                    difficultyMultiplier = 1.2d;
                    break;
                case 2:
                    difficultyMultiplier = 1.5d;
                    break;
                case 3:
                default:
                    difficultyMultiplier = 1d;
                    break;
            }

            double remainingRatio = Math.Min(1d, currentLife / (double)maximumLife);
            long basePenalty = kind == RaceBossPenaltyKind.WallOfFlesh
                ? WallOfFleshBasePenaltyMilliseconds
                : 0L;
            long proportionalPenalty = kind == RaceBossPenaltyKind.WallOfFlesh
                ? WallOfFleshProportionalPenaltyMilliseconds
                : SkeletronProportionalPenaltyMilliseconds;
            return (long)Math.Round(
                (basePenalty + proportionalPenalty * remainingRatio) *
                difficultyMultiplier,
                MidpointRounding.AwayFromZero);
        }

        public static bool IsValidMilliseconds(RaceBossPenaltyKind kind, long milliseconds)
        {
            if (!IsSupportedKind(kind) || milliseconds <= 0L)
            {
                return false;
            }

            long maximum = kind == RaceBossPenaltyKind.WallOfFlesh
                ? WallOfFleshBasePenaltyMilliseconds +
                    WallOfFleshProportionalPenaltyMilliseconds
                : SkeletronProportionalPenaltyMilliseconds;

            maximum = maximum * 3L / 2L;
            return milliseconds <= maximum;
        }

        public static bool IsSupportedKind(RaceBossPenaltyKind kind)
        {
            return kind == RaceBossPenaltyKind.Skeletron ||
                kind == RaceBossPenaltyKind.WallOfFlesh;
        }

        public static string CreateActionValue(
            RaceBossPenaltyKind kind,
            string packageDigest,
            long milliseconds,
            long settlementId)
        {
            if (!IsSupportedKind(kind) ||
                string.IsNullOrWhiteSpace(packageDigest) ||
                !IsValidMilliseconds(kind, milliseconds) ||
                settlementId <= 0L)
            {
                throw new ArgumentException("The Race boss penalty action is invalid.");
            }

            return string.Join(
                ":",
                ((int)kind).ToString(CultureInfo.InvariantCulture),
                packageDigest,
                milliseconds.ToString(CultureInfo.InvariantCulture),
                settlementId.ToString(CultureInfo.InvariantCulture));
        }

        public static bool TryParseActionValue(
            string value,
            string expectedPackageDigest,
            out RaceBossPenaltyKind kind,
            out long milliseconds,
            out long settlementId)
        {
            kind = 0;
            milliseconds = 0L;
            settlementId = 0L;
            string[] parts = string.IsNullOrEmpty(value)
                ? new string[0]
                : value.Split(':');
            if (parts.Length != 4 ||
                !int.TryParse(
                    parts[0],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int parsedKind) ||
                !IsSupportedKind((RaceBossPenaltyKind)parsedKind) ||
                !string.Equals(
                    parts[1],
                    expectedPackageDigest,
                    StringComparison.Ordinal) ||
                !long.TryParse(
                    parts[2],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long parsed) ||
                !IsValidMilliseconds((RaceBossPenaltyKind)parsedKind, parsed) ||
                !long.TryParse(
                    parts[3],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long parsedSettlementId) ||
                parsedSettlementId <= 0L)
            {
                return false;
            }

            kind = (RaceBossPenaltyKind)parsedKind;
            milliseconds = parsed;
            settlementId = parsedSettlementId;
            return true;
        }
    }
}
