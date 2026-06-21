using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class DataSettingsPage : SettingsPageBase
{
    private readonly ThemedDropDownList referenceSetBox = new();
    private readonly TextBox newReferenceSetNameBox = new();
    private readonly CheckBox usePersonalBestAsReferenceTimeBox = new();
    private readonly ThemedDropDownList personalBestTimeSetBox = new();
    private readonly ThemedDropDownList personalBestSegmentSetBox = new();
    private readonly CheckBox autoUpdatePersonalBestDataBox = new();
    private readonly CheckBox askBeforeUpdatingPersonalBestDataBox = new();
    private readonly Dictionary<string, TextBox> splitTextBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> personalBestTimeTextBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> personalBestSegmentTextBoxes = new(StringComparer.OrdinalIgnoreCase);

    private Button? addReferenceSetButton;
    private TableLayoutPanel? referenceDataGrid;
    private TableLayoutPanel? personalBestTimeGrid;
    private TableLayoutPanel? personalBestSegmentGrid;
    private bool updatingReferenceSetSelection;
    private bool referenceSetsLoadedForEditing;
    private string? referenceDataSignature;
    private string? personalBestTimeGridSignature;
    private string? personalBestSegmentGridSignature;

    public override SettingsPageId Id => SettingsPageId.Data;

    internal CheckBox UsePersonalBestAsReferenceTimeBox => usePersonalBestAsReferenceTimeBox;

    internal ThemedDropDownList ReferenceSetBox => referenceSetBox;

    internal TextBox NewReferenceSetNameBox => newReferenceSetNameBox;

    internal IReadOnlyDictionary<string, TextBox> SplitTextBoxes => splitTextBoxes;

    protected override Control BuildPage(SettingsPageContext context)
    {
        referenceSetsLoadedForEditing = !Draft.Comparison.UsePersonalBestAsReferenceTime;
        return context.BuildScrollPage(content =>
        {
            AddReferenceDataSection(content);
            AddPersonalBestDataSection(content);
        });
    }

    public override void Apply(AppSettings settings)
    {
        settings.Comparison.UsePersonalBestAsReferenceTime = usePersonalBestAsReferenceTimeBox.Checked;
        settings.Comparison.AutoUpdatePersonalBestData = autoUpdatePersonalBestDataBox.Checked;
        settings.Comparison.AskBeforeUpdatingPersonalBestData = askBeforeUpdatingPersonalBestDataBox.Checked;
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
                : settings.GetActiveReferenceSet().Name;
        }

        settings.Comparison.ActivePersonalBestTimeSet = personalBestTimeSetBox.SelectedItem is string selectedPersonalBestTimeSet
            ? selectedPersonalBestTimeSet
            : settings.GetActivePersonalBestTimeSet().Name;
        settings.Comparison.ActivePersonalBestSegmentSet = personalBestSegmentSetBox.SelectedItem is string selectedPersonalBestSegmentSet
            ? selectedPersonalBestSegmentSet
            : settings.GetActivePersonalBestSegmentSet().Name;
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

    private void AddReferenceDataSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = Factory.CreateSection("Reference Data");

        ConfigureCheckBox(usePersonalBestAsReferenceTimeBox, Draft.Comparison.UsePersonalBestAsReferenceTime);
        usePersonalBestAsReferenceTimeBox.CheckedChanged += (_, _) => ToggleUsePersonalBestAsReferenceTime();

        TableLayoutPanel modeGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(modeGrid, "Use PB as reference time", usePersonalBestAsReferenceTimeBox);
        SettingsUiFactory.AddSectionControl(section, modeGrid);

        ConfigureReferenceSetBox();
        newReferenceSetNameBox.PlaceholderText = Context.Localize("new group name");
        newReferenceSetNameBox.Dock = DockStyle.Fill;
        UiTheme.StyleTextBox(newReferenceSetNameBox);

        addReferenceSetButton = Factory.CreateButton("Add", accent: false, minimumWidth: 120);
        addReferenceSetButton.Click += (_, _) => AddReferenceSet();

        TableLayoutPanel selectorGrid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStyleAbsolute(220f),
            SettingsUiFactory.ColumnStyleAbsolute(260f),
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(240f),
            SettingsUiFactory.ColumnStyleAbsolute(136f));
        int selectorRow = Factory.AddGridRow(selectorGrid);
        selectorGrid.Controls.Add(Factory.CreateRowLabel("Active group"), 0, selectorRow);
        selectorGrid.Controls.Add(referenceSetBox, 1, selectorRow);
        selectorGrid.Controls.Add(newReferenceSetNameBox, 3, selectorRow);
        selectorGrid.Controls.Add(Factory.CreateButtonPanel(addReferenceSetButton), 4, selectorRow);
        SettingsUiFactory.AddSectionControl(section, selectorGrid);

        referenceDataGrid = Factory.CreateTwoColumnGrid(280f);
        PopulateReferenceDataGrid();
        SettingsUiFactory.AddSectionControl(section, referenceDataGrid);
        SettingsUiFactory.AddSection(parent, section);
    }

    private void AddPersonalBestDataSection(TableLayoutPanel parent)
    {
        ConfigureCheckBox(autoUpdatePersonalBestDataBox, Draft.Comparison.AutoUpdatePersonalBestData);
        ConfigureCheckBox(askBeforeUpdatingPersonalBestDataBox, Draft.Comparison.AskBeforeUpdatingPersonalBestData);
        TableLayoutPanel autoUpdateSection = Factory.CreateSection("Personal Data");
        TableLayoutPanel autoUpdateGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(autoUpdateGrid, "Auto update personal data", autoUpdatePersonalBestDataBox);
        Factory.AddSettingRow(autoUpdateGrid, "Ask before updating personal data", askBeforeUpdatingPersonalBestDataBox);
        SettingsUiFactory.AddSectionControl(autoUpdateSection, autoUpdateGrid);
        SettingsUiFactory.AddSection(parent, autoUpdateSection);

        TableLayoutPanel personalBestSection = Factory.CreateSection("Personal Cumulative Best");
        ConfigurePersonalBestTimeSetBox();
        TableLayoutPanel personalBestSelectorGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(personalBestSelectorGrid, "Active file", personalBestTimeSetBox);
        SettingsUiFactory.AddSectionControl(personalBestSection, personalBestSelectorGrid);
        personalBestTimeGrid = Factory.CreateTwoColumnGrid(280f);
        PopulatePersonalBestTimeGrid();
        SettingsUiFactory.AddSectionControl(personalBestSection, personalBestTimeGrid);
        SettingsUiFactory.AddSection(parent, personalBestSection);

        TableLayoutPanel personalBestSegmentSection = Factory.CreateSection("Personal segment best");
        ConfigurePersonalBestSegmentSetBox();
        TableLayoutPanel segmentSelectorGrid = Factory.CreateTwoColumnGrid(280f);
        Factory.AddSettingRow(segmentSelectorGrid, "Active file", personalBestSegmentSetBox);
        SettingsUiFactory.AddSectionControl(personalBestSegmentSection, segmentSelectorGrid);
        personalBestSegmentGrid = Factory.CreateTwoColumnGrid(280f);
        PopulatePersonalBestSegmentGrid();
        SettingsUiFactory.AddSectionControl(personalBestSegmentSection, personalBestSegmentGrid);
        SettingsUiFactory.AddSection(parent, personalBestSegmentSection);
    }

    private void PopulateReferenceDataGrid()
    {
        if (referenceDataGrid is null)
        {
            return;
        }

        List<SplitConditionDataRow> rows = GetCumulativeRows().ToList();
        string signature = string.Join('\u001F', rows.Select(row => row.Key));
        if (referenceDataSignature == signature && splitTextBoxes.Count > 0)
        {
            LoadReferenceTextBoxes();
            RefreshReferenceDataEditState();
            return;
        }

        referenceDataGrid.SuspendLayout();
        try
        {
            SettingsUiFactory.ClearGrid(referenceDataGrid);
            splitTextBoxes.Clear();
            foreach (SplitConditionDataRow row in rows)
            {
                TextBox textBox = Factory.CreateTextBox(GetDisplayedReferenceTimeText(row.Key));
                textBox.PlaceholderText = Context.Localize("m:ss or h:mm:ss");
                splitTextBoxes[row.Key] = textBox;
                Factory.AddRawSettingRow(referenceDataGrid, row.DisplayName, textBox);
            }

            referenceDataSignature = signature;
            RefreshReferenceDataEditState();
        }
        finally
        {
            referenceDataGrid.ResumeLayout(true);
        }
    }

    private void PopulatePersonalBestTimeGrid()
    {
        if (personalBestTimeGrid is null)
        {
            return;
        }

        List<SplitConditionDataRow> rows = GetCumulativeRows().ToList();
        string signature = string.Join('\u001F', rows.Select(row => row.Key));
        if (personalBestTimeGridSignature == signature && personalBestTimeTextBoxes.Count > 0)
        {
            foreach (SplitConditionDataRow row in rows)
            {
                if (personalBestTimeTextBoxes.TryGetValue(row.Key, out TextBox? textBox))
                {
                    textBox.Text = Draft.GetPersonalBestTimeText(row.Key);
                }
            }

            return;
        }

        personalBestTimeGrid.SuspendLayout();
        try
        {
            SettingsUiFactory.ClearGrid(personalBestTimeGrid);
            personalBestTimeTextBoxes.Clear();
            foreach (SplitConditionDataRow row in rows)
            {
                TextBox textBox = Factory.CreateTextBox(Draft.GetPersonalBestTimeText(row.Key));
                textBox.PlaceholderText = Context.Localize("m:ss or h:mm:ss");
                textBox.TextChanged += (_, _) => RefreshReferenceDataFromPersonalBest();
                personalBestTimeTextBoxes[row.Key] = textBox;
                Factory.AddRawSettingRow(personalBestTimeGrid, row.DisplayName, textBox);
            }

            personalBestTimeGridSignature = signature;
        }
        finally
        {
            personalBestTimeGrid.ResumeLayout(true);
        }
    }

    private void PopulatePersonalBestSegmentGrid()
    {
        if (personalBestSegmentGrid is null)
        {
            return;
        }

        List<RouteGroup> groups = SplitRouteGroups.Build(Draft).ToList();
        string signature = string.Join('\u001F', groups.Select(group => group.Key));
        if (personalBestSegmentGridSignature == signature && personalBestSegmentTextBoxes.Count > 0)
        {
            foreach (RouteGroup group in groups)
            {
                if (personalBestSegmentTextBoxes.TryGetValue(group.Key, out TextBox? textBox))
                {
                    textBox.Text = Draft.GetPersonalBestSegmentText(group.Key);
                }
            }

            return;
        }

        personalBestSegmentGrid.SuspendLayout();
        try
        {
            SettingsUiFactory.ClearGrid(personalBestSegmentGrid);
            personalBestSegmentTextBoxes.Clear();
            foreach (RouteGroup group in groups)
            {
                TextBox textBox = Factory.CreateTextBox(Draft.GetPersonalBestSegmentText(group.Key));
                textBox.PlaceholderText = Context.Localize("m:ss or h:mm:ss");
                personalBestSegmentTextBoxes[group.Key] = textBox;
                Factory.AddRawSettingRow(personalBestSegmentGrid, GetRawGroupDisplayName(group), textBox);
            }

            personalBestSegmentGridSignature = signature;
        }
        finally
        {
            personalBestSegmentGrid.ResumeLayout(true);
        }
    }

    private void ConfigureReferenceSetBox()
    {
        referenceSetBox.Dock = DockStyle.Fill;
        PopulateReferenceSetBox();
        referenceSetBox.SelectedIndexChanged += (_, _) => SwitchReferenceSet();
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

    private void ConfigurePersonalBestTimeSetBox()
    {
        ConfigurePersonalSetBox(
            personalBestTimeSetBox,
            Draft.Comparison.PersonalBestTimeSets,
            Draft.GetActivePersonalBestTimeSet().Name,
            SwitchPersonalBestTimeSet);
    }

    private void ConfigurePersonalBestSegmentSetBox()
    {
        ConfigurePersonalSetBox(
            personalBestSegmentSetBox,
            Draft.Comparison.PersonalBestSegmentSets,
            Draft.GetActivePersonalBestSegmentSet().Name,
            SwitchPersonalBestSegmentSet);
    }

    private static void ConfigurePersonalSetBox(
        ThemedDropDownList comboBox,
        IEnumerable<ReferenceSplitSet> sets,
        string activeName,
        EventHandler selectionChanged)
    {
        comboBox.Dock = DockStyle.Fill;
        comboBox.Items.Clear();

        foreach (ReferenceSplitSet set in sets)
        {
            comboBox.Items.Add(set.Name);
        }

        comboBox.SelectedItem = activeName;
        comboBox.SelectedIndexChanged += selectionChanged;
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

    private void SwitchPersonalBestTimeSet(object? sender, EventArgs e)
    {
        SavePersonalBestTimeTextBoxes();
        if (personalBestTimeSetBox.SelectedItem is string selectedName)
        {
            Draft.Comparison.ActivePersonalBestTimeSet = selectedName;
            Draft.SyncPersonalBestTimesFromActiveSet();
        }

        LoadPersonalBestTimeTextBoxes();
        Context.NotifyModelChanged(SettingsModelChange.PersonalBestTimeChanged);
    }

    private void SwitchPersonalBestSegmentSet(object? sender, EventArgs e)
    {
        SavePersonalBestSegmentTextBoxes();
        if (personalBestSegmentSetBox.SelectedItem is string selectedName)
        {
            Draft.Comparison.ActivePersonalBestSegmentSet = selectedName;
            Draft.SyncPersonalBestSegmentsFromActiveSet();
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

        Draft.Comparison.ReferenceSplitSets.Add(AppSettings.CreateReferenceSet(
            name,
            keys: GetCumulativeRows().Select(row => row.Key)));
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

        ReferenceSplitSet activeSet = Draft.GetActiveReferenceSet();
        foreach ((string name, TextBox textBox) in splitTextBoxes)
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
        foreach ((string name, TextBox textBox) in personalBestTimeTextBoxes)
        {
            Draft.SetPersonalBestTimeText(name, NormalizeTimeText(textBox.Text));
        }

        Draft.SyncActivePersonalBestTimeSetFromDictionary();
    }

    private void SavePersonalBestSegmentTextBoxes()
    {
        foreach ((string name, TextBox textBox) in personalBestSegmentTextBoxes)
        {
            Draft.SetPersonalBestSegmentText(name, NormalizeTimeText(textBox.Text));
        }

        Draft.SyncActivePersonalBestSegmentSetFromDictionary();
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
        foreach ((string name, TextBox textBox) in splitTextBoxes)
        {
            textBox.Text = GetDisplayedReferenceTimeText(name);
        }
    }

    private void LoadPersonalBestTimeTextBoxes()
    {
        foreach ((string name, TextBox textBox) in personalBestTimeTextBoxes)
        {
            textBox.Text = Draft.GetPersonalBestTimeText(name);
        }
    }

    private void LoadPersonalBestSegmentTextBoxes()
    {
        foreach ((string name, TextBox textBox) in personalBestSegmentTextBoxes)
        {
            textBox.Text = Draft.GetPersonalBestSegmentText(name);
        }
    }

    private void ToggleUsePersonalBestAsReferenceTime()
    {
        bool usePersonalBest = usePersonalBestAsReferenceTimeBox.Checked;
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

        Draft.Comparison.ReferenceSplitSets = SplitTimeSetStore.LoadReferenceSets();
        referenceSetsLoadedForEditing = true;
        SettingsNormalizer.Normalize(Draft);
        PopulateReferenceSetBox();
    }

    private void RefreshReferenceDataEditState()
    {
        bool usePersonalBest = usePersonalBestAsReferenceTimeBox.Checked;
        referenceSetBox.Enabled = !usePersonalBest;
        newReferenceSetNameBox.Enabled = !usePersonalBest;
        if (addReferenceSetButton is not null)
        {
            addReferenceSetButton.Enabled = !usePersonalBest;
        }

        foreach (TextBox textBox in splitTextBoxes.Values)
        {
            textBox.ReadOnly = usePersonalBest;
            textBox.TabStop = !usePersonalBest;
            textBox.Cursor = usePersonalBest ? Cursors.Default : Cursors.IBeam;
        }

        LoadReferenceTextBoxes();
    }

    private void RefreshReferenceDataFromPersonalBest()
    {
        if (usePersonalBestAsReferenceTimeBox.Checked)
        {
            LoadReferenceTextBoxes();
        }
    }

    private string GetDisplayedReferenceTimeText(string name)
    {
        if (!usePersonalBestAsReferenceTimeBox.Checked)
        {
            return Draft.GetReferenceText(name);
        }

        return personalBestTimeTextBoxes.TryGetValue(name, out TextBox? personalBestTextBox)
            ? personalBestTextBox.Text
            : Draft.GetPersonalBestTimeText(name);
    }

    private IEnumerable<SplitConditionDataRow> GetCumulativeRows()
    {
        return SplitConditionDataRows.Build(Draft);
    }

    private static string GetRawGroupDisplayName(RouteGroup group)
    {
        return string.IsNullOrWhiteSpace(group.DisplayName)
            ? group.Key
            : group.DisplayName;
    }

    private static void ConfigureCheckBox(CheckBox checkBox, bool selected)
    {
        checkBox.Checked = selected;
        checkBox.Dock = DockStyle.Fill;
        UiTheme.StyleCheckBox(checkBox);
    }
}
