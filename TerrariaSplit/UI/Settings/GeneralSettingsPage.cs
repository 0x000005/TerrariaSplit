using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class GeneralSettingsPage : SettingsPageBase
{
    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(Owner.AddHotkeySection);
    }

    public override void Apply(AppSettings settings)
    {
        Owner.ApplyGeneralSettings(settings);
    }
}
