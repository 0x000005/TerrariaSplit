using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TerrariaSplit.Race.Determinism;

namespace TerrariaSplit.MemoryBridge.Payload
{
    public static partial class EntryPoint
    {
        private const int PlayerTriggeredCapability = 4;
        private const int AlchemyAndLuckCapability = 8;
        private const int WorldTransitionCapability = 16;

        private static MethodInfo worldGenRandomGetter;
        private static FieldInfo tileTypeField;
        private static FieldInfo tileFrameXField;
        private static FieldInfo tileFrameYField;
        private static FieldInfo itemTypeField;
        private static FieldInfo playerWhoAmIField;
        private static FieldInfo recipeAlchemyField;
        private static FieldInfo recipeCreateItemField;
        private static FieldInfo requiredItemIdField;
        private static FieldInfo requiredItemStackField;
        private static PropertyInfo playerAlchemyTableProperty;
        private static FieldInfo playerAlchemyTableField;
        private static PropertyInfo worldItemTypeProperty;
        private static MethodInfo getTreeTypeAndTreeBottomMethod;
        private static MethodInfo unifiedRandomNextMethod;
        private static MethodInfo unifiedRandomNextRangeMethod;
        private static AttemptPlanteraBulbDelegate attemptPlanteraBulb;

        private delegate bool AttemptPlanteraBulbDelegate(int x, int y, bool forceBulb);

        [ThreadStatic]
        private static object threadWorldGenRandom;

        [ThreadStatic]
        private static string activeChanceContext;

        [ThreadStatic]
        private static int activeLuckCallIndex;

        [ThreadStatic]
        private static string shimmerAlchemyContext;

        [ThreadStatic]
        private static WorldLockConfiguration activeScopeConfiguration;

        private static void ResetAdvancedDeterminismState()
        {
            ResetRaceBossPenalty();
            threadWorldGenRandom = null;
            activeChanceContext = null;
            activeLuckCallIndex = 0;
            npcDropRuleDepth = 0;
            shimmerAlchemyContext = null;
            activeScopeConfiguration = null;
        }

        private static int EnsurePlayerTriggeredPatchesInstalled()
        {
            lock (PatchSync)
            {
                if (playerTriggeredPatchInstalled)
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
                    return 30;
                }

                MethodInfo shakeTree = worldGenType.GetMethod("ShakeTree", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(int), typeof(int) }, null);
                MethodInfo potDrops = worldGenType.GetMethod("SpawnThingsFromPot", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(int) }, null);
                MethodInfo potFruit = worldGenType.GetMethod("GetFruitForPot", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(int), typeof(int), typeof(int) }, null);
                MethodInfo tileDrops = worldGenType.GetMethod("KillTile_DropItems", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(int), typeof(int), tileType, typeof(bool) }, null);
                MethodInfo tileDropResolver = worldGenType.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .SingleOrDefault(method => method.Name == "KillTile_GetItemDrops");
                MethodInfo treeDropResolver = worldGenType.GetMethod(
                    "KillTile_GetTreeDrops",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    null,
                    new[]
                    {
                        typeof(int),
                        typeof(int),
                        tileType,
                        typeof(bool).MakeByRefType(),
                        typeof(bool).MakeByRefType(),
                        typeof(int).MakeByRefType(),
                        typeof(int).MakeByRefType()
                    },
                    null);
                MethodInfo tileBaitDrops = worldGenType.GetMethod("KillTile_DropBait", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(int), typeof(int), tileType }, null);
                MethodInfo check2x1 = worldGenType.GetMethod("Check2x1", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(int), typeof(int), typeof(ushort) }, null);
                MethodInfo checkOrb = worldGenType.GetMethod("CheckOrb", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(int), typeof(int), typeof(int) }, null);
                MethodInfo openBossBag = playerType.GetMethod("OpenBossBag", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(int) }, null);
                MethodInfo openFishingCrate = playerType.GetMethod("OpenFishingCrate", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(int) }, null);
                MethodInfo openHerbBag = playerType.GetMethod("OpenHerbBag", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(int) }, null);
                MethodInfo openCanOfWorms = playerType.GetMethod("OpenCanofWorms", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(int) }, null);
                MethodInfo teleport = playerType.GetMethod("TeleportationPotion", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                MethodInfo prefix = itemType.GetMethod("Prefix", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(int), typeof(bool).MakeByRefType() }, null);
                MethodInfo[] methods = { shakeTree, potDrops, potFruit, tileDrops, tileDropResolver, treeDropResolver, tileBaitDrops, check2x1, checkOrb, openBossBag, openFishingCrate, openHerbBag, openCanOfWorms, teleport, prefix };
                string[] hashes =
                {
                    "0DFF48319CBB1E7E23D32DE705450B8F0113FCDF2EEA5B40B79A317A92669BBE",
                    "F958799927308BB444E676CBD8D61891F0398D240CA91F0D9F31CFB98F72FAE6",
                    "654B79F693E8B3D00C5C81B8C1F92E3F9085BBFE8FDC0F5B1132C1FDCC589173",
                    "6DC6A82681C0E0DE3C839D02CBDD54C7D5F1D9A3F1384221FEE877BBD0D59C2E",
                    "9EE37E14C9C1261873E203346DC934C290A13B13ECAEC92AA1599B19DDD3430B",
                    "F12306844455C584BB9DD812A21B36F6FA2F205D60E0445A83A2660A3FF9DBE2",
                    "46548D424CA46A631928D3ADF17461528277C660866D2A0A102A9168ADC29EC0",
                    "58880C93231255287D973EC1C57A1520AD17AB2D3F3A4E38F7960071F4C23EAC",
                    "062C366545787819F898B127A1D87C8C51F68C1D529605B3C1A446C0802E3551",
                    "702F6D7EF9825119D34F9AADC0FD2AA11F73ED3BD8AB93E4A384D73AD6E55CB4",
                    "203E00B2E611302386E06117F0FF8EDFC71091A8B27DF97229191458BE5CE6CE",
                    "88B07C6989C339B6C46122114EB24946E68F281DF10DA917EC979AE7C21B0A70",
                    "4B66352EBB1AFCB6BB854432E1AE28DAD48062504F57EA854F657BBE44626725",
                    "DC54C18641D46826E650A15B94D767BD367CFD6F08941AE9D4CEAC57F5CF249F",
                    "45BEF7914D6F790ACDEF1925026C186613D04CEF5665D2946B4E680BD013B1D8"
                };
                if (!MethodsMatch(methods, hashes))
                {
                    return 31;
                }

                tileTypeField = tileType.GetField("type", BindingFlags.Instance | BindingFlags.Public);
                tileFrameXField = tileType.GetField("frameX", BindingFlags.Instance | BindingFlags.Public);
                tileFrameYField = tileType.GetField("frameY", BindingFlags.Instance | BindingFlags.Public);
                itemTypeField = itemType.GetField("type", BindingFlags.Instance | BindingFlags.Public);
                playerWhoAmIField = playerType.GetField("whoAmI", BindingFlags.Instance | BindingFlags.Public);
                getTreeTypeAndTreeBottomMethod = worldGenType.GetMethod(
                    "GetTreeTypeAndTreeBottom",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { typeof(int), typeof(int), typeof(int).MakeByRefType(), typeof(int).MakeByRefType() },
                    null);
                if (tileTypeField == null || tileFrameXField == null || tileFrameYField == null ||
                    itemTypeField == null || playerWhoAmIField == null || getTreeTypeAndTreeBottomMethod == null)
                {
                    return 32;
                }

                MethodInfo treePrefix = GetPrivateMethod("TreeScopePrefix");
                MethodInfo potPrefix = GetPrivateMethod("PotScopePrefix");
                MethodInfo tilePrefix = GetPrivateMethod("TileDropScopePrefix");
                MethodInfo tileBaitPrefix = GetPrivateMethod("TileBaitDropScopePrefix");
                MethodInfo smallPilePrefix = GetPrivateMethod("SmallPileScopePrefix");
                MethodInfo orbPrefix = GetPrivateMethod("OrbScopePrefix");
                MethodInfo playerPrefix = GetPrivateMethod("PlayerActionScopePrefix");
                MethodInfo itemPrefix = GetPrivateMethod("ItemPrefixScopePrefix");
                MethodInfo worldGenProviderTranspiler = GetPrivateMethod("WorldGenProviderTranspiler");
                MethodInfo finalizer = GetPrivateMethod("AdvancedScopeFinalizer");
                if (treePrefix == null || potPrefix == null || tilePrefix == null || tileBaitPrefix == null || smallPilePrefix == null || orbPrefix == null ||
                    playerPrefix == null || itemPrefix == null || worldGenProviderTranspiler == null || finalizer == null)
                {
                    return 33;
                }

                var harmony = new Harmony(HarmonyId);
                int installResult = InstallPatchSet(
                    harmony,
                    methods,
                    () =>
                    {
                        harmony.Patch(shakeTree, prefix: new HarmonyMethod(treePrefix), transpiler: new HarmonyMethod(worldGenProviderTranspiler), finalizer: new HarmonyMethod(finalizer));
                        harmony.Patch(potDrops, prefix: new HarmonyMethod(potPrefix), transpiler: new HarmonyMethod(worldGenProviderTranspiler), finalizer: new HarmonyMethod(finalizer));
                        harmony.Patch(potFruit, transpiler: new HarmonyMethod(worldGenProviderTranspiler));
                        harmony.Patch(tileDrops, prefix: new HarmonyMethod(tilePrefix), transpiler: new HarmonyMethod(worldGenProviderTranspiler), finalizer: new HarmonyMethod(finalizer));
                        harmony.Patch(tileDropResolver, transpiler: new HarmonyMethod(worldGenProviderTranspiler));
                        harmony.Patch(treeDropResolver, transpiler: new HarmonyMethod(worldGenProviderTranspiler));
                        harmony.Patch(tileBaitDrops, prefix: new HarmonyMethod(tileBaitPrefix), transpiler: new HarmonyMethod(worldGenProviderTranspiler), finalizer: new HarmonyMethod(finalizer));
                        harmony.Patch(check2x1, prefix: new HarmonyMethod(smallPilePrefix), transpiler: new HarmonyMethod(worldGenProviderTranspiler), finalizer: new HarmonyMethod(finalizer));
                        harmony.Patch(checkOrb, prefix: new HarmonyMethod(orbPrefix), transpiler: new HarmonyMethod(worldGenProviderTranspiler), finalizer: new HarmonyMethod(finalizer));
                        foreach (MethodInfo method in new[] { openBossBag, openFishingCrate, openHerbBag, openCanOfWorms, teleport })
                        {
                            harmony.Patch(method, prefix: new HarmonyMethod(playerPrefix), finalizer: new HarmonyMethod(finalizer));
                        }
                        harmony.Patch(prefix, prefix: new HarmonyMethod(itemPrefix), finalizer: new HarmonyMethod(finalizer));
                    },
                    () => new[] { shakeTree, potDrops, tileDrops, tileBaitDrops, check2x1, checkOrb, openBossBag, openFishingCrate, openHerbBag, openCanOfWorms, teleport, prefix }
                            .All(method => HasOwnedPrefix(method) && HasOwnedFinalizer(method)) &&
                        new[] { shakeTree, potDrops, potFruit, tileDrops, tileDropResolver, treeDropResolver, tileBaitDrops, check2x1, checkOrb }.All(HasOwnedTranspiler),
                    34);
                if (installResult != 0)
                {
                    return installResult;
                }

                playerTriggeredPatchInstalled = true;
                return 0;
            }
        }

        private static int EnsureAlchemyAndLuckPatchesInstalled()
        {
            lock (PatchSync)
            {
                if (alchemyAndLuckPatchInstalled)
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
                    return 40;
                }

                Type recipeType = terraria.GetType("Terraria.Recipe", false);
                Type requiredItemType = terraria.GetType("Terraria.Recipe+RequiredItemEntry", false);
                Type worldItemType = terraria.GetType("Terraria.WorldItem", false);
                Type luckType = terraria.GetType("Terraria.GameContent.Luck", false);
                if (recipeType == null || requiredItemType == null || worldItemType == null || luckType == null)
                {
                    return 41;
                }

                MethodInfo craftDiscount = recipeType.GetMethod("GetIngredientCraftingDiscount", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { playerType, requiredItemType }, null);
                MethodInfo shimmer = worldItemType.GetMethod("GetShimmered", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                string[] luckNames = { "RollLuck", "RollBadLuck", "RollOnlyBadLuck", "RollBadLuckExtreme", "RollOnlyBadLuckExtreme" };
                MethodInfo[] luckMethods = luckNames.Select(name => luckType.GetMethod(name, BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(float), typeof(int) }, null)).ToArray();
                MethodInfo[] methods = new[] { craftDiscount, shimmer }.Concat(luckMethods).ToArray();
                string[] hashes =
                {
                    "B8E57DAB0E09D4BA72239CD53541C06D032A48D2F441E74DB4EE01068B56D43B",
                    "4031277142B9EB4EEDEA24C94A68BC17D3430E21A7EC3682FEFA875446615115",
                    "A7359C293AF8D349C45566065E94847FC4ADF108165C240A3160D4EE795BFD5B",
                    "48BF34705B55E79C6F982B8ED2C958398EEEC01F187918B974A40857D368B412",
                    "77603BDF64C34F0932D7D81AB8DB91A0BADF708170BB7B5AA2EA776052A0B081",
                    "CCD762343E7C5EC814734535AB0010D3DB4000EFDCA8C73EB600EB36C97084AC",
                    "63BAC858D369C6E5C908ED9765413D9F054A59FA685C313A50EF80603C9A401E"
                };
                if (!MethodsMatch(methods, hashes))
                {
                    return 42;
                }
                if (!HasExpectedLuckCallSites(terraria))
                {
                    return 46;
                }

                recipeAlchemyField = recipeType.GetField("alchemy", BindingFlags.Instance | BindingFlags.Public);
                recipeCreateItemField = recipeType.GetField("createItem", BindingFlags.Instance | BindingFlags.Public);
                requiredItemIdField = requiredItemType.GetField("itemIdOrRecipeGroup", BindingFlags.Instance | BindingFlags.Public);
                requiredItemStackField = requiredItemType.GetField("stack", BindingFlags.Instance | BindingFlags.Public);
                playerAlchemyTableField = playerType.GetField("alchemyTable", BindingFlags.Instance | BindingFlags.Public);
                playerAlchemyTableProperty = playerType.GetProperty("alchemyTable", BindingFlags.Instance | BindingFlags.Public);
                worldItemTypeProperty = worldItemType.GetProperty("type", BindingFlags.Instance | BindingFlags.Public);
                if (recipeAlchemyField == null || recipeCreateItemField == null || requiredItemIdField == null || requiredItemStackField == null ||
                    (playerAlchemyTableField == null && playerAlchemyTableProperty == null) || worldItemTypeProperty == null)
                {
                    return 43;
                }

                MethodInfo craftPrefix = GetPrivateMethod("CraftDiscountPrefix");
                MethodInfo shimmerPrefix = GetPrivateMethod("ShimmerPrefix");
                MethodInfo shimmerFinalizer = GetPrivateMethod("ShimmerFinalizer");
                MethodInfo shimmerTranspiler = GetPrivateMethod("ShimmerTranspiler");
                MethodInfo luckPrefix = GetPrivateMethod("LuckPrefix");
                if (craftPrefix == null || shimmerPrefix == null || shimmerFinalizer == null || shimmerTranspiler == null || luckPrefix == null)
                {
                    return 44;
                }

                var harmony = new Harmony(HarmonyId);
                int installResult = InstallPatchSet(
                    harmony,
                    methods,
                    () =>
                    {
                        harmony.Patch(craftDiscount, prefix: new HarmonyMethod(craftPrefix));
                        harmony.Patch(shimmer, prefix: new HarmonyMethod(shimmerPrefix), transpiler: new HarmonyMethod(shimmerTranspiler), finalizer: new HarmonyMethod(shimmerFinalizer));
                        foreach (MethodInfo method in luckMethods)
                        {
                            harmony.Patch(method, prefix: new HarmonyMethod(luckPrefix));
                        }
                    },
                    () => HasOwnedPrefix(craftDiscount) && HasOwnedPrefix(shimmer) &&
                        HasOwnedTranspiler(shimmer) && HasOwnedFinalizer(shimmer) &&
                        luckMethods.All(HasOwnedPrefix),
                    45);
                if (installResult != 0)
                {
                    return installResult;
                }

                alchemyAndLuckPatchInstalled = true;
                return 0;
            }
        }

        private static int EnsureWorldTransitionPatchesInstalled()
        {
            lock (PatchSync)
            {
                if (worldTransitionPatchInstalled)
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
                    return 50;
                }

                MethodInfo initialize = worldGenType.GetMethod("initializeHardMode", BindingFlags.Static | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                MethodInfo smashAltar = worldGenType.GetMethod("SmashAltar", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(int), typeof(int) }, null);
                MethodInfo lunar = worldGenType.GetMethod("TriggerLunarApocalypse", BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
                MethodInfo bulb = worldGenType.GetMethod("GeneratePlanteraBulbOnAllMechsDefeated", BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
                MethodInfo attemptBulb = worldGenType.GetMethod("AttemptToGeneratePlanteraBulbAt", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(int), typeof(int), typeof(bool) }, null);
                MethodInfo geRunner = worldGenType.GetMethods(BindingFlags.Static | BindingFlags.Public).SingleOrDefault(method => method.Name == "GERunner");
                MethodInfo oreRunner = worldGenType.GetMethods(BindingFlags.Static | BindingFlags.Public).SingleOrDefault(method => method.Name == "OreRunner");
                MethodInfo[] randomWorldPoints = worldGenType.GetMethods(BindingFlags.Static | BindingFlags.Public).Where(method => method.Name == "RandomWorldPoint").OrderBy(method => method.MetadataToken).ToArray();
                if (randomWorldPoints.Length != 2)
                {
                    return 51;
                }

                MethodInfo[] providerMethods = new[] { initialize, smashAltar, bulb, geRunner, oreRunner }.Concat(randomWorldPoints).ToArray();
                string[] providerHashes =
                {
                    "EE6E02E568AB5E372BD2B0DA0A8C547BB8137AD90811C8778974B4216A6647CC",
                    "FBDA48BF920344C56A3E7E19637ABD52B45ABC63049D609AD8C68FF1EA5623EF",
                    "D652B8CC3385AAB8DA699F0E92D0F0475EA79F512B9D24E2E607F5AE0E428C18",
                    "2F3CB22E28DC9BABAFFCFB77413E1D78598AAE778E44C836BBD2C57E793FC0E9",
                    "C8328B8206502072E2A8F71565AA5087A37E1DD5839C8F86E50A1EEF7578DF57",
                    "5AAA2B97C267FC50021BAB00F627F5EEC60F0680B79A39DB24D0F3D9918D5CB1",
                    "32E7E00B0A807C314ECDA48E15F5540F94B2766A00989BEBF14E04F546680AB5"
                };
                if (!MethodsMatch(providerMethods, providerHashes) || lunar == null || attemptBulb == null || !HasExpectedBody(lunar, "C4D29B8361480CC4E793DC71C7A7847EF93E702CECAE140AA053B8517BF157CB"))
                {
                    return 52;
                }

                try
                {
                    attemptPlanteraBulb = (AttemptPlanteraBulbDelegate)Delegate.CreateDelegate(typeof(AttemptPlanteraBulbDelegate), attemptBulb);
                }
                catch (ArgumentException)
                {
                    return 55;
                }

                MethodInfo providerTranspiler = GetPrivateMethod("WorldGenProviderTranspiler");
                MethodInfo transitionPrefix = GetPrivateMethod("WorldTransitionPrefix");
                MethodInfo planteraBulbPrefix = GetPrivateMethod("PlanteraBulbPrefix");
                MethodInfo transitionFinalizer = GetPrivateMethod("ThreadRandomFinalizer");
                MethodInfo lunarPrefix = GetPrivateMethod("LunarScopePrefix");
                MethodInfo globalFinalizer = GetPrivateMethod("AdvancedScopeFinalizer");
                if (providerTranspiler == null || transitionPrefix == null || planteraBulbPrefix == null || transitionFinalizer == null || lunarPrefix == null || globalFinalizer == null)
                {
                    return 53;
                }

                var harmony = new Harmony(HarmonyId);
                MethodInfo[] patchedMethods = providerMethods.Concat(new[] { lunar }).ToArray();
                int installResult = InstallPatchSet(
                    harmony,
                    patchedMethods,
                    () =>
                    {
                        foreach (MethodInfo method in providerMethods)
                        {
                            harmony.Patch(method, transpiler: new HarmonyMethod(providerTranspiler));
                        }
                        foreach (MethodInfo method in new[] { initialize, smashAltar })
                        {
                            harmony.Patch(method, prefix: new HarmonyMethod(transitionPrefix), finalizer: new HarmonyMethod(transitionFinalizer));
                        }
                        harmony.Patch(bulb, prefix: new HarmonyMethod(planteraBulbPrefix), finalizer: new HarmonyMethod(transitionFinalizer));
                        harmony.Patch(lunar, prefix: new HarmonyMethod(lunarPrefix), finalizer: new HarmonyMethod(globalFinalizer));
                    },
                    () => providerMethods.All(HasOwnedTranspiler) &&
                        new[] { initialize, smashAltar }.All(method => HasOwnedPrefix(method) && HasOwnedFinalizer(method)) &&
                        HasOwnedPrefix(bulb) && HasOwnedFinalizer(bulb) &&
                        HasOwnedPrefix(lunar) && HasOwnedFinalizer(lunar),
                    54);
                if (installResult != 0)
                {
                    return installResult;
                }

                worldTransitionPatchInstalled = true;
                return 0;
            }
        }

        private static bool TryGetAdvancedTerrariaTypes(out Assembly terraria, out Type worldGenType, out Type playerType, out Type itemType, out Type tileType)
        {
            terraria = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(candidate => string.Equals(candidate.GetName().Name, "Terraria", StringComparison.Ordinal));
            worldGenType = null;
            playerType = null;
            itemType = null;
            tileType = null;
            if (terraria == null || terraria.GetName().Version != new Version(1, 4, 5, 8) || terraria.ManifestModule.ModuleVersionId != SupportedMvid)
            {
                return false;
            }

            Type mainType = terraria.GetType("Terraria.Main", false);
            Type unifiedRandomType = terraria.GetType("Terraria.Utilities.UnifiedRandom", false);
            worldGenType = terraria.GetType("Terraria.WorldGen", false);
            playerType = terraria.GetType("Terraria.Player", false);
            itemType = terraria.GetType("Terraria.Item", false);
            tileType = terraria.GetType("Terraria.Tile", false);
            if (mainType == null || unifiedRandomType == null || worldGenType == null || playerType == null || itemType == null || tileType == null)
            {
                return false;
            }

            mainRandomField = mainType.GetField("rand", BindingFlags.Static | BindingFlags.Public);
            PropertyInfo worldGenRandomProperty = worldGenType.GetProperty("genRand", BindingFlags.Static | BindingFlags.Public);
            worldGenRandomGetter = worldGenRandomProperty == null ? null : worldGenRandomProperty.GetGetMethod();
            unifiedRandomNextMethod = unifiedRandomType.GetMethod("Next", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(int) }, null);
            unifiedRandomNextRangeMethod = unifiedRandomType.GetMethod("Next", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(int), typeof(int) }, null);
            return mainRandomField != null && worldGenRandomGetter != null && unifiedRandomNextMethod != null && unifiedRandomNextRangeMethod != null;
        }

        private static bool MethodsMatch(MethodInfo[] methods, string[] hashes)
        {
            if (methods.Length != hashes.Length)
            {
                return false;
            }

            for (int index = 0; index < methods.Length; index++)
            {
                if (methods[index] == null || !HasExpectedBody(methods[index], hashes[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasExpectedLuckCallSites(Assembly terraria)
        {
            string[] expected =
            {
                "06000936@0007->0600245A",
                "06000937@0007->0600245B",
                "06000938@0007->0600245C",
                "06000939@0007->0600245D",
                "0600093A@0007->0600245E",
                "06003EB6@0007->0600245A",
                "06003EB7@0007->0600245B",
                "06003EB8@0007->0600245C",
                "06003EB9@0007->0600245D",
                "06003EBA@0007->0600245E"
            };
            try
            {
                var oneByte = new Dictionary<byte, OpCode>();
                var twoByte = new Dictionary<byte, OpCode>();
                foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public))
                {
                    OpCode opcode = (OpCode)field.GetValue(null);
                    ushort value = unchecked((ushort)opcode.Value);
                    if (value < 0x100) oneByte[(byte)value] = opcode;
                    else if ((value & 0xFF00) == 0xFE00) twoByte[(byte)value] = opcode;
                }

                var actual = new List<string>();
                foreach (Type type in terraria.GetTypes())
                {
                    IEnumerable<MethodBase> members = type
                        .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                        .Cast<MethodBase>()
                        .Concat(type.GetConstructors(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
                    foreach (MethodBase method in members)
                    {
                        MethodBody body;
                        try { body = method.GetMethodBody(); }
                        catch { continue; }
                        byte[] il = body == null ? null : body.GetILAsByteArray();
                        if (il == null) continue;
                        int offset = 0;
                        while (offset < il.Length)
                        {
                            int instructionOffset = offset;
                            OpCode opcode;
                            byte first = il[offset++];
                            if (first == 0xFE)
                            {
                                if (offset >= il.Length || !twoByte.TryGetValue(il[offset++], out opcode)) return false;
                            }
                            else if (!oneByte.TryGetValue(first, out opcode))
                            {
                                return false;
                            }

                            int operandSize = GetIlOperandSize(opcode.OperandType, il, offset);
                            if ((opcode == OpCodes.Call || opcode == OpCodes.Callvirt) && operandSize == 4)
                            {
                                int token = BitConverter.ToInt32(il, offset);
                                MethodBase target;
                                try { target = method.Module.ResolveMethod(token); }
                                catch { target = null; }
                                if (target != null && target.DeclaringType != null && target.DeclaringType.FullName == "Terraria.GameContent.Luck" && target.Name.StartsWith("Roll", StringComparison.Ordinal))
                                {
                                    actual.Add(method.MetadataToken.ToString("X8", CultureInfo.InvariantCulture) + "@" + instructionOffset.ToString("X4", CultureInfo.InvariantCulture) + "->" + target.MetadataToken.ToString("X8", CultureInfo.InvariantCulture));
                                }
                            }
                            offset += operandSize;
                        }
                    }
                }
                actual.Sort(StringComparer.Ordinal);
                return expected.SequenceEqual(actual, StringComparer.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static int GetIlOperandSize(OperandType type, byte[] il, int offset)
        {
            switch (type)
            {
                case OperandType.InlineNone: return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar: return 1;
                case OperandType.InlineVar: return 2;
                case OperandType.InlineI:
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR: return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR: return 8;
                case OperandType.InlineSwitch: return 4 + BitConverter.ToInt32(il, offset) * 4;
                default: throw new InvalidOperationException(type.ToString());
            }
        }

        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(EntryPoint).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        }

        private static void UnpatchMethods(Harmony harmony, IEnumerable<MethodInfo> methods)
        {
            foreach (MethodInfo method in methods.Where(method => method != null))
            {
                try
                {
                    harmony.Unpatch(method, HarmonyPatchType.All, HarmonyId);
                }
                catch
                {
                }
            }
        }

        private static int InstallPatchSet(
            Harmony harmony,
            IEnumerable<MethodInfo> methods,
            Action install,
            Func<bool> validate,
            int failureCode)
        {
            MethodInfo[] ownedMethods = methods.Where(method => method != null).Distinct().ToArray();
            try
            {
                install();
                if (validate())
                {
                    return 0;
                }
            }
            catch
            {
            }

            UnpatchMethods(harmony, ownedMethods);
            return failureCode;
        }

        private static bool TreeScopePrefix(int i, int j, ref AdvancedScopeState __state)
        {
            WorldLockConfiguration current = configuration;
            if (current == null || !current.HasCapability(PlayerTriggeredCapability))
            {
                return true;
            }

            try
            {
                object[] args = { i, j, 0, 0 };
                object treeType = getTreeTypeAndTreeBottomMethod.Invoke(null, args);
                int bottomX = (int)args[2];
                int bottomY = (int)args[3];
                int kind = Convert.ToInt32(treeType, CultureInfo.InvariantCulture);
                string source = WorldKey(current) + "|" + bottomX + "|" + bottomY + "|" + kind;
                long occurrence = current.State.EventCounters.Next("tree-shake", source);
                return TryBeginAdvancedScope(current, "tree-shake", source + "|" + occurrence, ref __state);
            }
            catch (Exception ex)
            {
                return FailScope("tree-shake", ex);
            }
        }

        private static bool TileDropScopePrefix(int x, int y, object tileCache, ref AdvancedScopeState __state)
        {
            WorldLockConfiguration current = configuration;
            if (current == null || !current.HasCapability(PlayerTriggeredCapability))
            {
                return true;
            }

            try
            {
                string source = string.Join("|", WorldKey(current), x, y, tileTypeField.GetValue(tileCache), tileFrameXField.GetValue(tileCache), tileFrameYField.GetValue(tileCache));
                long occurrence = current.State.EventCounters.Next("tile-drop", source);
                return TryBeginAdvancedScope(current, "tile-drop", source + "|" + occurrence, ref __state);
            }
            catch (Exception ex)
            {
                return FailScope("tile-drop", ex);
            }
        }

        private static bool PotScopePrefix(int x2, int y2, int style, ref AdvancedScopeState __state)
        {
            WorldLockConfiguration current = configuration;
            if (current == null || !current.HasCapability(PlayerTriggeredCapability))
            {
                return true;
            }

            try
            {
                string source = WorldKey(current) + "|" + x2 + "|" + y2 + "|28|" + style;
                long occurrence = current.State.EventCounters.Next("pot-drop", source);
                return TryBeginAdvancedScope(current, "pot-drop", source + "|" + occurrence, ref __state);
            }
            catch (Exception ex)
            {
                return FailScope("pot-drop", ex);
            }
        }

        private static bool SmallPileScopePrefix(int i, int y, ushort type, ref AdvancedScopeState __state)
        {
            WorldLockConfiguration current = configuration;
            if (current == null || !current.HasCapability(PlayerTriggeredCapability) || type != 185)
            {
                return true;
            }

            try
            {
                object selectedTile = GetTile(i, y);
                int selectedFrameX = Convert.ToInt32(tileFrameXField.GetValue(selectedTile), CultureInfo.InvariantCulture);
                int horizontalSegment = selectedFrameX / 18 & 1;
                int left = i - horizontalSegment;
                int objectFrameX = selectedFrameX - horizontalSegment * 18;
                string source = string.Join(
                    "|",
                    WorldKey(current),
                    left,
                    y,
                    type,
                    objectFrameX,
                    tileFrameYField.GetValue(selectedTile));
                return TryBeginAdvancedScope(current, "small-pile-drop", source, ref __state);
            }
            catch (Exception ex)
            {
                return FailScope("small-pile-drop", ex);
            }
        }

        private static bool TileBaitDropScopePrefix(int i, int j, object tileCache, ref AdvancedScopeState __state)
        {
            WorldLockConfiguration current = configuration;
            if (current == null || !current.HasCapability(PlayerTriggeredCapability))
            {
                return true;
            }

            try
            {
                string source = string.Join("|", WorldKey(current), i, j, tileTypeField.GetValue(tileCache), tileFrameXField.GetValue(tileCache), tileFrameYField.GetValue(tileCache));
                long occurrence = current.State.EventCounters.Next("tile-bait-drop", source);
                return TryBeginAdvancedScope(current, "tile-bait-drop", source + "|" + occurrence, ref __state);
            }
            catch (Exception ex)
            {
                return FailScope("tile-bait-drop", ex);
            }
        }

        private static bool OrbScopePrefix(int i, int j, int type, ref AdvancedScopeState __state)
        {
            WorldLockConfiguration current = configuration;
            if (current == null || !current.HasCapability(PlayerTriggeredCapability) || type != 31)
            {
                return true;
            }

            try
            {
                object selectedTile = GetTile(i, j);
                int selectedFrameX = Convert.ToInt32(tileFrameXField.GetValue(selectedTile), CultureInfo.InvariantCulture);
                int selectedFrameY = Convert.ToInt32(tileFrameYField.GetValue(selectedTile), CultureInfo.InvariantCulture);
                int horizontalSegment = selectedFrameX / 18 & 1;
                int verticalSegment = selectedFrameY / 18 & 1;
                int left = i - horizontalSegment;
                int top = j - verticalSegment;
                int objectFrameX = selectedFrameX - horizontalSegment * 18;
                string source = string.Join("|", WorldKey(current), left, top, type, objectFrameX);
                return TryBeginAdvancedScope(current, "evil-orb", source, ref __state);
            }
            catch (Exception ex)
            {
                return FailScope("evil-orb", ex);
            }
        }

        private static object GetTile(int x, int y)
        {
            Assembly terraria = mainRandomField.DeclaringType.Assembly;
            Type mainType = terraria.GetType("Terraria.Main", true);
            Array tiles = (Array)mainType.GetField("tile", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            return tiles.GetValue(x, y);
        }

        private static bool PlayerActionScopePrefix(MethodBase __originalMethod, object __instance, object[] __args, ref AdvancedScopeState __state)
        {
            WorldLockConfiguration current = configuration;
            if (current == null || !current.HasCapability(PlayerTriggeredCapability))
            {
                return true;
            }

            try
            {
                int player = (int)playerWhoAmIField.GetValue(__instance);
                string sourceType = (__args != null && __args.Length > 0 ? Convert.ToString(__args[0], CultureInfo.InvariantCulture) : player.ToString(CultureInfo.InvariantCulture));
                string domain = "player-" + __originalMethod.Name.ToLowerInvariant();
                string source = WorldKey(current) + "|" + player + "|" + sourceType;
                long occurrence = current.State.EventCounters.Next(domain, source);
                return TryBeginAdvancedScope(current, domain, source + "|" + occurrence, ref __state);
            }
            catch (Exception ex)
            {
                return FailScope(__originalMethod.Name, ex);
            }
        }

        private static bool ItemPrefixScopePrefix(object __instance, int prefixWeWant, ref AdvancedScopeState __state)
        {
            WorldLockConfiguration current = configuration;
            if (current == null || !current.HasCapability(PlayerTriggeredCapability) || (prefixWeWant != -1 && prefixWeWant != -2))
            {
                return true;
            }

            try
            {
                int type = (int)itemTypeField.GetValue(__instance);
                string source = WorldKey(current) + "|" + prefixWeWant + "|" + type;
                long occurrence = current.State.EventCounters.Next("item-prefix", source);
                return TryBeginAdvancedScope(current, "item-prefix", source + "|" + occurrence, ref __state);
            }
            catch (Exception ex)
            {
                return FailScope("item-prefix", ex);
            }
        }

        private static bool TryBeginAdvancedScope(WorldLockConfiguration current, string domain, string eventKey, ref AdvancedScopeState state)
        {
            object previousMain = mainRandomField.GetValue(null);
            string previousContext = activeChanceContext;
            int previousLuckIndex = activeLuckCallIndex;
            WorldLockConfiguration previousConfiguration = activeScopeConfiguration;
            try
            {
                mainRandomField.SetValue(null, CreateRandom(current, domain + "/main", eventKey));
                activeChanceContext = domain + "|" + eventKey;
                activeLuckCallIndex = 0;
                activeScopeConfiguration = current;
                state = new AdvancedScopeState(previousMain, previousContext, previousLuckIndex, previousConfiguration);
                return true;
            }
            catch
            {
                if (previousMain != null) mainRandomField.SetValue(null, previousMain);
                activeChanceContext = previousContext;
                activeLuckCallIndex = previousLuckIndex;
                activeScopeConfiguration = previousConfiguration;
                throw;
            }
        }

        private static Exception AdvancedScopeFinalizer(Exception __exception, AdvancedScopeState __state)
        {
            if (__state != null)
            {
                try
                {
                    mainRandomField.SetValue(null, __state.PreviousMain);
                    activeChanceContext = __state.PreviousChanceContext;
                    activeLuckCallIndex = __state.PreviousLuckCallIndex;
                    activeScopeConfiguration = __state.PreviousConfiguration;
                }
                catch (Exception ex)
                {
                    runtimeFailure = "The deterministic RNG scope restore failed: " + ex.GetType().Name;
                }
            }
            return __exception;
        }

        private static bool CraftDiscountPrefix(object __instance, object player, object req, ref int __result)
        {
            WorldLockConfiguration current = configuration;
            if (current == null || !current.HasCapability(AlchemyAndLuckCapability))
            {
                return true;
            }

            try
            {
                bool alchemy = (bool)recipeAlchemyField.GetValue(__instance);
                bool hasTable = playerAlchemyTableField != null
                    ? (bool)playerAlchemyTableField.GetValue(player)
                    : (bool)playerAlchemyTableProperty.GetValue(player, null);
                if (!alchemy || !hasTable)
                {
                    __result = 0;
                    return false;
                }

                int stack = (int)requiredItemStackField.GetValue(req);
                int ingredient = (int)requiredItemIdField.GetValue(req);
                object createItem = recipeCreateItemField.GetValue(__instance);
                int output = (int)itemTypeField.GetValue(createItem);
                string chanceId = "alchemy-craft|" + WorldKey(current) + "|" + output + "|" + ingredient;
                int saved = 0;
                for (int index = 0; index < stack; index++)
                {
                    if (RollChance(current, chanceId, 1, 3)) saved++;
                }
                __result = saved;
                return false;
            }
            catch (Exception ex)
            {
                runtimeFailure = "The alchemy-table accumulator failed: " + ex.GetType().Name;
                return false;
            }
        }

        private static void ShimmerPrefix(object __instance, ref ShimmerScopeState __state)
        {
            WorldLockConfiguration current = configuration;
            if (current == null || !current.HasCapability(AlchemyAndLuckCapability))
            {
                return;
            }
            __state = new ShimmerScopeState(shimmerAlchemyContext, activeScopeConfiguration);
            shimmerAlchemyContext = "alchemy-shimmer|" + WorldKey(current) + "|" + worldItemTypeProperty.GetValue(__instance, null);
            activeScopeConfiguration = current;
        }

        private static Exception ShimmerFinalizer(Exception __exception, ShimmerScopeState __state)
        {
            if (__state != null)
            {
                shimmerAlchemyContext = __state.PreviousContext;
                activeScopeConfiguration = __state.PreviousConfiguration;
            }
            return __exception;
        }

        private static IEnumerable<CodeInstruction> ShimmerTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();
            CodeInstruction ingredientStore = null;
            for (int index = 0; index < codes.Count - 1; index++)
            {
                MethodInfo called = codes[index].operand as MethodInfo;
                if (called != null && called.Name == "get_Current" && called.ReturnType.FullName == "Terraria.Recipe+RequiredItemEntry")
                {
                    ingredientStore = codes[index + 1];
                    break;
                }
            }
            if (ingredientStore == null)
            {
                throw new InvalidOperationException("The shimmer ingredient local was not found.");
            }

            MethodInfo wrapper = GetPrivateMethod("ShimmerAlchemyNext");
            int replacements = 0;
            for (int index = 0; index < codes.Count - 2; index++)
            {
                MethodInfo called = codes[index + 2].operand as MethodInfo;
                if (codes[index].LoadsField(mainRandomField) && codes[index + 1].opcode == OpCodes.Ldc_I4_3 &&
                    called != null && called.Name == "Next" && called.GetParameters().Length == 1)
                {
                    CodeInstruction loadIngredient = LoadStoredLocal(ingredientStore);
                    loadIngredient.labels.AddRange(codes[index].labels);
                    loadIngredient.blocks.AddRange(codes[index].blocks);
                    codes[index] = loadIngredient;
                    codes.Insert(index + 1, new CodeInstruction(OpCodes.Box, requiredItemIdField.DeclaringType));
                    codes[index + 3] = new CodeInstruction(OpCodes.Call, wrapper);
                    replacements++;
                    break;
                }
            }
            if (replacements != 1)
            {
                throw new InvalidOperationException("The shimmer alchemy roll was not uniquely identified.");
            }
            return codes;
        }

        private static CodeInstruction LoadStoredLocal(CodeInstruction store)
        {
            if (store.opcode == OpCodes.Stloc_0) return new CodeInstruction(OpCodes.Ldloc_0);
            if (store.opcode == OpCodes.Stloc_1) return new CodeInstruction(OpCodes.Ldloc_1);
            if (store.opcode == OpCodes.Stloc_2) return new CodeInstruction(OpCodes.Ldloc_2);
            if (store.opcode == OpCodes.Stloc_3) return new CodeInstruction(OpCodes.Ldloc_3);
            if (store.opcode == OpCodes.Stloc_S) return new CodeInstruction(OpCodes.Ldloc_S, store.operand);
            if (store.opcode == OpCodes.Stloc) return new CodeInstruction(OpCodes.Ldloc, store.operand);
            throw new InvalidOperationException("The shimmer ingredient local store is unsupported.");
        }

        private static int ShimmerAlchemyNext(object ingredient, int maximum)
        {
            WorldLockConfiguration current = activeScopeConfiguration ?? configuration;
            if (current == null || shimmerAlchemyContext == null || maximum != 3)
            {
                return RandomNext(mainRandomField.GetValue(null), maximum);
            }
            int ingredientId = (int)requiredItemIdField.GetValue(ingredient);
            return RollChance(current, shimmerAlchemyContext + "|" + ingredientId, 1, 3) ? 0 : 1;
        }

        private static bool LuckPrefix(MethodBase __originalMethod, float luck, int range, ref int __result)
        {
            WorldLockConfiguration current = activeScopeConfiguration ?? configuration;
            if (current == null || !current.HasCapability(AlchemyAndLuckCapability) || activeChanceContext == null)
            {
                return true;
            }

            try
            {
                int callIndex = activeLuckCallIndex++;
                string chanceId = "luck|" + activeChanceContext + "|" + __originalMethod.MetadataToken.ToString("X8", CultureInfo.InvariantCulture) + "|" + callIndex;
                object random = CreateRandom(current, "luck-roll/main", chanceId);
                bool positive = luck > 0f && StepFloatChance(current, chanceId + "|positive", luck);
                bool negative = luck < 0f && StepFloatChance(current, chanceId + "|negative", -luck);
                switch (__originalMethod.Name)
                {
                    case "RollLuck":
                        __result = positive ? RandomNext(random, RandomNext(random, range / 2, range)) : negative ? RandomNext(random, RandomNext(random, range, range * 2)) : RandomNext(random, range);
                        break;
                    case "RollBadLuck":
                        __result = positive ? RandomNext(random, RandomNext(random, range, range * 2)) : negative ? RandomNext(random, RandomNext(random, range / 2, range)) : RandomNext(random, range);
                        break;
                    case "RollOnlyBadLuck":
                        __result = negative ? RandomNext(random, RandomNext(random, range / 2, range)) : RandomNext(random, range);
                        break;
                    case "RollBadLuckExtreme":
                        __result = positive ? RandomNext(random, range * 10) : negative ? RandomNext(random, range / 10) : RandomNext(random, range);
                        break;
                    case "RollOnlyBadLuckExtreme":
                        __result = negative ? RandomNext(random, range / 10) : -1;
                        break;
                    default:
                        return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                runtimeFailure = "The luck accumulator failed: " + ex.GetType().Name;
                return false;
            }
        }

        private static bool WorldTransitionPrefix(MethodBase __originalMethod, object[] __args, ref ThreadRandomScopeState __state)
        {
            WorldLockConfiguration current = configuration;
            if (current == null || !current.HasCapability(WorldTransitionCapability))
            {
                return true;
            }

            try
            {
                string domain;
                string source;
                if (__originalMethod.Name == "initializeHardMode")
                {
                    domain = "hardmode-v";
                    source = WorldKey(current);
                }
                else if (__originalMethod.Name == "SmashAltar")
                {
                    domain = "hardmode-altar";
                    source = DeterministicEventIdentity.HardmodeAltarCounterSource(WorldKey(current));
                }
                else
                {
                    domain = "first-plantera-bulb";
                    source = WorldKey(current);
                }
                long occurrence = current.State.EventCounters.Next(domain, source);
                object previousRandom = threadWorldGenRandom;
                string previousContext = activeChanceContext;
                int previousLuckIndex = activeLuckCallIndex;
                WorldLockConfiguration previousConfiguration = activeScopeConfiguration;
                threadWorldGenRandom = CreateRandom(current, domain + "/worldgen", source + "|" + occurrence);
                activeChanceContext = domain + "|" + source + "|" + occurrence;
                activeLuckCallIndex = 0;
                activeScopeConfiguration = current;
                __state = new ThreadRandomScopeState(previousRandom, previousContext, previousLuckIndex, previousConfiguration);
                return true;
            }
            catch (Exception ex)
            {
                return FailScope(__originalMethod.Name, ex);
            }
        }

        private static bool PlanteraBulbPrefix(ref bool __result, ref ThreadRandomScopeState __state)
        {
            WorldLockConfiguration current = configuration;
            if (current == null || !current.HasCapability(WorldTransitionCapability))
            {
                return true;
            }

            try
            {
                string domain = "first-plantera-bulb";
                string source = WorldKey(current);
                long occurrence = current.State.EventCounters.Next(domain, source);
                object previousRandom = threadWorldGenRandom;
                string previousContext = activeChanceContext;
                int previousLuckIndex = activeLuckCallIndex;
                WorldLockConfiguration previousConfiguration = activeScopeConfiguration;
                threadWorldGenRandom = CreateRandom(current, domain + "/worldgen", source + "|" + occurrence);
                activeChanceContext = domain + "|" + source + "|" + occurrence;
                activeLuckCallIndex = 0;
                activeScopeConfiguration = current;
                __state = new ThreadRandomScopeState(previousRandom, previousContext, previousLuckIndex, previousConfiguration);

                if (current.PlanteraBulb != null && current.PlanteraBulb.Anchors.Count > 0)
                {
                    __result = TryPlacePlannedPlanteraBulb(current.PlanteraBulb);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                runtimeFailure = "The deterministic Plantera bulb placement failed: " + ex.GetType().Name;
                return true;
            }
        }

        private static bool TryPlacePlannedPlanteraBulb(PlanteraBulbConfiguration plan)
        {
            if (plan == null || plan.Anchors.Count == 0 || attemptPlanteraBulb == null)
            {
                return false;
            }

            PlanteraBulbAnchor anchor = plan.Anchors[0];
            if (attemptPlanteraBulb(anchor.X, anchor.Y, false))
            {
                return true;
            }

            const int localSearchRadiusSquared = 40 * 40;
            for (int index = 1; index < plan.Anchors.Count; index++)
            {
                PlanteraBulbAnchor candidate = plan.Anchors[index];
                long dx = (long)candidate.X - anchor.X;
                long dy = (long)candidate.Y - anchor.Y;
                if (dx * dx + dy * dy <= localSearchRadiusSquared &&
                    attemptPlanteraBulb(candidate.X, candidate.Y, false))
                {
                    return true;
                }
            }

            foreach (PlanteraBulbAnchor candidate in plan.Anchors)
            {
                if (attemptPlanteraBulb(candidate.X, candidate.Y, true))
                {
                    return true;
                }
            }

            return false;
        }

        private static Exception ThreadRandomFinalizer(Exception __exception, ThreadRandomScopeState __state)
        {
            if (__state != null)
            {
                threadWorldGenRandom = __state.PreviousRandom;
                activeChanceContext = __state.PreviousChanceContext;
                activeLuckCallIndex = __state.PreviousLuckCallIndex;
                activeScopeConfiguration = __state.PreviousConfiguration;
            }
            return __exception;
        }

        private static bool LunarScopePrefix(ref AdvancedScopeState __state)
        {
            WorldLockConfiguration current = configuration;
            if (current == null || !current.HasCapability(WorldTransitionCapability))
            {
                return true;
            }
            try
            {
                string source = WorldKey(current);
                long occurrence = current.State.EventCounters.Next("lunar-pillars", source);
                return TryBeginAdvancedScope(current, "lunar-pillars", source + "|" + occurrence, ref __state);
            }
            catch (Exception ex)
            {
                return FailScope("lunar-pillars", ex);
            }
        }

        private static IEnumerable<CodeInstruction> WorldGenProviderTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getter = GetPrivateMethod("GetWorldGenRandomForCurrentThread");
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(worldGenRandomGetter))
                {
                    CodeInstruction replacement = new CodeInstruction(OpCodes.Call, getter);
                    replacement.labels.AddRange(instruction.labels);
                    replacement.blocks.AddRange(instruction.blocks);
                    yield return replacement;
                    yield return new CodeInstruction(OpCodes.Castclass, worldGenRandomGetter.ReturnType);
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        private static object GetWorldGenRandomForCurrentThread()
        {
            if (threadWorldGenRandom != null)
            {
                return threadWorldGenRandom;
            }

            if (activeScopeConfiguration != null)
            {
                return mainRandomField.GetValue(null);
            }

            return worldGenRandomGetter.Invoke(null, null);
        }

        private static object CreateRandom(WorldLockConfiguration current, string domain, string eventKey)
        {
            byte[] seed = DeterministicDomainSeed.Derive(current.EntropySeed, current.ProtocolVersion, domain, eventKey);
            return Activator.CreateInstance(mainRandomField.FieldType, DeterministicDomainSeed.ToPositiveInt32(seed));
        }

        private static bool StepChance(WorldLockConfiguration current, string chanceId, long numerator, long denominator)
        {
            lock (current.State.AccumulatorSync)
            {
                IntegerChanceAccumulator accumulator;
                if (!current.State.ChanceAccumulators.TryGetValue(chanceId, out accumulator))
                {
                    byte[] seed = DeterministicDomainSeed.Derive(current.EntropySeed, current.ProtocolVersion, "chance-accumulator", chanceId);
                    ulong phase = BitConverter.ToUInt64(seed, 0);
                    accumulator = new IntegerChanceAccumulator(phase);
                    current.State.ChanceAccumulators.Add(chanceId, accumulator);
                }
                return accumulator.Step(numerator, denominator);
            }
        }

        private static bool RollChance(WorldLockConfiguration current, string chanceId, long numerator, long denominator)
        {
            long occurrence = current.State.EventCounters.Next("chance-roll", chanceId);
            return DeterministicChanceRoller.Roll(
                current.EntropySeed,
                current.ProtocolVersion,
                "chance-roll",
                chanceId + "|" + occurrence.ToString(CultureInfo.InvariantCulture),
                numerator,
                denominator);
        }

        private static bool StepFloatChance(WorldLockConfiguration current, string chanceId, float probability)
        {
            const long denominator = 1L << 24;
            long numerator = (long)Math.Round(Math.Max(0d, Math.Min(1d, probability)) * denominator, MidpointRounding.AwayFromZero);
            return StepChance(current, chanceId, numerator, denominator);
        }

        private static int RandomNext(object random, int maximum)
        {
            return (int)unifiedRandomNextMethod.Invoke(random, new object[] { maximum });
        }

        private static int RandomNext(object random, int minimum, int maximum)
        {
            return (int)unifiedRandomNextRangeMethod.Invoke(random, new object[] { minimum, maximum });
        }

        private static string WorldKey(WorldLockConfiguration current)
        {
            return current.WorldId.ToString(CultureInfo.InvariantCulture) + "|" + current.UniqueId.ToString("N");
        }

        private static bool FailScope(string domain, Exception exception)
        {
            runtimeFailure = "The " + domain + " deterministic scope failed: " + exception.GetType().Name;
            return false;
        }

        private sealed class AdvancedScopeState
        {
            public AdvancedScopeState(object previousMain, string previousChanceContext, int previousLuckCallIndex, WorldLockConfiguration previousConfiguration)
            {
                PreviousMain = previousMain;
                PreviousChanceContext = previousChanceContext;
                PreviousLuckCallIndex = previousLuckCallIndex;
                PreviousConfiguration = previousConfiguration;
            }

            public object PreviousMain { get; private set; }
            public string PreviousChanceContext { get; private set; }
            public int PreviousLuckCallIndex { get; private set; }
            public WorldLockConfiguration PreviousConfiguration { get; private set; }
        }

        private sealed class ThreadRandomScopeState
        {
            public ThreadRandomScopeState(object previousRandom, string previousChanceContext, int previousLuckCallIndex, WorldLockConfiguration previousConfiguration)
            {
                PreviousRandom = previousRandom;
                PreviousChanceContext = previousChanceContext;
                PreviousLuckCallIndex = previousLuckCallIndex;
                PreviousConfiguration = previousConfiguration;
            }

            public object PreviousRandom { get; private set; }
            public string PreviousChanceContext { get; private set; }
            public int PreviousLuckCallIndex { get; private set; }
            public WorldLockConfiguration PreviousConfiguration { get; private set; }
        }

        private sealed class ShimmerScopeState
        {
            public ShimmerScopeState(string previousContext, WorldLockConfiguration previousConfiguration)
            {
                PreviousContext = previousContext;
                PreviousConfiguration = previousConfiguration;
            }

            public string PreviousContext { get; private set; }
            public WorldLockConfiguration PreviousConfiguration { get; private set; }
        }
    }
}
