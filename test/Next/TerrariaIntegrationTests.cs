using System.Text;
using TerrariaSplit.Race.Determinism;
using TerrariaSplit.Terraria;
using TerrariaSplit.Terraria.Automation;
using TerrariaSplit.Terraria.WorldGeneration;
using TerrariaSplit.WorldGuard.Payload;

namespace TerrariaSplit.Tests;

internal static class TerrariaIntegrationTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("pyramid pre-screen evaluates known positive, item mismatch and no-pyramid seeds", TestSuite.Flow, PyramidPredictionJourney, timeoutSeconds: 30);
        yield return TestCase.Async("native jungle seed judge preserves protocol and returns seed-only analysis", TestSuite.Flow, JungleSeedJudgeNativeJourney, timeoutSeconds: 30);
        yield return TestCase.Async("world seed filter skips a seed when the native call times out", TestSuite.Flow, WorldSeedFilterTimeoutJourney, timeoutSeconds: 10);
        yield return TestCase.Async("native jungle seed judge applies its timeout while waiting for a call slot", TestSuite.Core, JungleSeedJudgeGateTimeoutJourney, timeoutSeconds: 10);
        yield return TestCase.Sync("world seed filter skips candidate-local native failures", TestSuite.Core, WorldSeedFilterCandidateFailureClassification);
        yield return TestCase.Async("world seed filter stops when native world generation fails", TestSuite.Core, WorldSeedFilterGenerationFailureJourney);
        yield return TestCase.Async("world seed filter skips a seed when the jungle route is partial", TestSuite.Flow, WorldSeedFilterPartialRouteJourney, timeoutSeconds: 10);
        yield return TestCase.Sync("race seed filter concurrency uses eighty percent of processors", TestSuite.Core, RaceSeedFilterConcurrency);
        yield return TestCase.Async("race seed filter evaluates candidate seeds as one parallel batch", TestSuite.Flow, RaceSeedFilterBatchJourney, timeoutSeconds: 15);
        yield return TestCase.Async("UI seed pre-screen restarts after an empty batch or RNG drift without seed writeback", TestSuite.Flow, UiSeedBatchReplanJourney, timeoutSeconds: 30);
        yield return TestCase.Async("race world upload validates, hashes, deduplicates, locates and deletes a Terraria world", TestSuite.Flow, WorldFileTransferJourney);
        yield return TestCase.Sync("world automation settings normalize incompatible options and secret seed lists", TestSuite.Core, WorldSettingsNormalization);
        yield return TestCase.Sync("race UI reflection targets compatible runtime fields and preserves deferred failures", TestSuite.Core, RaceUiRuntimeSafety);
    }

    private static void RaceUiRuntimeSafety()
    {
        var element = new DerivedRaceUiElement();
        Check.True(RaceUiReflection.TrySetPublicInstanceField(element, "Left", 12.5f));
        Check.Equal(12.5f, element.Left);

        var point = new RaceUiLinkPoint();
        Check.True(RaceUiReflection.TrySetPublicInstanceField(point, "Left", -3));
        Check.True(RaceUiReflection.TrySetPublicInstanceField(point, "Right", -4));
        Check.True(RaceUiReflection.TrySetPublicInstanceField(point, "Enabled", true));
        Check.Equal(-3, point.Left);
        Check.Equal(-4, point.Right);
        Check.True(point.Enabled);

        Check.False(RaceUiReflection.TrySetPublicInstanceField(point, "Left", 1.5f));
        Check.False(RaceUiReflection.TrySetPublicInstanceField(point, "Missing", 1));
        Check.Equal(-3, point.Left);

        Check.False(RaceUiRuntimeFailure.TryResolve(string.Empty, string.Empty, out string empty));
        Check.Equal(string.Empty, empty);
        Check.True(RaceUiRuntimeFailure.TryResolve(
            "delayed UI failure",
            "world lock failure",
            out string uiFailure));
        Check.Equal("delayed UI failure", uiFailure);
        Check.True(RaceUiRuntimeFailure.TryResolve(
            string.Empty,
            "world lock failure",
            out string worldLockFailure));
        Check.Equal("world lock failure", worldLockFailure);
    }

    private static async Task JungleSeedJudgeNativeJourney(CancellationToken cancellationToken)
    {
        string? workerPath = Environment.GetEnvironmentVariable(
            "TERRARIA_WORLD_FILTER");
        if (string.IsNullOrWhiteSpace(workerPath))
        {
            return;
        }

        var client = new JungleSeedJudgeNativeClient(
            workerPath,
            TimeSpan.FromSeconds(5));
        JungleSeedJudgeResult result = await client.AnalyzeAsync(
            "1527488",
            JungleSeedJudgeGameMode.Classic,
            cancellationToken);
        Check.Equal(JungleSeedJudgeStatus.Complete, result.Status);
        Check.True(result.Complete);
        Check.Equal(62, result.CheckpointPassIndex);
        Check.Equal(JungleSeedAnalysisStatus.Complete, result.Jungle!.AnalysisStatus);
        Check.Equal(JungleRouteStatus.Complete, result.Jungle.Route.Status);
        Check.Equal(2754, result.Jungle.Route.DeepestX);
        Check.Equal(846, result.Jungle.Route.DeepestY);
        Check.True(result.Jungle.Resources.Count >= 10);
        Check.True(result.Jungle.Resources.Any(resource =>
            resource.Category == "FeralClaws" &&
            resource.X == 2806 &&
            resource.Y == 431 &&
            Math.Abs(resource.Cost - 1.2) < 0.001));
        Check.Equal(2, result.CrimsonVertices!.Count);
        Check.Equal(new CrimsonCorridorVertex(1, 1608, 279), result.CrimsonVertices[0]);
        Check.Equal(new CrimsonCorridorVertex(2, 3687, 223), result.CrimsonVertices[1]);
        var filterSettings = new AutoCreateWorldSettings
        {
            EnableCheats = true,
            EnablePyramidFilter = false,
            RequireCrimsonBetweenDungeonAndSpawn = true,
            CrimsonDistance = AutoCreateCrimsonDistance.Near,
            JungleRouteDepth = AutoCreateJungleRouteDepth.VeryDeep,
            ResourceFilterItemMask = AutoCreateResourceFilterItem.FeralClawsMask
        };
        Check.True(JungleSeedFilterMatcher.Match(filterSettings, result).Matches);
        JungleSeedJudgeResult shallow = result with
        {
            Jungle = result.Jungle with
            {
                Route = result.Jungle.Route with { DeepestY = 749 }
            }
        };
        Check.False(JungleSeedFilterMatcher.Match(filterSettings, shallow).Matches);

        string[] reportedStallSeeds =
        {
            "1083872473",
            "1160429121",
            "1261980980"
        };
        for (int cycle = 0; cycle < 2; cycle++)
        {
            foreach (string seedText in reportedStallSeeds)
            {
                JungleSeedJudgeResult repeated = await client.AnalyzeAsync(
                    seedText,
                    JungleSeedJudgeGameMode.Classic,
                    cancellationToken);
                Check.Equal(JungleSeedJudgeStatus.Complete, repeated.Status);
                Check.True(repeated.Complete);
                Check.Equal(seedText, repeated.SeedText);
            }
        }

        JungleSeedJudgeResult rejected = await client.AnalyzeAsync(
            "5162020",
            JungleSeedJudgeGameMode.Classic,
            cancellationToken);
        Check.Equal(JungleSeedJudgeStatus.SpecialSeedUnsupported, rejected.Status);
        Check.False(rejected.Complete);
        Check.True(rejected.Jungle is null);
    }

    private static async Task WorldSeedFilterTimeoutJourney(CancellationToken cancellationToken)
    {
        string? workerPath = Environment.GetEnvironmentVariable(
            "TERRARIA_WORLD_FILTER");
        if (string.IsNullOrWhiteSpace(workerPath))
        {
            return;
        }

        var nativeClient = new JungleSeedJudgeNativeClient(
            workerPath,
            TimeSpan.FromMilliseconds(1));
        using var evaluator = new WorldSeedFilterEvaluator(
            nativeClient: nativeClient);
        var settings = new AutoCreateWorldSettings
        {
            EnableCheats = true,
            EnablePyramidFilter = false,
            WorldSize = AutoCreateWorldSize.Small,
            WorldDifficulty = AutoCreateWorldDifficulty.Classic,
            WorldEvil = AutoCreateWorldEvil.Crimson,
            JungleRouteDepth = AutoCreateJungleRouteDepth.Medium
        };

        WorldSeedFilterPrediction prediction = await evaluator.EvaluateAsync(
            settings,
            "1083872473",
            TerrariaWorldGenerationVersion.Modern1456,
            cancellationToken);

        Check.True(prediction.CanUsePrediction);
        Check.False(prediction.AcceptSeed);
        Check.True(prediction.Detail.Contains(
            "transient failure; skip seed",
            StringComparison.Ordinal));
    }

    private static void WorldSeedFilterCandidateFailureClassification()
    {
        Check.True(WorldSeedFilterEvaluator.IsCandidateRejection(
            JungleSeedJudgeStatus.InvalidSeed));
        Check.True(WorldSeedFilterEvaluator.IsCandidateRejection(
            JungleSeedJudgeStatus.SpecialSeedUnsupported));
        Check.False(WorldSeedFilterEvaluator.IsCandidateRejection(
            JungleSeedJudgeStatus.GenerationFailed));
        Check.False(WorldSeedFilterEvaluator.IsCandidateRejection(
            JungleSeedJudgeStatus.InvalidRequest));
        Check.False(WorldSeedFilterEvaluator.IsCandidateRejection(
            JungleSeedJudgeStatus.Complete));
    }

    private static async Task JungleSeedJudgeGateTimeoutJourney(
        CancellationToken cancellationToken)
    {
        var nativeCallStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseNativeCall = new ManualResetEventSlim(false);
        var gate = new SemaphoreSlim(1, 1);
        var client = new JungleSeedJudgeNativeClient(
            (_, _, _) =>
            {
                nativeCallStarted.TrySetResult();
                releaseNativeCall.Wait();
                return null!;
            },
            TimeSpan.FromMilliseconds(50),
            gate);

        Task<JungleSeedJudgeResult> firstCall = client.AnalyzeAsync(
            "first",
            JungleSeedJudgeGameMode.Classic,
            cancellationToken);
        await nativeCallStarted.Task.WaitAsync(cancellationToken);
        await Check.ThrowsAsync<TimeoutException>(() => firstCall);

        try
        {
            await Check.ThrowsAsync<TimeoutException>(() => client.AnalyzeAsync(
                "second",
                JungleSeedJudgeGameMode.Classic,
                cancellationToken));
        }
        finally
        {
            releaseNativeCall.Set();
        }
    }

    private static async Task WorldSeedFilterGenerationFailureJourney(
        CancellationToken cancellationToken)
    {
        var gate = new SemaphoreSlim(1, 1);
        var nativeClient = new JungleSeedJudgeNativeClient(
            (seedText, _, requestId) => new JungleSeedJudgeResult(
                JungleSeedJudgeProtocol.Version,
                requestId,
                JungleSeedJudgeProtocol.CompatibilityId,
                JungleSeedJudgeStatus.GenerationFailed,
                seedText,
                0,
                0,
                0,
                Jungle: null,
                CrimsonVertices: null,
                Detail: "native generation failed"),
            TimeSpan.FromSeconds(1),
            gate);
        using var evaluator = new WorldSeedFilterEvaluator(
            nativeClient: nativeClient);
        var settings = new AutoCreateWorldSettings
        {
            EnableCheats = true,
            EnablePyramidFilter = false,
            WorldSize = AutoCreateWorldSize.Small,
            WorldDifficulty = AutoCreateWorldDifficulty.Classic,
            WorldEvil = AutoCreateWorldEvil.Crimson,
            JungleRouteDepth = AutoCreateJungleRouteDepth.Medium
        };

        WorldSeedFilterPrediction prediction = await evaluator.EvaluateAsync(
            settings,
            "12345",
            TerrariaWorldGenerationVersion.Modern1456,
            cancellationToken);

        if (prediction.CanUsePrediction)
        {
            throw new InvalidOperationException(
                "GenerationFailed must make seed prediction unavailable: " +
                prediction.Detail);
        }
        Check.False(prediction.CanContinueWithoutPrediction);
        Check.False(prediction.AcceptSeed);
        Check.True(prediction.Detail.Contains(
            nameof(JungleSeedJudgeStatus.GenerationFailed),
            StringComparison.Ordinal));
    }

    private static async Task WorldSeedFilterPartialRouteJourney(CancellationToken cancellationToken)
    {
        string? workerPath = Environment.GetEnvironmentVariable(
            "TERRARIA_WORLD_FILTER");
        if (string.IsNullOrWhiteSpace(workerPath))
        {
            return;
        }

        var nativeClient = new JungleSeedJudgeNativeClient(
            workerPath,
            TimeSpan.FromSeconds(5));
        using var evaluator = new WorldSeedFilterEvaluator(
            nativeClient: nativeClient);
        var settings = new AutoCreateWorldSettings
        {
            EnableCheats = true,
            EnablePyramidFilter = false,
            WorldSize = AutoCreateWorldSize.Small,
            WorldDifficulty = AutoCreateWorldDifficulty.Classic,
            WorldEvil = AutoCreateWorldEvil.Crimson,
            JungleRouteDepth = AutoCreateJungleRouteDepth.Medium
        };

        WorldSeedFilterPrediction prediction = await evaluator.EvaluateAsync(
            settings,
            "1160429121",
            TerrariaWorldGenerationVersion.Modern1456,
            cancellationToken);

        Check.True(prediction.CanUsePrediction);
        Check.False(prediction.AcceptSeed);
        Check.True(prediction.Detail.Contains(
            "jungle route depth 534 < 550; routeStatus=Partial",
            StringComparison.Ordinal));
    }

    private static void RaceSeedFilterConcurrency()
    {
        Check.Equal(1, TerrariaRaceWorldGenerationService.CalculateSeedFilterConcurrency(1));
        Check.Equal(1, TerrariaRaceWorldGenerationService.CalculateSeedFilterConcurrency(2));
        Check.Equal(3, TerrariaRaceWorldGenerationService.CalculateSeedFilterConcurrency(4));
        Check.Equal(6, TerrariaRaceWorldGenerationService.CalculateSeedFilterConcurrency(8));
        Check.Equal(12, TerrariaRaceWorldGenerationService.CalculateSeedFilterConcurrency(16));
    }

    private static async Task RaceSeedFilterBatchJourney(CancellationToken cancellationToken)
    {
        string? workerPath = Environment.GetEnvironmentVariable(
            "TERRARIA_WORLD_FILTER");
        if (string.IsNullOrWhiteSpace(workerPath))
        {
            return;
        }

        var settings = new AutoCreateWorldSettings
        {
            EnableCheats = true,
            EnablePyramidFilter = false,
            WorldSize = AutoCreateWorldSize.Small,
            WorldDifficulty = AutoCreateWorldDifficulty.Classic,
            WorldEvil = AutoCreateWorldEvil.Crimson,
            JungleRouteDepth = AutoCreateJungleRouteDepth.Medium
        };
        string[] seeds = ["576122169", "1527488", "1083872473"];
        using var service = new TerrariaRaceWorldGenerationService();

        TerrariaRaceSeedFilterBatchResult result =
            await service.FilterSeedBatchAsync(
                settings,
                seeds,
                cancellationToken);

        Check.False(result.HasFatalError);
        Check.Equal(seeds.Length, result.EvaluatedCount);
        Check.True(result.AcceptedCandidates.Any(candidate =>
            candidate.SeedText == "1527488" &&
            candidate.BatchIndex == 1));
    }

    private static async Task UiSeedBatchReplanJourney(
        CancellationToken cancellationToken)
    {
        int batchSize = WorldSeedFilterEvaluator.CalculateParallelism(
            Environment.ProcessorCount);
        string[] emptyPrediction = Enumerable.Repeat("702683177", batchSize).ToArray();
        string[] driftedPrediction = Enumerable.Repeat("702683177", batchSize).ToArray();
        driftedPrediction[1] = "540278984";
        string[] acceptedPrediction = Enumerable.Repeat("702683177", batchSize).ToArray();
        acceptedPrediction[0] = "540278984";
        var ui = new FakePredictedSeedUi(
            [
                new FakeSeedPlan(
                    emptyPrediction,
                    Enumerable.Repeat("702683177", batchSize).ToArray()),
                new FakeSeedPlan(driftedPrediction, ["111111111", "999999999"]),
                new FakeSeedPlan(acceptedPrediction, ["540278984"])
            ]);
        var settings = new AutoCreateWorldSettings
        {
            EnableCheats = true,
            EnablePyramidFilter = true,
            WorldSize = AutoCreateWorldSize.Small,
            WorldDifficulty = AutoCreateWorldDifficulty.Classic,
            WorldEvil = AutoCreateWorldEvil.Crimson,
            PyramidFilterItemMask = AutoCreatePyramidFilterItem.SandstormInABottleMask
        };
        using var evaluator = new WorldSeedFilterEvaluator();
        var loop = new PyramidSeedPreScreenLoop(evaluator, _ => { });

        PyramidSeedPreScreenLoopResult result = await loop.RunAsync(
            settings,
            TerrariaMenuProfile.Modern1456,
            ui,
            ui,
            cancellationToken);

        Check.True(result.Accepted);
        Check.Equal("540278984", result.AcceptedSeed);
        Check.Equal(batchSize + 3, result.Attempts);
        Check.Equal(batchSize + 3, ui.RandomizeClicks);
        Check.Equal(3, ui.PredictionReads);
        Check.Equal("540278984", ui.ReadCurrentSeed());
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
        string[] hookStart = TerrariaRaceWorldLockService.BuildStartCommand(
            "TerrariaSplit.RaceHook.1234",
            4321).Split('\n');
        Check.Equal("start", hookStart[0]);
        Check.Equal(
            "TerrariaSplit.RaceHook.1234",
            System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(hookStart[1])));
        Check.Equal("4321", hookStart[2]);

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
            ResourceFilterSpelunkerPotionMinimum = 7,
            ResourceFilterFeatherfallPotionMinimum = -1
        };
        SettingsNormalizer.Normalize(new AppSettings { Automation = { AutoCreate = settings } });
        Check.Equal(AutoCreateWorldSize.Small, settings.WorldSize);
        Check.Equal(AutoCreateWorldDifficulty.Classic, settings.WorldDifficulty);
        Check.Equal(AutoCreateWorldEvil.Crimson, settings.WorldEvil);
        Check.Equal(AutoCreateCrimsonDistance.Far, settings.CrimsonDistance);
        Check.True((settings.PyramidFilterItemMask & ~AutoCreatePyramidFilterItem.AllMask) == 0);
        Check.Equal(0, settings.ResourceFilterItemMask);
        Check.Equal(0, settings.ResourceFilterLifeCrystalMinimum);
        Check.Equal(0, settings.ResourceFilterSpelunkerPotionMinimum);
        Check.Equal(0, settings.ResourceFilterFeatherfallPotionMinimum);
        Check.Equal(2, AutoCreateSeedList.Parse(settings.SecretSeeds).Count);
    }

    private sealed record FakeSeedPlan(
        IReadOnlyList<string> PredictedSeeds,
        IReadOnlyList<string> VisibleSeedsAfterClicks);

    private sealed class FakePredictedSeedUi :
        IPyramidSeedRandomizer,
        IPyramidVisibleSeedReader
    {
        private readonly IReadOnlyList<FakeSeedPlan> plans;
        private int nextPlanIndex;
        private FakeSeedPlan? activePlan;
        private int activeClickIndex;
        private string currentSeed = "0";

        public FakePredictedSeedUi(IReadOnlyList<FakeSeedPlan> plans)
        {
            this.plans = plans;
        }

        public int RandomizeClicks { get; private set; }

        public int PredictionReads { get; private set; }

        public string? ReadCurrentSeed() => currentSeed;

        public bool TryPredictNextSeedBatch(
            int count,
            out IReadOnlyList<string> seedTexts,
            out string detail)
        {
            PredictionReads++;
            if (nextPlanIndex >= plans.Count)
            {
                seedTexts = Array.Empty<string>();
                detail = "No fake plan remains.";
                return false;
            }

            activePlan = plans[nextPlanIndex++];
            activeClickIndex = 0;
            seedTexts = activePlan.PredictedSeeds;
            detail = "fake Terraria Main.rand";
            return seedTexts.Count == count;
        }

        public Task<bool> RandomizeVisibleSeedAsync(
            int attempt,
            CancellationToken cancellationToken)
        {
            _ = attempt;
            cancellationToken.ThrowIfCancellationRequested();
            if (activePlan is null ||
                activeClickIndex >= activePlan.VisibleSeedsAfterClicks.Count)
            {
                return Task.FromResult(false);
            }

            currentSeed = activePlan.VisibleSeedsAfterClicks[activeClickIndex++];
            RandomizeClicks++;
            return Task.FromResult(true);
        }

        public Task<PyramidVisibleSeedReadResult> WaitForSeedAfterRandomizeAsync(
            string? previousSeedText,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                string.Equals(currentSeed, previousSeedText, StringComparison.Ordinal)
                    ? PyramidVisibleSeedReadResult.Failed(
                        TerrariaWorldCreationSeedStatus.Seed,
                        1,
                        currentSeed)
                    : PyramidVisibleSeedReadResult.FromSeed(currentSeed, 1));
        }
    }

    private class RaceUiElement
    {
        public float Left = 0f;
    }

    private sealed class DerivedRaceUiElement : RaceUiElement
    {
    }

    private sealed class RaceUiLinkPoint
    {
        public int Left = 0;
        public int Right = 0;
        public bool Enabled = false;
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
