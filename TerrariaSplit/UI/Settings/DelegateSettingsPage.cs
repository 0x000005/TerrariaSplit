using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class DelegateSettingsPage : ISettingsPage
{
    private readonly Func<SettingsPageContext, Control> build;
    private readonly Action<AppSettings>? apply;

    public DelegateSettingsPage(Func<SettingsPageContext, Control> build, Action<AppSettings>? apply = null)
    {
        this.build = build;
        this.apply = apply;
    }

    public Control Build(SettingsPageContext context)
    {
        return build(context);
    }

    public void Apply(AppSettings settings)
    {
        apply?.Invoke(settings);
    }
}
