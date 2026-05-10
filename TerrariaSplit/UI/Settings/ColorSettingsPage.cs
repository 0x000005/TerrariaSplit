using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class ColorSettingsPage : SettingsPageBase
{
    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(Owner.AddColorSection);
    }

    public override void Apply(AppSettings settings)
    {
        Owner.ApplyColorSettings(settings);
    }
}
