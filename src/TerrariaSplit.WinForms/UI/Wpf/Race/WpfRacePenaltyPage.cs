using System.Windows;
using System.Windows.Controls;
using TerrariaSplit.Race.Contracts;
using TerrariaSplit.UI.Wpf.Settings;
using WpfButton = System.Windows.Controls.Button;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace TerrariaSplit.UI.Wpf.Race;

internal sealed class WpfRacePenaltyPage
{
    private const double NumColWidth = 74;

    private readonly IRacePanelShell shell;
    private readonly Dictionary<string, BossPenaltyControls> penaltyControls = new(StringComparer.OrdinalIgnoreCase);
    private RaceBossPenaltySettings bossPenaltySettings;
    private WpfButton? applyButton;

    public WpfRacePenaltyPage(IRacePanelShell shell)
    {
        this.shell = shell;
        bossPenaltySettings = AppSettingsCloner.CloneRaceBossPenaltySettings(shell.BossPenaltySettings);
    }

    public FrameworkElement Build()
    {
        return WpfSettingsUi.CreateScrollPage(parent =>
        {
            var card = WpfSettingsUi.CreateCard(
                Localize("Penalty"),
                out StackPanel body);

            body.Children.Add(CreateHeaderRow());

            IReadOnlyList<RaceBossPenaltyDescriptor> bosses = RaceBossPenaltyConfiguration.Bosses;
            for (int i = 0; i < bosses.Count; i++)
            {
                RaceBossPenaltyDescriptor desc = bosses[i];
                body.Children.Add(CreateBossRow(desc, desc.GetSettings(bossPenaltySettings), isLast: i == bosses.Count - 1));
            }

            applyButton = WpfSettingsUi.CreateButton(Localize("Apply penalty settings"), minWidth: 120);
            applyButton.HorizontalAlignment = WpfHorizontalAlignment.Left;
            applyButton.Margin = new Thickness(0, 10, 0, 10);
            applyButton.Click += (_, _) => SaveBossPenaltySettings();
            body.Children.Add(applyButton);

            parent.Children.Add(card);
            UpdateState(shell.State);
        });
    }

    public void UpdateState(RaceRoomState? state)
    {
        bool enabled = state is null || state.Status == RaceRoomStatus.Closed;
        foreach (BossPenaltyControls controls in penaltyControls.Values)
        {
            foreach (WpfTextBox box in controls.GetAll())
            {
                box.IsEnabled = enabled;
            }
        }

        if (applyButton is not null)
        {
            applyButton.IsEnabled = enabled;
        }
    }

    private static Grid CreateRowGrid()
    {
        return WpfSettingsUi.CreateColumnGrid(
            new GridLength(1, GridUnitType.Star),
            new GridLength(NumColWidth),
            new GridLength(NumColWidth),
            new GridLength(NumColWidth),
            new GridLength(NumColWidth),
            new GridLength(NumColWidth),
            new GridLength(NumColWidth),
            new GridLength(NumColWidth),
            new GridLength(NumColWidth));
    }

    private Border CreateHeaderRow()
    {
        Grid grid = CreateRowGrid();
        string[] labels =
        [
            Localize("BOSS"),
            Localize("Journey base"),
            Localize("Journey proportional"),
            Localize("Classic base"),
            Localize("Classic proportional"),
            Localize("Expert base"),
            Localize("Expert proportional"),
            Localize("Master base"),
            Localize("Master proportional")
        ];

        for (int i = 0; i < labels.Length; i++)
        {
            TextBlock h = WpfSettingsUi.CreateTableHeaderText(labels[i], i == 0 ? TextAlignment.Left : TextAlignment.Center);
            h.TextWrapping = TextWrapping.Wrap;
            h.TextTrimming = TextTrimming.None;
            h.Margin = i == 0 ? new Thickness(0, 0, 6, 0) : new Thickness(3, 0, 3, 0);
            Grid.SetColumn(h, i);
            grid.Children.Add(h);
        }

        return WpfSettingsUi.CreateTableHeaderContainer(grid);
    }

    private Border CreateBossRow(
        RaceBossPenaltyDescriptor descriptor,
        RaceBossPenaltyBossSettings settings,
        bool isLast)
    {
        int max = RaceBossPenaltySettings.MaximumSeconds;
        var controls = new BossPenaltyControls(
            CreatePenaltyBox(settings.JourneyBaseSeconds, max),
            CreatePenaltyBox(settings.JourneyProportionalSeconds, max),
            CreatePenaltyBox(settings.ClassicBaseSeconds, max),
            CreatePenaltyBox(settings.ClassicProportionalSeconds, max),
            CreatePenaltyBox(settings.ExpertBaseSeconds, max),
            CreatePenaltyBox(settings.ExpertProportionalSeconds, max),
            CreatePenaltyBox(settings.MasterBaseSeconds, max),
            CreatePenaltyBox(settings.MasterProportionalSeconds, max));
        penaltyControls[descriptor.Key] = controls;

        Grid grid = CreateRowGrid();
        TextBlock label = WpfSettingsUi.CreateRowLabel(Localize(descriptor.Label));
        label.Margin = new Thickness(0, 0, 6, 0);
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        WpfTextBox[] boxes = controls.GetAll();
        for (int i = 0; i < boxes.Length; i++)
        {
            Grid.SetColumn(boxes[i], i + 1);
            grid.Children.Add(boxes[i]);
        }

        return WpfSettingsUi.CreateTableRowContainer(grid, showBottomBorder: !isLast);
    }

    private static WpfTextBox CreatePenaltyBox(int value, int max)
    {
        WpfTextBox box = WpfSettingsUi.CreateNumberBox(value, 0, max, width: NumColWidth - 10);
        box.HorizontalAlignment = WpfHorizontalAlignment.Center;
        return box;
    }

    private void SaveBossPenaltySettings()
    {
        if (shell.State is not null && shell.State.Status != RaceRoomStatus.Closed)
        {
            return;
        }

        RaceBossPenaltySettings next = AppSettingsCloner.CloneRaceBossPenaltySettings(bossPenaltySettings);
        int max = RaceBossPenaltySettings.MaximumSeconds;
        foreach (RaceBossPenaltyDescriptor descriptor in RaceBossPenaltyConfiguration.Bosses)
        {
            if (!penaltyControls.TryGetValue(descriptor.Key, out BossPenaltyControls? c))
            {
                continue;
            }

            RaceBossPenaltyBossSettings target = descriptor.GetSettings(next);
            target.JourneyBaseSeconds = WpfSettingsValueParser.ParseIntBox(c.JourneyBase, target.JourneyBaseSeconds, 0, max);
            target.JourneyProportionalSeconds = WpfSettingsValueParser.ParseIntBox(c.JourneyProportional, target.JourneyProportionalSeconds, 0, max);
            target.ClassicBaseSeconds = WpfSettingsValueParser.ParseIntBox(c.ClassicBase, target.ClassicBaseSeconds, 0, max);
            target.ClassicProportionalSeconds = WpfSettingsValueParser.ParseIntBox(c.ClassicProportional, target.ClassicProportionalSeconds, 0, max);
            target.ExpertBaseSeconds = WpfSettingsValueParser.ParseIntBox(c.ExpertBase, target.ExpertBaseSeconds, 0, max);
            target.ExpertProportionalSeconds = WpfSettingsValueParser.ParseIntBox(c.ExpertProportional, target.ExpertProportionalSeconds, 0, max);
            target.MasterBaseSeconds = WpfSettingsValueParser.ParseIntBox(c.MasterBase, target.MasterBaseSeconds, 0, max);
            target.MasterProportionalSeconds = WpfSettingsValueParser.ParseIntBox(c.MasterProportional, target.MasterProportionalSeconds, 0, max);
        }

        bossPenaltySettings = next;
        shell.SaveBossPenaltySettings(next);
    }

    private string Localize(string key) => shell.Localize(key);

    private sealed record BossPenaltyControls(
        WpfTextBox JourneyBase,
        WpfTextBox JourneyProportional,
        WpfTextBox ClassicBase,
        WpfTextBox ClassicProportional,
        WpfTextBox ExpertBase,
        WpfTextBox ExpertProportional,
        WpfTextBox MasterBase,
        WpfTextBox MasterProportional)
    {
        public WpfTextBox[] GetAll() =>
        [
            JourneyBase,
            JourneyProportional,
            ClassicBase,
            ClassicProportional,
            ExpertBase,
            ExpertProportional,
            MasterBase,
            MasterProportional
        ];
    }
}
