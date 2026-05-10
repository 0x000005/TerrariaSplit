using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class DataSettingsPage : SettingsPageBase
{
    protected override Control BuildPage(SettingsPageContext context)
    {
        return context.BuildScrollPage(content =>
        {
            Owner.AddReferenceDataSection(content);
            Owner.AddPersonalBestDataSection(content);
        });
    }

    public override void Apply(AppSettings settings)
    {
        Owner.ApplyDataSettings(settings);
    }
}
