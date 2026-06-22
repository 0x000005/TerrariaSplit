using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed partial class SplitSettingsPage : SettingsPageBase
{
    private const int EditorListHeight = 468;
    private const int TopSettingsRowsHeight = 174;

    private readonly SplitRouteDraft routeDraft = new();
    private readonly SplitRouteListController routeController = new();
    private readonly SplitConditionEditorController conditionController = new();
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
    private bool updatingUi;

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

    internal bool AdvancedConditionModeForTests => conditionController.AdvancedMode;

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
            throw new SettingsApplyFailedException(conditionController.AdvancedError);
        }

        routeDraft.EnsureEntryIds();
        routeDraft.NormalizeAttachedRouteFlags();
        if (TryValidateRoute(out string validationMessage))
        {
            settings.Route.SplitRoute = routeDraft.CreateSnapshot();

            SettingsNormalizer.Normalize(settings);
            statusLabel.Text = string.Empty;
            if (routeController.Dirty)
            {
                Context.NotifyModelChanged(SettingsModelChange.RouteChanged);
            }

            routeController.ClearDirty();
            return;
        }

        statusLabel.Text = validationMessage;
        throw new SettingsApplyFailedException(validationMessage);
    }

    public override void OnDeselected()
    {
        if (!SaveSelectedEntryFromControls())
        {
            statusLabel.Text = conditionController.AdvancedError;
            return;
        }

        routeDraft.EnsureEntryIds();
        routeDraft.NormalizeAttachedRouteFlags();
        string validationMessage = string.Empty;
        if (routeController.Dirty && TryValidateRoute(out validationMessage))
        {
            Draft.Route.SplitRoute = routeDraft.CreateSnapshot();
            SettingsNormalizer.Normalize(Draft);
            Context.NotifyModelChanged(SettingsModelChange.RouteChanged);
            statusLabel.Text = string.Empty;
            routeController.ClearDirty();
            return;
        }

        if (routeController.Dirty)
        {
            statusLabel.Text = validationMessage;
        }
    }





}
