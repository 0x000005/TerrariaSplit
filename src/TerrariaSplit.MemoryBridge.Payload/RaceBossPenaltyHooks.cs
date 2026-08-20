using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using TerrariaSplit.Race.InGame;

namespace TerrariaSplit.MemoryBridge.Payload
{
    public static partial class EntryPoint
    {
        private const int SkeletronHeadNpcType = 35;
        private const int WallOfFleshNpcType = 113;
        private const int RetinazerNpcType = 125;
        private const int SpazmatismNpcType = 126;
        private const int SkeletronPrimeNpcType = 127;
        private const int DestroyerHeadNpcType = 134;
        private const int GolemBodyNpcType = 245;
        private const int PlanteraNpcType = 262;
        private const int LunaticCultistNpcType = 439;
        private const int RetinazerBaseLife = 20000;
        private const int SpazmatismBaseLife = 23000;
        private const float WallOfFleshDisengageThreshold = 1f - 1f / 180f - 0.0001f;
        private const int VanillaSkeletronPrimeDisengageTicks = 500;
        private const int RaceSkeletronPrimeDisengageTicks = 300;
        private const int RacePlanteraActiveTicks = 550;
        private const string BossPenaltySettlementCommand = "settle-race-boss";
        private static readonly object BossPenaltySync = new object();
        private static PropertyInfo bossPenaltyLocalPlayerProperty;
        private static PropertyInfo bossPenaltyGameModeProperty;
        private static FieldInfo bossPenaltyMainNpcField;
        private static FieldInfo bossPenaltyMainMyPlayerField;
        private static FieldInfo bossPenaltyMainNetModeField;
        private static FieldInfo bossPenaltyMainGameMenuField;
        private static FieldInfo bossPenaltyMainMaxTilesXField;
        private static FieldInfo bossPenaltyMainRockLayerField;
        private static FieldInfo bossPenaltyMainSpriteBatchField;
        private static FieldInfo bossPenaltyMainScreenWidthField;
        private static FieldInfo bossPenaltyMainScreenHeightField;
        private static FieldInfo bossPenaltyMainMouseXField;
        private static FieldInfo bossPenaltyMainMouseYField;
        private static FieldInfo bossPenaltyMainMouseLeftField;
        private static FieldInfo bossPenaltyMainMouseLeftReleaseField;
        private static FieldInfo bossPenaltyMainDayTimeField;
        private static FieldInfo bossPenaltyPlayerDeadField;
        private static FieldInfo bossPenaltyPlayerLastDeathPositionField;
        private static FieldInfo bossPenaltyPlayerPositionField;
        private static FieldInfo bossPenaltyPlayerVelocityField;
        private static FieldInfo bossPenaltyPlayerOldPositionField;
        private static FieldInfo bossPenaltyPlayerNetOffsetField;
        private static FieldInfo bossPenaltyPlayerWidthField;
        private static FieldInfo bossPenaltyPlayerHeightField;
        private static FieldInfo bossPenaltyPlayerFallStartField;
        private static FieldInfo bossPenaltyPlayerFallStart2Field;
        private static FieldInfo bossPenaltyNpcActiveField;
        private static FieldInfo bossPenaltyNpcTypeField;
        private static FieldInfo bossPenaltyNpcLifeField;
        private static FieldInfo bossPenaltyNpcLifeMaxField;
        private static FieldInfo bossPenaltyNpcTimeLeftField;
        private static FieldInfo bossPenaltyNpcActiveTimeField;
        private static FieldInfo bossPenaltyNpcLastInteractionField;
        private static FieldInfo bossPenaltyNpcPlayerInteractionField;
        private static FieldInfo bossPenaltyNpcLocalAiField;
        private static FieldInfo bossPenaltyNpcPositionField;
        private static FieldInfo bossPenaltyNpcWidthField;
        private static FieldInfo bossPenaltyNpcHeightField;
        private static FieldInfo bossPenaltyVectorXField;
        private static FieldInfo bossPenaltyVectorYField;
        private static Type bossPenaltyColorType;
        private static Type bossPenaltySpriteEffectsType;
        private static FieldInfo bossPenaltyDeathTextFontField;
        private static PropertyInfo bossPenaltyDeathTextFontValueProperty;
        private static PropertyInfo bossPenaltyTransparentColorProperty;
        private static MethodInfo bossPenaltyDeathTextMeasureStringMethod;
        private static MethodInfo bossPenaltyDeathTextDrawStringMethod;
        private static MethodInfo bossPenaltyPlayerGetDeathAlphaMethod;
        private static MethodInfo bossPenaltyNpcCheckDeadMethod;
        private static MethodInfo bossPenaltyFindClosestTeleportSpotMethod;
        private static readonly List<PendingBossPenaltySettlement> pendingBossSettlements =
            new List<PendingBossPenaltySettlement>();
        private static volatile PendingBossDeathContext pendingBossDeathContext;
        private static long nextBossSettlementId;

        [ThreadStatic]
        private static object activeBossLootPosition;

        private static bool TryResolveRaceBossPenaltyMembers(
            Type mainType,
            Type playerType,
            Type npcType,
            Type itemType,
            Type entitySourceType,
            Type playerSpawnContextType,
            Type teleportHelpersType,
            out MethodInfo checkActiveMethod,
            out MethodInfo encourageDespawnMethod,
            out MethodInfo deathInterfaceDrawMethod,
            out MethodInfo checkDeadMethod,
            out MethodInfo aiMethod,
            out MethodInfo playerSpawnMethod,
            out MethodInfo itemNewItemMethod)
        {
            checkActiveMethod = npcType.GetMethod(
                "CheckActive",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            encourageDespawnMethod = npcType.GetMethod(
                "EncourageDespawn",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(int) },
                null);
            deathInterfaceDrawMethod = mainType.GetMethod(
                "DrawInterface_35_YouDied",
                BindingFlags.Static | BindingFlags.NonPublic,
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
            playerSpawnMethod = playerType.GetMethod(
                "Spawn",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { playerSpawnContextType },
                null);
            itemNewItemMethod = itemType
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .SingleOrDefault(method =>
                {
                    if (!string.Equals(method.Name, "NewItem", StringComparison.Ordinal))
                    {
                        return false;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 12 &&
                        parameters[0].ParameterType == entitySourceType &&
                        parameters.Skip(1).Take(6).All(parameter => parameter.ParameterType == typeof(int)) &&
                        parameters[7].ParameterType == typeof(bool) &&
                        parameters[8].ParameterType == typeof(int) &&
                        string.Equals(parameters[9].ParameterType.FullName, "Terraria.NewItemOwnership", StringComparison.Ordinal) &&
                        parameters[10].ParameterType.IsGenericType &&
                        parameters[10].ParameterType.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                        string.Equals(parameters[10].ParameterType.GetGenericArguments()[0].FullName, "Microsoft.Xna.Framework.Vector2", StringComparison.Ordinal) &&
                        string.Equals(parameters[11].ParameterType.FullName, "Terraria.Item+NewItemModifier", StringComparison.Ordinal);
                });
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
            bossPenaltyMainRockLayerField = mainType.GetField("rockLayer", BindingFlags.Static | BindingFlags.Public);
            bossPenaltyMainSpriteBatchField = mainType.GetField("spriteBatch", BindingFlags.Static | BindingFlags.Public);
            bossPenaltyMainScreenWidthField = mainType.GetField("screenWidth", BindingFlags.Static | BindingFlags.Public);
            bossPenaltyMainScreenHeightField = mainType.GetField("screenHeight", BindingFlags.Static | BindingFlags.Public);
            bossPenaltyMainMouseXField = mainType.GetField("mouseX", BindingFlags.Static | BindingFlags.Public);
            bossPenaltyMainMouseYField = mainType.GetField("mouseY", BindingFlags.Static | BindingFlags.Public);
            bossPenaltyMainMouseLeftField = mainType.GetField("mouseLeft", BindingFlags.Static | BindingFlags.Public);
            bossPenaltyMainMouseLeftReleaseField = mainType.GetField(
                "mouseLeftRelease",
                BindingFlags.Static | BindingFlags.Public);
            bossPenaltyMainDayTimeField = mainType.GetField("dayTime", BindingFlags.Static | BindingFlags.Public);
            bossPenaltyPlayerDeadField = playerType.GetField("dead", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyPlayerLastDeathPositionField = playerType.GetField("lastDeathPostion", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyPlayerPositionField = playerType.GetField("position", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyPlayerVelocityField = playerType.GetField("velocity", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyPlayerOldPositionField = playerType.GetField("oldPosition", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyPlayerNetOffsetField = playerType.GetField("netOffset", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyPlayerWidthField = playerType.GetField("width", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyPlayerHeightField = playerType.GetField("height", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyPlayerFallStartField = playerType.GetField("fallStart", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyPlayerFallStart2Field = playerType.GetField("fallStart2", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcActiveField = npcType.GetField("active", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcTypeField = npcType.GetField("type", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcLifeField = npcType.GetField("life", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcLifeMaxField = npcType.GetField("lifeMax", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcTimeLeftField = npcType.GetField("timeLeft", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcLastInteractionField = npcType.GetField(
                "lastInteraction",
                BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcActiveTimeField = npcType.GetField(
                "activeTime",
                BindingFlags.Static | BindingFlags.NonPublic);
            bossPenaltyNpcPlayerInteractionField = npcType.GetField(
                "playerInteraction",
                BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcLocalAiField = npcType.GetField("localAI", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcPositionField = npcType.GetField("position", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcWidthField = npcType.GetField("width", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyNpcHeightField = npcType.GetField("height", BindingFlags.Instance | BindingFlags.Public);
            Type vectorType = bossPenaltyNpcPositionField == null
                ? null
                : bossPenaltyNpcPositionField.FieldType;
            bossPenaltyVectorXField = vectorType == null
                ? null
                : vectorType.GetField("X", BindingFlags.Instance | BindingFlags.Public);
            bossPenaltyVectorYField = vectorType == null
                ? null
                : vectorType.GetField("Y", BindingFlags.Instance | BindingFlags.Public);
            Type colorType = vectorType == null
                ? null
                : vectorType.Assembly.GetType("Microsoft.Xna.Framework.Color", false);
            bossPenaltyColorType = colorType;
            Type fontAssetsType = mainType.Assembly.GetType("Terraria.GameContent.FontAssets", false);
            bossPenaltyDeathTextFontField = fontAssetsType == null
                ? null
                : fontAssetsType.GetField("DeathText", BindingFlags.Static | BindingFlags.Public);
            bossPenaltyDeathTextFontValueProperty = bossPenaltyDeathTextFontField == null
                ? null
                : bossPenaltyDeathTextFontField.FieldType.GetProperty(
                    "Value",
                    BindingFlags.Instance | BindingFlags.Public);
            Type deathTextFontType = bossPenaltyDeathTextFontValueProperty == null
                ? null
                : bossPenaltyDeathTextFontValueProperty.PropertyType;
            bossPenaltyDeathTextMeasureStringMethod = deathTextFontType == null
                ? null
                : deathTextFontType.GetMethod(
                    "MeasureString",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(string) },
                    null);
            bossPenaltyTransparentColorProperty = colorType == null
                ? null
                : colorType.GetProperty("Transparent", BindingFlags.Static | BindingFlags.Public);
            bossPenaltyPlayerGetDeathAlphaMethod = colorType == null
                ? null
                : playerType.GetMethod(
                    "GetDeathAlpha",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { colorType },
                    null);
            bossPenaltySpriteEffectsType = bossPenaltyMainSpriteBatchField == null
                ? null
                : bossPenaltyMainSpriteBatchField.FieldType.Assembly.GetType(
                    "Microsoft.Xna.Framework.Graphics.SpriteEffects",
                    false);
            bossPenaltyDeathTextDrawStringMethod = FindBossPenaltyDeathTextDrawStringMethod(
                deathTextFontType,
                vectorType,
                colorType);
            bossPenaltyFindClosestTeleportSpotMethod = vectorType == null
                ? null
                : teleportHelpersType.GetMethod(
                    "FindClosestTeleportSpotNoSpace",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { playerType, vectorType.MakeByRefType() },
                    null);
            bossPenaltyNpcCheckDeadMethod = checkDeadMethod;

            return checkActiveMethod != null &&
                checkDeadMethod != null &&
                aiMethod != null &&
                encourageDespawnMethod != null &&
                deathInterfaceDrawMethod != null &&
                playerSpawnMethod != null &&
                itemNewItemMethod != null &&
                bossPenaltyLocalPlayerProperty != null &&
                bossPenaltyGameModeProperty != null &&
                bossPenaltyMainNpcField != null &&
                bossPenaltyMainMyPlayerField != null &&
                bossPenaltyMainNetModeField != null &&
                bossPenaltyMainGameMenuField != null &&
                bossPenaltyMainMaxTilesXField != null &&
                bossPenaltyMainRockLayerField != null &&
                bossPenaltyMainSpriteBatchField != null &&
                bossPenaltyMainScreenWidthField != null &&
                bossPenaltyMainScreenHeightField != null &&
                bossPenaltyMainMouseXField != null &&
                bossPenaltyMainMouseYField != null &&
                bossPenaltyMainMouseLeftField != null &&
                bossPenaltyMainMouseLeftReleaseField != null &&
                bossPenaltyMainDayTimeField != null &&
                bossPenaltyPlayerDeadField != null &&
                bossPenaltyPlayerLastDeathPositionField != null &&
                bossPenaltyPlayerPositionField != null &&
                bossPenaltyPlayerVelocityField != null &&
                bossPenaltyPlayerOldPositionField != null &&
                bossPenaltyPlayerNetOffsetField != null &&
                bossPenaltyPlayerWidthField != null &&
                bossPenaltyPlayerHeightField != null &&
                bossPenaltyPlayerFallStartField != null &&
                bossPenaltyPlayerFallStart2Field != null &&
                bossPenaltyNpcActiveField != null &&
                bossPenaltyNpcTypeField != null &&
                bossPenaltyNpcLifeField != null &&
                bossPenaltyNpcLifeMaxField != null &&
                bossPenaltyNpcTimeLeftField != null &&
                bossPenaltyNpcLastInteractionField != null &&
                bossPenaltyNpcPlayerInteractionField != null &&
                bossPenaltyNpcActiveTimeField != null &&
                bossPenaltyNpcLocalAiField != null &&
                bossPenaltyNpcPositionField != null &&
                bossPenaltyNpcWidthField != null &&
                bossPenaltyNpcHeightField != null &&
                bossPenaltyVectorXField != null &&
                bossPenaltyVectorYField != null &&
                bossPenaltyColorType != null &&
                bossPenaltySpriteEffectsType != null &&
                bossPenaltyDeathTextFontField != null &&
                bossPenaltyDeathTextFontValueProperty != null &&
                bossPenaltyTransparentColorProperty != null &&
                bossPenaltyDeathTextMeasureStringMethod != null &&
                bossPenaltyDeathTextDrawStringMethod != null &&
                bossPenaltyPlayerGetDeathAlphaMethod != null &&
                bossPenaltyFindClosestTeleportSpotMethod != null &&
                bossPenaltyNpcCheckDeadMethod != null;
        }

        private static MethodInfo FindBossPenaltyDeathTextDrawStringMethod(
            Type fontType,
            Type vectorType,
            Type colorType)
        {
            if (fontType == null ||
                vectorType == null ||
                colorType == null ||
                bossPenaltyMainSpriteBatchField == null ||
                bossPenaltySpriteEffectsType == null)
            {
                return null;
            }

            Type extensionsType = fontType.Assembly.GetType(
                "ReLogic.Graphics.DynamicSpriteFontExtensionMethods",
                false);
            if (extensionsType == null)
            {
                return null;
            }

            MethodInfo[] methods = extensionsType.GetMethods(BindingFlags.Static | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                ParameterInfo[] parameters = method.GetParameters();
                if (string.Equals(method.Name, "DrawString", StringComparison.Ordinal) &&
                    parameters.Length == 12 &&
                    parameters[0].ParameterType == bossPenaltyMainSpriteBatchField.FieldType &&
                    parameters[1].ParameterType == fontType &&
                    parameters[2].ParameterType == typeof(string) &&
                    parameters[3].ParameterType == vectorType &&
                    parameters[4].ParameterType == colorType &&
                    parameters[5].ParameterType == typeof(float) &&
                    parameters[6].ParameterType == vectorType &&
                    parameters[7].ParameterType == typeof(float) &&
                    parameters[8].ParameterType == bossPenaltySpriteEffectsType &&
                    parameters[9].ParameterType == typeof(float) &&
                    parameters[10].ParameterType == vectorType.MakeArrayType() &&
                    parameters[11].ParameterType == colorType.MakeArrayType())
                {
                    return method;
                }
            }

            return null;
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
                    (int)bossPenaltyMainNetModeField.GetValue(null) != 0 ||
                    (bool)bossPenaltyMainGameMenuField.GetValue(null))
                {
                    return;
                }

                List<PendingBossEncounter> encounters = FindActivePenaltyBosses(current);
                if (encounters.Count == 0)
                {
                    return;
                }

                object deathPosition = bossPenaltyPlayerLastDeathPositionField.GetValue(player);
                lock (BossPenaltySync)
                {
                    pendingBossDeathContext = new PendingBossDeathContext(
                        current,
                        encounters,
                        deathPosition);
                }
            }
            catch
            {
            }
        }

        private static List<PendingBossEncounter> FindActivePenaltyBosses(
            WorldLockConfiguration current)
        {
            var encounters = new List<PendingBossEncounter>();
            IEnumerable npcs = bossPenaltyMainNpcField == null
                ? null
                : bossPenaltyMainNpcField.GetValue(null) as IEnumerable;
            if (npcs == null)
            {
                return encounters;
            }

            foreach (object npc in npcs)
            {
                if (npc == null ||
                    !(bool)bossPenaltyNpcActiveField.GetValue(npc) ||
                    (int)bossPenaltyNpcLifeField.GetValue(npc) <= 0)
                {
                    continue;
                }

                int npcType = (int)bossPenaltyNpcTypeField.GetValue(npc);
                RaceBossPenaltyKind kind = GetPenaltyKind(npcType);
                if (!RaceBossPenalty.AreKindsEnabled(current.BossPenaltyEnabledKinds, kind))
                {
                    continue;
                }

                PendingBossEncounter encounter = FindEncounter(encounters, kind);
                if (encounter == null)
                {
                    encounter = new PendingBossEncounter(kind);
                    encounters.Add(encounter);
                }

                encounter.Members.Add(npc);
                encounter.MaximumLife += Math.Max(0, (int)bossPenaltyNpcLifeMaxField.GetValue(npc));
            }

            PendingBossEncounter twins = FindEncounter(encounters, RaceBossPenaltyKind.Twins);
            if (twins != null && twins.Members.Count == 1)
            {
                object remainingTwin = twins.Members[0];
                int remainingType = (int)bossPenaltyNpcTypeField.GetValue(remainingTwin);
                int remainingMaximumLife = (int)bossPenaltyNpcLifeMaxField.GetValue(remainingTwin);
                int missingMaximumLife = remainingType == RetinazerNpcType
                    ? (int)Math.Round(
                        remainingMaximumLife * (SpazmatismBaseLife / (double)RetinazerBaseLife),
                        MidpointRounding.AwayFromZero)
                    : (int)Math.Round(
                        remainingMaximumLife * (RetinazerBaseLife / (double)SpazmatismBaseLife),
                        MidpointRounding.AwayFromZero);
                twins.MaximumLife += Math.Max(0, missingMaximumLife);
            }

            encounters.Sort((left, right) => ((int)left.Kind).CompareTo((int)right.Kind));
            return encounters;
        }

        private static PendingBossEncounter FindEncounter(
            List<PendingBossEncounter> encounters,
            RaceBossPenaltyKind kind)
        {
            for (int i = 0; i < encounters.Count; i++)
            {
                if (encounters[i].Kind == kind)
                {
                    return encounters[i];
                }
            }

            return null;
        }

        private static void RacePlanteraCheckActivePrefix(object __instance, out int __state)
        {
            __state = -1;
            try
            {
                if (!IsRaceBossDisengageOverrideEnabled(RaceBossPenaltyKind.Plantera) ||
                    (int)bossPenaltyNpcTypeField.GetValue(__instance) != PlanteraNpcType)
                {
                    return;
                }

                __state = (int)bossPenaltyNpcActiveTimeField.GetValue(null);
                bossPenaltyNpcActiveTimeField.SetValue(null, RacePlanteraActiveTicks);
            }
            catch
            {
                if (__state >= 0)
                {
                    try
                    {
                        bossPenaltyNpcActiveTimeField.SetValue(null, __state);
                    }
                    catch
                    {
                    }
                }

                __state = -1;
            }
        }

        private static Exception RacePlanteraCheckActiveFinalizer(Exception __exception, int __state)
        {
            if (__state >= 0)
            {
                try
                {
                    bossPenaltyNpcActiveTimeField.SetValue(null, __state);
                }
                catch
                {
                }
            }

            return __exception;
        }

        private static void RaceBossEncourageDespawnPrefix(object __instance, ref int __0)
        {
            try
            {
                if (__0 == VanillaSkeletronPrimeDisengageTicks &&
                    IsRaceBossDisengageOverrideEnabled(RaceBossPenaltyKind.SkeletronPrime) &&
                    (int)bossPenaltyNpcTypeField.GetValue(__instance) == SkeletronPrimeNpcType)
                {
                    __0 = RaceSkeletronPrimeDisengageTicks;
                }
            }
            catch
            {
            }
        }

        private static bool IsRaceBossDisengageOverrideEnabled(RaceBossPenaltyKind kind)
        {
            WorldLockConfiguration current = configuration;
            return current != null &&
                current.EntryAllowed &&
                current.BossFailurePenaltyEnabled &&
                RaceBossPenalty.AreKindsEnabled(current.BossPenaltyEnabledKinds, kind) &&
                (int)bossPenaltyMainNetModeField.GetValue(null) == 0 &&
                !(bool)bossPenaltyMainGameMenuField.GetValue(null);
        }

        private static bool RaceBossCheckActivePrefix(object __instance)
        {
            PendingBossPenaltySettlement batch = null;
            try
            {
                PendingBossEncounter encounter;
                if (TryGetPendingBoss(__instance, out batch, out encounter))
                {
                    if (Interlocked.Read(ref batch.SettlementId) > 0L)
                    {
                        bossPenaltyNpcTimeLeftField.SetValue(__instance, 2);
                        return false;
                    }

                    return true;
                }

                RaceBossPenaltyKind kind = GetPenaltyKind(
                    (int)bossPenaltyNpcTypeField.GetValue(__instance));
                if (!RaceBossPenalty.IsSupportedKind(kind) ||
                    (int)bossPenaltyNpcTimeLeftField.GetValue(__instance) > 1)
                {
                    return true;
                }

                batch = TryCreateBossSettlement(__instance, kind);
                if (batch == null || !TryBeginBossSettlement(batch))
                {
                    RollBackBossSettlement(batch);
                    return true;
                }

                bossPenaltyNpcTimeLeftField.SetValue(__instance, 2);
                return false;
            }
            catch
            {
                RollBackBossSettlement(batch);
                return true;
            }
        }

        private static bool RaceBossAiPrefix(object __instance)
        {
            PendingBossPenaltySettlement batch = null;
            try
            {
                PendingBossEncounter encounter;
                if (TryGetPendingBoss(__instance, out batch, out encounter))
                {
                    return Interlocked.Read(ref batch.SettlementId) <= 0L;
                }

                RaceBossPenaltyKind kind = GetPenaltyKind(
                    (int)bossPenaltyNpcTypeField.GetValue(__instance));
                if (!RaceBossPenalty.IsSupportedKind(kind) ||
                    !ShouldBeginBossSettlementFromAi(__instance, kind))
                {
                    return true;
                }

                batch = TryCreateBossSettlement(__instance, kind);
                if (batch == null || !TryBeginBossSettlement(batch))
                {
                    RollBackBossSettlement(batch);
                    return true;
                }

                return false;
            }
            catch
            {
                RollBackBossSettlement(batch);
                return true;
            }
        }

        private static bool ShouldBeginBossSettlementFromAi(
            object instance,
            RaceBossPenaltyKind kind)
        {
            if (kind == RaceBossPenaltyKind.WallOfFlesh)
            {
                float[] localAi = bossPenaltyNpcLocalAiField.GetValue(instance) as float[];
                float positionX = GetVectorX(bossPenaltyNpcPositionField.GetValue(instance));
                int maxTilesX = (int)bossPenaltyMainMaxTilesXField.GetValue(null);
                bool outsideWorld = positionX < 160f || positionX > (maxTilesX - 10) * 16f;
                bool targetDeathTimerExpired = localAi != null &&
                    localAi.Length > 1 &&
                    localAi[1] >= WallOfFleshDisengageThreshold;
                return outsideWorld || targetDeathTimerExpired;
            }

            object localPlayer = bossPenaltyLocalPlayerProperty.GetValue(null, null);
            if (localPlayer == null)
            {
                return false;
            }

            bool playerDead = (bool)bossPenaltyPlayerDeadField.GetValue(localPlayer);

            if (kind == RaceBossPenaltyKind.Destroyer)
            {
                float positionY = GetVectorY(bossPenaltyNpcPositionField.GetValue(instance));
                double rockLayer = (double)bossPenaltyMainRockLayerField.GetValue(null);
                bool dayTime = (bool)bossPenaltyMainDayTimeField.GetValue(null);
                return (playerDead || dayTime) && positionY > rockLayer * 16d;
            }

            if (kind == RaceBossPenaltyKind.Golem)
            {
                return GetCenterManhattanDistance(instance, localPlayer) > 3000f;
            }

            return playerDead && kind == RaceBossPenaltyKind.LunaticCultist;
        }

        private static bool TryGetPendingBoss(
            object instance,
            out PendingBossPenaltySettlement batch,
            out PendingBossEncounter encounter)
        {
            batch = null;
            encounter = null;
            if (instance == null)
            {
                return false;
            }

            WorldLockConfiguration current = configuration;
            lock (BossPenaltySync)
            {
                if (current == null || !current.EntryAllowed)
                {
                    return false;
                }

                for (int i = 0; i < pendingBossSettlements.Count; i++)
                {
                    PendingBossPenaltySettlement candidateSettlement = pendingBossSettlements[i];
                    if (!ReferenceEquals(current, candidateSettlement.Configuration))
                    {
                        continue;
                    }

                    if (ContainsBoss(candidateSettlement.Encounter, instance))
                    {
                        batch = candidateSettlement;
                        encounter = candidateSettlement.Encounter;
                        return true;
                    }
                }
            }

            return false;
        }

        private static PendingBossPenaltySettlement TryCreateBossSettlement(
            object triggerBoss,
            RaceBossPenaltyKind triggerKind)
        {
            WorldLockConfiguration current = configuration;
            if (current == null ||
                !current.EntryAllowed ||
                !current.BossFailurePenaltyEnabled ||
                !RaceBossPenalty.AreKindsEnabled(current.BossPenaltyEnabledKinds, triggerKind) ||
                triggerBoss == null ||
                (int)bossPenaltyMainNetModeField.GetValue(null) != 0 ||
                (bool)bossPenaltyMainGameMenuField.GetValue(null))
            {
                return null;
            }

            lock (BossPenaltySync)
            {
                PendingBossDeathContext deathContext = pendingBossDeathContext;
                PendingBossEncounter deathEncounter = deathContext == null ||
                    !ReferenceEquals(current, deathContext.Configuration)
                        ? null
                        : FindEncounterContainingBoss(deathContext.Encounters, triggerBoss);
                if (deathEncounter != null)
                {
                    if (!deathEncounter.PenaltyEnabled || deathEncounter.SettlementStarted)
                    {
                        return null;
                    }

                    deathEncounter.SettlementStarted = true;
                    deathContext.PenaltyTriggered = true;
                    var batch = new PendingBossPenaltySettlement(
                        current,
                        deathEncounter,
                        deathContext.DeathPosition,
                        deathContext);
                    pendingBossSettlements.Add(batch);
                    return batch;
                }
            }

            List<PendingBossEncounter> activeEncounters = FindActivePenaltyBosses(current);
            PendingBossEncounter automaticEncounter = FindEncounter(activeEncounters, triggerKind);
            if (automaticEncounter == null ||
                !ContainsBoss(automaticEncounter, triggerBoss) ||
                !HasActiveBossMember(automaticEncounter))
            {
                return null;
            }

            lock (BossPenaltySync)
            {
                if (!ReferenceEquals(current, configuration) ||
                    FindPendingBossSettlementLocked(triggerBoss) != null)
                {
                    return null;
                }

                automaticEncounter.SettlementStarted = true;
                var batch = new PendingBossPenaltySettlement(
                    current,
                    automaticEncounter,
                    null,
                    null);
                pendingBossSettlements.Add(batch);
                return batch;
            }
        }

        private static void RollBackBossSettlement(PendingBossPenaltySettlement batch)
        {
            if (batch == null)
            {
                return;
            }

            lock (BossPenaltySync)
            {
                if (!pendingBossSettlements.Contains(batch) || batch.SettlementId > 0L)
                {
                    return;
                }

                batch.Encounter.SettlementStarted = false;
                batch.Encounter.PenaltyMilliseconds = 0L;

                if (batch.DeathContext != null)
                {
                    bool remainingPenalty = false;
                    for (int i = 0; i < batch.DeathContext.Encounters.Count; i++)
                    {
                        if (batch.DeathContext.Encounters[i].SettlementStarted)
                        {
                            remainingPenalty = true;
                            break;
                        }
                    }

                    batch.DeathContext.PenaltyTriggered = remainingPenalty;
                }

                pendingBossSettlements.Remove(batch);
            }
        }

        private static PendingBossPenaltySettlement FindPendingBossSettlementLocked(object boss)
        {
            for (int i = 0; i < pendingBossSettlements.Count; i++)
            {
                PendingBossPenaltySettlement batch = pendingBossSettlements[i];
                if (ContainsBoss(batch.Encounter, boss))
                {
                    return batch;
                }
            }

            return null;
        }

        private static PendingBossPenaltySettlement FindPendingBossSettlementLocked(long settlementId)
        {
            for (int i = 0; i < pendingBossSettlements.Count; i++)
            {
                PendingBossPenaltySettlement batch = pendingBossSettlements[i];
                if (batch.SettlementId == settlementId)
                {
                    return batch;
                }
            }

            return null;
        }

        private static PendingBossEncounter FindEncounterContainingBoss(
            List<PendingBossEncounter> encounters,
            object boss)
        {
            if (encounters == null || boss == null)
            {
                return null;
            }

            for (int i = 0; i < encounters.Count; i++)
            {
                if (ContainsBoss(encounters[i], boss))
                {
                    return encounters[i];
                }
            }

            return null;
        }

        private static bool ContainsBoss(PendingBossEncounter encounter, object boss)
        {
            for (int i = 0; i < encounter.Members.Count; i++)
            {
                if (ReferenceEquals(encounter.Members[i], boss))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasActiveBossMember(PendingBossEncounter encounter)
        {
            for (int i = 0; i < encounter.Members.Count; i++)
            {
                object member = encounter.Members[i];
                if (member != null &&
                    (bool)bossPenaltyNpcActiveField.GetValue(member) &&
                    (int)bossPenaltyNpcLifeField.GetValue(member) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryBeginBossSettlement(PendingBossPenaltySettlement batch)
        {
            WorldLockConfiguration current = configuration;
            if (current == null ||
                !current.EntryAllowed ||
                !current.BossFailurePenaltyEnabled ||
                !ReferenceEquals(current, batch.Configuration) ||
                !RaceBossPenalty.AreKindsEnabled(
                    current.BossPenaltyEnabledKinds,
                    batch.Encounter.Kind) ||
                (int)bossPenaltyMainNetModeField.GetValue(null) != 0 ||
                (bool)bossPenaltyMainGameMenuField.GetValue(null))
            {
                return false;
            }

            int gameMode = (int)bossPenaltyGameModeProperty.GetValue(null, null);
            PendingBossEncounter encounter = batch.Encounter;
            int currentLife = 0;
            for (int i = 0; i < encounter.Members.Count; i++)
            {
                object member = encounter.Members[i];
                if ((bool)bossPenaltyNpcActiveField.GetValue(member))
                {
                    currentLife += Math.Max(0, (int)bossPenaltyNpcLifeField.GetValue(member));
                }
            }

            RaceBossPenaltyKind kind = encounter.Kind;
            long penaltyMilliseconds = RaceBossPenalty.CalculateMilliseconds(
                current.BossPenaltySchedule,
                kind,
                currentLife,
                encounter.MaximumLife,
                gameMode);
            encounter.PenaltyMilliseconds = penaltyMilliseconds;

            if (!RaceBossPenalty.IsSupportedKind(kind) ||
                !RaceBossPenalty.IsValidMilliseconds(
                    current.BossPenaltySchedule,
                    kind,
                    penaltyMilliseconds))
            {
                return false;
            }

            long settlementId;
            lock (BossPenaltySync)
            {
                if (!pendingBossSettlements.Contains(batch) ||
                    !ReferenceEquals(current, batch.Configuration) ||
                    batch.SettlementId > 0L)
                {
                    return false;
                }

                settlementId = Interlocked.Increment(ref nextBossSettlementId);
                batch.SettlementKind = kind;
                batch.SettlementId = settlementId;
            }

            try
            {
                QueueRaceUiAction(
                    RaceBossPenalty.ActionControlId,
                    RaceInGameActionKind.Activate,
                    RaceBossPenalty.CreateActionValue(
                        current.BossPenaltySchedule,
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
                    if (pendingBossSettlements.Contains(batch) &&
                        batch.SettlementId == settlementId)
                    {
                        batch.SettlementKind = 0;
                        batch.SettlementId = 0L;
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
                PendingBossPenaltySettlement batch = FindPendingBossSettlementLocked(settlementId);
                if (current == null ||
                    !current.EntryAllowed ||
                    batch == null ||
                    !ReferenceEquals(current, batch.Configuration) ||
                    !RaceBossPenalty.AreKindsEnabled(current.BossPenaltyEnabledKinds, kind) ||
                    batch.SettlementKind != kind ||
                    batch.SettlementId != settlementId ||
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
            PendingBossPenaltySettlement batch;
            lock (BossPenaltySync)
            {
                batch = FindPendingBossSettlementLocked(settlementId);
                if (batch == null ||
                    batch.SettlementId != settlementId ||
                    batch.SettlementKind != kind)
                {
                    return;
                }

                pendingBossSettlements.Remove(batch);
            }

            try
            {
                WorldLockConfiguration current = configuration;
                if (current == null ||
                    !current.EntryAllowed ||
                    !ReferenceEquals(current, batch.Configuration) ||
                    !RaceBossPenalty.AreKindsEnabled(current.BossPenaltyEnabledKinds, kind) ||
                    (int)bossPenaltyMainNetModeField.GetValue(null) != 0 ||
                    (bool)bossPenaltyMainGameMenuField.GetValue(null))
                {
                    return;
                }

                int localPlayerIndex = (int)bossPenaltyMainMyPlayerField.GetValue(null);
                PendingBossEncounter encounter = batch.Encounter;
                for (int i = 0; i < encounter.Members.Count; i++)
                {
                    ApplyBossPlayerInteraction(encounter.Members[i], localPlayerIndex);
                }

                for (int i = 0; i < encounter.Members.Count; i++)
                {
                    SettleBossMember(encounter.Members[i], encounter.Kind, batch.DeathPosition);
                }
            }
            catch
            {
            }
        }

        private static void ApplyBossPlayerInteraction(object boss, int localPlayerIndex)
        {
            if (boss == null)
            {
                return;
            }

            bool[] interactions = bossPenaltyNpcPlayerInteractionField.GetValue(boss) as bool[];
            if (interactions != null &&
                localPlayerIndex >= 0 &&
                localPlayerIndex < interactions.Length)
            {
                interactions[localPlayerIndex] = true;
                bossPenaltyNpcLastInteractionField.SetValue(boss, localPlayerIndex);
            }
        }

        private static void SettleBossMember(
            object boss,
            RaceBossPenaltyKind kind,
            object deathPosition)
        {
            if (boss == null ||
                !(bool)bossPenaltyNpcActiveField.GetValue(boss) ||
                (int)bossPenaltyNpcLifeField.GetValue(boss) <= 0 ||
                GetPenaltyKind((int)bossPenaltyNpcTypeField.GetValue(boss)) != kind)
            {
                return;
            }

            bossPenaltyNpcLifeField.SetValue(boss, 0);
            object previousLootPosition = activeBossLootPosition;
            if (kind != RaceBossPenaltyKind.WallOfFlesh)
            {
                activeBossLootPosition = deathPosition;
            }

            try
            {
                bossPenaltyNpcCheckDeadMethod.Invoke(boss, null);
            }
            catch
            {
                bossPenaltyNpcActiveField.SetValue(boss, false);
                throw;
            }
            finally
            {
                activeBossLootPosition = previousLootPosition;
            }
        }

        private static void RaceBossCheckDeadPostfix(object __instance)
        {
            if (__instance == null)
            {
                return;
            }

            lock (BossPenaltySync)
            {
                for (int batchIndex = pendingBossSettlements.Count - 1; batchIndex >= 0; batchIndex--)
                {
                    PendingBossPenaltySettlement batch = pendingBossSettlements[batchIndex];
                    if (batch.SettlementId > 0L)
                    {
                        continue;
                    }

                    PendingBossEncounter encounter = batch.Encounter;
                    for (int i = encounter.Members.Count - 1; i >= 0; i--)
                    {
                        if (ReferenceEquals(__instance, encounter.Members[i]))
                        {
                            encounter.Members.RemoveAt(i);
                        }
                    }

                    if (encounter.Members.Count == 0)
                    {
                        pendingBossSettlements.RemoveAt(batchIndex);
                    }
                }
            }
        }

        private static void RacePlayerSpawnPostfix(object __instance, object __0)
        {
            try
            {
                if (__instance == null ||
                    __0 == null ||
                    Convert.ToInt32(__0, CultureInfo.InvariantCulture) != 0 ||
                    !ReferenceEquals(__instance, bossPenaltyLocalPlayerProperty.GetValue(null, null)))
                {
                    return;
                }

                PendingBossDeathContext deathContext;
                lock (BossPenaltySync)
                {
                    deathContext = pendingBossDeathContext;
                    pendingBossDeathContext = null;
                }

                WorldLockConfiguration current = configuration;
                if (deathContext == null ||
                    !deathContext.PenaltyTriggered ||
                    deathContext.DeathPosition == null ||
                    current == null ||
                    !current.EntryAllowed ||
                    !current.BossFailurePenaltyEnabled ||
                    !ReferenceEquals(current, deathContext.Configuration) ||
                    (int)bossPenaltyMainNetModeField.GetValue(null) != 0)
                {
                    return;
                }

                object deathPosition = deathContext.DeathPosition;
                object vanillaPosition = bossPenaltyPlayerPositionField.GetValue(__instance);
                int width = (int)bossPenaltyPlayerWidthField.GetValue(__instance);
                int height = (int)bossPenaltyPlayerHeightField.GetValue(__instance);
                object deathTopLeft = CreateVector(
                    GetVectorX(deathPosition) - width / 2f,
                    GetVectorY(deathPosition) - height / 2f);
                bossPenaltyPlayerPositionField.SetValue(__instance, deathTopLeft);

                object[] arguments = { __instance, null };
                bool foundSafePosition = (bool)bossPenaltyFindClosestTeleportSpotMethod.Invoke(null, arguments);
                object destination = foundSafePosition && arguments[1] != null
                    ? arguments[1]
                    : vanillaPosition;
                object zero = CreateVector(0f, 0f);
                bossPenaltyPlayerPositionField.SetValue(__instance, destination);
                bossPenaltyPlayerVelocityField.SetValue(__instance, zero);
                bossPenaltyPlayerOldPositionField.SetValue(__instance, destination);
                bossPenaltyPlayerNetOffsetField.SetValue(__instance, zero);
                int fallStart = (int)(GetVectorY(destination) / 16f);
                bossPenaltyPlayerFallStartField.SetValue(__instance, fallStart);
                bossPenaltyPlayerFallStart2Field.SetValue(__instance, fallStart);
            }
            catch
            {
            }
        }

        private static void RaceBossDeathInterfacePostfix()
        {
            try
            {
                PendingBossDeathRow[] rows = GetBossDeathRows();
                if (rows.Length == 0)
                {
                    return;
                }

                int screenWidth = (int)bossPenaltyMainScreenWidthField.GetValue(null);
                int screenHeight = (int)bossPenaltyMainScreenHeightField.GetValue(null);
                int mouseX = (int)bossPenaltyMainMouseXField.GetValue(null);
                int mouseY = (int)bossPenaltyMainMouseYField.GetValue(null);
                bool click =
                    (bool)bossPenaltyMainMouseLeftField.GetValue(null) &&
                    (bool)bossPenaltyMainMouseLeftReleaseField.GetValue(null);
                bool consumedClick = false;
                float centerX = screenWidth / 2f;
                float firstRowY = screenHeight / 2f + 100f;
                const float rowSpacing = 44f;
                const float textScale = 0.7f;
                object deathTextAsset = bossPenaltyDeathTextFontField.GetValue(null);
                object deathTextFont = bossPenaltyDeathTextFontValueProperty.GetValue(
                    deathTextAsset,
                    null);
                object localPlayer = bossPenaltyLocalPlayerProperty.GetValue(null, null);
                object transparent = bossPenaltyTransparentColorProperty.GetValue(null, null);
                object color = bossPenaltyPlayerGetDeathAlphaMethod.Invoke(
                    localPlayer,
                    new[] { transparent });
                object zero = CreateVector(0f, 0f);
                object spriteEffects = Activator.CreateInstance(bossPenaltySpriteEffectsType);

                for (int i = 0; i < rows.Length; i++)
                {
                    PendingBossDeathRow row = rows[i];
                    string text = FormatBossDeathRow(row);
                    object textSize = bossPenaltyDeathTextMeasureStringMethod.Invoke(
                        deathTextFont,
                        new object[] { text });
                    float renderedWidth = GetVectorX(textSize) * textScale;
                    float renderedHeight = GetVectorY(textSize) * textScale;
                    float y = firstRowY + i * rowSpacing;
                    bool hovering =
                        mouseX >= centerX - renderedWidth / 2f - 12f &&
                        mouseX <= centerX + renderedWidth / 2f + 12f &&
                        mouseY >= y - 6f &&
                        mouseY <= y + renderedHeight + 6f;
                    bossPenaltyDeathTextDrawStringMethod.Invoke(
                        null,
                        new[]
                        {
                            bossPenaltyMainSpriteBatchField.GetValue(null),
                            deathTextFont,
                            text,
                            CreateVector(centerX - renderedWidth / 2f, y),
                            color,
                            (object)0f,
                            zero,
                            (object)textScale,
                            spriteEffects,
                            (object)0f,
                            null,
                            null
                        });

                    if (click && hovering && row.CanToggle)
                    {
                        ToggleBossDeathPenalty(row.Kind);
                        consumedClick = true;
                    }
                }

                if (consumedClick)
                {
                    bossPenaltyMainMouseLeftReleaseField.SetValue(null, false);
                }
            }
            catch
            {
            }
        }

        private static PendingBossDeathRow[] GetBossDeathRows()
        {
            lock (BossPenaltySync)
            {
                PendingBossDeathContext deathContext = pendingBossDeathContext;
                WorldLockConfiguration current = configuration;
                object player = bossPenaltyLocalPlayerProperty.GetValue(null, null);
                if (deathContext == null ||
                    current == null ||
                    !ReferenceEquals(current, deathContext.Configuration) ||
                    player == null ||
                    !(bool)bossPenaltyPlayerDeadField.GetValue(player))
                {
                    return new PendingBossDeathRow[0];
                }

                var rows = new List<PendingBossDeathRow>();
                for (int i = 0; i < deathContext.Encounters.Count; i++)
                {
                    PendingBossEncounter encounter = deathContext.Encounters[i];
                    rows.Add(new PendingBossDeathRow(
                        encounter.Kind,
                        encounter.PenaltyEnabled,
                        !encounter.SettlementStarted && HasActiveBossMember(encounter),
                        encounter.SettlementStarted,
                        encounter.PenaltyMilliseconds));
                }

                return rows.ToArray();
            }
        }

        private static void ToggleBossDeathPenalty(RaceBossPenaltyKind kind)
        {
            lock (BossPenaltySync)
            {
                PendingBossDeathContext deathContext = pendingBossDeathContext;
                PendingBossEncounter encounter = deathContext == null
                    ? null
                    : FindEncounter(deathContext.Encounters, kind);
                if (encounter == null ||
                    encounter.SettlementStarted ||
                    !HasActiveBossMember(encounter))
                {
                    return;
                }

                encounter.PenaltyEnabled = !encounter.PenaltyEnabled;
            }
        }

        private static string FormatBossDeathRow(PendingBossDeathRow row)
        {
            string name = GetBossDisplayName(row.Kind);
            if (row.SettlementStarted)
            {
                return name + "：罚时时间 " + FormatBossPenaltyDuration(row.PenaltyMilliseconds);
            }

            if (!row.CanToggle)
            {
                return name + "：未接受罚时";
            }

            return row.Enabled
                ? "将会于" + name + "脱战后自动罚时，点击以取消"
                : "已关闭" + name + "脱战后自动罚时，点击以启用";
        }

        private static string FormatBossPenaltyDuration(long milliseconds)
        {
            long totalSeconds = Math.Max(0L, milliseconds + 999L) / 1000L;
            return (totalSeconds / 60L).ToString(CultureInfo.InvariantCulture) +
                "分" +
                (totalSeconds % 60L).ToString(CultureInfo.InvariantCulture) +
                "秒";
        }

        private static string GetBossDisplayName(RaceBossPenaltyKind kind)
        {
            switch (kind)
            {
                case RaceBossPenaltyKind.Skeletron:
                    return "骷髅王";
                case RaceBossPenaltyKind.WallOfFlesh:
                    return "血肉墙";
                case RaceBossPenaltyKind.SkeletronPrime:
                    return "机械骷髅王";
                case RaceBossPenaltyKind.Twins:
                    return "双子魔眼";
                case RaceBossPenaltyKind.Destroyer:
                    return "毁灭者";
                case RaceBossPenaltyKind.Plantera:
                    return "世纪之花";
                case RaceBossPenaltyKind.Golem:
                    return "石巨人";
                case RaceBossPenaltyKind.LunaticCultist:
                    return "拜月教邪教徒";
                default:
                    return "Boss";
            }
        }

        private static void RaceBossLootPositionPrefix(
            ref int __1,
            ref int __2,
            ref int __3,
            ref int __4)
        {
            object lootPosition = activeBossLootPosition;
            if (lootPosition == null)
            {
                return;
            }

            __1 = (int)Math.Round(GetVectorX(lootPosition), MidpointRounding.AwayFromZero);
            __2 = (int)Math.Round(GetVectorY(lootPosition), MidpointRounding.AwayFromZero);
            __3 = 0;
            __4 = 0;
        }

        private static RaceBossPenaltyKind GetPenaltyKind(int npcType)
        {
            switch (npcType)
            {
                case SkeletronHeadNpcType:
                    return RaceBossPenaltyKind.Skeletron;
                case WallOfFleshNpcType:
                    return RaceBossPenaltyKind.WallOfFlesh;
                case SkeletronPrimeNpcType:
                    return RaceBossPenaltyKind.SkeletronPrime;
                case RetinazerNpcType:
                case SpazmatismNpcType:
                    return RaceBossPenaltyKind.Twins;
                case DestroyerHeadNpcType:
                    return RaceBossPenaltyKind.Destroyer;
                case PlanteraNpcType:
                    return RaceBossPenaltyKind.Plantera;
                case GolemBodyNpcType:
                    return RaceBossPenaltyKind.Golem;
                case LunaticCultistNpcType:
                    return RaceBossPenaltyKind.LunaticCultist;
                default:
                    return 0;
            }
        }

        private static float GetCenterManhattanDistance(object npc, object player)
        {
            object npcPosition = bossPenaltyNpcPositionField.GetValue(npc);
            object playerPosition = bossPenaltyPlayerPositionField.GetValue(player);
            float npcCenterX = GetVectorX(npcPosition) + (int)bossPenaltyNpcWidthField.GetValue(npc) / 2f;
            float npcCenterY = GetVectorY(npcPosition) + (int)bossPenaltyNpcHeightField.GetValue(npc) / 2f;
            float playerCenterX = GetVectorX(playerPosition) + (int)bossPenaltyPlayerWidthField.GetValue(player) / 2f;
            float playerCenterY = GetVectorY(playerPosition) + (int)bossPenaltyPlayerHeightField.GetValue(player) / 2f;
            return Math.Abs(npcCenterX - playerCenterX) + Math.Abs(npcCenterY - playerCenterY);
        }

        private static object CreateVector(float x, float y)
        {
            object vector = Activator.CreateInstance(bossPenaltyNpcPositionField.FieldType);
            bossPenaltyVectorXField.SetValue(vector, x);
            bossPenaltyVectorYField.SetValue(vector, y);
            return vector;
        }

        private static float GetVectorX(object vector)
        {
            return vector == null ? 0f : (float)bossPenaltyVectorXField.GetValue(vector);
        }

        private static float GetVectorY(object vector)
        {
            return vector == null ? 0f : (float)bossPenaltyVectorYField.GetValue(vector);
        }

        private static void ClearPendingBossPenaltyLocked()
        {
            pendingBossSettlements.Clear();
        }

        private static void ResetRaceBossPenalty()
        {
            lock (BossPenaltySync)
            {
                ClearPendingBossPenaltyLocked();
                pendingBossDeathContext = null;
            }
        }

        private sealed class PendingBossPenaltySettlement
        {
            public PendingBossPenaltySettlement(
                WorldLockConfiguration configuration,
                PendingBossEncounter encounter,
                object deathPosition,
                PendingBossDeathContext deathContext)
            {
                Configuration = configuration;
                Encounter = encounter;
                DeathPosition = deathPosition;
                DeathContext = deathContext;
            }

            public WorldLockConfiguration Configuration;
            public PendingBossEncounter Encounter;
            public object DeathPosition;
            public PendingBossDeathContext DeathContext;
            public RaceBossPenaltyKind SettlementKind;
            public long SettlementId;
        }

        private sealed class PendingBossDeathContext
        {
            public PendingBossDeathContext(
                WorldLockConfiguration configuration,
                List<PendingBossEncounter> encounters,
                object deathPosition)
            {
                Configuration = configuration;
                Encounters = encounters;
                DeathPosition = deathPosition;
            }

            public WorldLockConfiguration Configuration;
            public List<PendingBossEncounter> Encounters;
            public object DeathPosition;
            public bool PenaltyTriggered;
        }

        private sealed class PendingBossEncounter
        {
            public PendingBossEncounter(RaceBossPenaltyKind kind)
            {
                Kind = kind;
                Members = new List<object>();
            }

            public RaceBossPenaltyKind Kind;
            public List<object> Members;
            public int MaximumLife;
            public bool PenaltyEnabled = true;
            public bool SettlementStarted;
            public long PenaltyMilliseconds;
        }

        private sealed class PendingBossDeathRow
        {
            public PendingBossDeathRow(
                RaceBossPenaltyKind kind,
                bool enabled,
                bool canToggle,
                bool settlementStarted,
                long penaltyMilliseconds)
            {
                Kind = kind;
                Enabled = enabled;
                CanToggle = canToggle;
                SettlementStarted = settlementStarted;
                PenaltyMilliseconds = penaltyMilliseconds;
            }

            public RaceBossPenaltyKind Kind;
            public bool Enabled;
            public bool CanToggle;
            public bool SettlementStarted;
            public long PenaltyMilliseconds;
        }
    }
}
