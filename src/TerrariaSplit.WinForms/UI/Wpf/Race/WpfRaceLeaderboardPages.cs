using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TerrariaSplit.UI.Wpf.Settings;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfColor = System.Windows.Media.Color;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfPoint = System.Windows.Point;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace TerrariaSplit.UI.Wpf.Race;

internal sealed class WpfRaceLeaderboardPages
{
    private const double ShowColWidth = 44;
    private const double NumColWidth = 68;
    private const double AlignColWidth = 98;
    private const double FontColWidth = 148;
    private const double FlagColWidth = 44;
    private const double SwatchColWidth = 156;

    private const string KeyRank = "RaceLeaderboardRank";
    private const string KeyPlayer = "RaceLeaderboardPlayer";
    private const string KeyPlayerSelf = "RaceLeaderboardPlayerSelf";
    private const string KeyPlayerOther = "RaceLeaderboardPlayerOther";
    private const string KeyIcon = "RaceLeaderboardIcon";
    private const string KeyTime = "RaceLeaderboardTime";

    private const string SpaceRankPlayer = "RankPlayer";
    private const string SpacePlayerIcon = "PlayerIcon";
    private const string SpaceIconTime = "IconTime";

    private readonly IRacePanelShell shell;
    private readonly WpfSettingsDialogService dialogs;
    private readonly RaceLeaderboardSettings leaderboardSettings;

    private readonly Dictionary<string, ColumnControls> columnControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WpfTextBox> spacingControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ColorRowControls> colorControls = new(StringComparer.OrdinalIgnoreCase);
    private WpfCheckBox? useRankColorForMainTimerBox;
    private RankGradientControls? rankGradientControls;

    public WpfRaceLeaderboardPages(IRacePanelShell shell, WpfSettingsDialogService dialogs)
    {
        this.shell = shell;
        this.dialogs = dialogs;
        leaderboardSettings = CloneLeaderboardSettings(shell.LeaderboardSettings);
    }

    public FrameworkElement BuildInterfacePage()
    {
        return WpfSettingsUi.CreateScrollPage(parent =>
        {
            var card = WpfSettingsUi.CreateCard(
                Localize("Leaderboard display"),
                out StackPanel body);

            body.Children.Add(CreateInterfaceHeader());
            body.Children.Add(CreateInterfaceRow(KeyRank, "Rank", leaderboardSettings.Rank, leaderboardSettings.RankAlignment, leaderboardSettings.TextEffects.Rank));
            body.Children.Add(CreateSpacingRow(SpaceRankPlayer, leaderboardSettings.RankPlayerGap));
            body.Children.Add(CreateInterfaceRow(KeyPlayer, "Nickname", leaderboardSettings.Player, leaderboardSettings.PlayerAlignment, leaderboardSettings.TextEffects.Player));
            body.Children.Add(CreateSpacingRow(SpacePlayerIcon, leaderboardSettings.PlayerIconGap));
            body.Children.Add(CreateInterfaceRow(KeyIcon, "Icon", leaderboardSettings.Icon, leaderboardSettings.IconAlignment, leaderboardSettings.TextEffects.Icon, includeFontFamily: false, includeBold: false, includeItalic: false));
            body.Children.Add(CreateSpacingRow(SpaceIconTime, leaderboardSettings.IconTimeGap));
            body.Children.Add(CreateInterfaceRow(KeyTime, "Time", leaderboardSettings.Time, leaderboardSettings.TimeAlignment, leaderboardSettings.TextEffects.Time, isLast: true));

            foreach (ColumnControls controls in columnControls.Values)
            {
                controls.Show.Checked += (_, _) => UpdateColumnAvailability();
                controls.Show.Unchecked += (_, _) => UpdateColumnAvailability();
            }

            UpdateColumnAvailability();

            WpfButton applyBtn = WpfSettingsUi.CreateButton(Localize("Apply UI settings"), minWidth: 112);
            applyBtn.HorizontalAlignment = WpfHorizontalAlignment.Left;
            applyBtn.Margin = new Thickness(0, 10, 0, 10);
            applyBtn.Click += (_, _) => SaveLeaderboardSettings();
            body.Children.Add(applyBtn);

            parent.Children.Add(card);
        });
    }

    public FrameworkElement BuildColorsPage()
    {
        return WpfSettingsUi.CreateScrollPage(parent =>
        {
            leaderboardSettings.Colors.RankGradient ??= new RaceLeaderboardRankGradientColorSettings();
            leaderboardSettings.Colors.Player ??= new RaceLeaderboardColumnColorSettings();
            leaderboardSettings.Colors.PlayerSelf ??= CloneColumnColor(leaderboardSettings.Colors.Player);
            leaderboardSettings.Colors.PlayerOther ??= CloneColumnColor(leaderboardSettings.Colors.Player);

            var card = WpfSettingsUi.CreateCard(
                Localize("Colors"),
                out StackPanel body);

            useRankColorForMainTimerBox = WpfSettingsUi.CreateToggleSwitch(leaderboardSettings.UseRankColorForMainTimer);
            WpfSettingsUi.AddSettingRow(body, Localize("Use rank color for main timer"), useRankColorForMainTimerBox);

            body.Children.Add(CreateColorTableHeader());
            body.Children.Add(CreateRankGradientColorRow(leaderboardSettings.Colors.Rank, leaderboardSettings.Colors.RankGradient));
            body.Children.Add(CreateColorRow(KeyPlayerSelf, "Nickname: self", leaderboardSettings.Colors.PlayerSelf, includeText: true, isLast: false));
            body.Children.Add(CreateColorRow(KeyPlayerOther, "Nickname: other", leaderboardSettings.Colors.PlayerOther, includeText: true, isLast: false));
            body.Children.Add(CreateColorRow(KeyIcon, "Icon", leaderboardSettings.Colors.Icon, includeText: false, isLast: false));
            body.Children.Add(CreateColorRow(KeyTime, "Time", leaderboardSettings.Colors.Time, includeText: true, isLast: true));

            WpfButton applyBtn = WpfSettingsUi.CreateButton(Localize("Apply color settings"), minWidth: 112);
            applyBtn.HorizontalAlignment = WpfHorizontalAlignment.Left;
            applyBtn.Margin = new Thickness(0, 10, 0, 10);
            applyBtn.Click += (_, _) => SaveLeaderboardSettings();
            body.Children.Add(applyBtn);

            parent.Children.Add(card);
        });
    }

    // ==================== Interface Table ====================

    private static Grid CreateInterfaceGrid()
    {
        return WpfSettingsUi.CreateColumnGrid(
            new GridLength(1, GridUnitType.Star),
            new GridLength(ShowColWidth),
            new GridLength(NumColWidth),
            new GridLength(AlignColWidth),
            new GridLength(FontColWidth),
            new GridLength(NumColWidth),
            new GridLength(FlagColWidth),
            new GridLength(FlagColWidth),
            new GridLength(NumColWidth),
            new GridLength(NumColWidth),
            new GridLength(NumColWidth));
    }

    private Border CreateInterfaceHeader()
    {
        Grid grid = CreateInterfaceGrid();
        string[] labels =
        [
            Localize("Column"), Localize("Show"), Localize("Width"), Localize("Alignment"),
            Localize("Font family"), Localize("Size"), Localize("Bold"), Localize("Italic"),
            Localize("Opacity %"), Localize("Shadow %"), Localize("Outline %")
        ];
        TextAlignment[] aligns =
        [
            TextAlignment.Left, TextAlignment.Center, TextAlignment.Center, TextAlignment.Center,
            TextAlignment.Center, TextAlignment.Center, TextAlignment.Center, TextAlignment.Center,
            TextAlignment.Center, TextAlignment.Center, TextAlignment.Center
        ];
        for (int i = 0; i < labels.Length; i++)
        {
            TextBlock h = WpfSettingsUi.CreateTableHeaderText(labels[i], aligns[i]);
            h.Margin = i == 0 ? new Thickness(0) : new Thickness(3, 0, 3, 0);
            Grid.SetColumn(h, i);
            grid.Children.Add(h);
        }

        return WpfSettingsUi.CreateTableHeaderContainer(grid);
    }

    private Border CreateInterfaceRow(
        string key,
        string labelKey,
        UiColumnSettings column,
        string alignment,
        RaceLeaderboardColumnEffectSettings effect,
        bool includeFontFamily = true,
        bool includeBold = true,
        bool includeItalic = true,
        bool isLast = false)
    {
        Grid grid = CreateInterfaceGrid();
        var cellPad = new Thickness(3, 0, 3, 0);

        TextBlock label = WpfSettingsUi.CreateRowLabel(Localize(labelKey));
        WpfCheckBox showBox = WpfSettingsUi.CreateCheckBox(column.Show, centered: true);
        WpfTextBox widthBox = WpfSettingsUi.CreateNumberBox(column.Width, 1, 1000, width: NumColWidth - 8);
        widthBox.HorizontalAlignment = WpfHorizontalAlignment.Center;
        WpfComboBox alignBox = CreateAlignmentBox(alignment);
        alignBox.Width = AlignColWidth - 6;

        WpfFontFamilySelector? fontBox = null;
        if (includeFontFamily)
        {
            fontBox = new WpfFontFamilySelector { Width = FontColWidth - 6, Margin = cellPad };
            fontBox.SetSelectedFontFamily(column.FontFamily);
            Grid.SetColumn(fontBox, 4);
            grid.Children.Add(fontBox);
        }

        WpfTextBox fontSizeBox = WpfSettingsUi.CreateDecimalBox(column.FontSize, 6, 96, width: NumColWidth - 8);
        fontSizeBox.HorizontalAlignment = WpfHorizontalAlignment.Center;

        WpfCheckBox? boldBox = null;
        if (includeBold)
        {
            boldBox = WpfSettingsUi.CreateCheckBox(column.Bold, centered: true);
            Grid.SetColumn(boldBox, 6);
            grid.Children.Add(boldBox);
        }

        WpfCheckBox? italicBox = null;
        if (includeItalic)
        {
            italicBox = WpfSettingsUi.CreateCheckBox(column.Italic, centered: true);
            Grid.SetColumn(italicBox, 7);
            grid.Children.Add(italicBox);
        }

        WpfTextBox opacityBox = WpfSettingsUi.CreateNumberBox(effect.OpacityPercent, 0, 100, width: NumColWidth - 8);
        WpfTextBox shadowBox = WpfSettingsUi.CreateNumberBox(effect.ShadowPercent, 0, 100, width: NumColWidth - 8);
        WpfTextBox outlineBox = WpfSettingsUi.CreateNumberBox(effect.OutlineThicknessPercent, 0, 100, width: NumColWidth - 8);
        opacityBox.HorizontalAlignment = WpfHorizontalAlignment.Center;
        shadowBox.HorizontalAlignment = WpfHorizontalAlignment.Center;
        outlineBox.HorizontalAlignment = WpfHorizontalAlignment.Center;

        Grid.SetColumn(label, 0);
        Grid.SetColumn(showBox, 1);
        Grid.SetColumn(widthBox, 2);
        Grid.SetColumn(alignBox, 3);
        Grid.SetColumn(fontSizeBox, 5);
        Grid.SetColumn(opacityBox, 8);
        Grid.SetColumn(shadowBox, 9);
        Grid.SetColumn(outlineBox, 10);
        grid.Children.Add(label);
        grid.Children.Add(showBox);
        grid.Children.Add(widthBox);
        grid.Children.Add(alignBox);
        grid.Children.Add(fontSizeBox);
        grid.Children.Add(opacityBox);
        grid.Children.Add(shadowBox);
        grid.Children.Add(outlineBox);

        columnControls[key] = new ColumnControls(
            showBox,
            widthBox,
            alignBox,
            fontBox,
            fontSizeBox,
            boldBox,
            italicBox,
            opacityBox,
            shadowBox,
            outlineBox);

        return WpfSettingsUi.CreateTableRowContainer(grid, showBottomBorder: !isLast);
    }

    private Border CreateSpacingRow(string key, int value)
    {
        Grid grid = CreateInterfaceGrid();
        TextBlock label = WpfSettingsUi.CreateTableHeaderText(Localize("Spacing"));
        label.Margin = new Thickness(6, 0, 6, 0);
        WpfTextBox box = WpfSettingsUi.CreateNumberBox(value, 0, 1000, width: NumColWidth - 8);
        box.HorizontalAlignment = WpfHorizontalAlignment.Center;
        spacingControls[key] = box;

        Grid.SetColumn(label, 0);
        Grid.SetColumn(box, 2);
        grid.Children.Add(label);
        grid.Children.Add(box);
        return WpfSettingsUi.CreateTableRowContainer(grid, showBottomBorder: true);
    }

    private WpfComboBox CreateAlignmentBox(string selectedAlignment)
    {
        WpfComboBox box = WpfSettingsUi.CreateComboBox();
        string normalized = UiColumnAlignment.Normalize(selectedAlignment, UiColumnAlignment.Right);
        foreach (string alignment in UiColumnAlignment.All)
        {
            box.Items.Add(new OptionItem(Localize(UiColumnAlignment.GetDisplayName(alignment)), alignment));
        }

        for (int i = 0; i < box.Items.Count; i++)
        {
            if (box.Items[i] is OptionItem opt && string.Equals(opt.Value, normalized, StringComparison.Ordinal))
            {
                box.SelectedIndex = i;
                break;
            }
        }

        if (box.SelectedIndex < 0 && box.Items.Count > 0)
        {
            box.SelectedIndex = 0;
        }

        return box;
    }

    private void UpdateColumnAvailability()
    {
        foreach (ColumnControls controls in columnControls.Values)
        {
            bool visible = controls.Show.IsChecked == true;
            controls.Width.IsEnabled = visible;
            controls.Alignment.IsEnabled = visible;
            if (controls.FontFamily is not null)
            {
                controls.FontFamily.IsEnabled = visible;
            }

            controls.FontSize.IsEnabled = visible;
            if (controls.Bold is not null)
            {
                controls.Bold.IsEnabled = visible;
            }

            if (controls.Italic is not null)
            {
                controls.Italic.IsEnabled = visible;
            }

            controls.Opacity.IsEnabled = visible;
            controls.Shadow.IsEnabled = visible;
            controls.Outline.IsEnabled = visible;
        }

        SetSpacingEnabled(SpaceRankPlayer, IsColumnShown(KeyRank));
        SetSpacingEnabled(SpacePlayerIcon, IsColumnShown(KeyPlayer));
        SetSpacingEnabled(SpaceIconTime, IsColumnShown(KeyIcon));
    }

    private bool IsColumnShown(string key) =>
        columnControls.TryGetValue(key, out ColumnControls? c) && c.Show.IsChecked == true;

    private void SetSpacingEnabled(string key, bool enabled)
    {
        if (spacingControls.TryGetValue(key, out WpfTextBox? box))
        {
            box.IsEnabled = enabled;
        }
    }

    // ==================== Colors Table ====================

    private static Grid CreateColorTableGrid()
    {
        return WpfSettingsUi.CreateColumnGrid(
            new GridLength(1, GridUnitType.Star),
            new GridLength(330),
            new GridLength(SwatchColWidth),
            new GridLength(SwatchColWidth));
    }

    private Border CreateColorTableHeader()
    {
        Grid grid = CreateColorTableGrid();
        string[] labels = [Localize("Text type"), Localize("Text"), Localize("Outline"), Localize("Shadow")];
        for (int i = 0; i < labels.Length; i++)
        {
            TextBlock h = WpfSettingsUi.CreateTableHeaderText(labels[i], i == 0 ? TextAlignment.Left : TextAlignment.Center);
            h.Margin = i == 0 ? new Thickness(0, 0, 8, 0) : new Thickness(6, 0, 0, 0);
            Grid.SetColumn(h, i);
            grid.Children.Add(h);
        }

        return WpfSettingsUi.CreateTableHeaderContainer(grid);
    }

    private Border CreateRankGradientColorRow(
        RaceLeaderboardColumnColorSettings colors,
        RaceLeaderboardRankGradientColorSettings gradient)
    {
        Grid grid = CreateColorTableGrid();

        TextBlock label = WpfSettingsUi.CreateRowLabel(Localize("Rank"));
        label.Margin = new Thickness(0, 0, 8, 0);

        WpfTextBox startBox = CreateColorHexBox(gradient.Start, compact: true);
        WpfTextBox midBox = CreateColorHexBox(gradient.Middle, compact: true);
        WpfTextBox endBox = CreateColorHexBox(gradient.End, compact: true);
        WpfTextBox outlineBox = CreateColorHexBox(colors.Outline);
        WpfTextBox shadowBox = CreateColorHexBox(colors.Shadow);

        rankGradientControls = new RankGradientControls(startBox, midBox, endBox);
        colorControls[KeyRank] = new ColorRowControls(null, outlineBox, shadowBox);

        var previewBar = new Border
        {
            Height = 9,
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 5, 0, 0)
        };
        previewBar.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

        void UpdatePreview()
        {
            var defaults = new RaceLeaderboardRankGradientColorSettings();
            System.Drawing.Color s = ColorText.Parse(startBox.Text, ColorText.Parse(defaults.Start, System.Drawing.Color.Gold));
            System.Drawing.Color m = ColorText.Parse(midBox.Text, ColorText.Parse(defaults.Middle, System.Drawing.Color.White));
            System.Drawing.Color e = ColorText.Parse(endBox.Text, ColorText.Parse(defaults.End, System.Drawing.Color.Red));
            var brush = new LinearGradientBrush
            {
                StartPoint = new WpfPoint(0, 0.5),
                EndPoint = new WpfPoint(1, 0.5),
                GradientStops =
                [
                    new GradientStop(WpfColor.FromRgb(s.R, s.G, s.B), 0d),
                    new GradientStop(WpfColor.FromRgb(m.R, m.G, m.B), 0.5d),
                    new GradientStop(WpfColor.FromRgb(e.R, e.G, e.B), 1d)
                ]
            };
            brush.Freeze();
            previewBar.Background = brush;
        }

        startBox.TextChanged += (_, _) => UpdatePreview();
        midBox.TextChanged += (_, _) => UpdatePreview();
        endBox.TextChanged += (_, _) => UpdatePreview();
        UpdatePreview();

        Grid inputs = WpfSettingsUi.CreateColumnGrid(
            new GridLength(1, GridUnitType.Star),
            new GridLength(5),
            new GridLength(1, GridUnitType.Star),
            new GridLength(5),
            new GridLength(1, GridUnitType.Star));
        FrameworkElement startEd = CreateColorEditor(startBox);
        FrameworkElement midEd = CreateColorEditor(midBox);
        FrameworkElement endEd = CreateColorEditor(endBox);
        Grid.SetColumn(startEd, 0);
        Grid.SetColumn(midEd, 2);
        Grid.SetColumn(endEd, 4);
        inputs.Children.Add(startEd);
        inputs.Children.Add(midEd);
        inputs.Children.Add(endEd);

        var gradientEditor = new StackPanel
        {
            Orientation = WpfOrientation.Vertical,
            Margin = new Thickness(6, 0, 0, 0)
        };
        gradientEditor.Children.Add(inputs);
        gradientEditor.Children.Add(previewBar);

        FrameworkElement outlineEd = CreateColorEditor(outlineBox);
        outlineEd.Margin = new Thickness(6, 0, 0, 0);
        outlineEd.VerticalAlignment = VerticalAlignment.Top;

        FrameworkElement shadowEd = CreateColorEditor(shadowBox);
        shadowEd.Margin = new Thickness(6, 0, 0, 0);
        shadowEd.VerticalAlignment = VerticalAlignment.Top;

        Grid.SetColumn(label, 0);
        Grid.SetColumn(gradientEditor, 1);
        Grid.SetColumn(outlineEd, 2);
        Grid.SetColumn(shadowEd, 3);
        grid.Children.Add(label);
        grid.Children.Add(gradientEditor);
        grid.Children.Add(outlineEd);
        grid.Children.Add(shadowEd);

        return WpfSettingsUi.CreateRowDividerContainer(grid, showBottomBorder: true, padding: new Thickness(0, 5, 0, 5), minHeightResourceKey: "TableRowHeight");
    }

    private Border CreateColorRow(
        string key,
        string labelKey,
        RaceLeaderboardColumnColorSettings colors,
        bool includeText,
        bool isLast)
    {
        Grid grid = CreateColorTableGrid();

        TextBlock label = WpfSettingsUi.CreateRowLabel(Localize(labelKey));
        label.Margin = new Thickness(0, 0, 8, 0);

        WpfTextBox? textBox = includeText ? CreateColorHexBox(colors.Text) : null;
        WpfTextBox outlineBox = CreateColorHexBox(colors.Outline);
        WpfTextBox shadowBox = CreateColorHexBox(colors.Shadow);
        colorControls[key] = new ColorRowControls(textBox, outlineBox, shadowBox);

        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        if (textBox is not null)
        {
            FrameworkElement textEd = CreateColorEditor(textBox);
            textEd.Margin = new Thickness(6, 0, 0, 0);
            Grid.SetColumn(textEd, 1);
            grid.Children.Add(textEd);
        }

        FrameworkElement outlineEd = CreateColorEditor(outlineBox);
        outlineEd.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(outlineEd, 2);
        grid.Children.Add(outlineEd);

        FrameworkElement shadowEd = CreateColorEditor(shadowBox);
        shadowEd.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(shadowEd, 3);
        grid.Children.Add(shadowEd);

        return WpfSettingsUi.CreateTableRowContainer(grid, showBottomBorder: !isLast);
    }

    private static WpfTextBox CreateColorHexBox(string value, bool compact = false)
    {
        WpfTextBox box = WpfSettingsUi.CreateTextBox(value, mono: true, centerAlign: true);
        box.Padding = compact ? new Thickness(4, 0, 4, 0) : new Thickness(6, 0, 6, 0);
        return box;
    }

    private Grid CreateColorEditor(WpfTextBox textBox)
    {
        Grid grid = WpfSettingsUi.CreateColumnGrid(new GridLength(1, GridUnitType.Star), GridLength.Auto);
        textBox.Margin = new Thickness(0, 0, 4, 0);

        var swatch = new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1)
        };
        swatch.SetResourceReference(Border.BorderBrushProperty, "BorderStrongBrush");

        var glyph = new TextBlock
        {
            Text = "T",
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        glyph.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        swatch.Child = glyph;

        var pickButton = new WpfButton
        {
            Width = 26,
            Height = 26,
            MinHeight = 26,
            Padding = new Thickness(0),
            Content = swatch
        };
        pickButton.Click += (_, _) => dialogs.PickColor(textBox);

        void UpdateSwatch()
        {
            System.Drawing.Color parsed = ColorText.Parse(textBox.Text, System.Drawing.Color.White);
            if (parsed.A == 0)
            {
                swatch.SetResourceReference(Border.BackgroundProperty, "FieldBgBrush");
                glyph.Visibility = Visibility.Visible;
            }
            else
            {
                swatch.Background = WpfSettingsUi.Freeze(WpfColor.FromRgb(parsed.R, parsed.G, parsed.B));
                glyph.Visibility = Visibility.Collapsed;
            }
        }

        UpdateSwatch();
        textBox.TextChanged += (_, _) => UpdateSwatch();

        Grid.SetColumn(textBox, 0);
        Grid.SetColumn(pickButton, 1);
        grid.Children.Add(textBox);
        grid.Children.Add(pickButton);
        return grid;
    }

    // ==================== Save & Clone ====================

    private void SaveLeaderboardSettings()
    {
        RaceLeaderboardSettings settings = CloneLeaderboardSettings(shell.LeaderboardSettings);
        settings.UseRankColorForMainTimer = useRankColorForMainTimerBox?.IsChecked == true;
        settings.RankPlayerGap = GetSpacing(SpaceRankPlayer, settings.RankPlayerGap);
        settings.PlayerIconGap = GetSpacing(SpacePlayerIcon, settings.PlayerIconGap);
        settings.IconTimeGap = GetSpacing(SpaceIconTime, settings.IconTimeGap);
        settings.RankAlignment = GetAlignment(KeyRank, settings.RankAlignment);
        settings.PlayerAlignment = GetAlignment(KeyPlayer, settings.PlayerAlignment);
        settings.IconAlignment = GetAlignment(KeyIcon, settings.IconAlignment);
        settings.TimeAlignment = GetAlignment(KeyTime, settings.TimeAlignment);

        ApplyColumn(KeyRank, settings.Rank, settings.TextEffects.Rank, settings.Colors.Rank);
        ApplyColumn(KeyPlayer, settings.Player, settings.TextEffects.Player, settings.Colors.Player);
        ApplyColorRow(KeyPlayerSelf, settings.Colors.PlayerSelf);
        ApplyColorRow(KeyPlayerOther, settings.Colors.PlayerOther);
        ApplyColumn(KeyIcon, settings.Icon, settings.TextEffects.Icon, settings.Colors.Icon);
        ApplyColumn(KeyTime, settings.Time, settings.TextEffects.Time, settings.Colors.Time);

        if (rankGradientControls is not null)
        {
            settings.Colors.RankGradient.Start = NormalizeColor(rankGradientControls.Start.Text, settings.Colors.RankGradient.Start);
            settings.Colors.RankGradient.Middle = NormalizeColor(rankGradientControls.Middle.Text, settings.Colors.RankGradient.Middle);
            settings.Colors.RankGradient.End = NormalizeColor(rankGradientControls.End.Text, settings.Colors.RankGradient.End);
        }

        shell.SaveLeaderboardSettings(settings);
    }

    private int GetSpacing(string key, int fallback) =>
        spacingControls.TryGetValue(key, out WpfTextBox? box)
            ? WpfSettingsValueParser.ParseIntBox(box, fallback, 0, 1000)
            : fallback;

    private string GetAlignment(string key, string fallback) =>
        columnControls.TryGetValue(key, out ColumnControls? c) && c.Alignment.SelectedItem is OptionItem opt
            ? UiColumnAlignment.Normalize(opt.Value, fallback)
            : fallback;

    private void ApplyColumn(
        string key,
        UiColumnSettings column,
        RaceLeaderboardColumnEffectSettings effect,
        RaceLeaderboardColumnColorSettings colors)
    {
        if (columnControls.TryGetValue(key, out ColumnControls? c))
        {
            column.Show = c.Show.IsChecked == true;
            column.Width = WpfSettingsValueParser.ParseIntBox(c.Width, column.Width, 1, 1000);
            if (c.FontFamily is not null)
            {
                column.FontFamily = UiFontFactory.Default.NormalizeFamilyName(c.FontFamily.SelectedFontFamily);
            }

            column.FontSize = WpfSettingsValueParser.ParseFloatBox(c.FontSize, column.FontSize, 6f, 96f);
            if (c.Bold is not null)
            {
                column.Bold = c.Bold.IsChecked == true;
            }

            if (c.Italic is not null)
            {
                column.Italic = c.Italic.IsChecked == true;
            }

            effect.OpacityPercent = WpfSettingsValueParser.ParseIntBox(c.Opacity, effect.OpacityPercent, 0, 100);
            effect.ShadowPercent = WpfSettingsValueParser.ParseIntBox(c.Shadow, effect.ShadowPercent, 0, 100);
            effect.OutlineThicknessPercent = WpfSettingsValueParser.ParseIntBox(c.Outline, effect.OutlineThicknessPercent, 0, 100);
        }

        ApplyColorRow(key, colors);
    }

    private void ApplyColorRow(string key, RaceLeaderboardColumnColorSettings colors)
    {
        if (!colorControls.TryGetValue(key, out ColorRowControls? c))
        {
            return;
        }

        if (c.Text is not null)
        {
            colors.Text = NormalizeColor(c.Text.Text, colors.Text);
        }

        colors.Outline = NormalizeColor(c.Outline.Text, colors.Outline);
        colors.Shadow = NormalizeColor(c.Shadow.Text, colors.Shadow);
    }

    private static string NormalizeColor(string text, string fallback) =>
        ColorText.Format(ColorText.Parse(text, ColorText.Parse(fallback, System.Drawing.Color.White)));

    private static RaceLeaderboardSettings CloneLeaderboardSettings(RaceLeaderboardSettings source) => new()
    {
        UseRankColorForMainTimer = source.UseRankColorForMainTimer,
        WindowPositionX = source.WindowPositionX,
        WindowPositionY = source.WindowPositionY,
        RankPlayerGap = source.RankPlayerGap,
        PlayerIconGap = source.PlayerIconGap,
        IconTimeGap = source.IconTimeGap,
        RankAlignment = source.RankAlignment,
        PlayerAlignment = source.PlayerAlignment,
        IconAlignment = source.IconAlignment,
        TimeAlignment = source.TimeAlignment,
        Rank = CloneColumn(source.Rank),
        Player = CloneColumn(source.Player),
        Icon = CloneColumn(source.Icon),
        Time = CloneColumn(source.Time),
        TextEffects = new RaceLeaderboardTextEffectSettings
        {
            Rank = CloneEffect(source.TextEffects?.Rank),
            Player = CloneEffect(source.TextEffects?.Player),
            Icon = CloneEffect(source.TextEffects?.Icon),
            Time = CloneEffect(source.TextEffects?.Time)
        },
        Colors = new RaceLeaderboardColorSettings
        {
            RankGradient = new RaceLeaderboardRankGradientColorSettings
            {
                Start = source.Colors?.RankGradient?.Start ?? "#FFE673",
                Middle = source.Colors?.RankGradient?.Middle ?? "#FFFFFF",
                End = source.Colors?.RankGradient?.End ?? "#FF6B6B"
            },
            Rank = CloneColumnColor(source.Colors?.Rank),
            Player = CloneColumnColor(source.Colors?.Player),
            PlayerSelf = CloneColumnColor(source.Colors?.PlayerSelf ?? source.Colors?.Player),
            PlayerOther = CloneColumnColor(source.Colors?.PlayerOther ?? source.Colors?.Player),
            Icon = CloneColumnColor(source.Colors?.Icon),
            Time = CloneColumnColor(source.Colors?.Time)
        }
    };

    private static UiColumnSettings CloneColumn(UiColumnSettings? s) => s is null
        ? new UiColumnSettings()
        : new UiColumnSettings
        {
            Show = s.Show,
            Width = s.Width,
            FontFamily = s.FontFamily,
            FontSize = s.FontSize,
            Bold = s.Bold,
            Italic = s.Italic
        };

    private static RaceLeaderboardColumnEffectSettings CloneEffect(RaceLeaderboardColumnEffectSettings? s) => s is null
        ? new RaceLeaderboardColumnEffectSettings()
        : new RaceLeaderboardColumnEffectSettings
        {
            OpacityPercent = s.OpacityPercent,
            ShadowPercent = s.ShadowPercent,
            OutlineThicknessPercent = s.OutlineThicknessPercent
        };

    private static RaceLeaderboardColumnColorSettings CloneColumnColor(RaceLeaderboardColumnColorSettings? s) => s is null
        ? new RaceLeaderboardColumnColorSettings()
        : new RaceLeaderboardColumnColorSettings
        {
            Text = s.Text,
            Outline = s.Outline,
            Shadow = s.Shadow
        };

    private string Localize(string key) => shell.Localize(key);

    private sealed record OptionItem(string DisplayName, string Value)
    {
        public override string ToString() => DisplayName;
    }

    private sealed record ColumnControls(
        WpfCheckBox Show,
        WpfTextBox Width,
        WpfComboBox Alignment,
        WpfFontFamilySelector? FontFamily,
        WpfTextBox FontSize,
        WpfCheckBox? Bold,
        WpfCheckBox? Italic,
        WpfTextBox Opacity,
        WpfTextBox Shadow,
        WpfTextBox Outline);

    private sealed record RankGradientControls(WpfTextBox Start, WpfTextBox Middle, WpfTextBox End);

    private sealed record ColorRowControls(WpfTextBox? Text, WpfTextBox Outline, WpfTextBox Shadow);
}
