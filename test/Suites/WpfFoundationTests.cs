using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Media;
using TerrariaSplit.Race.Contracts;
using TerrariaSplit.UI.Wpf;
using TerrariaSplit.UI.Wpf.Controls;
using TerrariaSplit.UI.Wpf.Dialogs;
using TerrariaSplit.UI.Wpf.Interop;
using TerrariaSplit.UI.Wpf.Theme;
using TerrariaSplit.UI.Wpf.Windows;

namespace TerrariaSplit.Tests;

internal static class WpfFoundationTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Async("wpf theme manager loads all visual and density presets without missing tokens", TestSuite.Windows, ThemeBundlesLoadJourney);
        yield return TestCase.Async("wpf modern window and compact controls enforce contracts and live theme switching", TestSuite.Windows, ModernWindowAndControlsJourney);
        yield return TestCase.Async("wpf modern dialogs, practice palette and statistics window initialize cleanly", TestSuite.Windows, Stage1WindowsJourney);
        yield return TestCase.Async("wpf race window initializes all five pages and reacts to room state updates", TestSuite.Windows, RaceWindowJourney);
        yield return TestCase.Async("wpf management windows and dialogs fit scaled and secondary-screen work areas", TestSuite.Windows, WindowWorkingAreaJourney);
        yield return TestCase.Async("wpf hotkey capture accepts Tab and modifier chords and clears on Escape", TestSuite.Windows, HotkeyCaptureJourney);
        yield return TestCase.Async("wpf statistics totals evaluate the final main condition rather than the final row", TestSuite.Windows, StatisticsTotalsJourney);
        yield return TestCase.Async("wpf modal windows preserve owner layering and restore interaction after closing", TestSuite.Windows, ModalWindowLayerJourney);
    }

    private static Task ThemeBundlesLoadJourney(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        WpfBootstrap.EnsureInitialized();

        foreach (WpfThemePreset preset in Enum.GetValues<WpfThemePreset>())
        {
            foreach (WpfDensityPreset density in Enum.GetValues<WpfDensityPreset>())
            {
                ResourceDictionary bundle = WpfThemeManager.CreateThemeBundle(preset, density);
                Check.True(bundle["WindowBgBrush"] is not null);
                Check.True(bundle["SidebarBgBrush"] is not null);
                Check.True(bundle["SurfaceBrush"] is not null);
                Check.True(bundle["AccentBrush"] is not null);
                Check.True(bundle["ModernWindowStyle"] is not null);
                Check.True(bundle["ToggleSwitchStyle"] is not null);
                Check.True(bundle["ChipToggleStyle"] is not null);
                Check.True(bundle["SegmentedRadioStyle"] is not null);
                Check.True(bundle["NavRadioStyle"] is not null);

                double expectedControlHeight = density == WpfDensityPreset.Compact ? 26d : 30d;
                double expectedTitleBarHeight = density == WpfDensityPreset.Compact ? 34d : 38d;
                Check.Equal(expectedControlHeight, (double)bundle["ControlHeight"]!);
                Check.Equal(expectedTitleBarHeight, (double)bundle["TitleBarHeight"]!);
            }
        }
    }, cancellationToken);

    private static Task ModernWindowAndControlsJourney(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        WpfBootstrap.EnsureInitialized();

        var window = new ModernWindow
        {
            Title = "Foundation Smoke",
            HeaderSubtitle = "Compact",
            Width = 640,
            Height = 400
        };
        Check.Equal(34d, (double)window.FindResource("TitleBarHeight")!);
        Check.Equal(26d, (double)window.FindResource("ControlHeight")!);

        WpfThemeManager.ApplyTheme(window, WpfThemePreset.ShimmerCraft, WpfDensityPreset.Balanced);
        Check.Equal(38d, (double)window.FindResource("TitleBarHeight")!);
        Check.Equal(30d, (double)window.FindResource("ControlHeight")!);
        System.Windows.Media.Color dwmBorder = WpfThemeManager.ResolveDwmBorderColor(window);
        Check.Equal((byte)0x35, dwmBorder.R);
        Check.Equal((byte)0x35, dwmBorder.G);
        Check.Equal((byte)0x3A, dwmBorder.B);

        var stepper = new NumericStepper
        {
            Minimum = 10,
            Maximum = 50,
            DecimalPlaces = 1,
            Value = 99.94
        };
        Check.Equal(50d, stepper.Value);
        stepper.Value = 12.36;
        Check.Equal(12.4, stepper.Value);
        stepper.Value = -5;
        Check.Equal(10d, stepper.Value);

        var hotkeyBox = new HotkeyCaptureBox
        {
            Hotkey = Keys.Control | Keys.F8
        };
        Check.Equal("Ctrl + F8", hotkeyBox.DisplayText);
        hotkeyBox.Hotkey = Keys.ControlKey;
        Check.Equal(Keys.None, hotkeyBox.Hotkey);
        Check.Equal("None", hotkeyBox.DisplayText);

        var swatch = new ColorSwatchButton { ColorHex = "#2DD4BF" };
        var brush = (SolidColorBrush)swatch.SwatchBrush;
        Check.Equal((byte)0x2D, brush.Color.R);
        Check.Equal((byte)0xD4, brush.Color.G);
        Check.Equal((byte)0xBF, brush.Color.B);

        window.Content = new SettingCard
        {
            Header = "General",
            Subtitle = "Compact desktop controls",
            Content = new SettingRow
            {
                Title = "Practice mode",
                Description = "Allow editing split and total times",
                ShowDivider = false,
                Content = hotkeyBox
            }
        };
        window.ApplyTemplate();
    }, cancellationToken);

    private static Task Stage1WindowsJourney(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        WpfBootstrap.EnsureInitialized();
        AppSettings settings = AppSettingsDefaults.Create();

        string longReport = string.Join(Environment.NewLine, Enumerable.Repeat("Diagnostic detail line.", 120));
        var messageDialog = new ModernMessageDialog(
            "Create World",
            longReport,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning,
            static value => value,
            selectableMessage: true);
        Check.Equal(longReport, messageDialog.DisplayedMessage);
        Check.True(messageDialog.HasSelectableMessage);
        Check.True(messageDialog.HasCopyDetailsButton);
        Check.True(messageDialog.Width <= messageDialog.MaxWidth);

        var timeDialog = new ModernTimeEditDialog(settings, "Edit total time", "12:34.56", allowEmpty: false);
        Check.Equal("Edit total time", timeDialog.Title);

        settings.PracticeWorlds.Slots[0].Name = "Pre-Skeletron";
        settings.PracticeWorlds.Slots[0].PlayerFilePath = @"C:\Saves\Runner.plr";
        settings.PracticeWorlds.Slots[0].WorldFilePath = @"C:\Saves\CrimsonSmall.wld";
        var palette = new PracticeWorldPaletteWindow(settings);
        Check.True(palette.SelectedSlot is null);

        using var dir = new TestDirectory("wpf-stats-smoke");
        var timeSetRepo = new SplitTimeSetRepository(new AppContextRuntimeDataPaths(dir.Path));
        var statsRepo = new RunStatsRepository(timeSetRepo);
        var statsWindow = new StatisticsWindow(settings, statsRepo, timeSetRepo);
        Check.True(statsWindow.RowCount > 0);
        Check.True(statsWindow.GroupCount > 0);
        Check.True(statsWindow.MultiRowGroupCount >= 1);
        Check.True(statsWindow.RowCount > statsWindow.GroupCount);
        Check.True(!string.IsNullOrWhiteSpace(statsWindow.PersonalBestDisplayText));
    }, cancellationToken);

    private static Task RaceWindowJourney(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        WpfBootstrap.EnsureInitialized();
        var fakeShell = new FakeRacePanelShell();
        var raceWindow = new RaceWindow(fakeShell);
        Check.Equal(5, raceWindow.PageCount);
        raceWindow.UpdateRaceState(null);
    }, cancellationToken);

    private static Task WindowWorkingAreaJourney(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        foreach (Rect workArea in new[]
                 {
                     new Rect(0, 0, 1366d / 1.5, 728d / 1.5),
                     new Rect(0, 0, 1920d / 2, 1040d / 2),
                     new Rect(-1280, -100, 1280, 720)
                 })
        {
            var window = new ModernWindow
            {
                Width = 1120,
                Height = 700,
                MinWidth = 1020,
                MinHeight = 560,
                Left = workArea.Right - 100,
                Top = workArea.Bottom - 100
            };
            WpfWindowInterop.FitToWorkingArea(window, workArea);
            Check.True(window.Width <= workArea.Width && window.Height <= workArea.Height);
            Check.True(window.MinWidth <= workArea.Width && window.MinHeight <= workArea.Height);
            Check.True(window.Left >= workArea.Left && window.Left + window.Width <= workArea.Right + 0.01);
            Check.True(window.Top >= workArea.Top && window.Top + window.Height <= workArea.Bottom + 0.01);
            window.Close();

            var dialog = new ModernMessageDialog("Review", "Details", MessageBoxButtons.OK,
                MessageBoxIcon.Warning, static value => value, selectableMessage: true)
            {
                Left = workArea.Left + 20,
                Top = workArea.Top + 20
            };
            WpfWindowInterop.FitToWorkingArea(dialog, workArea);
            Check.Equal(workArea.Height, dialog.MaxHeight);
            Check.Equal(workArea.Left + 20, dialog.Left);
            Check.Equal(workArea.Top + 20, dialog.Top);
            dialog.Close();
        }
    }, cancellationToken);

    private static Task HotkeyCaptureJourney(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        var capture = new HotkeyCaptureBox { Hotkey = Keys.F8 };
        int captured = 0;
        capture.HotkeyCaptured += (_, _) => captured++;
        capture.CaptureKey(System.Windows.Input.Key.Tab, System.Windows.Input.ModifierKeys.None);
        Check.Equal(Keys.Tab, capture.Hotkey);
        capture.CaptureKey(System.Windows.Input.Key.Tab, System.Windows.Input.ModifierKeys.Shift);
        Check.Equal(Keys.Shift | Keys.Tab, capture.Hotkey);
        capture.CaptureKey(System.Windows.Input.Key.F8, System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt);
        Check.Equal(Keys.Control | Keys.Alt | Keys.F8, capture.Hotkey);
        capture.CaptureKey(System.Windows.Input.Key.LeftCtrl, System.Windows.Input.ModifierKeys.Control);
        Check.Equal(3, captured);
        capture.CaptureKey(System.Windows.Input.Key.Escape, System.Windows.Input.ModifierKeys.None);
        Check.Equal(Keys.None, capture.Hotkey);
        Check.Equal(4, captured);
    }, cancellationToken);

    private static Task StatisticsTotalsJourney(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        foreach ((string mode, int expectedMinutes) in new[] { ("All", 20), ("Any", 10), ("AtLeast", 15) })
        {
            AppSettings settings = AppSettingsDefaults.Create();
            List<SplitCondition> facts = settings.Route.SplitRoute.Take(3)
                .Select(entry => entry.Condition.ToFlatGroup().GetFactConditions().First().Clone()).ToList();
            SplitCondition condition = mode switch
            {
                "All" => SplitCondition.All(facts),
                "Any" => SplitCondition.Any(facts),
                _ => SplitCondition.AtLeast(facts, 2)
            };
            settings.Route.SplitRoute =
            [
                new SplitRouteEntry { Id = "review-final", DisplayName = "Final", Enabled = true, Condition = condition },
                new SplitRouteEntry { Id = "disabled", Enabled = false, Condition = facts[0].Clone() }
            ];
            IReadOnlyList<SplitConditionDataRow> rows = SplitConditionDataRows.Build(settings);
            settings.Comparison.PersonalBestTimes.Clear();
            var reference = new ReferenceSplitSet { Name = "Review" };
            var personal = new Dictionary<string, string>();
            int[] minutes = [20, 15, 10];
            for (int i = 0; i < rows.Count; i++)
            {
                settings.Comparison.PersonalBestTimes[rows[i].Key] = TimeText.FormatRecord(TimeSpan.FromMinutes(minutes[i]));
                reference.Splits[rows[i].Key] = TimeText.FormatRecord(TimeSpan.FromMinutes(minutes[i] + 10));
                personal[rows[i].Key] = TimeText.FormatRecord(TimeSpan.FromMinutes(minutes[i] + 2));
            }

            settings.Comparison.ReferenceSplitSets = [reference];
            settings.Comparison.ActiveReferenceSplitSet = reference.Name;
            settings.Comparison.UsePersonalBestAsReferenceTime = false;
            using var dir = new TestDirectory("wpf-statistics-totals");
            var timeSets = new SplitTimeSetRepository(new AppContextRuntimeDataPaths(dir.Path));
            timeSets.SaveLastRun(personal);
            var window = new StatisticsWindow(settings, new RunStatsRepository(timeSets), timeSets);
            Check.Equal(TimeText.FormatRecord(TimeSpan.FromMinutes(expectedMinutes)), window.PersonalBestDisplayText);
            Check.Equal(TimeText.FormatRecord(TimeSpan.FromMinutes(expectedMinutes + 2)), window.SelectedTotalDisplayText);
            Check.Equal(TimeText.FormatRecord(TimeSpan.FromMinutes(expectedMinutes + 10)), window.ReferenceTotalDisplayText);
            Check.Equal("-8:00.0", window.TotalDeltaDisplayText);
            window.Close();

            if (mode == "All")
            {
                settings.Comparison.PersonalBestTimes.Remove(rows[0].Key);
                var incomplete = new StatisticsWindow(settings, new RunStatsRepository(timeSets), timeSets);
                Check.Equal("--", incomplete.PersonalBestDisplayText);
                incomplete.Close();
            }
        }
    }, cancellationToken);

    private static Task ModalWindowLayerJourney(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        using var owner = new System.Windows.Forms.Form();
        _ = owner.Handle;
        var coordinator = new ProgramModalWindowCoordinator(owner, _ => { }, () => IntPtr.Zero);
        coordinator.SetAlwaysOnTop(true);
        foreach (ModalWindowOptions options in new[] { ModalWindowOptions.Default, ModalWindowOptions.ForceTopMostForeground })
        {
            var dialog = new ModernMessageDialog("Layer test", "Layer test", MessageBoxButtons.OK,
                MessageBoxIcon.Warning, static value => value)
            {
                ShowActivated = false,
                Opacity = 0
            };
            bool actualTopMost = false;
            bool ownerDisabled = false;
            IntPtr actualOwner = IntPtr.Zero;
            dialog.Loaded += (_, _) =>
            {
                actualTopMost = (GetWindowLongPtr(dialog.Handle, -20).ToInt64() & 8) != 0;
                ownerDisabled = !IsWindowEnabled(owner.Handle);
                actualOwner = GetWindow(dialog.Handle, 4);
                _ = dialog.Dispatcher.BeginInvoke(new Action(dialog.Close));
            };
            _ = coordinator.ShowWpfDialog(dialog, options);
            Check.Equal(options.ForceTopMost, actualTopMost);
            Check.Equal(owner.Handle, actualOwner);
            Check.True(ownerDisabled);
            Check.True(IsWindowEnabled(owner.Handle));
            Check.True((GetWindowLongPtr(owner.Handle, -20).ToInt64() & 8) != 0);
            Check.False(coordinator.HasModalWindow);
        }
    }, cancellationToken);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(IntPtr window);

    private sealed class FakeRacePanelShell : IRacePanelShell
    {
        public RaceRoomState? State => null;
        public bool IsRaceEnabled => false;
        public RacePanelDraftState DraftState { get; private set; } = new("http://127.0.0.1:5000", "Runner", string.Empty, string.Empty, string.Empty, string.Empty, RacePanelRole.Host, RacePanelWorldSource.Random);
        public RaceServerConnectionStatus ServerConnectionStatus => RaceServerConnectionStatus.Disconnected;
        public string? LocalNickname => "Runner";
        public bool IsHostInCurrentRoom => false;
        public string? LocalWorldPath => null;
        public RaceLeaderboardSettings LeaderboardSettings { get; private set; } = new();
        public RaceBossPenaltySettings BossPenaltySettings { get; private set; } = new();
        public RaceVoiceSettings VoiceSettings { get; private set; } = new();
        public IReadOnlyList<RaceVoiceOption> InstalledVoices => [];
        public string Localize(string key) => key;
        public void SaveRaceEnabled(bool enabled) { }
        public void SaveDraftState(RacePanelDraftState draftState) => DraftState = draftState;
        public void OpenInGameMenu() { }
        public void SaveLeaderboardSettings(RaceLeaderboardSettings leaderboardSettings) => LeaderboardSettings = leaderboardSettings;
        public void SaveBossPenaltySettings(RaceBossPenaltySettings bossPenaltySettings) => BossPenaltySettings = bossPenaltySettings;
        public void SaveVoiceSettings(RaceVoiceSettings voiceSettings) => VoiceSettings = voiceSettings;
        public void PreviewVoice(RaceVoiceSettings voiceSettings) { }
        public Task CreateRoomAsync(string serverUrl, string nickname) => Task.CompletedTask;
        public Task<RaceOperationResult<RaceRoomState>> JoinRoomAsync(string serverUrl, string roomCode, string nickname) => throw new NotSupportedException();
        public Task CloseRoomAsync() => Task.CompletedTask;
        public Task KickPlayerAsync(string nickname) => Task.CompletedTask;
        public Task<RacePanelWorldGenerationResult> GenerateRandomWorldAsync(RaceWorldSettings worldSettings, IProgress<int>? progress = null) => throw new NotSupportedException();
        public Task<RacePanelWorldGenerationResult> GenerateCustomSeedWorldAsync(RaceWorldSettings worldSettings, string seedText, IProgress<int>? progress = null) => throw new NotSupportedException();
        public Task<RaceOperationResult<RaceRoomState>> UploadWorldAsync(string serverUrl, string nickname, string worldPath, RaceWorldSettings worldSettings, string seedText, IProgress<int>? progress = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CancelWorldGenerationAsync() => Task.CompletedTask;
        public Task DiscardLocalWorldAsync(string worldPath) => Task.CompletedTask;
        public Task<RaceOperationResult<RaceRoomState>> StartAsync() => throw new NotSupportedException();
        public Task<RaceOperationResult<RaceRoomState>> RestartAsync() => throw new NotSupportedException();
        public Task LeaveAsync() => Task.CompletedTask;
    }
}
