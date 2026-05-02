using System.Windows.Forms;

namespace TerrariaSplit;

internal static class SoundSettingsPage
{
    public static Control Build(SettingsForm owner)
    {
        return owner.BuildScrollPage(owner.AddSoundSection);
    }
}
