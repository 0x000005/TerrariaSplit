using System.Windows.Forms;

namespace TerrariaSplit;

internal static class DataSettingsPage
{
    public static Control Build(SettingsForm owner)
    {
        return owner.BuildScrollPage(content =>
        {
            owner.AddReferenceDataSection(content);
            owner.AddPersonalBestDataSection(content);
        });
    }
}
