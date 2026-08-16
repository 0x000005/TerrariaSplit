using System;
using System.Globalization;

namespace TerrariaSplit.Race.InGame
{
    [Flags]
    internal enum RaceBossPenaltyKind
    {
        Skeletron = 1,
        WallOfFlesh = 2,
        SkeletronPrime = 4,
        Twins = 8,
        Destroyer = 16,
        Plantera = 32,
        Golem = 64,
        LunaticCultist = 128
    }

    internal static class RaceBossPenalty
    {
        public const string ActionControlId = "race-boss-penalty";
        private const long MinuteMilliseconds = 60L * 1000L;
        private const RaceBossPenaltyKind AllKinds =
            RaceBossPenaltyKind.Skeletron |
            RaceBossPenaltyKind.WallOfFlesh |
            RaceBossPenaltyKind.SkeletronPrime |
            RaceBossPenaltyKind.Twins |
            RaceBossPenaltyKind.Destroyer |
            RaceBossPenaltyKind.Plantera |
            RaceBossPenaltyKind.Golem |
            RaceBossPenaltyKind.LunaticCultist;

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
            GetPenaltyMinutes(kind, out int baseMinutes, out int proportionalMinutes);
            return (long)Math.Round(
                (baseMinutes * MinuteMilliseconds +
                    proportionalMinutes * MinuteMilliseconds * remainingRatio) *
                difficultyMultiplier,
                MidpointRounding.AwayFromZero);
        }

        public static bool IsValidMilliseconds(RaceBossPenaltyKind kind, long milliseconds)
        {
            if (!AreSupportedKinds(kind) || milliseconds <= 0L)
            {
                return false;
            }

            long maximum = 0L;
            int value = (int)kind;
            for (int bit = 1; bit <= (int)RaceBossPenaltyKind.LunaticCultist; bit <<= 1)
            {
                if ((value & bit) == 0)
                {
                    continue;
                }

                GetPenaltyMinutes(
                    (RaceBossPenaltyKind)bit,
                    out int baseMinutes,
                    out int proportionalMinutes);
                maximum = checked(
                    maximum +
                    (baseMinutes + proportionalMinutes) * MinuteMilliseconds * 3L / 2L);
            }

            return milliseconds <= maximum;
        }

        public static bool IsSupportedKind(RaceBossPenaltyKind kind)
        {
            int value = (int)kind;
            return AreSupportedKinds(kind) && (value & (value - 1)) == 0;
        }

        public static bool AreSupportedKinds(RaceBossPenaltyKind kinds)
        {
            return kinds != 0 && (kinds & ~AllKinds) == 0;
        }

        public static string CreateActionValue(
            RaceBossPenaltyKind kind,
            string packageDigest,
            long milliseconds,
            long settlementId)
        {
            if (!AreSupportedKinds(kind) ||
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
                !AreSupportedKinds((RaceBossPenaltyKind)parsedKind) ||
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

        private static void GetPenaltyMinutes(
            RaceBossPenaltyKind kind,
            out int baseMinutes,
            out int proportionalMinutes)
        {
            switch (kind)
            {
                case RaceBossPenaltyKind.Skeletron:
                    baseMinutes = 2;
                    proportionalMinutes = 3;
                    return;
                case RaceBossPenaltyKind.WallOfFlesh:
                    baseMinutes = 2;
                    proportionalMinutes = 4;
                    return;
                case RaceBossPenaltyKind.SkeletronPrime:
                    baseMinutes = 1;
                    proportionalMinutes = 4;
                    return;
                case RaceBossPenaltyKind.Twins:
                    baseMinutes = 2;
                    proportionalMinutes = 3;
                    return;
                case RaceBossPenaltyKind.Destroyer:
                    baseMinutes = 2;
                    proportionalMinutes = 1;
                    return;
                case RaceBossPenaltyKind.Plantera:
                case RaceBossPenaltyKind.Golem:
                case RaceBossPenaltyKind.LunaticCultist:
                    baseMinutes = 3;
                    proportionalMinutes = 4;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
