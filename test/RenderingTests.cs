using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using TerrariaSplit;

namespace TerrariaSplit.Tests;

internal static class RenderingTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("UiPalette maps configured text colors", UiPaletteMapsConfiguredTextColors);
        yield return ("LayeredWindowNative preserves Win32 struct layouts", LayeredWindowNativePreservesWin32StructLayouts);
        yield return ("TextEffectGeometry applies opacity without changing RGB", TextEffectGeometryAppliesOpacity);
        yield return ("TextEffectGeometry fits images without stretching", TextEffectGeometryFitsImagesWithoutStretching);
        yield return ("TextEffectRenderer draws direct styled string", TextEffectRendererDrawsDirectStyledString);
        yield return ("TextEffectRenderer draws direct shadow-only text", TextEffectRendererDrawsDirectShadowOnlyText);
        yield return ("OverlayFontCache keeps main timer font independent from milliseconds visibility", OverlayFontCacheKeepsMainTimerFontIndependentFromMillisecondsVisibility);
        yield return ("OverlayFontCache honors configured font families", OverlayFontCacheHonorsConfiguredFontFamilies);
        yield return ("BossIconCache crops animated item textures", BossIconCacheCropsAnimatedItemTextures);
        yield return ("BossIconCache loads boss icons from Icons Bosses", BossIconCacheLoadsBossIconsFromIconsBosses);
        yield return ("SplitSoundSelector routes equal times to not-faster sounds", SplitSoundSelectorRoutesEqualTimesToNotFasterSounds);
        yield return ("SplitSoundSelector treats missing comparison data as faster", SplitSoundSelectorTreatsMissingComparisonDataAsFaster);
        yield return ("SplitRenderData filters OR completion icons to matched target", SplitRenderDataFiltersOrCompletionIconsToMatchedTarget);
        yield return ("SplitRenderData filters active OR icons to satisfied targets", SplitRenderDataFiltersActiveOrIconsToSatisfiedTargets);
        yield return ("SplitDisplayRows shows attached rows for active following anchor", SplitDisplayRowsShowsAttachedRowsForActiveFollowingAnchor);
        yield return ("SplitDisplayRows expands multi condition rows with reserved height", SplitDisplayRowsExpandsMultiConditionRowsWithReservedHeight);
        yield return ("SplitListRenderer orders satisfied icons first", SplitListRendererOrdersSatisfiedIconsFirst);
        yield return ("SplitListRenderer shows skipped time for skipped splits", SplitListRendererShowsSkippedTimeForSkippedSplits);
        yield return ("SplitListRenderer lights facts only after timer starts", SplitListRendererLightsFactsOnlyAfterTimerStarts);
        yield return ("SplitListRenderer keeps ever owned item icons lit after item leaves inventory", SplitListRendererKeepsEverOwnedItemIconsLitAfterItemLeavesInventory);
        yield return ("SplitListRenderer lights target override ever owned item icons", SplitListRendererLightsTargetOverrideEverOwnedItemIcons);
        yield return ("SplitListRenderer preserves current split depth curve", SplitListRendererPreservesCurrentSplitDepthCurve);
        yield return ("SplitListRenderer partial region redraw matches full render", SplitListRendererPartialRegionMatchesFullRender);
        yield return ("SplitCompletionAnimationFactory filters OR completion icons to matched target", SplitCompletionAnimationFactoryFiltersOrCompletionIconsToMatchedTarget);
        yield return ("SplitCompletionAnimationFactory creates animation with split delta", SplitCompletionAnimationFactoryCreatesAnimationWithSplitDelta);
        yield return ("SplitCompletionAnimationRenderer preserves fade curve", SplitCompletionAnimationRendererPreservesFadeCurve);
        yield return ("SplitCompletionAnimationRenderer preserves delta slide curve", SplitCompletionAnimationRendererPreservesDeltaSlideCurve);
        yield return ("SplitCompletionAnimationRenderer centers on rendered rows", SplitCompletionAnimationRendererCentersOnRenderedRows);
        yield return ("OverlayTextStyles can ignore attached groups for timer comparison", OverlayTextStylesCanIgnoreAttachedGroupsForTimerComparison);
        yield return ("OverlayTextStyles maps text effect percentages", OverlayTextStylesMapsTextEffectPercentages);
    }

    private static void UiPaletteMapsConfiguredTextColors()
    {
        var colors = new UiColorSettings
        {
            TimerText = "#112233",
            TimerTextOutline = "#445566",
            TimerTextShadow = "#778899",
            SplitCompletionSegmentLabelText = "#8899AA",
            SplitCompletionLabelText = "#99AABB",
            SplitCompletionSegmentTimeText = "#AABBCC",
            SplitCompletionTimeText = "#BBCCDD"
        };

        UiPalette palette = UiPalette.From(colors);

        TestAssert.Equal(Color.FromArgb(0x11, 0x22, 0x33), palette.TimerText);
        TestAssert.Equal(Color.FromArgb(0x44, 0x55, 0x66), palette.TimerTextOutline);
        TestAssert.Equal(Color.FromArgb(0x77, 0x88, 0x99), palette.TimerTextShadow);
        TestAssert.Equal(Color.FromArgb(0x88, 0x99, 0xAA), palette.SplitCompletionSegmentLabelText);
        TestAssert.Equal(Color.FromArgb(0x99, 0xAA, 0xBB), palette.SplitCompletionLabelText);
        TestAssert.Equal(Color.FromArgb(0xAA, 0xBB, 0xCC), palette.SplitCompletionSegmentTimeText);
        TestAssert.Equal(Color.FromArgb(0xBB, 0xCC, 0xDD), palette.SplitCompletionTimeText);
    }

    private static void LayeredWindowNativePreservesWin32StructLayouts()
    {
        TestAssert.Equal(8, Marshal.SizeOf<LayeredWindowNative.NativePoint>());
        TestAssert.Equal(8, Marshal.SizeOf<LayeredWindowNative.NativeSize>());
        TestAssert.Equal(16, Marshal.SizeOf<LayeredWindowNative.NativeRect>());
        TestAssert.Equal(4, Marshal.SizeOf<LayeredWindowNative.BlendFunction>());
        TestAssert.Equal(40, Marshal.SizeOf<LayeredWindowNative.BitmapInfoHeader>());
        TestAssert.Equal(44, Marshal.SizeOf<LayeredWindowNative.BitmapInfo>());
        TestAssert.Equal(IntPtr.Size == 8 ? 80 : 40, Marshal.SizeOf<LayeredWindowNative.UpdateLayeredWindowInfo>());
    }

    private static void TextEffectGeometryAppliesOpacity()
    {
        Color color = TextEffectGeometry.WithOpacity(Color.FromArgb(200, 10, 20, 30), 0.5f);

        TestAssert.Equal(100, color.A);
        TestAssert.Equal(10, color.R);
        TestAssert.Equal(20, color.G);
        TestAssert.Equal(30, color.B);
    }

    private static void TextEffectGeometryFitsImagesWithoutStretching()
    {
        using var wide = new Bitmap(40, 20);
        Rectangle wideBounds = TextEffectGeometry.GetAspectFitBounds(wide.Size, new Rectangle(10, 20, 32, 32));
        TestAssert.Equal(new Rectangle(10, 28, 32, 16), wideBounds);

        using var tall = new Bitmap(20, 40);
        Rectangle tallBounds = TextEffectGeometry.GetAspectFitBounds(tall.Size, new Rectangle(10, 20, 32, 32));
        TestAssert.Equal(new Rectangle(18, 20, 16, 32), tallBounds);
    }

    private static void TextEffectRendererDrawsDirectStyledString()
    {
        using var bitmap = new Bitmap(180, 80);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        using var font = new Font(UiTheme.FontFamilyName, 32f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var format = new StringFormat(StringFormat.GenericTypographic);

        TextEffectRenderer.DrawStyledString(
            graphics,
            "12.34",
            font,
            new TextRenderStyle(Color.White, Color.Black, Color.Black, 50, 50),
            4f,
            8f,
            format,
            1f,
            supersampleEffects: false);

        TestAssert.Equal(true, HasVisiblePixel(bitmap));
    }

    private static void TextEffectRendererDrawsDirectShadowOnlyText()
    {
        using var bitmap = new Bitmap(180, 80);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        using var font = new Font(UiTheme.FontFamilyName, 30f, FontStyle.Regular, GraphicsUnit.Pixel);

        TextEffectRenderer.DrawStyledText(
            graphics,
            "Delta",
            font,
            new TextRenderStyle(Color.White, Color.Black, Color.Black, 70, 0),
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ContentAlignment.MiddleCenter,
            1f,
            supersampleEffects: false);

        TestAssert.Equal(true, HasVisiblePixel(bitmap));
    }

    private static void OverlayFontCacheKeepsMainTimerFontIndependentFromMillisecondsVisibility()
    {
        var settings = new AppSettings();
        settings.Overlay.Columns.ScalePercent = 150;
        settings.Overlay.Columns.Timer.FontSize = 38;
        settings.Overlay.Columns.Timer.Show = true;
        settings.Overlay.Columns.TimerMilliseconds.FontSize = 12;
        settings.Overlay.Columns.TimerMilliseconds.Show = true;

        float withMilliseconds = OverlayFontCache.GetColumnFontSize(
            settings.Overlay.Columns.Timer,
            OverlayRenderContext.GetScaleFactor(settings));

        settings.Overlay.Columns.TimerMilliseconds.Show = false;
        float withoutMilliseconds = OverlayFontCache.GetColumnFontSize(
            settings.Overlay.Columns.Timer,
            OverlayRenderContext.GetScaleFactor(settings));

        TestAssert.Equal(withMilliseconds, withoutMilliseconds);
        Nearly(57f, withMilliseconds);
    }

    private static void OverlayFontCacheHonorsConfiguredFontFamilies()
    {
        using var resources = new OverlayRenderResources();
        using Font defaultProbe = UiFontSettings.CreateFont(UiFontSettings.DefaultFamilyName, 20f, FontStyle.Regular);
        var missingSettings = new UiColumnSettings
        {
            FontFamily = "Definitely Missing TerrariaSplit Font",
            FontSize = 20f
        };

        Font missingFont = resources.Fonts.GetColumnFont(missingSettings, 1f);
        TestAssert.Equal(defaultProbe.FontFamily.Name, missingFont.FontFamily.Name);

        string? alternateFamily = FindRenderableAlternateFontFamily(defaultProbe.FontFamily.Name);
        if (alternateFamily is null)
        {
            return;
        }

        var defaultSettings = new UiColumnSettings
        {
            FontFamily = UiFontSettings.DefaultFamilyName,
            FontSize = 20f
        };
        var alternateSettings = new UiColumnSettings
        {
            FontFamily = alternateFamily,
            FontSize = 20f
        };

        Font defaultFont = resources.Fonts.GetColumnFont(defaultSettings, 1f);
        Font alternateFont = resources.Fonts.GetColumnFont(alternateSettings, 1f);
        TestAssert.Equal(false, ReferenceEquals(defaultFont, alternateFont));
        TestAssert.Equal(alternateFamily, alternateFont.FontFamily.Name);
    }

    private static string? FindRenderableAlternateFontFamily(string defaultRenderedFamily)
    {
        foreach (string family in UiFontSettings.GetInstalledFamilyNames())
        {
            if (string.Equals(family, defaultRenderedFamily, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using Font probe = UiFontSettings.CreateFont(family, 20f, FontStyle.Regular);
            if (string.Equals(probe.FontFamily.Name, family, StringComparison.OrdinalIgnoreCase))
            {
                return family;
            }
        }

        return null;
    }

    private static void BossIconCacheCropsAnimatedItemTextures()
    {
        TestAssert.Equal(true, ItemIconAnimationCatalog.TryGetAnimation(75, out ItemIconAnimation animation));
        TestAssert.Equal(8, animation.FrameCount);

        string directory = Path.Combine(
            Path.GetTempPath(),
            "TerrariaSplit.Tests",
            "item-icon-animation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "Item_75.png");
        try
        {
            using (var spriteSheet = new Bitmap(4, 80, PixelFormat.Format32bppArgb))
            {
                using Graphics graphics = Graphics.FromImage(spriteSheet);
                graphics.Clear(Color.Transparent);
                using var red = new SolidBrush(Color.Red);
                using var blue = new SolidBrush(Color.Blue);
                graphics.FillRectangle(red, 0, 0, 4, 8);
                graphics.FillRectangle(blue, 0, 8, 4, 2);
                graphics.FillRectangle(blue, 0, 10, 4, 10);
                spriteSheet.Save(path, ImageFormat.Png);
            }

            var settings = new AppSettings();
            var definition = new SplitDefinition(
                "split:item-75",
                "Falling Star",
                SplitCatalog.CreateItemEverOwnedCondition(75, 1),
                [path],
                [SplitCatalog.CreateItemTargetId(75)],
                [SplitCatalog.CreateItemTargetId(75)]);

            using var cache = new BossIconCache();
            IconPair icons = cache.Load(definition, path, settings);

            using var lit = new Bitmap(icons.Lit);
            TestAssert.Equal(4, lit.Width);
            TestAssert.Equal(8, lit.Height);
            TestAssert.Equal(Color.Red.ToArgb(), lit.GetPixel(0, 0).ToArgb());
            TestAssert.Equal(Color.Red.ToArgb(), lit.GetPixel(0, lit.Height - 1).ToArgb());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void BossIconCacheLoadsBossIconsFromIconsBosses()
    {
        string expectedPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "Bosses", "king-slime.png");
        string itemPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "Items", "Item_50.png");
        TestAssert.Equal(true, File.Exists(expectedPath));
        TestAssert.Equal(true, File.Exists(itemPath));

        using var expected = new Bitmap(expectedPath);
        using var itemIcon = new Bitmap(itemPath);
        var definition = new SplitDefinition(
            "split:boss-king-slime",
            "King Slime",
            SplitCatalog.CreateBossFactCondition("boss:king-slime"),
            ["king-slime.png"],
            ["boss:king-slime"],
            ["boss:king-slime"]);

        using var cache = new BossIconCache();
        IconPair icons = cache.Load(definition, "king-slime.png", new AppSettings());
        using var lit = new Bitmap(icons.Lit);

        TestAssert.Equal(expected.Width, lit.Width);
        TestAssert.Equal(expected.Height, lit.Height);
        TestAssert.Equal(false, lit.Width == itemIcon.Width && lit.Height == itemIcon.Height);
    }

    private static void SplitSoundSelectorRoutesEqualTimesToNotFasterSounds()
    {
        var sounds = new UiSoundSettings
        {
            SplitBehindReferenceBehindSegment = "normal-notfaster-notfaster.wav",
            SplitAheadReferenceAheadSegment = "normal-faster-faster.wav",
            MoonLordBehindReferenceBehindSegment = "moonlord-notfaster-notfaster.wav"
        };
        var normalDefinition = new SplitDefinition(
            "split:skeletron",
            "Skeletron",
            SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
            Array.Empty<string>(),
            Array.Empty<string>(),
            [SplitCatalog.Skeletron]);
        var moonLordDefinition = new SplitDefinition(
            "split:moon-lord",
            "Moon Lord",
            SplitCatalog.CreateBossFactCondition(SplitCatalog.MoonLord),
            Array.Empty<string>(),
            Array.Empty<string>(),
            [SplitCatalog.MoonLord]);

        TestAssert.Equal(
            "normal-notfaster-notfaster.wav",
            SplitSoundSelector.GetPath(
                sounds,
                normalDefinition,
                cumulativeFasterThanReference: false,
                segmentFasterThanPersonalBest: false));
        TestAssert.Equal(
            "normal-faster-faster.wav",
            SplitSoundSelector.GetPath(
                sounds,
                normalDefinition,
                cumulativeFasterThanReference: true,
                segmentFasterThanPersonalBest: true));
        TestAssert.Equal(
            "moonlord-notfaster-notfaster.wav",
            SplitSoundSelector.GetPath(
                sounds,
                moonLordDefinition,
                cumulativeFasterThanReference: false,
                segmentFasterThanPersonalBest: false));
    }

    private static void SplitSoundSelectorTreatsMissingComparisonDataAsFaster()
    {
        var sounds = new UiSoundSettings
        {
            SplitBehindReferenceBehindSegment = "normal-notfaster-notfaster.wav",
            SplitAheadReferenceAheadSegment = "normal-faster-faster.wav"
        };
        var definition = new SplitDefinition(
            "split:skeletron",
            "Skeletron",
            SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
            Array.Empty<string>(),
            Array.Empty<string>(),
            [SplitCatalog.Skeletron]);

        TestAssert.Equal(
            "normal-faster-faster.wav",
            SplitSoundSelector.GetPath(
                sounds,
                definition,
                splitTime: TimeSpan.FromMinutes(3),
                referenceSplit: null,
                segmentTime: null,
                personalBestSegment: null));
        TestAssert.Equal(
            "normal-notfaster-notfaster.wav",
            SplitSoundSelector.GetPath(
                sounds,
                definition,
                splitTime: TimeSpan.FromMinutes(3),
                referenceSplit: TimeSpan.FromMinutes(3),
                segmentTime: TimeSpan.FromMinutes(3),
                personalBestSegment: TimeSpan.FromMinutes(3)));
    }

    private static void SplitRenderDataFiltersOrCompletionIconsToMatchedTarget()
    {
        SplitDefinition definition = CreateAnyBossDefinition();
        SplitCondition wallOfFlesh = SplitCatalog.CreateBossFactCondition(SplitCatalog.WallOfFlesh);
        var status = new SplitStatusSnapshot(
            definition,
            TimeSpan.FromSeconds(10),
            IsSkipped: false,
            CompletedFactKeys: [wallOfFlesh.FactKey]);

        SplitDefinition display = SplitRenderData.GetDisplayDefinition(status);

        TestAssert.Equal(definition.Id, display.Id);
        TestAssert.Equal(1, display.IconKeys.Count);
        TestAssert.Equal(SplitCatalog.WallOfFlesh, display.IconKeys.Single());
        TestAssert.Equal("wof.png", display.IconFileNames.Single());
        TestAssert.Equal(2, definition.IconKeys.Count);

        var laterStatus = status with
        {
            CompletedFactKeys =
            [
                wallOfFlesh.FactKey,
                SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron).FactKey
            ]
        };
        SplitDefinition laterDisplay = SplitRenderData.GetDisplayDefinition(laterStatus);

        TestAssert.Equal(2, laterDisplay.IconKeys.Count);
        TestAssert.Equal(SplitCatalog.Skeletron, laterDisplay.IconKeys[0]);
        TestAssert.Equal(SplitCatalog.WallOfFlesh, laterDisplay.IconKeys[1]);
    }

    private static void SplitRenderDataFiltersActiveOrIconsToSatisfiedTargets()
    {
        SplitDefinition definition = CreateAnyBossDefinition();
        SplitCondition skeletron = SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron);
        SplitCondition wallOfFlesh = SplitCatalog.CreateBossFactCondition(SplitCatalog.WallOfFlesh);
        var status = new SplitStatusSnapshot(
            definition,
            null,
            IsSkipped: false,
            CompletedFactKeys: []);

        SplitDefinition display = SplitRenderData.GetDisplayDefinition(
            status,
            CreateFacts(
                (skeletron.FactKey, false),
                (wallOfFlesh.FactKey, true)));

        TestAssert.Equal(1, display.IconKeys.Count);
        TestAssert.Equal(SplitCatalog.WallOfFlesh, display.IconKeys.Single());
        TestAssert.Equal("wof.png", display.IconFileNames.Single());

        SplitDefinition laterDisplay = SplitRenderData.GetDisplayDefinition(
            status,
            CreateFacts(
                (skeletron.FactKey, true),
                (wallOfFlesh.FactKey, true)));

        TestAssert.Equal(2, laterDisplay.IconKeys.Count);
        TestAssert.Equal(SplitCatalog.Skeletron, laterDisplay.IconKeys[0]);
        TestAssert.Equal(SplitCatalog.WallOfFlesh, laterDisplay.IconKeys[1]);

        IReadOnlyList<string> matchedFactKeys = definition.GetMatchedFactKeys(CreateFacts(
            (skeletron.FactKey, true),
            (wallOfFlesh.FactKey, true)));
        TestAssert.Equal(2, matchedFactKeys.Count);
        TestAssert.Equal(true, matchedFactKeys.Contains(skeletron.FactKey, StringComparer.OrdinalIgnoreCase));
        TestAssert.Equal(true, matchedFactKeys.Contains(wallOfFlesh.FactKey, StringComparer.OrdinalIgnoreCase));
    }

    private static void SplitDisplayRowsShowsAttachedRowsForActiveFollowingAnchor()
    {
        SplitDefinition previousA = CreateDisplayRowDefinition("split:previous-a", isAttached: false);
        SplitDefinition previousB = CreateDisplayRowDefinition("split:previous-b", isAttached: false);
        SplitDefinition attachedA = CreateDisplayRowDefinition("split:attached-a", isAttached: true);
        SplitDefinition attachedB = CreateDisplayRowDefinition("split:attached-b", isAttached: true);
        SplitDefinition parent = CreateDisplayRowDefinition("split:parent", isAttached: false);
        SplitDefinition next = CreateDisplayRowDefinition("split:next", isAttached: false);
        var hiddenBeforePreviousCompletion = new[]
        {
            new SplitStatusSnapshot(previousA, null, IsSkipped: false, CompletedFactKeys: []),
            new SplitStatusSnapshot(previousB, null, IsSkipped: false, CompletedFactKeys: []),
            new SplitStatusSnapshot(attachedA, null, IsSkipped: false, CompletedFactKeys: []),
            new SplitStatusSnapshot(attachedB, null, IsSkipped: false, CompletedFactKeys: []),
            new SplitStatusSnapshot(parent, null, IsSkipped: false, CompletedFactKeys: []),
            new SplitStatusSnapshot(next, null, IsSkipped: false, CompletedFactKeys: [])
        };

        IReadOnlyList<SplitDisplayRow> hiddenBeforePreviousCompletionRows =
            SplitDisplayRows.Build(hiddenBeforePreviousCompletion);
        TestAssert.Equal(4, hiddenBeforePreviousCompletionRows.Count);
        TestAssert.Equal(6, SplitDisplayRows.GetRequiredRowCount(hiddenBeforePreviousCompletion));
        TestAssert.Equal(new SplitDisplayRow(0, 2), hiddenBeforePreviousCompletionRows[0]);
        TestAssert.Equal(new SplitDisplayRow(1, 3), hiddenBeforePreviousCompletionRows[1]);
        TestAssert.Equal(new SplitDisplayRow(4, 4), hiddenBeforePreviousCompletionRows[2]);
        TestAssert.Equal(new SplitDisplayRow(5, 5), hiddenBeforePreviousCompletionRows[3]);

        var showAllAttachedSettings = new AppSettings { Route = { AutoHideAttachedGroups = false } };
        IReadOnlyList<SplitDisplayRow> showAllBeforePreviousCompletionRows =
            SplitDisplayRows.Build(showAllAttachedSettings, hiddenBeforePreviousCompletion);
        TestAssert.Equal(6, showAllBeforePreviousCompletionRows.Count);
        for (int i = 0; i < showAllBeforePreviousCompletionRows.Count; i++)
        {
            TestAssert.Equal(new SplitDisplayRow(i, i), showAllBeforePreviousCompletionRows[i]);
        }

        var visible = hiddenBeforePreviousCompletion.ToArray();
        visible[0] = new SplitStatusSnapshot(previousA, TimeSpan.FromSeconds(1), IsSkipped: false, CompletedFactKeys: []);
        visible[1] = new SplitStatusSnapshot(previousB, null, IsSkipped: true, CompletedFactKeys: []);
        IReadOnlyList<SplitDisplayRow> visibleRows = SplitDisplayRows.Build(visible);

        TestAssert.Equal(6, visibleRows.Count);
        TestAssert.Equal(6, SplitDisplayRows.GetRequiredRowCount(visible));
        for (int i = 0; i < visibleRows.Count; i++)
        {
            TestAssert.Equal(new SplitDisplayRow(i, i), visibleRows[i]);
        }

        var completedOutOfOrder = visible.ToArray();
        completedOutOfOrder[3] = new SplitStatusSnapshot(attachedB, TimeSpan.FromSeconds(2), IsSkipped: false, CompletedFactKeys: []);
        IReadOnlyList<SplitDisplayRow> completedOutOfOrderRows = SplitDisplayRows.Build(completedOutOfOrder);

        TestAssert.Equal(new SplitDisplayRow(3, 2), completedOutOfOrderRows[2]);
        TestAssert.Equal(new SplitDisplayRow(2, 3), completedOutOfOrderRows[3]);
        TestAssert.Equal(new SplitDisplayRow(4, 4), completedOutOfOrderRows[4]);

        completedOutOfOrder[2] = new SplitStatusSnapshot(attachedA, TimeSpan.FromSeconds(5), IsSkipped: false, CompletedFactKeys: []);
        IReadOnlyList<SplitDisplayRow> completedByTimeRows = SplitDisplayRows.Build(completedOutOfOrder);

        TestAssert.Equal(new SplitDisplayRow(3, 2), completedByTimeRows[2]);
        TestAssert.Equal(new SplitDisplayRow(2, 3), completedByTimeRows[3]);

        var skippedBeforeTimed = visible.ToArray();
        skippedBeforeTimed[2] = new SplitStatusSnapshot(attachedA, null, IsSkipped: true, CompletedFactKeys: ["fact:attached-a"]);
        skippedBeforeTimed[3] = new SplitStatusSnapshot(attachedB, TimeSpan.FromSeconds(2), IsSkipped: false, CompletedFactKeys: []);
        IReadOnlyList<SplitDisplayRow> skippedBeforeTimedRows = SplitDisplayRows.Build(skippedBeforeTimed);

        TestAssert.Equal(new SplitDisplayRow(2, 2), skippedBeforeTimedRows[2]);
        TestAssert.Equal(new SplitDisplayRow(3, 3), skippedBeforeTimedRows[3]);

        var hiddenAfterCompletedAnchor = visible.ToArray();
        hiddenAfterCompletedAnchor[4] = new SplitStatusSnapshot(parent, TimeSpan.FromSeconds(1), IsSkipped: false, CompletedFactKeys: []);
        IReadOnlyList<SplitDisplayRow> hiddenAfterCompletedAnchorRows = SplitDisplayRows.Build(hiddenAfterCompletedAnchor);

        TestAssert.Equal(4, hiddenAfterCompletedAnchorRows.Count);
        TestAssert.Equal(new SplitDisplayRow(0, 2), hiddenAfterCompletedAnchorRows[0]);
        TestAssert.Equal(new SplitDisplayRow(1, 3), hiddenAfterCompletedAnchorRows[1]);
        TestAssert.Equal(new SplitDisplayRow(4, 4), hiddenAfterCompletedAnchorRows[2]);
        TestAssert.Equal(new SplitDisplayRow(5, 5), hiddenAfterCompletedAnchorRows[3]);

        IReadOnlyList<SplitDisplayRow> showAllAfterCompletedAnchorRows =
            SplitDisplayRows.Build(showAllAttachedSettings, hiddenAfterCompletedAnchor);
        TestAssert.Equal(6, showAllAfterCompletedAnchorRows.Count);
        for (int i = 0; i < showAllAfterCompletedAnchorRows.Count; i++)
        {
            TestAssert.Equal(new SplitDisplayRow(i, i), showAllAfterCompletedAnchorRows[i]);
        }

        var firstAttached = new[]
        {
            new SplitStatusSnapshot(attachedA, null, IsSkipped: false, CompletedFactKeys: []),
            new SplitStatusSnapshot(parent, null, IsSkipped: false, CompletedFactKeys: []),
            new SplitStatusSnapshot(next, null, IsSkipped: false, CompletedFactKeys: [])
        };
        IReadOnlyList<SplitDisplayRow> firstAttachedRows = SplitDisplayRows.Build(firstAttached);

        TestAssert.Equal(3, firstAttachedRows.Count);
        TestAssert.Equal(new SplitDisplayRow(0, 0), firstAttachedRows[0]);
        TestAssert.Equal(new SplitDisplayRow(1, 1), firstAttachedRows[1]);
        TestAssert.Equal(new SplitDisplayRow(2, 2), firstAttachedRows[2]);

        var hiddenFirstAttached = firstAttached.ToArray();
        hiddenFirstAttached[1] = new SplitStatusSnapshot(parent, TimeSpan.FromSeconds(1), IsSkipped: false, CompletedFactKeys: []);
        IReadOnlyList<SplitDisplayRow> hiddenFirstAttachedRows = SplitDisplayRows.Build(hiddenFirstAttached);

        TestAssert.Equal(2, hiddenFirstAttachedRows.Count);
        TestAssert.Equal(new SplitDisplayRow(1, 1), hiddenFirstAttachedRows[0]);
        TestAssert.Equal(new SplitDisplayRow(2, 2), hiddenFirstAttachedRows[1]);
    }

    private static void SplitDisplayRowsExpandsMultiConditionRowsWithReservedHeight()
    {
        SplitCondition factA = SplitCondition.Fact("fact:a");
        SplitCondition factB = SplitCondition.Fact("fact:b");
        SplitCondition factC = SplitCondition.Fact("fact:c");
        SplitCondition condition = SplitCondition.AtLeast([factA, factB, factC], 2);
        SplitDefinition previous = CreateDisplayRowDefinition("split:previous", isAttached: false);
        SplitDefinition next = CreateDisplayRowDefinition("split:next", isAttached: false);
        var expanded = new SplitDefinition(
            "split:expanded",
            "Expanded",
            condition,
            ["a.png", "b.png", "c.png"],
            ["target:a", "target:b", "target:c"],
            ["target:a", "target:b", "target:c"]);
        AppSettings settings = CreateExpandedRowsSettings(expanded, condition);
        IReadOnlyList<SplitConditionDataRow> conditionRows = SplitConditionDataRows.ForSplit(settings, expanded.Id).ToList();
        settings.Comparison.ReferenceSplitSets[0].Splits[conditionRows[0].Key] = "00:30.00";
        settings.Comparison.ReferenceSplitSets[0].Splits[conditionRows[1].Key] = "00:20.00";
        settings.Comparison.ReferenceSplitSets[0].Splits[conditionRows[2].Key] = "00:40.00";

        SplitStatusSnapshot previousCompleted = new(previous, TimeSpan.FromSeconds(1), IsSkipped: false, CompletedFactKeys: []);
        SplitStatusSnapshot expandedPending = new(expanded, null, IsSkipped: false, CompletedFactKeys: []);
        var statuses = new[] { previousCompleted, expandedPending };

        IReadOnlyList<SplitDisplayRow> pendingRows = SplitDisplayRows.Build(settings, statuses);
        TestAssert.Equal(2, pendingRows.Count);
        TestAssert.Equal(2, SplitDisplayRows.GetRequiredRowCount(settings, statuses));
        TestAssert.Equal(3, SplitDisplayRows.GetReservedRowCount(settings, statuses));
        TestAssert.Equal(new SplitDisplayRow(0, 0), pendingRows[0]);
        TestAssert.Equal(new SplitDisplayRow(1, 1, 1), pendingRows[1]);

        IReadOnlyList<SplitExpandedConditionRow> pendingExpandedRows =
            SplitExpandedConditionRows.Build(settings, statuses, statusIndex: 1);
        TestAssert.Equal(1, pendingExpandedRows.Count);
        TestAssert.Equal(TimeSpan.FromSeconds(20), pendingExpandedRows[0].ReferenceTime);

        SplitStatusSnapshot cCompleted = expandedPending with
        {
            FactCompletionTimes = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
            {
                [factC.FactKey] = TimeSpan.FromSeconds(5)
            }
        };
        var partiallyCompleted = new[] { previousCompleted, cCompleted };

        IReadOnlyList<SplitDisplayRow> completedFirstRows = SplitDisplayRows.Build(settings, partiallyCompleted);
        TestAssert.Equal(3, completedFirstRows.Count);
        TestAssert.Equal(new SplitDisplayRow(1, 1, 2), completedFirstRows[1]);
        TestAssert.Equal(new SplitDisplayRow(1, 2, 1), completedFirstRows[2]);

        IReadOnlyList<SplitExpandedConditionRow> completedExpandedRows =
            SplitExpandedConditionRows.Build(settings, partiallyCompleted, statusIndex: 1);
        TestAssert.Equal(2, completedExpandedRows.Count);
        TestAssert.Equal(TimeSpan.FromSeconds(5), completedExpandedRows[0].CompletionTime);
        TestAssert.Equal(TimeSpan.FromSeconds(40), completedExpandedRows[0].ReferenceTime);

        settings.Route.CollapseSplitDetailsOnCompletion = false;
        SplitStatusSnapshot completedKeyWithoutFactTime = expandedPending with
        {
            Time = TimeSpan.FromSeconds(25),
            CompletedFactKeys = [factA.FactKey],
            FactCompletionTimes = null
        };
        IReadOnlyList<SplitExpandedConditionRow> completedKeyRows =
            SplitExpandedConditionRows.Build(settings, [previousCompleted, completedKeyWithoutFactTime], statusIndex: 1);
        TestAssert.Equal(0, completedKeyRows[0].ConditionIndex);
        TestAssert.Equal(TimeSpan.Zero, completedKeyRows[0].CompletionTime);
        settings.Route.CollapseSplitDetailsOnCompletion = true;

        var blocked = new[]
        {
            new SplitStatusSnapshot(previous, null, IsSkipped: false, CompletedFactKeys: []),
            expandedPending
        };
        IReadOnlyList<SplitDisplayRow> blockedRows = SplitDisplayRows.Build(settings, blocked);
        TestAssert.Equal(2, blockedRows.Count);
        TestAssert.Equal(2, SplitDisplayRows.GetRequiredRowCount(settings, blocked));
        TestAssert.Equal(3, SplitDisplayRows.GetReservedRowCount(settings, blocked));

        var blockedWithNext = new[]
        {
            blocked[0],
            blocked[1],
            new SplitStatusSnapshot(next, null, IsSkipped: false, CompletedFactKeys: [])
        };
        IReadOnlyList<SplitDisplayRow> blockedWithNextRows = SplitDisplayRows.Build(settings, blockedWithNext);
        TestAssert.Equal(3, blockedWithNextRows.Count);
        TestAssert.Equal(new SplitDisplayRow(0, 0), blockedWithNextRows[0]);
        TestAssert.Equal(new SplitDisplayRow(1, 1), blockedWithNextRows[1]);
        TestAssert.Equal(new SplitDisplayRow(2, 2), blockedWithNextRows[2]);
        TestAssert.Equal(3, SplitDisplayRows.GetRequiredRowCount(settings, blockedWithNext));
        TestAssert.Equal(4, SplitDisplayRows.GetReservedRowCount(settings, blockedWithNext));

        SplitStatusSnapshot expandedCompleted = expandedPending with
        {
            Time = TimeSpan.FromSeconds(25),
            CompletedFactKeys = [factA.FactKey, factB.FactKey],
            FactCompletionTimes = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
            {
                [factA.FactKey] = TimeSpan.FromSeconds(20),
                [factB.FactKey] = TimeSpan.FromSeconds(25)
            }
        };
        var completed = new[] { previousCompleted, expandedCompleted };
        TestAssert.Equal(2, SplitDisplayRows.Build(settings, completed).Count);

        settings.Route.CollapseSplitDetailsOnCompletion = false;
        TestAssert.Equal(3, SplitDisplayRows.Build(settings, completed).Count);

        var attached = expanded with { IsAttached = true };
        var attachedStatuses = new[]
        {
            previousCompleted,
            new SplitStatusSnapshot(attached, null, IsSkipped: false, CompletedFactKeys: []),
            new SplitStatusSnapshot(next, null, IsSkipped: false, CompletedFactKeys: [])
        };
        TestAssert.Equal(3, SplitDisplayRows.Build(settings, attachedStatuses).Count);
        TestAssert.Equal(3, SplitDisplayRows.GetReservedRowCount(settings, attachedStatuses));
    }

    private static void SplitListRendererShowsSkippedTimeForSkippedSplits()
    {
        SplitDefinition skipped = CreateSkippedTimeDefinition("split:skipped");
        SplitDefinition pending = CreateSkippedTimeDefinition("split:pending");

        TestAssert.Equal(
            true,
            SplitListRenderer.ShouldShowSkippedTime(new SplitStatusSnapshot(
                skipped,
                null,
                IsSkipped: true,
                CompletedFactKeys: [])));
        TestAssert.Equal(
            false,
            SplitListRenderer.ShouldShowSkippedTime(new SplitStatusSnapshot(
                pending,
                null,
                IsSkipped: false,
                CompletedFactKeys: [])));
        TestAssert.Equal(
            false,
            SplitListRenderer.ShouldShowSkippedTime(new SplitStatusSnapshot(
                skipped,
                TimeSpan.FromSeconds(1),
                IsSkipped: true,
                CompletedFactKeys: [])));
    }

    private static void SplitListRendererOrdersSatisfiedIconsFirst()
    {
        SplitCondition skeletron = SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron);
        SplitCondition wallOfFlesh = SplitCatalog.CreateBossFactCondition(SplitCatalog.WallOfFlesh);
        SplitCondition destroyer = SplitCatalog.CreateBossFactCondition(SplitCatalog.Destroyer);
        var definition = new SplitDefinition(
            "split:expanded-icons",
            "Expanded Icons",
            SplitCondition.AtLeast([skeletron, wallOfFlesh, destroyer], 2),
            ["skeletron.png", "wof.png", "destroyer.png"],
            [SplitCatalog.Skeletron, SplitCatalog.WallOfFlesh, SplitCatalog.Destroyer],
            [SplitCatalog.Skeletron, SplitCatalog.WallOfFlesh, SplitCatalog.Destroyer]);
        var status = new SplitStatusSnapshot(
            definition,
            null,
            IsSkipped: false,
            CompletedFactKeys: [],
            FactCompletionTimes: new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
            {
                [destroyer.FactKey] = TimeSpan.FromSeconds(5),
                [skeletron.FactKey] = TimeSpan.FromSeconds(8)
            });
        var context = new OverlayRenderContext(
            new AppSettings(),
            UiPalette.From(new UiColorSettings()),
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts(
                    (skeletron.FactKey, false),
                    (wallOfFlesh.FactKey, false),
                    (destroyer.FactKey, false))),
            [status],
            CurrentSplitIndex: 0,
            SplitTimerPhase.Running,
            TimeSpan.FromSeconds(9),
            new SplitLayout(new Rectangle(0, 0, 160, 32), new Rectangle(0, 40, 160, 64), 6),
            VisibleStatusRowCount: 1,
            MouseClickThrough: false,
            SplitCompletionAnimation: null,
            SegmentBestDeltaHighlights: new Dictionary<int, SegmentBestDeltaHighlight>(),
            NowUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        IReadOnlyList<int> order = InvokeIconDrawOrder(context, status, definition);
        TestAssert.Equal(3, order.Count);
        TestAssert.Equal(2, order[0]);
        TestAssert.Equal(0, order[1]);
        TestAssert.Equal(1, order[2]);
        TestAssert.Equal(true, InvokeIsIconLit(context, status, definition, iconIndex: 2));
        TestAssert.Equal(false, InvokeIsIconLit(context, status, definition, iconIndex: 1));

        SplitStatusSnapshot completedStatus = status with
        {
            Time = TimeSpan.FromSeconds(9)
        };
        IReadOnlyList<int> completedOrder = InvokeIconDrawOrder(context, completedStatus, definition);
        TestAssert.Equal(2, completedOrder.Count);
        TestAssert.Equal(2, completedOrder[0]);
        TestAssert.Equal(0, completedOrder[1]);

        SplitStatusSnapshot orderedByCompletedKeys = status with
        {
            CompletedFactKeys = [destroyer.FactKey, skeletron.FactKey],
            FactCompletionTimes = null
        };
        IReadOnlyList<int> completedKeyOrder = InvokeIconDrawOrder(context, orderedByCompletedKeys, definition);
        TestAssert.Equal(3, completedKeyOrder.Count);
        TestAssert.Equal(2, completedKeyOrder[0]);
        TestAssert.Equal(0, completedKeyOrder[1]);
        TestAssert.Equal(1, completedKeyOrder[2]);

        SplitStatusSnapshot skippedKeyBeforeTimed = status with
        {
            IsSkipped = true,
            CompletedFactKeys = [wallOfFlesh.FactKey],
            FactCompletionTimes = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
            {
                [destroyer.FactKey] = TimeSpan.FromSeconds(5)
            }
        };
        IReadOnlyList<int> skippedKeyBeforeTimedOrder = InvokeIconDrawOrder(context, skippedKeyBeforeTimed, definition);
        TestAssert.Equal(2, skippedKeyBeforeTimedOrder.Count);
        TestAssert.Equal(1, skippedKeyBeforeTimedOrder[0]);
        TestAssert.Equal(2, skippedKeyBeforeTimedOrder[1]);

        SplitStatusSnapshot partialStatus = status with
        {
            FactCompletionTimes = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
            {
                [destroyer.FactKey] = TimeSpan.FromSeconds(5)
            }
        };
        IReadOnlyList<int> partialOrder = InvokeIconDrawOrder(context, partialStatus, definition);
        TestAssert.Equal(3, partialOrder.Count);
        TestAssert.Equal(2, partialOrder[0]);
        TestAssert.Equal(0, partialOrder[1]);
        TestAssert.Equal(1, partialOrder[2]);

        SplitStatusSnapshot laterStatus = status with
        {
            FactCompletionTimes = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
            {
                [destroyer.FactKey] = TimeSpan.FromSeconds(5),
                [skeletron.FactKey] = TimeSpan.FromSeconds(8),
                [wallOfFlesh.FactKey] = TimeSpan.FromSeconds(11)
            }
        };
        IReadOnlyList<int> laterOrder = InvokeIconDrawOrder(context, laterStatus, definition);
        TestAssert.Equal(3, laterOrder.Count);
        TestAssert.Equal(2, laterOrder[0]);
        TestAssert.Equal(0, laterOrder[1]);
        TestAssert.Equal(1, laterOrder[2]);
    }

    private static void SplitListRendererPreservesCurrentSplitDepthCurve()
    {
        var settings = new AppSettings { Overlay = { CurrentSplitHighlightScalePercent = 130,
            CurrentSplitDepthStrengthPercent = 50 } };

        Nearly(1.30f, SplitListRenderer.GetCurrentSplitDepthScale(settings, rowIndex: 2, focusIndex: 2));
        Nearly(1.174f, SplitListRenderer.GetCurrentSplitDepthScale(settings, rowIndex: 1, focusIndex: 2));
        Nearly(1.084f, SplitListRenderer.GetCurrentSplitDepthScale(settings, rowIndex: 0, focusIndex: 2));
        Nearly(1.03f, SplitListRenderer.GetCurrentSplitDepthScale(settings, rowIndex: 5, focusIndex: 2));
        Nearly(1f, SplitListRenderer.GetCurrentSplitDepthScale(settings, rowIndex: 6, focusIndex: 2));

        Nearly(0.8f, SplitListRenderer.GetCurrentSplitDepthOpacity(settings, rowIndex: 2, focusIndex: 2, baseOpacity: 0.8f));
        Nearly(0.608f, SplitListRenderer.GetCurrentSplitDepthOpacity(settings, rowIndex: 1, focusIndex: 2, baseOpacity: 0.8f));
        Nearly(0.432f, SplitListRenderer.GetCurrentSplitDepthOpacity(settings, rowIndex: 0, focusIndex: 2, baseOpacity: 0.8f));
        Nearly(0.304f, SplitListRenderer.GetCurrentSplitDepthOpacity(settings, rowIndex: 5, focusIndex: 2, baseOpacity: 0.8f));
        Nearly(0.224f, SplitListRenderer.GetCurrentSplitDepthOpacity(settings, rowIndex: 6, focusIndex: 2, baseOpacity: 0.8f));
    }

    private static void SplitListRendererLightsFactsOnlyAfterTimerStarts()
    {
        string guideTargetId = SplitCatalog.CreateNpcTargetId(22);
        string guideFactKey = SplitCatalog.CreateNpcPresentFactKey(22);
        var definition = new SplitDefinition(
            "split:guide",
            "Guide",
            SplitCatalog.CreateNpcPresentCondition(22),
            ["NPC_Head_1.png"],
            [guideTargetId],
            [guideTargetId]);
        var status = new SplitStatusSnapshot(
            definition,
            null,
            IsSkipped: false,
            CompletedFactKeys: []);
        var settings = new AppSettings { Overlay = { EnableDefeatedBossIconLighting = true } };
        var context = new OverlayRenderContext(
            settings,
            UiPalette.From(settings.Overlay.Colors),
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts((guideFactKey, true))),
            [status],
            CurrentSplitIndex: 0,
            SplitTimerPhase.NotStarted,
            TimeSpan.Zero,
            new SplitLayout(new Rectangle(0, 0, 120, 32), new Rectangle(0, 40, 120, 64), 6),
            VisibleStatusRowCount: 1,
            MouseClickThrough: false,
            SplitCompletionAnimation: null,
            SegmentBestDeltaHighlights: new Dictionary<int, SegmentBestDeltaHighlight>(),
            NowUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        TestAssert.Equal(false, InvokeIsIconLit(context, status, definition, iconIndex: 0));

        OverlayRenderContext runningContext = context with
        {
            TimerPhase = SplitTimerPhase.Running
        };
        TestAssert.Equal(true, InvokeIsIconLit(runningContext, status, definition, iconIndex: 0));

        OverlayRenderContext missingContext = context with
        {
            Snapshot = TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts((guideFactKey, false)))
        };
        TestAssert.Equal(false, InvokeIsIconLit(missingContext, status, definition, iconIndex: 0));

        var skippedOptionalStatus = status with
        {
            IsSkipped = true
        };
        TestAssert.Equal(false, InvokeIsIconLit(runningContext, skippedOptionalStatus, definition, iconIndex: 0));

        var skippedSatisfiedStatus = status with
        {
            IsSkipped = true,
            CompletedFactKeys = [guideFactKey]
        };
        TestAssert.Equal(true, InvokeIsIconLit(runningContext, skippedSatisfiedStatus, definition, iconIndex: 0));

        SplitCondition skeletron = SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron);
        var bossDefinition = new SplitDefinition(
            "split:skeletron",
            "Skeletron",
            skeletron,
            ["skeletron.png"],
            [SplitCatalog.Skeletron],
            [SplitCatalog.Skeletron]);
        var bossStatus = new SplitStatusSnapshot(
            bossDefinition,
            null,
            IsSkipped: false,
            CompletedFactKeys: []);
        OverlayRenderContext bossContext = context with
        {
            Snapshot = TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts((skeletron.FactKey, true)))
        };

        TestAssert.Equal(false, InvokeIsIconLit(bossContext, bossStatus, bossDefinition, iconIndex: 0));
    }

    private static void SplitListRendererKeepsEverOwnedItemIconsLitAfterItemLeavesInventory()
    {
        const int itemId = 50;
        string itemTargetId = SplitCatalog.CreateItemTargetId(itemId);
        string currentItemFactKey = SplitCatalog.CreateItemFactKey(itemId);
        string everOwnedFactKey = SplitCatalog.CreateItemEverOwnedFactKey(itemId);
        var definition = new SplitDefinition(
            "split:item-50",
            "Item",
            SplitCatalog.CreateItemEverOwnedCondition(itemId, 2),
            ["item-50.png"],
            [itemTargetId],
            [itemTargetId]);
        var status = new SplitStatusSnapshot(
            definition,
            null,
            IsSkipped: false,
            CompletedFactKeys: [],
            FactCompletionTimes: new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
            {
                [everOwnedFactKey] = TimeSpan.FromSeconds(4)
            });
        var settings = new AppSettings { Overlay = { EnableDefeatedBossIconLighting = true } };
        var context = new OverlayRenderContext(
            settings,
            UiPalette.From(settings.Overlay.Colors),
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts((currentItemFactKey, 0))),
            [status],
            CurrentSplitIndex: 0,
            SplitTimerPhase.Running,
            TimeSpan.FromSeconds(5),
            new SplitLayout(new Rectangle(0, 0, 120, 32), new Rectangle(0, 40, 120, 64), 6),
            VisibleStatusRowCount: 1,
            MouseClickThrough: false,
            SplitCompletionAnimation: null,
            SegmentBestDeltaHighlights: new Dictionary<int, SegmentBestDeltaHighlight>(),
            NowUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        TestAssert.Equal(true, InvokeIsIconLit(context, status, definition, iconIndex: 0));

        var completedStatus = status with
        {
            CompletedFactKeys = [everOwnedFactKey],
            FactCompletionTimes = new Dictionary<string, TimeSpan>()
        };
        TestAssert.Equal(true, InvokeIsIconLit(context, completedStatus, definition, iconIndex: 0));

        var unlitStatus = status with
        {
            CompletedFactKeys = [],
            FactCompletionTimes = null
        };
        TestAssert.Equal(false, InvokeIsIconLit(context, unlitStatus, definition, iconIndex: 0));
    }

    private static void SplitListRendererLightsTargetOverrideEverOwnedItemIcons()
    {
        const int itemId = 520;
        string otherItemTargetId = SplitCatalog.CreateItemTargetId(43);
        string otherItemFactKey = SplitCatalog.CreateItemFactKey(43);
        string itemTargetId = SplitCatalog.CreateItemTargetId(itemId);
        string currentItemFactKey = SplitCatalog.CreateItemFactKey(itemId);
        string everOwnedFactKey = SplitCatalog.CreateItemEverOwnedFactKey(itemId);
        var settings = new AppSettings
        {
            Overlay =
            {
                EnableDefeatedBossIconLighting = true
            },
            Route =
            {
                SplitRoute =
                [
                    new SplitRouteEntry
                    {
                        Id = "split:item-520",
                        DisplayName = "Summon Prep",
                        Enabled = true,
                        Condition = SplitCondition.All(
                        [
                            SplitCatalog.CreateItemEverOwnedCondition(43, 1),
                            SplitCatalog.CreateItemEverOwnedCondition(itemId, 9)
                        ]),
                        IconTargetIds = [otherItemTargetId, itemTargetId],
                        IconOverride = new SplitIconOverride
                        {
                            Source = SplitIconOverrideSource.Target,
                            TargetId = itemTargetId
                        }
                    }
                ]
            }
        };
        SettingsNormalizer.Normalize(settings);
        SplitDefinition definition = SplitCatalog.Build(settings).Single();
        TestAssert.Equal(1, definition.IconLightingConditions.Count);
        TestAssert.Equal(SplitConditionKind.All, definition.IconLightingConditions.Single().Kind);
        var status = new SplitStatusSnapshot(
            definition,
            null,
            IsSkipped: false,
            CompletedFactKeys: []);
        var context = new OverlayRenderContext(
            settings,
            UiPalette.From(settings.Overlay.Colors),
            TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts((currentItemFactKey, 9))),
            [status],
            CurrentSplitIndex: 0,
            SplitTimerPhase.Running,
            TimeSpan.FromSeconds(5),
            new SplitLayout(new Rectangle(0, 0, 120, 32), new Rectangle(0, 40, 120, 64), 6),
            VisibleStatusRowCount: 1,
            MouseClickThrough: false,
            SplitCompletionAnimation: null,
            SegmentBestDeltaHighlights: new Dictionary<int, SegmentBestDeltaHighlight>(),
            NowUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        TestAssert.Equal(false, InvokeIsIconLit(context, status, definition, iconIndex: 0));

        OverlayRenderContext otherItemOnlyContext = context with
        {
            Snapshot = TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts((otherItemFactKey, 1), (currentItemFactKey, 0)))
        };
        TestAssert.Equal(false, InvokeIsIconLit(otherItemOnlyContext, status, definition, iconIndex: 0));

        OverlayRenderContext completeContext = context with
        {
            Snapshot = TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts((otherItemFactKey, 1), (currentItemFactKey, 9)))
        };
        TestAssert.Equal(true, InvokeIsIconLit(completeContext, status, definition, iconIndex: 0));

        var completedStatus = status with
        {
            Time = TimeSpan.FromSeconds(5),
            FactCompletionTimes = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
            {
                [SplitCatalog.CreateItemEverOwnedFactKey(43)] = TimeSpan.FromSeconds(4),
                [everOwnedFactKey] = TimeSpan.FromSeconds(5)
            }
        };
        OverlayRenderContext emptyInventoryContext = context with
        {
            Snapshot = TestSnapshots.Terraria(
                isGameMenu: false,
                bossStates: CreateFacts((otherItemFactKey, 0), (currentItemFactKey, 0)))
        };
        TestAssert.Equal(true, InvokeIsIconLit(emptyInventoryContext, completedStatus, definition, iconIndex: 0));

        var partialRememberedStatus = status with
        {
            FactCompletionTimes = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
            {
                [everOwnedFactKey] = TimeSpan.FromSeconds(4)
            }
        };
        TestAssert.Equal(false, InvokeIsIconLit(emptyInventoryContext, partialRememberedStatus, definition, iconIndex: 0));
    }

    private static void SplitListRendererPartialRegionMatchesFullRender()
    {
        var settings = new AppSettings
        {
            Overlay =
            {
                ShowEarlyDeltaTime = true,
                EarlyDeltaTimeSeconds = 3600
            },
            Route =
            {
                SplitRoute =
                [
                    new SplitRouteEntry
                    {
                        Id = "split:skeletron",
                        DisplayName = "Skeletron",
                        Enabled = true,
                        Condition = SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                        IconTargetIds = [SplitCatalog.Skeletron]
                    }
                ]
            }
        };
        SettingsNormalizer.Normalize(settings);
        var definition = new SplitDefinition(
            "split:skeletron",
            "Skeletron",
            SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
            Array.Empty<string>(),
            Array.Empty<string>(),
            [SplitCatalog.Skeletron]);
        settings.GetActiveReferenceSet().Splits[SingleCumulativeKey(settings, definition.Id)] = "0:10.00";

        var statuses = new List<SplitStatusSnapshot>
        {
            new(definition, TimeSpan.FromSeconds(5), IsSkipped: false, CompletedFactKeys: []),
            new(definition, null, IsSkipped: false, CompletedFactKeys: [])
        };

        var size = new Size(260, 220);
        if (!SplitLayoutCalculator.TryCreate(
                new Rectangle(Point.Empty, size),
                statuses.Count,
                6,
                value => OverlayRenderContext.ScaleInt(settings, value),
                out SplitLayout layout))
        {
            throw new InvalidOperationException("Could not create split layout for partial render test.");
        }

        UiPalette palette = UiPalette.From(settings.Overlay.Colors);
        var nowUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        OverlayRenderContext CreateContext(TimeSpan elapsed) => new(
            settings,
            palette,
            TestSnapshots.Terraria(isGameMenu: false),
            statuses,
            CurrentSplitIndex: 1,
            SplitTimerPhase.Running,
            elapsed,
            layout,
            VisibleStatusRowCount: statuses.Count,
            MouseClickThrough: false,
            SplitCompletionAnimation: null,
            SegmentBestDeltaHighlights: new Dictionary<int, SegmentBestDeltaHighlight>(),
            NowUtc: nowUtc);

        using var resources = new OverlayRenderResources();
        OverlayRenderContext before = CreateContext(TimeSpan.FromSeconds(30));
        OverlayRenderContext after = CreateContext(TimeSpan.FromSeconds(31.5));

        using Bitmap expected = RenderStatusFrame(after, resources, size, clipBounds: null, baseFrame: null);
        using Bitmap actual = RenderStatusFrame(before, resources, size, clipBounds: null, baseFrame: null);

        int bleed = SplitListRenderer.GetRowBleedMargin(settings);
        Rectangle region = Rectangle.Inflate(layout.GetRowRect(1), bleed, bleed);
        region.Intersect(new Rectangle(Point.Empty, size));
        using Bitmap patched = RenderStatusFrame(after, resources, size, region, actual);

        TestAssert.Equal(true, BitmapsMatch(expected, patched));
    }

    private static void SplitCompletionAnimationFactoryFiltersOrCompletionIconsToMatchedTarget()
    {
        SplitDefinition definition = CreateAnyBossDefinition();
        SplitCondition wallOfFlesh = SplitCatalog.CreateBossFactCondition(SplitCatalog.WallOfFlesh);
        var settings = new AppSettings
        {
            Route =
            {
                SplitRoute =
                [
                    new SplitRouteEntry
                    {
                        Id = definition.Id,
                        DisplayName = definition.DisplayName,
                        Enabled = true,
                        Condition = definition.Condition,
                        IconTargetIds = definition.TargetIds.ToList()
                    }
                ]
            }
        };
        SettingsNormalizer.Normalize(settings);
        var statuses = new[]
        {
            new SplitStatusSnapshot(
                definition,
                TimeSpan.FromSeconds(10),
                IsSkipped: false,
                CompletedFactKeys: [wallOfFlesh.FactKey])
        };

        SplitCompletionAnimation? animation = SplitCompletionAnimationFactory.Create(
            settings,
            statuses,
            completedIndex: 0,
            startedAtUtc: DateTime.UtcNow);

        TestAssert.Equal(true, animation is not null);
        TestAssert.Equal(1, animation!.Definition.IconKeys.Count);
        TestAssert.Equal(SplitCatalog.WallOfFlesh, animation.Definition.IconKeys.Single());
    }

    private static void SplitCompletionAnimationFactoryCreatesAnimationWithSplitDelta()
    {
        var settings = new AppSettings
        {
            Route =
            {
                SplitRoute =
                [
                    new SplitRouteEntry
                    {
                        Id = "split:skeletron",
                        DisplayName = "Skeletron",
                        Enabled = true,
                        Condition = SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                        IconTargetIds = [SplitCatalog.Skeletron]
                    }
                ]
            },
            Comparison =
            {
                PersonalBestSegmentTimes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["split:skeletron"] = "13:30.00"
                }
            }
        };
        SettingsNormalizer.Normalize(settings);
        settings.Comparison.ReferenceSplitSets =
        [
            AppSettings.CreateReferenceSet("WR", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [SingleCumulativeKey(settings, "split:skeletron")] = "13:42.00"
            }, SplitConditionDataRows.Build(settings).Select(row => row.Key))
        ];
        SplitDefinition definition = SplitCatalog.Build(settings).First(item => item.Id == "split:skeletron");
        var statuses = new[]
        {
            new SplitStatusSnapshot(definition, TimeSpan.FromMinutes(13), IsSkipped: false, CompletedFactKeys: [])
        };

        SplitCompletionAnimation? animation = SplitCompletionAnimationFactory.Create(
            settings,
            statuses,
            completedIndex: 0,
            startedAtUtc: DateTime.UtcNow);

        TestAssert.Equal(true, animation is not null);
        TestAssert.Equal(true, animation!.ReferenceSplitComparison.ShowDelta);
        TestAssert.Equal(TimeSpan.FromSeconds(-42), animation.ReferenceSplitComparison.Delta);
        TestAssert.Equal(true, animation.PersonalBestSegmentComparison.ShowDelta);
        TestAssert.Equal(TimeSpan.FromSeconds(-30), animation.PersonalBestSegmentComparison.Delta);
    }

    private static Bitmap RenderStatusFrame(
        OverlayRenderContext context,
        OverlayRenderResources resources,
        Size size,
        Rectangle? clipBounds,
        Bitmap? baseFrame)
    {
        var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        ConfigureOverlayGraphics(graphics);
        if (baseFrame is null)
        {
            graphics.Clear(Color.Transparent);
            OverlayRenderer.RenderStatus(graphics, context, resources, clipBounds);
            return bitmap;
        }

        // Replicates LayeredWindowRenderTarget.RenderRegion: keep the previous
        // frame, clear only the dirty region, and redraw clipped to it.
        CompositingMode previousMode = graphics.CompositingMode;
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.DrawImage(baseFrame, Point.Empty);
        graphics.CompositingMode = previousMode;
        if (clipBounds is Rectangle region)
        {
            graphics.SetClip(region);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            using (var clearBrush = new SolidBrush(Color.Transparent))
            {
                graphics.FillRectangle(clearBrush, region);
            }

            graphics.CompositingMode = CompositingMode.SourceOver;
        }

        OverlayRenderer.RenderStatus(graphics, context, resources, clipBounds);
        graphics.ResetClip();
        return bitmap;
    }

    private static void ConfigureOverlayGraphics(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.CompositingMode = CompositingMode.SourceOver;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
    }

    private static bool BitmapsMatch(Bitmap expected, Bitmap actual)
    {
        if (expected.Size != actual.Size)
        {
            return false;
        }

        var bounds = new Rectangle(Point.Empty, expected.Size);
        BitmapData expectedData = expected.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            BitmapData actualData = actual.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                int rowBytes = expected.Width * 4;
                var expectedRow = new byte[rowBytes];
                var actualRow = new byte[rowBytes];
                for (int y = 0; y < expected.Height; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(expectedData.Scan0 + y * expectedData.Stride, expectedRow, 0, rowBytes);
                    System.Runtime.InteropServices.Marshal.Copy(actualData.Scan0 + y * actualData.Stride, actualRow, 0, rowBytes);
                    if (!expectedRow.AsSpan().SequenceEqual(actualRow))
                    {
                        return false;
                    }
                }

                return true;
            }
            finally
            {
                actual.UnlockBits(actualData);
            }
        }
        finally
        {
            expected.UnlockBits(expectedData);
        }
    }

    private static void SplitCompletionAnimationRendererPreservesFadeCurve()
    {
        TimeSpan duration = TimeSpan.FromSeconds(4);
        TimeSpan halfFade = TimeSpan.FromMilliseconds(225);

        Nearly(0f, SplitCompletionAnimationRenderer.GetAnimationOpacity(TimeSpan.Zero, duration));
        Nearly(0.5f, SplitCompletionAnimationRenderer.GetAnimationOpacity(halfFade, duration));
        Nearly(1f, SplitCompletionAnimationRenderer.GetAnimationOpacity(TimeSpan.FromSeconds(1), duration));
        Nearly(0.5f, SplitCompletionAnimationRenderer.GetAnimationOpacity(duration - halfFade, duration));
        Nearly(0f, SplitCompletionAnimationRenderer.GetAnimationOpacity(duration, duration));
    }

    private static void SplitCompletionAnimationRendererPreservesDeltaSlideCurve()
    {
        TimeSpan duration = TimeSpan.FromSeconds(4);
        float slide = SplitCompletionAnimationRenderer.GetDeltaSlideDistance(20f);

        SplitCompletionDeltaMotion beforeIntro = SplitCompletionAnimationRenderer.GetDeltaMotion(
            TimeSpan.FromMilliseconds(500),
            duration,
            slide);
        Nearly(slide, beforeIntro.OffsetX);
        Nearly(0f, beforeIntro.Opacity);

        SplitCompletionDeltaMotion visible = SplitCompletionAnimationRenderer.GetDeltaMotion(
            TimeSpan.FromSeconds(1.5),
            duration,
            slide);
        Nearly(0f, visible.OffsetX);
        Nearly(1f, visible.Opacity);

        SplitCompletionDeltaMotion ended = SplitCompletionAnimationRenderer.GetDeltaMotion(duration, duration, slide);
        Nearly(slide, ended.OffsetX);
        Nearly(0f, ended.Opacity);
    }

    private static void SplitCompletionAnimationRendererCentersOnRenderedRows()
    {
        AppSettings settings = new();
        var statuses = new List<SplitStatusSnapshot>();
        for (int i = 0; i < 4; i++)
        {
            statuses.Add(new SplitStatusSnapshot(
                CreateDisplayRowDefinition($"split:attached-{i}", isAttached: true),
                null,
                IsSkipped: false,
                CompletedFactKeys: []));
        }

        statuses.Add(new SplitStatusSnapshot(
            CreateDisplayRowDefinition("split:completed-anchor", isAttached: false),
            TimeSpan.FromSeconds(1),
            IsSkipped: false,
            CompletedFactKeys: []));
        for (int i = 0; i < 9; i++)
        {
            statuses.Add(new SplitStatusSnapshot(
                CreateDisplayRowDefinition($"split:visible-{i}", isAttached: false),
                null,
                IsSkipped: false,
                CompletedFactKeys: []));
        }

        int visibleRowCount = SplitDisplayRows.GetRequiredRowCount(settings, statuses);
        TestAssert.Equal(14, visibleRowCount);
        IReadOnlyList<SplitDisplayRow> rows = SplitDisplayRows.Build(settings, statuses);
        TestAssert.Equal(4, rows.Min(row => row.RowIndex));
        TestAssert.Equal(13, rows.Max(row => row.RowIndex));
        TestAssert.Equal(true, SplitLayoutCalculator.TryCreate(
            new Rectangle(0, 0, 640, 900),
            visibleRowCount,
            baseRowGap: 9,
            value => OverlayRenderContext.ScaleInt(settings, value),
            out SplitLayout layout));

        var context = new OverlayRenderContext(
            settings,
            UiPalette.From(settings.Overlay.Colors),
            TestSnapshots.Terraria(isGameMenu: false),
            statuses,
            CurrentSplitIndex: 4,
            SplitTimerPhase.Running,
            TimeSpan.FromSeconds(3),
            layout,
            visibleRowCount,
            MouseClickThrough: false,
            SplitCompletionAnimation: null,
            SegmentBestDeltaHighlights: new Dictionary<int, SegmentBestDeltaHighlight>(),
            NowUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Rectangle bounds = SplitCompletionAnimationRenderer.GetAnimationBounds(context);
        Rectangle firstRenderedRow = layout.GetRowRect(4);
        Rectangle lastRenderedRow = layout.GetRowRect(13);
        int expectedCenterY = firstRenderedRow.Top + (lastRenderedRow.Bottom - firstRenderedRow.Top) / 2;

        TestAssert.Equal(expectedCenterY, bounds.Top + bounds.Height / 2);
        TestAssert.Equal(true, bounds.Top > layout.GetRowRect(0).Top);
    }

    private static void OverlayTextStylesMapsTextEffectPercentages()
    {
        var settings = new AppSettings
        {
            Overlay =
            {
                TextEffects = new UiTextEffectSettings
                {
                    IconOpacityPercent = 15,
                    TimeOpacityPercent = 16,
                    TimeShadowPercent = 11,
                    TimeOutlineThicknessPercent = 12,
                    DeltaOpacityPercent = 26,
                    DeltaShadowPercent = 21,
                    DeltaOutlineThicknessPercent = 22,
                    TimerOpacityPercent = 36,
                    TimerShadowPercent = 31,
                    TimerOutlineThicknessPercent = 32,
                    TimerMillisecondsOpacityPercent = 46,
                    TimerMillisecondsShadowPercent = 41,
                    TimerMillisecondsOutlineThicknessPercent = 42
                }
            }
        };
        UiPalette palette = UiPalette.From(settings.Overlay.Colors);

        TextRenderStyle split = OverlayTextStyles.GetSplitTextStyle(settings, palette);
        TextRenderStyle delta = OverlayTextStyles.GetDeltaTextStyle(settings, new SplitComparison(TimeSpan.FromSeconds(-1), true), palette);
        TextRenderStyle timer = OverlayTextStyles.GetTimerTextStyle(
            settings,
            Array.Empty<SplitStatusSnapshot>(),
            currentSplitIndex: 0,
            SplitTimerPhase.NotStarted,
            TimeSpan.Zero,
            palette,
            milliseconds: false);
        TextRenderStyle milliseconds = OverlayTextStyles.GetTimerTextStyle(
            settings,
            Array.Empty<SplitStatusSnapshot>(),
            currentSplitIndex: 0,
            SplitTimerPhase.NotStarted,
            TimeSpan.Zero,
            palette,
            milliseconds: true);

        Nearly(0.15f, OverlayTextStyles.GetIconOpacity(settings));
        Nearly(0.16f, OverlayTextStyles.GetTimeTextOpacity(settings));
        Nearly(0.26f, OverlayTextStyles.GetDeltaTextOpacity(settings));
        Nearly(0.36f, OverlayTextStyles.GetTimerTextOpacity(settings, milliseconds: false));
        Nearly(0.46f, OverlayTextStyles.GetTimerTextOpacity(settings, milliseconds: true));
        TestAssert.Equal(11, split.ShadowPercent);
        TestAssert.Equal(12, split.OutlineThicknessPercent);
        TestAssert.Equal(21, delta.ShadowPercent);
        TestAssert.Equal(22, delta.OutlineThicknessPercent);
        TestAssert.Equal(31, timer.ShadowPercent);
        TestAssert.Equal(32, timer.OutlineThicknessPercent);
        TestAssert.Equal(41, milliseconds.ShadowPercent);
        TestAssert.Equal(42, milliseconds.OutlineThicknessPercent);
    }

    private static void OverlayTextStylesCanIgnoreAttachedGroupsForTimerComparison()
    {
        var settings = new AppSettings
        {
            Comparison =
            {
                ActiveReferenceSplitSet = "WR",
                ReferenceSplitSets =
                [
                    new ReferenceSplitSet
                    {
                        Name = "WR",
                        Splits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    }
                ]
            },
            Overlay =
            {
                EnableTimerGradientColor = false,
                ShowEarlyDeltaTime = true,
                EarlyDeltaTimeSeconds = 3600,
                Colors = new UiColorSettings
                {
                    TimerAheadText = "#112233",
                    TimerAheadTextOutline = "#000000",
                    TimerAheadTextShadow = "#000000",
                    TimerBehindText = "#445566",
                    TimerBehindTextOutline = "#000000",
                    TimerBehindTextShadow = "#000000"
                }
            },
            Route =
            {
                SplitRoute =
                [
                    new SplitRouteEntry
                    {
                        Id = "split:attached",
                        DisplayName = "Attached",
                        Enabled = true,
                        Condition = SplitCondition.Fact("fact:attached"),
                        IsAttached = true
                    },
                    new SplitRouteEntry
                    {
                        Id = "split:main",
                        DisplayName = "Main",
                        Enabled = true,
                        Condition = SplitCondition.Fact("fact:main")
                    }
                ]
            }
        };
        SettingsNormalizer.Normalize(settings);
        settings.GetActiveReferenceSet().Splits[SingleCumulativeKey(settings, "split:attached")] = "0:10.00";
        settings.GetActiveReferenceSet().Splits[SingleCumulativeKey(settings, "split:main")] = "0:30.00";
        IReadOnlyList<SplitStatusSnapshot> statuses = SplitCatalog.Build(settings)
            .Select(SplitStatusSnapshot.FromDefinition)
            .ToArray();
        UiPalette palette = UiPalette.From(settings.Overlay.Colors);

        settings.Route.AttachedGroupsAffectTimerComparison = true;
        TextRenderStyle attachedComparison = OverlayTextStyles.GetTimerTextStyle(
            settings,
            statuses,
            currentSplitIndex: 0,
            SplitTimerPhase.Running,
            TimeSpan.FromSeconds(20),
            palette,
            milliseconds: false);

        settings.Route.AttachedGroupsAffectTimerComparison = false;
        TextRenderStyle mainComparison = OverlayTextStyles.GetTimerTextStyle(
            settings,
            statuses,
            currentSplitIndex: 0,
            SplitTimerPhase.Running,
            TimeSpan.FromSeconds(20),
            palette,
            milliseconds: false);

        TestAssert.Equal(Color.FromArgb(0x44, 0x55, 0x66).ToArgb(), attachedComparison.Fill.ToArgb());
        TestAssert.Equal(Color.FromArgb(0x11, 0x22, 0x33).ToArgb(), mainComparison.Fill.ToArgb());
    }

    private static SplitDefinition CreateAnyBossDefinition()
    {
        return new SplitDefinition(
            "split:any-boss",
            "Any Boss",
            SplitCondition.Any(
            [
                SplitCatalog.CreateBossFactCondition(SplitCatalog.Skeletron),
                SplitCatalog.CreateBossFactCondition(SplitCatalog.WallOfFlesh)
            ]),
            ["skeletron.png", "wof.png"],
            [SplitCatalog.Skeletron, SplitCatalog.WallOfFlesh],
            [SplitCatalog.Skeletron, SplitCatalog.WallOfFlesh]);
    }

    private static SplitDefinition CreateDisplayRowDefinition(string id, bool isAttached)
    {
        return new SplitDefinition(
            id,
            id,
            SplitCondition.Fact($"{id}:fact"),
            [],
            [],
            [],
            IsAttached: isAttached);
    }

    private static AppSettings CreateExpandedRowsSettings(SplitDefinition definition, SplitCondition condition)
    {
        return new AppSettings
        {
            Comparison =
            {
                ActiveReferenceSplitSet = "WR",
                ReferenceSplitSets =
                [
                    new ReferenceSplitSet
                    {
                        Name = "WR",
                        Splits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    }
                ]
            },
            Route =
            {
                ExpandSplitDetails = true,
                CollapseSplitDetailsOnCompletion = true,
                SplitRoute =
                [
                    new SplitRouteEntry
                    {
                        Id = definition.Id,
                        DisplayName = definition.DisplayName,
                        Condition = condition.Clone(),
                        IconTargetIds = definition.TargetIds.ToList()
                    }
                ]
            }
        };
    }

    private static SplitDefinition CreateSkippedTimeDefinition(string id)
    {
        return new SplitDefinition(
            id,
            id,
            SplitCondition.Fact($"{id}:fact"),
            [],
            [],
            []);
    }

    private static bool InvokeIsIconLit(
        OverlayRenderContext context,
        SplitStatusSnapshot status,
        SplitDefinition definition,
        int iconIndex)
    {
        MethodInfo method = typeof(SplitListRenderer).GetMethod(
                "IsIconLit",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing SplitListRenderer.IsIconLit.");
        return (bool)(method.Invoke(null, [context, status, definition, iconIndex])
            ?? throw new InvalidOperationException("IsIconLit returned null."));
    }

    private static IReadOnlyList<int> InvokeIconDrawOrder(
        OverlayRenderContext context,
        SplitStatusSnapshot status,
        SplitDefinition definition)
    {
        MethodInfo method = typeof(SplitListRenderer).GetMethod(
                "GetIconDrawOrder",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing SplitListRenderer.GetIconDrawOrder.");
        object result = method.Invoke(null, [context, status, definition])
            ?? throw new InvalidOperationException("GetIconDrawOrder returned null.");
        return ((IEnumerable<int>)result).ToArray();
    }

    private static TerrariaGameFacts CreateFacts(params (string Key, bool Value)[] values)
    {
        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        foreach ((string key, bool value) in values)
        {
            builder.SetBoolean(key, value);
        }

        return builder.Build();
    }

    private static TerrariaGameFacts CreateFacts(params (string Key, int Value)[] values)
    {
        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        foreach ((string key, int value) in values)
        {
            builder.SetInteger(key, value);
        }

        return builder.Build();
    }

    private static string SingleCumulativeKey(AppSettings settings, string splitId)
    {
        return SplitConditionDataRows.Build(settings)
            .Single(row => string.Equals(row.SplitId, splitId, StringComparison.OrdinalIgnoreCase))
            .Key;
    }

    private static void Nearly(float expected, float actual)
    {
        if (Math.Abs(expected - actual) > 0.001f)
        {
            throw new InvalidOperationException($"Expected approximately '{expected}', got '{actual}'.");
        }
    }

    private static bool HasVisiblePixel(Bitmap bitmap)
    {
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
