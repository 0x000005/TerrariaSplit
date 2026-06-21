using System.Drawing;
using System.Drawing.Text;

namespace TerrariaSplit.Configuration;

internal static class UiFontSettings
{
    public const string DefaultFamilyName = "Segoe UI";

    private static readonly Lazy<IReadOnlyList<string>> InstalledFamilyNames = new(LoadInstalledFamilyNames);

    public static IReadOnlyList<string> GetInstalledFamilyNames()
    {
        return InstalledFamilyNames.Value;
    }

    public static string NormalizeFamilyName(string? familyName)
    {
        string trimmed = familyName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return DefaultFamilyName;
        }

        foreach (string installed in GetInstalledFamilyNames())
        {
            if (string.Equals(installed, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return installed;
            }
        }

        return DefaultFamilyName;
    }

    public static Font CreateFont(
        string? familyName,
        float size,
        FontStyle style,
        GraphicsUnit unit = GraphicsUnit.Point)
    {
        float normalizedSize = Math.Max(1f, size);
        string normalizedFamily = NormalizeFamilyName(familyName);
        return TryCreateFont(normalizedFamily, normalizedSize, style, unit) ??
            TryCreateFont(DefaultFamilyName, normalizedSize, style, unit) ??
            new Font(FontFamily.GenericSansSerif, normalizedSize, style, unit);
    }

    private static Font? TryCreateFont(string familyName, float size, FontStyle style, GraphicsUnit unit)
    {
        try
        {
            return new Font(familyName, size, style, unit);
        }
        catch
        {
            if (style == FontStyle.Regular)
            {
                return null;
            }
        }

        try
        {
            return new Font(familyName, size, FontStyle.Regular, unit);
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<string> LoadInstalledFamilyNames()
    {
        try
        {
            using var fonts = new InstalledFontCollection();
            string[] names = fonts.Families
                .Select(family => family.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name) && !name.StartsWith('@'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            return names.Length > 0 ? names : [DefaultFamilyName];
        }
        catch
        {
            return [DefaultFamilyName];
        }
    }
}
