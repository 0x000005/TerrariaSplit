namespace TerrariaSplit.Tests;

internal static class ConfigurationStorageFlowTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("settings save, profile selection and reload preserve normalized user choices", TestSuite.Flow, SettingsRoundTrip);
        yield return TestCase.Sync("settings save persists edited reference data while PB reference mode is active", TestSuite.Flow, PersonalBestModePreservesReferenceEdits);
        yield return TestCase.Sync("settings save reports split-set write failures before writing the settings document", TestSuite.Flow, SplitSetWriteFailureIsReported);
        yield return TestCase.Sync("new settings are deep clones of the single embedded default template", TestSuite.Flow, CanonicalSettingsDefaults);
        yield return TestCase.Sync("switching routes preserves reference data owned by another profile", TestSuite.Flow, SwitchingRoutesPreservesInactiveReferenceData);
        yield return TestCase.Sync("advanced world filters require a plain small Crimson world and fixed seeds disable every filter", TestSuite.Flow, AdvancedFilterEligibility);
        yield return TestCase.Sync("split time and run statistics persist through injected runtime paths", TestSuite.Flow, SplitAndRunStorageFlow);
        yield return TestCase.Sync("corrupt settings recover to usable defaults without escaping the data root", TestSuite.Flow, CorruptSettingsRecovery);
    }

    private static void SettingsRoundTrip()
    {
        using var directory = new TestDirectory();
        var paths = new AppContextRuntimeDataPaths(directory.Path);
        var repository = new AppSettingsRepository(paths);
        AppSettings settings = AppSettingsDefaults.Create();
        settings.General.Language = "Chinese";
        settings.General.AlwaysOnTop = true;
        settings.Advanced.EnableManualSplit = true;
        settings.Hotkeys.ManualSplitKey = "Control, F7";
        settings.Route.VisibleGroupCountLimit = -20;
        settings.Race.ServerUrl = "  https://example.test/race  ";
        settings.Race.LastRoomCode = "  ABC123  ";
        settings.Race.PlayerTemplateCode = "  { player-template }  ";
        settings.Race.WorldSetup.Source = "LegacyExistingFile";
        settings.Race.WorldSetup.SeedText = "  seed with spaces  ";
        settings.Race.WorldSetup.WorldSize = AutoCreateWorldSize.Large;
        settings.Race.WorldSetup.WorldDifficulty = AutoCreateWorldDifficulty.Master;
        settings.Race.WorldSetup.WorldEvil = AutoCreateWorldEvil.Corruption;
        settings.Race.WorldSetup.SpecialSeeds = "not the bees | no traps";
        settings.Race.WorldSetup.RngControlEnabled = false;
        settings.Race.WorldSetup.BossFailurePenaltyEnabled = false;
        settings.Race.WorldSetup.BossPenaltyEnabledKinds =
            RaceWorldSetupSettings.AllBossPenaltyKinds & ~RaceBossPenaltyKinds.Plantera;
        settings.Race.WorldSetup.LifeCrystalMinimum = 5;
        settings.Race.BossPenalty.Skeletron.JourneyBaseSeconds = 37;
        settings.Race.BossPenalty.Skeletron.ClassicProportionalSeconds = 999999;
        settings.Race.Voice.Enabled = true;
        settings.Race.Voice.VoiceName = "  Test Voice  ";
        settings.Race.Voice.SpeedPercent = 250;
        settings.Race.Voice.Volume = -5;
        settings.Race.Leaderboard.WindowPositionX = -900;
        settings.Race.Leaderboard.WindowPositionY = 240;
        settings.Overlay.WindowPositionX = -1200;
        settings.Overlay.WindowPositionY = 120;
        settings.Overlay.Colors.DeltaEqualText = "#123456";
        settings.Overlay.Colors.TimerEqualText = "#ABCDEF";
        settings.Automation.AutoCreate.EnableCheats = true;
        settings.Automation.AutoCreate.RequireCrimsonBetweenDungeonAndSpawn = true;
        settings.Automation.AutoCreate.CrimsonDistance = AutoCreateCrimsonDistance.Near;
        settings.Automation.AutoCreate.JungleRouteDepth = AutoCreateJungleRouteDepth.Deep;
        settings.Automation.AutoCreate.ResourceFilterItemMask = AutoCreateResourceFilterItem.BoomstickMask;
        settings.Automation.AutoCreate.ResourceFilterLifeCrystalMinimum = 8;
        settings.Automation.AutoCreate.ResourceFilterSpelunkerPotionMinimum = 2;
        settings.Automation.AutoCreate.ResourceFilterFeatherfallPotionMinimum = 1;
        SplitRouteEntry multiIconEntry = settings.Route.SplitRoute.First(entry => entry.IconTargetIds.Count >= 2);
        string customizedTargetId = multiIconEntry.IconTargetIds[0];
        const string customizedIconPath = @"C:\icons\custom-all-icon.png";
        multiIconEntry.IconOverride.Source = SplitIconOverrideSource.All;
        multiIconEntry.IconOverride.AllIconFilePaths[customizedTargetId] = $"  {customizedIconPath}  ";
        multiIconEntry.IconOverride.AllIconFilePaths["boss:not-in-condition"] = @"C:\icons\unused.png";

        Check.True(repository.Save(settings).Succeeded);
        AppSettings loaded = new AppSettingsRepository(paths).Load();
        Check.Equal("中文", loaded.General.Language);
        Check.Equal("罚时", TerrariaSplit.Localization.Localizer.Get("Penalty", loaded));
        Check.Equal("旅途基础时间", TerrariaSplit.Localization.Localizer.Get("Journey base", loaded));
        Check.Equal("毁灭者", TerrariaSplit.Localization.Localizer.Get("Destroyer", loaded));
        Check.True(loaded.General.AlwaysOnTop);
        Check.True(loaded.Advanced.EnableManualSplit);
        Check.Equal("Control, F7", loaded.Hotkeys.ManualSplitKey);
        Check.True(loaded.Route.VisibleGroupCountLimit > 0);
        Check.Equal("https://example.test/race", loaded.Race.ServerUrl);
        Check.Equal(string.Empty, loaded.Race.LastRoomCode);
        Check.Equal("{ player-template }", loaded.Race.PlayerTemplateCode);
        Check.Equal(RacePreferredWorldSource.Random, loaded.Race.WorldSetup.Source);
        Check.Equal("seed with spaces", loaded.Race.WorldSetup.SeedText);
        Check.Equal(AutoCreateWorldSize.Large, loaded.Race.WorldSetup.WorldSize);
        Check.Equal(AutoCreateWorldDifficulty.Master, loaded.Race.WorldSetup.WorldDifficulty);
        Check.Equal(AutoCreateWorldEvil.Corruption, loaded.Race.WorldSetup.WorldEvil);
        Check.Sequence(
            [AutoCreateSpecialWorldSeed.NotTheBees, AutoCreateSpecialWorldSeed.NoTraps],
            AutoCreateSpecialWorldSeed.ParseList(loaded.Race.WorldSetup.SpecialSeeds));
        Check.False(loaded.Race.WorldSetup.RngControlEnabled);
        Check.False(loaded.Race.WorldSetup.BossFailurePenaltyEnabled);
        Check.Equal(
            RaceWorldSetupSettings.AllBossPenaltyKinds & ~RaceBossPenaltyKinds.Plantera,
            loaded.Race.WorldSetup.BossPenaltyEnabledKinds);
        Check.Equal(37, loaded.Race.BossPenalty.Skeletron.JourneyBaseSeconds);
        Check.Equal(
            RaceBossPenaltySettings.MaximumSeconds,
            loaded.Race.BossPenalty.Skeletron.ClassicProportionalSeconds);
        Check.False(loaded.Race.WorldSetup.CrimsonEnabled);
        Check.Equal(AutoCreateJungleRouteDepth.None, loaded.Race.WorldSetup.JungleRouteDepth);
        Check.Equal(0, loaded.Race.WorldSetup.LifeCrystalMinimum);
        Check.True(loaded.Race.Voice.Enabled);
        Check.Equal("Test Voice", loaded.Race.Voice.VoiceName);
        Check.Equal(200, loaded.Race.Voice.SpeedPercent);
        Check.Equal(0, loaded.Race.Voice.Volume);
        Check.Equal(-900, loaded.Race.Leaderboard.WindowPositionX);
        Check.Equal(240, loaded.Race.Leaderboard.WindowPositionY);
        Check.Equal(-1200, loaded.Overlay.WindowPositionX);
        Check.Equal(120, loaded.Overlay.WindowPositionY);
        Check.Equal("#123456", loaded.Overlay.Colors.DeltaEqualText);
        Check.Equal("#ABCDEF", loaded.Overlay.Colors.TimerEqualText);
        Check.True(loaded.Automation.AutoCreate.EnableCheats);
        Check.True(loaded.Automation.AutoCreate.RequireCrimsonBetweenDungeonAndSpawn);
        Check.Equal(AutoCreateCrimsonDistance.Near, loaded.Automation.AutoCreate.CrimsonDistance);
        Check.Equal(AutoCreateJungleRouteDepth.Deep, loaded.Automation.AutoCreate.JungleRouteDepth);
        Check.Equal(AutoCreateResourceFilterItem.BoomstickMask, loaded.Automation.AutoCreate.ResourceFilterItemMask);
        Check.Equal(6, loaded.Automation.AutoCreate.ResourceFilterLifeCrystalMinimum);
        Check.Equal(2, loaded.Automation.AutoCreate.ResourceFilterSpelunkerPotionMinimum);
        Check.Equal(1, loaded.Automation.AutoCreate.ResourceFilterFeatherfallPotionMinimum);
        SplitRouteEntry loadedMultiIconEntry = loaded.Route.SplitRoute.Single(entry => entry.Id == multiIconEntry.Id);
        Check.Equal(customizedIconPath, loadedMultiIconEntry.IconOverride.AllIconFilePaths[customizedTargetId]);
        Check.Equal(1, loadedMultiIconEntry.IconOverride.AllIconFilePaths.Count);
        SplitDefinition loadedMultiIconDefinition = SplitCatalog.Build(loaded)
            .Single(definition => definition.Id == multiIconEntry.Id);
        Check.Equal(customizedIconPath, loadedMultiIconDefinition.IconFileNames[0]);
        Check.Equal(customizedTargetId, loadedMultiIconDefinition.IconKeys[0]);
        Check.True(File.Exists(Path.Combine(paths.SettingsDirectory, "settings.json")));
        Check.True(File.Exists(Path.Combine(paths.SettingsDirectory, "active-profile.txt")));
    }

    private static void CanonicalSettingsDefaults()
    {
        AppSettings constructed = new();
        AppSettings templateClone = AppSettingsDefaults.Create();
        string constructedJson = System.Text.Json.JsonSerializer.Serialize(
            constructed,
            AppSettingsJsonContext.Default.AppSettings);
        string templateJson = System.Text.Json.JsonSerializer.Serialize(
            templateClone,
            AppSettingsJsonContext.Default.AppSettings);
        Check.Equal(templateJson, constructedJson);

        constructed.General.Language = "Changed";
        constructed.Route.SplitRoute.Clear();
        AppSettings freshDefaults = AppSettingsDefaults.Create();
        Check.Equal("English", freshDefaults.General.Language);
        Check.True(freshDefaults.Route.SplitRoute.Count > 0);
    }

    private static void SwitchingRoutesPreservesInactiveReferenceData()
    {
        using var directory = new TestDirectory();
        var paths = new AppContextRuntimeDataPaths(directory.Path);
        var splitSets = new SplitTimeSetRepository(paths);
        var repository = new AppSettingsRepository(paths, splitSets);
        AppSettings settings = AppSettingsDefaults.Create();
        settings.Route.SplitRoute =
        [
            new SplitRouteEntry
            {
                Id = "route-a",
                DisplayName = "Route A",
                Condition = SplitCondition.Fact("event:a")
            }
        ];
        string routeAKey = SplitConditionDataRows.BuildKeys(settings).Single();
        var routeBEntry = new SplitRouteEntry
        {
            Id = "route-b",
            DisplayName = "Route B",
            Condition = SplitCondition.Fact("event:b")
        };
        string routeBKey = SplitConditionDataRows.BuildKeys([routeBEntry]).Single();

        var routeAReference = new ReferenceSplitSet
        {
            Name = "Reference A",
            Splits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [routeAKey] = "00:10"
            }
        };
        var routeBReference = new ReferenceSplitSet
        {
            Name = "Reference B",
            Splits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [routeBKey] = "00:20"
            }
        };
        settings.Comparison.ReferenceSplitSets = [routeAReference, routeBReference];
        settings.Comparison.ActiveReferenceSplitSet = routeAReference.Name;

        SettingsNormalizer.Normalize(settings);
        Check.Equal("00:20", routeBReference.Splits[routeBKey]);
        Check.True(repository.Save(settings).Succeeded);
        Check.Equal(
            "00:20",
            splitSets.LoadReferenceSets().Single(set => set.Name == routeBReference.Name).Splits[routeBKey]);

        settings.Route.SplitRoute = [routeBEntry];
        SettingsNormalizer.Normalize(settings);
        Check.Equal("00:10", routeAReference.Splits[routeAKey]);
        Check.Equal("00:20", routeBReference.Splits[routeBKey]);

        settings.Comparison.ActiveReferenceSplitSet = routeBReference.Name;

        SettingsNormalizer.Normalize(settings);
        Check.Equal("00:20", routeBReference.Splits[routeBKey]);
        Check.Equal("00:10", routeAReference.Splits[routeAKey]);
        Check.True(repository.Save(settings).Succeeded);
        Check.Equal(
            "00:10",
            splitSets.LoadReferenceSets().Single(set => set.Name == routeAReference.Name).Splits[routeAKey]);
    }

    private static void PersonalBestModePreservesReferenceEdits()
    {
        using var directory = new TestDirectory();
        var paths = new AppContextRuntimeDataPaths(directory.Path);
        var repository = new AppSettingsRepository(paths);
        AppSettings settings = AppSettingsDefaults.Create();
        settings.Comparison.UsePersonalBestAsReferenceTime = true;
        settings.Comparison.ReferenceSplitSets =
        [
            new ReferenceSplitSet
            {
                Name = "Edited WR",
                Splits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["split:edited"] = "00:30"
                }
            }
        ];
        settings.Comparison.ActiveReferenceSplitSet = "Edited WR";

        Check.True(repository.Save(settings).Succeeded);

        AppSettings loaded = new AppSettingsRepository(paths).Load();
        ReferenceSplitSet edited = loaded.Comparison.ReferenceSplitSets
            .Single(set => set.Name == "Edited WR");
        Check.Equal("00:30", edited.Splits["split:edited"]);
        Check.True(loaded.Comparison.UsePersonalBestAsReferenceTime);
    }

    private static void SplitSetWriteFailureIsReported()
    {
        using var directory = new TestDirectory();
        var paths = new AppContextRuntimeDataPaths(directory.Path);
        Directory.CreateDirectory(paths.DataDirectory);
        File.WriteAllText(paths.ReferenceTimesDirectory, "blocks directory creation");
        var repository = new AppSettingsRepository(paths);

        OperationResult result = repository.Save(AppSettingsDefaults.Create());

        Check.True(result.Failed);
        Check.True(result.Message.Contains(
            "reference split time set",
            StringComparison.OrdinalIgnoreCase));
        Check.False(File.Exists(Path.Combine(paths.SettingsDirectory, "settings.json")));
    }

    private static void AdvancedFilterEligibility()
    {
        var valid = new AutoCreateWorldSettings
        {
            WorldSize = AutoCreateWorldSize.Small,
            WorldEvil = AutoCreateWorldEvil.Crimson,
            EnableCheats = true,
            EnablePyramidFilter = true,
            RequireCrimsonBetweenDungeonAndSpawn = true,
            JungleRouteDepth = AutoCreateJungleRouteDepth.Deep,
            ResourceFilterItemMask = AutoCreateResourceFilterItem.BoomstickMask,
            ResourceFilterLifeCrystalMinimum = 5
        };
        SettingsSectionNormalizer.NormalizeAutoCreate(valid);
        Check.True(AutoCreateAdvancedFilterEligibility.IsEligible(valid));
        Check.True(valid.RequireCrimsonBetweenDungeonAndSpawn);
        Check.Equal(AutoCreateJungleRouteDepth.Deep, valid.JungleRouteDepth);

        foreach ((Action<AutoCreateWorldSettings> makeUnsupported, bool pyramidRemainsEnabled) in new (Action<AutoCreateWorldSettings>, bool)[]
        {
            (settings => settings.WorldSize = AutoCreateWorldSize.Medium, true),
            (settings => settings.WorldEvil = AutoCreateWorldEvil.Corruption, true),
            (settings => settings.SpecialSeeds = AutoCreateSpecialWorldSeed.NotTheBees, true),
            (settings => settings.SecretSeeds = "secret", true),
            (settings => settings.FixedSeed = "  12345  ", false)
        })
        {
            var unsupported = new AutoCreateWorldSettings
            {
                WorldSize = AutoCreateWorldSize.Small,
                WorldEvil = AutoCreateWorldEvil.Crimson,
                EnableCheats = true,
                EnablePyramidFilter = true,
                RequireCrimsonBetweenDungeonAndSpawn = true,
                JungleRouteDepth = AutoCreateJungleRouteDepth.VeryDeep,
                ResourceFilterItemMask = AutoCreateResourceFilterItem.FeralClawsMask,
                ResourceFilterLifeCrystalMinimum = 5,
                ResourceFilterSpelunkerPotionMinimum = 3,
                ResourceFilterFeatherfallPotionMinimum = 3
            };
            makeUnsupported(unsupported);
            SettingsSectionNormalizer.NormalizeAutoCreate(unsupported);
            Check.False(AutoCreateAdvancedFilterEligibility.IsEligible(unsupported));
            Check.Equal(pyramidRemainsEnabled, unsupported.EnablePyramidFilter);
            if (!pyramidRemainsEnabled)
            {
                Check.Equal("12345", unsupported.FixedSeed);
            }
            Check.False(unsupported.RequireCrimsonBetweenDungeonAndSpawn);
            Check.Equal(AutoCreateJungleRouteDepth.None, unsupported.JungleRouteDepth);
            Check.Equal(0, unsupported.ResourceFilterItemMask);
            Check.Equal(0, unsupported.ResourceFilterLifeCrystalMinimum);
            Check.Equal(0, unsupported.ResourceFilterSpelunkerPotionMinimum);
            Check.Equal(0, unsupported.ResourceFilterFeatherfallPotionMinimum);
        }
    }

    private static void SplitAndRunStorageFlow()
    {
        using var directory = new TestDirectory();
        var paths = new AppContextRuntimeDataPaths(directory.Path);
        var splitSets = new SplitTimeSetRepository(paths);
        splitSets.SaveReferenceSets(
        [
            new ReferenceSplitSet
            {
                Name = "Route A",
                Splits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["split:a"] = "00:04" }
            }
        ]);
        Check.Equal("Route A", splitSets.LoadReferenceSets().Single().Name);

        var stats = new RunStatsRepository(splitSets);
        stats.RecordRun(
        [
            new SplitStatusSnapshot(
                new SplitDefinition("split:a", "A", SplitCondition.Fact("a"), [], [], []),
                TimeSpan.FromSeconds(4), false, [])
        ]);
        RunStats loaded = stats.Load();
        Check.Equal(TimeText.FormatRecord(TimeSpan.FromSeconds(4)), loaded.LastRunSplits["split:a"]);
        Check.Equal(1, Directory.EnumerateFiles(paths.LastRunTimesDirectory, "*.json").Count());
    }

    private static void CorruptSettingsRecovery()
    {
        using var directory = new TestDirectory();
        var paths = new AppContextRuntimeDataPaths(directory.Path);
        Directory.CreateDirectory(paths.SettingsDirectory);
        File.WriteAllText(Path.Combine(paths.SettingsDirectory, "settings.json"), "{ this is not json");

        var repository = new AppSettingsRepository(paths);
        AppSettings loaded = repository.Load();
        Check.True(loaded.Route.SplitRoute.Count > 0);
        Check.True(repository.SettingsPath.StartsWith(paths.SettingsDirectory, StringComparison.OrdinalIgnoreCase));
        Check.True(repository.Save(loaded).Succeeded);
    }
}
