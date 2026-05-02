using System.Windows.Forms;

namespace TerrariaSplit;

internal static class AnimationSettingsPage
{
    public static Control Build(SettingsForm owner)
    {
        return owner.BuildScrollPage(owner.AddAnimationSection);
    }
}
