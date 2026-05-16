using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed partial class SettingsForm : Form
{

    internal void AddReferenceDataSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("Reference Data");

        ConfigureCheckBox(usePersonalBestAsReferenceTimeBox, settings.UsePersonalBestAsReferenceTime);
        usePersonalBestAsReferenceTimeBox.CheckedChanged += (_, _) => ToggleUsePersonalBestAsReferenceTime();

        TableLayoutPanel modeGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(modeGrid, "Use PB as reference time", usePersonalBestAsReferenceTimeBox);
        AddSectionControl(section, modeGrid);

        ConfigureReferenceSetBox();
        newReferenceSetNameBox.PlaceholderText = Localizer.Get("new group name", settings);
        newReferenceSetNameBox.Dock = DockStyle.Fill;
        UiTheme.StyleTextBox(newReferenceSetNameBox);

        addReferenceSetButton = CreateButton("Add", accent: false, minimumWidth: 120);
        addReferenceSetButton.Click += (_, _) => AddReferenceSet();

        TableLayoutPanel selectorGrid = CreateGrid(
            ColumnStyleAbsolute(220f),
            ColumnStyleAbsolute(260f),
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(240f),
            ColumnStyleAbsolute(136f));
        int selectorRow = AddGridRow(selectorGrid);
        selectorGrid.Controls.Add(CreateRowLabel("Active group"), 0, selectorRow);
        selectorGrid.Controls.Add(referenceSetBox, 1, selectorRow);
        selectorGrid.Controls.Add(newReferenceSetNameBox, 3, selectorRow);
        selectorGrid.Controls.Add(CreateButtonPanel(addReferenceSetButton), 4, selectorRow);
        AddSectionControl(section, selectorGrid);

        TableLayoutPanel grid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        foreach (BossRouteEntry entry in GetRouteOrderedEntries())
        {
            if (!BossSplitDefinitions.TryGetUnit(entry.BossId, out BossUnitDefinition unit))
            {
                continue;
            }

            TextBox textBox = CreateTextBox(GetDisplayedReferenceTimeText(unit.Id));
            textBox.PlaceholderText = "m:ss or h:mm:ss";
            splitTextBoxes[unit.Id] = textBox;
            AddSettingRow(grid, Localizer.Get(unit.DisplayName, settings), textBox);
        }

        RefreshReferenceDataEditState();
        AddSectionControl(section, grid);
        AddSection(parent, section);
    }


    internal void AddPersonalBestDataSection(TableLayoutPanel parent)
    {
        ConfigureCheckBox(autoUpdatePersonalBestDataBox, settings.AutoUpdatePersonalBestData);
        ConfigureCheckBox(askBeforeUpdatingPersonalBestDataBox, settings.AskBeforeUpdatingPersonalBestData);
        TableLayoutPanel autoUpdateSection = CreateSection("Personal Data");
        TableLayoutPanel autoUpdateGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(autoUpdateGrid, "Auto update personal data", autoUpdatePersonalBestDataBox);
        AddSettingRow(autoUpdateGrid, "Ask before updating personal data", askBeforeUpdatingPersonalBestDataBox);
        AddSectionControl(autoUpdateSection, autoUpdateGrid);
        AddSection(parent, autoUpdateSection);

        TableLayoutPanel personalBestSection = CreateSection("Personal Cumulative Best");
        ConfigurePersonalBestTimeSetBox();
        TableLayoutPanel personalBestSelectorGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(personalBestSelectorGrid, "Active file", personalBestTimeSetBox);
        AddSectionControl(personalBestSection, personalBestSelectorGrid);
        personalBestTimeGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        PopulatePersonalBestTimeGrid();
        AddSectionControl(personalBestSection, personalBestTimeGrid);
        AddSection(parent, personalBestSection);

        TableLayoutPanel personalBestSegmentSection = CreateSection("Personal segment best");
        ConfigurePersonalBestSegmentSetBox();
        TableLayoutPanel segmentSelectorGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        AddSettingRow(segmentSelectorGrid, "Active file", personalBestSegmentSetBox);
        AddSectionControl(personalBestSegmentSection, segmentSelectorGrid);
        personalBestSegmentGrid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(280f));
        PopulatePersonalBestSegmentGrid();
        AddSectionControl(personalBestSegmentSection, personalBestSegmentGrid);
        AddSection(parent, personalBestSegmentSection);
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
                    textBox.Text = settings.GetPersonalBestTimeText(entry.BossId);
                }
            }

            return;
        }

        personalBestTimeGrid.SuspendLayout();
        try
        {
            ClearGrid(personalBestTimeGrid);
            personalBestTimeTextBoxes.Clear();
            foreach (BossRouteEntry entry in entries)
            {
                if (!BossSplitDefinitions.TryGetUnit(entry.BossId, out BossUnitDefinition unit))
                {
                    continue;
                }

                TextBox textBox = CreateTextBox(settings.GetPersonalBestTimeText(unit.Id));
                textBox.PlaceholderText = "m:ss or h:mm:ss";
                textBox.TextChanged += (_, _) => RefreshReferenceDataFromPersonalBest();
                personalBestTimeTextBoxes[unit.Id] = textBox;
                AddSettingRow(personalBestTimeGrid, Localizer.Get(unit.DisplayName, settings), textBox);
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

        List<RouteGroup> groups = BossRouteGroups.Build(settings).ToList();
        string signature = string.Join('\u001F', groups.Select(group => group.Key));
        if (personalBestSegmentGridSignature == signature && personalBestSegmentTextBoxes.Count > 0)
        {
            foreach (RouteGroup group in groups)
            {
                if (personalBestSegmentTextBoxes.TryGetValue(group.Key, out TextBox? textBox))
                {
                    textBox.Text = settings.GetPersonalBestSegmentText(group.Key);
                }
            }

            return;
        }

        personalBestSegmentGrid.SuspendLayout();
        try
        {
            ClearGrid(personalBestSegmentGrid);
            personalBestSegmentTextBoxes.Clear();
            foreach (RouteGroup group in groups)
            {
                TextBox textBox = CreateTextBox(settings.GetPersonalBestSegmentText(group.Key));
                textBox.PlaceholderText = "m:ss or h:mm:ss";
                personalBestSegmentTextBoxes[group.Key] = textBox;
                AddSettingRow(personalBestSegmentGrid, BossRouteGroups.GetGroupDisplayName(group, settings), textBox);
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

        foreach (ReferenceSplitSet set in settings.ReferenceSplitSets)
        {
            referenceSetBox.Items.Add(set.Name);
        }

        referenceSetBox.SelectedItem = settings.ActiveReferenceSplitSet;
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
            settings.PersonalBestTimeSets,
            settings.GetActivePersonalBestTimeSet().Name,
            SwitchPersonalBestTimeSet);
    }


    private void ConfigurePersonalBestSegmentSetBox()
    {
        ConfigurePersonalSetBox(
            personalBestSegmentSetBox,
            settings.PersonalBestSegmentSets,
            settings.GetActivePersonalBestSegmentSet().Name,
            SwitchPersonalBestSegmentSet);
    }


    private void SwitchReferenceSet()
    {
        if (updatingReferenceSetSelection || settings.UsePersonalBestAsReferenceTime)
        {
            return;
        }

        SaveReferenceTextBoxes();
        if (referenceSetBox.SelectedItem is string selectedName)
        {
            settings.ActiveReferenceSplitSet = selectedName;
        }

        LoadReferenceTextBoxes();
    }


    private void SwitchPersonalBestTimeSet(object? sender, EventArgs e)
    {
        SavePersonalBestTimeTextBoxes();
        if (personalBestTimeSetBox.SelectedItem is string selectedName)
        {
            settings.ActivePersonalBestTimeSet = selectedName;
            settings.SyncPersonalBestTimesFromActiveSet();
        }

        LoadPersonalBestTimeTextBoxes();
    }


    private void SwitchPersonalBestSegmentSet(object? sender, EventArgs e)
    {
        SavePersonalBestSegmentTextBoxes();
        if (personalBestSegmentSetBox.SelectedItem is string selectedName)
        {
            settings.ActivePersonalBestSegmentSet = selectedName;
            settings.SyncPersonalBestSegmentsFromActiveSet();
        }

        LoadPersonalBestSegmentTextBoxes();
    }


    private void AddReferenceSet()
    {
        if (settings.UsePersonalBestAsReferenceTime)
        {
            return;
        }

        SaveReferenceTextBoxes();
        string name = newReferenceSetNameBox.Text.Trim();
        if (name.Length == 0 ||
            settings.ReferenceSplitSets.Any(set => string.Equals(set.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        settings.ReferenceSplitSets.Add(AppSettings.CreateReferenceSet(name));
        referenceSetBox.Items.Add(name);
        referenceSetBox.SelectedItem = name;
        newReferenceSetNameBox.Clear();
    }


    private void DeleteReferenceSet()
    {
        if (settings.UsePersonalBestAsReferenceTime)
        {
            return;
        }

        if (settings.ReferenceSplitSets.Count <= 1 ||
            referenceSetBox.SelectedItem is not string selectedName)
        {
            return;
        }

        ReferenceSplitSet? selectedSet = settings.ReferenceSplitSets.FirstOrDefault(
            set => string.Equals(set.Name, selectedName, StringComparison.OrdinalIgnoreCase));
        if (selectedSet is null)
        {
            return;
        }

        settings.ReferenceSplitSets.Remove(selectedSet);
        updatingReferenceSetSelection = true;
        referenceSetBox.Items.Remove(selectedName);
        settings.ActiveReferenceSplitSet = settings.ReferenceSplitSets[0].Name;
        referenceSetBox.SelectedItem = settings.ActiveReferenceSplitSet;
        updatingReferenceSetSelection = false;
        LoadReferenceTextBoxes();
    }


    private void SaveReferenceTextBoxes()
    {
        if (settings.UsePersonalBestAsReferenceTime)
        {
            return;
        }

        ReferenceSplitSet activeSet = settings.GetActiveReferenceSet();
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
            settings.SetPersonalBestTimeText(name, NormalizeTimeText(textBox.Text));
        }

        settings.SyncActivePersonalBestTimeSetFromDictionary();
    }


    private void SavePersonalBestSegmentTextBoxes()
    {
        foreach ((string name, TextBox textBox) in personalBestSegmentTextBoxes)
        {
            settings.SetPersonalBestSegmentText(name, NormalizeTimeText(textBox.Text));
        }

        settings.SyncActivePersonalBestSegmentSetFromDictionary();
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
            textBox.Text = settings.GetPersonalBestTimeText(name);
        }
    }


    private void LoadPersonalBestSegmentTextBoxes()
    {
        foreach ((string name, TextBox textBox) in personalBestSegmentTextBoxes)
        {
            textBox.Text = settings.GetPersonalBestSegmentText(name);
        }
    }


    private void ToggleUsePersonalBestAsReferenceTime()
    {
        bool usePersonalBest = usePersonalBestAsReferenceTimeBox.Checked;
        if (!settings.UsePersonalBestAsReferenceTime && usePersonalBest)
        {
            SaveReferenceTextBoxes();
        }

        settings.UsePersonalBestAsReferenceTime = usePersonalBest;
        if (!usePersonalBest)
        {
            EnsureReferenceSetsLoadedForEditing();
        }

        RefreshReferenceDataEditState();
    }


    private void EnsureReferenceSetsLoadedForEditing()
    {
        if (referenceSetsLoadedForEditing)
        {
            return;
        }

        settings.ReferenceSplitSets = SplitTimeSetStore.LoadReferenceSets();
        referenceSetsLoadedForEditing = true;
        SettingsNormalizer.Normalize(settings);
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
            return settings.GetReferenceText(name);
        }

        return personalBestTimeTextBoxes.TryGetValue(name, out TextBox? personalBestTextBox)
            ? personalBestTextBox.Text
            : settings.GetPersonalBestTimeText(name);
    }
}
