using TerrariaSplit.UI.Rendering;

namespace TerrariaSplit.Tests;

internal static class WindowsFlowTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Async("settings window exposes About last, displays the executable version and owns update cancellation", TestSuite.Windows, AboutPageJourney);
        yield return TestCase.Async("settings window opens every page and produces a normalized draft without mutating the source", TestSuite.Windows, SettingsDraftJourney);
        yield return TestCase.Sync("overlay restores a visible multi-monitor position and keeps dense layouts inside composite bounds", TestSuite.Windows, OverlayLayoutJourney);
        yield return TestCase.Sync("timer reserves stable proportional-font slots for milliseconds and indicators", TestSuite.Windows, TimerProportionalFontLayoutJourney);
        yield return TestCase.Sync("expanded current condition follows early delta timing after a prior condition completes", TestSuite.Windows, ExpandedConditionEarlyDeltaJourney);
        yield return TestCase.Sync("cheat filter indicator uses yellow orange and red priority", TestSuite.Core, CheatFilterIndicatorPriority);
        yield return TestCase.Sync("hotkey settings normalize modifiers and fall back when keys are unsafe", TestSuite.Windows, HotkeyJourney);
        yield return TestCase.Async("hotkey restore returns to the main window thread after an external lifecycle event", TestSuite.Windows, HotkeyThreadAffinityJourney);
    }

    private static void CheatFilterIndicatorPriority()
    {
        var settings = new AutoCreateWorldSettings
        {
            EnableCheats = true,
            EnablePyramidFilter = true,
            RequireCrimsonBetweenDungeonAndSpawn = false,
            JungleRouteDepth = AutoCreateJungleRouteDepth.None
        };
        Check.Equal(
            Color.FromArgb(217, 166, 46).ToArgb(),
            CheatFilterIndicator.GetColor(
                CheatFilterIndicator.Resolve(settings)).ToArgb());

        settings.RequireCrimsonBetweenDungeonAndSpawn = true;
        Check.Equal(
            Color.FromArgb(240, 138, 50).ToArgb(),
            CheatFilterIndicator.GetColor(
                CheatFilterIndicator.Resolve(settings)).ToArgb());

        settings.ResourceFilterItemMask =
            AutoCreateResourceFilterItem.BoomstickMask;
        Check.Equal(
            Color.FromArgb(213, 72, 72).ToArgb(),
            CheatFilterIndicator.GetColor(
                CheatFilterIndicator.Resolve(settings)).ToArgb());
    }

    private static Task AboutPageJourney(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        using var service = new FakeUpdateService(new Version(9, 8, 7, 6));
        using var form = new SettingsForm(new AppSettings(), applicationUpdateService: service);
        Check.Equal(SettingsPageId.About, form.PageHost.Pages.Last().Id);
        AboutSettingsPage page = form.PageHost.GetOrCreatePage<AboutSettingsPage>(SettingsPageId.About);
        Check.Equal("v9.8.7.6", page.DisplayedVersion);
        Check.True(page.ProductSectionNaturalHeight > 0);
        Check.Equal(page.ProductSectionNaturalHeight * 2, page.ProductSectionMinimumHeight);
        form.Dispose();
        Check.True(service.Disposed);
    }, cancellationToken);

    private static Task SettingsDraftJourney(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        AppSettings source = AppSettingsDefaults.Create();
        source.General.Language = "English";
        source.Automation.AutoCreate.WorldSize = AutoCreateWorldSize.Large;
        using var form = new SettingsForm(source, applicationUpdateService: new FakeUpdateService(new Version(1, 0, 0, 0)));
        foreach (SettingsPageId pageId in form.PageHost.Pages.Select(page => page.Id))
        {
            form.PageHost.Select(pageId);
            Check.True(form.PageHost.IsCreated(pageId));
        }
        UiSettingsPage ui = form.PageHost.GetOrCreatePage<UiSettingsPage>(SettingsPageId.Ui);
        foreach ((UiColumnDescriptor primary, UiColumnDescriptor attached) in UiColumnDescriptors.SharedWidthPairs)
        {
            System.Windows.Forms.TextBox primaryWidth = ui.GetColumnWidthBoxForTests(primary.Key);
            System.Windows.Forms.TextBox attachedWidth = ui.GetColumnWidthBoxForTests(attached.Key);
            Check.True(ReferenceEquals(primaryWidth, attachedWidth));
            primaryWidth.Text = "333";
            ThemedDropDownList primaryAlignment = ui.GetColumnAlignmentBoxForTests(primary.Key);
            ThemedDropDownList attachedAlignment = ui.GetColumnAlignmentBoxForTests(attached.Key);
            Check.True(ReferenceEquals(primaryAlignment, attachedAlignment));
            primaryAlignment.SelectedIndex = 1;
        }
        ui.IconNameGapBoxForTests.Text = "7";
        ui.NameTimeGapBoxForTests.Text = "8";
        ui.TimeDeltaGapBoxForTests.Text = "9";

        System.Windows.Forms.CheckBox nameShow = ui.GetColumnShowBoxForTests(UiColumnDescriptors.Name.Key);
        System.Windows.Forms.CheckBox attachedNameShow = ui.GetColumnShowBoxForTests(UiColumnDescriptors.AttachedName.Key);
        Check.False(nameShow.Checked);
        Check.False(attachedNameShow.Checked);
        nameShow.Checked = true;
        attachedNameShow.Checked = true;
        nameShow.Checked = false;
        Check.False(ui.GetColumnFontSizeBoxForTests(UiColumnDescriptors.Name.Key).Enabled);
        Check.True(ui.GetColumnWidthBoxForTests(UiColumnDescriptors.Name.Key).Enabled);
        Check.True(ui.GetColumnAlignmentBoxForTests(UiColumnDescriptors.Name.Key).Enabled);
        Check.True(ui.IconNameGapBoxForTests.Enabled);
        Check.True(ui.NameTimeGapBoxForTests.Enabled);

        attachedNameShow.Checked = false;
        Check.False(ui.GetColumnFontSizeBoxForTests(UiColumnDescriptors.AttachedName.Key).Enabled);
        Check.False(ui.GetColumnWidthBoxForTests(UiColumnDescriptors.Name.Key).Enabled);
        Check.False(ui.GetColumnAlignmentBoxForTests(UiColumnDescriptors.Name.Key).Enabled);
        Check.True(ui.IconNameGapBoxForTests.Enabled);
        Check.False(ui.NameTimeGapBoxForTests.Enabled);

        nameShow.Checked = true;
        attachedNameShow.Checked = true;
        Check.True(ui.GetColumnFontSizeBoxForTests(UiColumnDescriptors.Name.Key).Enabled);
        Check.True(ui.GetColumnWidthBoxForTests(UiColumnDescriptors.Name.Key).Enabled);
        Check.True(ui.GetColumnAlignmentBoxForTests(UiColumnDescriptors.Name.Key).Enabled);
        Check.True(ui.IconNameGapBoxForTests.Enabled);
        Check.True(ui.NameTimeGapBoxForTests.Enabled);

        System.Windows.Forms.CheckBox iconShow = ui.GetColumnShowBoxForTests(UiColumnDescriptors.Icon.Key);
        System.Windows.Forms.CheckBox attachedIconShow = ui.GetColumnShowBoxForTests(UiColumnDescriptors.AttachedIcon.Key);
        iconShow.Checked = false;
        attachedIconShow.Checked = false;
        Check.False(ui.IconNameGapBoxForTests.Enabled);
        iconShow.Checked = true;
        attachedIconShow.Checked = true;
        Check.True(ui.IconNameGapBoxForTests.Enabled);

        System.Windows.Forms.CheckBox timeShow = ui.GetColumnShowBoxForTests(UiColumnDescriptors.Time.Key);
        System.Windows.Forms.CheckBox attachedTimeShow = ui.GetColumnShowBoxForTests(UiColumnDescriptors.AttachedTime.Key);
        timeShow.Checked = false;
        attachedTimeShow.Checked = false;
        Check.False(ui.TimeDeltaGapBoxForTests.Enabled);
        timeShow.Checked = true;
        attachedTimeShow.Checked = true;
        Check.True(ui.TimeDeltaGapBoxForTests.Enabled);

        foreach (UiColumnDescriptor descriptor in UiColumnDescriptors.All.Where(static descriptor => descriptor.ShowItalic))
        {
            System.Windows.Forms.CheckBox italicBox = Check.Is<System.Windows.Forms.CheckBox>(ui.GetItalicBoxForTests(descriptor.Key));
            italicBox.Checked = true;
        }
        ColorSettingsPage colors = form.PageHost.GetOrCreatePage<ColorSettingsPage>(SettingsPageId.Colors);
        Check.True(colors.ColorTextBoxes.ContainsKey(nameof(UiColorSettings.NameText)));
        Check.True(colors.ColorTextBoxes.ContainsKey(nameof(UiColorSettings.ActiveNameText)));
        Check.True(colors.ColorTextBoxes.ContainsKey(nameof(UiColorSettings.CompletedNameText)));
        colors.ColorTextBoxes[nameof(UiColorSettings.NameText)].Text = "#123456";
        colors.ColorTextBoxes[nameof(UiColorSettings.ActiveNameText)].Text = "#345678";
        colors.ColorTextBoxes[nameof(UiColorSettings.CompletedNameText)].Text = "#654321";
        AppSettings draft = form.PageHost.CreateAppliedSnapshot();
        Check.False(ReferenceEquals(source, draft));
        Check.Equal("English", draft.General.Language);
        Check.True(draft.Route.SplitRoute.Count > 0);
        foreach (UiColumnDescriptor descriptor in UiColumnDescriptors.All.Where(static descriptor => descriptor.ShowItalic))
        {
            Check.True(descriptor.GetValue(draft.Overlay.Columns)?.Italic == true);
            Check.False(descriptor.GetValue(source.Overlay.Columns)?.Italic == true);
        }
        foreach ((UiColumnDescriptor primary, UiColumnDescriptor attached) in UiColumnDescriptors.SharedWidthPairs)
        {
            Check.Equal(333, primary.GetValue(draft.Overlay.Columns)?.Width ?? 0);
            Check.Equal(333, attached.GetValue(draft.Overlay.Columns)?.Width ?? 0);
            Check.Equal(UiColumnAlignment.Center, UiColumnDescriptors.GetSharedAlignment(draft.Overlay.Columns, primary));
            Check.Equal(UiColumnAlignment.Center, UiColumnDescriptors.GetSharedAlignment(draft.Overlay.Columns, attached));
        }
        Check.Equal(7, draft.Overlay.Columns.IconNameGap);
        Check.Equal(8, draft.Overlay.Columns.NameTimeGap);
        Check.Equal(9, draft.Overlay.Columns.TimeDeltaGap);
        Check.Equal("#123456", draft.Overlay.Colors.NameText);
        Check.Equal("#345678", draft.Overlay.Colors.ActiveNameText);
        Check.Equal("#654321", draft.Overlay.Colors.CompletedNameText);
        Check.False(string.Equals(source.Overlay.Colors.NameText, draft.Overlay.Colors.NameText, StringComparison.Ordinal));
        Check.Equal("English", source.General.Language);
        AutomationSettingsPage automation = form.PageHost.GetOrCreatePage<AutomationSettingsPage>(SettingsPageId.Automation);
        Check.Equal(6, automation.AutoCreateLifeCrystalMinimumBoxes.Count);
        Check.Equal("5+", automation.AutoCreateLifeCrystalMinimumBoxes[5].Text);
        Check.False(automation.AutoCreateLifeCrystalMinimumBoxes[5].AutoEllipsis);
        Check.True(automation.AutoCreatePyramidFilterBox.Enabled);
        Check.False(automation.AutoCreateCrimsonBetweenDungeonAndSpawnBox.Enabled);
        Check.False(automation.AutoCreateCrimsonBetweenDungeonAndSpawnBox.Checked);
        Check.False(automation.AutoCreateJungleRouteDepthBox.Enabled);
        automation.AutoCreateCheatsBox.Checked = true;
        automation.AutoCreateWorldSizeBox.SelectedIndex = Array.IndexOf(AutoCreateWorldSize.All, AutoCreateWorldSize.Small);
        Check.True(automation.AutoCreateCrimsonBetweenDungeonAndSpawnBox.Enabled);
        automation.AutoCreateCrimsonBetweenDungeonAndSpawnBox.Checked = true;
        Check.True(automation.AutoCreateCrimsonDistanceBoxes.Values.All(static button => button.Enabled));
        Check.True(automation.AutoCreateJungleRouteDepthBox.Enabled);
        automation.AutoCreateJungleRouteDepthBox.Checked = true;
        Check.True(automation.AutoCreateJungleRouteDepthBox.Checked);
        Check.True(automation.AutoCreateJungleRouteDepthBoxes.Values.All(static button => button.Enabled));
        Check.True(automation.AutoCreateResourceItemBoxes.Values.All(static button => button.Enabled));
        Check.True(automation.AutoCreateLifeCrystalMinimumBoxes[0].Enabled);
        Check.True(automation.AutoCreateSpelunkerMinimumBoxes[0].Enabled);
        Check.True(automation.AutoCreateFeatherfallMinimumBoxes[0].Enabled);
        automation.AutoCreateSpecialSeedBoxes[AutoCreateSpecialWorldSeed.NotTheBees].Checked = true;
        Check.True(automation.AutoCreatePyramidFilterBox.Enabled);
        Check.False(automation.AutoCreateCrimsonBetweenDungeonAndSpawnBox.Enabled);
        Check.False(automation.AutoCreateCrimsonBetweenDungeonAndSpawnBox.Checked);
        Check.False(automation.AutoCreateJungleRouteDepthBox.Enabled);
        Check.False(automation.AutoCreateJungleRouteDepthBox.Checked);
        automation.AutoCreateSpecialSeedBoxes[AutoCreateSpecialWorldSeed.NotTheBees].Checked = false;
        Check.True(automation.AutoCreateCrimsonBetweenDungeonAndSpawnBox.Enabled);
        automation.AutoCreateSecretSeedsBox.Text = "secret";
        Check.False(automation.AutoCreateCrimsonBetweenDungeonAndSpawnBox.Enabled);
        Check.True(automation.AutoCreatePyramidFilterBox.Enabled);
        automation.AutoCreateSecretSeedsBox.Text = string.Empty;
        automation.AutoCreateCrimsonBetweenDungeonAndSpawnBox.Checked = true;
        automation.AutoCreateJungleRouteDepthBox.Checked = true;
        AppSettings resourceDraft = form.PageHost.CreateAppliedSnapshot();
        Check.True(resourceDraft.Automation.AutoCreate.EnableCheats);
        Check.Equal(AutoCreateJungleRouteDepth.Medium, resourceDraft.Automation.AutoCreate.JungleRouteDepth);
        Check.Equal(0, resourceDraft.Automation.AutoCreate.ResourceFilterItemMask);
        Check.Equal(0, resourceDraft.Automation.AutoCreate.ResourceFilterLifeCrystalMinimum);
        Check.Equal(0, resourceDraft.Automation.AutoCreate.ResourceFilterSpelunkerPotionMinimum);
        Check.Equal(0, resourceDraft.Automation.AutoCreate.ResourceFilterFeatherfallPotionMinimum);
        Check.True(source.Automation.AutoCreate.RequireCrimsonBetweenDungeonAndSpawn);

        using var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        bool cheatsToggleRequested = false;
        new MainFormContextMenuBuilder().Rebuild(
            contextMenu,
            resourceDraft,
            canSwitchSettingsFile: true,
            canUseStandardAutomation: true,
            static () => { },
            static () => { },
            static () => { },
            () => cheatsToggleRequested = true,
            static _ => { },
            static () => { });
        var cheatsItem = Check.Is<System.Windows.Forms.ToolStripMenuItem>(
            contextMenu.Items[MainFormContextMenuBuilder.CheatsToggleItemName]);
        Check.Equal("Cheats", cheatsItem.Text);
        Check.True(cheatsItem.Checked);
        cheatsItem.PerformClick();
        Check.True(cheatsToggleRequested);

        var settingsFileMenu = Check.Is<System.Windows.Forms.ToolStripMenuItem>(
            contextMenu.Items[MainFormContextMenuBuilder.SettingsFileMenuItemName]);
        Check.True(settingsFileMenu.Enabled);
        new MainFormContextMenuBuilder().Rebuild(
            contextMenu,
            resourceDraft,
            canSwitchSettingsFile: false,
            canUseStandardAutomation: false,
            static () => { },
            static () => { },
            static () => { },
            static () => { },
            static _ => { },
            static () => { });
        settingsFileMenu = Check.Is<System.Windows.Forms.ToolStripMenuItem>(
            contextMenu.Items[MainFormContextMenuBuilder.SettingsFileMenuItemName]);
        var settingsItem = Check.Is<System.Windows.Forms.ToolStripMenuItem>(
            contextMenu.Items[MainFormContextMenuBuilder.SettingsItemName]);
        Check.False(settingsFileMenu.Enabled);
        Check.False(settingsItem.Enabled);
        cheatsItem = Check.Is<System.Windows.Forms.ToolStripMenuItem>(
            contextMenu.Items[MainFormContextMenuBuilder.CheatsToggleItemName]);
        Check.False(cheatsItem.Enabled);

        Check.False(HotkeyCommandMapper.TryMap(
            HotkeyAction.PauseResume,
            DateTime.UtcNow,
            createWorldRunning: false,
            enterWorldRunning: false,
            isRaceModeEnabled: true,
            isInRaceRoom: false,
            out _));
        Check.False(HotkeyCommandMapper.TryMap(
            HotkeyAction.CreateWorld,
            DateTime.UtcNow,
            createWorldRunning: false,
            enterWorldRunning: false,
            isRaceModeEnabled: true,
            isInRaceRoom: false,
            out _));
        Check.True(HotkeyCommandMapper.TryMap(
            HotkeyAction.CreateWorld,
            DateTime.UtcNow,
            createWorldRunning: true,
            enterWorldRunning: false,
            isRaceModeEnabled: true,
            isInRaceRoom: false,
            out AppCommand cancelCreate));
        Check.Is<CancelCreateWorldCommand>(cancelCreate);
        Check.True(HotkeyCommandMapper.TryMap(
            HotkeyAction.MouseClickThrough,
            DateTime.UtcNow,
            createWorldRunning: true,
            enterWorldRunning: false,
            isRaceModeEnabled: false,
            isInRaceRoom: false,
            out AppCommand toggleMouseClickThrough));
        Check.Is<ToggleMouseClickThroughCommand>(toggleMouseClickThrough);
    }, cancellationToken);

    private static Task HotkeyThreadAffinityJourney(
        CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        using var owner = new System.Windows.Forms.Form();
        _ = owner.Handle;
        int ownerThreadId = Environment.CurrentManagedThreadId;
        var manager = new RecordingHotkeyManager();
        using var shell = new HotkeyShell(
            owner,
            manager,
            AppSettingsDefaults.Create,
            () => owner.Handle,
            () => owner.IsHandleCreated,
            registerGlobalHotkeys: true,
            _ => { });

        Task.Run(shell.Register).GetAwaiter().GetResult();
        System.Windows.Forms.Application.DoEvents();
        Check.Equal(ownerThreadId, manager.LastRegisterThreadId);

        Task.Run(shell.Unregister).GetAwaiter().GetResult();
        System.Windows.Forms.Application.DoEvents();
        Check.Equal(ownerThreadId, manager.LastDisposeThreadId);
    }, cancellationToken);

    private static void OverlayLayoutJourney()
    {
        var settings = AppSettingsDefaults.Create();
        Check.False(settings.Overlay.Columns.Name.Show);
        Check.False(settings.Overlay.Columns.AttachedName.Show);
        Check.Equal(260, settings.Overlay.Columns.Name.Width);
        Check.Equal(16f, settings.Overlay.Columns.Name.FontSize);
        Check.Equal(UiColumnAlignment.Center, settings.Overlay.Columns.NameAlignment);
        int initialHeight = OverlayCompositeLayoutCalculator.GetFittingHeight(900, 700, settings, 12, 5, 9);
        var initial = new System.Drawing.Rectangle(100, 200, 900, initialHeight);
        Check.True(OverlayCompositeLayoutCalculator.TryCreate(initial, settings, 12, 5, 9, out OverlayCompositeLayout layout));
        Check.True(new System.Drawing.Rectangle(System.Drawing.Point.Empty, initial.Size).Contains(layout.StatusLocalBounds));
        Check.True(new System.Drawing.Rectangle(System.Drawing.Point.Empty, initial.Size).Contains(layout.TimerLocalBounds));
        Check.True(layout.TimerLocalBounds.Contains(layout.Layout.TimerRect));

        settings.Overlay.Columns.Name.Show = true;
        settings.Overlay.Columns.AttachedName.Show = true;
        ColumnRects primaryColumns = SplitListRenderer.GetColumnRects(settings, new System.Drawing.Rectangle(0, 0, 1000, 60));
        if (primaryColumns.Icon is not System.Drawing.Rectangle primaryIcon ||
            primaryColumns.Name is not System.Drawing.Rectangle primaryName ||
            primaryColumns.Time is not System.Drawing.Rectangle primaryTime ||
            primaryColumns.Delta is not System.Drawing.Rectangle primaryDelta)
        {
            throw new InvalidOperationException("All primary split columns should be visible by default.");
        }

        Check.True(primaryIcon.Right <= primaryName.Left);
        Check.True(primaryName.Right <= primaryTime.Left);
        Check.True(primaryTime.Right <= primaryDelta.Left);
        Check.Equal(settings.Overlay.Columns.IconNameGap, primaryName.Left - 4 - primaryIcon.Right);
        Check.Equal(settings.Overlay.Columns.NameTimeGap, primaryTime.Left - 4 - (primaryName.Right + 4));
        Check.Equal(settings.Overlay.Columns.TimeDeltaGap, primaryDelta.Left - 4 - (primaryTime.Right + 4));

        settings.Overlay.Columns.Name.Show = false;
        ColumnRects reservedName = SplitListRenderer.GetColumnRects(settings, new System.Drawing.Rectangle(0, 0, 1000, 60));
        if (reservedName.Name is not System.Drawing.Rectangle reservedNameRect ||
            reservedName.Time is not System.Drawing.Rectangle reservedTimeRect)
        {
            throw new InvalidOperationException("A column enabled for attached rows should remain reserved in primary rows.");
        }

        Check.Equal(primaryName, reservedNameRect);
        Check.Equal(primaryTime, reservedTimeRect);

        settings.Overlay.Columns.AttachedName.Show = false;
        ColumnRects withoutName = SplitListRenderer.GetColumnRects(settings, new System.Drawing.Rectangle(0, 0, 1000, 60));
        if (withoutName.Icon is not System.Drawing.Rectangle iconWithoutName ||
            withoutName.Time is not System.Drawing.Rectangle timeWithoutName)
        {
            throw new InvalidOperationException("Icon and time columns should remain visible when name is hidden.");
        }

        Check.False(withoutName.Name.HasValue);
        Check.Equal(settings.Overlay.Columns.NameTimeGap, timeWithoutName.Left - 4 - iconWithoutName.Right);
        settings.Overlay.Columns.Name.Show = true;
        settings.Overlay.Columns.AttachedName.Show = true;

        ColumnRects attachedColumns = SplitListRenderer.GetColumnRects(settings, new System.Drawing.Rectangle(0, 0, 1000, 60), attached: true);
        if (attachedColumns.Icon is not System.Drawing.Rectangle attachedIcon ||
            attachedColumns.Name is not System.Drawing.Rectangle attachedName ||
            attachedColumns.Time is not System.Drawing.Rectangle attachedTime)
        {
            throw new InvalidOperationException("Attached icon, name, and time columns should be visible by default.");
        }

        Check.True(attachedIcon.Right <= attachedName.Left);
        Check.True(attachedName.Right <= attachedTime.Left);
        Check.Equal(primaryColumns, attachedColumns);

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

        var controller = new OverlayBoundsController(
            9,
            settings,
            statusCount: 12,
            visibleStatusCount: 12);
        controller.Initialize(new System.Drawing.Rectangle(100, 100, 900, 700));
        System.Drawing.Point timerLocation = controller.CurrentLayout.TimerScreenBounds.Location;
        controller.ApplyCompositeBounds(new System.Drawing.Rectangle(100, 100, 1000, 800));
        Check.Equal(timerLocation, controller.CurrentLayout.TimerScreenBounds.Location);

        controller.MoveBy(new System.Drawing.Point(-100, -timerLocation.Y));
        System.Drawing.Point topEdgeTimerLocation = controller.CurrentLayout.TimerScreenBounds.Location;
        Check.Equal(0, topEdgeTimerLocation.Y);
        controller.UpdateContext(settings, statusCount: 7, visibleStatusCount: 7);
        Check.Equal(topEdgeTimerLocation, controller.CurrentLayout.TimerScreenBounds.Location);
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

    private static void TimerProportionalFontLayoutJourney()
    {
        AppSettings settings = AppSettingsDefaults.Create();
        settings.Overlay.Columns.Timer.FontFamily = "Segoe Script";
        settings.Overlay.Columns.TimerMilliseconds.FontFamily = "Segoe Script";
        settings.Overlay.Columns.Timer.Italic = true;
        settings.Overlay.Columns.TimerMilliseconds.Italic = true;
        settings.General.ShowMouseClickThroughIndicator = true;

        using var bitmap = new System.Drawing.Bitmap(1000, 300);
        using System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap);
        using var resources = new OverlayRenderResources();
        Check.True(resources.Fonts.GetColumnFont(settings.Overlay.Columns.Timer, 1f).Italic);
        Check.True(resources.Fonts.GetColumnFont(settings.Overlay.Columns.TimerMilliseconds, 1f).Italic);
        var layout = new SplitLayout(
            new System.Drawing.Rectangle(0, 0, 900, 60),
            new System.Drawing.Rectangle(0, 80, 900, 180),
            0);
        var context = new OverlayRenderContext(
            settings,
            UiPalette.From(settings.Overlay.Colors),
            default,
            [],
            0,
            SplitTimerPhase.Running,
            TimeSpan.FromMinutes(11) + TimeSpan.FromSeconds(11.11),
            layout,
            0,
            MouseClickThrough: false,
            SplitCompletionAnimation: null,
            SegmentBestDeltaHighlights: new Dictionary<int, SegmentBestDeltaHighlight>(),
            NowUtc: DateTime.UtcNow);

        TimerPaintFrame narrowDigits = TimerRenderer.GetTimerPaintFrame(graphics, context, resources);
        TimerPaintFrame wideDigits = TimerRenderer.GetTimerPaintFrame(
            graphics,
            context with { TimerElapsed = TimeSpan.FromMinutes(58) + TimeSpan.FromSeconds(58.88) },
            resources);

        Check.Equal(narrowDigits.Milliseconds.Bounds.X, wideDigits.Milliseconds.Bounds.X);
        Check.Equal(narrowDigits.Indicator.Bounds.X, wideDigits.Indicator.Bounds.X);
    }

    private static void ExpandedConditionEarlyDeltaJourney()
    {
        AppSettings settings = AppSettingsDefaults.Create();
        settings.Overlay.ShowEarlyDeltaTime = true;
        settings.Overlay.EarlyDeltaTimeSeconds = 10;
        var nextCondition = new SplitExpandedConditionRow(
            ConditionIndex: 1,
            SplitCondition.Fact("boss:next"),
            ReferenceTime: TimeSpan.FromSeconds(60),
            CompletionTime: null);

        SplitComparison hidden = OverlayFrameBuilder.GetExpandedComparison(
            settings,
            SplitTimerPhase.Running,
            TimeSpan.FromSeconds(49),
            nextCondition,
            isCurrent: true);
        Check.False(hidden.ShowDelta);

        SplitComparison visible = OverlayFrameBuilder.GetExpandedComparison(
            settings,
            SplitTimerPhase.Running,
            TimeSpan.FromSeconds(50),
            nextCondition,
            isCurrent: true);
        Check.True(visible.ShowDelta);
        Check.Equal(TimeSpan.FromSeconds(-10), visible.Delta);

        SplitComparison completed = OverlayFrameBuilder.GetExpandedComparison(
            settings,
            SplitTimerPhase.Running,
            TimeSpan.FromSeconds(70),
            nextCondition with { CompletionTime = TimeSpan.FromSeconds(58) },
            isCurrent: true);
        Check.True(completed.ShowDelta);
        Check.Equal(TimeSpan.FromSeconds(-2), completed.Delta);
    }

    private sealed class RecordingHotkeyManager :
        IHotkeyRegistrationManager
    {
        public int LastRegisterThreadId { get; private set; }

        public int LastDisposeThreadId { get; private set; }

        public IReadOnlyList<HotkeyRegistrationWarning>
            RegisterConfiguredHotkeys(
                IntPtr windowHandle,
                AppSettings settings)
        {
            LastRegisterThreadId = Environment.CurrentManagedThreadId;
            return [];
        }

        public bool TryGetAction(
            System.Windows.Forms.Message message,
            out HotkeyAction action)
        {
            action = default;
            return false;
        }

        public void Dispose()
        {
            LastDisposeThreadId = Environment.CurrentManagedThreadId;
        }
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
