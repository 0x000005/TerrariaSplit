using System.Text.Json;
using System.Windows;
using Button = System.Windows.Controls.Button;
using TerrariaSplit.Race.Client;
using TerrariaSplit.UI.Wpf.Settings;
using TerrariaSplit.UI.Wpf.Windows;

namespace TerrariaSplit.Tests;

internal static class RoutePresetTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("named routes persist externally with independent profile selection and complete icon settings", TestSuite.Flow, ProfileRoundTrip);
        yield return TestCase.Sync("named routes preserve shared edits across stale profile saves and detect competing edits", TestSuite.Flow, SharedRouteRevisions);
        yield return TestCase.Sync("named routes preserve embedded profiles and recover from missing and corrupt route files", TestSuite.Flow, EmbeddedProfiles);
        yield return TestCase.Sync("named routes retain inactive PB and effects without leaking the catalog into race payloads", TestSuite.Flow, InactiveDataAndRace);
        yield return TestCase.Sync("named route write failures prevent creating the selected profile", TestSuite.Flow, WriteFailure);
        yield return TestCase.Sync("named empty routes stay empty across normalization switching saving and reloading", TestSuite.Flow, EmptyRouteRoundTrip);
        yield return TestCase.Async("named route editor creates empty routes and switches drafts while refreshing dependent pages", TestSuite.Windows, EditorJourney);
        yield return TestCase.Async("named route editor blocks invalid switches and cancel leaves route files unchanged", TestSuite.Windows, ValidationAndCancel);
    }

    private static AppSettings Settings(string name = "Original", string id = "split:shared")
    {
        AppSettings settings = AppSettingsDefaults.Create();
        settings.Route.SplitRoute =
        [
            new SplitRouteEntry
            {
                Id = id, DisplayName = name, Enabled = true,
                Condition = SplitCondition.All([
                    SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer),
                    SplitCatalog.CreateBossFactCondition(SplitCatalog.Twins)]),
                UseAdvancedConditionEditor = true, ExpandDetails = false,
                IconOverride = new SplitIconOverride
                {
                    AllIconFilePaths = new Dictionary<string, string> { [SplitCatalog.Destroyer] = @"C:\icons\custom.png" }
                }
            }
        ];
        SettingsNormalizer.Normalize(settings);
        return settings;
    }

    private static void ProfileRoundTrip()
    {
        using var directory = new TestDirectory();
        var paths = new AppContextRuntimeDataPaths(directory.Path);
        var repository = new AppSettingsRepository(paths);
        Check.True(repository.Save(Settings()).Succeeded);
        AppSettings first = repository.Load();
        Check.Equal("Default", first.Route.ActiveRouteName);
        Check.True(File.Exists(Path.Combine(paths.RoutesDirectory, "Default.json")));
        using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(repository.SettingsPath)))
        {
            JsonElement route = document.RootElement.GetProperty("Settings").GetProperty("Route");
            Check.Equal("Default", route.GetProperty("ActiveRouteName").GetString());
            Check.False(route.TryGetProperty("SplitRoute", out _));
            Check.False(route.TryGetProperty("Presets", out _));
        }

        var secondRepository = new AppSettingsRepository(paths);
        AppSettings second = secondRepository.Load("second.json");
        Check.Equal("Default", second.Route.ActiveRouteName);
        Check.Equal("Original", second.Route.SplitRoute.Single().DisplayName);
        Check.True(RoutePresetService.Add(second.Route, "Practice"));
        Check.Equal(0, second.Route.SplitRoute.Count);
        second.Route.SplitRoute = Settings("Practice only").Route.SplitRoute;
        second.Route.VisibleGroupCountLimit = 2;
        second.Overlay.Columns.Time.FontFamily = "Arial";
        Check.True(secondRepository.Save(second).Succeeded);

        AppSettings reloadedFirst = repository.Load("settings.json");
        AppSettings reloadedSecond = secondRepository.Load("second.json");
        Check.Equal("Default", reloadedFirst.Route.ActiveRouteName);
        Check.Equal("Practice", reloadedSecond.Route.ActiveRouteName);
        Check.Equal("Original", reloadedFirst.Route.SplitRoute.Single().DisplayName);
        Check.Equal("Practice only", reloadedSecond.Route.SplitRoute.Single().DisplayName);
        Check.Equal(2, reloadedSecond.Route.VisibleGroupCountLimit);
        Check.False(reloadedFirst.Route.VisibleGroupCountLimit == 2);
        Check.Equal("Arial", reloadedSecond.Overlay.Columns.Time.FontFamily);
        SplitRouteEntry entry = reloadedSecond.Route.SplitRoute.Single();
        Check.True(entry.UseAdvancedConditionEditor);
        Check.False(entry.ExpandDetails);
        Check.Equal(@"C:\icons\custom.png", entry.IconOverride.AllIconFilePaths[SplitCatalog.Destroyer]);
        Check.Sequence([@"C:\icons\custom.png", "twins.png"], SplitCatalog.Build(reloadedSecond).Single().IconFileNames);
        Check.Equal("Original", reloadedSecond.Route.Presets.Single(preset => preset.Name == "Default").SplitRoute.Single().DisplayName);
    }

    private static void SharedRouteRevisions()
    {
        using var directory = new TestDirectory();
        var paths = new AppContextRuntimeDataPaths(directory.Path);
        var writer = new AppSettingsRepository(paths);
        Check.True(writer.Save(Settings()).Succeeded);
        AppSettings edited = writer.Load();
        var staleRepository = new AppSettingsRepository(paths);
        AppSettings stale = staleRepository.Load("second.json");

        edited.Route.SplitRoute.Single().DisplayName = "Shared edit";
        Check.True(writer.Save(AppSettingsCloner.Clone(edited)).Succeeded);
        stale.General.AlwaysOnTop = !stale.General.AlwaysOnTop;
        Check.True(staleRepository.Save(stale).Succeeded);
        Check.Equal("Shared edit", staleRepository.Load("second.json").Route.SplitRoute.Single().DisplayName);

        // A later effect can carry the original loaded fingerprint. Reverting the
        // actual edit must still be saved after the first successful write.
        edited.Route.SplitRoute.Single().DisplayName = "Original";
        Check.True(writer.Save(AppSettingsCloner.Clone(edited)).Succeeded);
        Check.Equal("Original", new AppSettingsRepository(paths).Load("settings.json").Route.SplitRoute.Single().DisplayName);

        AppSettings competing = staleRepository.Load("second.json");
        edited.Route.SplitRoute.Single().DisplayName = "Writer again";
        Check.True(writer.Save(edited).Succeeded);
        competing.Route.SplitRoute.Single().DisplayName = "Stale edit";
        string profileBefore = File.ReadAllText(staleRepository.SettingsPath);
        Check.True(staleRepository.Save(competing).Failed);
        Check.Equal(profileBefore, File.ReadAllText(staleRepository.SettingsPath));
        Check.Equal("Writer again", new AppSettingsRepository(paths).Load("settings.json").Route.SplitRoute.Single().DisplayName);
    }

    private static void EmptyRouteRoundTrip()
    {
        using var directory = new TestDirectory();
        var paths = new AppContextRuntimeDataPaths(directory.Path);
        var repository = new AppSettingsRepository(paths);
        Check.True(repository.Save(Settings()).Succeeded);
        AppSettings settings = repository.Load();
        Check.True(RoutePresetService.Add(settings.Route, "Empty"));
        Check.Equal(0, settings.Route.SplitRoute.Count);
        SettingsNormalizer.Normalize(settings);
        Check.Equal(0, settings.Route.SplitRoute.Count);
        Check.Equal(0, SplitCatalog.Build(settings).Count);
        Check.True(repository.Save(settings).Succeeded);
        AppSettings loaded = repository.Load();
        Check.Equal("Empty", loaded.Route.ActiveRouteName);
        Check.Equal(0, loaded.Route.SplitRoute.Count);
        Check.Equal(0, new ApplicationController(loaded, new SettingsSnapshotFactory()).Definitions.Count);
        Check.True(RoutePresetService.Select(loaded.Route, "Default"));
        Check.Equal("Original", loaded.Route.SplitRoute.Single().DisplayName);
        Check.True(RoutePresetService.Select(loaded.Route, "Empty"));
        Check.Equal(0, loaded.Route.SplitRoute.Count);
        Check.True(repository.Save(loaded).Succeeded);
        Check.Equal(0, new AppSettingsRepository(paths).Load().Route.SplitRoute.Count);
    }

    private static void WriteFailure()
    {
        using var directory = new TestDirectory();
        var paths = new AppContextRuntimeDataPaths(directory.Path);
        Check.True(JsonFileStore.TryWriteText(paths.RoutesDirectory, "blocked", "fixture").Succeeded);
        var repository = new AppSettingsRepository(paths);
        Check.True(repository.Save(Settings()).Failed);
        Check.False(File.Exists(repository.SettingsPath));
    }

    private static void EmbeddedProfiles()
    {
        using var directory = new TestDirectory();
        var paths = new AppContextRuntimeDataPaths(directory.Path);
        Directory.CreateDirectory(paths.SettingsDirectory);
        string firstPath = Path.Combine(paths.SettingsDirectory, "settings.json");
        string secondPath = Path.Combine(paths.SettingsDirectory, "older.json");
        Check.True(JsonFileStore.TryWrite(firstPath, new SettingsDocument(Settings("First")), "fixture").Succeeded);
        Check.True(JsonFileStore.TryWrite(secondPath, new SettingsDocument(Settings("Second")), "fixture").Succeeded);
        var repository = new AppSettingsRepository(paths);
        Check.Equal("First", repository.Load(firstPath).Route.SplitRoute.Single().DisplayName);
        AppSettings older = repository.Load(secondPath);
        Check.Equal("older", older.Route.ActiveRouteName);
        Check.Equal("Second", older.Route.SplitRoute.Single().DisplayName);
        Check.Equal(2, Directory.GetFiles(paths.RoutesDirectory, "*.json").Length);
        Check.Equal("Second", repository.Load(secondPath).Route.SplitRoute.Single().DisplayName);

        // A second embedded default profile with different content cannot replace
        // the already shared Default file.
        Check.True(JsonFileStore.TryWrite(firstPath, new SettingsDocument(Settings("Recovered")), "fixture").Succeeded);
        AppSettings recovered = repository.Load(firstPath);
        Check.Equal("Default (2)", recovered.Route.ActiveRouteName);
        Check.Equal("First", recovered.Route.Presets.Single(preset => preset.Name == "Default").SplitRoute.Single().DisplayName);

        string selectedPath = Path.Combine(paths.RoutesDirectory, recovered.Route.ActiveRouteName + ".json");
        File.Delete(selectedPath);
        AppSettings missing = repository.Load(firstPath);
        Check.Equal("Default", missing.Route.ActiveRouteName);
        Check.Equal("First", missing.Route.SplitRoute.Single().DisplayName);
        Check.True(JsonFileStore.TryWriteText(Path.Combine(paths.RoutesDirectory, "broken.json"), "{", "fixture").Succeeded);
        Check.False(repository.Load(firstPath).Route.Presets.Any(preset => preset.Name == "broken"));
        Check.Equal("{", File.ReadAllText(Path.Combine(paths.RoutesDirectory, "broken.json")));
        Check.False(RoutePresetService.IsValidName("../escape"));
        Check.False(RoutePresetService.IsValidName("CON"));
        Check.False(RoutePresetService.IsValidName("trailing."));
    }

    private static void InactiveDataAndRace()
    {
        using var directory = new TestDirectory();
        var paths = new AppContextRuntimeDataPaths(directory.Path);
        var repository = new AppSettingsRepository(paths);
        AppSettings settings = Settings();
        RoutePresetService.CaptureActive(settings.Route);
        string firstKey = SplitConditionDataRows.BuildKeys(settings).First();
        settings.Comparison.PersonalBestTimes[firstKey] = "0:10.25";
        settings.Comparison.PersonalBestSegmentTimes["split:shared"] = "0:10.25";
        settings.Overlay.SplitCompletionOutlineSplitStyles["split:shared"] = SplitCompletionOutlineStyles.Aurora;
        PersonalBestSetService.SyncActivePersonalBestTimeSetFromDictionary(settings);
        PersonalBestSetService.SyncActivePersonalBestSegmentSetFromDictionary(settings);
        Check.True(RoutePresetService.Add(settings.Route, "Private route"));
        settings.Route.SplitRoute = Settings("Other", "split:other").Route.SplitRoute;
        RoutePresetService.CaptureActive(settings.Route);
        SettingsNormalizer.Normalize(settings);
        Check.Equal("0:10.25", settings.Comparison.PersonalBestTimes[firstKey]);
        Check.Equal("0:10.25", settings.Comparison.PersonalBestSegmentTimes["split:shared"]);
        Check.Equal(SplitCompletionOutlineStyles.Aurora, settings.Overlay.SplitCompletionOutlineSplitStyles["split:shared"]);
        Check.True(repository.Save(settings).Succeeded);
        AppSettings loaded = repository.Load();
        Check.True(RoutePresetService.Select(loaded.Route, "Default"));
        SettingsNormalizer.Normalize(loaded);
        Check.Equal("0:10.25", loaded.Comparison.PersonalBestTimes[firstKey]);
        Check.Equal("0:10.25", loaded.Comparison.PersonalBestSegmentTimes["split:shared"]);

        string localRouteFile = File.ReadAllText(Path.Combine(paths.RoutesDirectory, "Default.json"));
        var controller = new ApplicationController(loaded, new SettingsSnapshotFactory());
        controller.HandleSystemEvent(new ControlCommandSystemEvent(AppCommand.ApplyRouteOverride(new SettingsRouteOverridePackage
        {
            Key = "race", SplitRoute = Settings("Room only", "split:room").Route.SplitRoute
        })));
        RaceRoutePayload payload = RaceRoutePayloadFactory.Create(controller.Settings);
        Check.False(payload.SerializedRouteJson.Contains("Private route", StringComparison.Ordinal));
        Check.False(payload.SerializedRouteJson.Contains("Presets", StringComparison.Ordinal));
        ApplicationUpdate update = controller.HandleSystemEvent(new ControlCommandSystemEvent(AppCommand.ToggleCheats()));
        foreach (SaveSettingsEffect effect in update.Effects.OfType<SaveSettingsEffect>())
        {
            Check.True(repository.Save(effect.Settings).Succeeded);
        }
        Check.Equal(localRouteFile, File.ReadAllText(Path.Combine(paths.RoutesDirectory, "Default.json")));
    }

    private static Task EditorJourney(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        AppSettings source = Settings();
        RoutePresetService.CaptureActive(source.Route);
        Check.True(RoutePresetService.Add(source.Route, "Second"));
        source.Route.SplitRoute = Settings("Second label", "split:other").Route.SplitRoute;
        RoutePresetService.CaptureActive(source.Route);
        Check.True(RoutePresetService.Select(source.Route, "Default"));
        var warnings = new List<string>();
        using var window = new SettingsWindow(source,
            messageBoxPresenter: (_, message, _, _, _) => { warnings.Add(message); return DialogResult.OK; },
            applicationUpdateService: new FakeUpdateService(new Version(1, 0, 0, 0)));
        window.PageHost.Select(SettingsPageId.Data);
        WpfDataSettingsPage data = window.PageHost.GetOrCreatePage<WpfDataSettingsPage>(SettingsPageId.Data);
        string oldKey = data.SplitTextBoxes.Keys.First();
        data.SplitTextBoxes[oldKey].Text = "0:12.34";
        window.PageHost.Select(SettingsPageId.Effects);
        WpfAnimationSettingsPage effects = window.PageHost.GetOrCreatePage<WpfAnimationSettingsPage>(SettingsPageId.Effects);
        window.PageHost.Select(SettingsPageId.Sounds);
        _ = window.PageHost.GetOrCreatePage<WpfSoundSettingsPage>(SettingsPageId.Sounds);
        window.PageHost.Select(SettingsPageId.Splits);
        WpfSplitSettingsPage route = window.PageHost.GetOrCreatePage<WpfSplitSettingsPage>(SettingsPageId.Splits);
        route.SplitNameBoxForTests.Text = "Edited first";
        route.RoutePresetBoxForTests.SelectedItem = "Second";
        Check.Equal("Second label", route.SplitNameBoxForTests.Text);
        Check.True(effects.AnimationOutlineKeysForTests.Contains("split:other"));
        Check.False(effects.AnimationOutlineKeysForTests.Contains("split:shared"));
        Check.False(data.SplitTextBoxes.ContainsKey(oldKey));
        route.RoutePresetBoxForTests.SelectedItem = "Default";
        Check.Equal("Edited first", route.SplitNameBoxForTests.Text);
        Check.Equal("0:12.34", data.SplitTextBoxes[oldKey].Text);
        Check.Equal(@"C:\icons\custom.png", route.AllIconFileBoxesForTests[SplitCatalog.Destroyer].Text);

        route.NewRouteNameBoxForTests.Text = "Copy";
        route.AddRouteButtonForTests.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check.Equal("Copy", route.RoutePresetBoxForTests.SelectedItem as string);
        Check.Equal(0, route.RouteListForTests.Items.Count);
        Check.Equal(0, route.ConditionListForTests.Items.Count);
        Check.Equal(string.Empty, route.SplitNameBoxForTests.Text);
        Check.False(route.SplitNameBoxForTests.IsEnabled);
        Check.False(route.ConditionMatchModeBoxForTests.IsEnabled);
        Check.Equal(0, data.SplitTextBoxes.Count);
        Check.Equal(0, effects.AnimationOutlineKeysForTests.Count);
        Check.True(window.TryApplyForTests(out _));
        Check.Equal(0, window.Result.Route.SplitRoute.Count);
        using (var reopened = new SettingsWindow(window.Result, applicationUpdateService: new FakeUpdateService(new Version(1, 0, 0, 0))))
        {
            var emptyPage = reopened.PageHost.GetOrCreatePage<WpfSplitSettingsPage>(SettingsPageId.Splits);
            Check.Equal(0, emptyPage.RouteListForTests.Items.Count);
            Check.False(emptyPage.SplitNameBoxForTests.IsEnabled);
        }
        route.TargetSearchBoxForTests.Text = "Destroyer";
        route.TargetListForTests.SelectedIndex = 0;
        route.AddTargetToNewGroupButtonForTests.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check.True(route.SplitNameBoxForTests.IsEnabled);
        route.SplitNameBoxForTests.Text = "Copy only";
        route.RoutePresetBoxForTests.SelectedItem = "Default";
        Check.Equal("Edited first", route.SplitNameBoxForTests.Text);
        string[] dataLabels = PageTexts(window, SettingsPageId.Data).ToArray();
        Check.True(SplitConditionDataRows.Build(window.Result).All(row => dataLabels.Contains(row.DisplayName)));
        Check.True(PageTexts(window, SettingsPageId.Effects).Contains("Edited first"));
        Check.True(PageTexts(window, SettingsPageId.Sounds).Any(text => text.StartsWith("Edited first", StringComparison.Ordinal)));
        Check.True(window.TryApplyForTests(out _));
        Check.Equal(0, warnings.Count);
        Check.Equal("Original", source.Route.SplitRoute.Single().DisplayName);
        Check.Equal("Copy only", window.Result.Route.Presets.Single(preset => preset.Name == "Copy").SplitRoute.Single().DisplayName);
        Check.Equal("Default", window.Result.Route.ActiveRouteName);
        Check.Equal("0:12.34", ReferenceSplitSetService.GetActiveReferenceSet(window.Result).Splits[oldKey]);
    }, cancellationToken);

    private static IEnumerable<string> PageTexts(SettingsWindow window, SettingsPageId page) =>
        Texts(window.PageHost.Pages.Single(entry => entry.Id == page).PageElement!);

    private static IEnumerable<string> Texts(DependencyObject element)
    {
        if (element is System.Windows.Controls.TextBlock label)
        {
            yield return label.Text;
        }
        else if (element is TerrariaSplit.UI.Wpf.Controls.SettingRow row)
        {
            yield return row.Title;
        }

        foreach (object child in LogicalTreeHelper.GetChildren(element))
        {
            if (child is DependencyObject dependency)
            {
                foreach (string text in Texts(dependency))
                {
                    yield return text;
                }
            }
        }
    }

    private static Task ValidationAndCancel(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        using var directory = new TestDirectory();
        var paths = new AppContextRuntimeDataPaths(directory.Path);
        var repository = new AppSettingsRepository(paths);
        Check.True(repository.Save(Settings()).Succeeded);
        AppSettings source = repository.Load();
        Check.True(RoutePresetService.Add(source.Route, "Second"));
        source.Route.SplitRoute = Settings().Route.SplitRoute;
        Check.True(repository.Save(source).Succeeded);
        source = repository.Load();
        string before = File.ReadAllText(Path.Combine(paths.RoutesDirectory, "Second.json"));
        int warnings = 0;
        using (var window = new SettingsWindow(source,
            messageBoxPresenter: (_, _, _, _, _) => { warnings++; return DialogResult.OK; },
            applicationUpdateService: new FakeUpdateService(new Version(1, 0, 0, 0))))
        {
            window.PageHost.Select(SettingsPageId.Splits);
            var page = window.PageHost.GetOrCreatePage<WpfSplitSettingsPage>(SettingsPageId.Splits);
            page.AdvancedConditionBoxForTests.Text = "ALL(";
            page.RoutePresetBoxForTests.SelectedItem = "Default";
            Check.Equal("Second", page.RoutePresetBoxForTests.SelectedItem as string);
            Check.Equal(1, warnings);
            page.AdvancedConditionBoxForTests.Text = SplitConditionText.Format(source.Route.SplitRoute.Single().Condition, source.General.Language);
            page.SplitNameBoxForTests.Text = "Canceled";
            page.NewRouteNameBoxForTests.Text = "Canceled copy";
            page.AddRouteButtonForTests.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        Check.Equal(before, File.ReadAllText(Path.Combine(paths.RoutesDirectory, "Second.json")));
        Check.False(File.Exists(Path.Combine(paths.RoutesDirectory, "Canceled copy.json")));
    }, cancellationToken);

    private sealed class FakeUpdateService(Version version) : IApplicationUpdateService
    {
        public Version CurrentVersion { get; } = version;
        public Task<ApplicationUpdateCheckResult> CheckAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PreparedApplicationUpdate> PrepareAsync(ApplicationUpdateRelease release, IProgress<ApplicationUpdateProgress>? progress, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Dispose() { }
    }
}
