using System.Windows.Forms;

namespace TerrariaSplit;

internal static class ColorSettingsPage
{
    public static Control Build(SettingsForm owner)
    {
        return owner.BuildScrollPage(owner.AddColorSection);
    }
}
