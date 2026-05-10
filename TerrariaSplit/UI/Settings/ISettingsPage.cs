using System.Windows.Forms;

namespace TerrariaSplit;

internal interface ISettingsPage
{
    Control Build(SettingsPageContext context);

    void Apply(AppSettings settings)
    {
    }
}
