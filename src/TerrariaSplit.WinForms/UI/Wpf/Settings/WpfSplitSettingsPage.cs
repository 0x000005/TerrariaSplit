using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TerrariaSplit.UI.Wpf.Controls;
using FormsClipboard = System.Windows.Forms.Clipboard;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfListBoxItem = System.Windows.Controls.ListBoxItem;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfPoint = System.Windows.Point;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace TerrariaSplit.UI.Wpf.Settings;

internal sealed class WpfSplitSettingsPage : WpfSettingsPageBase
{
    private const int MaxTargetSearchResults = 500;
    private const double EditorListHeight = 350;

    private readonly SplitRouteDraft routeDraft = new();
    private readonly SplitRouteListController routeController = new();
    private readonly SplitConditionEditorController conditionController = new();
    private SplitSettingsCommitService commitService = null!;
    private WpfComboBox routePresetBox = null!;
    private WpfTextBox newRouteNameBox = null!;
    private WpfButton addRouteButton = null!;
    private bool updatingRouteSelection;

    private WpfComboBox targetKindBox = null!;
    private WpfTextBox targetSearchBox = null!;
    private WpfListBox targetList = null!;
    private WpfButton addTargetToSelectedGroupButton = null!;
    private WpfButton addTargetToNewGroupButton = null!;

    private WpfTextBox splitNameBox = null!;
    private WpfCheckBox splitEnabledBox = null!;
    private WpfCheckBox splitAttachedBox = null!;
    private WpfListBox routeList = null!;

    private WpfComboBox conditionMatchModeBox = null!;
    private WpfTextBox itemQuantityBox = null!;
    private WpfComboBox iconOverrideBox = null!;
    private WpfTextBox iconOverrideFileBox = null!;
    private WpfListBox conditionList = null!;
    private WpfTextBox advancedConditionBox = null!;
    private WpfButton removeConditionButton = null!;
    private WpfButton advancedConditionButton = null!;

    private SettingCard allIconFilesCard = null!;
    private StackPanel allIconFilesBody = null!;
    private readonly Dictionary<string, WpfTextBox> allIconFileBoxes = new(StringComparer.OrdinalIgnoreCase);

    private bool updatingUi;
    private bool allIconFilesSectionExpanded;

    private int routeDragIndex = -1;
    private WpfPoint routeDragStart;
    private int conditionDragIndex = -1;
    private WpfPoint conditionDragStart;

    private List<SplitRouteEntry> RouteEntries => routeDraft.Entries;

    public override SettingsPageId Id => SettingsPageId.Splits;

    internal WpfComboBox RoutePresetBoxForTests => routePresetBox;
    internal WpfTextBox NewRouteNameBoxForTests => newRouteNameBox;
    internal WpfButton AddRouteButtonForTests => addRouteButton;

    internal WpfTextBox TargetSearchBoxForTests => targetSearchBox;
    internal WpfListBox TargetListForTests => targetList;
    internal WpfComboBox TargetKindBoxForTests => targetKindBox;
    internal WpfListBox RouteListForTests => routeList;
    internal WpfListBox ConditionListForTests => conditionList;
    internal WpfTextBox ItemQuantityBoxForTests => itemQuantityBox;
    internal WpfTextBox SplitNameBoxForTests => splitNameBox;
    internal WpfCheckBox SplitEnabledBoxForTests => splitEnabledBox;
    internal WpfCheckBox SplitAttachedBoxForTests => splitAttachedBox;
    internal WpfComboBox IconOverrideBoxForTests => iconOverrideBox;
    internal WpfTextBox IconOverrideFileBoxForTests => iconOverrideFileBox;
    internal bool AllIconFilesSectionVisibleForTests => allIconFilesSectionExpanded;
    internal IReadOnlyDictionary<string, WpfTextBox> AllIconFileBoxesForTests => allIconFileBoxes;
    internal WpfComboBox ConditionMatchModeBoxForTests => conditionMatchModeBox;
    internal WpfButton AddTargetToSelectedGroupButtonForTests => addTargetToSelectedGroupButton;
    internal WpfButton AddTargetToNewGroupButtonForTests => addTargetToNewGroupButton;
    internal WpfButton AdvancedConditionButtonForTests => advancedConditionButton;
    internal WpfTextBox AdvancedConditionBoxForTests => advancedConditionBox;
    internal bool AdvancedConditionModeForTests => conditionController.AdvancedMode;

    protected override FrameworkElement BuildPage(WpfSettingsPageContext context)
    {
        commitService = new SplitSettingsCommitService(
            routeDraft,
            routeController,
            SaveSelectedEntryFromControls,
            () => conditionController.AdvancedError,
            context.Localize,
            context.NotifyModelChanged);
        RoutePresetService.CaptureActive(Draft.Route);
        routeDraft.LoadFrom(Draft.Route);

        ScrollViewer page = WpfSettingsUi.CreateScrollPage(BuildSections);
        page.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

        RefreshTargetList();
        RefreshRouteList();
        if (routeList.Items.Count > 0)
        {
            routeList.SelectedIndex = 0;
        }
        else
        {
            ClearSelectedRouteControls();
        }

        return page;
    }

    public override void Apply(AppSettings settings)
    {
        SplitCommitResult result = commitService.CommitTo(settings, SplitCommitMode.StrictApply);
        if (!result.Succeeded)
        {
            throw new SettingsApplyFailedException(result.Message);
        }
    }

    public override void OnDeselected()
    {
        _ = commitService.CommitTo(Draft, SplitCommitMode.LenientDeselection);
    }

    private void BuildSections(StackPanel parent)
    {
        var routeCard = WpfSettingsUi.CreateCard(Localize("Route"), out StackPanel body);
        routePresetBox = WpfSettingsUi.CreateComboBox(180);
        PopulateRoutePresetBox();
        routePresetBox.SelectionChanged += (_, _) => SwitchRoutePreset();
        newRouteNameBox = WpfSettingsUi.CreateTextBox(string.Empty, placeholder: Localize("New route name"), width: 164);
        newRouteNameBox.Margin = new Thickness(8, 0, 6, 0);
        addRouteButton = WpfSettingsUi.CreateButton(Localize("Add"), minWidth: 68);
        addRouteButton.Click += (_, _) => AddRoutePreset();
        var routeControls = new StackPanel { Orientation = WpfOrientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        routeControls.Children.Add(routePresetBox);
        routeControls.Children.Add(newRouteNameBox);
        routeControls.Children.Add(addRouteButton);
        WpfSettingsUi.AddSettingRow(body, Localize("Active route"), routeControls);

        Grid columns = WpfSettingsUi.CreateColumnGrid(
            new GridLength(1, GridUnitType.Star),
            new GridLength(12),
            new GridLength(1, GridUnitType.Star),
            new GridLength(12),
            new GridLength(1, GridUnitType.Star));
        columns.Margin = new Thickness(0, 0, 0, 8);
        columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid headers = WpfSettingsUi.CreateColumnGrid(
            new GridLength(1, GridUnitType.Star), new GridLength(12),
            new GridLength(1, GridUnitType.Star), new GridLength(12),
            new GridLength(1, GridUnitType.Star));
        string[] titles = ["Candidates", "Group", "Condition"];
        for (int index = 0; index < titles.Length; index++)
        {
            TextBlock title = WpfSettingsUi.CreateTableHeaderText(Localize(titles[index]), TextAlignment.Left);
            Grid.SetColumn(title, index * 2);
            headers.Children.Add(title);
        }
        Border headerRow = WpfSettingsUi.CreateTableHeaderContainer(headers);
        Grid.SetColumnSpan(headerRow, 5);
        columns.Children.Add(headerRow);

        FrameworkElement targetCol = BuildTargetColumn();
        FrameworkElement routeCol = BuildRouteColumn();
        FrameworkElement condCol = BuildConditionColumn();
        Grid.SetColumn(targetCol, 0);
        Grid.SetColumn(routeCol, 2);
        Grid.SetColumn(condCol, 4);
        Grid.SetRow(targetCol, 1);
        Grid.SetRow(routeCol, 1);
        Grid.SetRow(condCol, 1);
        columns.Children.Add(targetCol);
        columns.Children.Add(routeCol);
        columns.Children.Add(condCol);
        body.Children.Add(columns);

        parent.Children.Add(routeCard);

        // Conditional all-icons file overrides card
        allIconFilesCard = WpfSettingsUi.CreateCard(Localize("Icon files"), out allIconFilesBody);
        allIconFilesCard.Visibility = Visibility.Collapsed;
        parent.Children.Add(allIconFilesCard);
    }

    private void PopulateRoutePresetBox()
    {
        updatingRouteSelection = true;
        try
        {
            routePresetBox.Items.Clear();
            foreach (RoutePreset preset in Draft.Route.Presets)
            {
                routePresetBox.Items.Add(preset.Name);
            }

            routePresetBox.SelectedItem = Draft.Route.ActiveRouteName;
        }
        finally
        {
            updatingRouteSelection = false;
        }
    }

    private bool CommitBeforeRouteSelection()
    {
        SplitCommitResult result = commitService.CommitTo(Draft, SplitCommitMode.StrictApply);
        if (result.Succeeded)
        {
            return true;
        }

        PopulateRoutePresetBox();
        Dialogs.ShowWarning(result.Message, Localize("TerrariaSplit Settings"));
        return false;
    }

    private void SwitchRoutePreset()
    {
        if (updatingRouteSelection || routePresetBox.SelectedItem is not string name ||
            name.Equals(Draft.Route.ActiveRouteName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (CommitBeforeRouteSelection() && RoutePresetService.Select(Draft.Route, name))
        {
            ReloadSelectedRoute();
        }
    }

    private void AddRoutePreset()
    {
        string name = newRouteNameBox.Text.Trim();
        if (!RoutePresetService.IsValidName(name) ||
            Draft.Route.Presets.Any(preset => preset.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (CommitBeforeRouteSelection() && RoutePresetService.Add(Draft.Route, name))
        {
            newRouteNameBox.Clear();
            PopulateRoutePresetBox();
            ReloadSelectedRoute();
        }
    }

    private void ReloadSelectedRoute()
    {
        SettingsNormalizer.Normalize(Draft);
        RoutePresetService.CaptureActive(Draft.Route);
        routeController.ClearLoadedEntry();
        routeController.ClearDirty();
        routeDragIndex = -1;
        conditionDragIndex = -1;
        conditionController.AdvancedError = string.Empty;
        routeDraft.LoadFrom(Draft.Route);
        RefreshRouteList();
        if (routeList.Items.Count > 0)
        {
            routeList.SelectedIndex = 0;
            LoadSelectedRouteEntry();
        }
        else
        {
            ClearSelectedRouteControls();
        }

        Context.NotifyModelChanged(SettingsModelChange.RouteChanged);
    }

    private FrameworkElement BuildTargetColumn()
    {
        var panel = new StackPanel { Orientation = WpfOrientation.Vertical };

        targetKindBox = WpfSettingsUi.CreateComboBox();
        targetKindBox.Items.Add(new SplitTargetKindOption(SplitTargetKind.Boss, Localize("Boss")));
        targetKindBox.Items.Add(new SplitTargetKindOption(SplitTargetKind.Item, Localize("Item")));
        targetKindBox.Items.Add(new SplitTargetKindOption(SplitTargetKind.Npc, Localize("NPC")));
        targetKindBox.Items.Add(new SplitTargetKindOption(SplitTargetKind.Biome, Localize("Biome")));
        targetKindBox.SelectedIndex = 0;
        targetKindBox.SelectionChanged += (_, _) => RefreshTargetList();

        targetSearchBox = WpfSettingsUi.CreateTextBox(string.Empty, placeholder: Localize("Name / Id"));
        targetSearchBox.TextChanged += (_, _) => RefreshTargetList();

        Grid topGrid = CreateTopPropertiesGrid();
        AddCompactPropRow(topGrid, 0, Localize("Type"), targetKindBox);
        AddCompactPropRow(topGrid, 1, Localize("Search"), targetSearchBox);
        panel.Children.Add(topGrid);

        targetList = CreateEditorListBox();
        targetList.MouseDoubleClick += (_, _) => AddFactToCurrentSplit();
        panel.Children.Add(targetList);

        var actions = new UniformGrid { Columns = 2, Margin = new Thickness(0, 8, 0, 0) };
        addTargetToSelectedGroupButton = CreateCompactActionButton(Localize("Add to selected group"));
        addTargetToSelectedGroupButton.Margin = new Thickness(0, 0, 5, 0);
        addTargetToSelectedGroupButton.Click += (_, _) => AddFactToCurrentSplit();
        addTargetToNewGroupButton = CreateCompactActionButton(Localize("Add to new group"));
        addTargetToNewGroupButton.Click += (_, _) => AddTargetToNewGroup();
        actions.Children.Add(addTargetToSelectedGroupButton);
        actions.Children.Add(addTargetToNewGroupButton);
        panel.Children.Add(actions);

        return panel;
    }

    private FrameworkElement BuildRouteColumn()
    {
        var panel = new StackPanel { Orientation = WpfOrientation.Vertical };

        splitNameBox = WpfSettingsUi.CreateTextBox(string.Empty, placeholder: Localize("Display name"));
        splitNameBox.TextChanged += (_, _) => MarkSelectedEntryDirty();

        splitEnabledBox = WpfSettingsUi.CreateToggleSwitch(true);
        splitEnabledBox.Checked += (_, _) => MarkSelectedEntryDirty();
        splitEnabledBox.Unchecked += (_, _) => MarkSelectedEntryDirty();

        splitAttachedBox = WpfSettingsUi.CreateToggleSwitch(false);
        splitAttachedBox.Checked += (_, _) => MarkSelectedEntryDirty();
        splitAttachedBox.Unchecked += (_, _) => MarkSelectedEntryDirty();

        Grid topGrid = CreateTopPropertiesGrid();
        AddCompactPropRow(topGrid, 0, Localize("Name"), splitNameBox);
        AddCompactPropRow(topGrid, 1, Localize("Enabled"), AlignRight(splitEnabledBox));
        AddCompactPropRow(topGrid, 2, Localize("Attached"), AlignRight(splitAttachedBox));
        panel.Children.Add(topGrid);

        routeList = CreateEditorListBox();
        routeList.AllowDrop = true;
        routeList.SelectionChanged += (_, _) =>
        {
            if (!routeController.Refreshing)
            {
                LoadSelectedRouteEntry();
            }
        };
        routeList.PreviewMouseLeftButtonDown += RouteListPreviewMouseDown;
        routeList.PreviewMouseMove += RouteListPreviewMouseMove;
        routeList.PreviewMouseLeftButtonUp += (_, _) => routeDragIndex = -1;
        routeList.DragOver += RouteListDragOver;
        routeList.Drop += RouteListDrop;
        panel.Children.Add(routeList);

        var actions = new UniformGrid { Columns = 2, Margin = new Thickness(0, 8, 0, 0) };
        WpfButton addBlank = CreateCompactActionButton(Localize("Create new group"));
        addBlank.Margin = new Thickness(0, 0, 5, 0);
        addBlank.Click += (_, _) => AddBlankSplit();
        WpfButton removeSelected = CreateCompactActionButton(Localize("Remove selected group"));
        removeSelected.Click += (_, _) => DeleteSelectedSplit();
        actions.Children.Add(addBlank);
        actions.Children.Add(removeSelected);
        panel.Children.Add(actions);

        return panel;
    }

    private FrameworkElement BuildConditionColumn()
    {
        var panel = new StackPanel { Orientation = WpfOrientation.Vertical };

        conditionMatchModeBox = WpfSettingsUi.CreateComboBox();
        conditionMatchModeBox.SelectionChanged += (_, _) => UpdateSelectedConditionMatchCount();

        itemQuantityBox = WpfSettingsUi.CreateTextBox("1", placeholder: Localize("Quantity"), mono: true, centerAlign: true);
        itemQuantityBox.Padding = new Thickness(8, 0, 8, 0);
        itemQuantityBox.IsEnabled = false;
        itemQuantityBox.TextChanged += (_, _) => UpdateSelectedConditionQuantity();
        itemQuantityBox.LostFocus += (_, _) => LoadSelectedConditionSettings();

        iconOverrideBox = WpfSettingsUi.CreateComboBox();
        iconOverrideBox.Items.Add(new IconOverrideOption(SplitIconOverrideSource.All, string.Empty, Localize("All icons")));
        iconOverrideBox.SelectedIndex = 0;
        iconOverrideBox.SelectionChanged += (_, _) =>
        {
            UpdateAllIconFilesSectionVisibility();
            if (updatingUi || conditionController.UpdatingSettings)
            {
                return;
            }

            MarkSelectedEntryDirty();
        };
        _ = new ComboBoxSelectionCommitHandler(
            iconOverrideBox,
            () => !updatingUi && !conditionController.UpdatingSettings && !Owner.IsDisposed,
            PickCustomIconOverrideFileIfSelected);

        iconOverrideFileBox = WpfSettingsUi.CreateTextBox(string.Empty, placeholder: Localize("Custom image"));
        iconOverrideFileBox.Visibility = Visibility.Collapsed;
        iconOverrideFileBox.TextChanged += (_, _) =>
        {
            if (!updatingUi && !conditionController.UpdatingSettings)
            {
                MarkSelectedEntryDirty();
            }
        };

        Grid topGrid = CreateTopPropertiesGrid();
        AddCompactPropRow(topGrid, 0, Localize("Match"), conditionMatchModeBox);
        AddCompactPropRow(topGrid, 1, Localize("Quantity"), itemQuantityBox);
        AddCompactPropRow(topGrid, 2, Localize("Icon"), iconOverrideBox);
        panel.Children.Add(topGrid);

        conditionList = CreateEditorListBox();
        conditionList.AllowDrop = true;
        conditionList.SelectionChanged += (_, _) => LoadSelectedConditionSettings();
        conditionList.PreviewMouseLeftButtonDown += ConditionListPreviewMouseDown;
        conditionList.PreviewMouseMove += ConditionListPreviewMouseMove;
        conditionList.PreviewMouseLeftButtonUp += (_, _) => conditionDragIndex = -1;
        conditionList.DragOver += ConditionListDragOver;
        conditionList.Drop += ConditionListDrop;
        conditionList.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Delete)
            {
                RemoveSelectedFact();
                e.Handled = true;
            }
        };

        advancedConditionBox = WpfSettingsUi.CreateTextBox(string.Empty, mono: true);
        advancedConditionBox.AcceptsReturn = true;
        advancedConditionBox.AcceptsTab = true;
        advancedConditionBox.TextWrapping = TextWrapping.NoWrap;
        advancedConditionBox.VerticalContentAlignment = VerticalAlignment.Top;
        advancedConditionBox.Padding = new Thickness(10, 7, 10, 7);
        advancedConditionBox.Height = EditorListHeight;
        advancedConditionBox.Visibility = Visibility.Collapsed;
        ScrollViewer.SetHorizontalScrollBarVisibility(advancedConditionBox, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(advancedConditionBox, ScrollBarVisibility.Auto);
        advancedConditionBox.TextChanged += (_, _) => UpdateAdvancedConditionFromText();

        var editorHost = new Grid { Height = EditorListHeight };
        editorHost.Children.Add(conditionList);
        editorHost.Children.Add(advancedConditionBox);
        panel.Children.Add(editorHost);

        var actions = new UniformGrid { Columns = 2, Margin = new Thickness(0, 8, 0, 0) };
        removeConditionButton = CreateCompactActionButton(Localize("Remove selected condition"));
        removeConditionButton.Margin = new Thickness(0, 0, 5, 0);
        removeConditionButton.Click += (_, _) => RemoveSelectedFact();
        advancedConditionButton = CreateCompactActionButton(Localize("Switch to advanced"));
        advancedConditionButton.Click += (_, _) => ToggleAdvancedConditionMode();
        actions.Children.Add(removeConditionButton);
        actions.Children.Add(advancedConditionButton);
        panel.Children.Add(actions);

        return panel;
    }

    private static WpfButton CreateCompactActionButton(string text)
    {
        WpfButton button = WpfSettingsUi.CreateButton(text);
        button.MinWidth = 0;
        button.Padding = new Thickness(6, 0, 6, 0);
        button.HorizontalContentAlignment = WpfHorizontalAlignment.Stretch;
        SetCompactActionButtonText(button, text);
        return button;
    }

    private static void SetCompactActionButtonText(WpfButton button, string text)
    {
        button.Content = new TextBlock
        {
            Text = text,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        button.ToolTip = text;
    }

    private static Grid CreateTopPropertiesGrid()
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int index = 0; index < 3; index++)
        {
            var row = new RowDefinition { Height = GridLength.Auto };
            row.SetResourceReference(RowDefinition.MinHeightProperty, "SettingRowHeight");
            grid.RowDefinitions.Add(row);
        }
        return grid;
    }

    private static void AddCompactPropRow(Grid grid, int row, string labelText, FrameworkElement editor)
    {
        TextBlock label = WpfSettingsUi.CreateRowLabel(labelText);
        label.Margin = new Thickness(0, 0, 6, 0);
        Grid.SetRow(label, row);
        Grid.SetColumn(label, 0);
        Grid.SetRow(editor, row);
        Grid.SetColumn(editor, 1);
        grid.Children.Add(label);
        grid.Children.Add(editor);
    }

    private static FrameworkElement AlignRight(FrameworkElement element)
    {
        element.HorizontalAlignment = WpfHorizontalAlignment.Right;
        return element;
    }

    private static WpfListBox CreateEditorListBox()
    {
        var list = new WpfListBox
        {
            Height = EditorListHeight,
            HorizontalContentAlignment = WpfHorizontalAlignment.Stretch
        };
        VirtualizingPanel.SetIsVirtualizing(list, true);
        VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
        return list;
    }

    // ==================== Target Library ====================

    private void RefreshTargetList()
    {
        string query = targetSearchBox.Text.Trim();
        string targetKind = GetSelectedTargetKind();
        targetList.Items.Clear();

        List<SplitTargetDefinition> targets = SplitTargetSearch.QueryTargets(query, targetKind)
            .Take(MaxTargetSearchResults + 1)
            .ToList();
        if (targets.Count > MaxTargetSearchResults)
        {
            targetList.Items.Add(Localize("Too many results"));
            LoadSelectedConditionSettings();
            return;
        }

        string language = Draft.General.Language;
        foreach (SplitTargetDefinition target in targets)
        {
            targetList.Items.Add(new SplitTargetListItem(target, SplitTargetListController.FormatTargetListItem(target, language)));
        }

        LoadSelectedConditionSettings();
    }

    private string GetSelectedTargetKind()
    {
        return targetKindBox.SelectedItem is SplitTargetKindOption option
            ? option.Value
            : SplitTargetKind.Boss;
    }

    private bool TryGetSelectedTarget(out SplitTargetDefinition target)
    {
        if (targetList.SelectedItem is SplitTargetListItem selected)
        {
            target = selected.Target;
            return true;
        }

        target = null!;
        return false;
    }

    // ==================== Route List ====================

    private void RefreshRouteList()
    {
        int selected = routeList.SelectedIndex;
        routeController.Refreshing = true;
        try
        {
            routeList.Items.Clear();
            string attachedTag = Localize("Attached group");
            foreach (SplitRouteEntry entry in RouteEntries)
            {
                routeList.Items.Add(CreateRouteListElement(entry, attachedTag));
            }
        }
        finally
        {
            routeController.Refreshing = false;
        }

        if (routeList.Items.Count == 0)
        {
            ClearSelectedRouteControls();
            return;
        }

        routeList.SelectedIndex = Math.Clamp(selected, 0, routeList.Items.Count - 1);
        if (routeList.SelectedIndex != routeController.LoadedEntryIndex)
        {
            LoadSelectedRouteEntry();
        }
    }

    private static WpfListBoxItem CreateRouteListElement(SplitRouteEntry entry, string attachedLabel)
    {
        var panel = new Grid();
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (entry.IsAttached)
        {
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        var nameText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(entry.DisplayName) ? "Unnamed split" : entry.DisplayName,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        nameText.SetResourceReference(
            TextBlock.ForegroundProperty,
            entry.Enabled ? "TextPrimaryBrush" : "TextMutedBrush");
        Grid.SetColumn(nameText, 0);
        panel.Children.Add(nameText);

        if (entry.IsAttached)
        {
            var badge = new Border
            {
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 1, 5, 1),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.SetResourceReference(Border.BackgroundProperty, "SurfaceRaisedBrush");
            var badgeText = new TextBlock
            {
                Text = attachedLabel,
                FontSize = 10.5
            };
            badgeText.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
            badge.Child = badgeText;
            Grid.SetColumn(badge, 1);
            panel.Children.Add(badge);
        }

        return new WpfListBoxItem
        {
            Tag = entry,
            Content = panel
        };
    }

    private void LoadSelectedRouteEntry()
    {
        if (updatingUi)
        {
            return;
        }

        int newIndex = routeList.SelectedIndex;
        if (newIndex == routeController.LoadedEntryIndex)
        {
            return;
        }

        if (!SaveSelectedEntryFromControls())
        {
            RevertRouteSelection();
            return;
        }

        if (newIndex < 0 || newIndex >= RouteEntries.Count)
        {
            ClearSelectedRouteControls();
            return;
        }

        routeController.LoadedEntryIndex = newIndex;
        updatingUi = true;
        try
        {
            SplitRouteEntry entry = RouteEntries[newIndex];
            splitNameBox.Text = entry.DisplayName;
            splitEnabledBox.IsChecked = entry.Enabled;
            splitAttachedBox.IsChecked = entry.IsAttached;
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
        if (routeController.LoadedEntryIndex < 0 ||
            routeController.LoadedEntryIndex >= routeList.Items.Count ||
            routeList.SelectedIndex == routeController.LoadedEntryIndex)
        {
            return;
        }

        updatingUi = true;
        try
        {
            routeList.SelectedIndex = routeController.LoadedEntryIndex;
        }
        finally
        {
            updatingUi = false;
        }
    }

    private void ClearSelectedRouteControls()
    {
        routeController.ClearLoadedEntry();
        updatingUi = true;
        try
        {
            splitNameBox.Text = string.Empty;
            splitEnabledBox.IsChecked = false;
            splitAttachedBox.IsChecked = false;
            splitAttachedBox.IsEnabled = false;
            conditionList.Items.Clear();
            RefreshConditionMatchOptions(1);
            conditionController.CurrentCondition = SplitCondition.AtLeast([], 1);
            conditionController.PreserveCurrentCondition = false;
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
            routeController.LoadedEntryIndex < 0 ||
            routeController.LoadedEntryIndex >= RouteEntries.Count)
        {
            return true;
        }

        if (!TryCommitCurrentEditor())
        {
            return false;
        }

        SplitRouteEntry entry = RouteEntries[routeController.LoadedEntryIndex];
        entry.DisplayName = splitNameBox.Text.Trim();
        entry.Enabled = splitEnabledBox.IsChecked == true;
        entry.IsAttached = splitAttachedBox.IsEnabled && splitAttachedBox.IsChecked == true;
        entry.Condition = GetCurrentCondition();
        entry.IconTargetIds = SplitCatalog.InferTargetIds(entry.Condition).ToList();
        entry.IconOverride = GetCurrentIconOverride();
        entry.UseAdvancedConditionEditor = conditionController.AdvancedMode;
        return true;
    }

    private void UpdateSelectedAttachedAvailability()
    {
        if (routeController.LoadedEntryIndex < 0 || routeController.LoadedEntryIndex >= RouteEntries.Count)
        {
            return;
        }

        bool canAttach = routeDraft.CanEntryAttachToFollowingAnchor(routeController.LoadedEntryIndex);
        bool prev = updatingUi;
        updatingUi = true;
        try
        {
            splitAttachedBox.IsEnabled = canAttach;
            splitAttachedBox.IsChecked = canAttach && RouteEntries[routeController.LoadedEntryIndex].IsAttached;
        }
        finally
        {
            updatingUi = prev;
        }
    }

    private void MarkSelectedEntryDirty()
    {
        if (updatingUi)
        {
            return;
        }

        routeController.MarkDirty();
        if (!SaveSelectedEntryFromControls())
        {
            return;
        }

        routeDraft.NormalizeAttachedRouteFlags();
        UpdateSelectedAttachedAvailability();
        RefreshRouteList();
    }

    private void AddBlankSplit()
    {
        if (!SaveSelectedEntryFromControls())
        {
            return;
        }

        int index = RouteEntries.Count + 1;
        RouteEntries.Add(new SplitRouteEntry
        {
            Id = routeDraft.CreateUniqueSplitId($"split:custom-{index.ToString(CultureInfo.InvariantCulture)}"),
            DisplayName = $"Custom {index.ToString(CultureInfo.InvariantCulture)}",
            Enabled = true,
            IsAttached = false,
            Condition = SplitCondition.AtLeast([], 1),
            IconTargetIds = [],
            UseAdvancedConditionEditor = false
        });
        routeDraft.NormalizeAttachedRouteFlags();
        routeController.MarkDirty();
        RefreshRouteList();
        routeList.SelectedIndex = RouteEntries.Count - 1;
    }

    private void AddTargetToNewGroup()
    {
        if (!TryGetSelectedTarget(out SplitTargetDefinition target))
        {
            return;
        }

        if (!SaveSelectedEntryFromControls())
        {
            return;
        }

        SplitCondition condition = SplitCondition.AtLeast([CreateFactCondition(target)], 1);
        RouteEntries.Add(new SplitRouteEntry
        {
            Id = routeDraft.CreateUniqueSplitId(SplitSettingsRouteIdFactory.CreateSplitId(target)),
            DisplayName = SplitTargetDisplayNames.GetTargetName(target, Draft.General.Language),
            Enabled = true,
            IsAttached = false,
            Condition = condition,
            IconTargetIds = SplitCatalog.InferTargetIds(condition).ToList(),
            UseAdvancedConditionEditor = false
        });

        routeController.MarkDirty();
        routeDraft.NormalizeAttachedRouteFlags();
        RefreshRouteList();
        routeList.SelectedIndex = RouteEntries.Count - 1;
    }

    private void DeleteSelectedSplit()
    {
        int index = routeList.SelectedIndex;
        if (index < 0 || index >= RouteEntries.Count)
        {
            return;
        }

        RouteEntries.RemoveAt(index);
        routeController.ClearLoadedEntry();
        routeController.MarkDirty();
        routeDraft.NormalizeAttachedRouteFlags();
        RefreshRouteList();
        if (routeList.Items.Count > 0)
        {
            routeList.SelectedIndex = Math.Min(index, routeList.Items.Count - 1);
        }
    }

    // ==================== Conditions & Icon Overrides ====================

    private void AddFactToCurrentSplit()
    {
        if (conditionController.AdvancedMode)
        {
            CopySelectedTargetReferenceId();
            return;
        }

        if (!TryGetSelectedTarget(out SplitTargetDefinition target))
        {
            return;
        }

        if (!TryGetSelectedRouteEntry(out SplitRouteEntry entry))
        {
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
        routeController.MarkDirty();
    }

    private void CopySelectedTargetReferenceId()
    {
        if (!TryGetSelectedTarget(out SplitTargetDefinition target))
        {
            return;
        }

        string targetId = SplitTargetTokenFormatter.Format(target);
        FormsClipboard.SetText(targetId);
    }

    private void RemoveSelectedFact()
    {
        if (conditionController.AdvancedMode)
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
        int remaining = conditionList.Items.Count;
        int requiredAfter = matchAll ? remaining : Math.Max(1, selectedRequiredCount - 1);
        RefreshConditionMatchOptions(requiredAfter);
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
        routeController.MarkDirty();
    }

    private void ToggleAdvancedConditionMode()
    {
        if (!conditionController.AdvancedMode)
        {
            SetAdvancedConditionMode(true);
            return;
        }

        if (string.IsNullOrWhiteSpace(advancedConditionBox.Text))
        {
            conditionController.CurrentCondition = SplitCondition.AtLeast([], 1);
            conditionController.PreserveCurrentCondition = true;
            conditionController.AdvancedError = string.Empty;
            SplitIconOverride emptyOverride = GetCurrentIconOverride();
            RenderConditionList(conditionController.CurrentCondition, emptyOverride);
            SetAdvancedConditionMode(false, updateText: false);
            return;
        }

        if (!TryCommitAdvancedConditionText())
        {
            ShowAdvancedConditionWarning(conditionController.AdvancedError);
            return;
        }

        if (!SplitConditionEditorMode.CanUseBasicEditor(GetCurrentCondition()))
        {
            conditionController.AdvancedError = Localize("Advanced condition cannot be converted to basic editor without losing structure.");
            ShowAdvancedConditionWarning(conditionController.AdvancedError);
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
            message = Localize("Invalid advanced condition.");
        }

        conditionController.AdvancedError = message;
        Dialogs.ShowWarning(message, Localize("TerrariaSplit Settings"));
    }

    private void SetAdvancedConditionMode(
        bool enabled,
        bool updateEntry = true,
        bool updateText = true,
        bool markDirty = true)
    {
        conditionController.AdvancedMode = enabled;
        if (updateEntry && TryGetSelectedRouteEntry(out SplitRouteEntry entry) && entry.UseAdvancedConditionEditor != enabled)
        {
            entry.UseAdvancedConditionEditor = enabled;
            if (markDirty)
            {
                routeController.MarkDirty();
            }
        }

        if (enabled && updateText)
        {
            bool prev = conditionController.UpdatingSettings;
            conditionController.UpdatingSettings = true;
            try
            {
                advancedConditionBox.Text = SplitConditionText.Format(GetCurrentCondition(), Draft.General.Language);
            }
            finally
            {
                conditionController.UpdatingSettings = prev;
            }
        }

        advancedConditionBox.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        conditionList.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        SetCompactActionButtonText(advancedConditionButton, Localize(enabled ? "Switch to basic" : "Switch to advanced"));
        UpdateConditionEditorAvailability();
    }

    private void UpdateAdvancedConditionFromText()
    {
        if (updatingUi || conditionController.UpdatingSettings || !conditionController.AdvancedMode)
        {
            return;
        }

        if (!TryCommitAdvancedConditionText())
        {
            return;
        }

        routeController.MarkDirty();
    }

    private bool TryCommitCurrentEditor()
    {
        if (!conditionController.AdvancedMode)
        {
            conditionController.AdvancedError = string.Empty;
            return true;
        }

        if (TryCommitAdvancedConditionText())
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(conditionController.AdvancedError))
        {
            conditionController.AdvancedError = Localize("Invalid advanced condition.");
        }

        return false;
    }

    private bool TryCommitAdvancedConditionText()
    {
        if (!conditionController.AdvancedMode)
        {
            return true;
        }

        if (!SplitConditionText.TryParse(advancedConditionBox.Text, Draft.General.Language, out SplitCondition condition, out string errorMessage))
        {
            conditionController.AdvancedError = errorMessage;
            return false;
        }

        conditionController.AdvancedError = string.Empty;
        conditionController.CurrentCondition = condition;
        conditionController.PreserveCurrentCondition = true;
        if (TryGetSelectedRouteEntry(out SplitRouteEntry entry))
        {
            entry.Condition = GetCurrentCondition();
            entry.IconTargetIds = SplitCatalog.InferTargetIds(entry.Condition).ToList();
            entry.UseAdvancedConditionEditor = conditionController.AdvancedMode;
            SplitIconOverride previousOverride = GetCurrentIconOverride();
            RefreshIconOverrideOptions(previousOverride);
            entry.IconOverride = GetCurrentIconOverride();
        }

        return true;
    }

    private void UpdateConditionEditorAvailability()
    {
        bool basic = !conditionController.AdvancedMode;
        bool hasEntry = TryGetSelectedRouteEntry(out _);
        splitNameBox.IsEnabled = hasEntry;
        splitEnabledBox.IsEnabled = hasEntry;
        iconOverrideBox.IsEnabled = hasEntry;
        advancedConditionBox.IsEnabled = hasEntry;
        advancedConditionButton.IsEnabled = hasEntry;
        conditionMatchModeBox.IsEnabled = hasEntry && basic;
        conditionList.IsEnabled = hasEntry && basic;
        removeConditionButton.IsEnabled = hasEntry && basic;
        addTargetToSelectedGroupButton.IsEnabled = hasEntry;
        SetCompactActionButtonText(addTargetToSelectedGroupButton, Localize(basic ? "Add to selected group" : "Copy ID"));
        LoadSelectedConditionSettings();
    }

    private void LoadSelectedConditionSettings()
    {
        conditionController.UpdatingSettings = true;
        try
        {
            if (!conditionController.AdvancedMode &&
                TryGetSelectedConditionItem(out ConditionListItem item) &&
                SplitCatalog.TryParseItemFactKey(item.Condition.FactKey, out _))
            {
                itemQuantityBox.IsEnabled = true;
                itemQuantityBox.Text = Math.Max(1, item.Condition.Value).ToString(CultureInfo.InvariantCulture);
                return;
            }

            itemQuantityBox.IsEnabled = false;
            itemQuantityBox.Text = string.Empty;
        }
        finally
        {
            conditionController.UpdatingSettings = false;
        }
    }

    private void UpdateSelectedConditionQuantity()
    {
        if (updatingUi || conditionController.UpdatingSettings || conditionController.AdvancedMode)
        {
            return;
        }

        if (!TryGetSelectedConditionItem(out ConditionListItem item) ||
            !SplitCatalog.TryParseItemFactKey(item.Condition.FactKey, out _) ||
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
            routeController.MarkDirty();
        }
    }

    private void UpdateSelectedConditionMatchCount()
    {
        if (updatingUi || conditionController.UpdatingSettings || conditionController.AdvancedMode)
        {
            return;
        }

        if (TryGetSelectedRouteEntry(out SplitRouteEntry entry))
        {
            UseBasicConditionFromList();
            entry.Condition = GetCurrentCondition();
            routeController.MarkDirty();
        }
    }

    private void RefreshConditionMatchOptions(int selectedRequiredCount)
    {
        int conditionCount = conditionList.Items.Count;
        int normalized = conditionCount <= 0 ? 1 : Math.Clamp(selectedRequiredCount, 1, conditionCount);
        bool prev = conditionController.UpdatingSettings;
        conditionController.UpdatingSettings = true;
        try
        {
            conditionMatchModeBox.Items.Clear();
            string allText = Localize("All conditions");
            if (conditionCount <= 0)
            {
                conditionMatchModeBox.Items.Add(new MatchModeOption(1, allText));
            }
            else
            {
                conditionMatchModeBox.Items.Add(new MatchModeOption(conditionCount, allText));
                for (int count = 1; count < conditionCount; count++)
                {
                    conditionMatchModeBox.Items.Add(new MatchModeOption(
                        count,
                        string.Format(CultureInfo.InvariantCulture, Localize("At least {0} conditions"), count)));
                }
            }

            for (int i = 0; i < conditionMatchModeBox.Items.Count; i++)
            {
                if (conditionMatchModeBox.Items[i] is MatchModeOption option && option.RequiredCount == normalized)
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
            conditionController.UpdatingSettings = prev;
        }
    }

    private void RenderConditionList(SplitCondition condition, SplitIconOverride? selectedOverride = null)
    {
        conditionController.CurrentCondition = (condition ?? SplitCondition.All([])).Clone();
        conditionController.CurrentCondition.Normalize();
        conditionController.PreserveCurrentCondition = true;
        SplitCondition flat = conditionController.CurrentCondition.ToFlatGroup();
        bool prev = conditionController.UpdatingSettings;
        conditionController.UpdatingSettings = true;
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
            conditionController.UpdatingSettings = prev;
        }

        RefreshIconOverrideOptions(selectedOverride);
    }

    private void RefreshIconOverrideOptions(SplitIconOverride? selectedOverride = null)
    {
        SplitIconOverride iconOverride = selectedOverride ?? GetCurrentIconOverride();
        string source = SplitIconOverrideSource.Normalize(iconOverride.Source);
        string targetId = iconOverride.TargetId?.Trim() ?? string.Empty;
        string filePath = iconOverride.FilePath?.Trim() ?? string.Empty;

        conditionController.UpdatingSettings = true;
        try
        {
            iconOverrideBox.Items.Clear();
            iconOverrideBox.Items.Add(new IconOverrideOption(SplitIconOverrideSource.All, string.Empty, Localize("All icons")));

            foreach (SplitTargetDefinition target in GetCurrentConditionTargets())
            {
                iconOverrideBox.Items.Add(new IconOverrideOption(
                    SplitIconOverrideSource.Target,
                    target.Id,
                    FormatSingleIconOption(SplitTargetListController.FormatTargetListItem(target, Draft.General.Language))));
            }

            iconOverrideBox.Items.Add(new IconOverrideOption(
                SplitIconOverrideSource.CustomFile,
                string.Empty,
                FormatSingleIconOption(Localize("Custom image"))));

            IconOverrideOption? selected = null;
            if (source == SplitIconOverrideSource.Target)
            {
                selected = iconOverrideBox.Items
                    .Cast<IconOverrideOption>()
                    .FirstOrDefault(o =>
                        o.Source == SplitIconOverrideSource.Target &&
                        string.Equals(o.TargetId, targetId, StringComparison.OrdinalIgnoreCase));
            }
            else if (source == SplitIconOverrideSource.CustomFile)
            {
                selected = iconOverrideBox.Items
                    .Cast<IconOverrideOption>()
                    .FirstOrDefault(o => o.Source == SplitIconOverrideSource.CustomFile);
            }

            iconOverrideBox.SelectedItem = selected ?? iconOverrideBox.Items[0];
            iconOverrideFileBox.Text = source == SplitIconOverrideSource.CustomFile ? filePath : string.Empty;
            RefreshAllIconFileRows(iconOverride);
        }
        finally
        {
            conditionController.UpdatingSettings = false;
        }
    }

    private void RefreshAllIconFileRows(SplitIconOverride iconOverride)
    {
        IReadOnlyDictionary<string, string> filePaths = iconOverride.AllIconFilePaths ?? new Dictionary<string, string>();
        allIconFilesBody.Children.Clear();
        allIconFileBoxes.Clear();

        IReadOnlyList<SplitTargetDefinition> targets = GetCurrentConditionTargets();
        for (int i = 0; i < targets.Count; i++)
        {
            SplitTargetDefinition target = targets[i];
            string value = filePaths.FirstOrDefault(pair =>
                string.Equals(pair.Key, target.Id, StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;

            Grid row = WpfSettingsUi.CreateColumnGrid(
                new GridLength(1.0, GridUnitType.Star),
                new GridLength(1.4, GridUnitType.Star),
                GridLength.Auto,
                GridLength.Auto);

            TextBlock label = WpfSettingsUi.CreateRowLabel(SplitTargetDisplayNames.GetTargetName(target, Draft.General.Language));
            label.Margin = new Thickness(0, 0, 10, 0);

            WpfTextBox box = WpfSettingsUi.CreateTextBox(value);
            box.Margin = new Thickness(0, 0, 6, 0);
            box.TextChanged += (_, _) =>
            {
                if (!updatingUi && !conditionController.UpdatingSettings)
                {
                    MarkSelectedEntryDirty();
                }
            };

            WpfButton browse = WpfSettingsUi.CreateButton(Localize("Browse"), minWidth: 64);
            browse.Margin = new Thickness(0, 0, 6, 0);
            browse.Click += (_, _) => Dialogs.PickBossIcon(box);

            WpfButton clear = WpfSettingsUi.CreateButton(Localize("Clear"), ghost: true, minWidth: 56);
            clear.Click += (_, _) => box.Text = string.Empty;

            Grid.SetColumn(label, 0);
            Grid.SetColumn(box, 1);
            Grid.SetColumn(browse, 2);
            Grid.SetColumn(clear, 3);
            row.Children.Add(label);
            row.Children.Add(box);
            row.Children.Add(browse);
            row.Children.Add(clear);

            allIconFilesBody.Children.Add(WpfSettingsUi.CreateRowDividerContainer(row, showBottomBorder: i < targets.Count - 1));
            allIconFileBoxes[target.Id] = box;
        }

        UpdateAllIconFilesSectionVisibility();
    }

    private void UpdateAllIconFilesSectionVisibility()
    {
        allIconFilesSectionExpanded =
            iconOverrideBox.SelectedItem is IconOverrideOption option &&
            option.Source == SplitIconOverrideSource.All &&
            allIconFileBoxes.Count > 0;
        allIconFilesCard.Visibility = allIconFilesSectionExpanded ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PickCustomIconOverrideFileIfSelected()
    {
        if (iconOverrideBox.SelectedItem is not IconOverrideOption option ||
            option.Source != SplitIconOverrideSource.CustomFile)
        {
            return;
        }

        string previousPath = iconOverrideFileBox.Text.Trim();
        if (Dialogs.PickBossIcon(iconOverrideFileBox))
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

    private void SetIconOverrideSource(string source)
    {
        IconOverrideOption? option = iconOverrideBox.Items
            .Cast<IconOverrideOption>()
            .FirstOrDefault(item => item.Source == source);
        if (option is not null)
        {
            iconOverrideBox.SelectedItem = option;
        }
    }

    private string FormatSingleIconOption(string iconName) =>
        string.Format(CultureInfo.InvariantCulture, Localize("Single icon: {0}"), iconName);

    private SplitIconOverride GetCurrentIconOverride()
    {
        Dictionary<string, string> allIconFilePaths = allIconFileBoxes
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Value.Text))
            .ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.Text.Trim(),
                StringComparer.OrdinalIgnoreCase);

        if (iconOverrideBox.SelectedItem is not IconOverrideOption option)
        {
            return new SplitIconOverride { AllIconFilePaths = allIconFilePaths };
        }

        return option.Source switch
        {
            SplitIconOverrideSource.Target => new SplitIconOverride
            {
                Source = SplitIconOverrideSource.Target,
                TargetId = option.TargetId,
                FilePath = string.Empty,
                AllIconFilePaths = allIconFilePaths
            },
            SplitIconOverrideSource.CustomFile => new SplitIconOverride
            {
                Source = SplitIconOverrideSource.CustomFile,
                TargetId = string.Empty,
                FilePath = iconOverrideFileBox.Text.Trim(),
                AllIconFilePaths = allIconFilePaths
            },
            _ => new SplitIconOverride { AllIconFilePaths = allIconFilePaths }
        };
    }

    private IReadOnlyList<SplitTargetDefinition> GetCurrentConditionTargets()
    {
        var targets = new List<SplitTargetDefinition>();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string factKey in GetCurrentCondition().GetFactKeys())
        {
            if (SplitCatalog.TryGetTargetByFactKey(factKey, out SplitTargetDefinition target) && seen.Add(target.Id))
            {
                targets.Add(target);
            }
        }

        return targets;
    }

    private SplitCondition GetCurrentCondition() =>
        conditionController.PreserveCurrentCondition
            ? conditionController.CurrentCondition.Clone()
            : BuildConditionFromList();

    private SplitCondition BuildConditionFromList()
    {
        IEnumerable<SplitCondition> facts = conditionList.Items
            .Cast<ConditionListItem>()
            .Select(item => item.Condition);
        return SplitCondition.AtLeast(facts, GetConditionMatchCountFromSelection());
    }

    private void UseBasicConditionFromList()
    {
        conditionController.PreserveCurrentCondition = false;
        conditionController.CurrentCondition = BuildConditionFromList();
    }

    private int GetConditionMatchCountFromSelection()
    {
        int count = conditionList.Items.Count;
        if (count <= 0)
        {
            return 1;
        }

        return conditionMatchModeBox.SelectedItem is MatchModeOption option
            ? Math.Clamp(option.RequiredCount, 1, count)
            : count;
    }

    private bool IsAllMatchModeSelected()
    {
        int count = Math.Max(1, conditionList.Items.Count);
        return conditionMatchModeBox.SelectedItem is not MatchModeOption option || option.RequiredCount >= count;
    }

    private ConditionListItem CreateConditionListItem(SplitCondition condition)
    {
        string text = SplitCatalog.TryGetTargetByFactKey(condition.FactKey, out _)
            ? SplitTargetDisplayNames.FormatFact(condition, Draft.General.Language)
            : $"Fact: {condition.FactKey}";
        return new ConditionListItem(condition, text);
    }

    private bool TryGetSelectedRouteEntry(out SplitRouteEntry entry)
    {
        entry = null!;
        if (routeController.LoadedEntryIndex < 0 || routeController.LoadedEntryIndex >= RouteEntries.Count)
        {
            return false;
        }

        entry = RouteEntries[routeController.LoadedEntryIndex];
        return true;
    }

    private bool TryGetSelectedConditionItem(out ConditionListItem item)
    {
        item = null!;
        if (conditionList.SelectedIndex < 0 ||
            conditionList.SelectedIndex >= conditionList.Items.Count ||
            conditionList.Items[conditionList.SelectedIndex] is not ConditionListItem selected)
        {
            return false;
        }

        item = selected;
        return true;
    }

    private static SplitCondition CreateFactCondition(SplitTargetDefinition target)
    {
        if (target.Kind != SplitTargetKind.Item)
        {
            if (target.Kind == SplitTargetKind.Npc && SplitCatalog.TryParseNpcTargetId(target.Id, out int npcId))
            {
                return SplitCatalog.CreateNpcPresentCondition(npcId);
            }

            if (target.Kind == SplitTargetKind.Biome && SplitCatalog.TryParseBiomeTargetId(target.Id, out string? biomeId))
            {
                return SplitCatalog.CreateBiomeActiveCondition(biomeId);
            }

            return SplitCondition.Fact(target.FactKey);
        }

        return SplitCatalog.TryParseItemTargetId(target.Id, out int itemId)
            ? SplitCatalog.CreateItemEverOwnedCondition(itemId, 1)
            : SplitCondition.Fact(target.FactKey, SplitFactComparison.AtLeast, 1);
    }

    // ==================== WPF Drag & Drop Reordering ====================

    private void RouteListPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        routeDragIndex = FindItemIndexAtSource(routeList, e.OriginalSource as DependencyObject);
        routeDragStart = e.GetPosition(routeList);
    }

    private void RouteListPreviewMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (routeDragIndex < 0 || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        WpfPoint current = e.GetPosition(routeList);
        if (!ExceededDragDistance(routeDragStart, current))
        {
            return;
        }

        int index = routeDragIndex;
        routeDragIndex = -1;
        if (!SaveSelectedEntryFromControls())
        {
            return;
        }

        DragDrop.DoDragDrop(routeList, new System.Windows.DataObject(typeof(RouteDragItem), new RouteDragItem(index)), WpfDragDropEffects.Move);
    }

    private void RouteListDragOver(object sender, WpfDragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(RouteDragItem)) ? WpfDragDropEffects.Move : WpfDragDropEffects.None;
        e.Handled = true;
    }

    private void RouteListDrop(object sender, WpfDragEventArgs e)
    {
        if (e.Data.GetData(typeof(RouteDragItem)) is not RouteDragItem drag)
        {
            return;
        }

        int insertionIndex = GetInsertionIndex(routeList, e.GetPosition(routeList));
        MoveRouteEntry(drag.Index, insertionIndex);
    }

    private void MoveRouteEntry(int sourceIndex, int insertionIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= RouteEntries.Count ||
            insertionIndex == sourceIndex || insertionIndex == sourceIndex + 1)
        {
            return;
        }

        SplitRouteEntry entry = RouteEntries[sourceIndex];
        RouteEntries.RemoveAt(sourceIndex);
        if (insertionIndex > sourceIndex)
        {
            insertionIndex--;
        }

        insertionIndex = Math.Clamp(insertionIndex, 0, RouteEntries.Count);
        RouteEntries.Insert(insertionIndex, entry);
        routeController.ClearLoadedEntry();
        routeController.MarkDirty();
        routeDraft.NormalizeAttachedRouteFlags();
        RefreshRouteList();
        routeList.SelectedIndex = insertionIndex;
    }

    private void ConditionListPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (conditionController.AdvancedMode)
        {
            return;
        }

        conditionDragIndex = FindItemIndexAtSource(conditionList, e.OriginalSource as DependencyObject);
        conditionDragStart = e.GetPosition(conditionList);
    }

    private void ConditionListPreviewMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (conditionController.AdvancedMode || conditionDragIndex < 0 || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        WpfPoint current = e.GetPosition(conditionList);
        if (!ExceededDragDistance(conditionDragStart, current))
        {
            return;
        }

        int index = conditionDragIndex;
        conditionDragIndex = -1;
        conditionList.SelectedIndex = index;
        DragDrop.DoDragDrop(conditionList, new System.Windows.DataObject(typeof(ConditionDragItem), new ConditionDragItem(index)), WpfDragDropEffects.Move);
    }

    private void ConditionListDragOver(object sender, WpfDragEventArgs e)
    {
        e.Effects = !conditionController.AdvancedMode && e.Data.GetDataPresent(typeof(ConditionDragItem))
            ? WpfDragDropEffects.Move
            : WpfDragDropEffects.None;
        e.Handled = true;
    }

    private void ConditionListDrop(object sender, WpfDragEventArgs e)
    {
        if (conditionController.AdvancedMode || e.Data.GetData(typeof(ConditionDragItem)) is not ConditionDragItem drag)
        {
            return;
        }

        int insertionIndex = GetInsertionIndex(conditionList, e.GetPosition(conditionList));
        MoveConditionFact(drag.Index, insertionIndex);
    }

    private void MoveConditionFact(int sourceIndex, int insertionIndex)
    {
        if (conditionController.AdvancedMode ||
            !TryGetSelectedRouteEntry(out SplitRouteEntry entry) ||
            sourceIndex < 0 || sourceIndex >= conditionList.Items.Count ||
            insertionIndex == sourceIndex || insertionIndex == sourceIndex + 1)
        {
            return;
        }

        List<ConditionListItem> items = conditionList.Items.Cast<ConditionListItem>().ToList();
        ConditionListItem item = items[sourceIndex];
        items.RemoveAt(sourceIndex);
        if (insertionIndex > sourceIndex)
        {
            insertionIndex--;
        }

        insertionIndex = Math.Clamp(insertionIndex, 0, items.Count);
        items.Insert(insertionIndex, item);

        conditionList.Items.Clear();
        foreach (ConditionListItem conditionItem in items)
        {
            conditionList.Items.Add(conditionItem);
        }

        conditionList.SelectedIndex = insertionIndex;
        UseBasicConditionFromList();
        entry.Condition = GetCurrentCondition();
        entry.IconTargetIds = SplitCatalog.InferTargetIds(entry.Condition).ToList();
        SplitIconOverride previousOverride = GetCurrentIconOverride();
        RefreshIconOverrideOptions(previousOverride);
        entry.IconOverride = GetCurrentIconOverride();
        routeController.MarkDirty();
    }

    private static int FindItemIndexAtSource(WpfListBox listBox, DependencyObject? source)
    {
        DependencyObject? current = source;
        while (current is not null && current != listBox)
        {
            if (current is WpfListBoxItem item)
            {
                return listBox.ItemContainerGenerator.IndexFromContainer(item);
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return -1;
    }

    private static int GetInsertionIndex(WpfListBox listBox, WpfPoint position)
    {
        HitTestResult? hit = VisualTreeHelper.HitTest(listBox, position);
        int index = FindItemIndexAtSource(listBox, hit?.VisualHit);
        if (index < 0)
        {
            return listBox.Items.Count;
        }

        if (listBox.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement container)
        {
            WpfPoint local = listBox.TranslatePoint(position, container);
            return local.Y > container.ActualHeight / 2d ? index + 1 : index;
        }

        return index;
    }

    private static bool ExceededDragDistance(WpfPoint start, WpfPoint current) =>
        Math.Abs(current.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance ||
        Math.Abs(current.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance;

    private sealed record ConditionListItem(SplitCondition Condition, string DisplayText)
    {
        public override string ToString() => DisplayText;
    }

    private sealed record MatchModeOption(int RequiredCount, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private sealed record IconOverrideOption(string Source, string TargetId, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private readonly record struct RouteDragItem(int Index);

    private readonly record struct ConditionDragItem(int Index);
}
