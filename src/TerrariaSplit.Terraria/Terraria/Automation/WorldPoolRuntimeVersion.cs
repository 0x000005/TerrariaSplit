namespace TerrariaSplit.Terraria.Automation;

internal static class WorldPoolRuntimeVersion
{
    public static string SignatureFromCurrentRuntime(AppSettings settings)
    {
        string? version = TerrariaMenuProfile.TryGetRunningTerrariaFileVersion();
        if (string.IsNullOrWhiteSpace(version))
        {
            version = TerrariaServerLocator.TryResolveTarget()?.FileVersion;
        }

        return WorldPoolSignature.From(settings, version);
    }

    public static string SignatureFromServerTarget(AppSettings settings, TerrariaServerTarget target)
    {
        return WorldPoolSignature.From(settings, target.FileVersion);
    }
}
