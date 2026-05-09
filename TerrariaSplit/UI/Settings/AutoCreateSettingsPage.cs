using System.Windows.Forms;

namespace TerrariaSplit;

internal static class AutoCreateSettingsPage
{
    public static Control Build(SettingsForm owner)
    {
        return owner.BuildScrollPage(owner.AddAutoCreateSection);
    }
}
