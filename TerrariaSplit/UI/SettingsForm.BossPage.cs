using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed partial class SettingsForm : Form
{

    internal void AddRouteSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("BOSS Groups");
        TableLayoutPanel grid = CreateGrid(
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(124f),
            ColumnStyleAbsolute(96f));

        AddHeaderRow(grid, "BOSS", "Enabled", "Group");

        IReadOnlyDictionary<string, BossRouteEntry> route = settings.Route.ToDictionary(
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
                ForeColor = TextColor,
                TextAlign = ContentAlignment.MiddleCenter
            };
            UiTheme.StyleCheckBox(enabledBox);

            TextBox groupBox = CreateTextBox(Math.Clamp(entry.Segment, 1m, 99m).ToString("0.#", CultureInfo.InvariantCulture));
            enabledBox.CheckedChanged += (_, _) => bossRouteDirty = true;
            groupBox.TextChanged += (_, _) => bossRouteDirty = true;
            routeControls[unit.Id] = new RouteControls(enabledBox, groupBox);

            int row = AddGridRow(grid);
            grid.Controls.Add(CreateRowLabel(Localizer.Get(unit.DisplayName, settings)), 0, row);
            grid.Controls.Add(enabledBox, 1, row);
            grid.Controls.Add(groupBox, 2, row);
        }

        AddSectionControl(section, grid);
        AddSection(parent, section);
    }


    internal void AddBossIconSection(TableLayoutPanel parent)
    {
        TableLayoutPanel section = CreateSection("BOSS Icons");
        TableLayoutPanel grid = CreateGrid(
            ColumnStyleAbsolute(260f),
            ColumnStylePercent(100f),
            ColumnStyleAbsolute(156f));

        foreach (BossUnitDefinition unit in BossSplitDefinitions.Units)
        {
            TextBox textBox = CreateTextBox(settings.GetBossIconPath(unit.Id));
            textBox.PlaceholderText = Localizer.Get("empty = bundled icon", settings);
            bossIconTextBoxes[unit.Id] = textBox;

            Button browseButton = CreateButton("Browse", accent: false, minimumWidth: 140);
            browseButton.Margin = new Padding(8, 2, 0, 2);
            browseButton.Click += (_, _) => PickBossIcon(textBox);

            int row = AddGridRow(grid);
            grid.Controls.Add(CreateRowLabel(Localizer.Get(unit.DisplayName, settings)), 0, row);
            grid.Controls.Add(textBox, 1, row);
            grid.Controls.Add(browseButton, 2, row);
        }

        AddSectionControl(section, grid);
        AddSection(parent, section);
    }


    private bool ApplyBossPageRouteChanges()
    {
        if (!bossRouteDirty)
        {
            return false;
        }

        ApplyRouteSettings();
        AppSettingsStore.Normalize(settings);
        bossRouteDirty = false;
        PopulatePersonalBestTimeGrid();
        PopulatePersonalBestSegmentGrid();
        RefreshAnimationOutlineGrid();
        PopulateSegmentBestDeltaHighlightGrid();
        return true;
    }


    private void ApplyRouteSettings()
    {
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
                Segment = ParseRouteGroup(controls.Group.Text)
            });
        }

        settings.Route = route;
    }


    private IReadOnlyList<BossRouteEntry> GetRouteOrderedEntries()
    {
        return settings.Route
            .Select((entry, index) => new { Entry = entry, Index = index })
            .OrderBy(item => item.Entry.Segment)
            .ThenBy(item => item.Index)
            .Select(item => item.Entry)
            .ToList();
    }
}
