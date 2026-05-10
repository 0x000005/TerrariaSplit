using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class AnimationSettingsPage : SettingsPageBase
{
    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(Owner.AddAnimationSection);
    }

    public override void Apply(AppSettings settings)
    {
        Owner.ApplyAnimationSettings(settings);
    }
}
