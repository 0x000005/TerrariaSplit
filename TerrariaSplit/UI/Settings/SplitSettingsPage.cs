using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class SplitSettingsPage : SettingsPageBase
{
    private const int EditorListHeight = 468;
    private const int TopSettingsRowsHeight = 174;

    private readonly SplitRouteDraft routeDraft = new();
    private SplitTargetListController targetController = null!;
    private ListBox targetList = null!;
    private ThemedDropDownList targetKindBox = null!;
    private TextBox targetSearchBox = null!;
    private TextBox itemQuantityBox = null!;
    private ListBox routeList = null!;
    private TextBox splitNameBox = null!;
    private CheckBox splitEnabledBox = null!;
    private CheckBox splitAttachedBox = null!;
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

    private List<SplitRouteEntry> routeEntries => routeDraft.Entries;

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
        routeDraft.LoadFrom(Draft.Route);

        Control page = context.BuildScrollPage(content =>
        {
            AddEditorSection(content);
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

        routeDraft.EnsureEntryIds();
        routeDraft.NormalizeAttachedRouteFlags();
        if (TryValidateRoute(out string validationMessage))
        {
            settings.Route.SplitRoute = routeDraft.CreateSnapshot();

            AppSettingsStore.Normalize(settings);
            statusLabel.Text = string.Empty;
            if (routeDirty)
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

        routeDraft.EnsureEntryIds();
        routeDraft.NormalizeAttachedRouteFlags();
        string validationMessage = string.Empty;
        if (routeDirty && TryValidateRoute(out validationMessage))
        {
            Draft.Route.SplitRoute = routeDraft.CreateSnapshot();
            AppSettingsStore.Normalize(Draft);
            Context.NotifyModelChanged(SettingsModelChange.RouteChanged);
            statusLabel.Text = string.Empty;
            routeDirty = false;
            return;
        }

        if (routeDirty)
        {
            statusLabel.Text = validationMessage;
        }
    }





}
