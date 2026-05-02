using System.Windows.Forms;

namespace TerrariaSplit;

internal static class BossSettingsPage
{
    public static Control Build(SettingsForm owner)
    {
        return owner.BuildScrollPage(content =>
        {
            owner.AddRouteSection(content);
            owner.AddBossIconSection(content);
        });
    }
}
