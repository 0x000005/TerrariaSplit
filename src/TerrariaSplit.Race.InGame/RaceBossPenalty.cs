using System;
using System.Globalization;
using System.Text;

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

    internal sealed class RaceBossPenaltySchedule
    {
        private const int DifficultyCount = 4;
        private const int ValuesPerDifficulty = 2;
        private readonly int[] seconds;

        internal RaceBossPenaltySchedule(int[] values)
        {
            seconds = new int[values.Length];
            Array.Copy(values, seconds, values.Length);
        }

        public int GetBaseSeconds(RaceBossPenaltyKind kind, int gameMode)
        {
            return seconds[GetValueOffset(kind, gameMode)];
        }

        public int GetProportionalSeconds(RaceBossPenaltyKind kind, int gameMode)
        {
            return seconds[GetValueOffset(kind, gameMode) + 1];
        }

        public long GetMaximumMilliseconds(RaceBossPenaltyKind kinds)
        {
            long maximumSeconds = 0L;
            int value = (int)kinds;
            for (int bit = 1; bit <= (int)RaceBossPenaltyKind.LunaticCultist; bit <<= 1)
            {
                if ((value & bit) == 0)
                {
                    continue;
                }

                int bossOffset = GetBossIndex((RaceBossPenaltyKind)bit) * DifficultyCount * ValuesPerDifficulty;
                int bossMaximum = 0;
                for (int difficulty = 0; difficulty < DifficultyCount; difficulty++)
                {
                    int offset = bossOffset + difficulty * ValuesPerDifficulty;
                    bossMaximum = Math.Max(bossMaximum, checked(seconds[offset] + seconds[offset + 1]));
                }

                maximumSeconds = checked(maximumSeconds + bossMaximum);
            }

            return checked(maximumSeconds * 1000L);
        }

        public string Encode()
        {
            var builder = new StringBuilder(RaceBossPenalty.MaximumEncodedScheduleLength);
            builder.Append(RaceBossPenalty.ScheduleVersion);
            builder.Append(';');
            for (int index = 0; index < seconds.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder.Append(seconds[index].ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static int GetValueOffset(RaceBossPenaltyKind kind, int gameMode)
        {
            return (GetBossIndex(kind) * DifficultyCount + GetDifficultyIndex(gameMode)) * ValuesPerDifficulty;
        }

        private static int GetBossIndex(RaceBossPenaltyKind kind)
        {
            switch (kind)
            {
                case RaceBossPenaltyKind.Skeletron:
                    return 0;
                case RaceBossPenaltyKind.WallOfFlesh:
                    return 1;
                case RaceBossPenaltyKind.SkeletronPrime:
                    return 2;
                case RaceBossPenaltyKind.Twins:
                    return 3;
                case RaceBossPenaltyKind.Destroyer:
                    return 4;
                case RaceBossPenaltyKind.Plantera:
                    return 5;
                case RaceBossPenaltyKind.Golem:
                    return 6;
                case RaceBossPenaltyKind.LunaticCultist:
                    return 7;
                default:
                    throw new ArgumentOutOfRangeException("kind");
            }
        }

        private static int GetDifficultyIndex(int gameMode)
        {
            switch (gameMode)
            {
                case 3:
                    return 0;
                case 1:
                    return 2;
                case 2:
                    return 3;
                case 0:
                default:
                    return 1;
            }
        }
    }

    internal static class RaceBossPenalty
    {
        public const string ActionControlId = "race-boss-penalty";
        public const int MaximumSeconds = 86400;
        public const int ScheduleValueCount = 64;
        public const int MaximumEncodedScheduleLength = 512;
        public const string ScheduleVersion = "1";
        private const RaceBossPenaltyKind AllKinds =
            RaceBossPenaltyKind.Skeletron |
            RaceBossPenaltyKind.WallOfFlesh |
            RaceBossPenaltyKind.SkeletronPrime |
            RaceBossPenaltyKind.Twins |
            RaceBossPenaltyKind.Destroyer |
            RaceBossPenaltyKind.Plantera |
            RaceBossPenaltyKind.Golem |
            RaceBossPenaltyKind.LunaticCultist;
        public const int AllKindsMask = (int)AllKinds;
        private static readonly RaceBossPenaltySchedule Default = CreateDefaultSchedule();

        public static RaceBossPenaltySchedule DefaultSchedule
        {
            get { return Default; }
        }

        public static int NormalizeEnabledKinds(int kinds)
        {
            return kinds & AllKindsMask;
        }

        public static bool AreKindsEnabled(int enabledKinds, RaceBossPenaltyKind kinds)
        {
            int requested = (int)kinds;
            return IsSupportedKind(kinds) &&
                (NormalizeEnabledKinds(enabledKinds) & requested) == requested;
        }

        public static long CalculateMilliseconds(
            RaceBossPenaltyKind kind,
            int currentLife,
            int maximumLife,
            int gameMode)
        {
            return CalculateMilliseconds(Default, kind, currentLife, maximumLife, gameMode);
        }

        public static long CalculateMilliseconds(
            RaceBossPenaltySchedule schedule,
            RaceBossPenaltyKind kind,
            int currentLife,
            int maximumLife,
            int gameMode)
        {
            if (schedule == null || !IsSupportedKind(kind) || currentLife <= 0 || maximumLife <= 0)
            {
                return 0L;
            }

            double remainingRatio = Math.Min(1d, currentLife / (double)maximumLife);
            return (long)Math.Round(
                (schedule.GetBaseSeconds(kind, gameMode) +
                    schedule.GetProportionalSeconds(kind, gameMode) * remainingRatio) * 1000d,
                MidpointRounding.AwayFromZero);
        }

        public static bool IsValidMilliseconds(RaceBossPenaltyKind kind, long milliseconds)
        {
            return IsValidMilliseconds(Default, kind, milliseconds);
        }

        public static bool IsValidMilliseconds(
            RaceBossPenaltySchedule schedule,
            RaceBossPenaltyKind kind,
            long milliseconds)
        {
            return schedule != null &&
                AreSupportedKinds(kind) &&
                milliseconds > 0L &&
                milliseconds <= schedule.GetMaximumMilliseconds(kind);
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

        public static bool TryCreateSchedule(int[] values, out RaceBossPenaltySchedule schedule)
        {
            schedule = null;
            if (values == null || values.Length != ScheduleValueCount)
            {
                return false;
            }

            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] < 0 || values[index] > MaximumSeconds)
                {
                    return false;
                }
            }

            schedule = new RaceBossPenaltySchedule(values);
            return true;
        }

        public static bool TryParseSchedule(string value, out RaceBossPenaltySchedule schedule)
        {
            schedule = null;
            if (string.IsNullOrEmpty(value) || value.Length > MaximumEncodedScheduleLength)
            {
                return false;
            }

            string prefix = ScheduleVersion + ";";
            if (!value.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            string[] parts = value.Substring(prefix.Length).Split(',');
            if (parts.Length != ScheduleValueCount)
            {
                return false;
            }

            var values = new int[ScheduleValueCount];
            for (int index = 0; index < parts.Length; index++)
            {
                int seconds;
                if (!int.TryParse(
                        parts[index],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out seconds) ||
                    seconds < 0 ||
                    seconds > MaximumSeconds)
                {
                    return false;
                }

                values[index] = seconds;
            }

            schedule = new RaceBossPenaltySchedule(values);
            return true;
        }

        public static RaceBossPenaltySchedule ParseScheduleOrDefault(string value)
        {
            RaceBossPenaltySchedule schedule;
            return TryParseSchedule(value, out schedule) ? schedule : Default;
        }

        public static string CreateActionValue(
            RaceBossPenaltyKind kind,
            string packageDigest,
            long milliseconds,
            long settlementId)
        {
            return CreateActionValue(Default, kind, packageDigest, milliseconds, settlementId);
        }

        public static string CreateActionValue(
            RaceBossPenaltySchedule schedule,
            RaceBossPenaltyKind kind,
            string packageDigest,
            long milliseconds,
            long settlementId)
        {
            if (!AreSupportedKinds(kind) ||
                string.IsNullOrWhiteSpace(packageDigest) ||
                !IsValidMilliseconds(schedule, kind, milliseconds) ||
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
            return TryParseActionValue(
                Default,
                value,
                expectedPackageDigest,
                out kind,
                out milliseconds,
                out settlementId);
        }

        public static bool TryParseActionValue(
            RaceBossPenaltySchedule schedule,
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
            int parsedKind;
            long parsed;
            long parsedSettlementId;
            if (parts.Length != 4 ||
                !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out parsedKind) ||
                !AreSupportedKinds((RaceBossPenaltyKind)parsedKind) ||
                !string.Equals(parts[1], expectedPackageDigest, StringComparison.Ordinal) ||
                !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out parsed) ||
                !IsValidMilliseconds(schedule, (RaceBossPenaltyKind)parsedKind, parsed) ||
                !long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out parsedSettlementId) ||
                parsedSettlementId <= 0L)
            {
                return false;
            }

            kind = (RaceBossPenaltyKind)parsedKind;
            milliseconds = parsed;
            settlementId = parsedSettlementId;
            return true;
        }

        private static RaceBossPenaltySchedule CreateDefaultSchedule()
        {
            int[] values =
            {
                60, 90, 120, 180, 150, 225, 180, 270,
                60, 120, 120, 240, 150, 300, 180, 360,
                30, 120, 60, 240, 75, 300, 90, 360,
                60, 90, 120, 180, 150, 225, 180, 270,
                60, 30, 120, 60, 150, 75, 180, 90,
                90, 120, 180, 240, 225, 300, 270, 360,
                90, 120, 180, 240, 225, 300, 270, 360,
                90, 120, 180, 240, 225, 300, 270, 360
            };
            return new RaceBossPenaltySchedule(values);
        }
    }
}
