using System.Drawing;

namespace TerrariaSplit.UI.Rendering;

internal interface IUiFontFactory
{
    IReadOnlyList<string> GetInstalledFamilyNames();

    string NormalizeFamilyName(string? familyName);

    Font CreateFont(
        string? familyName,
        float size,
        FontStyle style,
        GraphicsUnit unit = GraphicsUnit.Point);
}

internal sealed class UiFontFactory : IUiFontFactory
{
    public static UiFontFactory Default { get; } = new(InstalledFontCatalog.Shared);

    private static readonly IReadOnlyList<string> FallbackFamilyNames = [UiFontDefaults.DefaultFamilyName];

    private readonly IInstalledFontCatalog installedFontCatalog;

    internal UiFontFactory(IInstalledFontCatalog installedFontCatalog)
    {
        this.installedFontCatalog = installedFontCatalog;
    }

    public IReadOnlyList<string> GetInstalledFamilyNames()
    {
        IReadOnlyList<string> installedFamilyNames = installedFontCatalog.GetInstalledFamilyNames();
        return installedFamilyNames.Count > 0 ? installedFamilyNames : FallbackFamilyNames;
    }

    public string NormalizeFamilyName(string? familyName)
    {
        return installedFontCatalog.NormalizeInstalledFamilyName(familyName, UiFontDefaults.DefaultFamilyName);
    }

    public Font CreateFont(
        string? familyName,
        float size,
        FontStyle style,
        GraphicsUnit unit = GraphicsUnit.Point)
    {
        float normalizedSize = Math.Max(1f, size);
        string normalizedFamily = NormalizeFamilyName(familyName);
        return TryCreateFont(normalizedFamily, normalizedSize, style, unit) ??
            TryCreateFont(UiFontDefaults.DefaultFamilyName, normalizedSize, style, unit) ??
            new Font(FontFamily.GenericSansSerif, normalizedSize, style, unit);
    }

    private static Font? TryCreateFont(string familyName, float size, FontStyle style, GraphicsUnit unit)
    {
        if (string.IsNullOrWhiteSpace(familyName))
        {
            return null;
        }

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
}
