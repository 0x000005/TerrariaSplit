using System.Windows.Forms;

namespace TerrariaSplit;

internal static class GeneralSettingsPage
{
    public static Control Build(SettingsForm owner)
    {
        return owner.BuildScrollPage(owner.AddHotkeySection);
    }
}
