using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TerrariaSplit.UI.Wpf.Controls;
using TerrariaSplit.UI.Wpf.Interop;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfFontFamily = System.Windows.Media.FontFamily;

namespace TerrariaSplit.UI.Wpf.Windows;

internal sealed partial class StatisticsWindow : ModernWindow
{
    private readonly AppSettings settings;
    private readonly RunStats stats;
    private readonly List<ReferenceSplitSet> referenceTimeSets;
    private readonly List<ReferenceSplitSet> personalBestSets;
    private readonly Dictionary<string, SplitDefinition> definitionsById;
    private int totalConditionRowCount;

    public StatisticsWindow(
        AppSettings settings,
        RunStatsRepository? runStatsRepository = null,
        SplitTimeSetRepository? splitTimeSets = null)
    {
        InitializeComponent();

        splitTimeSets ??= new SplitTimeSetRepository();
        runStatsRepository ??= new RunStatsRepository(splitTimeSets);
        this.settings = settings;
        stats = runStatsRepository.Load();
        definitionsById = WpfIconHelper.BuildDefinitionMap(settings);
        referenceTimeSets = settings.Comparison.UsePersonalBestAsReferenceTime
            ? [ReferenceSplitSetService.CreatePersonalBestReferenceSet(settings)]
            : settings.Comparison.ReferenceSplitSets.ToList();
        personalBestSets = splitTimeSets.LoadLastRunSets();

        RowsScrollViewer.ScrollChanged += (_, _) => SyncHeaderPaddingWithScrollBar();
        RowsScrollViewer.SizeChanged += (_, _) => SyncHeaderPaddingWithScrollBar();

        ApplyLocalizedLabels();
        ShowComparisonToggle.Checked += (_, _) => RefreshTable();
        ShowComparisonToggle.Unchecked += (_, _) => RefreshTable();
        PopulateSelectors();
        RefreshTable();
    }

    internal int RowCount => totalConditionRowCount;

    internal int GroupCount => RowsItemsControl.Items.Count;

    internal int MultiRowGroupCount => RowsItemsControl.Items
        .OfType<StatisticsGroupView>()
        .Count(group => group.IsMultiRowGroup);

    internal string SumOfBestDisplayText => KpiSobValue.Text;

    internal string PersonalBestDisplayText => KpiPbValue.Text;

    internal string SelectedTotalDisplayText => KpiSelectedValue.Text;

    internal string ReferenceTotalDisplayText => KpiReferenceValue.Text;

    internal string ReferenceTotalDeltaDisplayText => KpiReferenceDeltaText.Text;

    private void SyncHeaderPaddingWithScrollBar()
    {
        double scrollBarWidth = RowsScrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible
            ? Math.Max(0d, RowsScrollViewer.ActualWidth - RowsScrollViewer.ViewportWidth)
            : 0d;
        double rightPadding = 12d + scrollBarWidth;
        if (Math.Abs(HeaderRowContainer.Padding.Right - rightPadding) > 0.1)
        {
            HeaderRowContainer.Padding = new Thickness(12, 5, rightPadding, 5);
        }
    }

    private void ApplyLocalizedLabels()
    {
        Title = Localizer.Get("Statistics", settings);
        ReferenceRunLabel.Text = Localizer.Get("Reference run", settings);
        SelectedRunLabel.Text = Localizer.Get("Selected run", settings);
        ShowComparisonToggle.Content = Localizer.Get("Show comparison", settings);

        KpiSelectedLabel.Text = Localizer.Get("Run", settings);
        KpiReferenceLabel.Text = Localizer.Get("Reference", settings);
        KpiPbLabel.Text = Localizer.Get("PB", settings);
        KpiSobLabel.Text = "Sum of Best";

        ColSplitHeader.Text = Localizer.Get("Split", settings);
        SetHeader(ColSelTimeHeader, Localizer.Get("Selected run time column", settings));
        SetHeader(ColRefTimeHeader, Localizer.Get("Reference time column", settings));
        SetHeader(ColPbTimeHeader, Localizer.Get("Personal best time column", settings));
        SetHeader(ColSelSegHeader, Localizer.Get("Selected run segment time column", settings));
        SetHeader(ColRefSegHeader, Localizer.Get("Reference segment time column", settings));
        SetHeader(ColPbSegHeader, Localizer.Get("Personal best segment time column", settings));
    }

    private void UpdateKpiStripLayout(bool hidePb, bool hideSob)
    {
        KpiPbCard.Visibility = hidePb ? Visibility.Collapsed : Visibility.Visible;
        KpiSobCard.Visibility = hideSob ? Visibility.Collapsed : Visibility.Visible;

        if (!hidePb && !hideSob)
        {
            System.Windows.Controls.Grid.SetColumn(KpiSelectedCard, 0);
            System.Windows.Controls.Grid.SetColumnSpan(KpiSelectedCard, 1);
            System.Windows.Controls.Grid.SetColumn(KpiReferenceCard, 2);
            System.Windows.Controls.Grid.SetColumnSpan(KpiReferenceCard, 1);
            System.Windows.Controls.Grid.SetColumn(KpiPbCard, 4);
            System.Windows.Controls.Grid.SetColumnSpan(KpiPbCard, 1);
            System.Windows.Controls.Grid.SetColumn(KpiSobCard, 6);
            System.Windows.Controls.Grid.SetColumnSpan(KpiSobCard, 1);
        }
        else if (hidePb && !hideSob)
        {
            System.Windows.Controls.Grid.SetColumn(KpiSelectedCard, 0);
            System.Windows.Controls.Grid.SetColumnSpan(KpiSelectedCard, 3);
            System.Windows.Controls.Grid.SetColumn(KpiReferenceCard, 4);
            System.Windows.Controls.Grid.SetColumnSpan(KpiReferenceCard, 1);
            System.Windows.Controls.Grid.SetColumn(KpiSobCard, 6);
            System.Windows.Controls.Grid.SetColumnSpan(KpiSobCard, 1);
        }
        else if (!hidePb && hideSob)
        {
            System.Windows.Controls.Grid.SetColumn(KpiSelectedCard, 0);
            System.Windows.Controls.Grid.SetColumnSpan(KpiSelectedCard, 3);
            System.Windows.Controls.Grid.SetColumn(KpiReferenceCard, 4);
            System.Windows.Controls.Grid.SetColumnSpan(KpiReferenceCard, 1);
            System.Windows.Controls.Grid.SetColumn(KpiPbCard, 6);
            System.Windows.Controls.Grid.SetColumnSpan(KpiPbCard, 1);
        }
        else
        {
            System.Windows.Controls.Grid.SetColumn(KpiSelectedCard, 0);
            System.Windows.Controls.Grid.SetColumnSpan(KpiSelectedCard, 3);
            System.Windows.Controls.Grid.SetColumn(KpiReferenceCard, 4);
            System.Windows.Controls.Grid.SetColumnSpan(KpiReferenceCard, 3);
        }
    }

    private static void SetHeader(System.Windows.Controls.TextBlock header, string text)
    {
        header.Text = text;
        header.ToolTip = text;
    }

    private void PopulateSelectors()
    {
        foreach (ReferenceSplitSet set in referenceTimeSets)
        {
            ReferenceRunComboBox.Items.Add(set.Name);
        }

        ReferenceRunComboBox.SelectedItem = ReferenceSplitSetService.GetActiveReferenceSet(settings).Name;
        if (ReferenceRunComboBox.SelectedIndex < 0 && ReferenceRunComboBox.Items.Count > 0)
        {
            ReferenceRunComboBox.SelectedIndex = 0;
        }

        ReferenceRunComboBox.IsEnabled = !settings.Comparison.UsePersonalBestAsReferenceTime;
        ReferenceRunComboBox.SelectionChanged += (_, _) => RefreshTable();

        foreach (ReferenceSplitSet set in personalBestSets)
        {
            SelectedRunComboBox.Items.Add(set.Name);
        }

        if (SelectedRunComboBox.Items.Count > 0)
        {
            SelectedRunComboBox.SelectedIndex = 0;
        }

        SelectedRunComboBox.SelectionChanged += (_, _) => RefreshTable();
    }

    private void RefreshTable()
    {
        ReferenceSplitSet referenceSet = GetSelectedReferenceSet();
        Dictionary<string, string> personalSplits = GetSelectedPersonalSplits();

        List<StatisticsTableRow> rawRows = StatisticsTableBuilder.Build(settings, referenceSet, personalSplits);
        totalConditionRowCount = rawRows.Count;

        WpfBrush warningBrush = ResolveBrush("WarningBrush");
        WpfBrush primaryBrush = ResolveBrush("TextPrimaryBrush");
        WpfBrush mutedBrush = ResolveBrush("TextMutedBrush");
        UiPalette palette = UiPalette.From(settings.Overlay.Colors);
        bool showComparison = ShowComparisonToggle.IsChecked == true;

        bool hidePb = settings.Comparison.UsePersonalBestAsReferenceTime;
        GridLength pbColLength = hidePb ? new GridLength(0) : new GridLength(120);
        Visibility pbColVisibility = hidePb ? Visibility.Collapsed : Visibility.Visible;

        var rawGroups = new List<(List<(string DisplayName, BitmapSource? Icon, string PersonalTime, WpfBrush PersonalTimeFg, string RefTime, string PbTime)> SubRows, string PersonalSeg, WpfBrush PersonalSegFg, FontWeight PersonalSegWeight, string RefSeg, string PbSeg)>();
        var pendingSubRows = new List<(string DisplayName, BitmapSource? Icon, string PersonalTime, WpfBrush PersonalTimeFg, string RefTime, string PbTime)>();
        TimeSpan sumOfBest = TimeSpan.Zero;
        int mainGroupCount = 0;
        int validSobSegments = 0;

        int index = 0;
        while (index < rawRows.Count)
        {
            StatisticsTableRow first = rawRows[index];
            int chunkSize = Math.Max(1, Math.Min(first.GroupRowCount, rawRows.Count - index));
            bool attached = IsAttachedRow(first);

            for (int offset = 0; offset < chunkSize; offset++)
            {
                StatisticsTableRow row = rawRows[index + offset];
                BitmapSource? icon = ResolveRowIcon(row.ConditionRow.SplitId, offset);
                WpfBrush personalTimeBrush = primaryBrush;
                if (showComparison &&
                    TimeText.TryParse(row.PersonalTimeText, out TimeSpan personalTime) &&
                    TimeText.TryParse(row.ReferenceTimeText, out TimeSpan referenceTime))
                {
                    personalTimeBrush = CreateComparisonBrush(personalTime - referenceTime, palette);
                }

                pendingSubRows.Add((
                    row.DisplayName,
                    icon,
                    row.PersonalTimeText,
                    personalTimeBrush,
                    row.ReferenceTimeText,
                    row.PersonalBestText));
            }

            if (!attached)
            {
                mainGroupCount++;
                if (TimeText.TryParse(first.PersonalBestSegmentText, out TimeSpan bestSeg))
                {
                    sumOfBest += bestSeg;
                    validSobSegments++;
                }

                bool isGoldSegment =
                    showComparison &&
                    TimeText.TryParse(first.PersonalSegmentText, out TimeSpan personalSeg) &&
                    TimeText.TryParse(first.PersonalBestSegmentText, out TimeSpan pbSegment) &&
                    personalSeg <= pbSegment;

                rawGroups.Add((
                    pendingSubRows,
                    first.PersonalSegmentText,
                    isGoldSegment ? warningBrush : primaryBrush,
                    isGoldSegment ? FontWeights.SemiBold : FontWeights.Normal,
                    first.ReferenceSegmentText,
                    first.PersonalBestSegmentText));
                pendingSubRows = [];
            }

            index += chunkSize;
        }

        if (pendingSubRows.Count > 0)
        {
            rawGroups.Add((
                pendingSubRows,
                "--",
                mutedBrush,
                FontWeights.Normal,
                "--",
                "--"));
        }

        if (rawGroups.Count == 0)
        {
            totalConditionRowCount = 1;
            rawGroups.Add((
                [(Localizer.Get("No splits", settings), null, "--", primaryBrush, "--", "--")],
                "--",
                mutedBrush,
                FontWeights.Normal,
                "--",
                "--"));
        }

        bool hidePbSegment = validSobSegments == 0;
        GridLength pbSegmentColLength = hidePbSegment ? new GridLength(0) : new GridLength(120);
        Visibility pbSegmentColVisibility = hidePbSegment ? Visibility.Collapsed : Visibility.Visible;
        HeaderPbTimeCol.Width = pbColLength;
        ColPbTimeHeader.Visibility = pbColVisibility;
        HeaderPbSegCol.Width = pbSegmentColLength;
        ColPbSegHeader.Visibility = pbSegmentColVisibility;

        double personalTimeWidth = MeasureAlignedColumnWidth(rawGroups.SelectMany(g => g.SubRows).Select(r => (r.PersonalTime, FontWeights.Normal)), 110d);
        double refTimeWidth = MeasureAlignedColumnWidth(rawGroups.SelectMany(g => g.SubRows).Select(r => (r.RefTime, FontWeights.Normal)), 110d);
        double pbTimeWidth = hidePb ? double.NaN : MeasureAlignedColumnWidth(rawGroups.SelectMany(g => g.SubRows).Select(r => (r.PbTime, FontWeights.Normal)), 120d);
        double personalSegWidth = MeasureAlignedColumnWidth(rawGroups.Select(g => (g.PersonalSeg, g.PersonalSegWeight)), 110d);
        double refSegWidth = MeasureAlignedColumnWidth(rawGroups.Select(g => (g.RefSeg, FontWeights.Normal)), 110d);
        double pbSegWidth = hidePbSegment ? double.NaN : MeasureAlignedColumnWidth(rawGroups.Select(g => (g.PbSeg, FontWeights.Normal)), 120d);

        var groups = new List<StatisticsGroupView>(rawGroups.Count);
        foreach (var group in rawGroups)
        {
            var subViews = new List<StatisticsSubRowView>(group.SubRows.Count);
            for (int i = 0; i < group.SubRows.Count; i++)
            {
                var r = group.SubRows[i];
                Thickness innerDivider = i < group.SubRows.Count - 1
                    ? new Thickness(0, 0, 0, 1)
                    : new Thickness(0);

                subViews.Add(new StatisticsSubRowView(
                    r.DisplayName,
                    r.Icon,
                    r.PersonalTime,
                    r.PersonalTimeFg,
                    personalTimeWidth,
                    ResolveTimeAlignment(r.PersonalTime),
                    r.RefTime,
                    refTimeWidth,
                    ResolveTimeAlignment(r.RefTime),
                    r.PbTime,
                    pbTimeWidth,
                    ResolveTimeAlignment(r.PbTime),
                    pbColLength,
                    pbColVisibility,
                    innerDivider));
            }

            groups.Add(new StatisticsGroupView(
                subViews,
                group.SubRows.Count > 1,
                group.PersonalSeg,
                group.PersonalSegFg,
                group.PersonalSegWeight,
                personalSegWidth,
                ResolveTimeAlignment(group.PersonalSeg),
                group.RefSeg,
                refSegWidth,
                ResolveTimeAlignment(group.RefSeg),
                group.PbSeg,
                pbSegWidth,
                ResolveTimeAlignment(group.PbSeg),
                pbColLength,
                pbSegmentColLength,
                pbSegmentColVisibility));
        }

        RowsItemsControl.ItemsSource = groups;
        UpdateKpiCards(referenceSet, personalSplits, sumOfBest, mainGroupCount, validSobSegments, palette);
        SyncHeaderPaddingWithScrollBar();
    }

    private BitmapSource? ResolveRowIcon(string splitId, int offset)
    {
        if (!definitionsById.TryGetValue(splitId, out SplitDefinition? definition))
        {
            return null;
        }

        int iconIndex = offset < definition.IconFileNames.Count ? offset : 0;
        return WpfIconHelper.TryGetSplitIcon(definition, iconIndex);
    }

    private void UpdateKpiCards(
        ReferenceSplitSet referenceSet,
        IReadOnlyDictionary<string, string> personalSplits,
        TimeSpan sumOfBest,
        int mainGroupCount,
        int validSobSegments,
        UiPalette palette)
    {
        SplitRouteEntry? finalMain = settings.Route.SplitRoute.LastOrDefault(entry => entry.Enabled && !entry.IsAttached);
        bool hasSelected = TryGetFinalMainTime(personalSplits, finalMain, out TimeSpan selectedTotal);
        bool hasReference = TryGetFinalMainTime(referenceSet.Splits, finalMain, out TimeSpan referenceTotal);
        bool hasPb = TryGetFinalMainTime(settings.Comparison.PersonalBestTimes, finalMain, out TimeSpan pbTotal);
        KpiSelectedValue.Text = hasSelected ? TimeText.FormatRecord(selectedTotal) : "--";
        KpiPbValue.Text = hasPb ? TimeText.FormatRecord(pbTotal) : "--";
        KpiReferenceValue.Text = hasReference ? TimeText.FormatRecord(referenceTotal) : "--";
        bool hasCompleteSob = mainGroupCount > 0 && validSobSegments == mainGroupCount;
        KpiSobValue.Text = hasCompleteSob
            ? TimeText.FormatRecord(sumOfBest)
            : "--";

        UpdateKpiStripLayout(
            hidePb: settings.Comparison.UsePersonalBestAsReferenceTime,
            hideSob: !hasCompleteSob);

        TimeSpan? selected = hasSelected ? selectedTotal : null;
        UpdateTotalComparison(KpiReferenceDeltaText, selected, hasReference ? referenceTotal : null, palette);
        UpdateTotalComparison(KpiPbDeltaText, selected, hasPb ? pbTotal : null, palette);
        UpdateTotalComparison(KpiSobDeltaText, selected, hasCompleteSob ? sumOfBest : null, palette);
    }

    private void UpdateTotalComparison(
        System.Windows.Controls.TextBlock label,
        TimeSpan? selectedTotal,
        TimeSpan? comparisonTotal,
        UiPalette palette)
    {
        if (ShowComparisonToggle.IsChecked != true || selectedTotal is null || comparisonTotal is null)
        {
            label.Text = string.Empty;
            label.Visibility = Visibility.Collapsed;
            return;
        }

        TimeSpan delta = selectedTotal.Value - comparisonTotal.Value;
        label.Text = FormatDelta(delta);
        label.Foreground = CreateComparisonBrush(delta, palette);
        label.Visibility = Visibility.Visible;
    }

    private WpfBrush CreateComparisonBrush(TimeSpan delta, UiPalette palette)
    {
        System.Drawing.Color color = OverlayColorMath.GetDeltaComparisonColor(
            settings,
            new SplitComparison(delta, ShowDelta: true),
            palette,
            settings.Overlay.EnableDeltaGradientColor);
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    private bool TryGetFinalMainTime(IReadOnlyDictionary<string, string> values, SplitRouteEntry? finalMain, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (finalMain is null)
        {
            return false;
        }

        // Last-run archives may contain the split-level time instead of condition rows.
        return SplitConditionDataRows.TryGetSplitTime(settings, values, finalMain.Id, out time) ||
            values.TryGetValue(finalMain.Id, out string? text) && TimeText.TryParse(text, out time);
    }

    private bool IsAttachedRow(StatisticsTableRow row)
    {
        SplitRouteEntry? entry = settings.Route.SplitRoute.FirstOrDefault(
            item => string.Equals(item.Id, row.ConditionRow.SplitId, StringComparison.OrdinalIgnoreCase));
        return entry?.IsAttached == true;
    }

    private ReferenceSplitSet GetSelectedReferenceSet()
    {
        if (ReferenceRunComboBox.SelectedItem is string selectedName)
        {
            ReferenceSplitSet? selected = referenceTimeSets.FirstOrDefault(
                timeSet => string.Equals(timeSet.Name, selectedName, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                return selected;
            }
        }

        return ReferenceSplitSetService.GetActiveReferenceSet(settings);
    }

    private Dictionary<string, string> GetSelectedPersonalSplits()
    {
        if (SelectedRunComboBox.SelectedItem is string selectedName)
        {
            ReferenceSplitSet? selected = personalBestSets.FirstOrDefault(
                timeSet => string.Equals(timeSet.Name, selectedName, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                return selected.Splits;
            }
        }

        return stats.LastRunSplits;
    }

    private WpfBrush ResolveBrush(string resourceKey)
    {
        return TryFindResource(resourceKey) as WpfBrush ?? WpfBrushes.White;
    }

    private static string FormatDelta(TimeSpan delta)
    {
        string sign = delta < TimeSpan.Zero ? "-" : "+";
        TimeSpan abs = delta.Duration();
        if (abs >= TimeSpan.FromMinutes(1))
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}{1}:{2:00}.{3:00}",
                sign,
                abs.Ticks / TimeSpan.TicksPerMinute,
                abs.Seconds,
                abs.Milliseconds / 10);
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}{1}.{2:00}",
            sign,
            abs.Seconds,
            abs.Milliseconds / 10);
    }

    private double MeasureAlignedColumnWidth(IEnumerable<(string Text, FontWeight Weight)> values, double columnWidth)
    {
        WpfFontFamily fontFamily = TryFindResource("MonoFontFamily") as WpfFontFamily ?? new WpfFontFamily("Consolas");
        double fontSize = TryFindResource("FontSizeBody") is double size && size > 0 ? size : 13d;
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if (pixelsPerDip <= 0)
        {
            pixelsPerDip = 1d;
        }

        double maxWidth = 0d;
        foreach ((string text, FontWeight weight) in values)
        {
            if (string.IsNullOrWhiteSpace(text) || string.Equals(text, "--", StringComparison.Ordinal))
            {
                continue;
            }

            var typeface = new Typeface(fontFamily, FontStyles.Normal, weight, FontStretches.Normal);
            var formatted = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                typeface,
                fontSize,
                WpfBrushes.White,
                pixelsPerDip);
            if (formatted.WidthIncludingTrailingWhitespace > maxWidth)
            {
                maxWidth = formatted.WidthIncludingTrailingWhitespace;
            }
        }

        return maxWidth > 0d
            ? Math.Min(columnWidth - 6d, Math.Ceiling(maxWidth) + 2d)
            : double.NaN;
    }

    private static TextAlignment ResolveTimeAlignment(string text) =>
        string.IsNullOrWhiteSpace(text) || string.Equals(text, "--", StringComparison.Ordinal)
            ? TextAlignment.Center
            : TextAlignment.Right;

    internal sealed record StatisticsSubRowView(
        string DisplayName,
        BitmapSource? Icon,
        string PersonalTimeText,
        WpfBrush PersonalTimeForeground,
        double PersonalTimeWidth,
        TextAlignment PersonalTimeAlignment,
        string ReferenceTimeText,
        double ReferenceTimeWidth,
        TextAlignment ReferenceTimeAlignment,
        string PersonalBestText,
        double PersonalBestWidth,
        TextAlignment PersonalBestAlignment,
        GridLength PbColumnLength,
        Visibility PbColumnVisibility,
        Thickness InnerDividerThickness);

    internal sealed record StatisticsGroupView(
        IReadOnlyList<StatisticsSubRowView> SubRows,
        bool IsMultiRowGroup,
        string PersonalSegmentText,
        WpfBrush PersonalSegmentForeground,
        FontWeight PersonalSegmentFontWeight,
        double PersonalSegmentWidth,
        TextAlignment PersonalSegmentAlignment,
        string ReferenceSegmentText,
        double ReferenceSegmentWidth,
        TextAlignment ReferenceSegmentAlignment,
        string PersonalBestSegmentText,
        double PersonalBestSegmentWidth,
        TextAlignment PersonalBestSegmentAlignment,
        GridLength PbTimeColumnLength,
        GridLength PbSegmentColumnLength,
        Visibility PbSegmentColumnVisibility);
}
