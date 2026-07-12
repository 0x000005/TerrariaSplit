namespace TerrariaSplit.Tests;

internal static class WindowsFlowTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Async("settings window exposes About last, displays the executable version and owns update cancellation", TestSuite.Windows, AboutPageJourney);
        yield return TestCase.Async("settings window opens every page and produces a normalized draft without mutating the source", TestSuite.Windows, SettingsDraftJourney);
        yield return TestCase.Sync("overlay restores a visible multi-monitor position and keeps dense layouts inside composite bounds", TestSuite.Windows, OverlayLayoutJourney);
        yield return TestCase.Sync("hotkey settings normalize modifiers and fall back when keys are unsafe", TestSuite.Windows, HotkeyJourney);
    }

    private static Task AboutPageJourney(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        using var service = new FakeUpdateService(new Version(9, 8, 7, 6));
        using var form = new SettingsForm(new AppSettings(), applicationUpdateService: service);
        Check.Equal(SettingsPageId.About, form.PageHost.Pages.Last().Id);
        AboutSettingsPage page = form.PageHost.GetOrCreatePage<AboutSettingsPage>(SettingsPageId.About);
        Check.Equal("9.8.7.6", page.DisplayedVersion);
        form.Dispose();
        Check.True(service.Disposed);
    }, cancellationToken);

    private static Task SettingsDraftJourney(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        AppSettings source = AppSettingsDefaults.Create();
        source.General.Language = "English";
        using var form = new SettingsForm(source, applicationUpdateService: new FakeUpdateService(new Version(1, 0, 0, 0)));
        foreach (SettingsPageId pageId in form.PageHost.Pages.Select(page => page.Id))
        {
            form.PageHost.Select(pageId);
            Check.True(form.PageHost.IsCreated(pageId));
        }
        AppSettings draft = form.PageHost.CreateAppliedSnapshot();
        Check.False(ReferenceEquals(source, draft));
        Check.Equal("English", draft.General.Language);
        Check.True(draft.Route.SplitRoute.Count > 0);
        Check.Equal("English", source.General.Language);
        AutomationSettingsPage automation = form.PageHost.GetOrCreatePage<AutomationSettingsPage>(SettingsPageId.Automation);
        Check.True(automation.AutoCreateCrimsonBetweenDungeonAndSpawnBox.Enabled);
        automation.AutoCreateCrimsonBetweenDungeonAndSpawnBox.Checked = true;
        AppSettings filteredDraft = form.PageHost.CreateAppliedSnapshot();
        Check.True(filteredDraft.Automation.AutoCreate.RequireCrimsonBetweenDungeonAndSpawn);
        Check.False(source.Automation.AutoCreate.RequireCrimsonBetweenDungeonAndSpawn);
    }, cancellationToken);

    private static void OverlayLayoutJourney()
    {
        var settings = AppSettingsDefaults.Create();
        int initialHeight = OverlayCompositeLayoutCalculator.GetFittingHeight(900, 700, settings, 12, 5, 9);
        var initial = new System.Drawing.Rectangle(100, 200, 900, initialHeight);
        Check.True(OverlayCompositeLayoutCalculator.TryCreate(initial, settings, 12, 5, 9, out OverlayCompositeLayout layout));
        Check.True(new System.Drawing.Rectangle(System.Drawing.Point.Empty, initial.Size).Contains(layout.StatusLocalBounds));
        Check.True(new System.Drawing.Rectangle(System.Drawing.Point.Empty, initial.Size).Contains(layout.TimerLocalBounds));
        Check.True(layout.TimerLocalBounds.Contains(layout.Layout.TimerRect));

        int fittingHeight = OverlayCompositeLayoutCalculator.GetFittingHeight(900, 300, settings, 15, 15, 9);
        Check.True(fittingHeight >= 300);
        Check.True(OverlayCompositeLayoutCalculator.TryCreate(new System.Drawing.Rectangle(0, 0, 900, fittingHeight), settings, 15, 15, 9, out _));
        Check.False(OverlayCompositeLayoutCalculator.TryCreate(new System.Drawing.Rectangle(0, 0, 0, 700), settings, 5, 5, 9, out _));

        var primaryWorkingArea = new System.Drawing.Rectangle(0, 0, 1920, 1080);
        var secondaryWorkingArea = new System.Drawing.Rectangle(-1280, 0, 1280, 1024);
        var overlaySize = new System.Drawing.Size(800, 600);
        Check.Equal(
            new System.Drawing.Point(-1200, 120),
            OverlayWindowPlacement.Resolve(
                overlaySize,
                -1200,
                120,
                primaryWorkingArea,
                [primaryWorkingArea, secondaryWorkingArea]));
        Check.Equal(
            new System.Drawing.Point(560, 240),
            OverlayWindowPlacement.Resolve(
                overlaySize,
                5000,
                5000,
                primaryWorkingArea,
                [primaryWorkingArea, secondaryWorkingArea]));
    }

    private static void HotkeyJourney()
    {
        var settings = new AppSettings();
        settings.Hotkeys.PauseResumeKey = "Control, Shift, F10";
        settings.Hotkeys.ResetKey = "ControlKey";
        settings.Hotkeys.CreateWorldKey = "None";
        Check.Equal(System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.F10,
            TerrariaSplit.UI.Input.AppSettingsHotkeys.GetPauseResumeKeys(settings));
        Check.Equal(System.Windows.Forms.Keys.F6, TerrariaSplit.UI.Input.AppSettingsHotkeys.GetResetKeys(settings));
        Check.Equal(System.Windows.Forms.Keys.None, TerrariaSplit.UI.Input.AppSettingsHotkeys.GetCreateWorldKeys(settings));
        Check.Equal("Ctrl + Shift + F10", TerrariaSplit.UI.Input.HotkeyKeyValidator.Format(
            TerrariaSplit.UI.Input.AppSettingsHotkeys.GetPauseResumeKeys(settings)));
    }

    private sealed class FakeUpdateService(Version version) : IApplicationUpdateService
    {
        public Version CurrentVersion { get; } = version;
        public bool Disposed { get; private set; }
        public Task<ApplicationUpdateCheckResult> CheckAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PreparedApplicationUpdate> PrepareAsync(ApplicationUpdateRelease release, IProgress<ApplicationUpdateProgress>? progress, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Dispose() => Disposed = true;
    }
}
