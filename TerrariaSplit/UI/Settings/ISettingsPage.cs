using System.Windows.Forms;

namespace TerrariaSplit;

internal interface ISettingsPage
{
    SettingsPageId Id { get; }

    Control Build(SettingsPageContext context);

    void Apply(AppSettings settings)
    {
    }
}
