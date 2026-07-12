using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal sealed class SettingsPageContext
{
    private readonly Action<SettingsModelChange> notifyModelChanged;

    public SettingsPageContext(
        SettingsForm owner,
        AppSettings draft,
        SettingsUiFactory factory,
        SettingsDialogService dialogs,
        Action<SettingsModelChange> notifyModelChanged)
    {
        Owner = owner;
        Draft = draft;
        Factory = factory;
        Dialogs = dialogs;
        this.notifyModelChanged = notifyModelChanged;
    }

    internal SettingsForm Owner { get; }

    public AppSettings Draft { get; }

    public SettingsUiFactory Factory { get; }

    public SettingsDialogService Dialogs { get; }

    public string Localize(string key)
    {
        return Owner.Localize(key);
    }

    public Control BuildScrollPage(Action<TableLayoutPanel> populate)
    {
        return Factory.BuildScrollPage(populate);
    }

    public void NotifyModelChanged(SettingsModelChange change)
    {
        notifyModelChanged(change);
    }
}
