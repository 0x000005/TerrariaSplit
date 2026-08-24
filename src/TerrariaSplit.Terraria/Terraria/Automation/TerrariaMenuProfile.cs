using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Process = System.Diagnostics.Process;

namespace TerrariaSplit.Terraria.Automation;

public enum TerrariaMenuProfileKind
{
    Modern1458,
    Legacy1449
}

public readonly record struct TerrariaMenuProfile(
    TerrariaMenuProfileKind Kind,
    string Name,
    bool UsesLegacyCharacterCreationWizard,
    bool UsesLegacyWorldCreationWizard,
    bool SupportsPlayerTemplatePaste,
    bool SupportsJourneyPlayerDifficulty,
    bool SupportsAdvancedSeedMenu,
    bool SupportsSpecialSeedButtons,
    bool SupportsPyramidSeedPreScreen,
    bool SupportsJourneyWorldDifficulty)
{
    public static TerrariaMenuProfile Modern1458 { get; } = new(
        TerrariaMenuProfileKind.Modern1458,
        "Terraria 1.4.5.8 menu",
        UsesLegacyCharacterCreationWizard: false,
        UsesLegacyWorldCreationWizard: false,
        SupportsPlayerTemplatePaste: true,
        SupportsJourneyPlayerDifficulty: true,
        SupportsAdvancedSeedMenu: true,
        SupportsSpecialSeedButtons: true,
        SupportsPyramidSeedPreScreen: true,
        SupportsJourneyWorldDifficulty: true);

    public static TerrariaMenuProfile Legacy1449 { get; } = new(
        TerrariaMenuProfileKind.Legacy1449,
        "Terraria 1.4.4.9 legacy menu",
        UsesLegacyCharacterCreationWizard: false,
        UsesLegacyWorldCreationWizard: true,
        SupportsPlayerTemplatePaste: true,
        SupportsJourneyPlayerDifficulty: true,
        SupportsAdvancedSeedMenu: false,
        SupportsSpecialSeedButtons: false,
        SupportsPyramidSeedPreScreen: false,
        SupportsJourneyWorldDifficulty: true);

    public static TerrariaMenuProfile FromVersion(string? fileVersion)
    {
        return IsLegacy1449Version(fileVersion)
            ? Legacy1449
            : Modern1458;
    }

    internal static TerrariaMenuProfile ResolveRunningProcess()
    {
        return FromVersion(TryGetRunningTerrariaFileVersion());
    }

    internal static bool IsLegacy1449Version(string? fileVersion)
    {
        if (string.IsNullOrWhiteSpace(fileVersion))
        {
            return false;
        }

        string normalized = fileVersion.Trim();
        return normalized.StartsWith("1.4.4.9", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("v1.4.4.9", StringComparison.OrdinalIgnoreCase);
    }

    internal static string? TryGetRunningTerrariaFileVersion()
    {
        try
        {
            using Process? process = TerrariaProcessFinder.FindNewest();
            return process?.MainModule?.FileVersionInfo.FileVersion;
        }
        catch (Win32Exception)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static bool IsMainMenuUpscaleDisabled()
    {
        try
        {
            string configPath = TerrariaConfigPath();
            if (!File.Exists(configPath))
            {
                return false;
            }

            using FileStream stream = File.OpenRead(configPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            return document.RootElement.TryGetProperty("SettingDontScaleMainMenuUp", out JsonElement value) &&
                value.ValueKind == JsonValueKind.True;
        }
        catch (Exception ex)
        {
            FileAppLogger.Instance.Error(ex, "Failed to read Terraria main menu scale setting.");
            return false;
        }
    }

    private static string TerrariaConfigPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games",
            "Terraria",
            "config.json");
    }
}
