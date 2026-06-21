using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class SplitSettingsPage : SettingsPageBase
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





}
