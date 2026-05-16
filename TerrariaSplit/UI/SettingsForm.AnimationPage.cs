using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed partial class SettingsForm : Form
{

    internal void AddAnimationSection(TableLayoutPanel parent)
    {
        ConfigureCheckBox(enableDefeatedBossIconLightingBox, settings.EnableDefeatedBossIconLighting);
        ConfigureNumberBox(undefeatedIconGrayscaleBox, settings.UndefeatedIconGrayscalePercent, 0, 100);
        ConfigureNumberBox(undefeatedIconBrightnessBox, settings.UndefeatedIconBrightnessPercent, 0, 100);
        ConfigureNumberBox(currentBossIconGrayscaleWeakenBox, settings.CurrentBossIconGrayscaleWeakenPercent, 0, 100);
        ConfigureNumberBox(currentBossIconBrightnessBoostBox, settings.CurrentBossIconBrightnessBoostPercent, 0, 100);
        ConfigureCheckBox(showCurrentSplitHighlightBox, settings.ShowCurrentSplitHighlight);
        ConfigureNumberBox(currentSplitHighlightScaleBox, settings.CurrentSplitHighlightScalePercent, 100, 140);
        ConfigureNumberBox(currentSplitDepthStrengthBox, settings.CurrentSplitDepthStrengthPercent, 0, 100);
        ConfigureCheckBox(showEarlyDeltaTimeBox, settings.ShowEarlyDeltaTime);
        ConfigureNumberBox(earlyDeltaTimeSecondsBox, settings.EarlyDeltaTimeSeconds, 0, 3600);
        ConfigureCheckBox(enableDeltaGradientColorBox, settings.EnableDeltaGradientColor);
        ConfigureCheckBox(enableTimerGradientColorBox, settings.EnableTimerGradientColor);
        ConfigureTimeBox(deltaGradientThresholdBox, settings.DeltaGradientThresholdSeconds, 1, 3600);
        ConfigureDeltaGradientCurveBox();
        enableDeltaGradientColorBox.CheckedChanged += (_, _) => InvalidateDeltaGradientPreview();
        enableTimerGradientColorBox.CheckedChanged += (_, _) => InvalidateDeltaGradientPreview();
        deltaGradientThresholdBox.TextChanged += (_, _) => InvalidateDeltaGradientPreview();
        ConfigureCheckBox(showSplitCompletionAnimationBox, settings.ShowSplitCompletionAnimation);
        ConfigureDecimalBox(splitCompletionAnimationDurationBox, settings.SplitCompletionAnimationDurationSeconds, 2m, 20m);
        ConfigureNumberBox(splitCompletionOutlineThicknessBox, settings.SplitCompletionOutlineThicknessPercent, 0, 100);
        splitCompletionOutlineThicknessBox.TextChanged += (_, _) => outlineStylePreview.Invalidate();

        TableLayoutPanel iconSection = CreateSection("Light icons when BOSS defeated");
        TableLayoutPanel iconGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(iconGrid, "Enabled", enableDefeatedBossIconLightingBox);
        AddSettingRow(iconGrid, "Unlit grayscale %", undefeatedIconGrayscaleBox);
        AddSettingRow(iconGrid, "Unlit brightness %", undefeatedIconBrightnessBox);
        AddSettingRow(iconGrid, "Current boss grayscale weaken %", currentBossIconGrayscaleWeakenBox);
        AddSettingRow(iconGrid, "Current boss brightness boost %", currentBossIconBrightnessBoostBox);
        AddSectionControl(iconSection, iconGrid);
        AddSection(parent, iconSection);

        TableLayoutPanel currentSection = CreateSection("Highlight current stage");
        TableLayoutPanel currentGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(currentGrid, "Enabled", showCurrentSplitHighlightBox);
        AddSettingRow(currentGrid, "Scale %", currentSplitHighlightScaleBox);
        AddSettingRow(currentGrid, "Depth strength %", currentSplitDepthStrengthBox);
        AddSectionControl(currentSection, currentGrid);
        AddSection(parent, currentSection);

        TableLayoutPanel earlyDeltaSection = CreateSection("Early delta time");
        TableLayoutPanel earlyDeltaGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(earlyDeltaGrid, "Enabled", showEarlyDeltaTimeBox);
        AddSettingRow(earlyDeltaGrid, "Show when within seconds", earlyDeltaTimeSecondsBox);
        AddSectionControl(earlyDeltaSection, earlyDeltaGrid);
        AddSection(parent, earlyDeltaSection);

        TableLayoutPanel deltaGradientSection = CreateSection("Delta time gradient");
        TableLayoutPanel deltaGradientGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(deltaGradientGrid, "Enabled (Delta)", enableDeltaGradientColorBox);
        AddSettingRow(deltaGradientGrid, "Enabled (Main timer)", enableTimerGradientColorBox);
        AddSettingRow(deltaGradientGrid, "Threshold time", deltaGradientThresholdBox);
        AddSettingRow(deltaGradientGrid, "Gradient mode", deltaGradientCurveBox);
        AddSectionControl(deltaGradientSection, deltaGradientGrid);
        AddSectionControl(deltaGradientSection, CreateDeltaGradientPreview());
        AddSection(parent, deltaGradientSection);

        TableLayoutPanel section = CreateSection("BOSS defeat animation");
        TableLayoutPanel optionGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(optionGrid, "Enabled", showSplitCompletionAnimationBox);
        AddSettingRow(optionGrid, "Animation duration seconds", splitCompletionAnimationDurationBox);
        AddSectionControl(section, optionGrid);

        AddSectionControl(section, CreateSubsectionLabel("Show comparison with reference time"));
        animationComparisonGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(180f),
            ColumnStyleAbsolute(180f));
        AddSectionControl(section, animationComparisonGrid);

        AddSectionControl(section, CreateSubsectionLabel("Outline when faster than reference"));
        TableLayoutPanel outlineOptionGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(outlineOptionGrid, "Outline thickness %", splitCompletionOutlineThicknessBox);
        AddSectionControl(section, outlineOptionGrid);

        animationOutlineGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(180f),
            ColumnStyleAbsolute(180f));
        AddSectionControl(section, animationOutlineGrid);
        AddSectionControl(section, CreateOutlineStylePreview());
        PopulateAnimationOutlineGrid();
        AddSection(parent, section);

        AddSegmentBestDeltaHighlightSection(parent);
    }


    private void AddSegmentBestDeltaHighlightSection(TableLayoutPanel parent)
    {
        ConfigureCheckBox(showSegmentBestDeltaHighlightBox, settings.ShowSegmentBestDeltaHighlight);
        TableLayoutPanel section = CreateSection("Highlight best segment");
        TableLayoutPanel optionGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(optionGrid, "Enabled", showSegmentBestDeltaHighlightBox);
        AddSectionControl(section, optionGrid);

        segmentBestDeltaHighlightGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(220f));
        PopulateSegmentBestDeltaHighlightGrid();
        AddSectionControl(section, segmentBestDeltaHighlightGrid);
        AddSectionControl(section, CreateSegmentBestDeltaHighlightPreview());
        AddSection(parent, section);
    }


    private void PopulateAnimationOutlineGrid()
    {
        if (animationComparisonGrid is null || animationOutlineGrid is null)
        {
            return;
        }

        List<RouteGroup> groups = BossRouteGroups.Build(settings).ToList();
        string signature = string.Join('\u001F', groups.Select(group => group.Key));
        if (animationGridSignature == signature && animationOutlineControls.Count > 0)
        {
            foreach (RouteGroup group in groups)
            {
                if (!animationOutlineControls.TryGetValue(group.Key, out AnimationOutlineControls? controls))
                {
                    continue;
                }

                controls.SplitComparison.Checked = GetAnimationOutlineSetting(settings.SplitCompletionSplitComparisons, group.Key);
                controls.SegmentComparison.Checked = GetAnimationOutlineSetting(settings.SplitCompletionSegmentComparisons, group.Key);
                SetOutlineStyle(controls.SplitTime, GetAnimationOutlineStyle(settings.SplitCompletionOutlineSplitStyles, group.Key));
                SetOutlineStyle(controls.SegmentTime, GetAnimationOutlineStyle(settings.SplitCompletionOutlineSegmentStyles, group.Key));
            }

            return;
        }

        animationComparisonGrid.SuspendLayout();
        animationOutlineGrid.SuspendLayout();
        try
        {
            ClearGrid(animationComparisonGrid);
            ClearGrid(animationOutlineGrid);
            animationOutlineControls.Clear();
            AddHeaderRow(animationComparisonGrid, "BOSS Group", "Cumulative time", "Segment time");
            AddHeaderRow(animationOutlineGrid, "BOSS Group", "Cumulative time", "Segment time");
            foreach (RouteGroup group in groups)
            {
                var splitComparisonBox = new CheckBox
                {
                    Checked = GetAnimationOutlineSetting(settings.SplitCompletionSplitComparisons, group.Key),
                    Dock = DockStyle.Fill,
                    ForeColor = TextColor,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                UiTheme.StyleCheckBox(splitComparisonBox);

                var segmentComparisonBox = new CheckBox
                {
                    Checked = GetAnimationOutlineSetting(settings.SplitCompletionSegmentComparisons, group.Key),
                    Dock = DockStyle.Fill,
                    ForeColor = TextColor,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                UiTheme.StyleCheckBox(segmentComparisonBox);

                ComboBox splitTimeBox = CreateOutlineStyleBox(GetAnimationOutlineStyle(settings.SplitCompletionOutlineSplitStyles, group.Key));
                ComboBox segmentTimeBox = CreateOutlineStyleBox(GetAnimationOutlineStyle(settings.SplitCompletionOutlineSegmentStyles, group.Key));

                animationOutlineControls[group.Key] = new AnimationOutlineControls(splitComparisonBox, segmentComparisonBox, splitTimeBox, segmentTimeBox);

                int comparisonRow = AddGridRow(animationComparisonGrid);
                animationComparisonGrid.Controls.Add(CreateRowLabel(BossRouteGroups.GetGroupDisplayName(group, settings)), 0, comparisonRow);
                animationComparisonGrid.Controls.Add(splitComparisonBox, 1, comparisonRow);
                animationComparisonGrid.Controls.Add(segmentComparisonBox, 2, comparisonRow);

                int outlineRow = AddGridRow(animationOutlineGrid);
                animationOutlineGrid.Controls.Add(CreateRowLabel(BossRouteGroups.GetGroupDisplayName(group, settings)), 0, outlineRow);
                animationOutlineGrid.Controls.Add(splitTimeBox, 1, outlineRow);
                animationOutlineGrid.Controls.Add(segmentTimeBox, 2, outlineRow);
            }

            animationGridSignature = signature;
        }
        finally
        {
            animationOutlineGrid.ResumeLayout(true);
            animationComparisonGrid.ResumeLayout(true);
        }
    }


    private static bool GetAnimationOutlineSetting(Dictionary<string, bool> values, string key)
    {
        return !values.TryGetValue(key, out bool enabled) || enabled;
    }


    private static string GetAnimationOutlineStyle(Dictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out string? style)
            ? SplitCompletionOutlineStyles.Normalize(style)
            : SplitCompletionOutlineStyles.Rainbow;
    }


    private ComboBox CreateOutlineStyleBox(string selectedStyle)
    {
        var comboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        UiTheme.StyleComboBox(comboBox);

        foreach (string style in SplitCompletionOutlineStyles.Ids)
        {
            comboBox.Items.Add(new OutlineStyleOption(style, Localizer.Get(SplitCompletionOutlineStyles.GetDisplayName(style), settings)));
        }

        SetOutlineStyle(comboBox, selectedStyle);
        comboBox.SelectedIndexChanged += (_, _) =>
        {
            previewOutlineStyle = GetSelectedOutlineStyle(comboBox);
            outlineStylePreview.Invalidate();
        };
        return comboBox;
    }


    private static string GetSelectedOutlineStyle(ComboBox comboBox)
    {
        return comboBox.SelectedItem is OutlineStyleOption option
            ? option.Id
            : SplitCompletionOutlineStyles.None;
    }


    private static void SetOutlineStyle(ComboBox comboBox, string style)
    {
        string normalized = SplitCompletionOutlineStyles.Normalize(style);
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is OutlineStyleOption option &&
                string.Equals(option.Id, normalized, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }


    private Control CreateOutlineStylePreview()
    {
        outlineStylePreview.Dock = DockStyle.Fill;
        outlineStylePreview.Height = 96;
        outlineStylePreview.BackColor = FieldColor;
        outlineStylePreview.Margin = new Padding(0, 10, 0, 2);
        outlineStylePreview.Paint += (_, e) => PaintOutlineStylePreview(e.Graphics, outlineStylePreview.ClientRectangle);
        UiTheme.EnableDoubleBuffering(outlineStylePreview);
        outlineStylePreviewTimer.Interval = 120;
        outlineStylePreviewTimer.Tick += (_, _) => outlineStylePreview.Invalidate();
        outlineStylePreviewTimer.Start();
        outlineStylePreview.Disposed += (_, _) => outlineStylePreviewTimer.Stop();
        return outlineStylePreview;
    }


    private Control CreateSegmentBestDeltaHighlightPreview()
    {
        segmentBestDeltaHighlightPreview.Dock = DockStyle.Fill;
        segmentBestDeltaHighlightPreview.Height = 96;
        segmentBestDeltaHighlightPreview.BackColor = FieldColor;
        segmentBestDeltaHighlightPreview.Margin = new Padding(0, 10, 0, 2);
        segmentBestDeltaHighlightPreview.Paint += (_, e) => PaintSegmentBestDeltaHighlightPreview(e.Graphics, segmentBestDeltaHighlightPreview.ClientRectangle);
        UiTheme.EnableDoubleBuffering(segmentBestDeltaHighlightPreview);
        outlineStylePreviewTimer.Tick += (_, _) => segmentBestDeltaHighlightPreview.Invalidate();
        return segmentBestDeltaHighlightPreview;
    }


    private Control CreateDeltaGradientPreview()
    {
        deltaGradientPreview.Dock = DockStyle.Fill;
        deltaGradientPreview.Height = 88;
        deltaGradientPreview.BackColor = FieldColor;
        deltaGradientPreview.Margin = new Padding(0, 10, 0, 2);
        deltaGradientPreview.Paint += (_, e) => PaintDeltaGradientPreview(e.Graphics, deltaGradientPreview.ClientRectangle);
        UiTheme.EnableDoubleBuffering(deltaGradientPreview);
        return deltaGradientPreview;
    }


    private void PaintDeltaGradientPreview(Graphics graphics, Rectangle bounds)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var backgroundBrush = new SolidBrush(FieldColor);
        graphics.FillRectangle(backgroundBrush, bounds);
        using var borderPen = new Pen(BorderColor);
        graphics.DrawRectangle(borderPen, 0, 0, Math.Max(0, bounds.Width - 1), Math.Max(0, bounds.Height - 1));

        Rectangle fillBounds = Rectangle.Inflate(bounds, -1, -1);
        if (fillBounds.Width <= 0 || fillBounds.Height <= 0)
        {
            return;
        }

        int gap = 1;
        int rowHeight = Math.Max(1, (fillBounds.Height - gap) / 2);
        var deltaRow = new Rectangle(fillBounds.Left, fillBounds.Top, fillBounds.Width, rowHeight);
        var timerRow = new Rectangle(fillBounds.Left, deltaRow.Bottom + gap, fillBounds.Width, Math.Max(1, fillBounds.Bottom - deltaRow.Bottom - gap));
        DrawDeltaGradientPreviewRow(graphics, deltaRow, Localizer.Get("Delta", settings), GetPreviewDeltaGradientPalette());
        DrawDeltaGradientPreviewRow(graphics, timerRow, Localizer.Get("Main timer", settings), GetPreviewTimerGradientPalette());
        using var separatorPen = new Pen(BorderColor);
        graphics.DrawLine(separatorPen, fillBounds.Left, deltaRow.Bottom, fillBounds.Right, deltaRow.Bottom);
    }


    private void DrawDeltaGradientPreviewRow(
        Graphics graphics,
        Rectangle bounds,
        string label,
        (Color AheadColor, Color BaseColor, Color BehindColor, bool Enabled) palette)
    {
        string curve = GetPreviewDeltaGradientCurve();
        for (int x = 0; x < bounds.Width; x++)
        {
            float normalized = bounds.Width <= 1 ? 0f : x / (float)(bounds.Width - 1);
            float signed = normalized * 2f - 1f;
            float amount = DeltaGradientCurves.Evaluate(curve, Math.Abs(signed));
            Color color = signed < 0f
                ? BlendPreviewColor(palette.BaseColor, palette.AheadColor, amount)
                : BlendPreviewColor(palette.BaseColor, palette.BehindColor, amount);
            if (!palette.Enabled)
            {
                color = BlendPreviewColor(color, FieldColor, 0.45f);
            }

            using var pen = new Pen(color);
            graphics.DrawLine(pen, bounds.Left + x, bounds.Top, bounds.Left + x, bounds.Bottom);
        }

        using var font = UiTheme.FormFont(9f, FontStyle.Bold);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        };
        using var shadowBrush = new SolidBrush(Color.FromArgb(150, 0, 0, 0));
        using var textBrush = new SolidBrush(Color.FromArgb(235, 245, 245, 248));
        var labelBounds = new RectangleF(bounds.Left + 8, bounds.Top, Math.Max(1, bounds.Width - 16), bounds.Height);
        var shadowBounds = new RectangleF(labelBounds.X + 1, labelBounds.Y + 1, labelBounds.Width, labelBounds.Height);
        graphics.DrawString(label, font, shadowBrush, shadowBounds, format);
        graphics.DrawString(label, font, textBrush, labelBounds, format);
    }


    private (Color AheadColor, Color BaseColor, Color BehindColor, bool Enabled) GetPreviewDeltaGradientPalette()
    {
        return (
            GetPreviewColor(nameof(settings.Colors.DeltaAheadText), settings.Colors.DeltaAheadText, Color.FromArgb(114, 213, 114)),
            GetPreviewColor(nameof(settings.Colors.TimerText), settings.Colors.TimerText, Color.FromArgb(242, 242, 242)),
            GetPreviewColor(nameof(settings.Colors.DeltaBehindText), settings.Colors.DeltaBehindText, Color.FromArgb(240, 112, 112)),
            enableDeltaGradientColorBox.Checked);
    }


    private (Color AheadColor, Color BaseColor, Color BehindColor, bool Enabled) GetPreviewTimerGradientPalette()
    {
        return (
            GetPreviewColor(nameof(settings.Colors.TimerAheadText), settings.Colors.TimerAheadText, Color.LightGreen),
            GetPreviewColor(nameof(settings.Colors.TimerText), settings.Colors.TimerText, Color.FromArgb(242, 242, 242)),
            GetPreviewColor(nameof(settings.Colors.TimerBehindText), settings.Colors.TimerBehindText, Color.LightCoral),
            enableTimerGradientColorBox.Checked);
    }


    private Color GetPreviewColor(string key, string fallbackText, Color fallbackColor)
    {
        string text = colorTextBoxes.TryGetValue(key, out TextBox? textBox)
            ? textBox.Text
            : fallbackText;
        return ColorText.Parse(text, fallbackColor);
    }


    private string GetPreviewDeltaGradientCurve()
    {
        return GetSelectedDeltaGradientCurve(deltaGradientCurveBox);
    }


    private static Color BlendPreviewColor(Color from, Color to, float amount)
    {
        float t = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            (int)MathF.Round(from.R + (to.R - from.R) * t),
            (int)MathF.Round(from.G + (to.G - from.G) * t),
            (int)MathF.Round(from.B + (to.B - from.B) * t));
    }


    private void InvalidateDeltaGradientPreview()
    {
        if (!deltaGradientPreview.IsDisposed)
        {
            deltaGradientPreview.Invalidate();
        }
    }


    private void PaintSegmentBestDeltaHighlightPreview(Graphics graphics, Rectangle bounds)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var backgroundBrush = new SolidBrush(FieldColor);
        graphics.FillRectangle(backgroundBrush, bounds);
        using var borderPen = new Pen(BorderColor);
        graphics.DrawRectangle(borderPen, 0, 0, Math.Max(0, bounds.Width - 1), Math.Max(0, bounds.Height - 1));

        using var font = UiTheme.FormFont(16f, FontStyle.Bold);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        double seconds = Environment.TickCount64 / 1000.0;
        Color[] baseColors =
        {
            ColorText.Parse(settings.Colors.DeltaAheadText, Color.FromArgb(114, 213, 114)),
            ColorText.Parse(settings.Colors.DeltaBehindText, Color.FromArgb(240, 112, 112))
        };
        string[] texts = { "-0:01.23", "+0:01.23" };
        int columns = texts.Length;
        for (int i = 0; i < columns; i++)
        {
            var rect = new Rectangle(bounds.Left + i * bounds.Width / columns, bounds.Top, bounds.Width / columns, bounds.Height);
            Color color = SegmentBestDeltaHighlightStyles.Apply(baseColors[i], previewSegmentBestDeltaHighlightStyle, seconds);
            using var brush = new SolidBrush(color);
            graphics.DrawString(texts[i], font, brush, rect, format);
        }
    }


    private void PaintOutlineStylePreview(Graphics graphics, Rectangle bounds)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var backgroundBrush = new SolidBrush(FieldColor);
        graphics.FillRectangle(backgroundBrush, bounds);
        using var borderPen = new Pen(BorderColor);
        graphics.DrawRectangle(borderPen, 0, 0, Math.Max(0, bounds.Width - 1), Math.Max(0, bounds.Height - 1));

        using var font = UiTheme.FormFont(18f, FontStyle.Bold);
        string text = "0:01:23.45";
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Near
        };
        DrawPreviewOutlinedString(
            graphics,
            text,
            font,
            Color.White,
            bounds.Left + bounds.Width / 2f,
            bounds.Top + bounds.Height / 2f,
            format,
            previewOutlineStyle,
            ParseIntBox(splitCompletionOutlineThicknessBox, 30, 0, 100));
    }


    private static void DrawPreviewOutlinedString(
        Graphics graphics,
        string text,
        Font font,
        Color fillColor,
        float centerX,
        float centerY,
        StringFormat format,
        string style,
        int thicknessPercent)
    {
        string normalized = SplitCompletionOutlineStyles.Normalize(style);
        if (normalized == SplitCompletionOutlineStyles.None)
        {
            using var textBrush = new SolidBrush(fillColor);
            SizeF size = graphics.MeasureString(text, font, Size.Empty, format);
            graphics.DrawString(text, font, textBrush, centerX - size.Width / 2f, centerY - size.Height / 2f, format);
            return;
        }

        using GraphicsPath path = CreatePreviewTextPath(graphics, text, font, 0f, 0f, format);
        CenterPath(path, centerX, centerY);
        RectangleF pathBounds = path.GetBounds();
        RectangleF gradientBounds = InflateBounds(pathBounds, Math.Max(4f, font.Size * 0.35f));
        using var outlineBrush = new LinearGradientBrush(gradientBounds, Color.White, Color.White, LinearGradientMode.Horizontal);
        Color[] colors = SplitCompletionOutlineStyles.GetColors(normalized, Environment.TickCount64 / 1000.0);
        outlineBrush.InterpolationColors = new ColorBlend
        {
            Positions = CreateColorPositions(colors.Length),
            Colors = colors
        };

        float thickness = font.Size * Math.Clamp(thicknessPercent, 0, 100) / 100f;
        using var outlinePen = new Pen(outlineBrush, Math.Max(1f, thickness))
        {
            LineJoin = LineJoin.Round
        };
        graphics.DrawPath(outlinePen, path);

        using var fillBrush = new SolidBrush(fillColor);
        graphics.FillPath(fillBrush, path);
    }


    private static GraphicsPath CreatePreviewTextPath(Graphics graphics, string text, Font font, float x, float y, StringFormat format)
    {
        var path = new GraphicsPath();
        using StringFormat pathFormat = (StringFormat)format.Clone();
        path.AddString(
            text,
            font.FontFamily,
            (int)font.Style,
            emSize: font.SizeInPoints * graphics.DpiY / 72f,
            origin: new PointF(x, y),
            format: pathFormat);
        return path;
    }


    private static void CenterPath(GraphicsPath path, float centerX, float centerY)
    {
        RectangleF bounds = path.GetBounds();
        using var matrix = new Matrix();
        matrix.Translate(centerX - (bounds.Left + bounds.Width / 2f), centerY - (bounds.Top + bounds.Height / 2f));
        path.Transform(matrix);
    }


    private static RectangleF InflateBounds(RectangleF bounds, float amount)
    {
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return new RectangleF(bounds.X - amount, bounds.Y - amount, amount * 2f + 1f, amount * 2f + 1f);
        }

        bounds.Inflate(amount, amount);
        return bounds;
    }


    private static float[] CreateColorPositions(int count)
    {
        if (count <= 1)
        {
            return new[] { 0f };
        }

        var positions = new float[count];
        for (int i = 0; i < count; i++)
        {
            positions[i] = i / (float)(count - 1);
        }

        return positions;
    }


    private void RefreshAnimationOutlineGrid()
    {
        if (animationComparisonGrid is null || animationOutlineGrid is null)
        {
            return;
        }

        SaveAnimationOutlineControls();
        PopulateAnimationOutlineGrid();
        animationComparisonGrid.PerformLayout();
        animationOutlineGrid.PerformLayout();
    }


    private void PopulateSegmentBestDeltaHighlightGrid()
    {
        if (segmentBestDeltaHighlightGrid is null)
        {
            return;
        }

        List<RouteGroup> groups = BossRouteGroups.Build(settings).ToList();
        segmentBestDeltaHighlightGrid.SuspendLayout();
        try
        {
            ClearGrid(segmentBestDeltaHighlightGrid);
            segmentBestDeltaHighlightControls.Clear();
            AddHeaderRow(segmentBestDeltaHighlightGrid, "BOSS Group", "Effect");
            foreach (RouteGroup group in groups)
            {
                ComboBox styleBox = CreateSegmentBestDeltaHighlightStyleBox(GetSegmentBestDeltaHighlightStyle(group.Key));
                segmentBestDeltaHighlightControls[group.Key] = new SegmentBestDeltaHighlightControls(styleBox);
                int row = AddGridRow(segmentBestDeltaHighlightGrid);
                segmentBestDeltaHighlightGrid.Controls.Add(CreateRowLabel(BossRouteGroups.GetGroupDisplayName(group, settings)), 0, row);
                segmentBestDeltaHighlightGrid.Controls.Add(styleBox, 1, row);
            }
        }
        finally
        {
            segmentBestDeltaHighlightGrid.ResumeLayout(true);
        }
    }


    private string GetSegmentBestDeltaHighlightStyle(string key)
    {
        return settings.SegmentBestDeltaHighlightStyles.TryGetValue(key, out string? style)
            ? SegmentBestDeltaHighlightStyles.Normalize(style)
            : SegmentBestDeltaHighlightStyles.Aurora;
    }


    private ComboBox CreateSegmentBestDeltaHighlightStyleBox(string selectedStyle)
    {
        var comboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        UiTheme.StyleComboBox(comboBox);

        foreach (string style in SegmentBestDeltaHighlightStyles.Ids)
        {
            comboBox.Items.Add(new EffectStyleOption(style, Localizer.Get(SegmentBestDeltaHighlightStyles.GetDisplayName(style), settings)));
        }

        SetEffectStyle(comboBox, selectedStyle);
        comboBox.SelectedIndexChanged += (_, _) =>
        {
            previewSegmentBestDeltaHighlightStyle = GetSelectedEffectStyle(comboBox);
            segmentBestDeltaHighlightPreview.Invalidate();
        };
        return comboBox;
    }


    private static string GetSelectedEffectStyle(ComboBox comboBox)
    {
        return comboBox.SelectedItem is EffectStyleOption option
            ? option.Id
            : SegmentBestDeltaHighlightStyles.None;
    }


    private static void SetEffectStyle(ComboBox comboBox, string style)
    {
        string normalized = SegmentBestDeltaHighlightStyles.Normalize(style);
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is EffectStyleOption option &&
                string.Equals(option.Id, normalized, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }


    private void ConfigureDeltaGradientCurveBox()
    {
        deltaGradientCurveBox.Dock = DockStyle.Fill;
        deltaGradientCurveBox.DropDownStyle = ComboBoxStyle.DropDownList;
        UiTheme.StyleComboBox(deltaGradientCurveBox);
        deltaGradientCurveBox.Items.Clear();

        foreach (string curve in DeltaGradientCurves.Ids)
        {
            deltaGradientCurveBox.Items.Add(new EffectStyleOption(curve, Localizer.Get(DeltaGradientCurves.GetDisplayName(curve), settings)));
        }

        SetDeltaGradientCurve(deltaGradientCurveBox, settings.DeltaGradientCurve);
        deltaGradientCurveBox.SelectedIndexChanged += (_, _) => InvalidateDeltaGradientPreview();
    }


    private static string GetSelectedDeltaGradientCurve(ComboBox comboBox)
    {
        return comboBox.SelectedItem is EffectStyleOption option
            ? option.Id
            : DeltaGradientCurves.SoftStep;
    }


    private static void SetDeltaGradientCurve(ComboBox comboBox, string curve)
    {
        string normalized = DeltaGradientCurves.Normalize(curve);
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is EffectStyleOption option &&
                string.Equals(option.Id, normalized, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }


    private void SaveAnimationOutlineControls()
    {
        foreach ((string key, AnimationOutlineControls controls) in animationOutlineControls)
        {
            settings.SplitCompletionSplitComparisons[key] = controls.SplitComparison.Checked;
            settings.SplitCompletionSegmentComparisons[key] = controls.SegmentComparison.Checked;
            string splitStyle = GetSelectedOutlineStyle(controls.SplitTime);
            string segmentStyle = GetSelectedOutlineStyle(controls.SegmentTime);
            settings.SplitCompletionOutlineSplitStyles[key] = splitStyle;
            settings.SplitCompletionOutlineSegmentStyles[key] = segmentStyle;
        }

        foreach ((string key, SegmentBestDeltaHighlightControls controls) in segmentBestDeltaHighlightControls)
        {
            settings.SegmentBestDeltaHighlightStyles[key] = GetSelectedEffectStyle(controls.Style);
        }
    }
}
