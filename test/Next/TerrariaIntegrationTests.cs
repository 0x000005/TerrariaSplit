using System.Text;
using TerrariaSplit.Race.Determinism;
using TerrariaSplit.Terraria;
using TerrariaSplit.Terraria.Automation;
using TerrariaSplit.Terraria.WorldGeneration;

namespace TerrariaSplit.Tests;

internal static class TerrariaIntegrationTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("pyramid pre-screen evaluates known positive, item mismatch and no-pyramid seeds", TestSuite.Flow, PyramidPredictionJourney, timeoutSeconds: 30);
        yield return TestCase.Async("race world upload validates, hashes, deduplicates, locates and deletes a Terraria world", TestSuite.Flow, WorldFileTransferJourney);
        yield return TestCase.Sync("post-generation filter handles small, medium and large Crimson worlds and rejects other placement", TestSuite.Flow, CrimsonCorridorPostFilter);
        yield return TestCase.Sync("resource post-filter combines required items, count thresholds and hook tiers", TestSuite.Flow, ResourcePostFilterRules);
        yield return TestCase.Sync("world automation settings normalize incompatible options and secret seed lists", TestSuite.Core, WorldSettingsNormalization);
    }

    private static void PyramidPredictionJourney()
    {
        var evaluator = new PyramidSeedPreScreenEvaluator();
        var settings = new AutoCreateWorldSettings
        {
            EnableCheats = true,
            EnablePyramidFilter = true,
            WorldSize = AutoCreateWorldSize.Small,
            WorldDifficulty = AutoCreateWorldDifficulty.Classic,
            WorldEvil = AutoCreateWorldEvil.Crimson,
            PyramidFilterItemMask = AutoCreatePyramidFilterItem.SandstormInABottleMask
        };
        PyramidSeedPreScreenPrediction accepted = evaluator.Evaluate(settings, "540278984", TerrariaWorldGenerationVersion.Modern1456);
        Check.True(accepted.CanUsePrediction);
        Check.True(accepted.AcceptSeed);
        Check.True(accepted.Result.LootSummary.Contains("Sandstorm in a Bottle", StringComparison.Ordinal));

        settings.EnableCheats = false;
        Check.False(PyramidSeedPreScreenEvaluator.IsEnabledFor(settings));
        settings.EnableCheats = true;

        settings.PyramidFilterItemMask = AutoCreatePyramidFilterItem.FlyingCarpetMask;
        PyramidSeedPreScreenPrediction mismatch = evaluator.Evaluate(settings, "540278984", TerrariaWorldGenerationVersion.Modern1456);
        Check.False(mismatch.AcceptSeed);
        Check.Equal("item mismatch", mismatch.RejectReason);
        PyramidSeedPreScreenPrediction absent = evaluator.Evaluate(settings, "702683177", TerrariaWorldGenerationVersion.Modern1456);
        Check.False(absent.AcceptSeed);
        Check.Equal("no pyramid", absent.RejectReason);
    }

    private static async Task WorldFileTransferJourney(CancellationToken cancellationToken)
    {
        using var directory = new TestDirectory();
        byte[] world = CreateMinimalWorld();
        string path = directory.Combine("source.wld");
        await File.WriteAllBytesAsync(path, world, cancellationToken);
        Check.True(RaceWorldFileValidator.IsValidWorldFilePath(path));
        Check.False(RaceWorldFileValidator.IsValidWorldFilePath(directory.Combine("missing.wld")));
        Check.True(RaceWorldFileValidator.TryReadWorldIdentity(path, out RaceWorldIdentity? identity, out string identityDetail));
        Check.Equal(string.Empty, identityDetail);
        Check.Equal("test-world", identity!.Name);
        Check.Equal(24680, identity.WorldId);
        Check.Equal(new Guid("5c52f5aa-80ee-40e7-a6de-afb84ff79025"), identity.UniqueId);

        const string rejectionMessage = "Only this Race world is allowed.";
        var determinism = new RaceDeterminismPackage(
            RaceDeterminismProtocol.CurrentVersion,
            "5c52f5aa80ee40e7a6deafb84ff79025",
            Convert.ToBase64String(Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray()),
            RaceDeterminismProtocol.TerrariaCompatibilityId,
            RaceDeterminismCapability.WorldLock | RaceDeterminismCapability.NpcDirectDrops,
            RaceDeterminismProtocol.CurrentChancePolicyVersion);
        string lockCommand = TerrariaRaceWorldLockService.BuildLockCommand(
            new TerrariaRaceWorldLockTarget(
                path,
                identity.WorldId,
                identity.UniqueId,
                new TerrariaRaceDeterminismConfiguration(
                    determinism.ProtocolVersion,
                    determinism.EpochId,
                    determinism.EntropySeedBase64,
                    determinism.TerrariaCompatibilityId,
                    (int)determinism.EnabledCapabilities,
                    determinism.ChancePolicyVersion,
                    determinism.CreateDigest()),
                TerrariaPlanteraBulbPlan.Empty,
                EntryAllowed: false),
            Path.Combine(directory.Path, "Race_Player.plr"),
            rejectionMessage);
        string[] lockParts = lockCommand.Split('\n');
        Check.Equal(15, lockParts.Length);
        Check.Equal("configure", lockParts[0]);
        Check.Equal(Path.GetFullPath(path), Encoding.UTF8.GetString(Convert.FromBase64String(lockParts[1])));
        Check.Equal(identity.WorldId.ToString(System.Globalization.CultureInfo.InvariantCulture), lockParts[2]);
        Check.Equal(identity.UniqueId.ToString("D"), lockParts[3]);
        Check.Equal(Path.Combine(directory.Path, "Race_Player.plr"), Encoding.UTF8.GetString(Convert.FromBase64String(lockParts[4])));
        Check.Equal(rejectionMessage, Encoding.UTF8.GetString(Convert.FromBase64String(lockParts[5])));
        Check.Equal(determinism.EpochId, lockParts[7]);
        Check.Equal(determinism.EntropySeedBase64, lockParts[8]);
        Check.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("0")), lockParts[12]);
        Check.Equal("0", lockParts[13]);
        Check.Equal(determinism.CreateDigest(), lockParts[14]);
        string[] startParts = TerrariaRaceWorldLockService.BuildStartRaceCommand(
            TimeSpan.FromSeconds(7),
            "将在 {0} 秒后开始").Split('\n');
        Check.Equal(3, startParts.Length);
        Check.Equal("start-race", startParts[0]);
        Check.Equal("7000", startParts[1]);
        Check.Equal("将在 {0} 秒后开始", Encoding.UTF8.GetString(Convert.FromBase64String(startParts[2])));
        string createPlayerCommand = TerrariaRaceWorldLockService.BuildCreatePlayerCommand(
            new TerrariaRaceInitialPlayerConfiguration("Runner", "{ template }", AutoCreatePlayerDifficulty.Hardcore));
        string[] playerParts = createPlayerCommand.Split('\n');
        Check.Equal(4, playerParts.Length);
        Check.Equal("create-player", playerParts[0]);
        Check.Equal("Runner", Encoding.UTF8.GetString(Convert.FromBase64String(playerParts[1])));
        Check.Equal("{ template }", Encoding.UTF8.GetString(Convert.FromBase64String(playerParts[2])));
        Check.Equal(AutoCreatePlayerDifficulty.Hardcore, playerParts[3]);
        Check.Equal("TerrariaSplit.RaceHook.1234", TerrariaRaceWorldLockService.CreatePipeName(1234));
        Check.Equal("TerrariaSplit.RaceHook.5678", TerrariaRaceWorldLockService.CreatePipeName(5678));

        var store = new RaceWorldFileStore(directory.Combine("server"));
        RaceStoredWorldFile first;
        await using (var stream = new MemoryStream(world))
        {
            first = await store.SaveAsync("a-b-12", " host ", "../race.wld", stream, cancellationToken);
        }
        await using (var stream = new MemoryStream(world))
        {
            RaceStoredWorldFile second = await store.SaveAsync("a-b-12", "host", "race.wld", stream, cancellationToken);
            Check.Equal(first.Path, second.Path);
            Check.Equal(first.Info.Sha256, second.Info.Sha256);
        }
        Check.True(store.TryGetPath("AB12", first.Info, out string storedPath));
        Check.Equal(first.Path, storedPath);
        byte[] replacementWorld = CreateMinimalWorld("replacement-world", 13579);
        RaceStoredWorldFile replacement;
        await using (var stream = new MemoryStream(replacementWorld))
        {
            replacement = await store.SaveAsync("AB12", "host", "replacement.wld", stream, cancellationToken);
        }
        store.DeleteStoredFile("AB12", first.Info);
        Check.False(File.Exists(first.Path));
        Check.True(File.Exists(replacement.Path));
        store.DeleteAllRooms();
        Check.False(File.Exists(replacement.Path));
    }

    private static void WorldSettingsNormalization()
    {
        var settings = new AutoCreateWorldSettings
        {
            WorldSize = "invalid",
            WorldDifficulty = "invalid",
            WorldEvil = "invalid",
            SpecialSeeds = "for the worthy, FOR THE WORTHY, not the bees",
            SecretSeeds = "  first ; second\nfirst  ",
            PyramidFilterItemMask = int.MaxValue,
            CrimsonDistance = "invalid",
            ResourceFilterItemMask = int.MaxValue,
            ResourceFilterLifeCrystalMinimum = 16,
            ResourceFilterHookMinimum = "invalid",
            ResourceFilterSpelunkerPotionMinimum = 7,
            ResourceFilterFeatherfallPotionMinimum = -1
        };
        SettingsNormalizer.Normalize(new AppSettings { Automation = { AutoCreate = settings } });
        Check.Equal(AutoCreateWorldSize.Small, settings.WorldSize);
        Check.Equal(AutoCreateWorldDifficulty.Classic, settings.WorldDifficulty);
        Check.Equal(AutoCreateWorldEvil.Crimson, settings.WorldEvil);
        Check.Equal(AutoCreateCrimsonDistance.Far, settings.CrimsonDistance);
        Check.True((settings.PyramidFilterItemMask & ~AutoCreatePyramidFilterItem.AllMask) == 0);
        Check.Equal(AutoCreateResourceFilterItem.AllMask, settings.ResourceFilterItemMask);
        Check.Equal(0, settings.ResourceFilterLifeCrystalMinimum);
        Check.Equal(AutoCreateResourceHook.None, settings.ResourceFilterHookMinimum);
        Check.Equal(0, settings.ResourceFilterSpelunkerPotionMinimum);
        Check.Equal(0, settings.ResourceFilterFeatherfallPotionMinimum);
        Check.Equal(2, AutoCreateSeedList.Parse(settings.SecretSeeds).Count);
    }

    private static void ResourcePostFilterRules()
    {
        var settings = new AutoCreateWorldSettings
        {
            EnableCheats = true,
            WorldSize = AutoCreateWorldSize.Small,
            WorldEvil = AutoCreateWorldEvil.Crimson,
            ResourceFilterItemMask = AutoCreateResourceFilterItem.AllMask,
            ResourceFilterLifeCrystalMinimum = 8,
            ResourceFilterHookMinimum = AutoCreateResourceHook.Sapphire,
            ResourceFilterSpelunkerPotionMinimum = 2,
            ResourceFilterFeatherfallPotionMinimum = 1,
            JungleRouteDepth = AutoCreateJungleRouteDepth.None
        };
        Dictionary<string, int> gems = AutoCreateResourceHook.All
            .Where(hook => hook != AutoCreateResourceHook.None)
            .ToDictionary(hook => hook, _ => 0, StringComparer.Ordinal);
        gems[AutoCreateResourceHook.Sapphire] = 14;
        gems[AutoCreateResourceHook.Emerald] = 15;
        var resources = new WorldResourceFilterResult(
            false,
            Boomsticks: 1,
            FeralClaws: 1,
            CloudBottles: 1,
            AnkletsOfTheWind: 1,
            HermesBoots: 1,
            LifeCrystals: 8,
            SpelunkerPotions: 2,
            FeatherfallPotions: 1,
            gems,
            TimeSpan.Zero);

        Check.True(PyramidFilterWorldFileEvaluator.IsResourceFilterEnabled(settings));
        Check.True(WorldResourceFilterMatcher.Matches(settings, resources));
        settings.EnableCheats = false;
        Check.False(PyramidFilterWorldFileEvaluator.IsResourceFilterEnabled(settings));
        settings.EnableCheats = true;
        gems[AutoCreateResourceHook.Emerald] = 14;
        resources = resources with { Gems = new Dictionary<string, int>(gems, StringComparer.Ordinal) };
        Check.False(WorldResourceFilterMatcher.Matches(settings, resources));
        gems[AutoCreateResourceHook.Ruby] = 15;
        resources = resources with { Gems = new Dictionary<string, int>(gems, StringComparer.Ordinal) };
        Check.True(WorldResourceFilterMatcher.Matches(settings, resources));
        settings.ResourceFilterHookMinimum = AutoCreateResourceHook.Diamond;
        Check.False(WorldResourceFilterMatcher.Matches(settings, resources));
        gems[AutoCreateResourceHook.Diamond] = 15;
        resources = resources with { Gems = new Dictionary<string, int>(gems, StringComparer.Ordinal) };
        Check.True(WorldResourceFilterMatcher.Matches(settings, resources));

        settings.JungleRouteDepth = AutoCreateJungleRouteDepth.Deep;
        resources = resources with { JungleRouteDeepestY = 649 };
        Check.False(WorldResourceFilterMatcher.Matches(settings, resources));
        resources = resources with { JungleRouteDeepestY = 650 };
        Check.True(WorldResourceFilterMatcher.Matches(settings, resources));
        settings.JungleRouteDepth = AutoCreateJungleRouteDepth.None;

        settings.WorldSize = AutoCreateWorldSize.Medium;
        Check.False(PyramidFilterWorldFileEvaluator.IsResourceFilterEnabled(settings));
        settings.WorldSize = AutoCreateWorldSize.Small;
        settings.WorldEvil = AutoCreateWorldEvil.Corruption;
        Check.False(PyramidFilterWorldFileEvaluator.IsResourceFilterEnabled(settings));

        var disabledRequirements = new AutoCreateWorldSettings
        {
            JungleRouteDepth = AutoCreateJungleRouteDepth.None
        };
        Check.True(WorldResourceFilterMatcher.Matches(disabledRequirements, WorldResourceFilterResult.Empty));
    }

    private static void CrimsonCorridorPostFilter()
    {
        using var directory = new TestDirectory();
        var scanner = new TerrariaWorldFilePyramidScanner();
        var evaluator = new PyramidFilterWorldFileEvaluator(scanner);
        (string Size, int Width, int Height)[] sizes =
        {
            (AutoCreateWorldSize.Small, 4200, 1200),
            (AutoCreateWorldSize.Medium, 6400, 1800),
            (AutoCreateWorldSize.Large, 8400, 2400)
        };

        foreach ((string size, int width, int height) in sizes)
        {
            int dungeonX = 200;
            int spawnX = width / 2;
            int nearDistance = AutoCreateCrimsonDistance.MaximumDistanceTiles(width, AutoCreateCrimsonDistance.Near);
            int mediumDistance = AutoCreateCrimsonDistance.MaximumDistanceTiles(width, AutoCreateCrimsonDistance.Medium);
            Check.Equal((width / 2) / 4, nearDistance);
            Check.Equal((width / 2) * 9 / 20, mediumDistance);
            string nearPath = directory.Combine($"{size}-near.wld");
            string mediumPath = directory.Combine($"{size}-medium.wld");
            string farPath = directory.Combine($"{size}-far.wld");
            string outsidePath = directory.Combine($"{size}-outside.wld");
            File.WriteAllBytes(
                nearPath,
                CreatePostFilterWorld(width, height, spawnX, dungeonX, crimsonTileX: spawnX - nearDistance));
            File.WriteAllBytes(
                mediumPath,
                CreatePostFilterWorld(width, height, spawnX, dungeonX, crimsonTileX: spawnX - mediumDistance));
            File.WriteAllBytes(
                farPath,
                CreatePostFilterWorld(width, height, spawnX, dungeonX, crimsonTileX: dungeonX + 1));
            File.WriteAllBytes(
                outsidePath,
                CreatePostFilterWorld(width, height, spawnX, dungeonX, crimsonTileX: width - 200));

            Check.True(scanner.TryScanCrimsonBetweenDungeonAndSpawn(
                nearPath,
                out CrimsonCorridorScanResult near,
                out string nearDetail,
                AutoCreateCrimsonDistance.Near));
            Check.Equal(string.Empty, nearDetail);
            Check.True(near.HasCrimson);
            Check.True(near.CrimsonTileCount >= 300);
            Check.Equal(spawnX - nearDistance, near.Bounds.Left);
            Check.Equal(spawnX, near.Bounds.Right);

            Check.True(scanner.TryScanCrimsonBetweenDungeonAndSpawn(
                mediumPath,
                out CrimsonCorridorScanResult mediumRejectedByNear,
                out _,
                AutoCreateCrimsonDistance.Near));
            Check.False(mediumRejectedByNear.HasCrimson);
            Check.True(scanner.TryScanCrimsonBetweenDungeonAndSpawn(
                mediumPath,
                out CrimsonCorridorScanResult medium,
                out _,
                AutoCreateCrimsonDistance.Medium));
            Check.True(medium.HasCrimson);
            Check.Equal(spawnX - mediumDistance, medium.Bounds.Left);

            Check.True(scanner.TryScanCrimsonBetweenDungeonAndSpawn(
                farPath,
                out CrimsonCorridorScanResult farRejectedByMedium,
                out _,
                AutoCreateCrimsonDistance.Medium));
            Check.False(farRejectedByMedium.HasCrimson);
            Check.True(scanner.TryScanCrimsonBetweenDungeonAndSpawn(
                farPath,
                out CrimsonCorridorScanResult far,
                out _,
                AutoCreateCrimsonDistance.Far));
            Check.True(far.HasCrimson);
            Check.Equal(dungeonX + 1, far.Bounds.Left);

            Check.True(scanner.TryScanCrimsonBetweenDungeonAndSpawn(outsidePath, out CrimsonCorridorScanResult outside, out string outsideDetail));
            Check.Equal(string.Empty, outsideDetail);
            Check.False(outside.HasCrimson);
            Check.Equal(0, outside.CrimsonTileCount);

            var settings = new AutoCreateWorldSettings
            {
                EnableCheats = true,
                WorldSize = size,
                WorldEvil = AutoCreateWorldEvil.Crimson,
                EnablePyramidFilter = false,
                RequireCrimsonBetweenDungeonAndSpawn = true,
                JungleRouteDepth = AutoCreateJungleRouteDepth.None,
                CrimsonDistance = AutoCreateCrimsonDistance.Near
            };
            PyramidFilterWorldFileResult kept = evaluator.Evaluate(nearPath, settings);
            PyramidFilterWorldFileResult rejected = evaluator.Evaluate(mediumPath, settings);
            Check.True(kept.Keep);
            Check.True(kept.CrimsonCorridorFilterEnabled);
            Check.False(rejected.Keep);

            settings.EnableCheats = false;
            PyramidFilterWorldFileResult disabled = evaluator.Evaluate(mediumPath, settings);
            Check.True(disabled.Keep);
            Check.False(disabled.CrimsonCorridorFilterEnabled);
            settings.EnableCheats = true;

            string enabledSignature = WorldPoolSignature.From(settings);
            settings.CrimsonDistance = AutoCreateCrimsonDistance.Medium;
            Check.False(string.Equals(enabledSignature, WorldPoolSignature.From(settings), StringComparison.Ordinal));
            settings.RequireCrimsonBetweenDungeonAndSpawn = false;
            Check.False(string.Equals(enabledSignature, WorldPoolSignature.From(settings), StringComparison.Ordinal));
        }
    }

    private static byte[] CreateMinimalWorld(string worldName = "test-world", int worldId = 24680)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(279);
        writer.Write(0x026369676F6C6572UL);
        writer.Write((uint)0);
        writer.Write((ulong)0);
        writer.Write((short)1);
        long pointerPosition = stream.Position;
        writer.Write(0);
        writer.Write((short)0);
        int headerPosition = checked((int)stream.Position);
        writer.Write(worldName);
        writer.Write("test-seed");
        writer.Write((ulong)279);
        writer.Write(new Guid("5c52f5aa-80ee-40e7-a6de-afb84ff79025").ToByteArray());
        writer.Write(worldId);
        long end = stream.Position;
        stream.Position = pointerPosition;
        writer.Write(headerPosition);
        stream.Position = end;
        return stream.ToArray();
    }

    private static byte[] CreatePostFilterWorld(
        int width,
        int height,
        int spawnTileX,
        int dungeonTileX,
        int crimsonTileX)
    {
        const int version = 279;
        const int importanceCount = 753;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(version);
        writer.Write(0x026369676F6C6572UL);
        writer.Write((uint)0);
        writer.Write((ulong)0);
        writer.Write((short)3);
        long pointersPosition = stream.Position;
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write((ushort)importanceCount);
        writer.Write(new byte[(importanceCount + 7) / 8]);

        int headerOffset = checked((int)stream.Position);
        writer.Write("post-filter-world");
        writer.Write("12345");
        writer.Write((ulong)version);
        writer.Write(Guid.Empty.ToByteArray());
        writer.Write(12345);
        writer.Write(0);
        writer.Write(width * 16);
        writer.Write(0);
        writer.Write(height * 16);
        writer.Write(height);
        writer.Write(width);
        writer.Write(0); // classic
        for (int index = 0; index < 8; index++) writer.Write(false);
        writer.Write(DateTime.UnixEpoch.ToBinary());
        writer.Write((byte)0);
        for (int index = 0; index < 17; index++) writer.Write(0);
        writer.Write(spawnTileX);
        writer.Write(250);  // spawn y
        writer.Write(300d);
        writer.Write(500d);
        writer.Write(0d);
        writer.Write(true);
        writer.Write(0);
        writer.Write(false);
        writer.Write(false);
        writer.Write(dungeonTileX);
        writer.Write(250); // dungeon y
        writer.Write(true);

        int tileOffset = checked((int)stream.Position);
        for (int x = 0; x < width; x++)
        {
            if (x == crimsonTileX)
            {
                writer.Write((byte)0x82); // active tile, Int16 RLE
                writer.Write((byte)203);  // Crimstone
                writer.Write((short)(height - 1));
            }
            else
            {
                writer.Write((byte)0x80); // empty tile, Int16 RLE
                writer.Write((short)(height - 1));
            }
        }

        int chestOffset = checked((int)stream.Position);
        writer.Write((short)0);
        writer.Write((short)40);
        long end = stream.Position;
        stream.Position = pointersPosition;
        writer.Write(headerOffset);
        writer.Write(tileOffset);
        writer.Write(chestOffset);
        stream.Position = end;
        return stream.ToArray();
    }
}
