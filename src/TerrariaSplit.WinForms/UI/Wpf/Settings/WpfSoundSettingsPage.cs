using System.Windows;
using System.Windows.Controls;
using WpfButton = System.Windows.Controls.Button;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace TerrariaSplit.UI.Wpf.Settings;

internal sealed class WpfSoundSettingsPage : WpfSettingsPageBase
{
    private readonly Dictionary<string, WpfTextBox> soundTextBoxes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBlock> soundLabels = new(StringComparer.Ordinal);

    public override SettingsPageId Id => SettingsPageId.Sounds;

    internal IReadOnlyDictionary<string, WpfTextBox> SoundTextBoxes => soundTextBoxes;

    protected override FrameworkElement BuildPage(WpfSettingsPageContext context)
    {
        return WpfSettingsUi.CreateScrollPage(BuildSections);
    }

    public override void Apply(AppSettings settings)
    {
        WpfSettingsBinder.ApplySounds(settings, soundTextBoxes);
    }

    public override void OnModelChanged(SettingsModelChange change)
    {
        if (change == SettingsModelChange.RouteChanged)
        {
            UpdateFinalGroupLabels();
        }
    }

    private void BuildSections(StackPanel parent)
    {
        Draft.Overlay.Sounds ??= new UiSoundSettings();

        var card = WpfSettingsUi.CreateCard(Localize("Sounds"), out StackPanel body);
        IReadOnlyList<SoundDescriptor> descriptors = SettingsDescriptors.Sounds;
        for (int i = 0; i < descriptors.Count; i++)
        {
            SoundDescriptor descriptor = descriptors[i];
            bool isLast = i == descriptors.Count - 1;
            body.Children.Add(CreateSoundRow(descriptor, isLast));
        }

        parent.Children.Add(card);
    }

    private Border CreateSoundRow(SoundDescriptor descriptor, bool isLast)
    {
        Grid grid = WpfSettingsUi.CreateColumnGrid(
            new GridLength(1.15, GridUnitType.Star),
            new GridLength(1.0, GridUnitType.Star),
            GridLength.Auto);

        TextBlock label = WpfSettingsUi.CreateRowLabel(GetSoundLabel(descriptor));
        label.Margin = new Thickness(0, 0, 12, 0);
        soundLabels[descriptor.Key] = label;

        WpfTextBox pathBox = WpfSettingsUi.CreateTextBox(descriptor.GetValue(Draft.Overlay.Sounds));
        pathBox.Margin = new Thickness(0, 0, 6, 0);
        soundTextBoxes[descriptor.Key] = pathBox;

        WpfButton browseButton = WpfSettingsUi.CreateButton(Localize("Browse"), minWidth: 66);
        browseButton.Click += (_, _) => Dialogs.PickSound(pathBox);

        Grid.SetColumn(label, 0);
        Grid.SetColumn(pathBox, 1);
        Grid.SetColumn(browseButton, 2);
        grid.Children.Add(label);
        grid.Children.Add(pathBox);
        grid.Children.Add(browseButton);

        return WpfSettingsUi.CreateRowDividerContainer(grid, showBottomBorder: !isLast);
    }

    private string GetSoundLabel(SoundDescriptor descriptor)
    {
        if (!descriptor.PrefixWithFinalGroupName)
        {
            return Localize(descriptor.Label);
        }

        string finalGroupName = SplitRouteGroups.Build(Draft).LastOrDefault() is RouteGroup finalGroup
            ? SplitRouteGroups.GetGroupDisplayName(finalGroup, Draft)
            : Localize("Final group");
        return $"{finalGroupName}{Localize(": ")}{Localize(descriptor.Label)}";
    }

    private void UpdateFinalGroupLabels()
    {
        foreach (SoundDescriptor descriptor in SettingsDescriptors.Sounds.Where(sound => sound.PrefixWithFinalGroupName))
        {
            if (soundLabels.TryGetValue(descriptor.Key, out TextBlock? label))
            {
                string updated = GetSoundLabel(descriptor);
                label.Text = updated;
                label.ToolTip = updated;
            }
        }
    }
}
