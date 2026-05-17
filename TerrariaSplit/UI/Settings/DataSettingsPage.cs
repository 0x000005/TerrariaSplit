using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class DataSettingsPage : SettingsPageBase
{
    private readonly ComboBox referenceSetBox = new();
    private readonly TextBox newReferenceSetNameBox = new();
    private readonly CheckBox usePersonalBestAsReferenceTimeBox = new();
    private readonly ComboBox personalBestTimeSetBox = new();
    private readonly ComboBox personalBestSegmentSetBox = new();
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

    internal ComboBox ReferenceSetBox => referenceSetBox;

    internal TextBox NewReferenceSetNameBox => newReferenceSetNameBox;

    internal IReadOnlyDictionary<string, TextBox> SplitTextBoxes => splitTextBoxes;

    protected override Control BuildPage(SettingsPageContext context)
    {
        referenceSetsLoadedForEditing = !Draft.UsePersonalBestAsReferenceTime;
        return context.BuildScrollPage(content =>
        {
            AddReferenceDataSection(content);
            AddPersonalBestDataSection(content);
        });
    }

    public override void Apply(AppSettings settings)
    {
        settings.UsePersonalBestAsReferenceTime = usePersonalBestAsReferenceTimeBox.Checked;
        settings.AutoUpdatePersonalBestData = autoUpdatePersonalBestDataBox.Checked;
        settings.AskBeforeUpdatingPersonalBestData = askBeforeUpdatingPersonalBestDataBox.Checked;
        if (!settings.UsePersonalBestAsReferenceTime)
        {
            EnsureReferenceSetsLoadedForEditing();
            SaveReferenceTextBoxes();
        }

        SavePersonalBestTextBoxes();

        if (!settings.UsePersonalBestAsReferenceTime)
        {
            settings.ActiveReferenceSplitSet = referenceSetBox.SelectedItem is string selectedReferenceSet
                ? selectedReferenceSet
                : settings.GetActiveReferenceSet().Name;
        }

        settings.ActivePersonalBestTimeSet = personalBestTimeSetBox.SelectedItem is string selectedPersonalBestTimeSet
            ? selectedPersonalBestTimeSet
            : settings.GetActivePersonalBestTimeSet().Name;
        settings.ActivePersonalBestSegmentSet = personalBestSegmentSetBox.SelectedItem is string selectedPersonalBestSegmentSet
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

        ConfigureCheckBox(usePersonalBestAsReferenceTimeBox, Draft.UsePersonalBestAsReferenceTime);
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
        ConfigureCheckBox(autoUpdatePersonalBestDataBox, Draft.AutoUpdatePersonalBestData);
        ConfigureCheckBox(askBeforeUpdatingPersonalBestDataBox, Draft.AskBeforeUpdatingPersonalBestData);
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

        List<BossRouteEntry> entries = GetRouteOrderedEntries().ToList();
        string signature = string.Join('\u001F', entries.Select(entry => entry.BossId));
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
            foreach (BossRouteEntry entry in entries)
            {
                if (!BossSplitDefinitions.TryGetUnit(entry.BossId, out BossUnitDefinition unit))
                {
                    continue;
                }

                TextBox textBox = Factory.CreateTextBox(GetDisplayedReferenceTimeText(unit.Id));
                textBox.PlaceholderText = "m:ss or h:mm:ss";
                splitTextBoxes[unit.Id] = textBox;
                Factory.AddSettingRow(referenceDataGrid, Context.Localize(unit.DisplayName), textBox);
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

        List<BossRouteEntry> entries = GetRouteOrderedEntries().ToList();
        string signature = string.Join('\u001F', entries.Select(entry => entry.BossId));
        if (personalBestTimeGridSignature == signature && personalBestTimeTextBoxes.Count > 0)
        {
            foreach (BossRouteEntry entry in entries)
            {
                if (personalBestTimeTextBoxes.TryGetValue(entry.BossId, out TextBox? textBox))
                {
                    textBox.Text = Draft.GetPersonalBestTimeText(entry.BossId);
                }
            }

            return;
        }

        personalBestTimeGrid.SuspendLayout();
        try
        {
            SettingsUiFactory.ClearGrid(personalBestTimeGrid);
            personalBestTimeTextBoxes.Clear();
            foreach (BossRouteEntry entry in entries)
            {
                if (!BossSplitDefinitions.TryGetUnit(entry.BossId, out BossUnitDefinition unit))
                {
                    continue;
                }

                TextBox textBox = Factory.CreateTextBox(Draft.GetPersonalBestTimeText(unit.Id));
                textBox.PlaceholderText = "m:ss or h:mm:ss";
                textBox.TextChanged += (_, _) => RefreshReferenceDataFromPersonalBest();
                personalBestTimeTextBoxes[unit.Id] = textBox;
                Factory.AddSettingRow(personalBestTimeGrid, Context.Localize(unit.DisplayName), textBox);
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

        List<RouteGroup> groups = BossRouteGroups.Build(Draft).ToList();
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
                textBox.PlaceholderText = "m:ss or h:mm:ss";
                personalBestSegmentTextBoxes[group.Key] = textBox;
                Factory.AddSettingRow(personalBestSegmentGrid, BossRouteGroups.GetGroupDisplayName(group, Draft), textBox);
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
        UiTheme.StyleComboBox(referenceSetBox);
        PopulateReferenceSetBox();
        referenceSetBox.SelectedIndexChanged += (_, _) => SwitchReferenceSet();
    }

    private void PopulateReferenceSetBox()
    {
        updatingReferenceSetSelection = true;
        referenceSetBox.Items.Clear();

        foreach (ReferenceSplitSet set in Draft.ReferenceSplitSets)
        {
            referenceSetBox.Items.Add(set.Name);
        }

        referenceSetBox.SelectedItem = Draft.ActiveReferenceSplitSet;
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
            Draft.PersonalBestTimeSets,
            Draft.GetActivePersonalBestTimeSet().Name,
            SwitchPersonalBestTimeSet);
    }

    private void ConfigurePersonalBestSegmentSetBox()
    {
        ConfigurePersonalSetBox(
            personalBestSegmentSetBox,
            Draft.PersonalBestSegmentSets,
            Draft.GetActivePersonalBestSegmentSet().Name,
            SwitchPersonalBestSegmentSet);
    }

    private static void ConfigurePersonalSetBox(
        ComboBox comboBox,
        IEnumerable<ReferenceSplitSet> sets,
        string activeName,
        EventHandler selectionChanged)
    {
        comboBox.Dock = DockStyle.Fill;
        UiTheme.StyleComboBox(comboBox);
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
        if (updatingReferenceSetSelection || Draft.UsePersonalBestAsReferenceTime)
        {
            return;
        }

        SaveReferenceTextBoxes();
        if (referenceSetBox.SelectedItem is string selectedName)
        {
            Draft.ActiveReferenceSplitSet = selectedName;
        }

        LoadReferenceTextBoxes();
    }

    private void SwitchPersonalBestTimeSet(object? sender, EventArgs e)
    {
        SavePersonalBestTimeTextBoxes();
        if (personalBestTimeSetBox.SelectedItem is string selectedName)
        {
            Draft.ActivePersonalBestTimeSet = selectedName;
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
            Draft.ActivePersonalBestSegmentSet = selectedName;
            Draft.SyncPersonalBestSegmentsFromActiveSet();
        }

        LoadPersonalBestSegmentTextBoxes();
        Context.NotifyModelChanged(SettingsModelChange.PersonalBestSegmentChanged);
    }

    private void AddReferenceSet()
    {
        if (Draft.UsePersonalBestAsReferenceTime)
        {
            return;
        }

        SaveReferenceTextBoxes();
        string name = newReferenceSetNameBox.Text.Trim();
        if (name.Length == 0 ||
            Draft.ReferenceSplitSets.Any(set => string.Equals(set.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Draft.ReferenceSplitSets.Add(AppSettings.CreateReferenceSet(name));
        referenceSetBox.Items.Add(name);
        referenceSetBox.SelectedItem = name;
        newReferenceSetNameBox.Clear();
    }

    private void SaveReferenceTextBoxes()
    {
        if (Draft.UsePersonalBestAsReferenceTime)
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
        if (!Draft.UsePersonalBestAsReferenceTime && usePersonalBest)
        {
            SaveReferenceTextBoxes();
        }

        Draft.UsePersonalBestAsReferenceTime = usePersonalBest;
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

        Draft.ReferenceSplitSets = SplitTimeSetStore.LoadReferenceSets();
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

    private IEnumerable<BossRouteEntry> GetRouteOrderedEntries()
    {
        return Draft.Route
            .Select((entry, index) => new { Entry = entry, Index = index })
            .OrderBy(item => item.Entry.Segment)
            .ThenBy(item => item.Index)
            .Select(item => item.Entry)
            .ToList();
    }

    private static void ConfigureCheckBox(CheckBox checkBox, bool selected)
    {
        checkBox.Checked = selected;
        checkBox.Dock = DockStyle.Fill;
        UiTheme.StyleCheckBox(checkBox);
    }
}
