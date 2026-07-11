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

        Check.True(repository.Save(settings).Succeeded);
        AppSettings loaded = new AppSettingsRepository(paths).Load();
        Check.Equal("中文", loaded.General.Language);
        Check.True(loaded.General.AlwaysOnTop);
        Check.True(loaded.Route.VisibleGroupCountLimit > 0);
        Check.Equal("https://example.test/race", loaded.Race.ServerUrl);
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
