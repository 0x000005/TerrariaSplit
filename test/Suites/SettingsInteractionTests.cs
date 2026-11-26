using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TerrariaSplit.UI.Wpf;
using TerrariaSplit.UI.Wpf.Controls;
using TerrariaSplit.UI.Wpf.Settings;
using TerrariaSplit.UI.Wpf.Theme;
using TerrariaSplit.UI.Wpf.Windows;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfScrollViewer = System.Windows.Controls.ScrollViewer;
using WpfStackPanel = System.Windows.Controls.StackPanel;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace TerrariaSplit.Tests;

internal static class SettingsInteractionTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Async("settings interaction blocks profile switching while the settings window is open", TestSuite.Windows, ProfileSwitchGuard);
        yield return TestCase.Async("settings interaction wheels scroll pages and open lists without changing combo selections", TestSuite.Windows, ComboWheelJourney);
        yield return TestCase.Async("settings interaction commits custom icon choices from keyboard and mouse but not cancellation", TestSuite.Windows, SelectionCommitJourney);
        yield return TestCase.Async("settings interaction uses a complete condition dropdown and retains redundant icon choices", TestSuite.Windows, ConditionDropdownJourney);
        yield return TestCase.Async("settings layout aligns first rows and keeps Chinese and English route pages within the viewport", TestSuite.Windows, SettingsLayoutJourney);
    }

    private static Task ProfileSwitchGuard(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        using var directory = new TestDirectory();
        var repository = new TestSettingsRepository(directory.Path);
        AppSettings settings = AppSettingsDefaults.Create();
        var dispatches = new ConcurrentQueue<Action>();
        int applied = 0;
        int snapshotsRequested = 0;
        bool inRaceRoom = false;
        using var shell = new SettingsShell(
            () => { snapshotsRequested++; return settings; },
            () => inRaceRoom,
            repository,
            new SettingsSnapshotFactory(),
            dispatches.Enqueue,
            _ => applied++,
            static () => { }, static () => { }, static () => { },
            static () => true,
            static () => new System.Drawing.Rectangle(0, 0, 800, 600),
            static _ => { });
        string first = repository.SettingsPath;
        string second = Path.Combine(directory.Path, "other.json");
        Check.True(shell.CanSwitchSettingsFile);
        shell.SwitchSettingsFile(second);
        Check.Equal(1, repository.LoadCalls);
        Check.Equal(1, applied);

        Action<string> previousMenuCallback = shell.SwitchSettingsFile;
        shell.Open();
        Check.True(shell.IsOpen);
        Check.False(shell.CanSwitchSettingsFile);
        previousMenuCallback(first);
        Check.Equal(second, repository.SettingsPath);
        Check.Equal(1, repository.LoadCalls);
        Check.Equal(1, applied);

        using var menu = new ContextMenuStrip();
        new MainFormContextMenuBuilder(repository).Rebuild(
            menu, settings, canOpenSettings: true, shell.CanSwitchSettingsFile, canUseStandardAutomation: true,
            static () => { }, static () => { }, shell.Open, static () => { }, previousMenuCallback, static () => { });
        Check.False(menu.Items[MainFormContextMenuBuilder.SettingsFileMenuItemName]!.Enabled);
        var settingsItem = Check.Is<ToolStripMenuItem>(menu.Items[MainFormContextMenuBuilder.SettingsItemName]);
        Check.True(settingsItem.Enabled);
        settingsItem.PerformClick();
        Check.Equal(1, snapshotsRequested);

        shell.Dispose();
        Check.False(shell.IsOpen);
        Check.True(shell.CanSwitchSettingsFile);
        shell.SwitchSettingsFile(first);
        Check.Equal(first, repository.SettingsPath);
        Check.Equal(2, repository.LoadCalls);
        Check.Equal(2, applied);

        inRaceRoom = true;
        Check.False(shell.CanSwitchSettingsFile);
        shell.SwitchSettingsFile(second);
        Check.Equal(2, repository.LoadCalls);
        shell.Open();
        Check.False(shell.IsOpen);
    }, cancellationToken);

    private static Task ComboWheelJourney(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        WpfBootstrap.EnsureInitialized();
        var combo = new WpfComboBox { ItemsSource = Enumerable.Range(0, 60).Select(index => $"Option {index}"), SelectedIndex = 3, MaxDropDownHeight = 120 };
        var panel = new WpfStackPanel();
        panel.Children.Add(new WpfTextBlock { Height = 200, Text = "Before" });
        panel.Children.Add(combo);
        panel.Children.Add(new WpfTextBlock { Height = 1600, Text = "After" });
        var page = new WpfScrollViewer { Content = panel, Height = 180 };
        var window = TestWindow(page);
        try
        {
            window.Show();
            Drain();
            Check.True(combo.Focus());
            Check.Is<Popup>(combo.Template.FindName("PART_Popup", combo)).PopupAnimation = PopupAnimation.None;
            page.ScrollToVerticalOffset(150);
            window.UpdateLayout();
            double before = page.VerticalOffset;
            Check.True(Wheel(combo, -120));
            Drain();
            window.UpdateLayout();
            Check.Equal(3, combo.SelectedIndex);
            Check.True(page.VerticalOffset > before);

            combo.IsEditable = true;
            window.UpdateLayout();
            var input = Check.Is<UIElement>(combo.Template.FindName("PART_EditableTextBox", combo));
            Check.True(Wheel(input, 120));
            Drain();
            Check.Equal(3, combo.SelectedIndex);
            combo.IsEditable = false;

            combo.IsDropDownOpen = true;
            Drain();
            window.UpdateLayout();
            Check.True(combo.IsDropDownOpen);
            Popup popup = Check.Is<Popup>(combo.Template.FindName("PART_Popup", combo));
            WpfScrollViewer list = Descendants<WpfScrollViewer>(popup.Child).First();
            double listBefore = list.VerticalOffset;
            Wheel(list, -120);
            Drain();
            window.UpdateLayout();
            Check.Equal(3, combo.SelectedIndex);
            Check.True(list.VerticalOffset > listBefore);
            Wheel(combo, -120);
            Drain();
            Check.Equal(3, combo.SelectedIndex);
            CloseDropDown(combo, () => combo.IsDropDownOpen = false);

            KeyDown(combo, Key.Down);
            KeyUp(combo, Key.Down);
            Check.Equal(4, combo.SelectedIndex);
        }
        finally
        {
            window.Close();
        }
    }, cancellationToken);

    private static Task SelectionCommitJourney(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        WpfBootstrap.EnsureInitialized();
        var combo = new WpfComboBox { ItemsSource = new[] { "All icons", "Single icon", "Custom image" }, SelectedIndex = 0 };
        bool canCommit = true;
        int fileRequests = 0;
        _ = new ComboBoxSelectionCommitHandler(combo, () => canCommit, () =>
        {
            if (combo.SelectedIndex == 2) fileRequests++;
        });
        var window = TestWindow(combo);
        try
        {
            window.Show();
            Drain();
            Check.True(combo.Focus());
            Check.Is<Popup>(combo.Template.FindName("PART_Popup", combo)).PopupAnimation = PopupAnimation.None;
            combo.SelectedIndex = 2;
            Drain();
            Check.Equal(0, fileRequests);
            combo.SelectedIndex = 1;
            KeyDown(combo, Key.Down);
            KeyUp(combo, Key.Down);
            Drain();
            Check.Equal(2, combo.SelectedIndex);
            Check.Equal(1, fileRequests);

            combo.IsDropDownOpen = true;
            Drain();
            CloseDropDown(combo, () => { KeyDown(combo, Key.Escape); KeyUp(combo, Key.Escape); });
            Drain();
            Check.False(combo.IsDropDownOpen);
            Check.Equal(1, fileRequests);

            combo.IsDropDownOpen = true;
            Drain();
            CloseDropDown(combo, () => combo.IsDropDownOpen = false);
            Drain();
            Check.Equal(1, fileRequests);

            combo.IsDropDownOpen = true;
            Drain();
            CloseDropDown(combo, () => { KeyDown(combo, Key.Enter); KeyUp(combo, Key.Enter); });
            Drain();
            Check.Equal(2, fileRequests);

            combo.IsDropDownOpen = true;
            Drain();
            var item = Check.Is<System.Windows.Controls.ComboBoxItem>(combo.ItemContainerGenerator.ContainerFromIndex(2));
            item.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
            CloseDropDown(combo, () => combo.IsDropDownOpen = false);
            Drain();
            Check.Equal(3, fileRequests);

            combo.SelectedIndex = 1;
            KeyDown(combo, Key.Down);
            KeyUp(combo, Key.Down);
            canCommit = false;
            Drain();
            Check.Equal(3, fileRequests);
        }
        finally
        {
            canCommit = false;
            window.Close();
        }
    }, cancellationToken);

    private static Task ConditionDropdownJourney(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        AppSettings settings = AppSettingsDefaults.Create();
        settings.General.Language = "Chinese";
        settings.Route.SplitRoute =
        [
            new SplitRouteEntry
            {
                Id = "match-test", DisplayName = "Test", Enabled = true,
                Condition = SplitCondition.All([
                    SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer),
                    SplitCatalog.CreateBossFactCondition(SplitCatalog.Twins)])
            }
        ];
        using var window = new SettingsWindow(settings);
        window.PageHost.Select(SettingsPageId.Splits);
        var page = window.PageHost.GetOrCreatePage<WpfSplitSettingsPage>(SettingsPageId.Splits);
        Check.Sequence(["满足全部条件", "满足至少 1 个条件"], page.ConditionMatchModeBoxForTests.Items.Cast<object>().Select(item => item.ToString()));
        var conditionGrid = Check.Is<System.Windows.Controls.Grid>(page.ConditionMatchModeBoxForTests.Parent);
        Check.Equal(0, System.Windows.Controls.Grid.GetRow(page.ConditionMatchModeBoxForTests));
        Check.Equal(1, System.Windows.Controls.Grid.GetColumn(page.ConditionMatchModeBoxForTests));
        Check.False(conditionGrid.Children.OfType<WpfTextBlock>().Any(label => label.Text is "满足" or "条件"));
        page.ConditionMatchModeBoxForTests.SelectedIndex = 1;
        window.ApplyForTests();
        Check.Equal(1, window.Result.Route.SplitRoute.Single().Condition.GetRequiredCount());

        AppSettings single = AppSettingsDefaults.Create();
        single.General.Language = "English";
        single.Route.SplitRoute =
        [
            new SplitRouteEntry { Id = "single-test", DisplayName = "Single", Enabled = true, Condition = SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer) }
        ];
        using var singleWindow = new SettingsWindow(single);
        var singlePage = singleWindow.PageHost.GetOrCreatePage<WpfSplitSettingsPage>(SettingsPageId.Splits);
        Check.Equal("All icons", singlePage.IconOverrideBoxForTests.Items[0].ToString());
        Check.Equal(3, singlePage.IconOverrideBoxForTests.Items.Count);
    }, cancellationToken);

    private static System.Windows.Window TestWindow(UIElement content)
    {
        var window = new System.Windows.Window
        {
            Width = 380, Height = 260, Left = -10000, Top = -10000,
            ShowInTaskbar = false, ShowActivated = true, Content = content
        };
        WpfThemeManager.ApplyTheme(window, WpfThemePreset.ObsidianGlass, WpfDensityPreset.Compact);
        return window;
    }

    private static Task SettingsLayoutJourney(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        foreach (string language in new[] { "Chinese", "English" })
        foreach (WpfDensityPreset density in Enum.GetValues<WpfDensityPreset>())
        {
            AppSettings settings = AppSettingsDefaults.Create();
            settings.General.Language = language;
            using var window = new SettingsWindow(settings);
            WpfThemeManager.ApplyTheme(window, WpfThemePreset.FluentMica, density);
            foreach (SettingsPageId id in new[]
            {
                SettingsPageId.General, SettingsPageId.Splits, SettingsPageId.Data, SettingsPageId.Ui,
                SettingsPageId.Effects, SettingsPageId.Colors, SettingsPageId.Sounds, SettingsPageId.Advanced
            })
            {
                window.PageHost.Select(id);
                FrameworkElement element = window.PageHost.Pages.Single(entry => entry.Id == id).PageElement!;
                Layout(element, 850);
                AssertFirstCardRowsCentered(element, $"page={id}; language={language}; density={density}");
            }

            window.PageHost.Select(SettingsPageId.Splits);
            FrameworkElement routeElement = window.PageHost.Pages.Single(entry => entry.Id == SettingsPageId.Splits).PageElement!;
            var page = window.PageHost.GetOrCreatePage<WpfSplitSettingsPage>(SettingsPageId.Splits);
            foreach (double width in new[] { 640d, 850d })
            {
                Layout(routeElement, width);
                var scroll = Check.Is<WpfScrollViewer>(routeElement);
                Check.Equal(0d, scroll.ScrollableWidth);
                Check.True(scroll.DesiredSize.Width <= width);
                var properties = Check.Is<System.Windows.Controls.Grid>(page.SplitNameBoxForTests.Parent);
                double rowHeight = (double)routeElement.FindResource("SettingRowHeight");
                double pixel = 1 / VisualTreeHelper.GetDpi(routeElement).DpiScaleY;
                Check.True(properties.RowDefinitions.All(row => Math.Abs(row.ActualHeight - rowHeight) <= pixel + 0.01));
                foreach (WpfTextBlock label in properties.Children.OfType<WpfTextBlock>())
                {
                    var text = new FormattedText(label.Text, System.Globalization.CultureInfo.InvariantCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        new Typeface(label.FontFamily, label.FontStyle, label.FontWeight, label.FontStretch),
                        label.FontSize, System.Windows.Media.Brushes.White, VisualTreeHelper.GetDpi(label).PixelsPerDip);
                    Check.True(text.WidthIncludingTrailingWhitespace <= label.ActualWidth + pixel);
                }
                double[] centers = new FrameworkElement[]
                {
                    page.TargetKindBoxForTests, page.SplitNameBoxForTests, page.ConditionMatchModeBoxForTests
                }.Select(control => control.TranslatePoint(new System.Windows.Point(0, control.ActualHeight / 2), routeElement).Y).ToArray();
                Check.True(centers.Max() - centers.Min() <= pixel + 0.01);
                double[] listTops = new FrameworkElement[]
                {
                    page.TargetListForTests, page.RouteListForTests, page.ConditionListForTests
                }.Select(control => control.TranslatePoint(new System.Windows.Point(), routeElement).Y).ToArray();
                Check.True(listTops.Max() - listTops.Min() <= pixel + 0.01);
                foreach (System.Windows.Controls.Button button in Descendants<System.Windows.Controls.Button>(routeElement)
                    .Where(button => button.ToolTip is string))
                {
                    Check.Equal(TextTrimming.CharacterEllipsis, Check.Is<WpfTextBlock>(button.Content).TextTrimming);
                    double right = button.TranslatePoint(new System.Windows.Point(button.ActualWidth, 0), routeElement).X;
                    Check.True(right <= width + 0.01);
                }
            }

            if (Environment.GetEnvironmentVariable("TERRARIA_SPLIT_CAPTURE_ROUTE_LAYOUT") == "1")
            {
                string directory = Path.Combine(TestDirectory.FindSourceRoot(), "test", "Results", "RouteLayout");
                Directory.CreateDirectory(directory);
                using FileStream output = File.Create(Path.Combine(directory, $"{language}-{density}.png"));
                var bitmap = new RenderTargetBitmap(1275, 450, 144, 144, PixelFormats.Pbgra32);
                bitmap.Render(routeElement);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(output);
            }
        }

        static void Layout(FrameworkElement element, double width)
        {
            element.Measure(new System.Windows.Size(width, 740));
            element.Arrange(new Rect(0, 0, width, 740));
            element.UpdateLayout();
        }
    }, cancellationToken);

    private static void Drain() => Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(static () => { }));

    private static void CloseDropDown(WpfComboBox combo, Action close)
    {
        bool closed = false;
        EventHandler handler = (_, _) => closed = true;
        combo.DropDownClosed += handler;
        try
        {
            close();
            if (!closed)
            {
                var frame = new DispatcherFrame();
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
                DateTime deadline = DateTime.UtcNow.AddSeconds(3);
                timer.Tick += (_, _) => { if (closed || DateTime.UtcNow >= deadline) frame.Continue = false; };
                timer.Start();
                try { Dispatcher.PushFrame(frame); }
                finally { timer.Stop(); }
            }

            Check.True(closed);
            Drain();
        }
        finally
        {
            combo.DropDownClosed -= handler;
        }
    }

    private static bool Wheel(UIElement element, int delta)
    {
        var preview = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, delta) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
        element.RaiseEvent(preview);
        if (!preview.Handled)
        {
            element.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, delta) { RoutedEvent = Mouse.MouseWheelEvent });
        }
        return preview.Handled;
    }

    private static void KeyDown(UIElement element, Key key)
    {
        PresentationSource source = PresentationSource.FromVisual(element)!;
        var preview = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        element.RaiseEvent(preview);
        if (!preview.Handled)
        {
            element.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
        }
    }

    private static void KeyUp(UIElement element, Key key) => element.RaiseEvent(new KeyEventArgs(
        Keyboard.PrimaryDevice, PresentationSource.FromVisual(element)!, 0, key) { RoutedEvent = Keyboard.PreviewKeyUpEvent });

    internal static void AssertFirstCardRowsCentered(FrameworkElement element, string context)
    {
        foreach (SettingCard card in Descendants<SettingCard>(element))
        {
            if (card.Visibility != Visibility.Visible || card.ActualHeight <= 0) continue;
            if (card.Content is not WpfStackPanel body || string.IsNullOrWhiteSpace(card.Header)) continue;
            Check.Equal(0d, card.ContentPadding.Top);
            FrameworkElement? firstRow = body.Children.OfType<FrameworkElement>()
                .FirstOrDefault(child => child.Visibility == Visibility.Visible);
            System.Windows.Controls.Border root;
            FrameworkElement[] labels;
            if (firstRow is SettingRow settingRow)
            {
                root = Check.Is<System.Windows.Controls.Border>(settingRow.Template.FindName("RowRoot", settingRow));
                var grid = Check.Is<System.Windows.Controls.Grid>(root.Child);
                labels = [Check.Is<FrameworkElement>(grid.Children[0])];
            }
            else if (firstRow is System.Windows.Controls.Border { Child: System.Windows.Controls.Grid grid } border)
            {
                root = border;
                labels = grid.Children.OfType<WpfTextBlock>().Cast<FrameworkElement>().ToArray();
            }
            else
            {
                continue;
            }

            double expectedCenter = (root.ActualHeight + root.Padding.Top + root.BorderThickness.Top -
                root.Padding.Bottom - root.BorderThickness.Bottom) / 2;
            double pixel = 1 / VisualTreeHelper.GetDpi(root).DpiScaleY;
            foreach (FrameworkElement label in labels)
            {
                double center = label.TranslatePoint(new System.Windows.Point(0, label.ActualHeight / 2), root).Y;
                if (Math.Abs(center - expectedCenter) > pixel + 0.01)
                {
                    throw new InvalidOperationException($"First row alignment: {context}; card={card.Header}; actual={center}; expected={expectedCenter}; height={root.ActualHeight}");
                }
            }

            WpfTextBlock? header = Descendants<WpfTextBlock>(card).FirstOrDefault(text =>
                text.Text == card.Header && ReferenceEquals(text.TemplatedParent, card));
            if (header is null)
            {
                throw new InvalidOperationException($"Card header was not rendered: {context}; card={card.Header}; visibility={card.Visibility}; actualHeight={card.ActualHeight}");
            }
            DependencyObject? ancestor = VisualTreeHelper.GetParent(header);
            while (ancestor is not System.Windows.Controls.Border) ancestor = VisualTreeHelper.GetParent(ancestor!);
            var headerBorder = (System.Windows.Controls.Border)ancestor;
            double headerBottom = headerBorder.TranslatePoint(new System.Windows.Point(0, headerBorder.ActualHeight), card).Y;
            double rowTop = firstRow!.TranslatePoint(new System.Windows.Point(), card).Y;
            Check.True(Math.Abs(headerBottom - rowTop) <= pixel + 0.01);
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject element) where T : DependencyObject
    {
        if (element is T match) yield return match;
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            foreach (T child in Descendants<T>(VisualTreeHelper.GetChild(element, index))) yield return child;
        }
    }

    private sealed class TestSettingsRepository(string directory) : ISettingsRepository
    {
        public string SettingsDirectory => directory;
        public string SettingsPath { get; private set; } = Path.Combine(directory, "settings.json");
        public string SettingsFileName => Path.GetFileName(SettingsPath);
        public int LoadCalls { get; private set; }
        public AppSettings Load() => Load(SettingsPath);
        public AppSettings Load(string path) { LoadCalls++; SettingsPath = path; return AppSettingsDefaults.Create(); }
        public IReadOnlyList<string> GetSettingsFiles() => [SettingsPath];
    }
}
