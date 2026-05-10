using System;
using System.Windows.Forms;

namespace TerrariaSplit;

internal abstract class SettingsPageBase : ISettingsPage
{
    private SettingsForm? owner;

    protected SettingsForm Owner => owner ?? throw new InvalidOperationException("Settings page has not been built.");

    public Control Build(SettingsPageContext context)
    {
        owner = context.Owner;
        return BuildPage(context);
    }

    protected abstract Control BuildPage(SettingsPageContext context);

    public virtual void Apply(AppSettings settings)
    {
    }
}
