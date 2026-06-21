namespace TerrariaSplit.UI.Settings;

internal interface ISettingsModelListener
{
    void OnModelChanged(SettingsModelChange change)
    {
    }
}
