using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class UiSettingsPage : SettingsPageBase
{
    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(content =>
        {
            Owner.AddColumnSettingsSection(content);
            Owner.AddTimerSettingsSection(content);
        });
    }

    public override void Apply(AppSettings settings)
    {
        Owner.ApplyUiSettings(settings);
    }
}
