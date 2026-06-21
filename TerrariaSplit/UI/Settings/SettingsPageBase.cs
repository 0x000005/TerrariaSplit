using System;
using System.Windows.Forms;

namespace TerrariaSplit.UI.Settings;

internal abstract class SettingsPageBase : ISettingsPage, ISettingsPageLifecycle, ISettingsModelListener
{
    private SettingsPageContext? context;

    protected SettingsPageContext Context => context ?? throw new InvalidOperationException("Settings page has not been built.");

    protected SettingsForm Owner => Context.Owner;

    protected AppSettings Draft => Context.Draft;

    protected SettingsUiFactory Factory => Context.Factory;

    protected SettingsDialogService Dialogs => Context.Dialogs;

    public Control Build(SettingsPageContext context)
    {
        this.context = context;
        return BuildPage(context);
    }

    public abstract SettingsPageId Id { get; }

    protected abstract Control BuildPage(SettingsPageContext context);

    public virtual void Apply(AppSettings settings)
    {
    }

    public virtual void OnSelected()
    {
    }

    public virtual void OnDeselected()
    {
    }

    public virtual void OnModelChanged(SettingsModelChange change)
    {
    }
}
