using System.Drawing.Text;

namespace TerrariaSplit.Infrastructure.Windows;

public interface IInstalledFontCatalog
{
    IReadOnlyList<string> GetInstalledFamilyNames();

    string NormalizeInstalledFamilyName(string? familyName, string fallbackFamilyName);
}

public sealed class InstalledFontCatalog : IInstalledFontCatalog
{
    public static InstalledFontCatalog Shared { get; } = new();

    private readonly Lazy<IReadOnlyList<string>> installedFamilyNames;

    private InstalledFontCatalog()
        : this(LoadInstalledFamilyNames)
    {
    }

    internal InstalledFontCatalog(Func<IReadOnlyList<string>> loadInstalledFamilyNames)
    {
        installedFamilyNames = new Lazy<IReadOnlyList<string>>(loadInstalledFamilyNames);
    }

    public IReadOnlyList<string> GetInstalledFamilyNames()
    {
        return installedFamilyNames.Value;
    }

    public string NormalizeInstalledFamilyName(string? familyName, string fallbackFamilyName)
    {
        string trimmed = familyName?.Trim() ?? string.Empty;
        string fallback = string.IsNullOrWhiteSpace(fallbackFamilyName)
            ? string.Empty
            : fallbackFamilyName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return fallback;
        }

        foreach (string installed in GetInstalledFamilyNames())
        {
            if (string.Equals(installed, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return installed;
            }
        }

        return fallback;
    }

    private static IReadOnlyList<string> LoadInstalledFamilyNames()
    {
        try
        {
            using var fonts = new InstalledFontCollection();
            return fonts.Families
                .Select(family => family.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name) && !name.StartsWith('@'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }
}
