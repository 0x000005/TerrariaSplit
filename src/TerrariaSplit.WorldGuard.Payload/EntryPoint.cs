using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using HarmonyLib;
using TerrariaSplit.Race.Determinism;

namespace TerrariaSplit.WorldGuard.Payload
{
    public static partial class EntryPoint
    {
        private static readonly Guid SupportedMvid = new Guid("1d67a5fe-40eb-4168-b76e-f327a912128e");
        private const string SupportedWorldRejectionHash = "C40DE3E7739559AA2554B63D2A03764EAAD21CD5C893465EB8A2E4C2DF58D342";
        private const string SupportedSelectPlayerHash = "A68F7018C74D66AF03DF4F164BAA154F6D0D10AF13F674EA2CD331FDD5DFD40D";
        private const string SupportedCharacterListCompareHash = "86F89C1D4DB9DA06EC7160CF3C0A199874C6BD87298219793E892FEF6B9939F3";
        private const string SupportedWorldListCompareHash = "A9FC5EEE08819461D0D3A08F695F2A499D7C2D8C33821D7087B3A296AD775989";
        private const string SupportedCharacterListDrawHash = "D9E0C9967542359F4D0806B959AA9619318A2FD30779A26AE30E49F74FFA5DCC";
        private const string SupportedWorldListDrawHash = "F0A5F533B6E02755DAFBECEAD6A1E4A6C94A5945720D8CC48D2F0089F1DC4A9E";
        private const string HarmonyId = "TerrariaSplit.WorldGuard";

        private static readonly object PatchSync = new object();
        private static volatile WorldLockConfiguration configuration;
        private static PayloadCommandServer commandServer;
        private static int hostProcessId;
        private static volatile string runtimeFailure;
        private static bool patchesInstalled;
        private static bool lootPatchInstalled;
        private static bool playerTriggeredPatchInstalled;
        private static bool alchemyAndLuckPatchInstalled;
        private static bool worldTransitionPatchInstalled;
        private static bool stardustTownAndNaturalEventsPatchInstalled;
        private static int raceStartGeneration;
        private static int raceCountdownActive;
        private static FieldInfo worldListItemDataField;
        private static FieldInfo characterListItemDataField;
        private static FieldInfo panelBorderColorField;
        private static object assignedBorderColor;
        private static PropertyInfo worldPathProperty;
        private static FieldInfo worldIdField;
        private static FieldInfo worldUniqueIdField;
        private static FieldInfo statusTextField;
        private static FieldInfo menuModeField;
        private static FieldInfo menuMultiplayerField;
        private static FieldInfo menuServerField;
        private static PropertyInfo playerPathProperty;
        private static FieldInfo activePlayerFileDataField;
        private static PropertyInfo uiElementParentProperty;
        private static FieldInfo uiListItemsField;
        private static MethodInfo uiElementGetSnapPointsMethod;
        private static PropertyInfo snapPointIdProperty;
        private static MethodInfo snapPointIdSetter;
        private static FieldInfo mainRandomField;
        private static FieldInfo npcTypeField;
        private static FieldInfo npcBossField;
        private static FieldInfo npcNetIdField;
        private static FieldInfo itemDropResolverDatabaseField;
        private static FieldInfo dropAttemptNpcField;
        private static FieldInfo dropAttemptRngField;
        private static MethodInfo getRulesForNpcMethod;
        private static MethodInfo unifiedRandomSetSeedMethod;

        [ThreadStatic]
        private static int npcDropRuleDepth;

        public static int Initialize(string command)
        {
            try
            {
                string[] parts = (command ?? string.Empty).Split(new[] { '\n' }, StringSplitOptions.None);
                int nextHostProcessId;
                if (parts.Length != 3 ||
                    !string.Equals(parts[0], "start", StringComparison.Ordinal) ||
                    !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out nextHostProcessId) ||
                    nextHostProcessId <= 0)
                {
                    return 2;
                }

                string pipeName;
                try
                {
                    pipeName = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
                }
                catch (FormatException)
                {
                    return 2;
                }

                if (string.IsNullOrWhiteSpace(pipeName) || pipeName.Length > 200)
                {
                    return 2;
                }

                int compatibility = EnsurePatchesInstalled();
                if (compatibility != 0)
                {
                    return compatibility;
                }

                lock (PatchSync)
                {
                    if (commandServer != null)
                    {
                        if (string.Equals(commandServer.PipeName, pipeName, StringComparison.Ordinal))
                        {
                            hostProcessId = nextHostProcessId;
                            return 0;
                        }

                        if (IsProcessAlive(hostProcessId))
                        {
                            return 19;
                        }

                        commandServer.Stop();
                        commandServer = null;
                    }

                    commandServer = new PayloadCommandServer(pipeName, HandleCommand);
                    hostProcessId = nextHostProcessId;
                    commandServer.Start();
                }

                return 0;
            }
            catch
            {
                return 99;
            }
        }

        private static bool IsProcessAlive(int processId)
        {
            if (processId <= 0)
            {
                return false;
            }

            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    return !process.HasExited;
                }
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static PayloadCommandResult HandleCommand(string command)
        {
            PayloadCommandResult bossPenaltyResult;
            if (TryHandleRaceBossPenaltyCommand(command, out bossPenaltyResult))
            {
                return bossPenaltyResult;
            }

            PayloadCommandResult raceUiResult;
            if (TryHandleRaceUiCommand(command, out raceUiResult))
            {
                return raceUiResult;
            }

            PayloadCommandResult playerResult;
            if (TryHandleCreatePlayer(command, out playerResult))
            {
                return playerResult;
            }

            if (string.Equals(command, "prepare-restart", StringComparison.Ordinal))
            {
                return PrepareRestart();
            }

            if (string.Equals(command, "return-menu", StringComparison.Ordinal))
            {
                return PrepareRestart();
            }

            if (command != null && command.StartsWith("start-race\n", StringComparison.Ordinal))
            {
                return StartRaceAndEnterWorld(command);
            }

            if (string.Equals(command, "status", StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(runtimeFailure))
                {
                    return new PayloadCommandResult(4, runtimeFailure, false);
                }

                WorldLockConfiguration current = configuration;
                return current == null
                    ? new PayloadCommandResult(3, "The Race hook has no active package.", false)
                    : new PayloadCommandResult(0, current.PackageDigest, false);
            }

            if (string.Equals(command, "version", StringComparison.Ordinal))
            {
                return new PayloadCommandResult(
                    0,
                    Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                    false);
            }

            if (string.Equals(command, "hook-status", StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(runtimeFailure))
                {
                    return new PayloadCommandResult(4, runtimeFailure, false);
                }

                return new PayloadCommandResult(
                    0,
                    Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                    false);
            }

            if (string.Equals(command, "unlock", StringComparison.Ordinal))
            {
                PayloadCommandResult cancelled = CancelRaceStartCountdownAndRestoreMenu();
                if (cancelled.Code != 0)
                {
                    return cancelled;
                }

                configuration = null;
                ResetAdvancedDeterminismState();
                return new PayloadCommandResult(0, string.Empty, false);
            }

            if (string.Equals(command, "reset", StringComparison.Ordinal))
            {
                WorldLockConfiguration current = configuration;
                if (current == null)
                {
                    return new PayloadCommandResult(3, "The Race hook has no active package.", false);
                }

                PayloadCommandResult cancelled = CancelRaceStartCountdownAndRestoreMenu();
                if (cancelled.Code != 0)
                {
                    return cancelled;
                }

                configuration = current.CreateNextGeneration();
                ResetAdvancedDeterminismState();
                runtimeFailure = null;
                return new PayloadCommandResult(0, current.PackageDigest, false);
            }

            if (string.Equals(command, "shutdown", StringComparison.Ordinal))
            {
                PayloadCommandResult cancelled = CancelRaceStartCountdownAndRestoreMenu();
                if (cancelled.Code != 0)
                {
                    return cancelled;
                }

                configuration = null;
                ResetAdvancedDeterminismState();
                RemovePatches();
                lock (PatchSync)
                {
                    commandServer = null;
                    hostProcessId = 0;
                }

                return new PayloadCommandResult(0, string.Empty, true);
            }

            WorldLockConfiguration next;
            if (!WorldLockConfiguration.TryParse(command, out next))
            {
                return new PayloadCommandResult(2, "The Race hook configuration is invalid.", false);
            }

            if (next.HasCapability(2))
            {
                int lootCompatibility = EnsureLootPatchInstalled();
                if (lootCompatibility != 0)
                {
                    return new PayloadCommandResult(
                        lootCompatibility,
                        "The Terraria direct-drop hook is not compatible with this client.",
                        false);
                }
            }

            if (next.HasCapability(4))
            {
                int result = EnsurePlayerTriggeredPatchesInstalled();
                if (result != 0)
                {
                    return new PayloadCommandResult(result, "The Terraria player-triggered result hooks are not compatible with this client.", false);
                }
            }

            if (next.HasCapability(8))
            {
                int result = EnsureAlchemyAndLuckPatchesInstalled();
                if (result != 0)
                {
                    return new PayloadCommandResult(result, "The Terraria alchemy or luck hooks are not compatible with this client.", false);
                }
            }

            if (next.HasCapability(16))
            {
                int result = EnsureWorldTransitionPatchesInstalled();
                if (result != 0)
                {
                    return new PayloadCommandResult(result, "The Terraria world-transition hooks are not compatible with this client.", false);
                }
            }

            if (next.HasCapability(32))
            {
                int result = EnsureStardustTownAndNaturalEventsPatchesInstalled();
                if (result != 0)
                {
                    return new PayloadCommandResult(result, "The Terraria stardust, town-NPC or natural-event hooks are not compatible with this client.", false);
                }
            }

            ResetAdvancedDeterminismState();
            runtimeFailure = null;
            configuration = next;
            return new PayloadCommandResult(0, next.PackageDigest, false);
        }

        private static void RemovePatches()
        {
            lock (PatchSync)
            {
                if (!patchesInstalled)
                {
                    return;
                }

                new Harmony(HarmonyId).UnpatchAll(HarmonyId);
                patchesInstalled = false;
                lootPatchInstalled = false;
                playerTriggeredPatchInstalled = false;
                alchemyAndLuckPatchInstalled = false;
                worldTransitionPatchInstalled = false;
                stardustTownAndNaturalEventsPatchInstalled = false;
            }
        }

        private static int EnsureLootPatchInstalled()
        {
            lock (PatchSync)
            {
                if (lootPatchInstalled)
                {
                    return 0;
                }

                Assembly terraria = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(candidate => string.Equals(candidate.GetName().Name, "Terraria", StringComparison.Ordinal));
                if (terraria == null ||
                    terraria.GetName().Version != new Version(1, 4, 5, 6) ||
                    terraria.ManifestModule.ModuleVersionId != SupportedMvid)
                {
                    return 20;
                }

                Type npcType = terraria.GetType("Terraria.NPC", false);
                Type playerType = terraria.GetType("Terraria.Player", false);
                Type mainType = terraria.GetType("Terraria.Main", false);
                Type itemDropResolverType = terraria.GetType("Terraria.GameContent.ItemDropRules.ItemDropResolver", false);
                Type itemDropDatabaseType = terraria.GetType("Terraria.GameContent.ItemDropRules.ItemDropDatabase", false);
                Type itemDropRuleType = terraria.GetType("Terraria.GameContent.ItemDropRules.IItemDropRule", false);
                Type dropAttemptInfoType = terraria.GetType("Terraria.GameContent.ItemDropRules.DropAttemptInfo", false);
                if (npcType == null || playerType == null || mainType == null ||
                    itemDropResolverType == null || itemDropDatabaseType == null ||
                    itemDropRuleType == null || dropAttemptInfoType == null)
                {
                    return 21;
                }

                MethodInfo dropItemsMethod = npcType.GetMethod(
                    "NPCLoot_DropItems",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { playerType },
                    null);
                MethodInfo bossSuppliesMethod = npcType.GetMethod(
                    "DoDeathEvents_DropBossPotionsAndHearts",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);
                MethodInfo dropMoneyMethod = npcType.GetMethod(
                    "NPCLoot_DropMoney",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { playerType },
                    null);
                MethodInfo dropHealsMethod = npcType.GetMethod(
                    "NPCLoot_DropHeals",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { playerType },
                    null);
                MethodInfo resolveRuleMethod = itemDropResolverType.GetMethod(
                    "ResolveRule",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { itemDropRuleType, dropAttemptInfoType },
                    null);
                getRulesForNpcMethod = itemDropDatabaseType.GetMethod(
                    "GetRulesForNPCID",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(int), typeof(bool) },
                    null);
                mainRandomField = mainType.GetField("rand", BindingFlags.Static | BindingFlags.Public);
                npcTypeField = npcType.GetField("type", BindingFlags.Instance | BindingFlags.Public);
                npcBossField = npcType.GetField("boss", BindingFlags.Instance | BindingFlags.Public);
                npcNetIdField = npcType.GetField("netID", BindingFlags.Instance | BindingFlags.Public);
                itemDropResolverDatabaseField = itemDropResolverType.GetField("_database", BindingFlags.Instance | BindingFlags.NonPublic);
                dropAttemptNpcField = dropAttemptInfoType.GetField("npc", BindingFlags.Instance | BindingFlags.Public);
                dropAttemptRngField = dropAttemptInfoType.GetField("rng", BindingFlags.Instance | BindingFlags.Public);
                unifiedRandomSetSeedMethod = mainRandomField == null
                    ? null
                    : mainRandomField.FieldType.GetMethod(
                        "SetSeed",
                        BindingFlags.Instance | BindingFlags.Public,
                        null,
                        new[] { typeof(int) },
                        null);
                if (dropItemsMethod == null || bossSuppliesMethod == null || dropMoneyMethod == null || dropHealsMethod == null ||
                    resolveRuleMethod == null || getRulesForNpcMethod == null ||
                    mainRandomField == null || npcTypeField == null || npcBossField == null || npcNetIdField == null ||
                    itemDropResolverDatabaseField == null || dropAttemptNpcField == null || dropAttemptRngField == null ||
                    unifiedRandomSetSeedMethod == null ||
                    !string.Equals(mainRandomField.FieldType.FullName, "Terraria.Utilities.UnifiedRandom", StringComparison.Ordinal))
                {
                    return 22;
                }

                if (!HasExpectedBody(dropItemsMethod, "6EEB394A63213EBEB2178BA17F88ECBB7FB44BCD386D40E6E033F6DD59C01DE1") ||
                    !HasExpectedBody(bossSuppliesMethod, "4D5C6D44F79DB3E9C17A21EB8F2D5A065054485316AF1B55AB9302419D8F2F39") ||
                    !HasExpectedBody(dropMoneyMethod, "29D0E15157E723D3EA5FAE9E4053B2B2064A46C0D3350ECB46B3CA2E24AEEA96") ||
                    !HasExpectedBody(dropHealsMethod, "4E5EEE106A25D3ECA7DA186B600DC2ACA7DED24C30089C7558FAAF51DAB34154") ||
                    !HasExpectedBody(resolveRuleMethod, "EFD6B7E9EBF1B107B8A126ACF181708504C835BB080AFCC47A83F9D50895ECF7"))
                {
                    return 23;
                }

                MethodInfo prefix = typeof(EntryPoint).GetMethod(
                    "LootCategoryPrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo finalizer = typeof(EntryPoint).GetMethod(
                    "AdvancedScopeFinalizer",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo rulePrefix = typeof(EntryPoint).GetMethod(
                    "NpcDropRulePrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo ruleFinalizer = typeof(EntryPoint).GetMethod(
                    "NpcDropRuleFinalizer",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (prefix == null || finalizer == null || rulePrefix == null || ruleFinalizer == null)
                {
                    return 24;
                }

                var harmony = new Harmony(HarmonyId);
                MethodInfo[] categoryMethods = { dropItemsMethod, bossSuppliesMethod, dropMoneyMethod, dropHealsMethod };
                MethodInfo[] methods = categoryMethods.Concat(new[] { resolveRuleMethod }).ToArray();
                int installResult = InstallPatchSet(
                    harmony,
                    methods,
                    () =>
                    {
                        foreach (MethodInfo method in categoryMethods)
                        {
                            harmony.Patch(
                                method,
                                prefix: new HarmonyMethod(prefix),
                                finalizer: new HarmonyMethod(finalizer));
                        }
                        harmony.Patch(
                            resolveRuleMethod,
                            prefix: new HarmonyMethod(rulePrefix),
                            finalizer: new HarmonyMethod(ruleFinalizer));
                    },
                    () => categoryMethods.All(method => HasOwnedPrefix(method) && HasOwnedFinalizer(method)) &&
                        HasOwnedPrefix(resolveRuleMethod) && HasOwnedFinalizer(resolveRuleMethod),
                    25);
                if (installResult != 0)
                {
                    return installResult;
                }

                lootPatchInstalled = true;
                return 0;
            }
        }

        private static bool LootCategoryPrefix(MethodBase __originalMethod, object __instance, ref AdvancedScopeState __state)
        {
            WorldLockConfiguration current = configuration;
            if (current == null || !current.HasCapability(2))
            {
                return true;
            }

            try
            {
                int npcType = (int)npcTypeField.GetValue(__instance);
                bool isBossDrop = (bool)npcBossField.GetValue(__instance);
                string domain = GetLootCategoryDomain(__originalMethod.Name);
                string counterSource = DeterministicEventIdentity.NpcDropCounterSource(npcType, isBossDrop);
                long occurrence = current.State.EventCounters.Next(domain, counterSource);
                string eventKey = DeterministicEventIdentity.NpcDropEventKey(npcType, isBossDrop, occurrence);
                return TryBeginAdvancedScope(current, domain, eventKey, ref __state);
            }
            catch (Exception ex)
            {
                return FailScope(__originalMethod.Name, ex);
            }
        }

        private static string GetLootCategoryDomain(string methodName)
        {
            switch (methodName)
            {
                case "NPCLoot_DropItems":
                    return "npc-direct-drop";
                case "DoDeathEvents_DropBossPotionsAndHearts":
                    return "npc-boss-supplies";
                case "NPCLoot_DropMoney":
                    return "npc-money";
                case "NPCLoot_DropHeals":
                    return "npc-heals";
                default:
                    throw new InvalidOperationException("Unknown NPC loot category method: " + methodName);
            }
        }

        private static bool NpcDropRulePrefix(
            object __instance,
            object[] __args,
            ref NpcDropRuleScopeState __state)
        {
            if (npcDropRuleDepth > 0)
            {
                npcDropRuleDepth++;
                __state = new NpcDropRuleScopeState(true, false, null, 0, null);
                return true;
            }

            WorldLockConfiguration current = activeScopeConfiguration ?? configuration;
            if (current == null || !current.HasCapability(2) ||
                string.IsNullOrEmpty(activeChanceContext) ||
                !activeChanceContext.StartsWith("npc-direct-drop|", StringComparison.Ordinal))
            {
                return true;
            }

            npcDropRuleDepth = 1;
            try
            {
                if (__args == null || __args.Length != 2)
                {
                    throw new InvalidOperationException("The NPC drop rule arguments are unavailable.");
                }

                int ruleIndex = GetTopLevelNpcDropRuleIndex(__instance, __args[0], __args[1]);
                string parentContext = activeChanceContext;
                string ruleEventKey = DeterministicEventIdentity.NpcDropRuleEventKey(parentContext, ruleIndex);
                byte[] seed = DeterministicDomainSeed.Derive(
                    current.EntropySeed,
                    current.ProtocolVersion,
                    "npc-drop-rule/main",
                    ruleEventKey);
                object ruleRandom = dropAttemptRngField.GetValue(__args[1]);
                if (ruleRandom == null)
                {
                    throw new InvalidOperationException("The NPC drop rule random source is unavailable.");
                }

                string previousContext = activeChanceContext;
                int previousLuckIndex = activeLuckCallIndex;
                WorldLockConfiguration previousConfiguration = activeScopeConfiguration;
                int randomSeed = DeterministicDomainSeed.ToPositiveInt32(seed);
                unifiedRandomSetSeedMethod.Invoke(ruleRandom, new object[] { randomSeed });
                object mainRandom = mainRandomField.GetValue(null);
                if (mainRandom != null && !ReferenceEquals(mainRandom, ruleRandom))
                {
                    unifiedRandomSetSeedMethod.Invoke(mainRandom, new object[] { randomSeed });
                }

                activeChanceContext = "npc-drop-rule|" + ruleEventKey;
                activeLuckCallIndex = 0;
                activeScopeConfiguration = current;
                __state = new NpcDropRuleScopeState(
                    true,
                    true,
                    previousContext,
                    previousLuckIndex,
                    previousConfiguration);
                return true;
            }
            catch (Exception ex)
            {
                npcDropRuleDepth = 0;
                return FailScope("npc-drop-rule", ex);
            }
        }

        private static Exception NpcDropRuleFinalizer(
            Exception __exception,
            NpcDropRuleScopeState __state)
        {
            if (__state == null || !__state.Entered)
            {
                return __exception;
            }

            if (npcDropRuleDepth > 0)
            {
                npcDropRuleDepth--;
            }

            if (__state.OwnsRuleScope)
            {
                activeChanceContext = __state.PreviousChanceContext;
                activeLuckCallIndex = __state.PreviousLuckCallIndex;
                activeScopeConfiguration = __state.PreviousConfiguration;
            }
            return __exception;
        }

        private static int GetTopLevelNpcDropRuleIndex(object resolver, object rule, object dropAttemptInfo)
        {
            object database = itemDropResolverDatabaseField.GetValue(resolver);
            object npc = dropAttemptNpcField.GetValue(dropAttemptInfo);
            if (database == null || npc == null)
            {
                throw new InvalidOperationException("The NPC drop rule context is incomplete.");
            }

            int netId = (int)npcNetIdField.GetValue(npc);
            IEnumerable rules = getRulesForNpcMethod.Invoke(database, new object[] { netId, true }) as IEnumerable;
            if (rules == null)
            {
                throw new InvalidOperationException("The NPC drop rule list is unavailable.");
            }

            int index = 0;
            foreach (object candidate in rules)
            {
                if (ReferenceEquals(candidate, rule))
                {
                    return index;
                }
                index++;
            }

            throw new InvalidOperationException("The top-level NPC drop rule was not found.");
        }

        private sealed class NpcDropRuleScopeState
        {
            public NpcDropRuleScopeState(
                bool entered,
                bool ownsRuleScope,
                string previousChanceContext,
                int previousLuckCallIndex,
                WorldLockConfiguration previousConfiguration)
            {
                Entered = entered;
                OwnsRuleScope = ownsRuleScope;
                PreviousChanceContext = previousChanceContext;
                PreviousLuckCallIndex = previousLuckCallIndex;
                PreviousConfiguration = previousConfiguration;
            }

            public bool Entered { get; private set; }

            public bool OwnsRuleScope { get; private set; }

            public string PreviousChanceContext { get; private set; }

            public int PreviousLuckCallIndex { get; private set; }

            public WorldLockConfiguration PreviousConfiguration { get; private set; }
        }

        private static int EnsurePatchesInstalled()
        {
            lock (PatchSync)
            {
                if (patchesInstalled)
                {
                    return 0;
                }

                Assembly terraria = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(candidate => string.Equals(candidate.GetName().Name, "Terraria", StringComparison.Ordinal));
                if (terraria == null)
                {
                    return 10;
                }

                if (terraria.GetName().Version != new Version(1, 4, 5, 6))
                {
                    return 11;
                }

                if (terraria.ManifestModule.ModuleVersionId != SupportedMvid)
                {
                    return 12;
                }

                Type mainType = terraria.GetType("Terraria.Main", false);
                Type playerType = terraria.GetType("Terraria.Player", false);
                Type npcType = terraria.GetType("Terraria.NPC", false);
                Type playerDeathReasonType = terraria.GetType(
                    "Terraria.DataStructures.PlayerDeathReason",
                    false);
                Type playerFileDataType = terraria.GetType("Terraria.IO.PlayerFileData", false);
                Type worldFileDataType = terraria.GetType("Terraria.IO.WorldFileData", false);
                Type characterListItemType = terraria.GetType("Terraria.GameContent.UI.Elements.UICharacterListItem", false);
                Type worldListItemType = terraria.GetType("Terraria.GameContent.UI.Elements.UIWorldListItem", false);
                Type panelType = terraria.GetType("Terraria.GameContent.UI.Elements.UIPanel", false);
                Type uiListType = terraria.GetType("Terraria.GameContent.UI.Elements.UIList", false);
                Type uiElementType = terraria.GetType("Terraria.UI.UIElement", false);
                Type snapPointType = terraria.GetType("Terraria.UI.SnapPoint", false);
                if (mainType == null || playerType == null || npcType == null || playerDeathReasonType == null ||
                    playerFileDataType == null || worldFileDataType == null ||
                    characterListItemType == null || worldListItemType == null || panelType == null ||
                    uiListType == null || uiElementType == null || snapPointType == null)
                {
                    return 13;
                }

                FieldInfo showSplashField = mainType.GetField(
                    "showSplash",
                    BindingFlags.Static | BindingFlags.Public);
                if (showSplashField == null)
                {
                    return 13;
                }

                if ((bool)showSplashField.GetValue(null))
                {
                    return 10;
                }

                MethodInfo worldRejectionMethod = worldListItemType.GetMethod(
                    "TryMovingToRejectionMenuIfNeeded",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(int) },
                    null);
                MethodInfo selectPlayerMethod = mainType.GetMethod(
                    "SelectPlayer",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { playerFileDataType },
                    null);
                MethodInfo playerKillMeMethod = playerType.GetMethod(
                    "KillMe",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { playerDeathReasonType, typeof(double), typeof(int), typeof(bool) },
                    null);
                MethodInfo npcCheckActiveMethod;
                MethodInfo npcCheckDeadMethod;
                MethodInfo npcAiMethod;
                if (!TryResolveRaceBossPenaltyMembers(
                        mainType,
                        playerType,
                        npcType,
                        out npcCheckActiveMethod,
                        out npcCheckDeadMethod,
                        out npcAiMethod))
                {
                    return 14;
                }
                MethodInfo characterListCompareMethod = characterListItemType.GetMethod(
                    "CompareTo",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(object) },
                    null);
                MethodInfo worldListCompareMethod = worldListItemType.GetMethod(
                    "CompareTo",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(object) },
                    null);
                MethodInfo characterListDrawMethod = characterListItemType.GetMethods(
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .SingleOrDefault(method =>
                        method.Name == "DrawSelf" &&
                        method.DeclaringType == characterListItemType &&
                        method.GetParameters().Length == 1 &&
                        method.GetParameters()[0].ParameterType.FullName == "Microsoft.Xna.Framework.Graphics.SpriteBatch");
                MethodInfo worldListDrawMethod = worldListItemType.GetMethods(
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .SingleOrDefault(method =>
                        method.Name == "DrawSelf" &&
                        method.DeclaringType == worldListItemType &&
                        method.GetParameters().Length == 1 &&
                        method.GetParameters()[0].ParameterType.FullName == "Microsoft.Xna.Framework.Graphics.SpriteBatch");
                if (worldRejectionMethod == null || selectPlayerMethod == null ||
                    playerKillMeMethod == null ||
                    npcCheckActiveMethod == null || npcCheckDeadMethod == null ||
                    characterListCompareMethod == null || worldListCompareMethod == null ||
                    characterListDrawMethod == null || worldListDrawMethod == null ||
                    characterListCompareMethod.DeclaringType != characterListItemType ||
                    worldListCompareMethod.DeclaringType != worldListItemType)
                {
                    return 14;
                }

                if (!HasExpectedBody(worldRejectionMethod, SupportedWorldRejectionHash) ||
                    !HasExpectedBody(selectPlayerMethod, SupportedSelectPlayerHash) ||
                    !HasExpectedBody(characterListCompareMethod, SupportedCharacterListCompareHash) ||
                    !HasExpectedBody(worldListCompareMethod, SupportedWorldListCompareHash) ||
                    !HasExpectedBody(characterListDrawMethod, SupportedCharacterListDrawHash) ||
                    !HasExpectedBody(worldListDrawMethod, SupportedWorldListDrawHash))
                {
                    return 15;
                }

                worldListItemDataField = worldListItemType.GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic);
                characterListItemDataField = characterListItemType.GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic);
                panelBorderColorField = panelType.GetField("BorderColor", BindingFlags.Instance | BindingFlags.Public);
                worldPathProperty = worldFileDataType.GetProperty("Path", BindingFlags.Instance | BindingFlags.Public);
                playerPathProperty = playerFileDataType.GetProperty("Path", BindingFlags.Instance | BindingFlags.Public);
                activePlayerFileDataField = mainType.GetField("ActivePlayerFileData", BindingFlags.Static | BindingFlags.Public);
                uiElementParentProperty = uiElementType.GetProperty("Parent", BindingFlags.Instance | BindingFlags.Public);
                uiListItemsField = uiListType.GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic);
                uiElementGetSnapPointsMethod = uiElementType.GetMethod(
                    "GetSnapPoints",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    Type.EmptyTypes,
                    null);
                snapPointIdProperty = snapPointType.GetProperty("Id", BindingFlags.Instance | BindingFlags.Public);
                snapPointIdSetter = snapPointIdProperty == null
                    ? null
                    : snapPointIdProperty.GetSetMethod(true);
                worldIdField = worldFileDataType.GetField("WorldId", BindingFlags.Instance | BindingFlags.Public);
                worldUniqueIdField = worldFileDataType.GetField("UniqueId", BindingFlags.Instance | BindingFlags.Public);
                statusTextField = mainType.GetField("statusText", BindingFlags.Static | BindingFlags.Public);
                menuModeField = mainType.GetField("menuMode", BindingFlags.Static | BindingFlags.Public);
                menuMultiplayerField = mainType.GetField("menuMultiplayer", BindingFlags.Static | BindingFlags.Public);
                menuServerField = mainType.GetField("menuServer", BindingFlags.Static | BindingFlags.Public);
                raceUiPlayerNameField = playerType.GetField(
                    "name",
                    BindingFlags.Instance | BindingFlags.Public);
                raceUiDeathReasonGetTextMethod = playerDeathReasonType.GetMethod(
                    "GetDeathText",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(string) },
                    null);
                PropertyInfo goldColorProperty = panelBorderColorField == null
                    ? null
                    : panelBorderColorField.FieldType.GetProperty("Gold", BindingFlags.Static | BindingFlags.Public);
                assignedBorderColor = goldColorProperty == null
                    ? null
                    : goldColorProperty.GetValue(null, null);
                if (worldListItemDataField == null || characterListItemDataField == null ||
                    panelBorderColorField == null || assignedBorderColor == null ||
                    worldPathProperty == null || playerPathProperty == null ||
                    activePlayerFileDataField == null || uiElementParentProperty == null ||
                    uiListItemsField == null || uiElementGetSnapPointsMethod == null ||
                    snapPointIdProperty == null || snapPointIdSetter == null || worldIdField == null ||
                    worldUniqueIdField == null || statusTextField == null || menuModeField == null ||
                    menuMultiplayerField == null || menuServerField == null ||
                    raceUiPlayerNameField == null || raceUiDeathReasonGetTextMethod == null)
                {
                    return 16;
                }

                MethodInfo worldPrefix = typeof(EntryPoint).GetMethod(
                    "WorldRejectionPrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo multiplayerPrefix = typeof(EntryPoint).GetMethod(
                    "MultiplayerSelectionPrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo characterListComparePrefix = typeof(EntryPoint).GetMethod(
                    "CharacterListComparePrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo worldListComparePrefix = typeof(EntryPoint).GetMethod(
                    "WorldListComparePrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo characterListDrawPrefix = typeof(EntryPoint).GetMethod(
                    "CharacterListDrawPrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo worldListDrawPrefix = typeof(EntryPoint).GetMethod(
                    "WorldListDrawPrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo playerKillMePostfix = typeof(EntryPoint).GetMethod(
                    "PlayerKillMePostfix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo skeletronCheckActivePrefix = typeof(EntryPoint).GetMethod(
                    "SkeletronCheckActivePrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo wallOfFleshAiPrefix = typeof(EntryPoint).GetMethod(
                    "WallOfFleshAiPrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo raceBossCheckDeadPostfix = typeof(EntryPoint).GetMethod(
                    "RaceBossCheckDeadPostfix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (worldPrefix == null || multiplayerPrefix == null ||
                    characterListComparePrefix == null || worldListComparePrefix == null ||
                    characterListDrawPrefix == null || worldListDrawPrefix == null ||
                    playerKillMePostfix == null ||
                    skeletronCheckActivePrefix == null ||
                    wallOfFleshAiPrefix == null ||
                    raceBossCheckDeadPostfix == null)
                {
                    return 17;
                }

                var harmony = new Harmony(HarmonyId);
                MethodInfo[] patchedMethods =
                {
                    worldRejectionMethod,
                    selectPlayerMethod,
                    characterListCompareMethod,
                    worldListCompareMethod,
                    characterListDrawMethod,
                    worldListDrawMethod,
                    playerKillMeMethod,
                    npcCheckActiveMethod,
                    npcAiMethod,
                    npcCheckDeadMethod
                };
                int installResult = InstallPatchSet(
                    harmony,
                    patchedMethods,
                    () =>
                    {
                        harmony.Patch(worldRejectionMethod, new HarmonyMethod(worldPrefix));
                        harmony.Patch(selectPlayerMethod, new HarmonyMethod(multiplayerPrefix));
                        harmony.Patch(characterListCompareMethod, new HarmonyMethod(characterListComparePrefix));
                        harmony.Patch(worldListCompareMethod, new HarmonyMethod(worldListComparePrefix));
                        harmony.Patch(characterListDrawMethod, new HarmonyMethod(characterListDrawPrefix));
                        harmony.Patch(worldListDrawMethod, new HarmonyMethod(worldListDrawPrefix));
                        harmony.Patch(
                            playerKillMeMethod,
                            postfix: new HarmonyMethod(playerKillMePostfix));
                        harmony.Patch(
                            npcCheckActiveMethod,
                            prefix: new HarmonyMethod(skeletronCheckActivePrefix));
                        harmony.Patch(
                            npcAiMethod,
                            prefix: new HarmonyMethod(wallOfFleshAiPrefix));
                        harmony.Patch(
                            npcCheckDeadMethod,
                            postfix: new HarmonyMethod(raceBossCheckDeadPostfix));
                    },
                    () => HasOwnedPrefix(worldRejectionMethod) &&
                        HasOwnedPrefix(selectPlayerMethod) &&
                        HasOwnedPrefix(characterListCompareMethod) &&
                        HasOwnedPrefix(worldListCompareMethod) &&
                        HasOwnedPrefix(characterListDrawMethod) &&
                        HasOwnedPrefix(worldListDrawMethod) &&
                        HasOwnedPostfix(playerKillMeMethod) &&
                        HasOwnedPrefix(npcCheckActiveMethod) &&
                        HasOwnedPrefix(npcAiMethod) &&
                        HasOwnedPostfix(npcCheckDeadMethod),
                    18);
                if (installResult != 0)
                {
                    return installResult;
                }

                patchesInstalled = true;
                return 0;
            }
        }

        private static void PlayerKillMePostfix(object __instance, object __0)
        {
            try
            {
                TryArmRaceBossPenalty(__instance);
                if (configuration == null ||
                    __instance == null ||
                    __0 == null ||
                    raceUiLocalPlayerProperty == null ||
                    raceUiPlayerDeadField == null ||
                    raceUiPlayerNameField == null ||
                    raceUiDeathReasonGetTextMethod == null)
                {
                    return;
                }

                object localPlayer = raceUiLocalPlayerProperty.GetValue(null, null);
                if (!ReferenceEquals(__instance, localPlayer) ||
                    !(bool)raceUiPlayerDeadField.GetValue(__instance))
                {
                    return;
                }

                string playerName = (string)raceUiPlayerNameField.GetValue(__instance) ??
                    string.Empty;
                object deathText = raceUiDeathReasonGetTextMethod.Invoke(
                    __0,
                    new object[] { playerName });
                string message = deathText == null
                    ? string.Empty
                    : deathText.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    Interlocked.Exchange(ref raceUiLocalDeathMessage, message);
                }
            }
            catch
            {
            }
        }

        private static bool WorldRejectionPrefix(object __instance, ref bool __result)
        {
            WorldLockConfiguration current = configuration;
            if (current == null)
            {
                return true;
            }

            bool allowed = false;
            try
            {
                object data = worldListItemDataField.GetValue(__instance);
                string path = (string)worldPathProperty.GetValue(data, null);
                int worldId = (int)worldIdField.GetValue(data);
                Guid uniqueId = (Guid)worldUniqueIdField.GetValue(data);
                object activePlayer = activePlayerFileDataField.GetValue(null);
                string activePlayerPath = activePlayer == null
                    ? string.Empty
                    : (string)playerPathProperty.GetValue(activePlayer, null);
                allowed = current.EntryAllowed &&
                    current.Matches(path, worldId, uniqueId) &&
                    current.MatchesPlayer(activePlayerPath);
            }
            catch
            {
                allowed = false;
            }

            if (allowed)
            {
                return true;
            }

            ShowRejection(current.Message);
            __result = true;
            return false;
        }

        private static bool CharacterListComparePrefix(object __instance, object __0, ref int __result)
        {
            WorldLockConfiguration current = configuration;
            if (current == null)
            {
                return true;
            }

            try
            {
                bool leftIsAssigned = IsAssignedPlayerListItem(__instance, current);
                bool rightIsAssigned = IsAssignedPlayerListItem(__0, current);
                if (leftIsAssigned == rightIsAssigned)
                {
                    return true;
                }

                __result = leftIsAssigned ? -1 : 1;
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static bool WorldListComparePrefix(object __instance, object __0, ref int __result)
        {
            WorldLockConfiguration current = configuration;
            if (current == null)
            {
                return true;
            }

            try
            {
                bool leftIsAssigned = IsAssignedWorldListItem(__instance, current);
                bool rightIsAssigned = IsAssignedWorldListItem(__0, current);
                if (leftIsAssigned == rightIsAssigned)
                {
                    return true;
                }

                __result = leftIsAssigned ? -1 : 1;
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static bool IsAssignedPlayerListItem(object item, WorldLockConfiguration current)
        {
            if (item == null)
            {
                return false;
            }

            object data = characterListItemDataField.GetValue(item);
            string path = data == null
                ? string.Empty
                : (string)playerPathProperty.GetValue(data, null);
            return current.MatchesPlayer(path);
        }

        private static bool IsAssignedWorldListItem(object item, WorldLockConfiguration current)
        {
            if (item == null)
            {
                return false;
            }

            object data = worldListItemDataField.GetValue(item);
            if (data == null)
            {
                return false;
            }

            string path = (string)worldPathProperty.GetValue(data, null);
            int worldId = (int)worldIdField.GetValue(data);
            Guid uniqueId = (Guid)worldUniqueIdField.GetValue(data);
            return current.Matches(path, worldId, uniqueId);
        }

        private static void CharacterListDrawPrefix(object __instance)
        {
            WorldLockConfiguration current = configuration;
            if (current == null)
            {
                return;
            }

            try
            {
                SynchronizeCharacterListSnapPointIds(__instance);
                if (IsAssignedPlayerListItem(__instance, current))
                {
                    panelBorderColorField.SetValue(__instance, assignedBorderColor);
                }
            }
            catch
            {
            }
        }

        private static void SynchronizeCharacterListSnapPointIds(object item)
        {
            object innerList = uiElementParentProperty.GetValue(item, null);
            object list = innerList == null
                ? null
                : uiElementParentProperty.GetValue(innerList, null);
            IList items = list == null
                ? null
                : uiListItemsField.GetValue(list) as IList;
            int visualIndex = items == null ? -1 : items.IndexOf(item);
            if (visualIndex < 0)
            {
                return;
            }

            IEnumerable snapPoints = uiElementGetSnapPointsMethod.Invoke(item, null) as IEnumerable;
            if (snapPoints == null)
            {
                return;
            }

            foreach (object snapPoint in snapPoints)
            {
                if (snapPoint != null &&
                    (int)snapPointIdProperty.GetValue(snapPoint, null) != visualIndex)
                {
                    snapPointIdSetter.Invoke(snapPoint, new object[] { visualIndex });
                }
            }
        }

        private static void WorldListDrawPrefix(object __instance)
        {
            WorldLockConfiguration current = configuration;
            if (current == null)
            {
                return;
            }

            try
            {
                if (IsAssignedWorldListItem(__instance, current))
                {
                    panelBorderColorField.SetValue(__instance, assignedBorderColor);
                }
            }
            catch
            {
            }
        }

        private static bool MultiplayerSelectionPrefix(object __0)
        {
            WorldLockConfiguration current = configuration;
            if (current == null)
            {
                return true;
            }

            try
            {
                bool multiplayer = (bool)menuMultiplayerField.GetValue(null);
                bool hosting = (bool)menuServerField.GetValue(null);
                if (!multiplayer || hosting)
                {
                    return true;
                }
            }
            catch
            {
                RejectPlayerSelection(current.Message);
                return false;
            }

            RejectPlayerSelection(current.Message);
            return false;
        }

        private static void RejectPlayerSelection(string message)
        {
            try
            {
                statusTextField.SetValue(null, message);
                menuModeField.SetValue(null, 1);
            }
            catch
            {
            }
        }

        private static void ShowRejection(string message)
        {
            try
            {
                statusTextField.SetValue(null, message);
                menuModeField.SetValue(null, 1000000);
            }
            catch
            {
            }
        }

        private static bool HasOwnedPrefix(MethodInfo method)
        {
            Patches patches = Harmony.GetPatchInfo(method);
            return patches != null && patches.Prefixes.Any(patch => patch.owner == HarmonyId);
        }

        private static bool HasOwnedFinalizer(MethodInfo method)
        {
            Patches patches = Harmony.GetPatchInfo(method);
            return patches != null && patches.Finalizers.Any(patch => patch.owner == HarmonyId);
        }

        private static bool HasOwnedPostfix(MethodInfo method)
        {
            Patches patches = Harmony.GetPatchInfo(method);
            return patches != null && patches.Postfixes.Any(patch => string.Equals(patch.owner, HarmonyId, StringComparison.Ordinal));
        }

        private static bool HasOwnedTranspiler(MethodInfo method)
        {
            Patches patches = Harmony.GetPatchInfo(method);
            return patches != null && patches.Transpilers.Any(patch => patch.owner == HarmonyId);
        }

        private static bool HasExpectedBody(MethodInfo method, string expectedHash)
        {
            MethodBody body = method.GetMethodBody();
            byte[] il = body == null ? null : body.GetILAsByteArray();
            return il != null && string.Equals(Hash(il), expectedHash, StringComparison.Ordinal);
        }

        private static string Hash(byte[] data)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(data);
                return string.Concat(hash.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
            }
        }

        private sealed class WorldLockConfiguration
        {
            private volatile bool entryAllowed;

            private WorldLockConfiguration(
                string path,
                int worldId,
                Guid uniqueId,
                string playerPath,
                string message,
                int protocolVersion,
                string epochId,
                string entropySeedBase64,
                string compatibilityId,
                int enabledCapabilities,
                int chancePolicyVersion,
                PlanteraBulbConfiguration planteraBulb,
                bool entryAllowed,
                bool bossFailurePenaltyEnabled,
                string packageDigest)
            {
                Path = NormalizePath(path);
                WorldId = worldId;
                UniqueId = uniqueId;
                PlayerPath = NormalizePath(playerPath);
                Message = message;
                ProtocolVersion = protocolVersion;
                EpochId = epochId;
                EntropySeedBase64 = entropySeedBase64;
                CompatibilityId = compatibilityId;
                EnabledCapabilities = enabledCapabilities;
                ChancePolicyVersion = chancePolicyVersion;
                PlanteraBulb = planteraBulb;
                this.entryAllowed = entryAllowed;
                BossFailurePenaltyEnabled = bossFailurePenaltyEnabled;
                PackageDigest = packageDigest;
                EntropySeed = Convert.FromBase64String(entropySeedBase64);
                State = new DeterminismGenerationState();
            }

            public string Path { get; private set; }

            public int WorldId { get; private set; }

            public Guid UniqueId { get; private set; }

            public string PlayerPath { get; private set; }

            public string Message { get; private set; }

            public int ProtocolVersion { get; private set; }

            public string EpochId { get; private set; }

            public string EntropySeedBase64 { get; private set; }

            public string CompatibilityId { get; private set; }

            public int EnabledCapabilities { get; private set; }

            public int ChancePolicyVersion { get; private set; }

            public PlanteraBulbConfiguration PlanteraBulb { get; private set; }

            public bool EntryAllowed { get { return entryAllowed; } }

            public bool BossFailurePenaltyEnabled { get; private set; }

            public string PackageDigest { get; private set; }

            public byte[] EntropySeed { get; private set; }

            public DeterminismGenerationState State { get; private set; }

            public bool HasCapability(int capability)
            {
                return (EnabledCapabilities & capability) == capability;
            }

            public WorldLockConfiguration CreateNextGeneration()
            {
                return new WorldLockConfiguration(
                    Path,
                    WorldId,
                    UniqueId,
                    PlayerPath,
                    Message,
                    ProtocolVersion,
                    EpochId,
                    EntropySeedBase64,
                    CompatibilityId,
                    EnabledCapabilities,
                    ChancePolicyVersion,
                    PlanteraBulb,
                    EntryAllowed,
                    BossFailurePenaltyEnabled,
                    PackageDigest);
            }

            public void SetEntryAllowed(bool allowed)
            {
                entryAllowed = allowed;
            }

            public bool Matches(string path, int worldId, Guid uniqueId)
            {
                return worldId == WorldId &&
                    uniqueId == UniqueId &&
                    string.Equals(NormalizePath(path), Path, StringComparison.OrdinalIgnoreCase);
            }

            public bool MatchesPlayer(string path)
            {
                return !string.IsNullOrWhiteSpace(path) &&
                    string.Equals(NormalizePath(path), PlayerPath, StringComparison.OrdinalIgnoreCase);
            }

            public static bool TryParse(string command, out WorldLockConfiguration result)
            {
                result = null;
                string[] parts = (command ?? string.Empty).Split(new[] { '\n' }, StringSplitOptions.None);
                int worldId;
                Guid uniqueId;
                int protocolVersion;
                int enabledCapabilities;
                int chancePolicyVersion;
                Guid epochId;
                bool entryAllowed;
                bool bossFailurePenaltyEnabled;
                if (parts.Length != 16 || !string.Equals(parts[0], "configure", StringComparison.Ordinal) ||
                    !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out worldId) ||
                    !Guid.TryParse(parts[3], out uniqueId) ||
                    !int.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out protocolVersion) ||
                    !Guid.TryParseExact(parts[7], "N", out epochId) ||
                    !int.TryParse(parts[10], NumberStyles.Integer, CultureInfo.InvariantCulture, out enabledCapabilities) ||
                    !int.TryParse(parts[11], NumberStyles.Integer, CultureInfo.InvariantCulture, out chancePolicyVersion) ||
                    !TryParseFlag(parts[13], out entryAllowed) ||
                    !TryParseFlag(parts[14], out bossFailurePenaltyEnabled))
                {
                    return false;
                }

                try
                {
                    string path = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
                    string playerPath = Encoding.UTF8.GetString(Convert.FromBase64String(parts[4]));
                    string message = Encoding.UTF8.GetString(Convert.FromBase64String(parts[5]));
                    byte[] entropySeed = Convert.FromBase64String(parts[8]);
                    string compatibilityId = Encoding.UTF8.GetString(Convert.FromBase64String(parts[9]));
                    PlanteraBulbConfiguration planteraBulb;
                    if (!PlanteraBulbConfiguration.TryParse(parts[12], out planteraBulb))
                    {
                        return false;
                    }
                    string canonicalPackage = string.Join(
                        "|",
                        protocolVersion.ToString(CultureInfo.InvariantCulture),
                        epochId.ToString("N"),
                        parts[8],
                        compatibilityId,
                        enabledCapabilities.ToString(CultureInfo.InvariantCulture),
                        chancePolicyVersion.ToString(CultureInfo.InvariantCulture));
                    string expectedDigest = Hash(Encoding.UTF8.GetBytes(canonicalPackage));
                    if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(playerPath) ||
                        string.IsNullOrWhiteSpace(message) ||
                        entropySeed.Length != RaceDeterminismProtocol.EntropySeedLength ||
                        protocolVersion != RaceDeterminismProtocol.CurrentVersion ||
                        chancePolicyVersion != RaceDeterminismProtocol.CurrentChancePolicyVersion ||
                        (enabledCapabilities & RaceDeterminismProtocol.WorldLockCapability) == 0 ||
                        (enabledCapabilities & ~RaceDeterminismProtocol.KnownCapabilities) != 0 ||
                        !string.Equals(
                            compatibilityId,
                            RaceDeterminismProtocol.TerrariaCompatibilityId,
                            StringComparison.Ordinal) ||
                        !string.Equals(expectedDigest, parts[15], StringComparison.Ordinal))
                    {
                        return false;
                    }

                    result = new WorldLockConfiguration(
                        path,
                        worldId,
                        uniqueId,
                        playerPath,
                        message,
                        protocolVersion,
                        epochId.ToString("N"),
                        parts[8],
                        compatibilityId,
                        enabledCapabilities,
                        chancePolicyVersion,
                        planteraBulb,
                        entryAllowed,
                        bossFailurePenaltyEnabled,
                        parts[15]);
                    return true;
                }
                catch (FormatException)
                {
                    return false;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            private static bool TryParseFlag(string value, out bool result)
            {
                if (string.Equals(value, "1", StringComparison.Ordinal))
                {
                    result = true;
                    return true;
                }

                if (string.Equals(value, "0", StringComparison.Ordinal))
                {
                    result = false;
                    return true;
                }

                result = false;
                return false;
            }

            private static string NormalizePath(string path)
            {
                return System.IO.Path.GetFullPath(path).TrimEnd(
                    System.IO.Path.DirectorySeparatorChar,
                    System.IO.Path.AltDirectorySeparatorChar);
            }
        }

        private sealed class PlanteraBulbConfiguration
        {
            private PlanteraBulbConfiguration(
                int doorX,
                int doorY,
                int minimumY,
                int upperOuterRadius,
                int lowerInnerRadius,
                int lowerOuterRadius,
                IList<PlanteraBulbAnchor> anchors)
            {
                DoorX = doorX;
                DoorY = doorY;
                MinimumY = minimumY;
                UpperOuterRadius = upperOuterRadius;
                LowerInnerRadius = lowerInnerRadius;
                LowerOuterRadius = lowerOuterRadius;
                Anchors = anchors;
            }

            public static PlanteraBulbConfiguration Empty { get; } = new PlanteraBulbConfiguration(
                0,
                0,
                0,
                0,
                0,
                0,
                new List<PlanteraBulbAnchor>());

            public int DoorX { get; private set; }
            public int DoorY { get; private set; }
            public int MinimumY { get; private set; }
            public int UpperOuterRadius { get; private set; }
            public int LowerInnerRadius { get; private set; }
            public int LowerOuterRadius { get; private set; }
            public IList<PlanteraBulbAnchor> Anchors { get; private set; }

            public bool Contains(int x, int y)
            {
                if (Anchors.Count == 0 || y < MinimumY)
                {
                    return false;
                }

                long dx = (long)x * 2L - 1L - (long)DoorX * 2L;
                long dy = (long)y * 2L - 1L - (long)DoorY * 2L;
                long distanceSquared = dx * dx + dy * dy;
                long upperOuter = (long)UpperOuterRadius * 2L;
                long lowerInner = (long)LowerInnerRadius * 2L;
                long lowerOuter = (long)LowerOuterRadius * 2L;
                return dy <= 0L
                    ? distanceSquared <= upperOuter * upperOuter
                    : distanceSquared >= lowerInner * lowerInner && distanceSquared <= lowerOuter * lowerOuter;
            }

            public static bool TryParse(string encoded, out PlanteraBulbConfiguration result)
            {
                result = null;
                try
                {
                    string canonical = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                    if (string.Equals(canonical, "0", StringComparison.Ordinal))
                    {
                        result = Empty;
                        return true;
                    }

                    string[] parts = canonical.Split(new[] { '|' }, StringSplitOptions.None);
                    int version;
                    int doorX;
                    int doorY;
                    int minimumY;
                    int upperOuterRadius;
                    int lowerInnerRadius;
                    int lowerOuterRadius;
                    if (parts.Length != 8 ||
                        !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out version) ||
                        !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out doorX) ||
                        !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out doorY) ||
                        !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out minimumY) ||
                        !int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out upperOuterRadius) ||
                        !int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out lowerInnerRadius) ||
                        !int.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out lowerOuterRadius) ||
                        version != 1 || doorX < 5 || doorY < 5 || minimumY < 0 ||
                        upperOuterRadius <= 0 || lowerInnerRadius <= 0 || lowerOuterRadius <= lowerInnerRadius)
                    {
                        return false;
                    }

                    string[] encodedAnchors = parts[7].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if (encodedAnchors.Length == 0 || encodedAnchors.Length > 100000)
                    {
                        return false;
                    }

                    var anchors = new List<PlanteraBulbAnchor>(encodedAnchors.Length);
                    var uniqueAnchors = new HashSet<long>();
                    foreach (string encodedAnchor in encodedAnchors)
                    {
                        string[] coordinates = encodedAnchor.Split(',');
                        int x;
                        int y;
                        if (coordinates.Length != 2 ||
                            !int.TryParse(coordinates[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) ||
                            !int.TryParse(coordinates[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y) ||
                            x < 5 || y < minimumY)
                        {
                            return false;
                        }

                        long key = ((long)x << 32) ^ (uint)y;
                        if (!uniqueAnchors.Add(key))
                        {
                            return false;
                        }
                        anchors.Add(new PlanteraBulbAnchor(x, y));
                    }

                    var parsed = new PlanteraBulbConfiguration(
                        doorX,
                        doorY,
                        minimumY,
                        upperOuterRadius,
                        lowerInnerRadius,
                        lowerOuterRadius,
                        anchors);
                    if (!anchors.All(anchor => parsed.Contains(anchor.X, anchor.Y)))
                    {
                        return false;
                    }

                    result = parsed;
                    return true;
                }
                catch (FormatException)
                {
                    return false;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }
        }

        private sealed class PlanteraBulbAnchor
        {
            public PlanteraBulbAnchor(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; private set; }
            public int Y { get; private set; }
        }

        private sealed class DeterminismGenerationState
        {
            private readonly object townNpcSync = new object();
            private uint lastTownNpcUpdateTick;
            private bool hasTownNpcUpdateTick;

            public DeterminismGenerationState()
            {
                EventCounters = new DeterministicEventCounter();
                AccumulatorSync = new object();
                ChanceAccumulators = new Dictionary<string, IntegerChanceAccumulator>(StringComparer.Ordinal);
            }

            public DeterministicEventCounter EventCounters { get; private set; }
            public object AccumulatorSync { get; private set; }
            public Dictionary<string, IntegerChanceAccumulator> ChanceAccumulators { get; private set; }

            public bool AdvanceTownNpcCheck(uint updateTick, int currentElapsed, int period, out int nextElapsed)
            {
                lock (townNpcSync)
                {
                    if (hasTownNpcUpdateTick && updateTick == lastTownNpcUpdateTick)
                    {
                        nextElapsed = currentElapsed;
                        return false;
                    }

                    hasTownNpcUpdateTick = true;
                    lastTownNpcUpdateTick = updateTick;
                    nextElapsed = currentElapsed + 1;
                    if (nextElapsed < period)
                    {
                        return false;
                    }

                    nextElapsed = 0;
                    return true;
                }
            }

            public void ResetTownNpcCheck()
            {
                lock (townNpcSync)
                {
                    hasTownNpcUpdateTick = false;
                    lastTownNpcUpdateTick = 0;
                }
            }
        }

    }
}
