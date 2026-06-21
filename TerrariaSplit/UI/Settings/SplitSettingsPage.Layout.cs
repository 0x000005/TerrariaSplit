using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class SplitSettingsPage : SettingsPageBase
{
    private void AddEditorSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = Factory.CreateSection("Route");
        TableLayoutPanel editor = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(33.33f),
            SettingsUiFactory.ColumnStylePercent(33.34f),
            SettingsUiFactory.ColumnStylePercent(33.33f));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowCount = 1;

        editor.Controls.Add(CreateTargetLibraryPanel(), 0, 0);
        editor.Controls.Add(CreateRoutePanel(), 1, 0);
        editor.Controls.Add(CreateConditionPanel(), 2, 0);
        SettingsUiFactory.AddSectionControl(section, editor);

        statusLabel = new Label { Visible = false };
        SettingsUiFactory.AddSection(parent, section);
    }

    private void AddExpansionSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = Factory.CreateSection("Main groups");
        TableLayoutPanel grid = Factory.CreateTwoColumnGrid(280f);

        expandSplitDetailsBox = Factory.CreateCheckBox(Draft.Route.ExpandSplitDetails);
        expandSplitDetailsBox.CheckedChanged += (_, _) => UpdateCollapseSplitDetailsAvailability();
        Factory.AddSettingRow(grid, "Expand multi-condition groups", expandSplitDetailsBox);

        collapseSplitDetailsOnCompletionBox = Factory.CreateCheckBox(Draft.Route.CollapseSplitDetailsOnCompletion);
        Factory.AddSettingRow(grid, "Collapse after completion", collapseSplitDetailsOnCompletionBox);

        SettingsUiFactory.AddSectionControl(section, grid);
        SettingsUiFactory.AddSection(parent, section);
        UpdateCollapseSplitDetailsAvailability();
    }

    private void AddAttachedGroupsSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = Factory.CreateSection("Attached groups");
        TableLayoutPanel grid = Factory.CreateTwoColumnGrid(280f);

        autoHideAttachedGroupsBox = Factory.CreateCheckBox(Draft.Route.AutoHideAttachedGroups);
        Factory.AddSettingRow(grid, "Auto hide attached groups", autoHideAttachedGroupsBox);

        attachedGroupsAffectTimerComparisonBox = Factory.CreateCheckBox(Draft.Route.AttachedGroupsAffectTimerComparison);
        Factory.AddSettingRow(grid, "Attached groups affect main timer comparison", attachedGroupsAffectTimerComparisonBox);

        SettingsUiFactory.AddSectionControl(section, grid);
        SettingsUiFactory.AddSection(parent, section);
    }

    private void UpdateCollapseSplitDetailsAvailability()
    {
        if (collapseSplitDetailsOnCompletionBox is null)
        {
            return;
        }

        collapseSplitDetailsOnCompletionBox.Enabled = expandSplitDetailsBox?.Checked == true;
    }

    private Control CreateTargetLibraryPanel()
    {
        TableLayoutPanel panel = CreateColumnPanel();
        panel.Controls.Add(Factory.CreateSubsectionLabel("Candidates"), 0, panel.RowCount++);

        targetKindBox = CreateTargetKindBox(SplitTargetKind.Boss);
        targetKindBox.SelectedIndexChanged += (_, _) =>
        {
            RefreshTargetList();
        };

        targetSearchBox = Factory.CreateTextBox(string.Empty);
        targetSearchBox.PlaceholderText = Context.Localize("Name / Id");
        targetSearchBox.TextChanged += (_, _) => RefreshTargetList();
        TableLayoutPanel targetSettingsGrid = CreateTopSettingsGrid();
        Factory.AddSettingRow(targetSettingsGrid, "Type", targetKindBox);
        Factory.AddSettingRow(targetSettingsGrid, "Search", targetSearchBox);
        AddFullWidth(panel, targetSettingsGrid);

        targetList = CreateEditorListBox();
        targetList.DrawItem += DrawPlainListItem;
        AddFullWidth(panel, CreateEditorListFrame(targetList));

        FlowLayoutPanel buttons = Factory.CreateActionBar();
        addTargetToSelectedGroupButton = Factory.CreateSmallButton("Add to selected group");
        addTargetToSelectedGroupButton.Width = 188;
        addTargetToSelectedGroupButton.MinimumSize = new Size(188, 36);
        addTargetToSelectedGroupButton.Click += (_, _) => AddFactToCurrentSplit();
        addTargetToNewGroupButton = Factory.CreateSmallButton("Add to new group");
        addTargetToNewGroupButton.Width = 172;
        addTargetToNewGroupButton.MinimumSize = new Size(172, 36);
        addTargetToNewGroupButton.Click += (_, _) => AddTargetToNewGroup();
        buttons.Controls.Add(addTargetToSelectedGroupButton);
        buttons.Controls.Add(addTargetToNewGroupButton);
        AddFullWidth(panel, buttons);
        return panel;
    }

    private Control CreateRoutePanel()
    {
        TableLayoutPanel panel = CreateColumnPanel();
        panel.Controls.Add(Factory.CreateSubsectionLabel("Group"), 0, panel.RowCount++);

        TableLayoutPanel detailsGrid = CreateTopSettingsGrid();

        splitNameBox = Factory.CreateTextBox(string.Empty);
        splitNameBox.PlaceholderText = Context.Localize("Display name");
        splitNameBox.TextChanged += (_, _) => MarkSelectedEntryDirty();
        Factory.AddSettingRow(detailsGrid, "Name", splitNameBox);

        splitEnabledBox = Factory.CreateCheckBox(true);
        splitEnabledBox.CheckedChanged += (_, _) => MarkSelectedEntryDirty();
        Factory.AddSettingRow(detailsGrid, "Enabled", splitEnabledBox);

        splitAttachedBox = Factory.CreateCheckBox(false);
        splitAttachedBox.CheckedChanged += (_, _) => MarkSelectedEntryDirty();
        Factory.AddSettingRow(detailsGrid, "Attached", splitAttachedBox);
        AddFullWidth(panel, detailsGrid);

        routeList = CreateEditorListBox();
        routeList.AllowDrop = true;
        routeList.SelectedIndexChanged += (_, _) =>
        {
            if (!refreshingRouteList)
            {
                LoadSelectedRouteEntry();
            }
        };
        routeList.DrawItem += DrawRouteListItem;
        routeList.MouseDown += RouteListMouseDown;
        routeList.MouseMove += RouteListMouseMove;
        routeList.MouseUp += (_, _) => routeDragIndex = -1;
        routeList.DragOver += RouteListDragOver;
        routeList.DragDrop += RouteListDragDrop;
        AddFullWidth(panel, CreateEditorListFrame(routeList));

        FlowLayoutPanel routeButtons = Factory.CreateActionBar();
        Button addBlankButton = Factory.CreateSmallButton("Create new group");
        Button deleteButton = Factory.CreateSmallButton("Remove selected group");
        addBlankButton.Click += (_, _) => AddBlankSplit();
        deleteButton.Click += (_, _) => DeleteSelectedSplit();
        routeButtons.Controls.Add(addBlankButton);
        routeButtons.Controls.Add(deleteButton);
        AddFullWidth(panel, routeButtons);
        return panel;
    }

    private Control CreateConditionPanel()
    {
        TableLayoutPanel panel = CreateColumnPanel();
        panel.Controls.Add(Factory.CreateSubsectionLabel("Condition"), 0, panel.RowCount++);

        conditionMatchModeBox = Factory.CreateDropDownList();
        conditionMatchModeBox.CollapsedItemTextFormatter = FormatCollapsedMatchModeOption;
        conditionMatchModeBox.SelectedIndexChanged += (_, _) => UpdateSelectedConditionMatchCount();
        TableLayoutPanel conditionSettingsGrid = CreateTopSettingsGrid(250f);
        Factory.AddSettingRow(conditionSettingsGrid, "Match", CreateMatchCountEditor());

        itemQuantityBox = Factory.CreateTextBox("1");
        itemQuantityBox.PlaceholderText = Context.Localize("Quantity");
        itemQuantityBox.Enabled = false;
        itemQuantityBox.TextChanged += (_, _) => UpdateSelectedConditionQuantity();
        itemQuantityBox.Leave += (_, _) => LoadSelectedConditionSettings();
        Factory.AddSettingRow(conditionSettingsGrid, "Quantity", itemQuantityBox);

        iconOverrideBox = CreateIconOverrideBox();
        iconOverrideBox.SelectedIndexChanged += (_, _) =>
        {
            if (updatingUi || updatingConditionSettings)
            {
                return;
            }

            MarkSelectedEntryDirty();
        };
        iconOverrideBox.SelectionCommitted += (_, _) =>
        {
            if (!updatingUi && !updatingConditionSettings)
            {
                PickCustomIconOverrideFileIfSelected();
            }
        };
        Factory.AddSettingRow(conditionSettingsGrid, "Icon", iconOverrideBox);

        iconOverrideFileBox = Factory.CreateTextBox(string.Empty);
        iconOverrideFileBox.Visible = false;
        iconOverrideFileBox.PlaceholderText = Context.Localize("Custom image");
        iconOverrideFileBox.TextChanged += (_, _) =>
        {
            if (!updatingUi && !updatingConditionSettings)
            {
                MarkSelectedEntryDirty();
            }
        };
        AddFullWidth(panel, conditionSettingsGrid);

        conditionList = CreateEditorListBox();
        conditionList.AllowDrop = true;
        conditionList.DrawItem += DrawPlainListItem;
        conditionList.SelectedIndexChanged += (_, _) => LoadSelectedConditionSettings();
        conditionList.MouseDown += ConditionListMouseDown;
        conditionList.MouseMove += ConditionListMouseMove;
        conditionList.MouseUp += (_, _) => conditionDragIndex = -1;
        conditionList.DragOver += ConditionListDragOver;
        conditionList.DragDrop += ConditionListDragDrop;
        conditionList.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete)
            {
                RemoveSelectedFact();
                e.Handled = true;
            }
        };
        advancedConditionBox = CreateAdvancedConditionBox();
        conditionEditorFrame = CreateEditorFrame();
        conditionEditorFrame.Controls.Add(advancedConditionBox);
        conditionEditorFrame.Controls.Add(conditionList);
        advancedConditionBox.Visible = false;
        AddFullWidth(panel, conditionEditorFrame);

        FlowLayoutPanel groupButtons = Factory.CreateActionBar();
        groupButtons.FlowDirection = FlowDirection.RightToLeft;
        removeConditionButton = Factory.CreateSmallButton("Remove selected condition");
        advancedConditionButton = Factory.CreateSmallButton("Switch to advanced");
        removeConditionButton.Click += (_, _) => RemoveSelectedFact();
        advancedConditionButton.Click += (_, _) => ToggleAdvancedConditionMode();
        groupButtons.Controls.Add(advancedConditionButton);
        groupButtons.Controls.Add(removeConditionButton);
        AddFullWidth(panel, groupButtons);
        return panel;
    }

    private static TableLayoutPanel CreateColumnPanel()
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 18, 0),
            Padding = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        return panel;
    }

    private static void AddFullWidth(TableLayoutPanel panel, Control control)
    {
        int row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Margin = new Padding(0, 0, 0, 10);
        panel.Controls.Add(control, 0, row);
    }

    private static ListBox CreateEditorListBox()
    {
        var listBox = new ThemedListBox
        {
            Dock = DockStyle.Fill,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 32,
            BackColor = UiTheme.Field,
            ForeColor = UiTheme.Text,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
            Margin = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(listBox);
        return listBox;
    }

    private static Panel CreateEditorFrame()
    {
        var frame = new Panel
        {
            Dock = DockStyle.Top,
            Height = EditorListHeight,
            MinimumSize = new Size(0, EditorListHeight),
            BackColor = UiTheme.Field,
            Padding = new Padding(1)
        };
        UiTheme.EnableDoubleBuffering(frame);
        frame.Paint += (_, e) =>
        {
            using var borderPen = new Pen(UiTheme.Border);
            e.Graphics.DrawRectangle(
                borderPen,
                0,
                0,
                Math.Max(0, frame.ClientSize.Width - 1),
                Math.Max(0, frame.ClientSize.Height - 1));
        };
        return frame;
    }

    private static Control CreateEditorListFrame(ListBox listBox)
    {
        Panel frame = CreateEditorFrame();
        frame.Controls.Add(listBox);
        return frame;
    }

    private TextBox CreateAdvancedConditionBox()
    {
        var textBox = new ThemedMultilineTextBox
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            BackColor = UiTheme.Field,
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            Font = new Font(FontFamily.GenericMonospace, UiTheme.FormFont().Size),
            ForeColor = UiTheme.Text,
            Margin = Padding.Empty,
            Multiline = true,
            ScrollBars = ScrollBars.None,
            WordWrap = false
        };
        textBox.TextChanged += (_, _) => UpdateAdvancedConditionFromText();
        return textBox;
    }

    private static TableLayoutPanel CreateTopSettingsGrid(float valueColumnWidth = 220f)
    {
        TableLayoutPanel grid = new()
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Height = TopSettingsRowsHeight,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        UiTheme.EnableDoubleBuffering(grid);
        grid.ColumnStyles.Add(SettingsUiFactory.ColumnStylePercent(100f));
        grid.ColumnStyles.Add(SettingsUiFactory.ColumnStyleAbsolute(valueColumnWidth));
        return grid;
    }

    private ThemedDropDownList CreateTargetKindBox(string selectedKind)
    {
        ThemedDropDownList comboBox = Factory.CreateDropDownList();
        comboBox.Items.Add(new TargetKindOption(SplitTargetKind.Boss, Context.Localize("Boss")));
        comboBox.Items.Add(new TargetKindOption(SplitTargetKind.Item, Context.Localize("Item")));
        comboBox.Items.Add(new TargetKindOption(SplitTargetKind.Npc, Context.Localize("NPC")));
        comboBox.Items.Add(new TargetKindOption(SplitTargetKind.Biome, Context.Localize("Biome")));
        SetTargetKind(comboBox, selectedKind);
        return comboBox;
    }

    private static void SetTargetKind(ThemedDropDownList comboBox, string selectedKind)
    {
        string normalized = NormalizeTargetKind(selectedKind);
        comboBox.SelectedItem = comboBox.Items
            .Cast<TargetKindOption>()
            .FirstOrDefault(option => string.Equals(option.Value, normalized, StringComparison.OrdinalIgnoreCase));
        if (comboBox.SelectedIndex < 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private static string GetSelectedTargetKind(ThemedDropDownList comboBox)
    {
        return comboBox.SelectedItem is TargetKindOption option
            ? NormalizeTargetKind(option.Value)
            : SplitTargetKind.Boss;
    }

    private static string NormalizeTargetKind(string? value)
    {
        if (string.Equals(value, SplitTargetKind.Item, StringComparison.OrdinalIgnoreCase))
        {
            return SplitTargetKind.Item;
        }

        if (string.Equals(value, SplitTargetKind.Npc, StringComparison.OrdinalIgnoreCase))
        {
            return SplitTargetKind.Npc;
        }

        return string.Equals(value, SplitTargetKind.Biome, StringComparison.OrdinalIgnoreCase)
            ? SplitTargetKind.Biome
            : SplitTargetKind.Boss;
    }

    private Control CreateMatchCountEditor()
    {
        TableLayoutPanel editor = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(33.33f),
            SettingsUiFactory.ColumnStylePercent(33.34f),
            SettingsUiFactory.ColumnStylePercent(33.33f));
        editor.Margin = Padding.Empty;
        editor.Padding = Padding.Empty;
        int row = Factory.AddGridRow(editor);
        Label prefix = Factory.CreateRowLabel("Satisfy");
        prefix.Margin = new Padding(0, 8, 8, 8);
        Label suffix = Factory.CreateRowLabel("Conditions suffix");
        suffix.Margin = new Padding(8, 8, 0, 8);
        editor.Controls.Add(prefix, 0, row);
        editor.Controls.Add(conditionMatchModeBox, 1, row);
        editor.Controls.Add(suffix, 2, row);
        return editor;
    }

    private ThemedDropDownList CreateIconOverrideBox()
    {
        ThemedDropDownList comboBox = Factory.CreateDropDownList();
        comboBox.Items.Add(new IconOverrideOption(SplitIconOverrideSource.All, string.Empty, Context.Localize("All")));
        comboBox.SelectedIndex = 0;
        return comboBox;
    }

    private void RefreshIconOverrideOptions(SplitIconOverride? selectedOverride = null)
    {
        if (iconOverrideBox is null)
        {
            return;
        }

        SplitIconOverride iconOverride = selectedOverride ?? GetCurrentIconOverride();
        string source = SplitIconOverrideSource.Normalize(iconOverride.Source);
        string targetId = iconOverride.TargetId?.Trim() ?? string.Empty;
        string filePath = iconOverride.FilePath?.Trim() ?? string.Empty;

        updatingConditionSettings = true;
        try
        {
            iconOverrideBox.SuspendLayout();
            try
            {
                iconOverrideBox.Items.Clear();
                iconOverrideBox.Items.Add(new IconOverrideOption(
                    SplitIconOverrideSource.All,
                    string.Empty,
                    Context.Localize("All")));

                foreach (SplitTargetDefinition target in GetCurrentConditionTargets())
                {
                    iconOverrideBox.Items.Add(new IconOverrideOption(
                        SplitIconOverrideSource.Target,
                        target.Id,
                        FormatTargetListItem(target)));
                }

                iconOverrideBox.Items.Add(new IconOverrideOption(
                    SplitIconOverrideSource.CustomFile,
                    string.Empty,
                    Context.Localize("Custom image")));
            }
            finally
            {
                iconOverrideBox.ResumeLayout();
            }

            IconOverrideOption? selected = null;
            if (source == SplitIconOverrideSource.Target)
            {
                selected = iconOverrideBox.Items
                    .Cast<IconOverrideOption>()
                    .FirstOrDefault(option =>
                        option.Source == SplitIconOverrideSource.Target &&
                        string.Equals(option.TargetId, targetId, StringComparison.OrdinalIgnoreCase));
            }
            else if (source == SplitIconOverrideSource.CustomFile)
            {
                selected = iconOverrideBox.Items
                    .Cast<IconOverrideOption>()
                    .FirstOrDefault(option => option.Source == SplitIconOverrideSource.CustomFile);
            }

            iconOverrideBox.SelectedItem = selected ?? iconOverrideBox.Items[0];
            iconOverrideFileBox.Text = source == SplitIconOverrideSource.CustomFile ? filePath : string.Empty;
        }
        finally
        {
            updatingConditionSettings = false;
        }
    }

    private void SetIconOverrideSource(string source)
    {
        if (iconOverrideBox is null)
        {
            return;
        }

        IconOverrideOption? option = iconOverrideBox.Items
            .Cast<IconOverrideOption>()
            .FirstOrDefault(item => item.Source == source);
        if (option is not null)
        {
            iconOverrideBox.SelectedItem = option;
        }
    }

    private void PickCustomIconOverrideFileIfSelected()
    {
        if (iconOverrideBox?.SelectedItem is not IconOverrideOption option ||
            option.Source != SplitIconOverrideSource.CustomFile)
        {
            return;
        }

        string previousPath = iconOverrideFileBox.Text.Trim();
        if (Dialogs.PickFile(
                iconOverrideFileBox,
                "Choose icon",
                "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*"))
        {
            SetIconOverrideSource(SplitIconOverrideSource.CustomFile);
            MarkSelectedEntryDirty();
            return;
        }

        iconOverrideFileBox.Text = previousPath;
        if (string.IsNullOrWhiteSpace(previousPath))
        {
            SetIconOverrideSource(SplitIconOverrideSource.All);
        }
    }

}
