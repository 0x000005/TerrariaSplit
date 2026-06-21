using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class SplitSettingsPage : SettingsPageBase
{
    private const int EditorListHeight = 468;
    private const int TopSettingsRowsHeight = 174;
    private const int MaxTargetSearchResults = 500;

    private readonly List<SplitRouteEntry> routeEntries = new();
    private ListBox targetList = null!;
    private ThemedDropDownList targetKindBox = null!;
    private TextBox targetSearchBox = null!;
    private TextBox itemQuantityBox = null!;
    private ListBox routeList = null!;
    private TextBox splitNameBox = null!;
    private CheckBox splitEnabledBox = null!;
    private CheckBox splitAttachedBox = null!;
    private CheckBox expandSplitDetailsBox = null!;
    private CheckBox collapseSplitDetailsOnCompletionBox = null!;
    private CheckBox autoHideAttachedGroupsBox = null!;
    private CheckBox attachedGroupsAffectTimerComparisonBox = null!;
    private ThemedDropDownList conditionMatchModeBox = null!;
    private ThemedDropDownList iconOverrideBox = null!;
    private TextBox iconOverrideFileBox = null!;
    private ListBox conditionList = null!;
    private TextBox advancedConditionBox = null!;
    private Panel conditionEditorFrame = null!;
    private Button addTargetToSelectedGroupButton = null!;
    private Button removeConditionButton = null!;
    private Button addTargetToNewGroupButton = null!;
    private Button advancedConditionButton = null!;
    private Label statusLabel = null!;
    private SplitCondition currentCondition = SplitCondition.AtLeast([], 1);

    private bool updatingUi;
    private bool updatingConditionSettings;
    private bool refreshingRouteList;
    private bool routeDirty;
    private bool preserveCurrentCondition;
    private bool advancedConditionMode;
    private string advancedConditionError = string.Empty;
    private int loadedRouteEntryIndex = -1;
    private int routeDragIndex = -1;
    private Point routeDragStartPoint;
    private int conditionDragIndex = -1;
    private Point conditionDragStartPoint;

    public override SettingsPageId Id => SettingsPageId.Splits;

    internal TextBox TargetSearchBoxForTests => targetSearchBox;

    internal ListBox TargetListForTests => targetList;

    internal ThemedDropDownList TargetKindBoxForTests => targetKindBox;

    internal ListBox RouteListForTests => routeList;

    internal ListBox ConditionListForTests => conditionList;

    internal TextBox ItemQuantityBoxForTests => itemQuantityBox;

    internal TextBox SplitNameBoxForTests => splitNameBox;

    internal CheckBox SplitEnabledBoxForTests => splitEnabledBox;

    internal CheckBox SplitAttachedBoxForTests => splitAttachedBox;

    internal CheckBox SplitExpandDetailsBoxForTests => expandSplitDetailsBox;

    internal CheckBox ExpandSplitDetailsBoxForTests => expandSplitDetailsBox;

    internal CheckBox CollapseSplitDetailsOnCompletionBoxForTests => collapseSplitDetailsOnCompletionBox;

    internal CheckBox AutoHideAttachedGroupsBoxForTests => autoHideAttachedGroupsBox;

    internal CheckBox AttachedGroupsAffectTimerComparisonBoxForTests => attachedGroupsAffectTimerComparisonBox;

    internal ThemedDropDownList IconOverrideBoxForTests => iconOverrideBox;

    internal TextBox IconOverrideFileBoxForTests => iconOverrideFileBox;

    internal ThemedDropDownList ConditionMatchModeBoxForTests => conditionMatchModeBox;

    internal Button AddTargetToSelectedGroupButtonForTests => addTargetToSelectedGroupButton;

    internal Button AddTargetToNewGroupButtonForTests => addTargetToNewGroupButton;

    internal Button AdvancedConditionButtonForTests => advancedConditionButton;

    internal TextBox AdvancedConditionBoxForTests => advancedConditionBox;

    internal bool AdvancedConditionModeForTests => advancedConditionMode;

    protected override Control BuildPage(SettingsPageContext context)
    {
        routeEntries.Clear();
        routeEntries.AddRange(Draft.SplitRoute.Select(CloneEntry));
        if (routeEntries.Count == 0)
        {
            routeEntries.AddRange(SplitCatalog.CreateDefaultRoute().Select(CloneEntry));
        }

        Control page = context.BuildScrollPage(content =>
        {
            AddEditorSection(content);
            AddExpansionSection(content);
            AddAttachedGroupsSection(content);
        });

        RefreshTargetList();
        RefreshRouteList();
        if (routeList.Items.Count > 0)
        {
            routeList.SelectedIndex = 0;
        }

        return page;
    }

    public override void Apply(AppSettings settings)
    {
        if (!SaveSelectedEntryFromControls())
        {
            throw new SettingsApplyFailedException(advancedConditionError);
        }

        EnsureRouteEntryIds();
        NormalizeAttachedRouteFlags();
        if (TryValidateRoute(out string validationMessage))
        {
            settings.SplitRoute = routeEntries.Select(CloneEntry).ToList();
            bool expansionChanged = SaveExpansionSettings(settings);

            AppSettingsStore.Normalize(settings);
            statusLabel.Text = string.Empty;
            if (routeDirty || expansionChanged)
            {
                Context.NotifyModelChanged(SettingsModelChange.RouteChanged);
            }

            routeDirty = false;
            return;
        }

        statusLabel.Text = validationMessage;
        throw new SettingsApplyFailedException(validationMessage);
    }

    public override void OnDeselected()
    {
        if (!SaveSelectedEntryFromControls())
        {
            statusLabel.Text = advancedConditionError;
            return;
        }

        EnsureRouteEntryIds();
        NormalizeAttachedRouteFlags();
        bool expansionChanged = SaveExpansionSettings(Draft);
        string validationMessage = string.Empty;
        if (routeDirty && TryValidateRoute(out validationMessage))
        {
            Draft.SplitRoute = routeEntries.Select(CloneEntry).ToList();
            AppSettingsStore.Normalize(Draft);
            Context.NotifyModelChanged(SettingsModelChange.RouteChanged);
            statusLabel.Text = string.Empty;
            routeDirty = false;
            return;
        }

        if (!routeDirty && expansionChanged)
        {
            AppSettingsStore.Normalize(Draft);
            Context.NotifyModelChanged(SettingsModelChange.RouteChanged);
            statusLabel.Text = string.Empty;
            return;
        }

        if (routeDirty && expansionChanged)
        {
            Context.NotifyModelChanged(SettingsModelChange.RouteChanged);
        }

        if (routeDirty)
        {
            statusLabel.Text = validationMessage;
        }
    }

    private bool SaveExpansionSettings(AppSettings settings)
    {
        bool expand = expandSplitDetailsBox?.Checked == true;
        bool collapse = collapseSplitDetailsOnCompletionBox?.Checked != false;
        bool autoHideAttachedGroups = autoHideAttachedGroupsBox?.Checked != false;
        bool attachedGroupsAffectTimerComparison = attachedGroupsAffectTimerComparisonBox?.Checked != false;
        bool changed = settings.ExpandSplitDetails != expand ||
            settings.CollapseSplitDetailsOnCompletion != collapse ||
            settings.AutoHideAttachedGroups != autoHideAttachedGroups ||
            settings.AttachedGroupsAffectTimerComparison != attachedGroupsAffectTimerComparison;
        settings.ExpandSplitDetails = expand;
        settings.CollapseSplitDetailsOnCompletion = collapse;
        settings.AutoHideAttachedGroups = autoHideAttachedGroups;
        settings.AttachedGroupsAffectTimerComparison = attachedGroupsAffectTimerComparison;
        return changed;
    }

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

        expandSplitDetailsBox = Factory.CreateCheckBox(Draft.ExpandSplitDetails);
        expandSplitDetailsBox.CheckedChanged += (_, _) => UpdateCollapseSplitDetailsAvailability();
        Factory.AddSettingRow(grid, "Expand multi-condition groups", expandSplitDetailsBox);

        collapseSplitDetailsOnCompletionBox = Factory.CreateCheckBox(Draft.CollapseSplitDetailsOnCompletion);
        Factory.AddSettingRow(grid, "Collapse after completion", collapseSplitDetailsOnCompletionBox);

        SettingsUiFactory.AddSectionControl(section, grid);
        SettingsUiFactory.AddSection(parent, section);
        UpdateCollapseSplitDetailsAvailability();
    }

    private void AddAttachedGroupsSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = Factory.CreateSection("Attached groups");
        TableLayoutPanel grid = Factory.CreateTwoColumnGrid(280f);

        autoHideAttachedGroupsBox = Factory.CreateCheckBox(Draft.AutoHideAttachedGroups);
        Factory.AddSettingRow(grid, "Auto hide attached groups", autoHideAttachedGroupsBox);

        attachedGroupsAffectTimerComparisonBox = Factory.CreateCheckBox(Draft.AttachedGroupsAffectTimerComparison);
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

    private void RefreshTargetList()
    {
        if (targetList is null)
        {
            return;
        }

        string query = targetSearchBox?.Text.Trim() ?? string.Empty;
        string targetKind = targetKindBox is null ? SplitTargetKind.Boss : GetSelectedTargetKind(targetKindBox);
        targetList.BeginUpdate();
        try
        {
            targetList.Items.Clear();
            List<SplitTargetDefinition> targets = QueryTargets(query, targetKind)
                .Take(MaxTargetSearchResults + 1)
                .ToList();
            if (targets.Count > MaxTargetSearchResults)
            {
                targetList.Items.Add(Context.Localize("Too many results"));
                return;
            }

            foreach (SplitTargetDefinition target in targets)
            {
                targetList.Items.Add(new TargetListItem(target, FormatTargetListItem(target)));
            }
        }
        finally
        {
            targetList.EndUpdate();
        }

        LoadSelectedConditionSettings();
    }

    private static IEnumerable<SplitTargetDefinition> QueryTargets(string query, string targetKind)
    {
        if (string.Equals(targetKind, SplitTargetKind.Boss, StringComparison.OrdinalIgnoreCase))
        {
            foreach (BossFactDescriptor boss in SplitCatalog.BossFacts)
            {
                var target = new SplitTargetDefinition(
                    boss.TargetId,
                    SplitTargetKind.Boss,
                    boss.DisplayName,
                    boss.FactKey,
                    boss.IconFileName);
                if (MatchesTarget(query, target))
                {
                    yield return target;
                }
            }

            yield break;
        }

        if (string.Equals(targetKind, SplitTargetKind.Item, StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(query, out int itemId) &&
                SplitCatalog.TryGetTarget(SplitCatalog.CreateItemTargetId(itemId), out SplitTargetDefinition exactItemTarget))
            {
                yield return exactItemTarget;
                yield break;
            }

            foreach (TerrariaItemDefinition item in TerrariaItemCatalog.Items
                .Where(item => SplitCatalog.TryGetTarget(SplitCatalog.CreateItemTargetId(item.Id), out SplitTargetDefinition target) &&
                    MatchesTarget(query, target)))
            {
                if (SplitCatalog.TryGetTarget(SplitCatalog.CreateItemTargetId(item.Id), out SplitTargetDefinition target))
                {
                    yield return target;
                }
            }

            yield break;
        }

        if (string.Equals(targetKind, SplitTargetKind.Npc, StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(query, out int npcId) &&
                SplitCatalog.TryGetTarget(SplitCatalog.CreateNpcTargetId(npcId), out SplitTargetDefinition exactNpcTarget))
            {
                yield return exactNpcTarget;
                yield break;
            }

            foreach (TerrariaNpcDefinition npc in TerrariaNpcCatalog.Items)
            {
                if (SplitCatalog.TryGetTarget(SplitCatalog.CreateNpcTargetId(npc.Id), out SplitTargetDefinition target) &&
                    MatchesTarget(query, target))
                {
                    yield return target;
                }
            }

            yield break;
        }

        if (string.Equals(targetKind, SplitTargetKind.Biome, StringComparison.OrdinalIgnoreCase))
        {
            foreach (TerrariaBiomeDefinition biome in TerrariaBiomeCatalog.Items)
            {
                if (SplitCatalog.TryGetTarget(SplitCatalog.CreateBiomeTargetId(biome.Id), out SplitTargetDefinition target) &&
                    MatchesTarget(query, target))
                {
                    yield return target;
                }
            }
        }
    }

    private static bool MatchesTarget(string query, SplitTargetDefinition target)
    {
        return SplitTargetDisplayNames.GetSearchNames(target)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Any(value => Matches(query, value) || MatchesNormalized(query, value));
    }

    private static bool Matches(string query, string value)
    {
        return string.IsNullOrWhiteSpace(query) ||
            value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesNormalized(string query, string value)
    {
        string normalizedQuery = NormalizeSearchText(query);
        return normalizedQuery.Length > 0 &&
            NormalizeSearchText(value).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSearchText(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private void RefreshRouteList()
    {
        if (routeList is null)
        {
            return;
        }

        int selected = routeList.SelectedIndex;
        refreshingRouteList = true;
        routeList.BeginUpdate();
        try
        {
            routeList.Items.Clear();
            for (int i = 0; i < routeEntries.Count; i++)
            {
                routeList.Items.Add(new RouteListItem(routeEntries[i]));
            }
        }
        finally
        {
            routeList.EndUpdate();
            refreshingRouteList = false;
        }

        if (routeList.Items.Count == 0)
        {
            ClearSelectedRouteControls();
            return;
        }

        routeList.SelectedIndex = Math.Clamp(selected, 0, routeList.Items.Count - 1);
        if (routeList.SelectedIndex != loadedRouteEntryIndex)
        {
            LoadSelectedRouteEntry();
        }
    }

    private void LoadSelectedRouteEntry()
    {
        if (updatingUi)
        {
            return;
        }

        int newIndex = routeList.SelectedIndex;
        if (newIndex == loadedRouteEntryIndex)
        {
            return;
        }

        if (!SaveSelectedEntryFromControls())
        {
            RevertRouteSelection();
            return;
        }

        if (newIndex < 0 || newIndex >= routeEntries.Count)
        {
            ClearSelectedRouteControls();
            return;
        }

        loadedRouteEntryIndex = newIndex;
        updatingUi = true;
        try
        {
            SplitRouteEntry entry = routeEntries[newIndex];
            splitNameBox.Text = entry.DisplayName;
            splitEnabledBox.Checked = entry.Enabled;
            splitAttachedBox.Checked = entry.IsAttached;
            RenderConditionList(entry.Condition, entry.IconOverride);
            SetAdvancedConditionMode(entry.UseAdvancedConditionEditor, updateEntry: false, updateText: true, markDirty: false);
            UpdateSelectedAttachedAvailability();
        }
        finally
        {
            updatingUi = false;
        }
    }

    private void RevertRouteSelection()
    {
        if (loadedRouteEntryIndex < 0 ||
            loadedRouteEntryIndex >= routeList.Items.Count ||
            routeList.SelectedIndex == loadedRouteEntryIndex)
        {
            return;
        }

        updatingUi = true;
        try
        {
            routeList.SelectedIndex = loadedRouteEntryIndex;
        }
        finally
        {
            updatingUi = false;
        }
    }

    private void ClearSelectedRouteControls()
    {
        loadedRouteEntryIndex = -1;
        updatingUi = true;
        try
        {
            splitNameBox.Text = string.Empty;
            splitEnabledBox.Checked = false;
            splitAttachedBox.Checked = false;
            splitAttachedBox.Enabled = false;
            conditionList.Items.Clear();
            RefreshConditionMatchOptions(1);
            currentCondition = SplitCondition.AtLeast([], 1);
            preserveCurrentCondition = false;
            SetAdvancedConditionMode(false, updateEntry: false, updateText: false, markDirty: false);
            RefreshIconOverrideOptions(new SplitIconOverride());
            LoadSelectedConditionSettings();
        }
        finally
        {
            updatingUi = false;
        }
    }

    private bool SaveSelectedEntryFromControls()
    {
        if (updatingUi ||
            routeList is null ||
            loadedRouteEntryIndex < 0 ||
            loadedRouteEntryIndex >= routeEntries.Count)
        {
            return true;
        }

        if (!TryCommitCurrentEditor())
        {
            return false;
        }

        SplitRouteEntry entry = routeEntries[loadedRouteEntryIndex];
        entry.DisplayName = splitNameBox.Text.Trim();
        entry.Enabled = splitEnabledBox.Checked;
        entry.IsAttached = splitAttachedBox.Enabled && splitAttachedBox.Checked;
        entry.Condition = GetCurrentCondition();
        entry.IconTargetIds = SplitCatalog.InferTargetIds(entry.Condition).ToList();
        entry.IconOverride = GetCurrentIconOverride();
        entry.UseAdvancedConditionEditor = advancedConditionMode;
        return true;
    }

    private void NormalizeAttachedRouteFlags()
    {
        bool hasFollowingEnabledAnchor = false;
        for (int i = routeEntries.Count - 1; i >= 0; i--)
        {
            SplitRouteEntry entry = routeEntries[i];
            if (!entry.Enabled)
            {
                continue;
            }

            if (entry.IsAttached && !hasFollowingEnabledAnchor)
            {
                entry.IsAttached = false;
            }

            if (!entry.IsAttached)
            {
                hasFollowingEnabledAnchor = true;
            }
        }
    }

    private void UpdateSelectedAttachedAvailability()
    {
        if (splitAttachedBox is null ||
            loadedRouteEntryIndex < 0 ||
            loadedRouteEntryIndex >= routeEntries.Count)
        {
            return;
        }

        bool canAttach = CanEntryAttachToFollowingAnchor(loadedRouteEntryIndex);
        bool previousUpdating = updatingUi;
        updatingUi = true;
        try
        {
            splitAttachedBox.Enabled = canAttach;
            splitAttachedBox.Checked = canAttach && routeEntries[loadedRouteEntryIndex].IsAttached;
        }
        finally
        {
            updatingUi = previousUpdating;
        }
    }

    private bool CanEntryAttachToFollowingAnchor(int index)
    {
        if (index < 0 || index >= routeEntries.Count || !routeEntries[index].Enabled)
        {
            return false;
        }

        for (int i = index + 1; i < routeEntries.Count; i++)
        {
            if (routeEntries[i].Enabled)
            {
                return true;
            }
        }

        return false;
    }

    private void MarkSelectedEntryDirty()
    {
        if (updatingUi)
        {
            return;
        }

        routeDirty = true;
        if (!SaveSelectedEntryFromControls())
        {
            return;
        }

        NormalizeAttachedRouteFlags();
        UpdateSelectedAttachedAvailability();
        RefreshRouteList();
    }

    private void AddBlankSplit()
    {
        if (!SaveSelectedEntryFromControls())
        {
            return;
        }

        int index = routeEntries.Count + 1;
        routeEntries.Add(new SplitRouteEntry
        {
            Id = CreateUniqueSplitId($"split:custom-{index.ToString(CultureInfo.InvariantCulture)}"),
            DisplayName = $"Custom {index.ToString(CultureInfo.InvariantCulture)}",
            Enabled = true,
            IsAttached = false,
            Condition = SplitCondition.AtLeast([], 1),
            IconTargetIds = [],
            UseAdvancedConditionEditor = false
        });
        NormalizeAttachedRouteFlags();
        routeDirty = true;
        RefreshRouteList();
        routeList.SelectedIndex = routeEntries.Count - 1;
    }

    private void AddTargetToNewGroup()
    {
        if (!TryGetSelectedTarget(out SplitTargetDefinition target))
        {
            statusLabel.Text = Context.Localize("Select a target first.");
            return;
        }

        if (!SaveSelectedEntryFromControls())
        {
            return;
        }

        SplitCondition condition = SplitCondition.AtLeast([CreateFactCondition(target)], 1);
        routeEntries.Add(new SplitRouteEntry
        {
            Id = CreateUniqueSplitId(CreateSplitId(target)),
            DisplayName = SplitTargetDisplayNames.GetTargetName(target, Draft.Language),
            Enabled = true,
            IsAttached = false,
            Condition = condition,
            IconTargetIds = SplitCatalog.InferTargetIds(condition).ToList(),
            UseAdvancedConditionEditor = false
        });

        routeDirty = true;
        statusLabel.Text = string.Empty;
        NormalizeAttachedRouteFlags();
        RefreshRouteList();
        routeList.SelectedIndex = routeEntries.Count - 1;
    }

    private void DeleteSelectedSplit()
    {
        if (routeList.SelectedIndex < 0 || routeList.SelectedIndex >= routeEntries.Count)
        {
            return;
        }

        int index = routeList.SelectedIndex;
        routeEntries.RemoveAt(index);
        loadedRouteEntryIndex = -1;
        routeDirty = true;
        NormalizeAttachedRouteFlags();
        RefreshRouteList();
        if (routeList.Items.Count > 0)
        {
            routeList.SelectedIndex = Math.Min(index, routeList.Items.Count - 1);
        }
    }

    private void DrawRouteListItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ListBox listBox || e.Index < 0 || e.Index >= listBox.Items.Count)
        {
            return;
        }

        if (listBox.Items[e.Index] is not RouteListItem item)
        {
            return;
        }

        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        PaintListItemBackground(e.Graphics, e.Bounds, selected);
        Color color = item.Entry.Enabled
            ? UiTheme.Text
            : UiTheme.MutedText;
        Rectangle contentBounds = GetListItemContentBounds(listBox, e.Bounds);
        Rectangle textBounds = new(contentBounds.Left + 8, contentBounds.Top, contentBounds.Width - 16, contentBounds.Height);
        Font itemFont = e.Font ?? listBox.Font;
        if (item.Entry.IsAttached)
        {
            DrawRouteListItemWithAttachedMarker(e.Graphics, item, itemFont, textBounds, color);
            return;
        }

        TextRenderer.DrawText(
            e.Graphics,
            item.ToString(),
            itemFont,
            textBounds,
            color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void DrawRouteListItemWithAttachedMarker(
        Graphics graphics,
        RouteListItem item,
        Font itemFont,
        Rectangle textBounds,
        Color color)
    {
        string name = item.ToString();
        string marker = Context.Localize("Attached group");
        using Font markerFont = new(itemFont.FontFamily, Math.Max(6f, itemFont.Size - 1f), itemFont.Style);
        const TextFormatFlags markerFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
        Size markerSize = TextRenderer.MeasureText(
            graphics,
            marker,
            markerFont,
            Size.Empty,
            markerFlags);
        int gap = 6;
        int markerWidth = Math.Min(markerSize.Width + 4, Math.Max(0, textBounds.Width));
        int nameMaxWidth = Math.Max(0, textBounds.Width - markerWidth - gap);
        Size nameSize = TextRenderer.MeasureText(
            graphics,
            name,
            itemFont,
            Size.Empty,
            TextFormatFlags.NoPadding);
        int visibleNameWidth = Math.Min(nameSize.Width, nameMaxWidth);
        Rectangle nameBounds = new(
            textBounds.Left,
            textBounds.Top,
            nameMaxWidth,
            textBounds.Height);
        Rectangle markerBounds = new(
            textBounds.Left + visibleNameWidth + gap,
            textBounds.Top,
            markerWidth,
            textBounds.Height);

        TextRenderer.DrawText(
            graphics,
            name,
            itemFont,
            nameBounds,
            color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(
            graphics,
            marker,
            markerFont,
            markerBounds,
            UiTheme.MutedText,
            markerFlags);
    }

    private static void DrawPlainListItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ListBox listBox || e.Index < 0 || e.Index >= listBox.Items.Count)
        {
            return;
        }

        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        PaintListItemBackground(e.Graphics, e.Bounds, selected);
        Rectangle contentBounds = GetListItemContentBounds(listBox, e.Bounds);
        Rectangle textBounds = new(contentBounds.Left + 8, contentBounds.Top, contentBounds.Width - 16, contentBounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            listBox.GetItemText(listBox.Items[e.Index]),
            e.Font ?? listBox.Font,
            textBounds,
            listBox.Enabled ? UiTheme.Text : UiTheme.MutedText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static void PaintListItemBackground(Graphics graphics, Rectangle bounds, bool selected)
    {
        using var brush = new SolidBrush(selected ? UiTheme.Selection : UiTheme.Field);
        graphics.FillRectangle(brush, bounds);
    }

    private static Rectangle GetListItemContentBounds(ListBox listBox, Rectangle bounds)
    {
        return listBox is ThemedListBox themedListBox
            ? themedListBox.GetItemContentBounds(bounds)
            : bounds;
    }

    private void RouteListMouseDown(object? sender, MouseEventArgs e)
    {
        routeDragIndex = -1;
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        int index = routeList.IndexFromPoint(e.Location);
        if (index == ListBox.NoMatches)
        {
            return;
        }

        routeDragIndex = index;
        routeDragStartPoint = e.Location;
    }

    private void RouteListMouseMove(object? sender, MouseEventArgs e)
    {
        if (routeDragIndex < 0 ||
            e.Button != MouseButtons.Left ||
            !HasMovedBeyondDragThreshold(routeDragStartPoint, e.Location))
        {
            return;
        }

        int index = routeDragIndex;
        routeDragIndex = -1;
        if (!SaveSelectedEntryFromControls())
        {
            return;
        }

        routeList.DoDragDrop(new RouteDragItem(index), DragDropEffects.Move);
    }

    private void RouteListDragOver(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(typeof(RouteDragItem)) == true
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    private void RouteListDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(typeof(RouteDragItem)) is not RouteDragItem drag)
        {
            return;
        }

        Point point = routeList.PointToClient(new Point(e.X, e.Y));
        int insertionIndex = GetInsertionIndex(routeList, point);
        MoveRouteEntry(drag.Index, insertionIndex);
    }

    private void MoveRouteEntry(int sourceIndex, int insertionIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= routeEntries.Count)
        {
            return;
        }

        if (insertionIndex == sourceIndex || insertionIndex == sourceIndex + 1)
        {
            return;
        }

        SplitRouteEntry entry = routeEntries[sourceIndex];
        routeEntries.RemoveAt(sourceIndex);
        if (insertionIndex > sourceIndex)
        {
            insertionIndex--;
        }

        insertionIndex = Math.Clamp(insertionIndex, 0, routeEntries.Count);
        routeEntries.Insert(insertionIndex, entry);
        loadedRouteEntryIndex = -1;
        routeDirty = true;
        NormalizeAttachedRouteFlags();
        RefreshRouteList();
        routeList.SelectedIndex = insertionIndex;
    }

    private void AddFactToCurrentSplit()
    {
        if (advancedConditionMode)
        {
            CopySelectedTargetReferenceId();
            return;
        }

        if (!TryGetSelectedTarget(out SplitTargetDefinition target))
        {
            statusLabel.Text = Context.Localize("Select a target first.");
            return;
        }

        if (!TryGetSelectedRouteEntry(out SplitRouteEntry entry))
        {
            statusLabel.Text = Context.Localize("Select a split first.");
            return;
        }

        SplitCondition fact = CreateFactCondition(target);
        bool matchAll = IsAllMatchModeSelected();
        int selectedRequiredCount = GetConditionMatchCountFromSelection();
        int index = conditionList.Items.Add(CreateConditionListItem(fact));
        RefreshConditionMatchOptions(matchAll ? conditionList.Items.Count : selectedRequiredCount);
        conditionList.SelectedIndex = index;
        UseBasicConditionFromList();
        entry.Condition = GetCurrentCondition();
        entry.IconTargetIds = SplitCatalog.InferTargetIds(entry.Condition).ToList();
        SplitIconOverride previousOverride = GetCurrentIconOverride();
        RefreshIconOverrideOptions(previousOverride);
        entry.IconOverride = GetCurrentIconOverride();
        routeDirty = true;
        statusLabel.Text = string.Empty;
    }

    private void CopySelectedTargetReferenceId()
    {
        if (!TryGetSelectedTarget(out SplitTargetDefinition target))
        {
            statusLabel.Text = Context.Localize("Select a target first.");
            return;
        }

        string targetId = SplitTargetTokenFormatter.Format(target);
        Clipboard.SetText(targetId);
        statusLabel.Text = string.Format(
            CultureInfo.InvariantCulture,
            Context.Localize("Copied target ID: {0}"),
            targetId);
    }

    private void RemoveSelectedFact()
    {
        if (advancedConditionMode)
        {
            return;
        }

        if (!TryGetSelectedRouteEntry(out SplitRouteEntry entry) ||
            conditionList.SelectedIndex < 0 ||
            conditionList.SelectedIndex >= conditionList.Items.Count)
        {
            return;
        }

        int selected = conditionList.SelectedIndex;
        bool matchAll = IsAllMatchModeSelected();
        int selectedRequiredCount = GetConditionMatchCountFromSelection();
        conditionList.Items.RemoveAt(selected);
        int remainingConditionCount = conditionList.Items.Count;
        int requiredCountAfterRemoval = matchAll
            ? remainingConditionCount
            : Math.Max(1, selectedRequiredCount - 1);
        RefreshConditionMatchOptions(requiredCountAfterRemoval);
        if (conditionList.Items.Count > 0)
        {
            conditionList.SelectedIndex = Math.Min(selected, conditionList.Items.Count - 1);
        }

        UseBasicConditionFromList();
        entry.Condition = GetCurrentCondition();
        entry.IconTargetIds = SplitCatalog.InferTargetIds(entry.Condition).ToList();
        SplitIconOverride previousOverride = GetCurrentIconOverride();
        RefreshIconOverrideOptions(previousOverride);
        entry.IconOverride = GetCurrentIconOverride();
        routeDirty = true;
    }

    private void ToggleAdvancedConditionMode()
    {
        if (!advancedConditionMode)
        {
            SetAdvancedConditionMode(true);
            return;
        }

        if (string.IsNullOrWhiteSpace(advancedConditionBox.Text))
        {
            currentCondition = SplitCondition.AtLeast([], 1);
            preserveCurrentCondition = true;
            advancedConditionError = string.Empty;
            SplitIconOverride emptyOverride = GetCurrentIconOverride();
            RenderConditionList(currentCondition, emptyOverride);
            SetAdvancedConditionMode(false, updateText: false);
            return;
        }

        if (!TryCommitAdvancedConditionText())
        {
            ShowAdvancedConditionWarning(advancedConditionError);
            return;
        }

        if (!CanUseBasicConditionEditor(GetCurrentCondition()))
        {
            advancedConditionError = Context.Localize("Advanced condition cannot be converted to basic editor without losing structure.");
            ShowAdvancedConditionWarning(advancedConditionError);
            return;
        }

        SplitIconOverride previousOverride = GetCurrentIconOverride();
        RenderConditionList(GetCurrentCondition(), previousOverride);
        SetAdvancedConditionMode(false, updateText: false);
    }

    private void ShowAdvancedConditionWarning(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            message = Context.Localize("Invalid advanced condition.");
        }

        advancedConditionError = message;
        statusLabel.Text = message;
        Context.Dialogs.ShowWarning(message, Context.Localize("TerrariaSplit Settings"));
    }

    private void SetAdvancedConditionMode(
        bool enabled,
        bool updateEntry = true,
        bool updateText = true,
        bool markDirty = true)
    {
        bool changed = advancedConditionMode != enabled;
        advancedConditionMode = enabled;
        if (updateEntry && TryGetSelectedRouteEntry(out SplitRouteEntry entry))
        {
            if (entry.UseAdvancedConditionEditor != enabled)
            {
                entry.UseAdvancedConditionEditor = enabled;
                if (markDirty)
                {
                    routeDirty = true;
                }
            }
        }

        if (enabled && updateText)
        {
            bool previousUpdating = updatingConditionSettings;
            updatingConditionSettings = true;
            try
            {
                advancedConditionBox.Text = SplitConditionText.Format(GetCurrentCondition(), Draft.Language);
            }
            finally
            {
                updatingConditionSettings = previousUpdating;
            }
        }

        advancedConditionBox.Visible = enabled;
        conditionList.Visible = !enabled;
        advancedConditionButton.Text = Context.Localize(enabled ? "Switch to basic" : "Switch to advanced");
        UpdateConditionEditorAvailability();
        if (changed)
        {
            statusLabel.Text = string.Empty;
        }
    }

    private void UpdateAdvancedConditionFromText()
    {
        if (updatingUi || updatingConditionSettings || !advancedConditionMode)
        {
            return;
        }

        if (!TryCommitAdvancedConditionText(updateStatusOnFailure: true))
        {
            return;
        }

        routeDirty = true;
        statusLabel.Text = string.Empty;
    }

    private bool TryCommitCurrentEditor()
    {
        if (!advancedConditionMode)
        {
            advancedConditionError = string.Empty;
            return true;
        }

        if (TryCommitAdvancedConditionText(updateStatusOnFailure: true))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(advancedConditionError))
        {
            advancedConditionError = Context.Localize("Invalid advanced condition.");
        }

        return false;
    }

    private bool TryCommitAdvancedConditionText(bool updateStatusOnFailure = true)
    {
        if (!advancedConditionMode)
        {
            return true;
        }

        if (!SplitConditionText.TryParse(advancedConditionBox.Text, Draft.Language, out SplitCondition condition, out string errorMessage))
        {
            advancedConditionError = errorMessage;
            if (updateStatusOnFailure)
            {
                statusLabel.Text = errorMessage;
            }

            return false;
        }

        advancedConditionError = string.Empty;
        currentCondition = condition;
        preserveCurrentCondition = true;
        if (TryGetSelectedRouteEntry(out SplitRouteEntry entry))
        {
            entry.Condition = GetCurrentCondition();
            entry.IconTargetIds = SplitCatalog.InferTargetIds(entry.Condition).ToList();
            entry.UseAdvancedConditionEditor = advancedConditionMode;
            SplitIconOverride previousOverride = GetCurrentIconOverride();
            RefreshIconOverrideOptions(previousOverride);
            entry.IconOverride = GetCurrentIconOverride();
        }

        return true;
    }

    private void UpdateConditionEditorAvailability()
    {
        bool basic = !advancedConditionMode;
        conditionMatchModeBox.Enabled = basic;
        conditionList.Enabled = basic;
        removeConditionButton.Enabled = basic;
        addTargetToSelectedGroupButton.Enabled = true;
        addTargetToSelectedGroupButton.Text = Context.Localize(basic ? "Add to selected group" : "Copy ID");
        addTargetToNewGroupButton.Enabled = true;
        targetKindBox.Enabled = true;
        targetSearchBox.Enabled = true;
        targetList.Enabled = true;
        LoadSelectedConditionSettings();
    }

    private static bool CanUseBasicConditionEditor(SplitCondition condition)
    {
        string kind = SplitConditionKind.Normalize(condition.Kind);
        if (kind == SplitConditionKind.Fact)
        {
            return true;
        }

        if (kind != SplitConditionKind.All &&
            kind != SplitConditionKind.Any &&
            kind != SplitConditionKind.AtLeast)
        {
            return false;
        }

        return condition.Children.All(child => SplitConditionKind.Normalize(child.Kind) == SplitConditionKind.Fact);
    }

    private void ConditionListMouseDown(object? sender, MouseEventArgs e)
    {
        if (advancedConditionMode)
        {
            return;
        }

        conditionDragIndex = -1;
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        int index = conditionList.IndexFromPoint(e.Location);
        if (index == ListBox.NoMatches)
        {
            return;
        }

        conditionDragIndex = index;
        conditionDragStartPoint = e.Location;
    }

    private void ConditionListMouseMove(object? sender, MouseEventArgs e)
    {
        if (advancedConditionMode)
        {
            return;
        }

        if (conditionDragIndex < 0 ||
            e.Button != MouseButtons.Left ||
            !HasMovedBeyondDragThreshold(conditionDragStartPoint, e.Location))
        {
            return;
        }

        int index = conditionDragIndex;
        conditionDragIndex = -1;
        conditionList.SelectedIndex = index;
        conditionList.DoDragDrop(new ConditionDragItem(index), DragDropEffects.Move);
    }

    private void ConditionListDragOver(object? sender, DragEventArgs e)
    {
        if (advancedConditionMode)
        {
            e.Effect = DragDropEffects.None;
            return;
        }

        e.Effect = e.Data?.GetDataPresent(typeof(ConditionDragItem)) == true
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    private void ConditionListDragDrop(object? sender, DragEventArgs e)
    {
        if (advancedConditionMode)
        {
            return;
        }

        if (e.Data?.GetData(typeof(ConditionDragItem)) is not ConditionDragItem drag)
        {
            return;
        }

        Point point = conditionList.PointToClient(new Point(e.X, e.Y));
        int insertionIndex = GetInsertionIndex(conditionList, point);
        MoveConditionFact(drag.Index, insertionIndex);
    }

    private void MoveConditionFact(int sourceIndex, int insertionIndex)
    {
        if (advancedConditionMode)
        {
            return;
        }

        if (!TryGetSelectedRouteEntry(out SplitRouteEntry entry) ||
            sourceIndex < 0 ||
            sourceIndex >= conditionList.Items.Count)
        {
            return;
        }

        if (insertionIndex == sourceIndex || insertionIndex == sourceIndex + 1)
        {
            return;
        }

        List<ConditionListItem> items = conditionList.Items
            .Cast<ConditionListItem>()
            .ToList();
        ConditionListItem item = items[sourceIndex];
        items.RemoveAt(sourceIndex);
        if (insertionIndex > sourceIndex)
        {
            insertionIndex--;
        }

        insertionIndex = Math.Clamp(insertionIndex, 0, items.Count);
        items.Insert(insertionIndex, item);
        conditionList.BeginUpdate();
        try
        {
            conditionList.Items.Clear();
            foreach (ConditionListItem conditionItem in items)
            {
                conditionList.Items.Add(conditionItem);
            }
        }
        finally
        {
            conditionList.EndUpdate();
        }

        conditionList.SelectedIndex = insertionIndex;
        UseBasicConditionFromList();
        entry.Condition = GetCurrentCondition();
        entry.IconTargetIds = SplitCatalog.InferTargetIds(entry.Condition).ToList();
        SplitIconOverride previousOverride = GetCurrentIconOverride();
        RefreshIconOverrideOptions(previousOverride);
        entry.IconOverride = GetCurrentIconOverride();
        routeDirty = true;
    }

    private static int GetInsertionIndex(ListBox listBox, Point point)
    {
        int index = listBox.IndexFromPoint(point);
        if (index == ListBox.NoMatches)
        {
            return listBox.Items.Count;
        }

        Rectangle bounds = listBox.GetItemRectangle(index);
        return point.Y > bounds.Top + (bounds.Height / 2)
            ? index + 1
            : index;
    }

    private static bool HasMovedBeyondDragThreshold(Point startPoint, Point currentPoint)
    {
        Size dragSize = SystemInformation.DragSize;
        Rectangle dragBounds = new(
            startPoint.X - (dragSize.Width / 2),
            startPoint.Y - (dragSize.Height / 2),
            dragSize.Width,
            dragSize.Height);
        return !dragBounds.Contains(currentPoint);
    }

    private void LoadSelectedConditionSettings()
    {
        if (itemQuantityBox is null)
        {
            return;
        }

        updatingConditionSettings = true;
        try
        {
            if (!advancedConditionMode &&
                TryGetSelectedConditionItem(out ConditionListItem item) &&
                IsItemCondition(item.Condition))
            {
                itemQuantityBox.Enabled = true;
                itemQuantityBox.Text = Math.Max(1, item.Condition.Value).ToString(CultureInfo.InvariantCulture);
                return;
            }

            itemQuantityBox.Enabled = false;
            itemQuantityBox.Text = string.Empty;
        }
        finally
        {
            updatingConditionSettings = false;
        }
    }

    private void UpdateSelectedConditionQuantity()
    {
        if (updatingUi || updatingConditionSettings || advancedConditionMode)
        {
            return;
        }

        if (!TryGetSelectedConditionItem(out ConditionListItem item) ||
            !IsItemCondition(item.Condition) ||
            !int.TryParse(itemQuantityBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int quantity) ||
            quantity < 1)
        {
            return;
        }

        item.Condition.Comparison = SplitFactComparison.AtLeast;
        item.Condition.Value = quantity;
        int selected = conditionList.SelectedIndex;
        conditionList.Items[selected] = CreateConditionListItem(item.Condition);
        conditionList.SelectedIndex = selected;

        if (TryGetSelectedRouteEntry(out SplitRouteEntry entry))
        {
            UseBasicConditionFromList();
            entry.Condition = GetCurrentCondition();
            routeDirty = true;
            statusLabel.Text = string.Empty;
        }
    }

    private void UpdateSelectedConditionMatchCount()
    {
        if (updatingUi || updatingConditionSettings || advancedConditionMode)
        {
            return;
        }

        if (TryGetSelectedRouteEntry(out SplitRouteEntry entry))
        {
            UseBasicConditionFromList();
            entry.Condition = GetCurrentCondition();
            routeDirty = true;
            statusLabel.Text = string.Empty;
        }
    }

    private SplitCondition GetCurrentCondition()
    {
        return preserveCurrentCondition
            ? currentCondition.Clone()
            : BuildConditionFromList();
    }

    private SplitCondition BuildConditionFromList()
    {
        IEnumerable<SplitCondition> facts = conditionList.Items
            .Cast<ConditionListItem>()
            .Select(item => item.Condition);
        return SplitCondition.AtLeast(facts, GetConditionMatchCountFromSelection());
    }

    private void UseBasicConditionFromList()
    {
        preserveCurrentCondition = false;
        currentCondition = BuildConditionFromList();
    }

    private int GetConditionMatchCountFromSelection()
    {
        int conditionCount = GetCurrentConditionCount();
        if (conditionCount <= 0)
        {
            return 1;
        }

        return conditionMatchModeBox.SelectedItem is MatchModeOption option
            ? Math.Clamp(option.RequiredCount, 1, conditionCount)
            : conditionCount;
    }

    private bool IsAllMatchModeSelected()
    {
        int conditionCount = Math.Max(1, GetCurrentConditionCount());
        return conditionMatchModeBox?.SelectedItem is not MatchModeOption option ||
            option.RequiredCount >= conditionCount;
    }

    private int GetCurrentConditionCount()
    {
        return conditionList?.Items.Count ?? 0;
    }

    private void RefreshConditionMatchOptions(int selectedRequiredCount)
    {
        if (conditionMatchModeBox is null)
        {
            return;
        }

        int conditionCount = GetCurrentConditionCount();
        int normalizedRequiredCount = conditionCount <= 0
            ? 1
            : Math.Clamp(selectedRequiredCount, 1, conditionCount);
        bool previousUpdating = updatingConditionSettings;
        updatingConditionSettings = true;
        try
        {
            conditionMatchModeBox.Items.Clear();
            if (conditionCount <= 0)
            {
                string allText = Context.Localize("All");
                conditionMatchModeBox.Items.Add(new MatchModeOption(1, allText, allText));
            }
            else
            {
                string allText = Context.Localize("All");
                conditionMatchModeBox.Items.Add(new MatchModeOption(conditionCount, allText, allText));
                for (int count = 1; count < conditionCount; count++)
                {
                    conditionMatchModeBox.Items.Add(new MatchModeOption(
                        count,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            Context.Localize("At least {0}"),
                            count),
                        count.ToString(CultureInfo.InvariantCulture)));
                }
            }

            for (int i = 0; i < conditionMatchModeBox.Items.Count; i++)
            {
                if (conditionMatchModeBox.Items[i] is MatchModeOption option &&
                    option.RequiredCount == normalizedRequiredCount)
                {
                    conditionMatchModeBox.SelectedIndex = i;
                    return;
                }
            }

            if (conditionMatchModeBox.Items.Count > 0)
            {
                conditionMatchModeBox.SelectedIndex = 0;
            }
        }
        finally
        {
            updatingConditionSettings = previousUpdating;
        }
    }

    private SplitIconOverride GetCurrentIconOverride()
    {
        if (iconOverrideBox?.SelectedItem is not IconOverrideOption option)
        {
            return new SplitIconOverride();
        }

        return option.Source switch
        {
            SplitIconOverrideSource.Target => new SplitIconOverride
            {
                Source = SplitIconOverrideSource.Target,
                TargetId = option.TargetId,
                FilePath = string.Empty
            },
            SplitIconOverrideSource.CustomFile => new SplitIconOverride
            {
                Source = SplitIconOverrideSource.CustomFile,
                TargetId = string.Empty,
                FilePath = iconOverrideFileBox?.Text.Trim() ?? string.Empty
            },
            _ => new SplitIconOverride()
        };
    }

    private IReadOnlyList<SplitTargetDefinition> GetCurrentConditionTargets()
    {
        var targets = new List<SplitTargetDefinition>();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string factKey in GetCurrentCondition().GetFactKeys())
        {
            if (SplitCatalog.TryGetTargetByFactKey(factKey, out SplitTargetDefinition target) &&
                seen.Add(target.Id))
            {
                targets.Add(target);
            }
        }

        return targets;
    }

    private void RenderConditionList(SplitCondition condition, SplitIconOverride? selectedOverride = null)
    {
        currentCondition = (condition ?? SplitCondition.All([])).Clone();
        currentCondition.Normalize();
        preserveCurrentCondition = true;
        SplitCondition flat = currentCondition.ToFlatGroup();
        bool previousUpdating = updatingConditionSettings;
        updatingConditionSettings = true;
        conditionList.BeginUpdate();
        try
        {
            conditionList.Items.Clear();
            foreach (SplitCondition fact in flat.GetFactConditions())
            {
                conditionList.Items.Add(CreateConditionListItem(fact));
            }

            RefreshConditionMatchOptions(Math.Max(1, flat.GetRequiredCount()));
            if (conditionList.Items.Count > 0)
            {
                conditionList.SelectedIndex = 0;
            }
            else
            {
                LoadSelectedConditionSettings();
            }
        }
        finally
        {
            conditionList.EndUpdate();
            updatingConditionSettings = previousUpdating;
        }

        RefreshIconOverrideOptions(selectedOverride);
    }

    private string FormatFact(SplitCondition condition)
    {
        if (!SplitCatalog.TryGetTargetByFactKey(condition.FactKey, out SplitTargetDefinition target))
        {
            return $"Fact: {condition.FactKey}";
        }

        return SplitTargetDisplayNames.FormatFact(condition, Draft.Language);
    }

    private string FormatTargetListItem(SplitTargetDefinition target)
    {
        return $"{SplitTargetDisplayNames.GetTargetName(target, Draft.Language)} ({SplitTargetTokenFormatter.Format(target)})";
    }

    private ConditionListItem CreateConditionListItem(SplitCondition condition)
    {
        return new ConditionListItem(condition, FormatFact(condition));
    }

    private bool TryGetSelectedRouteEntry(out SplitRouteEntry entry)
    {
        entry = null!;
        if (loadedRouteEntryIndex < 0 || loadedRouteEntryIndex >= routeEntries.Count)
        {
            return false;
        }

        entry = routeEntries[loadedRouteEntryIndex];
        return true;
    }

    private bool TryGetSelectedTarget(out SplitTargetDefinition target)
    {
        if (targetList.SelectedItem is TargetListItem selected)
        {
            target = selected.Target;
            return true;
        }

        target = null!;
        return false;
    }

    private SplitCondition CreateFactCondition(SplitTargetDefinition target)
    {
        if (target.Kind != SplitTargetKind.Item)
        {
            if (target.Kind == SplitTargetKind.Npc &&
                SplitCatalog.TryParseNpcTargetId(target.Id, out int npcId))
            {
                return SplitCatalog.CreateNpcPresentCondition(npcId);
            }

            if (target.Kind == SplitTargetKind.Biome &&
                SplitCatalog.TryParseBiomeTargetId(target.Id, out string? biomeId))
            {
                return SplitCatalog.CreateBiomeActiveCondition(biomeId);
            }

            return SplitCondition.Fact(target.FactKey);
        }

        if (!SplitCatalog.TryParseItemTargetId(target.Id, out int itemId))
        {
            return SplitCondition.Fact(target.FactKey, SplitFactComparison.AtLeast, 1);
        }

        return SplitCatalog.CreateItemEverOwnedCondition(itemId, 1);
    }

    private bool TryGetSelectedConditionItem(out ConditionListItem item)
    {
        item = null!;
        if (conditionList is null ||
            conditionList.SelectedIndex < 0 ||
            conditionList.SelectedIndex >= conditionList.Items.Count ||
            conditionList.Items[conditionList.SelectedIndex] is not ConditionListItem selected)
        {
            return false;
        }

        item = selected;
        return true;
    }

    private static bool IsItemCondition(SplitCondition condition)
    {
        return SplitCatalog.TryParseItemFactKey(condition.FactKey, out _);
    }

    private static string CreateSplitId(SplitTargetDefinition target)
    {
        return target.Kind == SplitTargetKind.Item && SplitCatalog.TryParseItemTargetId(target.Id, out int itemId)
            ? $"split:item-{itemId.ToString(CultureInfo.InvariantCulture)}"
            : target.Kind == SplitTargetKind.Npc && SplitCatalog.TryParseNpcTargetId(target.Id, out int npcId)
                ? $"split:npc-{npcId.ToString(CultureInfo.InvariantCulture)}"
            : $"split:{target.Id.Replace(':', '-')}";
    }

    private void EnsureRouteEntryIds()
    {
        HashSet<string> seenIds = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < routeEntries.Count; i++)
        {
            SplitRouteEntry entry = routeEntries[i];
            string baseId = string.IsNullOrWhiteSpace(entry.Id)
                ? CreateSplitId(entry, i + 1)
                : entry.Id.Trim();
            entry.Id = CreateUniqueSplitId(baseId, seenIds, i + 1);
        }
    }

    private string CreateUniqueSplitId(string preferredId)
    {
        HashSet<string> seenIds = routeEntries
            .Select(entry => entry.Id.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return CreateUniqueSplitId(preferredId, seenIds, routeEntries.Count + 1);
    }

    private static string CreateUniqueSplitId(string preferredId, HashSet<string> seenIds, int index)
    {
        string baseId = string.IsNullOrWhiteSpace(preferredId)
            ? $"split:custom-{index.ToString(CultureInfo.InvariantCulture)}"
            : preferredId.Trim();
        string id = baseId;
        int suffix = index;
        while (!seenIds.Add(id))
        {
            id = $"{baseId}-{suffix.ToString(CultureInfo.InvariantCulture)}";
            suffix++;
        }

        return id;
    }

    private static string CreateSplitId(SplitRouteEntry entry, int index)
    {
        foreach (string factKey in (entry.Condition ?? SplitCondition.All([])).GetFactKeys())
        {
            if (SplitCatalog.TryGetTargetByFactKey(factKey, out SplitTargetDefinition target))
            {
                return CreateSplitId(target);
            }
        }

        return $"split:custom-{index.ToString(CultureInfo.InvariantCulture)}";
    }

    private bool TryValidateRoute(out string message)
    {
        message = string.Empty;
        if (routeEntries.Count == 0)
        {
            message = Context.Localize("Route must contain at least one split.");
            return false;
        }

        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        foreach (SplitRouteEntry entry in routeEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                message = Context.Localize("Every split needs an id.");
                return false;
            }

            if (!ids.Add(entry.Id.Trim()))
            {
                message = string.Format(CultureInfo.InvariantCulture, Context.Localize("Duplicate split id: {0}"), entry.Id);
                return false;
            }

            if (!ValidateCondition(entry.Condition, out string conditionMessage))
            {
                message = $"{entry.DisplayName}: {conditionMessage}";
                return false;
            }

            if (!ValidateIconOverride(entry, out string iconMessage))
            {
                message = $"{entry.DisplayName}: {iconMessage}";
                return false;
            }
        }

        return true;
    }

    private bool ValidateCondition(SplitCondition condition, out string message)
    {
        message = string.Empty;
        SplitCondition normalized = (condition ?? SplitCondition.All([])).Clone();
        normalized.Normalize();
        if (!normalized.GetFactConditions().Any())
        {
            message = Context.Localize("Condition group cannot be empty.");
            return false;
        }

        return ValidateConditionNode(normalized, out message);
    }

    private bool ValidateConditionNode(SplitCondition condition, out string message)
    {
        message = string.Empty;
        string kind = SplitConditionKind.Normalize(condition.Kind);
        if (kind == SplitConditionKind.Fact)
        {
            if (!SplitCatalog.TryGetTargetByFactKey(condition.FactKey, out _))
            {
                message = Context.Localize("Unknown fact.");
                return false;
            }

            string comparison = SplitFactComparison.Normalize(condition.Comparison);
            if ((comparison == SplitFactComparison.AtLeast || comparison == SplitFactComparison.Equal) &&
                condition.Value < 1)
            {
                message = Context.Localize("Item quantity must be at least 1.");
                return false;
            }

            return true;
        }

        if (kind != SplitConditionKind.All &&
            kind != SplitConditionKind.Any &&
            kind != SplitConditionKind.AtLeast)
        {
            message = Context.Localize("Unknown condition group.");
            return false;
        }

        if (condition.Children.Count == 0)
        {
            message = Context.Localize("Condition group cannot be empty.");
            return false;
        }

        int requiredCount = kind == SplitConditionKind.All
            ? condition.Children.Count
            : Math.Max(1, condition.Value);
        if (requiredCount < 1)
        {
            message = Context.Localize("Match count must be at least 1.");
            return false;
        }

        if (requiredCount > condition.Children.Count)
        {
            message = Context.Localize("Match count cannot exceed condition count.");
            return false;
        }

        foreach (SplitCondition child in condition.Children)
        {
            if (!ValidateConditionNode(child, out message))
            {
                return false;
            }
        }

        return true;
    }

    private bool ValidateIconOverride(SplitRouteEntry entry, out string message)
    {
        message = string.Empty;
        SplitIconOverride iconOverride = entry.IconOverride ?? new SplitIconOverride();
        string source = SplitIconOverrideSource.Normalize(iconOverride.Source);
        if (source == SplitIconOverrideSource.Target)
        {
            HashSet<string> conditionTargetIds = SplitCatalog.InferTargetIds(entry.Condition)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!conditionTargetIds.Contains(iconOverride.TargetId?.Trim() ?? string.Empty))
            {
                message = Context.Localize("Icon target must be in condition.");
                return false;
            }
        }

        if (source == SplitIconOverrideSource.CustomFile &&
            string.IsNullOrWhiteSpace(iconOverride.FilePath))
        {
            message = Context.Localize("Custom icon file is required.");
            return false;
        }

        return true;
    }

    private static SplitRouteEntry CloneEntry(SplitRouteEntry entry)
    {
        return new SplitRouteEntry
        {
            Id = entry.Id,
            Enabled = entry.Enabled,
            IsAttached = entry.IsAttached,
            DisplayName = entry.DisplayName,
            Condition = (entry.Condition ?? SplitCondition.All([])).Clone(),
            IconTargetIds = entry.IconTargetIds?.ToList() ?? new List<string>(),
            IconOverride = CloneIconOverride(entry.IconOverride),
            UseAdvancedConditionEditor = entry.UseAdvancedConditionEditor ||
                !CanUseBasicConditionEditor(entry.Condition ?? SplitCondition.All([]))
        };
    }

    private static SplitIconOverride CloneIconOverride(SplitIconOverride? iconOverride)
    {
        return new SplitIconOverride
        {
            Source = SplitIconOverrideSource.Normalize(iconOverride?.Source),
            TargetId = iconOverride?.TargetId ?? string.Empty,
            FilePath = iconOverride?.FilePath ?? string.Empty
        };
    }

    private sealed record TargetListItem(SplitTargetDefinition Target, string DisplayText)
    {
        public override string ToString()
        {
            return DisplayText;
        }
    }

    private sealed record RouteListItem(SplitRouteEntry Entry)
    {
        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Entry.DisplayName) ? "Unnamed split" : Entry.DisplayName;
        }
    }

    private sealed record ConditionListItem(SplitCondition Condition, string DisplayText)
    {
        public override string ToString()
        {
            return DisplayText;
        }
    }

    private sealed record TargetKindOption(string Value, string DisplayName)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }

    private sealed record MatchModeOption(int RequiredCount, string DisplayName, string CollapsedDisplayName)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }

    private static string FormatCollapsedMatchModeOption(object? item)
    {
        return item is MatchModeOption option
            ? option.CollapsedDisplayName
            : item?.ToString() ?? string.Empty;
    }

    private sealed record IconOverrideOption(string Source, string TargetId, string DisplayName)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }

    private readonly record struct RouteDragItem(int Index);

    private readonly record struct ConditionDragItem(int Index);
}
