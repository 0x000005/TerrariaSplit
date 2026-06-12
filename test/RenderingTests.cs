using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using TerrariaSplit;

namespace TerrariaSplit.Tests;

internal static class RenderingTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("UiPalette maps configured text colors", UiPaletteMapsConfiguredTextColors);
        yield return ("TextEffectRenderer applies opacity without changing RGB", TextEffectRendererAppliesOpacity);
        yield return ("TextEffectRenderer draws direct styled string", TextEffectRendererDrawsDirectStyledString);
        yield return ("TextEffectRenderer draws direct shadow-only text", TextEffectRendererDrawsDirectShadowOnlyText);
        yield return ("OverlayFontCache keeps main timer font independent from milliseconds visibility", OverlayFontCacheKeepsMainTimerFontIndependentFromMillisecondsVisibility);
        yield return ("SplitSoundSelector routes equal times to not-faster sounds", SplitSoundSelectorRoutesEqualTimesToNotFasterSounds);
        yield return ("SplitSoundSelector treats missing comparison data as faster", SplitSoundSelectorTreatsMissingComparisonDataAsFaster);
        yield return ("SplitListRenderer preserves current split depth curve", SplitListRendererPreservesCurrentSplitDepthCurve);
        yield return ("SplitListRenderer partial region redraw matches full render", SplitListRendererPartialRegionMatchesFullRender);
        yield return ("SplitCompletionAnimationRenderer preserves fade curve", SplitCompletionAnimationRendererPreservesFadeCurve);
        yield return ("SplitCompletionAnimationRenderer preserves delta slide curve", SplitCompletionAnimationRendererPreservesDeltaSlideCurve);
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

    private static void TextEffectRendererAppliesOpacity()
    {
        Color color = TextEffectRenderer.WithOpacity(Color.FromArgb(200, 10, 20, 30), 0.5f);

        TestAssert.Equal(100, color.A);
        TestAssert.Equal(10, color.R);
        TestAssert.Equal(20, color.G);
        TestAssert.Equal(30, color.B);
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
        settings.Columns.ScalePercent = 150;
        settings.Columns.Timer.FontSize = 38;
        settings.Columns.Timer.Show = true;
        settings.Columns.TimerMilliseconds.FontSize = 12;
        settings.Columns.TimerMilliseconds.Show = true;

        float withMilliseconds = OverlayFontCache.GetColumnFontSize(
            settings.Columns.Timer,
            OverlayRenderContext.GetScaleFactor(settings));

        settings.Columns.TimerMilliseconds.Show = false;
        float withoutMilliseconds = OverlayFontCache.GetColumnFontSize(
            settings.Columns.Timer,
            OverlayRenderContext.GetScaleFactor(settings));

        TestAssert.Equal(withMilliseconds, withoutMilliseconds);
        Nearly(57f, withMilliseconds);
    }

    private static void SplitSoundSelectorRoutesEqualTimesToNotFasterSounds()
    {
        var sounds = new UiSoundSettings
        {
            SplitBehindReferenceBehindSegment = "normal-notfaster-notfaster.wav",
            SplitAheadReferenceAheadSegment = "normal-faster-faster.wav",
            MoonLordBehindReferenceBehindSegment = "moonlord-notfaster-notfaster.wav"
        };
        var normalDefinition = new BossSplitDefinition(
            BossSplitDefinitions.Skeletron,
            "Skeletron",
            Array.Empty<BossFlag>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            [BossSplitDefinitions.Skeletron]);
        var moonLordDefinition = new BossSplitDefinition(
            BossSplitDefinitions.MoonLord,
            "Moon Lord",
            Array.Empty<BossFlag>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            [BossSplitDefinitions.MoonLord]);

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
        var definition = new BossSplitDefinition(
            BossSplitDefinitions.Skeletron,
            "Skeletron",
            Array.Empty<BossFlag>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            [BossSplitDefinitions.Skeletron]);

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

    private static void SplitListRendererPreservesCurrentSplitDepthCurve()
    {
        var settings = new AppSettings
        {
            CurrentSplitHighlightScalePercent = 130,
            CurrentSplitDepthStrengthPercent = 50
        };

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

    private static void SplitListRendererPartialRegionMatchesFullRender()
    {
        var settings = new AppSettings
        {
            ShowEarlyDeltaTime = true,
            EarlyDeltaTimeSeconds = 3600
        };
        var definition = new BossSplitDefinition(
            BossSplitDefinitions.Skeletron,
            "Skeletron",
            [BossFlag.Skeletron],
            Array.Empty<string>(),
            Array.Empty<string>(),
            [BossSplitDefinitions.Skeletron]);
        settings.GetActiveReferenceSet().Splits[BossSplitDefinitions.Skeletron] = "0:10.00";

        var statuses = new List<SplitStatusSnapshot>
        {
            new(definition, TimeSpan.FromSeconds(5), IsSkipped: false),
            new(definition, null, IsSkipped: false)
        };

        var size = new Size(260, 160);
        if (!SplitLayoutCalculator.TryCreate(
                new Rectangle(Point.Empty, size),
                statuses.Count,
                6,
                value => OverlayRenderContext.ScaleInt(settings, value),
                out SplitLayout layout))
        {
            throw new InvalidOperationException("Could not create split layout for partial render test.");
        }

        UiPalette palette = UiPalette.From(settings.Colors);
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
            MouseClickThrough: false,
            SplitCompletionAnimation: null,
            new Dictionary<int, SegmentBestDeltaHighlight>(),
            nowUtc);

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

    private static void OverlayTextStylesMapsTextEffectPercentages()
    {
        var settings = new AppSettings
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
        };
        UiPalette palette = UiPalette.From(settings.Colors);

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
