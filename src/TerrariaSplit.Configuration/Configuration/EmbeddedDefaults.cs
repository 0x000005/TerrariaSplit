using System.Reflection;

namespace TerrariaSplit.Configuration;

internal static class EmbeddedDefaults
{
    private static readonly Lazy<string> SettingsJsonValue = new(() => ReadResource("settings.default.json"));
    private static readonly Lazy<string> ReferenceTimesWrJsonValue = new(() => ReadResource("reference-splits.default.json"));

    public static string SettingsJson => SettingsJsonValue.Value;

    public static string ReferenceTimesWrJson => ReferenceTimesWrJsonValue.Value;

    private static string ReadResource(string fileName)
    {
        Assembly assembly = typeof(EmbeddedDefaults).Assembly;
        string? resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            throw new InvalidOperationException($"Embedded default resource is missing: {fileName}");
        }

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Embedded default resource could not be opened: {fileName}");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
