using System;
using System.Globalization;

namespace TerrariaSplit.Race.Determinism
{
    public static class DeterministicEventIdentity
    {
        public static string NpcDropCounterSource(int npcType, bool isBossDrop)
        {
            int identity = isBossDrop ? NormalizeNpcDropGroup(npcType) : npcType;
            return (isBossDrop ? "boss|" : "npc|") + identity.ToString(CultureInfo.InvariantCulture);
        }

        public static string NpcDropEventKey(int npcType, bool isBossDrop, long occurrence)
        {
            int identity = isBossDrop ? NormalizeNpcDropGroup(npcType) : npcType;
            return (isBossDrop ? "boss|" : "npc|") + identity.ToString(CultureInfo.InvariantCulture) + "|" +
                occurrence.ToString(CultureInfo.InvariantCulture);
        }

        public static string HardmodeAltarCounterSource(string worldKey)
        {
            return worldKey;
        }

        public static string NpcDropRuleEventKey(string parentContext, int ruleIndex)
        {
            if (string.IsNullOrWhiteSpace(parentContext))
            {
                throw new ArgumentException("The parent drop context is required.", nameof(parentContext));
            }

            if (ruleIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ruleIndex));
            }

            return parentContext + "|rule|" + ruleIndex.ToString(CultureInfo.InvariantCulture);
        }

        private static int NormalizeNpcDropGroup(int npcType)
        {
            switch (npcType)
            {
                case 13:
                case 14:
                case 15:
                    return 13;
                case 35:
                case 36:
                    return 35;
                case 113:
                case 114:
                    return 113;
                case 125:
                case 126:
                    return 125;
                case 127:
                case 128:
                case 129:
                case 130:
                case 131:
                    return 127;
                case 134:
                case 135:
                case 136:
                    return 134;
                case 245:
                case 246:
                case 247:
                case 248:
                case 249:
                    return 245;
                case 396:
                case 397:
                case 398:
                case 400:
                case 401:
                    return 398;
                default:
                    return npcType;
            }
        }
    }
}
