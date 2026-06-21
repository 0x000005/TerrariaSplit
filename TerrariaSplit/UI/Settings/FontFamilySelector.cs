namespace TerrariaSplit.UI.Settings;

internal sealed class FontFamilySelector : ThemedDropDownList
{
    public FontFamilySelector()
    {
        foreach (string family in UiFontSettings.GetInstalledFamilyNames())
        {
            Items.Add(family);
        }

        SetSelectedFontFamily(UiFontSettings.DefaultFamilyName);
    }

    public string SelectedFontFamily => SelectedItem is string family
        ? family
        : UiFontSettings.DefaultFamilyName;

    public void SetSelectedFontFamily(string familyName)
    {
        string normalized = UiFontSettings.NormalizeFamilyName(familyName);
        SelectedItem = normalized;
        if (SelectedIndex < 0 && Items.Count > 0)
        {
            SelectedIndex = 0;
        }
    }
}
