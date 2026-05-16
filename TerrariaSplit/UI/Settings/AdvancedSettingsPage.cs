using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class AdvancedSettingsPage : SettingsPageBase
{
    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(Owner.AddAdvancedSection);
    }

    public override void Apply(AppSettings settings)
    {
        Owner.ApplyAdvancedSettings(settings);
    }
}
