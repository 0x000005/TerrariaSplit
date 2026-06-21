using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class AnimationSettingsPage : SettingsPageBase
{
    private void PopulateAnimationOutlineGrid()
    {
        if (animationComparisonGrid is null || animationOutlineGrid is null)
        {
            return;
        }

        List<RouteGroup> groups = SplitRouteGroups.Build(Draft).ToList();
        string signature = string.Join('\u001F', groups.Select(group => group.Key));
        if (animationGridSignature == signature && animationOutlineControls.Count > 0)
        {
            foreach (RouteGroup group in groups)
            {
                if (!animationOutlineControls.TryGetValue(group.Key, out AnimationOutlineControls? controls))
                {
                    continue;
                }

                controls.SplitComparison.Checked = GetAnimationOutlineSetting(Draft.Overlay.SplitCompletionSplitComparisons, group.Key);
                controls.SegmentComparison.Checked = GetAnimationOutlineSetting(Draft.Overlay.SplitCompletionSegmentComparisons, group.Key);
                SetOutlineStyle(controls.SplitTime, GetAnimationOutlineStyle(Draft.Overlay.SplitCompletionOutlineSplitStyles, group.Key));
                SetOutlineStyle(controls.SegmentTime, GetAnimationOutlineStyle(Draft.Overlay.SplitCompletionOutlineSegmentStyles, group.Key));
            }

            return;
        }

        animationComparisonGrid.SuspendLayout();
        animationOutlineGrid.SuspendLayout();
        try
        {
            SettingsUiFactory.ClearGrid(animationComparisonGrid);
            SettingsUiFactory.ClearGrid(animationOutlineGrid);
            animationOutlineControls.Clear();
            Factory.AddHeaderRow(animationComparisonGrid, "Group", "Cumulative time", "Segment time");
            Factory.AddHeaderRow(animationOutlineGrid, "Group", "Cumulative time", "Segment time");
            foreach (RouteGroup group in groups)
            {
                var splitComparisonBox = CreateComparisonCheckBox(GetAnimationOutlineSetting(Draft.Overlay.SplitCompletionSplitComparisons, group.Key));
                var segmentComparisonBox = CreateComparisonCheckBox(GetAnimationOutlineSetting(Draft.Overlay.SplitCompletionSegmentComparisons, group.Key));
                ThemedDropDownList splitTimeBox = CreateOutlineStyleBox(GetAnimationOutlineStyle(Draft.Overlay.SplitCompletionOutlineSplitStyles, group.Key));
                ThemedDropDownList segmentTimeBox = CreateOutlineStyleBox(GetAnimationOutlineStyle(Draft.Overlay.SplitCompletionOutlineSegmentStyles, group.Key));

                animationOutlineControls[group.Key] = new AnimationOutlineControls(splitComparisonBox, segmentComparisonBox, splitTimeBox, segmentTimeBox);

                int comparisonRow = Factory.AddGridRow(animationComparisonGrid);
                animationComparisonGrid.Controls.Add(Factory.CreateRowLabel(SplitRouteGroups.GetGroupDisplayName(group, Draft)), 0, comparisonRow);
                animationComparisonGrid.Controls.Add(splitComparisonBox, 1, comparisonRow);
                animationComparisonGrid.Controls.Add(segmentComparisonBox, 2, comparisonRow);

                int outlineRow = Factory.AddGridRow(animationOutlineGrid);
                animationOutlineGrid.Controls.Add(Factory.CreateRowLabel(SplitRouteGroups.GetGroupDisplayName(group, Draft)), 0, outlineRow);
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

        UpdateSplitCompletionAvailability();
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

    private ThemedDropDownList CreateOutlineStyleBox(string selectedStyle)
    {
        ThemedDropDownList comboBox = Factory.CreateDropDownList();
        foreach (string style in SplitCompletionOutlineStyles.Ids)
        {
            comboBox.Items.Add(new OutlineStyleOption(style, Context.Localize(SplitCompletionOutlineStyles.GetDisplayName(style))));
        }

        SetOutlineStyle(comboBox, selectedStyle);
        comboBox.SelectedIndexChanged += (_, _) =>
        {
            previewOutlineStyle = GetSelectedOutlineStyle(comboBox);
            outlineStylePreview.Invalidate();
        };
        return comboBox;
    }

    private static string GetSelectedOutlineStyle(ThemedDropDownList comboBox)
    {
        return comboBox.SelectedItem is OutlineStyleOption option
            ? option.Id
            : SplitCompletionOutlineStyles.None;
    }

    private static void SetOutlineStyle(ThemedDropDownList comboBox, string style)
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
        outlineStylePreview.BackColor = UiTheme.Field;
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
        segmentBestDeltaHighlightPreview.BackColor = UiTheme.Field;
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
        deltaGradientPreview.BackColor = UiTheme.Field;
        deltaGradientPreview.Margin = new Padding(0, 10, 0, 2);
        deltaGradientPreview.Paint += (_, e) => PaintDeltaGradientPreview(e.Graphics, deltaGradientPreview.ClientRectangle);
        UiTheme.EnableDoubleBuffering(deltaGradientPreview);
        return deltaGradientPreview;
    }

    private void PaintDeltaGradientPreview(Graphics graphics, Rectangle bounds)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var backgroundBrush = new SolidBrush(UiTheme.Field);
        graphics.FillRectangle(backgroundBrush, bounds);
        using var borderPen = new Pen(UiTheme.Border);
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
        DrawDeltaGradientPreviewRow(graphics, deltaRow, Context.Localize("Delta"), GetPreviewDeltaGradientPalette());
        DrawDeltaGradientPreviewRow(graphics, timerRow, Context.Localize("Main timer"), GetPreviewTimerGradientPalette());
        using var separatorPen = new Pen(UiTheme.Border);
        graphics.DrawLine(separatorPen, fillBounds.Left, deltaRow.Bottom, fillBounds.Right, deltaRow.Bottom);
    }

    private void DrawDeltaGradientPreviewRow(
        Graphics graphics,
        Rectangle bounds,
        string label,
        (Color AheadColor, Color BaseColor, Color BehindColor, bool Enabled) palette)
    {
        string curve = GetSelectedDeltaGradientCurve(deltaGradientCurveBox);
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
                color = BlendPreviewColor(color, UiTheme.Field, 0.45f);
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
            ColorText.Parse(Draft.Overlay.Colors.DeltaAheadText, Color.FromArgb(114, 213, 114)),
            ColorText.Parse(Draft.Overlay.Colors.TimerText, Color.FromArgb(242, 242, 242)),
            ColorText.Parse(Draft.Overlay.Colors.DeltaBehindText, Color.FromArgb(240, 112, 112)),
            enableDeltaGradientColorBox.Checked || enableCurrentDeltaGradientColorBox.Checked);
    }

    private (Color AheadColor, Color BaseColor, Color BehindColor, bool Enabled) GetPreviewTimerGradientPalette()
    {
        return (
            ColorText.Parse(Draft.Overlay.Colors.TimerAheadText, Color.LightGreen),
            ColorText.Parse(Draft.Overlay.Colors.TimerText, Color.FromArgb(242, 242, 242)),
            ColorText.Parse(Draft.Overlay.Colors.TimerBehindText, Color.LightCoral),
            enableTimerGradientColorBox.Checked);
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

    private void UpdateDeltaGradientState()
    {
        UpdateEffectAvailability();
        InvalidateDeltaGradientPreview();
    }

    private void UpdateEffectAvailability()
    {
        SetEnabled(
            enableDefeatedBossIconLightingBox.Checked,
            undefeatedIconGrayscaleBox,
            undefeatedIconBrightnessBox,
            currentBossIconGrayscaleWeakenBox,
            currentBossIconBrightnessBoostBox);

        SetEnabled(
            showCurrentSplitHighlightBox.Checked,
            currentSplitHighlightScaleBox,
            currentSplitDepthStrengthBox);

        bool deltaGradientEnabled =
            enableDeltaGradientColorBox.Checked ||
            enableCurrentDeltaGradientColorBox.Checked ||
            enableTimerGradientColorBox.Checked;
        SetEnabled(deltaGradientEnabled, deltaGradientThresholdBox, deltaGradientCurveBox, deltaGradientPreview);

        UpdateSplitCompletionAvailability();
        UpdateSegmentBestDeltaHighlightAvailability();
    }

    private void UpdateSplitCompletionAvailability()
    {
        bool enabled = showSplitCompletionAnimationBox.Checked;
        SetEnabled(enabled, splitCompletionAnimationDurationBox, splitCompletionOutlineThicknessBox, outlineStylePreview);
        foreach (AnimationOutlineControls controls in animationOutlineControls.Values)
        {
            SetEnabled(enabled, controls.SplitComparison, controls.SegmentComparison, controls.SplitTime, controls.SegmentTime);
        }
    }

    private void UpdateSegmentBestDeltaHighlightAvailability()
    {
        bool enabled = showSegmentBestDeltaHighlightBox.Checked;
        SetEnabled(enabled, segmentBestDeltaHighlightPreview);
        foreach (SegmentBestDeltaHighlightControls controls in segmentBestDeltaHighlightControls.Values)
        {
            controls.Style.Enabled = enabled;
        }
    }

    private static void SetEnabled(bool enabled, params Control[] controls)
    {
        foreach (Control control in controls)
        {
            control.Enabled = enabled;
        }
    }

    private void PaintSegmentBestDeltaHighlightPreview(Graphics graphics, Rectangle bounds)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var backgroundBrush = new SolidBrush(UiTheme.Field);
        graphics.FillRectangle(backgroundBrush, bounds);
        using var borderPen = new Pen(UiTheme.Border);
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
            ColorText.Parse(Draft.Overlay.Colors.DeltaAheadText, Color.FromArgb(114, 213, 114)),
            ColorText.Parse(Draft.Overlay.Colors.DeltaBehindText, Color.FromArgb(240, 112, 112))
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
        using var backgroundBrush = new SolidBrush(UiTheme.Field);
        graphics.FillRectangle(backgroundBrush, bounds);
        using var borderPen = new Pen(UiTheme.Border);
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
            SettingsValueParser.ParseIntBox(splitCompletionOutlineThicknessBox, 30, 0, 100));
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

        using GraphicsPath path = TextEffectGeometry.CreateTextPath(graphics, text, font, 0f, 0f, format);
        TextEffectGeometry.CenterPath(path, centerX, centerY);
        RectangleF pathBounds = path.GetBounds();
        RectangleF gradientBounds = TextEffectGeometry.InflateBounds(pathBounds, Math.Max(4f, font.Size * 0.35f));
        using var outlineBrush = new LinearGradientBrush(gradientBounds, Color.White, Color.White, LinearGradientMode.Horizontal);
        Color[] colors = SplitCompletionOutlineStyles.GetColors(normalized, Environment.TickCount64 / 1000.0);
        outlineBrush.InterpolationColors = new ColorBlend
        {
            Positions = TextEffectGeometry.CreateColorPositions(colors.Length),
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

    private void PopulateSegmentBestDeltaHighlightGrid()
    {
        if (segmentBestDeltaHighlightGrid is null)
        {
            return;
        }

        List<RouteGroup> groups = SplitRouteGroups.Build(Draft).ToList();
        segmentBestDeltaHighlightGrid.SuspendLayout();
        try
        {
            SettingsUiFactory.ClearGrid(segmentBestDeltaHighlightGrid);
            segmentBestDeltaHighlightControls.Clear();
            Factory.AddHeaderRow(segmentBestDeltaHighlightGrid, "Group", "Effect");
            foreach (RouteGroup group in groups)
            {
                ThemedDropDownList styleBox = CreateSegmentBestDeltaHighlightStyleBox(GetSegmentBestDeltaHighlightStyle(group.Key));
                segmentBestDeltaHighlightControls[group.Key] = new SegmentBestDeltaHighlightControls(styleBox);
                int row = Factory.AddGridRow(segmentBestDeltaHighlightGrid);
                segmentBestDeltaHighlightGrid.Controls.Add(Factory.CreateRowLabel(SplitRouteGroups.GetGroupDisplayName(group, Draft)), 0, row);
                segmentBestDeltaHighlightGrid.Controls.Add(styleBox, 1, row);
            }
        }
        finally
        {
            segmentBestDeltaHighlightGrid.ResumeLayout(true);
        }

        UpdateSegmentBestDeltaHighlightAvailability();
    }

    private string GetSegmentBestDeltaHighlightStyle(string key)
    {
        return Draft.Overlay.SegmentBestDeltaHighlightStyles.TryGetValue(key, out string? style)
            ? SegmentBestDeltaHighlightStyles.Normalize(style)
            : SegmentBestDeltaHighlightStyles.Aurora;
    }

    private ThemedDropDownList CreateSegmentBestDeltaHighlightStyleBox(string selectedStyle)
    {
        ThemedDropDownList comboBox = Factory.CreateDropDownList();
        foreach (string style in SegmentBestDeltaHighlightStyles.Ids)
        {
            comboBox.Items.Add(new EffectStyleOption(style, Context.Localize(SegmentBestDeltaHighlightStyles.GetDisplayName(style))));
        }

        SetEffectStyle(comboBox, selectedStyle);
        comboBox.SelectedIndexChanged += (_, _) =>
        {
            previewSegmentBestDeltaHighlightStyle = GetSelectedEffectStyle(comboBox);
            segmentBestDeltaHighlightPreview.Invalidate();
        };
        return comboBox;
    }

    private static string GetSelectedEffectStyle(ThemedDropDownList comboBox)
    {
        return comboBox.SelectedItem is EffectStyleOption option
            ? option.Id
            : SegmentBestDeltaHighlightStyles.None;
    }

    private static void SetEffectStyle(ThemedDropDownList comboBox, string style)
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
        deltaGradientCurveBox.Items.Clear();

        foreach (string curve in DeltaGradientCurves.Ids)
        {
            deltaGradientCurveBox.Items.Add(new EffectStyleOption(curve, Context.Localize(DeltaGradientCurves.GetDisplayName(curve))));
        }

        SetDeltaGradientCurve(deltaGradientCurveBox, Draft.Overlay.DeltaGradientCurve);
        deltaGradientCurveBox.SelectedIndexChanged += (_, _) => InvalidateDeltaGradientPreview();
    }

    private static string GetSelectedDeltaGradientCurve(ThemedDropDownList comboBox)
    {
        return comboBox.SelectedItem is EffectStyleOption option
            ? option.Id
            : DeltaGradientCurves.SoftStep;
    }

    private static void SetDeltaGradientCurve(ThemedDropDownList comboBox, string curve)
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
            Draft.Overlay.SplitCompletionSplitComparisons[key] = controls.SplitComparison.Checked;
            Draft.Overlay.SplitCompletionSegmentComparisons[key] = controls.SegmentComparison.Checked;
            string splitStyle = GetSelectedOutlineStyle(controls.SplitTime);
            string segmentStyle = GetSelectedOutlineStyle(controls.SegmentTime);
            Draft.Overlay.SplitCompletionOutlineSplitStyles[key] = splitStyle;
            Draft.Overlay.SplitCompletionOutlineSegmentStyles[key] = segmentStyle;
        }

        foreach ((string key, SegmentBestDeltaHighlightControls controls) in segmentBestDeltaHighlightControls)
        {
            Draft.Overlay.SegmentBestDeltaHighlightStyles[key] = GetSelectedEffectStyle(controls.Style);
        }
    }

    private static CheckBox CreateComparisonCheckBox(bool checkedValue)
    {
        var checkBox = new CheckBox
        {
            Checked = checkedValue,
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.Text,
            TextAlign = ContentAlignment.MiddleCenter
        };
        UiTheme.StyleCheckBox(checkBox);
        return checkBox;
    }

    private static void ConfigureCheckBox(CheckBox checkBox, bool selected)
    {
        checkBox.Checked = selected;
        checkBox.Dock = DockStyle.Fill;
        UiTheme.StyleCheckBox(checkBox);
    }

    private static void ConfigureNumberBox(TextBox textBox, int selected, int minimum, int maximum)
    {
        UiTheme.StyleTextBox(textBox);
        textBox.Dock = DockStyle.Fill;
        textBox.Text = Math.Clamp(selected, minimum, maximum).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void ConfigureTimeBox(TextBox textBox, int selectedSeconds, int minimumSeconds, int maximumSeconds)
    {
        UiTheme.StyleTextBox(textBox);
        textBox.Dock = DockStyle.Fill;
        textBox.Text = TimeText.FormatSplit(TimeSpan.FromSeconds(Math.Clamp(selectedSeconds, minimumSeconds, maximumSeconds)));
        textBox.PlaceholderText = "m:ss or h:mm:ss";
    }

    private static void ConfigureDecimalBox(TextBox textBox, float value, decimal minimum, decimal maximum)
    {
        UiTheme.StyleTextBox(textBox);
        textBox.Dock = DockStyle.Fill;
        textBox.Text = Math.Clamp((decimal)value, minimum, maximum).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed record OutlineStyleOption(string Id, string DisplayName)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }

    private sealed record EffectStyleOption(string Id, string DisplayName)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }

    private sealed record AnimationOutlineControls(
        CheckBox SplitComparison,
        CheckBox SegmentComparison,
        ThemedDropDownList SplitTime,
        ThemedDropDownList SegmentTime);

    private sealed record SegmentBestDeltaHighlightControls(ThemedDropDownList Style);
}
