using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class BossSettingsPage : SettingsPageBase
{
    private readonly Dictionary<string, RouteControls> routeControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> bossIconTextBoxes = new(StringComparer.OrdinalIgnoreCase);
    private bool bossRouteDirty;

    public override SettingsPageId Id => SettingsPageId.Boss;

    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(content =>
        {
            AddRouteSection(content);
            AddBossIconSection(content);
        });
    }

    public override void Apply(AppSettings settings)
    {
        bool routeChanged = SaveRouteSettings();
        foreach ((string name, TextBox textBox) in bossIconTextBoxes)
        {
            settings.SetBossIconPath(name, textBox.Text.Trim());
        }

        if (routeChanged)
        {
            Context.NotifyModelChanged(SettingsModelChange.RouteChanged);
        }
    }

    public override void OnDeselected()
    {
        if (SaveRouteSettings())
        {
            Context.NotifyModelChanged(SettingsModelChange.RouteChanged);
        }
    }

    private void AddRouteSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = Factory.CreateSection("BOSS Groups");
        TableLayoutPanel grid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(124f),
            SettingsUiFactory.ColumnStyleAbsolute(96f));

        Factory.AddHeaderRow(grid, "BOSS", "Enabled", "Group");

        IReadOnlyDictionary<string, BossRouteEntry> route = Draft.Route.ToDictionary(
            entry => entry.BossId,
            StringComparer.OrdinalIgnoreCase);

        foreach (BossUnitDefinition unit in BossSplitDefinitions.Units)
        {
            BossRouteEntry entry = route.TryGetValue(unit.Id, out BossRouteEntry? existing)
                ? existing
                : new BossRouteEntry { BossId = unit.Id };

            var enabledBox = new CheckBox
            {
                Checked = entry.Enabled,
                Dock = DockStyle.Fill,
                ForeColor = UiTheme.Text,
                TextAlign = ContentAlignment.MiddleCenter
            };
            UiTheme.StyleCheckBox(enabledBox);

            TextBox groupBox = Factory.CreateTextBox(Math.Clamp(entry.Segment, 1m, 99m).ToString("0.#", CultureInfo.InvariantCulture));
            enabledBox.CheckedChanged += (_, _) => bossRouteDirty = true;
            groupBox.TextChanged += (_, _) => bossRouteDirty = true;
            routeControls[unit.Id] = new RouteControls(enabledBox, groupBox);

            int row = Factory.AddGridRow(grid);
            grid.Controls.Add(Factory.CreateRowLabel(Context.Localize(unit.DisplayName)), 0, row);
            grid.Controls.Add(enabledBox, 1, row);
            grid.Controls.Add(groupBox, 2, row);
        }

        SettingsUiFactory.AddSectionControl(section, grid);
        SettingsUiFactory.AddSection(parent, section);
    }

    private void AddBossIconSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = Factory.CreateSection("BOSS Icons");
        TableLayoutPanel grid = Factory.CreateGrid(
            SettingsUiFactory.ColumnStyleAbsolute(260f),
            SettingsUiFactory.ColumnStylePercent(100f),
            SettingsUiFactory.ColumnStyleAbsolute(156f));

        foreach (BossUnitDefinition unit in BossSplitDefinitions.Units)
        {
            TextBox textBox = Factory.CreateTextBox(Draft.GetBossIconPath(unit.Id));
            textBox.PlaceholderText = Context.Localize("empty = bundled icon");
            bossIconTextBoxes[unit.Id] = textBox;

            Button browseButton = Factory.CreateButton("Browse", accent: false, minimumWidth: 140);
            browseButton.Margin = new Padding(8, 2, 0, 2);
            browseButton.Click += (_, _) => Dialogs.PickBossIcon(textBox);

            int row = Factory.AddGridRow(grid);
            grid.Controls.Add(Factory.CreateRowLabel(Context.Localize(unit.DisplayName)), 0, row);
            grid.Controls.Add(textBox, 1, row);
            grid.Controls.Add(browseButton, 2, row);
        }

        SettingsUiFactory.AddSectionControl(section, grid);
        SettingsUiFactory.AddSection(parent, section);
    }

    private bool SaveRouteSettings()
    {
        if (!bossRouteDirty)
        {
            return false;
        }

        var route = new List<BossRouteEntry>();
        foreach (BossUnitDefinition unit in BossSplitDefinitions.Units)
        {
            if (!routeControls.TryGetValue(unit.Id, out RouteControls? controls))
            {
                continue;
            }

            route.Add(new BossRouteEntry
            {
                BossId = unit.Id,
                Enabled = controls.Enabled.Checked,
                Segment = SettingsValueParser.ParseRouteGroup(controls.Group.Text)
            });
        }

        Draft.Route = route;
        AppSettingsStore.Normalize(Draft);
        bossRouteDirty = false;
        return true;
    }

    private sealed record RouteControls(CheckBox Enabled, TextBox Group);
}
