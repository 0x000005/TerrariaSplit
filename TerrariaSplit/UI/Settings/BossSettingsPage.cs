using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class BossSettingsPage : SettingsPageBase
{
    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(content =>
        {
            Owner.AddRouteSection(content);
            Owner.AddBossIconSection(content);
        });
    }

    public override void Apply(AppSettings settings)
    {
        Owner.ApplyBossSettings(settings);
    }
}
