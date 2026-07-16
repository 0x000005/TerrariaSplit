using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace TerrariaSplit.WorldGuard.Payload
{
    public static partial class EntryPoint
    {
        private const int StardustTownAndNaturalEventsCapability = 32;
        private const int TownNpcCheckPeriod = 1800;

        private static FieldInfo checkForSpawnsField;
        private static FieldInfo mainNetModeField;
        private static PropertyInfo gameUpdateCountProperty;
        private static FieldInfo stardustZoneField;
        private static FieldInfo spawnEyeField;
        private static FieldInfo spawnHardBossField;
        private static MethodInfo startRainMethod;
        private static MethodInfo startSlimeRainMethod;
        private static MethodInfo spawnOnPlayerMethod;

        private static int EnsureStardustTownAndNaturalEventsPatchesInstalled()
        {
            lock (PatchSync)
            {
                if (stardustTownAndNaturalEventsPatchInstalled)
                {
                    return 0;
                }

                Assembly terraria;
                Type worldGenType;
                Type playerType;
                Type itemType;
                Type tileType;
                if (!TryGetAdvancedTerrariaTypes(out terraria, out worldGenType, out playerType, out itemType, out tileType))
                {
                    return 60;
                }

                Type mainType = terraria.GetType("Terraria.Main", false);
                Type npcType = terraria.GetType("Terraria.NPC", false);
                Type spawnerType = npcType == null ? null : npcType.GetNestedType("Spawner", BindingFlags.Public | BindingFlags.NonPublic);
                Type birthdayPartyType = terraria.GetType("Terraria.GameContent.Events.BirthdayParty", false);
                if (mainType == null || npcType == null || spawnerType == null || birthdayPartyType == null)
                {
                    return 61;
                }

                MethodInfo updateTime = mainType.GetMethod("UpdateTime", BindingFlags.Static | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                MethodInfo startNight = mainType.GetMethod("UpdateTime_StartNight", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(bool).MakeByRefType() }, null);
                MethodInfo startDay = mainType.GetMethod("UpdateTime_StartDay", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(bool).MakeByRefType() }, null);
                MethodInfo townCheck = mainType.GetMethod("UpdateTime_SpawnTownNPCs", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null);
                MethodInfo playWorldCallback = worldGenType.GetMethod("playWorldCallBack", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(object) }, null);
                MethodInfo spawnAnNpc = spawnerType.GetMethod("SpawnAnNPC", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(int) }, null);
                MethodInfo stardustTower = npcType.GetMethod("SpawnStardustMark_StardustTower", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                MethodInfo stardustWorm = npcType.GetMethod("SpawnStardustMark_StardustWorm", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                MethodInfo naturalParty = birthdayPartyType.GetMethod("NaturalAttempt", BindingFlags.Static | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

                MethodInfo[] auditedMethods =
                {
                    updateTime,
                    startNight,
                    startDay,
                    townCheck,
                    playWorldCallback,
                    spawnAnNpc,
                    stardustTower,
                    stardustWorm,
                    naturalParty
                };
                string[] auditedHashes =
                {
                    "C8DA2AAA41D3CBAEDCACE039534462FD71D71ED8A8D05F1DB771A93F2AD5F85B",
                    "7DDBCC6CE6FFCF8873CFE049DFE36BFAC654DEBDBB3A1139CE5A53441A5B9F34",
                    "F26C6C5C80950CAD1BC2E594E4DBF61A1B0AE6624B2EB434315DB9268861DF93",
                    "D43DB2A7D85D511C599C78065A2C2FABD1503C47CC78EA7E4D343E7B45C7A68A",
                    "0EC95917ADC2980DE9EA8999035E768DFA10C6FBB25F0F8021F31558C7ACBE7F",
                    "30364C2288CEA0342E3EF7110D72CAEA002CE4DDA901BC085729634AE7C3A4C6",
                    "DAE38E5AE32469E350FD5F2867A09605DBE135443C428A2437624B11C64CBA76",
                    "EA62F42ABE466B57D36576BC4076582BDC500AF45FBE926DE1949181B5862644",
                    "08B88B59637A5EC6C38B107171CF44C3D85C85D5AD73ED8B394CE87C1A72AA07"
                };
                if (!MethodsMatch(auditedMethods, auditedHashes))
                {
                    return 62;
                }

                checkForSpawnsField = mainType.GetField("checkForSpawns", BindingFlags.Static | BindingFlags.Public);
                mainNetModeField = mainType.GetField("netMode", BindingFlags.Static | BindingFlags.Public);
                gameUpdateCountProperty = mainType.GetProperty("GameUpdateCount", BindingFlags.Static | BindingFlags.Public);
                stardustZoneField = spawnerType.GetField("ZoneTowerStardust", BindingFlags.Instance | BindingFlags.Public);
                spawnEyeField = worldGenType.GetField("spawnEye", BindingFlags.Static | BindingFlags.Public);
                spawnHardBossField = worldGenType.GetField("spawnHardBoss", BindingFlags.Static | BindingFlags.Public);
                startRainMethod = mainType.GetMethod("StartRain", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(bool), typeof(float?), typeof(bool) }, null);
                startSlimeRainMethod = mainType.GetMethod("StartSlimeRain", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(bool) }, null);
                spawnOnPlayerMethod = npcType.GetMethod("SpawnOnPlayer", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(int), typeof(int), typeof(float), typeof(float), typeof(float), typeof(float) }, null);
                if (checkForSpawnsField == null || mainNetModeField == null || gameUpdateCountProperty == null ||
                    stardustZoneField == null || spawnEyeField == null || spawnHardBossField == null ||
                    startRainMethod == null || startSlimeRainMethod == null || spawnOnPlayerMethod == null)
                {
                    return 63;
                }

                MethodInfo stardustZonePrefix = GetPrivateMethod("StardustZonePrefix");
                MethodInfo stardustDerivedPrefix = GetPrivateMethod("StardustDerivedPrefix");
                MethodInfo scopeFinalizer = GetPrivateMethod("AdvancedScopeFinalizer");
                MethodInfo townPrefix = GetPrivateMethod("TownNpcCheckPrefix");
                MethodInfo boundaryPrefix = GetPrivateMethod("NaturalEventBoundaryPrefix");
                MethodInfo boundaryFinalizer = GetPrivateMethod("NaturalEventBoundaryFinalizer");
                MethodInfo worldEnteredPostfix = GetPrivateMethod("RaceWorldEnteredPostfix");
                MethodInfo naturalPartyPrefix = GetPrivateMethod("NaturalPartyPrefix");
                MethodInfo updateTimeTranspiler = GetPrivateMethod("NaturalEventTranspiler");
                if (stardustZonePrefix == null || stardustDerivedPrefix == null || scopeFinalizer == null ||
                    townPrefix == null || boundaryPrefix == null || boundaryFinalizer == null ||
                    worldEnteredPostfix == null || naturalPartyPrefix == null || updateTimeTranspiler == null)
                {
                    return 64;
                }

                var harmony = new Harmony(HarmonyId);
                MethodInfo[] scopedMethods = { spawnAnNpc, stardustTower, stardustWorm, townCheck };
                MethodInfo[] boundaryMethods = { startNight, startDay };
                int installResult = InstallPatchSet(
                    harmony,
                    auditedMethods,
                    () =>
                    {
                        harmony.Patch(spawnAnNpc, prefix: new HarmonyMethod(stardustZonePrefix), finalizer: new HarmonyMethod(scopeFinalizer));
                        harmony.Patch(stardustTower, prefix: new HarmonyMethod(stardustDerivedPrefix), finalizer: new HarmonyMethod(scopeFinalizer));
                        harmony.Patch(stardustWorm, prefix: new HarmonyMethod(stardustDerivedPrefix), finalizer: new HarmonyMethod(scopeFinalizer));
                        harmony.Patch(townCheck, prefix: new HarmonyMethod(townPrefix), finalizer: new HarmonyMethod(scopeFinalizer));
                        harmony.Patch(startNight, prefix: new HarmonyMethod(boundaryPrefix), finalizer: new HarmonyMethod(boundaryFinalizer));
                        harmony.Patch(startDay, prefix: new HarmonyMethod(boundaryPrefix), finalizer: new HarmonyMethod(boundaryFinalizer));
                        harmony.Patch(playWorldCallback, postfix: new HarmonyMethod(worldEnteredPostfix));
                        harmony.Patch(naturalParty, prefix: new HarmonyMethod(naturalPartyPrefix));
                        harmony.Patch(updateTime, transpiler: new HarmonyMethod(updateTimeTranspiler));
                    },
                    () => scopedMethods.All(method => HasOwnedPrefix(method) && HasOwnedFinalizer(method)) &&
                        boundaryMethods.All(method => HasOwnedPrefix(method) && HasOwnedFinalizer(method)) &&
                        HasOwnedPostfix(playWorldCallback) && HasOwnedPrefix(naturalParty) && HasOwnedTranspiler(updateTime),
                    65);
                if (installResult != 0)
                {
                    return installResult;
                }

                stardustTownAndNaturalEventsPatchInstalled = true;
                return 0;
            }
        }

        private static bool StardustZonePrefix(object __instance, ref AdvancedScopeState __state)
        {
            WorldLockConfiguration current = configuration;
            if (current == null || !current.HasCapability(StardustTownAndNaturalEventsCapability) ||
                !(bool)stardustZoneField.GetValue(__instance))
            {
                return true;
            }

            try
            {
                string source = WorldKey(current);
                long occurrence = current.State.EventCounters.Next("stardust-zone-choice", source);
                return TryBeginAdvancedScope(current, "stardust-zone-choice", source + "|" + occurrence, ref __state);
            }
            catch (Exception ex)
            {
                return FailScope("stardust-zone-choice", ex);
            }
        }

        private static bool StardustDerivedPrefix(MethodBase __originalMethod, ref AdvancedScopeState __state)
        {
            WorldLockConfiguration current = configuration;
            if (current == null || !current.HasCapability(StardustTownAndNaturalEventsCapability))
            {
                return true;
            }

            try
            {
                string domain = "stardust-derived-" + __originalMethod.Name.ToLowerInvariant();
                string source = WorldKey(current);
                long occurrence = current.State.EventCounters.Next(domain, source);
                return TryBeginAdvancedScope(current, domain, source + "|" + occurrence, ref __state);
            }
            catch (Exception ex)
            {
                return FailScope(__originalMethod.Name, ex);
            }
        }

        private static bool TownNpcCheckPrefix(ref bool forceUpdate, ref AdvancedScopeState __state)
        {
            WorldLockConfiguration current = configuration;
            if (current == null || !current.HasCapability(StardustTownAndNaturalEventsCapability) ||
                (int)mainNetModeField.GetValue(null) == 1)
            {
                return true;
            }

            try
            {
                uint updateTick = (uint)gameUpdateCountProperty.GetValue(null, null);
                int elapsed;
                if (!current.State.AdvanceTownNpcCheck(
                        updateTick,
                        (int)checkForSpawnsField.GetValue(null),
                        TownNpcCheckPeriod,
                        out elapsed))
                {
                    checkForSpawnsField.SetValue(null, elapsed);
                    return false;
                }

                checkForSpawnsField.SetValue(null, 0);
                forceUpdate = true;
                string source = WorldKey(current);
                long occurrence = current.State.EventCounters.Next("town-npc-check", source);
                return TryBeginAdvancedScope(current, "town-npc-check", source + "|" + occurrence, ref __state);
            }
            catch (Exception ex)
            {
                return FailScope("town-npc-check", ex);
            }
        }

        private static void NaturalEventBoundaryPrefix(ref bool stopEvents, ref TownBoundaryState __state)
        {
            WorldLockConfiguration current = configuration;
            if (current == null || !current.HasCapability(StardustTownAndNaturalEventsCapability))
            {
                return;
            }

            __state = new TownBoundaryState(
                (int)checkForSpawnsField.GetValue(null),
                activeScopeConfiguration);
            activeScopeConfiguration = current;
            stopEvents = true;
        }

        private static Exception NaturalEventBoundaryFinalizer(Exception __exception, TownBoundaryState __state)
        {
            if (__state != null)
            {
                try
                {
                    checkForSpawnsField.SetValue(null, __state.Counter);
                }
                catch (Exception ex)
                {
                    runtimeFailure = "The town NPC timer restore failed: " + ex.GetType().Name;
                }
                finally
                {
                    activeScopeConfiguration = __state.PreviousConfiguration;
                }
            }

            return __exception;
        }

        private static void RaceWorldEnteredPostfix()
        {
            WorldLockConfiguration current = activeScopeConfiguration ?? configuration;
            if (current == null || !current.HasCapability(StardustTownAndNaturalEventsCapability))
            {
                return;
            }

            checkForSpawnsField.SetValue(null, 0);
            spawnEyeField.SetValue(null, false);
            spawnHardBossField.SetValue(null, 0);
            current.State.ResetTownNpcCheck();
        }

        private static bool NaturalPartyPrefix()
        {
            WorldLockConfiguration current = activeScopeConfiguration ?? configuration;
            return current == null || !current.HasCapability(StardustTownAndNaturalEventsCapability);
        }

        private static IEnumerable<CodeInstruction> NaturalEventTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();
            MethodInfo rainWrapper = GetPrivateMethod("NaturalStartRain");
            MethodInfo slimeWrapper = GetPrivateMethod("NaturalStartSlimeRain");
            MethodInfo spawnWrapper = GetPrivateMethod("NaturalSpawnOnPlayer");
            int rainCalls = 0;
            int slimeCalls = 0;
            int spawnCalls = 0;

            for (int index = 0; index < codes.Count; index++)
            {
                MethodInfo called = codes[index].operand as MethodInfo;
                MethodInfo replacement = null;
                if (called == startRainMethod)
                {
                    replacement = rainWrapper;
                    rainCalls++;
                }
                else if (called == startSlimeRainMethod)
                {
                    replacement = slimeWrapper;
                    slimeCalls++;
                }
                else if (called == spawnOnPlayerMethod)
                {
                    replacement = spawnWrapper;
                    spawnCalls++;
                }

                if (replacement != null)
                {
                    var instruction = new CodeInstruction(OpCodes.Call, replacement);
                    instruction.labels.AddRange(codes[index].labels);
                    instruction.blocks.AddRange(codes[index].blocks);
                    codes[index] = instruction;
                }
            }

            if (rainCalls != 3 || slimeCalls != 1 || spawnCalls != 7)
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    "Natural event call sites changed: rain={0}, slime={1}, boss={2}.",
                    rainCalls,
                    slimeCalls,
                    spawnCalls));
            }

            return codes;
        }

        private static void NaturalStartRain(bool instant, float? strengthOverride, bool guaranteeCoinRain)
        {
            WorldLockConfiguration current = activeScopeConfiguration ?? configuration;
            if (current != null && current.HasCapability(StardustTownAndNaturalEventsCapability))
            {
                return;
            }

            startRainMethod.Invoke(null, new object[] { instant, strengthOverride, guaranteeCoinRain });
        }

        private static void NaturalStartSlimeRain(bool announce)
        {
            WorldLockConfiguration current = activeScopeConfiguration ?? configuration;
            if (current != null && current.HasCapability(StardustTownAndNaturalEventsCapability))
            {
                return;
            }

            startSlimeRainMethod.Invoke(null, new object[] { announce });
        }

        private static void NaturalSpawnOnPlayer(int player, int npcType, float ai0, float ai1, float ai2, float ai3)
        {
            WorldLockConfiguration current = configuration;
            if (npcType == 668 && current != null && current.HasCapability(StardustTownAndNaturalEventsCapability))
            {
                return;
            }

            spawnOnPlayerMethod.Invoke(null, new object[] { player, npcType, ai0, ai1, ai2, ai3 });
        }

        private sealed class TownBoundaryState
        {
            public TownBoundaryState(int counter, WorldLockConfiguration previousConfiguration)
            {
                Counter = counter;
                PreviousConfiguration = previousConfiguration;
            }

            public int Counter { get; private set; }
            public WorldLockConfiguration PreviousConfiguration { get; private set; }
        }
    }
}
