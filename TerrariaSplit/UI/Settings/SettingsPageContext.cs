using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class SettingsPageContext
{
    public SettingsPageContext(SettingsForm owner)
    {
        Owner = owner;
    }

    internal SettingsForm Owner { get; }

    public string Localize(string key)
    {
        return Owner.Localize(key);
    }

    public Control BuildScrollPage(Action<TableLayoutPanel> populate)
    {
        return Owner.BuildScrollPage(populate);
    }
}
