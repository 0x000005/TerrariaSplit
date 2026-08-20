using System.Drawing;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using TerrariaSplit.Race.Determinism;
using TerrariaSplit.Race.InGame;
using TerrariaSplit.Terraria;
using TerrariaSplit.Terraria.Automation;
using TerrariaSplit.Terraria.Memory;
using TerrariaSplit.Terraria.WorldGeneration;
using TerrariaSplit.MemoryBridge.Payload;

namespace TerrariaSplit.Tests;

internal static class TerrariaIntegrationTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("pyramid pre-screen evaluates known positive, item mismatch and no-pyramid seeds", TestSuite.Flow, PyramidPredictionJourney, timeoutSeconds: 30);
        yield return TestCase.Async("native jungle seed judge preserves protocol and returns seed-only analysis", TestSuite.Native, JungleSeedJudgeNativeJourney, timeoutSeconds: 30);
        yield return TestCase.Async("world seed filter skips a seed when the native call times out", TestSuite.Native, WorldSeedFilterTimeoutJourney, timeoutSeconds: 10);
        yield return TestCase.Async("native jungle seed judge applies its timeout while waiting for a call slot", TestSuite.Core, JungleSeedJudgeGateTimeoutJourney, timeoutSeconds: 10);
        yield return TestCase.Sync("world seed filter skips candidate-local native failures", TestSuite.Core, WorldSeedFilterCandidateFailureClassification);
        yield return TestCase.Async("world seed filter classifies native generation failures as candidate failures", TestSuite.Core, WorldSeedFilterGenerationFailureJourney);
        yield return TestCase.Async("UI seed filtering skips candidate failures and stops after three consecutive failures", TestSuite.Core, UiSeedCandidateFailureJourney, timeoutSeconds: 10);
        yield return TestCase.Async("world seed filter skips a seed when the jungle route is partial", TestSuite.Native, WorldSeedFilterPartialRouteJourney, timeoutSeconds: 10);
        yield return TestCase.Sync("race seed filter concurrency uses eighty percent of processors", TestSuite.Core, RaceSeedFilterConcurrency);
        yield return TestCase.Sync("race seed filtering skips isolated candidate failures and preserves the failure circuit", TestSuite.Core, RaceSeedCandidateFailureBatch);
        yield return TestCase.Async("race seed filter evaluates candidate seeds as one parallel batch", TestSuite.Native, RaceSeedFilterBatchJourney, timeoutSeconds: 15);
        yield return TestCase.Async("UI seed pre-screen restarts after an empty batch or RNG drift without seed writeback", TestSuite.Flow, UiSeedBatchReplanJourney, timeoutSeconds: 30);
        yield return TestCase.Async("race world upload validates, hashes, deduplicates, locates and deletes a Terraria world", TestSuite.Flow, WorldFileTransferJourney);
        yield return TestCase.Sync("world automation settings normalize incompatible options and secret seed lists", TestSuite.Core, WorldSettingsNormalization);
        yield return TestCase.Sync("Terraria 1.4.5.7 created-player selection follows favorite and LastPlayed ordering", TestSuite.Core, CreatedPlayerSelectionOrdering);
        yield return TestCase.Sync("Terraria 1.4.5.7 menu geometry mirrors source layout at multiple client sizes", TestSuite.Core, Terraria1457MenuGeometry);
        yield return TestCase.Sync("Terraria 1.4.5.7 seed inputs separate secret bootstrap text from fixed seed", TestSuite.Core, Terraria1457SeedInputs);
        yield return TestCase.Sync("biome facts read all required zone bytes in one memory operation", TestSuite.Core, BiomeZoneBatchRead);
        yield return TestCase.Sync("window coordinate transform round-trips logical and physical client centers", TestSuite.Core, WindowCoordinateTransform);
        yield return TestCase.Sync("race UI reflection targets compatible runtime fields and preserves deferred failures", TestSuite.Core, RaceUiRuntimeSafety);
        yield return TestCase.Sync("Terraria 1.4.5.7 catalogs and world files reject obsolete identifiers and versions", TestSuite.Core, Terraria1457CompatibilityCatalog);
    }

    private static void Terraria1457CompatibilityCatalog()
    {
        Check.Equal(6195, SplitFactKeys.MaxItemId);
        Check.Equal("八音盒（彩虹巨石）", TerrariaItemCatalog.ById[6145].ChineseName);
        Check.Equal("八音盒（寂静）", TerrariaItemCatalog.ById[6146].ChineseName);
        Check.Equal("Trusty Foxparks", TerrariaItemCatalog.ById[6149].DisplayName);
        Check.Equal("GiantTiki", TerrariaItemCatalog.ById[6147].InternalName);
        Check.Equal("OldStyleParkourBookInactive", TerrariaItemCatalog.ById[6195].InternalName);
        Check.True(TerrariaItemCatalog.IsDeprecated(6143));
        Check.True(TerrariaItemCatalog.IsDeprecated(6160));
        Check.True(TerrariaItemCatalog.IsDeprecated(6170));
        Check.True(TerrariaItemCatalog.IsDeprecated(6171));
        Check.False(SplitCatalog.TryGetTarget("item:6143", out _));
        Check.False(SplitCatalog.TryGetTarget("item:6160", out _));
        Check.True(SplitCatalog.TryGetTarget("item:6195", out SplitTargetDefinition newestItem));
        Check.Equal("Guide to Old World Parkour (Inactive)", newestItem.DisplayName);
        Check.False(SplitCatalog.TryGetTarget("item:6196", out _));

        byte[] currentWorld = CreateMinimalWorld();
        using var currentStream = new MemoryStream(currentWorld, writable: false);
        Check.True(RaceWorldFileValidator.TryValidateWorldStream(currentStream, out _));

        byte[] obsoleteWorld = (byte[])currentWorld.Clone();
        BitConverter.GetBytes(319).CopyTo(obsoleteWorld, 0);
        using var obsoleteStream = new MemoryStream(obsoleteWorld, writable: false);
        Check.False(RaceWorldFileValidator.TryValidateWorldStream(obsoleteStream, out string detail));
        Check.True(detail.Contains("319", StringComparison.Ordinal));
    }

    private static void BiomeZoneBatchRead()
    {
        const string jungleFact = "biome:jungle:active";
        IntPtr playerAddress = new(1_000);
        var layout = new TerrariaBiomeMemoryLayout(
            IntPtr.Zero,
            IntPtr.Zero,
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["zone1"] = 10,
                ["zone2"] = 11,
                ["zone3"] = 12,
                ["zone4"] = 13,
                ["zone5"] = 14
            },
            ManagedArrayLengthOffset: 0,
            ManagedArrayFirstElementOffset: 0,
            ObjectReferenceSize: 4);
        var context = new TerrariaMemoryContext(
            BossLayout: null,
            playerAddress,
            ItemLayout: null,
            NpcLayout: null,
            layout,
            Is64Bit: false);
        var memory = new RecordingMemoryReader(
            IntPtr.Add(playerAddress, 10),
            [(byte)(1 << 4), 0, (byte)(1 << 1)]);

        TerrariaFactReadPlan readPlan = TerrariaFactReadPlan.FromObservedFactKeys([jungleFact]);
        TerrariaGameFacts facts = new BiomeFactProvider().Read(memory, context, readPlan);

        Check.Equal(true, facts.Get(jungleFact).AsBoolean());
        Check.Equal(1, memory.ReadBytesCallCount);
        Check.Equal(IntPtr.Add(playerAddress, 10), memory.LastReadAddress);
        Check.Equal(3, memory.LastReadCount);
    }

    private static void CreatedPlayerSelectionOrdering()
    {
        DateTime now = DateTime.UtcNow;
        TerrariaPlayerSelectionEntry[] players =
        [
            new("favorite-old.plr", "Favorite Old", true, now.AddDays(-10)),
            new("existing-newer-file.plr", "Existing", false, now.AddMinutes(1)),
            new("created.plr", "Created", false, now),
            new("favorite-new.plr", "Favorite New", true, now)
        ];

        int index = TerrariaPlayerSelectionIndexResolver.ResolveCreatedPlayerIndex(
            TerrariaMenuProfile.Modern1457,
            players,
            "created.plr",
            fallbackIndex: 0);

        Check.Equal(2, index);
    }

    private static void Terraria1457MenuGeometry()
    {
        TerrariaMenuGeometry compact = TerrariaMenuGeometry.From(
            new Size(800, 626),
            TerrariaMenuProfile.Modern1457,
            mainMenuUpscaleDisabled: false);

        Check.Equal(1f, compact.Scale);
        Check.Equal(new Point(182, 296), compact.CharacterInfoCategoryButton());
        Check.Equal(new Point(230, 296), compact.CharacterClothingCategoryButton());
        Check.Equal(new Point(254, 392), compact.PlayerDifficultyButton(AutoCreatePlayerDifficulty.Journey));
        Check.Equal(new Point(254, 419), compact.PlayerDifficultyButton(AutoCreatePlayerDifficulty.Softcore));
        Check.Equal(new Point(254, 446), compact.PlayerDifficultyButton(AutoCreatePlayerDifficulty.Mediumcore));
        Check.Equal(new Point(254, 473), compact.PlayerDifficultyButton(AutoCreatePlayerDifficulty.Hardcore));

        Check.Equal(new Point(236, 321), compact.WorldSizeButton(AutoCreateWorldSize.Small));
        Check.Equal(new Point(400, 321), compact.WorldSizeButton(AutoCreateWorldSize.Medium));
        Check.Equal(new Point(564, 321), compact.WorldSizeButton(AutoCreateWorldSize.Large));
        Check.Equal(new Point(218, 369), compact.WorldDifficultyButton(AutoCreateWorldDifficulty.Journey));
        Check.Equal(new Point(340, 369), compact.WorldDifficultyButton(AutoCreateWorldDifficulty.Classic));
        Check.Equal(new Point(460, 369), compact.WorldDifficultyButton(AutoCreateWorldDifficulty.Expert));
        Check.Equal(new Point(582, 369), compact.WorldDifficultyButton(AutoCreateWorldDifficulty.Master));
        Check.Equal(new Point(236, 417), compact.WorldEvilButton(AutoCreateWorldEvil.Random));
        Check.Equal(new Point(400, 417), compact.WorldEvilButton(AutoCreateWorldEvil.Corruption));
        Check.Equal(new Point(564, 417), compact.WorldEvilButton(AutoCreateWorldEvil.Crimson));

        Check.Equal(new Point(378, 274), compact.WorldSeedFieldButton());
        Check.Equal(new Point(422, 230), compact.AdvancedSeedTextButton());
        Check.Equal(new Point(586, 287), compact.AdvancedSpecialSeedButton(AutoCreateSpecialWorldSeed.ForTheWorthy));
        Check.Equal(new Point(214, 354), compact.AdvancedSpecialSeedButton(AutoCreateSpecialWorldSeed.NoTraps));
        Check.Equal(new Point(113, 311), compact.PlayerPlayButton(0));
        Check.Equal(new Point(530, 534), compact.CreatePlayerButton());
        Check.Equal(new Point(400, 534), compact.WorldAdvancedApplyButton());

        TerrariaMenuGeometry shortWindow = TerrariaMenuGeometry.From(
            new Size(800, 500),
            TerrariaMenuProfile.Modern1457,
            mainMenuUpscaleDisabled: false);
        Check.Equal(new Point(400, 434), shortWindow.WorldAdvancedApplyButton());

        TerrariaMenuGeometry highResolution = TerrariaMenuGeometry.From(
            new Size(1920, 1080),
            TerrariaMenuProfile.Modern1457,
            mainMenuUpscaleDisabled: false);
        Check.Equal(1.2f, highResolution.Scale);
        Check.Equal(new Point(960, 294), highResolution.MainMenuSinglePlayer());
        Check.Equal(new Point(1116, 641), highResolution.CreatePlayerButton());
        Check.Equal(new Point(763, 385), highResolution.WorldSizeButton(AutoCreateWorldSize.Small));
        Check.Equal(new Point(1161, 996), highResolution.SelectMenuNewButton());

        TerrariaMenuGeometry unscaledHighResolution = TerrariaMenuGeometry.From(
            new Size(1920, 1080),
            TerrariaMenuProfile.Modern1457,
            mainMenuUpscaleDisabled: true);
        Check.Equal(1f, unscaledHighResolution.Scale);
        Check.Equal(new Point(1090, 534), unscaledHighResolution.CreatePlayerButton());
    }

    private static void WindowCoordinateTransform()
    {
        IntPtr[] dpiContexts = [new(-1), new(-2), new(-4), new(-5)];
        foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
        {
            foreach (IntPtr dpiContext in dpiContexts)
            {
                VerifyWindowCoordinateTransform(screen.WorkingArea, dpiContext);
            }
        }
    }

    private static void Terraria1457SeedInputs()
    {
        const string secretSeeds = "abandoned manors|arachnophobia|beam me up|bring a towel|double daring dangers|fish mox|hocus pocus|how did i get here|i am error|invisible plane|jagged rocks|jingle all the way|mole people|monochrome|more traps please|negative infinity|night of the living dead|planetoids|pumpkin season|purify this|rainbow road|royale with cheese|does that sparkle|too easy|water park|what a horrible night to have a curse|winter is coming|x-ray vision|truck stop|sandy britches|save the rainforest|such great heights|the care bears movie|toadstool|we don't even test for that";
        Check.Equal(
            "1.1.1.0." + secretSeeds + "|",
            TerrariaCopiedSeedBuilder.BuildSecretSeedBootstrapText(secretSeeds));
        Check.Equal(string.Empty, TerrariaCopiedSeedBuilder.BuildSecretSeedBootstrapText("  "));

        var settings = new AutoCreateWorldSettings
        {
            SecretSeeds = "abandoned manors|arachnophobia",
            FixedSeed = "  123456789  "
        };
        TerrariaCopiedSeed copiedSeed = TerrariaCopiedSeedBuilder.Create(settings);
        Check.Equal("abandoned manors|arachnophobia|123456789", copiedSeed.Metadata.SeedText);

        var specialSeed = new AutoCreateWorldSettings
        {
            WorldSize = AutoCreateWorldSize.Small,
            WorldEvil = AutoCreateWorldEvil.Crimson,
            SpecialSeeds = AutoCreateSpecialWorldSeed.NotTheBees,
            EnableCheats = true,
            EnablePyramidFilter = true
        };
        Check.True(PyramidSeedPreScreenEvaluator.IsEnabledFor(specialSeed));
        Check.True(WorldSeedFilterEvaluator.IsEnabledFor(specialSeed));

        specialSeed.SpecialSeeds = string.Empty;
        specialSeed.SecretSeeds = "abandoned manors";
        Check.True(PyramidSeedPreScreenEvaluator.IsEnabledFor(specialSeed));
        Check.True(WorldSeedFilterEvaluator.IsEnabledFor(specialSeed));

        var fixedOnly = new AutoCreateWorldSettings
        {
            WorldSize = AutoCreateWorldSize.Small,
            WorldEvil = AutoCreateWorldEvil.Crimson,
            FixedSeed = "123456789",
            EnableCheats = true,
            EnablePyramidFilter = true,
            RequireCrimsonBetweenDungeonAndSpawn = true
        };
        Check.False(PyramidSeedPreScreenEvaluator.IsEnabledFor(fixedOnly));
        Check.False(WorldSeedFilterEvaluator.IsEnabledFor(fixedOnly));
    }

    private static void VerifyWindowCoordinateTransform(Rectangle workingArea, IntPtr dpiContext)
    {
        System.Windows.Forms.Form? form = null;
        IntPtr previousDpiContext = SetThreadDpiAwarenessContext(dpiContext);
        Check.True(previousDpiContext != IntPtr.Zero);
        try
        {
            form = new System.Windows.Forms.Form
            {
                ClientSize = new Size(800, 600),
                StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                Location = new Point(workingArea.Left + 40, workingArea.Top + 40),
                ShowInTaskbar = false
            };
            _ = form.Handle;
        }
        finally
        {
            _ = SetThreadDpiAwarenessContext(previousDpiContext);
        }

        using (form)
        {
            Check.True(form is not null);
            bool resolved = TerrariaWindowController.TryInspectCoordinateTransform(
                form!.Handle,
                out Size logicalClientSize,
                out Rectangle physicalClientBounds,
                out Point logicalCenter,
                out Point physicalCenter,
                out string detail);

            if (!resolved)
            {
                throw new InvalidOperationException(
                    $"Coordinate transform failed for DPI context {dpiContext} at {workingArea}: {detail}");
            }
            Check.True(logicalClientSize.Width > 0 && logicalClientSize.Height > 0);
            Check.True(physicalClientBounds.Width > 0 && physicalClientBounds.Height > 0);
            Check.True(logicalCenter.X > 0 && logicalCenter.Y > 0);
            Check.True(Math.Abs(physicalCenter.X - (physicalClientBounds.Left + physicalClientBounds.Width / 2)) <= 2);
            Check.True(Math.Abs(physicalCenter.Y - (physicalClientBounds.Top + physicalClientBounds.Height / 2)) <= 2);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

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
        string workerPath = JungleSeedJudgeNativeLibraryLocator.ResolvePath();

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
        string workerPath = JungleSeedJudgeNativeLibraryLocator.ResolvePath();

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
            TerrariaWorldGenerationVersion.Modern1457,
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
        Check.True(WorldSeedFilterEvaluator.IsCandidateFailure(
            JungleSeedJudgeStatus.GenerationFailed));
        Check.False(WorldSeedFilterEvaluator.IsCandidateFailure(
            JungleSeedJudgeStatus.InvalidRequest));
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
            TerrariaWorldGenerationVersion.Modern1457,
            cancellationToken);

        Check.True(prediction.CanUsePrediction);
        Check.False(prediction.CanContinueWithoutPrediction);
        Check.False(prediction.AcceptSeed);
        Check.True(prediction.IsCandidateFailure);
        Check.Equal(
            WorldSeedFilterPredictionKind.CandidateFailure,
            prediction.Kind);
        Check.True(prediction.Detail.Contains(
            nameof(JungleSeedJudgeStatus.GenerationFailed),
            StringComparison.Ordinal));
        Check.True(prediction.Detail.Contains(
            "seed=12345, mode=Classic",
            StringComparison.Ordinal));
    }

    private static async Task UiSeedCandidateFailureJourney(
        CancellationToken cancellationToken)
    {
        var gate = new SemaphoreSlim(1, 1);
        var nativeClient = new JungleSeedJudgeNativeClient(
            (seedText, _, requestId) => CreateFilterJudgeResult(
                seedText,
                requestId,
                string.Equals(seedText, "300", StringComparison.Ordinal)
                    ? JungleSeedJudgeStatus.Complete
                    : JungleSeedJudgeStatus.GenerationFailed),
            TimeSpan.FromSeconds(1),
            gate);
        using var evaluator = new WorldSeedFilterEvaluator(
            nativeClient: nativeClient);
        var settings = CandidateFailureSettings();
        var continuingUi = new FakePredictedSeedUi(
            [new FakeSeedPlan(Array.Empty<string>(), ["100", "200", "300"])]);
        var continuingLoop = new PyramidSeedPreScreenLoop(
            evaluator,
            _ => { });

        PyramidSeedPreScreenLoopResult accepted = await continuingLoop.RunAsync(
            settings,
            TerrariaMenuProfile.Modern1457,
            continuingUi,
            continuingUi,
            cancellationToken);

        Check.True(accepted.Accepted);
        Check.Equal("300", accepted.AcceptedSeed);
        Check.Equal(3, accepted.Attempts);

        var failingClient = new JungleSeedJudgeNativeClient(
            (seedText, _, requestId) => CreateFilterJudgeResult(
                seedText,
                requestId,
                JungleSeedJudgeStatus.GenerationFailed),
            TimeSpan.FromSeconds(1),
            new SemaphoreSlim(1, 1));
        using var failingEvaluator = new WorldSeedFilterEvaluator(
            nativeClient: failingClient);
        var failingUi = new FakePredictedSeedUi(
            [new FakeSeedPlan(Array.Empty<string>(), ["400", "500", "600"])]);
        var failingLoop = new PyramidSeedPreScreenLoop(
            failingEvaluator,
            _ => { });

        PyramidSeedPreScreenLoopResult failed = await failingLoop.RunAsync(
            settings,
            TerrariaMenuProfile.Modern1457,
            failingUi,
            failingUi,
            cancellationToken);

        Check.Equal(
            PyramidSeedPreScreenLoopStatus.CandidateFailuresExceeded,
            failed.Status);
        Check.Equal(3, failed.Attempts);
        Check.True(failed.Detail.Contains(
            "3 consecutive candidate generation failures",
            StringComparison.Ordinal));
        Check.True(failed.Detail.Contains(
            "seed=600",
            StringComparison.Ordinal));
    }

    private static async Task WorldSeedFilterPartialRouteJourney(CancellationToken cancellationToken)
    {
        string workerPath = JungleSeedJudgeNativeLibraryLocator.ResolvePath();

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
            TerrariaWorldGenerationVersion.Modern1457,
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
        _ = JungleSeedJudgeNativeLibraryLocator.ResolvePath();

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
            PyramidFilterItemMask = AutoCreatePyramidFilterItem.SandstormInABottleMask,
            RequireCrimsonBetweenDungeonAndSpawn = false,
            JungleRouteDepth = AutoCreateJungleRouteDepth.None
        };
        using var evaluator = new WorldSeedFilterEvaluator();
        var messages = new List<string>();
        var loop = new PyramidSeedPreScreenLoop(evaluator, messages.Add);

        PyramidSeedPreScreenLoopResult result = await loop.RunAsync(
            settings,
            TerrariaMenuProfile.Modern1457,
            ui,
            ui,
            cancellationToken);

        if (!result.Accepted)
        {
            throw new InvalidOperationException(
                $"Expected the replanned batch to be accepted, but got {result.Status}: " +
                $"{result.Detail} Logs: {string.Join(" | ", messages)}");
        }
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
        PyramidSeedPreScreenPrediction accepted = evaluator.Evaluate(settings, "540278984", TerrariaWorldGenerationVersion.Modern1457);
        Check.True(accepted.CanUsePrediction);
        Check.True(accepted.AcceptSeed);
        Check.True(accepted.Result.LootSummary.Contains("Sandstorm in a Bottle", StringComparison.Ordinal));

        settings.EnableCheats = false;
        Check.False(PyramidSeedPreScreenEvaluator.IsEnabledFor(settings));
        settings.EnableCheats = true;

        settings.PyramidFilterItemMask = AutoCreatePyramidFilterItem.FlyingCarpetMask;
        PyramidSeedPreScreenPrediction mismatch = evaluator.Evaluate(settings, "540278984", TerrariaWorldGenerationVersion.Modern1457);
        Check.False(mismatch.AcceptSeed);
        Check.Equal("item mismatch", mismatch.RejectReason);
        PyramidSeedPreScreenPrediction absent = evaluator.Evaluate(settings, "702683177", TerrariaWorldGenerationVersion.Modern1457);
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
                EntryAllowed: false,
                BossFailurePenaltyEnabled: false,
                BossPenaltyEnabledKinds:
                    RaceBossPenaltyKinds.All & ~RaceBossPenaltyKinds.Plantera,
                BossPenaltySchedule: RaceBossPenalty.DefaultSchedule.Encode()),
            Path.Combine(directory.Path, "Race_Player.plr"),
            rejectionMessage);
        string[] lockParts = lockCommand.Split('\n');
        Check.Equal(18, lockParts.Length);
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
        Check.Equal("0", lockParts[14]);
        Check.Equal(
            (RaceBossPenaltyKinds.All & ~RaceBossPenaltyKinds.Plantera)
                .ToString(System.Globalization.CultureInfo.InvariantCulture),
            lockParts[15]);
        Check.Equal(RaceBossPenalty.DefaultSchedule.Encode(), lockParts[16]);
        Check.Equal(determinism.CreateDigest(), lockParts[17]);
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
        Check.True(first.WasCreated);
        await using (var stream = new MemoryStream(world))
        {
            RaceStoredWorldFile second = await store.SaveAsync("a-b-12", "host", "race.wld", stream, cancellationToken);
            Check.Equal(first.Path, second.Path);
            Check.Equal(first.Info.Sha256, second.Info.Sha256);
            Check.False(second.WasCreated);
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
            FixedSeed = "  12345  ",
            EnablePyramidFilter = true,
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
        Check.Equal("12345", settings.FixedSeed);
        Check.False(settings.EnablePyramidFilter);
    }

    private static AutoCreateWorldSettings CandidateFailureSettings()
    {
        return new AutoCreateWorldSettings
        {
            EnableCheats = true,
            EnablePyramidFilter = false,
            WorldSize = AutoCreateWorldSize.Small,
            WorldDifficulty = AutoCreateWorldDifficulty.Classic,
            WorldEvil = AutoCreateWorldEvil.Crimson,
            JungleRouteDepth = AutoCreateJungleRouteDepth.Medium
        };
    }

    private static JungleSeedJudgeResult CreateFilterJudgeResult(
        string seedText,
        string requestId,
        JungleSeedJudgeStatus status)
    {
        if (status != JungleSeedJudgeStatus.Complete)
        {
            return new JungleSeedJudgeResult(
                JungleSeedJudgeProtocol.Version,
                requestId,
                JungleSeedJudgeProtocol.CompatibilityId,
                status,
                seedText,
                0,
                0,
                0,
                Jungle: null,
                CrimsonVertices: null,
                Detail: "pass 34 (Beaches): candidate generation failed");
        }

        return new JungleSeedJudgeResult(
            JungleSeedJudgeProtocol.Version,
            requestId,
            JungleSeedJudgeProtocol.CompatibilityId,
            JungleSeedJudgeStatus.Complete,
            seedText,
            62,
            1,
            1,
            new JungleSeedAnalysis(
                JungleSeedAnalysisStatus.Complete,
                "Left",
                1000,
                800,
                1400,
                new JungleRouteSummary(
                    JungleRouteStatus.Complete,
                    1,
                    48,
                    100,
                    1000,
                    900),
                1,
                100,
                Array.Empty<JungleResourceLocation>()),
            [
                new CrimsonCorridorVertex(1, 2300, 300),
                new CrimsonCorridorVertex(2, 2600, 300)
            ],
            "accepted");
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

    private static void RaceSeedCandidateFailureBatch()
    {
        WorldSeedFilterPrediction failed =
            WorldSeedFilterPrediction.CandidateFailure(
                "seed judge status GenerationFailed; seed=100, mode=Classic: pass 34 (Beaches): failed",
                pyramid: null,
                judge: null);
        WorldSeedFilterPrediction accepted =
            WorldSeedFilterPrediction.Accepted(
                "accepted",
                pyramid: null,
                judge: null);
        TerrariaRaceSeedFilterBatchResult mixed =
            TerrariaRaceWorldGenerationService.ClassifySeedFilterBatch(
                [
                    new TerrariaRaceWorldGenerationService.SeedFilterEvaluation(
                        "100",
                        0,
                        failed),
                    new TerrariaRaceWorldGenerationService.SeedFilterEvaluation(
                        "200",
                        1,
                        accepted)
                ]);

        Check.False(mixed.HasFatalError);
        Check.Equal(1, mixed.AcceptedCandidates.Count);
        Check.Equal("200", mixed.AcceptedCandidates[0].SeedText);
        Check.Equal(0, mixed.ConsecutiveCandidateFailures);

        TerrariaRaceSeedFilterBatchResult threshold =
            TerrariaRaceWorldGenerationService.ClassifySeedFilterBatch(
                [
                    new TerrariaRaceWorldGenerationService.SeedFilterEvaluation(
                        "300",
                        0,
                        failed)
                ],
                initialConsecutiveCandidateFailures: 2);

        Check.True(threshold.HasFatalError);
        Check.Equal(3, threshold.ConsecutiveCandidateFailures);
        Check.True(threshold.FatalError.Contains(
            "3 consecutive candidate generation failures",
            StringComparison.Ordinal));
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

    private sealed class RecordingMemoryReader(IntPtr expectedAddress, byte[] expectedBytes) : IProcessMemoryReader
    {
        public bool Is64Bit => false;

        public int ReadBytesCallCount { get; private set; }

        public IntPtr LastReadAddress { get; private set; }

        public int LastReadCount { get; private set; }

        public bool TryReadBytes(IntPtr address, int count, [NotNullWhen(true)] out byte[]? bytes)
        {
            ReadBytesCallCount++;
            LastReadAddress = address;
            LastReadCount = count;
            if (address == expectedAddress && count == expectedBytes.Length)
            {
                bytes = (byte[])expectedBytes.Clone();
                return true;
            }

            bytes = null;
            return false;
        }

        public bool TryReadBool(IntPtr address, out bool value)
        {
            _ = address;
            value = false;
            return false;
        }

        public bool TryReadInt32(IntPtr address, out int value)
        {
            _ = address;
            value = 0;
            return false;
        }

        public bool TryReadDouble(IntPtr address, out double value)
        {
            _ = address;
            value = 0;
            return false;
        }

        public bool TryReadPointer(IntPtr address, out IntPtr value)
        {
            _ = address;
            value = IntPtr.Zero;
            return false;
        }

        public bool TryReadPointerValue(IntPtr address, out IntPtr value)
        {
            _ = address;
            value = IntPtr.Zero;
            return false;
        }

        public IEnumerable<MemoryPage> ExecutablePages() => [];

        public IEnumerable<MemoryPage> ExecutablePrivatePages() => [];
    }

    internal static byte[] CreateMinimalWorld(string worldName = "test-world", int worldId = 24680)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(RaceTerrariaCompatibility.WorldFileVersion);
        writer.Write(0x026369676F6C6572UL);
        writer.Write((uint)0);
        writer.Write((ulong)0);
        writer.Write((short)1);
        long pointerPosition = stream.Position;
        writer.Write(0);
        const short importanceCount = 754;
        writer.Write(importanceCount);
        writer.Write(new byte[(importanceCount + 7) / 8]);
        int headerPosition = checked((int)stream.Position);
        writer.Write(worldName);
        writer.Write("test-seed");
        writer.Write(1_395_864_371_201UL);
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
