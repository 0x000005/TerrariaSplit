namespace TerrariaSplit.UI.Settings;

internal sealed class FontFamilySelector : ThemedDropDownList
{
    public FontFamilySelector()
    {
        foreach (string family in UiFontFactory.Default.GetInstalledFamilyNames())
        {
            Items.Add(family);
        }

        SetSelectedFontFamily(UiFontDefaults.DefaultFamilyName);
    }

    public string SelectedFontFamily => SelectedItem is string family
        ? family
        : UiFontDefaults.DefaultFamilyName;

    public void SetSelectedFontFamily(string familyName)
    {
        string normalized = UiFontFactory.Default.NormalizeFamilyName(familyName);
        SelectedItem = normalized;
        if (SelectedIndex < 0 && Items.Count > 0)
        {
            SelectedIndex = 0;
        }
    }
}
