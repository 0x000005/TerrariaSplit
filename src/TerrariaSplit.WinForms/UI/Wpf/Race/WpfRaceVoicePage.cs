using System.Windows;
using System.Windows.Controls;
using TerrariaSplit.UI.Wpf.Settings;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfSlider = System.Windows.Controls.Slider;

namespace TerrariaSplit.UI.Wpf.Race;

internal sealed class WpfRaceVoicePage
{
    private readonly IRacePanelShell shell;
    private readonly RaceVoiceSettings voiceSettings;

    private WpfCheckBox voiceEnabledBox = null!;
    private WpfComboBox voiceNameBox = null!;
    private WpfSlider voiceSpeedSlider = null!;
    private WpfSlider voiceVolumeSlider = null!;
    private TextBlock voiceSpeedLabel = null!;
    private TextBlock voiceVolumeLabel = null!;
    private WpfButton voicePreviewButton = null!;

    public WpfRaceVoicePage(IRacePanelShell shell)
    {
        this.shell = shell;
        RaceVoiceSettings source = shell.VoiceSettings ?? new RaceVoiceSettings();
        voiceSettings = new RaceVoiceSettings
        {
            Enabled = source.Enabled,
            VoiceName = source.VoiceName?.Trim() ?? string.Empty,
            SpeedPercent = Math.Clamp(source.SpeedPercent, 50, 200),
            Volume = Math.Clamp(source.Volume, 0, 100)
        };
    }

    public FrameworkElement Build()
    {
        return WpfSettingsUi.CreateScrollPage(parent =>
        {
            var card = WpfSettingsUi.CreateCard(
                Localize("Voice announcements"),
                out StackPanel body);

            voiceEnabledBox = WpfSettingsUi.CreateToggleSwitch(voiceSettings.Enabled);
            voiceEnabledBox.Checked += (_, _) => UpdateAvailability();
            voiceEnabledBox.Unchecked += (_, _) => UpdateAvailability();

            voiceNameBox = WpfSettingsUi.CreateComboBox(240);
            voiceNameBox.Items.Add(new VoiceOption(Localize("System default"), string.Empty));
            foreach (RaceVoiceOption voice in shell.InstalledVoices)
            {
                voiceNameBox.Items.Add(new VoiceOption(voice.DisplayName, voice.Name));
            }

            voiceNameBox.SelectedItem = voiceNameBox.Items
                .Cast<VoiceOption>()
                .FirstOrDefault(v => string.Equals(v.Name, voiceSettings.VoiceName, StringComparison.OrdinalIgnoreCase))
                ?? voiceNameBox.Items[0];

            voiceSpeedSlider = new WpfSlider
            {
                Minimum = 50,
                Maximum = 200,
                Value = voiceSettings.SpeedPercent,
                Width = 180,
                VerticalAlignment = VerticalAlignment.Center
            };
            voiceSpeedLabel = CreateReadoutLabel();
            voiceSpeedSlider.ValueChanged += (_, _) => UpdateReadouts();

            voiceVolumeSlider = new WpfSlider
            {
                Minimum = 0,
                Maximum = 100,
                Value = voiceSettings.Volume,
                Width = 180,
                VerticalAlignment = VerticalAlignment.Center
            };
            voiceVolumeLabel = CreateReadoutLabel();
            voiceVolumeSlider.ValueChanged += (_, _) => UpdateReadouts();

            WpfSettingsUi.AddSettingRow(body, Localize("Enabled"), voiceEnabledBox);
            WpfSettingsUi.AddSettingRow(body, Localize("Installed voice"), voiceNameBox);
            WpfSettingsUi.AddSettingRow(body, Localize("Speech speed"), WrapSliderRow(voiceSpeedSlider, voiceSpeedLabel));
            WpfSettingsUi.AddSettingRow(body, Localize("Volume"), WrapSliderRow(voiceVolumeSlider, voiceVolumeLabel), isLast: true);

            var actions = new StackPanel
            {
                Orientation = WpfOrientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 10)
            };
            WpfButton applyBtn = WpfSettingsUi.CreateButton(Localize("Apply voice settings"), minWidth: 112);
            applyBtn.Margin = new Thickness(0, 0, 6, 0);
            applyBtn.Click += (_, _) => shell.SaveVoiceSettings(BuildVoiceSettings());

            voicePreviewButton = WpfSettingsUi.CreateButton(Localize("Preview"), minWidth: 80);
            voicePreviewButton.Click += (_, _) => shell.PreviewVoice(BuildVoiceSettings());

            actions.Children.Add(applyBtn);
            actions.Children.Add(voicePreviewButton);
            body.Children.Add(actions);

            UpdateReadouts();
            UpdateAvailability();
            parent.Children.Add(card);
        });
    }

    private static TextBlock CreateReadoutLabel()
    {
        var label = new TextBlock
        {
            Width = 52,
            TextAlignment = TextAlignment.Right,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        label.SetResourceReference(TextBlock.FontFamilyProperty, "MonoFontFamily");
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        return label;
    }

    private static StackPanel WrapSliderRow(WpfSlider slider, TextBlock readout)
    {
        var panel = new StackPanel { Orientation = WpfOrientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(slider);
        panel.Children.Add(readout);
        return panel;
    }

    private void UpdateReadouts()
    {
        int speed = (int)Math.Round(voiceSpeedSlider.Value);
        int volume = (int)Math.Round(voiceVolumeSlider.Value);
        voiceSpeedLabel.Text = $"{speed / 100d:0.00}×";
        voiceVolumeLabel.Text = $"{volume}%";
    }

    private void UpdateAvailability()
    {
        bool enabled = voiceEnabledBox.IsChecked == true;
        voiceNameBox.IsEnabled = enabled;
        voiceSpeedSlider.IsEnabled = enabled;
        voiceVolumeSlider.IsEnabled = enabled;
        voiceSpeedLabel.Opacity = enabled ? 1d : 0.45d;
        voiceVolumeLabel.Opacity = enabled ? 1d : 0.45d;
        voicePreviewButton.IsEnabled = enabled;
    }

    private RaceVoiceSettings BuildVoiceSettings()
    {
        string voiceName = voiceNameBox.SelectedItem is VoiceOption opt ? opt.Name : string.Empty;
        return new RaceVoiceSettings
        {
            Enabled = voiceEnabledBox.IsChecked == true,
            VoiceName = voiceName,
            SpeedPercent = Math.Clamp((int)Math.Round(voiceSpeedSlider.Value), 50, 200),
            Volume = Math.Clamp((int)Math.Round(voiceVolumeSlider.Value), 0, 100)
        };
    }

    private string Localize(string key) => shell.Localize(key);

    private sealed record VoiceOption(string DisplayName, string Name)
    {
        public override string ToString() => DisplayName;
    }
}
