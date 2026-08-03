using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Threading;
using TerrariaSplit.Race.InGame;

namespace TerrariaSplit.WorldGuard.Payload
{
    public static partial class EntryPoint
    {
        private const int SkeletronHeadNpcType = 35;
        private const int WallOfFleshNpcType = 113;
        private const float WallOfFleshDisengageThreshold = 1f - 1f / 180f - 0.0001f;
        private const string BossPenaltySettlementCommand = "settle-race-boss";
        private static readonly object BossPenaltySync = new object();
        private static PropertyInfo bossPenaltyLocalPlayerProperty;
        private static PropertyInfo bossPenaltyGameModeProperty;
        private static FieldInfo bossPenaltyMainNpcField;
        private static FieldInfo bossPenaltyMainMyPlayerField;
        private static FieldInfo bossPenaltyMainNetModeField;
        private static FieldInfo bossPenaltyMainGameMenuField;
        private static FieldInfo bossPenaltyMainMaxTilesXField;
        private static FieldInfo bossPenaltyPlayerDeadField;
        private static FieldInfo bossPenaltyNpcActiveField;
        private static FieldInfo bossPenaltyNpcTypeField;
        private static FieldInfo bossPenaltyNpcLifeField;
        private static FieldInfo bossPenaltyNpcLifeMaxField;
        private static FieldInfo bossPenaltyNpcTimeLeftField;
        private static FieldInfo bossPenaltyNpcTargetField;
        private static FieldInfo bossPenaltyNpcLastInteractionField;
        private static FieldInfo bossPenaltyNpcPlayerInteractionField;
        private static FieldInfo bossPenaltyNpcLocalAiField;
        private static FieldInfo bossPenaltyNpcPositionField;
        private static FieldInfo bossPenaltyVectorXField;
        private static MethodInfo bossPenaltyNpcCheckDeadMethod;
        private static volatile WorldLockConfiguration pendingBossConfiguration;
        private static volatile object pendingBoss;
        private static RaceBossPenaltyKind pendingBossKind;
        private static long pendingBossSettlementId;
        private static long nextBossSettlementId;

        private static bool TryResolveRaceBossPenaltyMembers(
            Type mainType,
            Type playerType,
            Type npcType,
            out MethodInfo checkActiveMethod,
            out MethodInfo checkDeadMethod,
            out MethodInfo aiMethod)
        {
            checkActiveMethod = npcType.GetMethod(
                "CheckActive",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            checkDeadMethod = npcType.GetMethod(
                "checkDead",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            aiMethod = npcType.GetMethod(
                "AI",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            bossPenaltyLocalPlayerProperty = mainType.GetProperty(
                "LocalPlayer",
                BindingFlags.Static | BindingFlags.Public);
            bossPenaltyGameModeProperty = mainType.GetProperty(
                "GameMode",
                BindingFlags.Static | BindingFlags.Public);
            bossPenaltyMainNpcField = mainType.GetField("npc", BindingFlags.Static | BindingFlags.Public);
            bossPenaltyMainMyPlayerField = mainType.GetField("myPlayer", BindingFlags.Static | BindingFlags.Public);
            bossPenaltyMainNetModeField = mainType.GetField("netMode", BindingFlags.Static | BindingFlags.Public);
            bossPenaltyMainGameMenuField = mainType.GetField("gameMenu", BindingFlags.Static | BindingFlags.Public);
            bossPenaltyMainMaxTilesXField = mainType.GetField("maxTilesX", BindingFlags.Static | BindingFlags.Public);
            bossPenaltyPlayerDeadField = playerType.GetField("dead", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcActiveField = npcType.GetField("active", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcTypeField = npcType.GetField("type", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcLifeField = npcType.GetField("life", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcLifeMaxField = npcType.GetField("lifeMax", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcTimeLeftField = npcType.GetField("timeLeft", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcTargetField = npcType.GetField("target", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcLastInteractionField = npcType.GetField(
                "lastInteraction",
                BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcPlayerInteractionField = npcType.GetField(
                "playerInteraction",
                BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcLocalAiField = npcType.GetField("localAI", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcPositionField = npcType.GetField("position", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyVectorXField = bossPenaltyNpcPositionField == null
                ? null
                : bossPenaltyNpcPositionField.FieldType.GetField("X", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcCheckDeadMethod = checkDeadMethod;

            return checkActiveMethod != null &&
                checkDeadMethod != null &&
                aiMethod != null &&
                bossPenaltyLocalPlayerProperty != null &&
                bossPenaltyGameModeProperty != null &&
                bossPenaltyMainNpcField != null &&
                bossPenaltyMainMyPlayerField != null &&
                bossPenaltyMainNetModeField != null &&
                bossPenaltyMainGameMenuField != null &&
                bossPenaltyMainMaxTilesXField != null &&
                bossPenaltyPlayerDeadField != null &&
                bossPenaltyNpcActiveField != null &&
                bossPenaltyNpcTypeField != null &&
                bossPenaltyNpcLifeField != null &&
                bossPenaltyNpcLifeMaxField != null &&
                bossPenaltyNpcTimeLeftField != null &&
                bossPenaltyNpcTargetField != null &&
                bossPenaltyNpcLastInteractionField != null &&
                bossPenaltyNpcPlayerInteractionField != null &&
                bossPenaltyNpcLocalAiField != null &&
                bossPenaltyNpcPositionField != null &&
                bossPenaltyVectorXField != null;
        }

        private static void TryArmRaceBossPenalty(object player)
        {
            try
            {
                WorldLockConfiguration current = configuration;
                if (current == null ||
                    !current.EntryAllowed ||
                    !current.BossFailurePenaltyEnabled ||
                    player == null ||
                    bossPenaltyLocalPlayerProperty == null ||
                    bossPenaltyPlayerDeadField == null ||
                    bossPenaltyMainGameMenuField == null ||
                    !ReferenceEquals(player, bossPenaltyLocalPlayerProperty.GetValue(null, null)) ||
                    !(bool)bossPenaltyPlayerDeadField.GetValue(player) ||
                    (bool)bossPenaltyMainGameMenuField.GetValue(null))
                {
                    return;
                }

                RaceBossPenaltyKind kind;
                object boss = FindActivePenaltyBoss(out kind);
                if (boss == null)
                {
                    return;
                }

                lock (BossPenaltySync)
                {
                    if (pendingBoss != null)
                    {
                        return;
                    }

                    pendingBossConfiguration = current;
                    pendingBoss = boss;
                    pendingBossKind = kind;
                }
            }
            catch
            {
            }
        }

        private static object FindActivePenaltyBoss(out RaceBossPenaltyKind kind)
        {
            kind = 0;
            IEnumerable npcs = bossPenaltyMainNpcField == null
                ? null
                : bossPenaltyMainNpcField.GetValue(null) as IEnumerable;
            if (npcs == null)
            {
                return null;
            }

            int localPlayerIndex = (int)bossPenaltyMainMyPlayerField.GetValue(null);
            object firstActiveBoss = null;
            RaceBossPenaltyKind firstActiveKind = 0;
            foreach (object npc in npcs)
            {
                if (npc == null ||
                    !(bool)bossPenaltyNpcActiveField.GetValue(npc) ||
                    (int)bossPenaltyNpcLifeField.GetValue(npc) <= 0)
                {
                    continue;
                }

                RaceBossPenaltyKind candidateKind;
                int npcType = (int)bossPenaltyNpcTypeField.GetValue(npc);
                if (npcType == SkeletronHeadNpcType)
                {
                    candidateKind = RaceBossPenaltyKind.Skeletron;
                }
                else if (npcType == WallOfFleshNpcType)
                {
                    candidateKind = RaceBossPenaltyKind.WallOfFlesh;
                }
                else
                {
                    continue;
                }

                if ((int)bossPenaltyNpcTargetField.GetValue(npc) == localPlayerIndex)
                {
                    kind = candidateKind;
                    return npc;
                }

                if (firstActiveBoss == null)
                {
                    firstActiveBoss = npc;
                    firstActiveKind = candidateKind;
                }
            }

            kind = firstActiveKind;
            return firstActiveBoss;
        }

        private static bool SkeletronCheckActivePrefix(object __instance)
        {
            if (!IsPendingBoss(__instance, RaceBossPenaltyKind.Skeletron))
            {
                return true;
            }

            try
            {
                if (TryCancelPendingBossAfterRespawn(__instance))
                {
                    return true;
                }

                if (Interlocked.Read(ref pendingBossSettlementId) > 0L)
                {
                    bossPenaltyNpcTimeLeftField.SetValue(__instance, 2);
                    return false;
                }

                if ((int)bossPenaltyNpcTimeLeftField.GetValue(__instance) > 1)
                {
                    return true;
                }

                if (!TryBeginBossSettlement(__instance, RaceBossPenaltyKind.Skeletron))
                {
                    return true;
                }

                bossPenaltyNpcTimeLeftField.SetValue(__instance, 2);
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static bool WallOfFleshAiPrefix(object __instance)
        {
            if (!IsPendingBoss(__instance, RaceBossPenaltyKind.WallOfFlesh))
            {
                return true;
            }

            try
            {
                if (TryCancelPendingBossAfterRespawn(__instance))
                {
                    return true;
                }

                if (Interlocked.Read(ref pendingBossSettlementId) > 0L)
                {
                    return false;
                }

                float[] localAi = bossPenaltyNpcLocalAiField.GetValue(__instance) as float[];
                object position = bossPenaltyNpcPositionField.GetValue(__instance);
                float positionX = position == null
                    ? 160f
                    : (float)bossPenaltyVectorXField.GetValue(position);
                int maxTilesX = (int)bossPenaltyMainMaxTilesXField.GetValue(null);
                bool outsideWorld = positionX < 160f || positionX > (maxTilesX - 10) * 16f;
                bool targetDeathTimerExpired = localAi != null &&
                    localAi.Length > 1 &&
                    localAi[1] >= WallOfFleshDisengageThreshold;
                if (!outsideWorld && !targetDeathTimerExpired)
                {
                    return true;
                }

                return !TryBeginBossSettlement(__instance, RaceBossPenaltyKind.WallOfFlesh);
            }
            catch
            {
                return true;
            }
        }

        private static bool IsPendingBoss(object instance, RaceBossPenaltyKind kind)
        {
            object observedPendingBoss = pendingBoss;
            if (instance == null ||
                observedPendingBoss == null ||
                !ReferenceEquals(instance, observedPendingBoss))
            {
                return false;
            }

            WorldLockConfiguration current = configuration;
            lock (BossPenaltySync)
            {
                return current != null &&
                    current.EntryAllowed &&
                    ReferenceEquals(current, pendingBossConfiguration) &&
                    ReferenceEquals(instance, pendingBoss) &&
                    pendingBossKind == kind &&
                    (bool)bossPenaltyNpcActiveField.GetValue(instance);
            }
        }

        private static bool TryCancelPendingBossAfterRespawn(object instance)
        {
            object localPlayer = bossPenaltyLocalPlayerProperty.GetValue(null, null);
            if (localPlayer != null && (bool)bossPenaltyPlayerDeadField.GetValue(localPlayer))
            {
                return false;
            }

            lock (BossPenaltySync)
            {
                if (!ReferenceEquals(instance, pendingBoss) || pendingBossSettlementId > 0L)
                {
                    return false;
                }

                ClearPendingBossPenaltyLocked();
                return true;
            }
        }

        private static bool TryBeginBossSettlement(object instance, RaceBossPenaltyKind kind)
        {
            WorldLockConfiguration current = configuration;
            if (current == null ||
                !current.EntryAllowed ||
                !current.BossFailurePenaltyEnabled ||
                (int)bossPenaltyMainNetModeField.GetValue(null) != 0 ||
                (bool)bossPenaltyMainGameMenuField.GetValue(null) ||
                (int)bossPenaltyNpcTypeField.GetValue(instance) != GetNpcType(kind))
            {
                return false;
            }

            int currentLife = (int)bossPenaltyNpcLifeField.GetValue(instance);
            int maximumLife = (int)bossPenaltyNpcLifeMaxField.GetValue(instance);
            int gameMode = (int)bossPenaltyGameModeProperty.GetValue(null, null);
            long penaltyMilliseconds = RaceBossPenalty.CalculateMilliseconds(
                kind,
                currentLife,
                maximumLife,
                gameMode);
            if (!RaceBossPenalty.IsValidMilliseconds(kind, penaltyMilliseconds))
            {
                return false;
            }

            long settlementId;
            lock (BossPenaltySync)
            {
                if (!ReferenceEquals(current, pendingBossConfiguration) ||
                    !ReferenceEquals(instance, pendingBoss) ||
                    pendingBossKind != kind ||
                    pendingBossSettlementId > 0L)
                {
                    return false;
                }

                settlementId = Interlocked.Increment(ref nextBossSettlementId);
                pendingBossSettlementId = settlementId;
            }

            try
            {
                QueueRaceUiAction(
                    RaceBossPenalty.ActionControlId,
                    RaceInGameActionKind.Activate,
                    RaceBossPenalty.CreateActionValue(
                        kind,
                        current.PackageDigest,
                        penaltyMilliseconds,
                        settlementId));
                return true;
            }
            catch
            {
                lock (BossPenaltySync)
                {
                    if (pendingBossSettlementId == settlementId)
                    {
                        pendingBossSettlementId = 0L;
                    }
                }

                return false;
            }
        }

        private static bool TryHandleRaceBossPenaltyCommand(
            string command,
            out PayloadCommandResult result)
        {
            string[] parts = (command ?? string.Empty).Split(new[] { '\n' }, StringSplitOptions.None);
            if (parts.Length == 0 ||
                !string.Equals(parts[0], BossPenaltySettlementCommand, StringComparison.Ordinal))
            {
                result = null;
                return false;
            }

            int parsedKind;
            long settlementId;
            RaceBossPenaltyKind kind;
            if (parts.Length != 4 ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out parsedKind) ||
                !RaceBossPenalty.IsSupportedKind((RaceBossPenaltyKind)parsedKind) ||
                !long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out settlementId) ||
                settlementId <= 0L)
            {
                result = new PayloadCommandResult(2, "The Race boss settlement is invalid.", false);
                return true;
            }

            kind = (RaceBossPenaltyKind)parsedKind;
            lock (BossPenaltySync)
            {
                WorldLockConfiguration current = configuration;
                if (current == null ||
                    !current.EntryAllowed ||
                    !ReferenceEquals(current, pendingBossConfiguration) ||
                    pendingBoss == null ||
                    pendingBossKind != kind ||
                    pendingBossSettlementId != settlementId ||
                    !string.Equals(current.PackageDigest, parts[2], StringComparison.Ordinal))
                {
                    result = new PayloadCommandResult(3, "The Race boss settlement is no longer active.", false);
                    return true;
                }
            }

            QueueOnTerrariaMainThread(() => SettleBossAfterPenalty(kind, settlementId));
            result = new PayloadCommandResult(0, settlementId.ToString(CultureInfo.InvariantCulture), false);
            return true;
        }

        private static void SettleBossAfterPenalty(RaceBossPenaltyKind kind, long settlementId)
        {
            object boss;
            WorldLockConfiguration expectedConfiguration;
            lock (BossPenaltySync)
            {
                if (pendingBossSettlementId != settlementId ||
                    pendingBoss == null ||
                    pendingBossKind != kind)
                {
                    return;
                }

                boss = pendingBoss;
                expectedConfiguration = pendingBossConfiguration;
                ClearPendingBossPenaltyLocked();
            }

            try
            {
                WorldLockConfiguration current = configuration;
                if (current == null ||
                    !current.EntryAllowed ||
                    !ReferenceEquals(current, expectedConfiguration) ||
                    (int)bossPenaltyMainNetModeField.GetValue(null) != 0 ||
                    (bool)bossPenaltyMainGameMenuField.GetValue(null) ||
                    (int)bossPenaltyNpcTypeField.GetValue(boss) != GetNpcType(kind))
                {
                    return;
                }

                int localPlayerIndex = (int)bossPenaltyMainMyPlayerField.GetValue(null);
                bool[] interactions = bossPenaltyNpcPlayerInteractionField.GetValue(boss) as bool[];
                if (interactions != null &&
                    localPlayerIndex >= 0 &&
                    localPlayerIndex < interactions.Length)
                {
                    interactions[localPlayerIndex] = true;
                    bossPenaltyNpcLastInteractionField.SetValue(boss, localPlayerIndex);
                }

                bossPenaltyNpcActiveField.SetValue(boss, true);
                bossPenaltyNpcLifeField.SetValue(boss, 0);
                try
                {
                    bossPenaltyNpcCheckDeadMethod.Invoke(boss, null);
                }
                catch
                {
                    bossPenaltyNpcActiveField.SetValue(boss, false);
                    throw;
                }
            }
            catch
            {
            }
        }

        private static void RaceBossCheckDeadPostfix(object __instance)
        {
            object currentPendingBoss = pendingBoss;
            if (currentPendingBoss == null || !ReferenceEquals(__instance, currentPendingBoss))
            {
                return;
            }

            lock (BossPenaltySync)
            {
                if (ReferenceEquals(__instance, pendingBoss) &&
                    (!(bool)bossPenaltyNpcActiveField.GetValue(__instance) ||
                        (int)bossPenaltyNpcLifeField.GetValue(__instance) <= 0))
                {
                    ClearPendingBossPenaltyLocked();
                }
            }
        }

        private static int GetNpcType(RaceBossPenaltyKind kind)
        {
            return kind == RaceBossPenaltyKind.WallOfFlesh
                ? WallOfFleshNpcType
                : SkeletronHeadNpcType;
        }

        private static void ClearPendingBossPenaltyLocked()
        {
            pendingBossConfiguration = null;
            pendingBoss = null;
            pendingBossKind = 0;
            pendingBossSettlementId = 0L;
        }

        private static void ResetRaceBossPenalty()
        {
            lock (BossPenaltySync)
            {
                ClearPendingBossPenaltyLocked();
            }
        }
    }
}
