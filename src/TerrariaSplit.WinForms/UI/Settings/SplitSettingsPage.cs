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
    private SplitSettingsCommitService commitService = null!;
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
    private TableLayoutPanel allIconFilesSection = null!;
    private TableLayoutPanel allIconFilesGrid = null!;
    private readonly Dictionary<string, TextBox> allIconFileBoxes = new(StringComparer.OrdinalIgnoreCase);
    private ListBox conditionList = null!;
    private TextBox advancedConditionBox = null!;
    private Panel conditionEditorFrame = null!;
    private Button addTargetToSelectedGroupButton = null!;
    private Button removeConditionButton = null!;
    private Button addTargetToNewGroupButton = null!;
    private Button advancedConditionButton = null!;
    private Label statusLabel = null!;
    private bool updatingUi;
    private bool allIconFilesSectionExpanded;

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

    internal bool AllIconFilesSectionVisibleForTests => allIconFilesSectionExpanded;

    internal IReadOnlyDictionary<string, TextBox> AllIconFileBoxesForTests => allIconFileBoxes;

    internal ThemedDropDownList ConditionMatchModeBoxForTests => conditionMatchModeBox;

    internal Button AddTargetToSelectedGroupButtonForTests => addTargetToSelectedGroupButton;

    internal Button AddTargetToNewGroupButtonForTests => addTargetToNewGroupButton;

    internal Button AdvancedConditionButtonForTests => advancedConditionButton;

    internal TextBox AdvancedConditionBoxForTests => advancedConditionBox;

    internal bool AdvancedConditionModeForTests => conditionController.AdvancedMode;

    protected override Control BuildPage(SettingsPageContext context)
    {
        commitService = new SplitSettingsCommitService(
            routeDraft,
            routeController,
            SaveSelectedEntryFromControls,
            () => conditionController.AdvancedError,
            context.Localize,
            context.NotifyModelChanged);
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
        SplitCommitResult result = commitService.CommitTo(settings, SplitCommitMode.StrictApply);
        statusLabel.Text = result.Message;
        if (!result.Succeeded)
        {
            throw new SettingsApplyFailedException(result.Message);
        }
    }

    public override void OnDeselected()
    {
        SplitCommitResult result = commitService.CommitTo(Draft, SplitCommitMode.LenientDeselection);
        if (!result.Succeeded || result.RouteChanged)
        {
            statusLabel.Text = result.Message;
        }
    }





}
