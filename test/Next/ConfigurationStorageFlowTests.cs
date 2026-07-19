namespace TerrariaSplit.Tests;

internal static class ConfigurationStorageFlowTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("settings save, profile selection and reload preserve normalized user choices", TestSuite.Flow, SettingsRoundTrip);
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
        settings.Race.WorldSetup.LifeCrystalMinimum = 5;
        settings.Race.Voice.Enabled = true;
        settings.Race.Voice.VoiceName = "  Test Voice  ";
        settings.Race.Voice.SpeedPercent = 250;
        settings.Race.Voice.Volume = -5;
        settings.Overlay.WindowPositionX = -1200;
        settings.Overlay.WindowPositionY = 120;
        settings.Automation.AutoCreate.EnableCheats = true;
        settings.Automation.AutoCreate.RequireCrimsonBetweenDungeonAndSpawn = true;
        settings.Automation.AutoCreate.CrimsonDistance = AutoCreateCrimsonDistance.Near;
        settings.Automation.AutoCreate.JungleRouteDepth = AutoCreateJungleRouteDepth.Deep;
        settings.Automation.AutoCreate.ResourceFilterItemMask = AutoCreateResourceFilterItem.BoomstickMask;
        settings.Automation.AutoCreate.ResourceFilterLifeCrystalMinimum = 8;
        settings.Automation.AutoCreate.ResourceFilterSpelunkerPotionMinimum = 2;
        settings.Automation.AutoCreate.ResourceFilterFeatherfallPotionMinimum = 1;

        Check.True(repository.Save(settings).Succeeded);
        AppSettings loaded = new AppSettingsRepository(paths).Load();
        Check.Equal("中文", loaded.General.Language);
        Check.True(loaded.General.AlwaysOnTop);
        Check.True(loaded.Route.VisibleGroupCountLimit > 0);
        Check.Equal("https://example.test/race", loaded.Race.ServerUrl);
        Check.Equal("ABC123", loaded.Race.LastRoomCode);
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
        Check.Equal(5, loaded.Race.WorldSetup.LifeCrystalMinimum);
        Check.True(loaded.Race.Voice.Enabled);
        Check.Equal("Test Voice", loaded.Race.Voice.VoiceName);
        Check.Equal(200, loaded.Race.Voice.SpeedPercent);
        Check.Equal(0, loaded.Race.Voice.Volume);
        Check.Equal(-1200, loaded.Overlay.WindowPositionX);
        Check.Equal(120, loaded.Overlay.WindowPositionY);
        Check.True(loaded.Automation.AutoCreate.EnableCheats);
        Check.True(loaded.Automation.AutoCreate.RequireCrimsonBetweenDungeonAndSpawn);
        Check.Equal(AutoCreateCrimsonDistance.Near, loaded.Automation.AutoCreate.CrimsonDistance);
        Check.Equal(AutoCreateJungleRouteDepth.Deep, loaded.Automation.AutoCreate.JungleRouteDepth);
        Check.Equal(AutoCreateResourceFilterItem.BoomstickMask, loaded.Automation.AutoCreate.ResourceFilterItemMask);
        Check.Equal(5, loaded.Automation.AutoCreate.ResourceFilterLifeCrystalMinimum);
        Check.Equal(2, loaded.Automation.AutoCreate.ResourceFilterSpelunkerPotionMinimum);
        Check.Equal(1, loaded.Automation.AutoCreate.ResourceFilterFeatherfallPotionMinimum);
        Check.True(File.Exists(Path.Combine(paths.SettingsDirectory, "settings.json")));
        Check.True(File.Exists(Path.Combine(paths.SettingsDirectory, "active-profile.txt")));
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
