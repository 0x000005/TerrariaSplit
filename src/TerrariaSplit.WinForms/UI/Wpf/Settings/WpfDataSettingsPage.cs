using System.Windows;
using System.Windows.Controls;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace TerrariaSplit.UI.Wpf.Settings;

internal sealed class WpfDataSettingsPage : WpfSettingsPageBase
{
    private readonly SplitTimeSetRepository splitTimeSets;
    private readonly Dictionary<string, WpfTextBox> splitTextBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WpfTextBox> personalBestTimeTextBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WpfTextBox> personalBestSegmentTextBoxes = new(StringComparer.OrdinalIgnoreCase);

    private WpfComboBox referenceSetBox = null!;
    private WpfTextBox newReferenceSetNameBox = null!;
    private WpfCheckBox usePersonalBestAsReferenceTimeBox = null!;
    private WpfComboBox personalBestTimeSetBox = null!;
    private WpfComboBox personalBestSegmentSetBox = null!;
    private WpfCheckBox autoUpdatePersonalBestDataBox = null!;
    private WpfCheckBox askBeforeUpdatingPersonalBestDataBox = null!;
    private WpfButton addReferenceSetButton = null!;

    private StackPanel? referenceDataList;
    private StackPanel? personalBestTimeList;
    private StackPanel? personalBestSegmentList;
    private bool updatingReferenceSetSelection;
    private bool referenceSetsLoadedForEditing;
    private string? referenceDataSignature;
    private string? personalBestTimeGridSignature;
    private string? personalBestSegmentGridSignature;

    public WpfDataSettingsPage()
        : this(new SplitTimeSetRepository())
    {
    }

    internal WpfDataSettingsPage(SplitTimeSetRepository splitTimeSets)
    {
        this.splitTimeSets = splitTimeSets;
    }

    public override SettingsPageId Id => SettingsPageId.Data;

    internal WpfCheckBox UsePersonalBestAsReferenceTimeBox => usePersonalBestAsReferenceTimeBox;

    internal WpfComboBox ReferenceSetBox => referenceSetBox;

    internal WpfTextBox NewReferenceSetNameBox => newReferenceSetNameBox;

    internal IReadOnlyDictionary<string, WpfTextBox> SplitTextBoxes => splitTextBoxes;

    internal WpfCheckBox AutoUpdatePersonalBestDataBoxForTests => autoUpdatePersonalBestDataBox;

    internal WpfCheckBox AskBeforeUpdatingPersonalBestDataBoxForTests => askBeforeUpdatingPersonalBestDataBox;

    protected override FrameworkElement BuildPage(WpfSettingsPageContext context)
    {
        referenceSetsLoadedForEditing = !Draft.Comparison.UsePersonalBestAsReferenceTime;
        return WpfSettingsUi.CreateScrollPage(content =>
        {
            AddReferenceDataSection(content);
            AddPersonalBestDataSection(content);
        });
    }

    public override void Apply(AppSettings settings)
    {
        settings.Comparison.UsePersonalBestAsReferenceTime = usePersonalBestAsReferenceTimeBox.IsChecked == true;
        settings.Comparison.AutoUpdatePersonalBestData = autoUpdatePersonalBestDataBox.IsChecked == true;
        settings.Comparison.AskBeforeUpdatingPersonalBestData = askBeforeUpdatingPersonalBestDataBox.IsChecked == true;
        if (!settings.Comparison.UsePersonalBestAsReferenceTime)
        {
            EnsureReferenceSetsLoadedForEditing();
            SaveReferenceTextBoxes();
        }

        SavePersonalBestTextBoxes();

        if (!settings.Comparison.UsePersonalBestAsReferenceTime)
        {
            settings.Comparison.ActiveReferenceSplitSet = referenceSetBox.SelectedItem is string selectedReferenceSet
                ? selectedReferenceSet
                : ReferenceSplitSetService.GetActiveReferenceSet(settings).Name;
        }

        settings.Comparison.ActivePersonalBestTimeSet = personalBestTimeSetBox.SelectedItem is string selectedPersonalBestTimeSet
            ? selectedPersonalBestTimeSet
            : PersonalBestSetService.GetActivePersonalBestTimeSet(settings).Name;
        settings.Comparison.ActivePersonalBestSegmentSet = personalBestSegmentSetBox.SelectedItem is string selectedPersonalBestSegmentSet
            ? selectedPersonalBestSegmentSet
            : PersonalBestSetService.GetActivePersonalBestSegmentSet(settings).Name;
    }

    public override void OnModelChanged(SettingsModelChange change)
    {
        if (change != SettingsModelChange.RouteChanged)
        {
            return;
        }

        PopulateReferenceDataGrid();
        PopulatePersonalBestTimeGrid();
        PopulatePersonalBestSegmentGrid();
    }

    private void AddReferenceDataSection(StackPanel parent)
    {
        var card = WpfSettingsUi.CreateCard(Localize("Reference Data"), out StackPanel body);

        usePersonalBestAsReferenceTimeBox = WpfSettingsUi.CreateToggleSwitch(Draft.Comparison.UsePersonalBestAsReferenceTime);
        usePersonalBestAsReferenceTimeBox.Checked += (_, _) => ToggleUsePersonalBestAsReferenceTime();
        usePersonalBestAsReferenceTimeBox.Unchecked += (_, _) => ToggleUsePersonalBestAsReferenceTime();
        WpfSettingsUi.AddSettingRow(body, Localize("Use PB as reference time"), usePersonalBestAsReferenceTimeBox);

        referenceSetBox = WpfSettingsUi.CreateComboBox(180);
        PopulateReferenceSetBox();
        referenceSetBox.SelectionChanged += (_, _) => SwitchReferenceSet();

        newReferenceSetNameBox = WpfSettingsUi.CreateTextBox(string.Empty, placeholder: Localize("new group name"), width: 164);
        newReferenceSetNameBox.Margin = new Thickness(8, 0, 6, 0);

        addReferenceSetButton = WpfSettingsUi.CreateButton(Localize("Add"), minWidth: 68);
        addReferenceSetButton.Click += (_, _) => AddReferenceSet();

        var groupControls = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        groupControls.Children.Add(referenceSetBox);
        groupControls.Children.Add(newReferenceSetNameBox);
        groupControls.Children.Add(addReferenceSetButton);
        WpfSettingsUi.AddSettingRow(body, Localize("Active group"), groupControls);

        referenceDataList = new StackPanel { Orientation = WpfOrientation.Vertical };
        body.Children.Add(referenceDataList);
        PopulateReferenceDataGrid();

        parent.Children.Add(card);
    }

    private void AddPersonalBestDataSection(StackPanel parent)
    {
        autoUpdatePersonalBestDataBox = WpfSettingsUi.CreateToggleSwitch(Draft.Comparison.AutoUpdatePersonalBestData);
        askBeforeUpdatingPersonalBestDataBox = WpfSettingsUi.CreateToggleSwitch(Draft.Comparison.AskBeforeUpdatingPersonalBestData);
        autoUpdatePersonalBestDataBox.Checked += (_, _) => RefreshPersonalBestUpdateOptions();
        autoUpdatePersonalBestDataBox.Unchecked += (_, _) => RefreshPersonalBestUpdateOptions();
        RefreshPersonalBestUpdateOptions();

        var autoUpdateCard = WpfSettingsUi.CreateCard(Localize("Auto update personal data"), out StackPanel autoBody);
        WpfSettingsUi.AddSettingRow(autoBody, Localize("Enabled"), autoUpdatePersonalBestDataBox);
        WpfSettingsUi.AddSettingRow(autoBody, Localize("Ask before updating personal data"), askBeforeUpdatingPersonalBestDataBox, isLast: true);
        parent.Children.Add(autoUpdateCard);

        // Personal Cumulative Best
        var pbCard = WpfSettingsUi.CreateCard(Localize("Personal Cumulative Best"), out StackPanel pbBody);
        personalBestTimeSetBox = CreatePersonalSetBox(
            Draft.Comparison.PersonalBestTimeSets,
            PersonalBestSetService.GetActivePersonalBestTimeSet(Draft).Name,
            SwitchPersonalBestTimeSet);
        WpfSettingsUi.AddSettingRow(pbBody, Localize("Active file"), personalBestTimeSetBox);

        personalBestTimeList = new StackPanel { Orientation = WpfOrientation.Vertical };
        pbBody.Children.Add(personalBestTimeList);
        PopulatePersonalBestTimeGrid();
        parent.Children.Add(pbCard);

        // Personal segment best
        var segCard = WpfSettingsUi.CreateCard(Localize("Personal segment best"), out StackPanel segBody);
        personalBestSegmentSetBox = CreatePersonalSetBox(
            Draft.Comparison.PersonalBestSegmentSets,
            PersonalBestSetService.GetActivePersonalBestSegmentSet(Draft).Name,
            SwitchPersonalBestSegmentSet);
        WpfSettingsUi.AddSettingRow(segBody, Localize("Active file"), personalBestSegmentSetBox);

        personalBestSegmentList = new StackPanel { Orientation = WpfOrientation.Vertical };
        segBody.Children.Add(personalBestSegmentList);
        PopulatePersonalBestSegmentGrid();
        parent.Children.Add(segCard);
    }

    private void RefreshPersonalBestUpdateOptions()
    {
        askBeforeUpdatingPersonalBestDataBox.IsEnabled = autoUpdatePersonalBestDataBox.IsChecked == true;
    }

    private void PopulateReferenceDataGrid()
    {
        if (referenceDataList is null)
        {
            return;
        }

        List<SplitConditionDataRow> rows = SplitConditionDataRows.Build(Draft).ToList();
        string signature = string.Join('\u001F', rows.Select(row => row.Key));
        if (referenceDataSignature == signature && splitTextBoxes.Count > 0)
        {
            LoadReferenceTextBoxes();
            RefreshReferenceDataEditState();
            return;
        }

        referenceDataList.Children.Clear();
        splitTextBoxes.Clear();
        string placeholder = Localize("m:ss or h:mm:ss");
        for (int i = 0; i < rows.Count; i++)
        {
            SplitConditionDataRow row = rows[i];
            WpfTextBox box = WpfSettingsUi.CreateTimeBox(
                GetDisplayedReferenceTimeText(row.Key),
                placeholder: placeholder);
            splitTextBoxes[row.Key] = box;
            WpfSettingsUi.AddSettingRow(referenceDataList, row.DisplayName, box, isLast: i == rows.Count - 1);
        }

        referenceDataSignature = signature;
        RefreshReferenceDataEditState();
    }

    private void PopulatePersonalBestTimeGrid()
    {
        if (personalBestTimeList is null)
        {
            return;
        }

        List<SplitConditionDataRow> rows = SplitConditionDataRows.Build(Draft).ToList();
        string signature = string.Join('\u001F', rows.Select(row => row.Key));
        if (personalBestTimeGridSignature == signature && personalBestTimeTextBoxes.Count > 0)
        {
            foreach (SplitConditionDataRow row in rows)
            {
                if (personalBestTimeTextBoxes.TryGetValue(row.Key, out WpfTextBox? textBox))
                {
                    textBox.Text = PersonalBestSetService.GetPersonalBestTimeText(Draft, row.Key);
                }
            }

            return;
        }

        personalBestTimeList.Children.Clear();
        personalBestTimeTextBoxes.Clear();
        string placeholder = Localize("m:ss or h:mm:ss");
        for (int i = 0; i < rows.Count; i++)
        {
            SplitConditionDataRow row = rows[i];
            WpfTextBox box = WpfSettingsUi.CreateTimeBox(
                PersonalBestSetService.GetPersonalBestTimeText(Draft, row.Key),
                placeholder: placeholder);
            box.TextChanged += (_, _) => RefreshReferenceDataFromPersonalBest();
            personalBestTimeTextBoxes[row.Key] = box;
            WpfSettingsUi.AddSettingRow(personalBestTimeList, row.DisplayName, box, isLast: i == rows.Count - 1);
        }

        personalBestTimeGridSignature = signature;
    }

    private void PopulatePersonalBestSegmentGrid()
    {
        if (personalBestSegmentList is null)
        {
            return;
        }

        List<RouteGroup> groups = SplitRouteGroups.Build(Draft).ToList();
        string signature = string.Join('\u001F', groups.Select(group => group.Key));
        if (personalBestSegmentGridSignature == signature && personalBestSegmentTextBoxes.Count > 0)
        {
            foreach (RouteGroup group in groups)
            {
                if (personalBestSegmentTextBoxes.TryGetValue(group.Key, out WpfTextBox? textBox))
                {
                    textBox.Text = PersonalBestSetService.GetPersonalBestSegmentText(Draft, group.Key);
                }
            }

            return;
        }

        personalBestSegmentList.Children.Clear();
        personalBestSegmentTextBoxes.Clear();
        string placeholder = Localize("m:ss or h:mm:ss");
        for (int i = 0; i < groups.Count; i++)
        {
            RouteGroup group = groups[i];
            WpfTextBox box = WpfSettingsUi.CreateTimeBox(
                PersonalBestSetService.GetPersonalBestSegmentText(Draft, group.Key),
                placeholder: placeholder);
            personalBestSegmentTextBoxes[group.Key] = box;
            WpfSettingsUi.AddSettingRow(
                personalBestSegmentList,
                string.IsNullOrWhiteSpace(group.DisplayName) ? group.Key : group.DisplayName,
                box,
                isLast: i == groups.Count - 1);
        }

        personalBestSegmentGridSignature = signature;
    }

    private void PopulateReferenceSetBox()
    {
        updatingReferenceSetSelection = true;
        referenceSetBox.Items.Clear();

        foreach (ReferenceSplitSet set in Draft.Comparison.ReferenceSplitSets)
        {
            referenceSetBox.Items.Add(set.Name);
        }

        referenceSetBox.SelectedItem = Draft.Comparison.ActiveReferenceSplitSet;
        if (referenceSetBox.SelectedIndex < 0 && referenceSetBox.Items.Count > 0)
        {
            referenceSetBox.SelectedIndex = 0;
        }

        updatingReferenceSetSelection = false;
    }

    private static WpfComboBox CreatePersonalSetBox(
        IEnumerable<ReferenceSplitSet> sets,
        string activeName,
        SelectionChangedEventHandler selectionChanged)
    {
        WpfComboBox comboBox = WpfSettingsUi.CreateComboBox(WpfSettingsUi.StandardFieldWidth);
        foreach (ReferenceSplitSet set in sets)
        {
            comboBox.Items.Add(set.Name);
        }

        comboBox.SelectedItem = activeName;
        comboBox.SelectionChanged += selectionChanged;
        return comboBox;
    }

    private void SwitchReferenceSet()
    {
        if (updatingReferenceSetSelection || Draft.Comparison.UsePersonalBestAsReferenceTime)
        {
            return;
        }

        SaveReferenceTextBoxes();
        if (referenceSetBox.SelectedItem is string selectedName)
        {
            Draft.Comparison.ActiveReferenceSplitSet = selectedName;
        }

        LoadReferenceTextBoxes();
    }

    private void SwitchPersonalBestTimeSet(object sender, SelectionChangedEventArgs e)
    {
        SavePersonalBestTimeTextBoxes();
        if (personalBestTimeSetBox.SelectedItem is string selectedName)
        {
            Draft.Comparison.ActivePersonalBestTimeSet = selectedName;
            PersonalBestSetService.SyncPersonalBestTimesFromActiveSet(Draft);
        }

        LoadPersonalBestTimeTextBoxes();
        Context.NotifyModelChanged(SettingsModelChange.PersonalBestTimeChanged);
    }

    private void SwitchPersonalBestSegmentSet(object sender, SelectionChangedEventArgs e)
    {
        SavePersonalBestSegmentTextBoxes();
        if (personalBestSegmentSetBox.SelectedItem is string selectedName)
        {
            Draft.Comparison.ActivePersonalBestSegmentSet = selectedName;
            PersonalBestSetService.SyncPersonalBestSegmentsFromActiveSet(Draft);
        }

        LoadPersonalBestSegmentTextBoxes();
        Context.NotifyModelChanged(SettingsModelChange.PersonalBestSegmentChanged);
    }

    private void AddReferenceSet()
    {
        if (Draft.Comparison.UsePersonalBestAsReferenceTime)
        {
            return;
        }

        SaveReferenceTextBoxes();
        string name = newReferenceSetNameBox.Text.Trim();
        if (name.Length == 0 ||
            Draft.Comparison.ReferenceSplitSets.Any(set => string.Equals(set.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Draft.Comparison.ReferenceSplitSets.Add(ReferenceSplitSetService.CreateReferenceSet(
            name,
            keys: SplitConditionDataRows.Build(Draft).Select(row => row.Key)));
        referenceSetBox.Items.Add(name);
        referenceSetBox.SelectedItem = name;
        newReferenceSetNameBox.Clear();
    }

    private void SaveReferenceTextBoxes()
    {
        if (Draft.Comparison.UsePersonalBestAsReferenceTime)
        {
            return;
        }

        ReferenceSplitSet activeSet = ReferenceSplitSetService.GetActiveReferenceSet(Draft);
        foreach ((string name, WpfTextBox textBox) in splitTextBoxes)
        {
            string text = textBox.Text.Trim();
            activeSet.Splits[name] = TimeText.TryParse(text, out TimeSpan parsed)
                ? TimeText.FormatRecord(parsed)
                : text;
        }
    }

    private void SavePersonalBestTextBoxes()
    {
        SavePersonalBestTimeTextBoxes();
        SavePersonalBestSegmentTextBoxes();
    }

    private void SavePersonalBestTimeTextBoxes()
    {
        foreach ((string name, WpfTextBox textBox) in personalBestTimeTextBoxes)
        {
            PersonalBestSetService.SetPersonalBestTimeText(Draft, name, NormalizeTimeText(textBox.Text));
        }

        PersonalBestSetService.SyncActivePersonalBestTimeSetFromDictionary(Draft);
    }

    private void SavePersonalBestSegmentTextBoxes()
    {
        foreach ((string name, WpfTextBox textBox) in personalBestSegmentTextBoxes)
        {
            PersonalBestSetService.SetPersonalBestSegmentText(Draft, name, NormalizeTimeText(textBox.Text));
        }

        PersonalBestSetService.SyncActivePersonalBestSegmentSetFromDictionary(Draft);
    }

    private static string NormalizeTimeText(string text)
    {
        string trimmed = text.Trim();
        return TimeText.TryParse(trimmed, out TimeSpan parsed)
            ? TimeText.FormatRecord(parsed)
            : trimmed;
    }

    private void LoadReferenceTextBoxes()
    {
        foreach ((string name, WpfTextBox textBox) in splitTextBoxes)
        {
            textBox.Text = GetDisplayedReferenceTimeText(name);
        }
    }

    private void LoadPersonalBestTimeTextBoxes()
    {
        foreach ((string name, WpfTextBox textBox) in personalBestTimeTextBoxes)
        {
            textBox.Text = PersonalBestSetService.GetPersonalBestTimeText(Draft, name);
        }
    }

    private void LoadPersonalBestSegmentTextBoxes()
    {
        foreach ((string name, WpfTextBox textBox) in personalBestSegmentTextBoxes)
        {
            textBox.Text = PersonalBestSetService.GetPersonalBestSegmentText(Draft, name);
        }
    }

    private void ToggleUsePersonalBestAsReferenceTime()
    {
        bool usePersonalBest = usePersonalBestAsReferenceTimeBox.IsChecked == true;
        if (!Draft.Comparison.UsePersonalBestAsReferenceTime && usePersonalBest)
        {
            SaveReferenceTextBoxes();
        }

        Draft.Comparison.UsePersonalBestAsReferenceTime = usePersonalBest;
        if (!usePersonalBest)
        {
            EnsureReferenceSetsLoadedForEditing();
        }

        RefreshReferenceDataEditState();
        Context.NotifyModelChanged(SettingsModelChange.ReferenceModeChanged);
    }

    private void EnsureReferenceSetsLoadedForEditing()
    {
        if (referenceSetsLoadedForEditing)
        {
            return;
        }

        Draft.Comparison.ReferenceSplitSets = splitTimeSets.LoadReferenceSets();
        referenceSetsLoadedForEditing = true;
        SettingsNormalizer.Normalize(Draft);
        PopulateReferenceSetBox();
    }

    private void RefreshReferenceDataEditState()
    {
        bool usePersonalBest = usePersonalBestAsReferenceTimeBox.IsChecked == true;
        referenceSetBox.IsEnabled = !usePersonalBest;
        newReferenceSetNameBox.IsEnabled = !usePersonalBest;
        addReferenceSetButton.IsEnabled = !usePersonalBest;

        foreach (WpfTextBox textBox in splitTextBoxes.Values)
        {
            textBox.IsReadOnly = usePersonalBest;
            textBox.IsTabStop = !usePersonalBest;
        }

        LoadReferenceTextBoxes();
    }

    private void RefreshReferenceDataFromPersonalBest()
    {
        if (usePersonalBestAsReferenceTimeBox.IsChecked == true)
        {
            LoadReferenceTextBoxes();
        }
    }

    private string GetDisplayedReferenceTimeText(string name)
    {
        if (usePersonalBestAsReferenceTimeBox.IsChecked != true)
        {
            return ReferenceSplitSetService.GetReferenceText(Draft, name);
        }

        return personalBestTimeTextBoxes.TryGetValue(name, out WpfTextBox? personalBestTextBox)
            ? personalBestTextBox.Text
            : PersonalBestSetService.GetPersonalBestTimeText(Draft, name);
    }
}
