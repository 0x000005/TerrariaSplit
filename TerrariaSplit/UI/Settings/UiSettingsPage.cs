using System.Windows.Forms;

namespace TerrariaSplit;

internal static class UiSettingsPage
{
    public static Control Build(SettingsForm owner)
    {
        return owner.BuildScrollPage(content =>
        {
            owner.AddColumnSettingsSection(content);
            owner.AddTimerSettingsSection(content);
        });
    }
}
