using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class SoundSettingsPage : SettingsPageBase
{
    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(Owner.AddSoundSection);
    }

    public override void Apply(AppSettings settings)
    {
        Owner.ApplySoundSettings(settings);
    }
}
