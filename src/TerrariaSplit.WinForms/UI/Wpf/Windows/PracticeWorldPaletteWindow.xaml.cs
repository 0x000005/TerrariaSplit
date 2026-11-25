using System.Globalization;
using System.Windows;
using System.Windows.Input;
using TerrariaSplit.UI.Wpf.Controls;

namespace TerrariaSplit.UI.Wpf.Windows;

internal sealed partial class PracticeWorldPaletteWindow : ModernWindow
{
    private readonly AppSettings settings;
    private readonly List<PracticeSlotItem> items;

    public PracticeWorldPaletteWindow(AppSettings settings)
    {
        InitializeComponent();

        this.settings = settings;
        Title = Localizer.Get("Save Selector", settings);
        PaletteTitleText.Text = Title;
        FooterHintText.Text = Localizer.Get("Press ESC to exit", settings);

        items = BuildSlotItems(settings);
        SlotListBox.ItemsSource = items;

        int firstConfigured = items.FindIndex(item => item.IsConfigured);
        if (firstConfigured >= 0)
        {
            SlotListBox.SelectedIndex = firstConfigured;
        }

        PreviewKeyDown += OnPalettePreviewKeyDown;
        SlotListBox.PreviewMouseLeftButtonUp += OnSlotMouseLeftButtonUp;
        Loaded += (_, _) =>
        {
            Activate();
            Focus();
            Keyboard.Focus(SlotListBox);
        };
    }

    public PracticeWorldSlot? SelectedSlot { get; private set; }

    private void OnPalettePreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DialogResult = false;
            Close();
            return;
        }

        if (e.Key == Key.Enter)
        {
            if (SlotListBox.SelectedItem is PracticeSlotItem selected && selected.IsConfigured)
            {
                e.Handled = true;
                AcceptSlot(selected.Index);
            }

            return;
        }

        int slotIndex = KeyToSlotIndex(e.Key);
        if (slotIndex < 0)
        {
            return;
        }

        e.Handled = true;
        AcceptSlot(slotIndex);
    }

    private void OnSlotMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (SlotListBox.SelectedItem is PracticeSlotItem selected && selected.IsConfigured)
        {
            AcceptSlot(selected.Index);
        }
    }

    private void AcceptSlot(int index)
    {
        IReadOnlyList<PracticeWorldSlot> slots = settings.PracticeWorlds.Slots;
        if (index < 0 || index >= slots.Count)
        {
            DialogResult = false;
            Close();
            return;
        }

        PracticeWorldSlot slot = slots[index];
        if (!slot.IsConfigured)
        {
            AppLog.ForSource().LogInformation($"Practice world slot {index + 1} is not configured.");
            DialogResult = false;
            Close();
            return;
        }

        SelectedSlot = new PracticeWorldSlot
        {
            Name = slot.Name,
            PlayerFilePath = slot.PlayerFilePath,
            WorldFilePath = slot.WorldFilePath
        };
        DialogResult = true;
        Close();
    }

    private List<PracticeSlotItem> BuildSlotItems(AppSettings currentSettings)
    {
        var result = new List<PracticeSlotItem>(PracticeWorldSettings.SlotCount);
        for (int index = 0; index < PracticeWorldSettings.SlotCount; index++)
        {
            PracticeWorldSlot slot = index < currentSettings.PracticeWorlds.Slots.Count
                ? currentSettings.PracticeWorlds.Slots[index]
                : new PracticeWorldSlot();
            result.Add(new PracticeSlotItem(
                index,
                SlotKeyText(index),
                GetSlotDisplayName(slot, index),
                GetSlotMetadata(slot),
                slot.IsConfigured));
        }

        return result;
    }

    private string GetSlotDisplayName(PracticeWorldSlot slot, int index)
    {
        if (!slot.IsConfigured)
        {
            return Localizer.Get("Not configured", settings);
        }

        if (!string.IsNullOrWhiteSpace(slot.Name))
        {
            return slot.Name;
        }

        if (!string.IsNullOrWhiteSpace(slot.WorldFilePath))
        {
            return Path.GetFileNameWithoutExtension(slot.WorldFilePath);
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} {1}",
            Localizer.Get("Practice world", settings),
            SlotKeyText(index));
    }

    private static string GetSlotMetadata(PracticeWorldSlot slot)
    {
        if (!slot.IsConfigured)
        {
            return string.Empty;
        }

        string player = string.IsNullOrWhiteSpace(slot.PlayerFilePath)
            ? string.Empty
            : Path.GetFileNameWithoutExtension(slot.PlayerFilePath);
        string world = string.IsNullOrWhiteSpace(slot.WorldFilePath)
            ? string.Empty
            : Path.GetFileNameWithoutExtension(slot.WorldFilePath);

        if (player.Length > 0 && world.Length > 0)
        {
            return $"{player}  ·  {world}";
        }

        return world.Length > 0 ? world : player;
    }

    private static int KeyToSlotIndex(Key key)
    {
        return key switch
        {
            Key.D1 or Key.NumPad1 => 0,
            Key.D2 or Key.NumPad2 => 1,
            Key.D3 or Key.NumPad3 => 2,
            Key.D4 or Key.NumPad4 => 3,
            Key.D5 or Key.NumPad5 => 4,
            Key.D6 or Key.NumPad6 => 5,
            Key.D7 or Key.NumPad7 => 6,
            Key.D8 or Key.NumPad8 => 7,
            Key.D9 or Key.NumPad9 => 8,
            Key.D0 or Key.NumPad0 => 9,
            _ => -1
        };
    }

    private static string SlotKeyText(int index)
    {
        return index == 9 ? "0" : (index + 1).ToString(CultureInfo.InvariantCulture);
    }

    internal sealed record PracticeSlotItem(
        int Index,
        string KeyLabel,
        string DisplayName,
        string MetadataText,
        bool IsConfigured);
}
