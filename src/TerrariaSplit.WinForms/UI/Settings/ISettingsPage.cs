using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal interface ISettingsPage
{
    SettingsPageId Id { get; }

    Control Build(SettingsPageContext context);

    void Apply(AppSettings settings)
    {
    }
}
