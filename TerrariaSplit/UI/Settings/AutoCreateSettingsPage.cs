using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class AutoCreateSettingsPage : SettingsPageBase
{
    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(Owner.AddAutoCreateSection);
    }

    public override void Apply(AppSettings settings)
    {
        Owner.ApplyAutoCreateSettings(settings);
    }
}
