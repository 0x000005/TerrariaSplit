using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using TerrariaSplit.Compatibility;
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
    public string GameVersion { get; init; } = string.Empty;

    public static TerrariaMenuProfile Modern1458 { get; } = new(
        TerrariaMenuProfileKind.Modern1458,
        "Terraria 1.4.5+ state-driven menu",
        UsesLegacyCharacterCreationWizard: false,
        UsesLegacyWorldCreationWizard: false,
        SupportsPlayerTemplatePaste: true,
        SupportsJourneyPlayerDifficulty: true,
        SupportsAdvancedSeedMenu: true,
        SupportsSpecialSeedButtons: true,
        SupportsPyramidSeedPreScreen: true,
        SupportsJourneyWorldDifficulty: true) { GameVersion = "1.4.5.8" };

    public static TerrariaMenuProfile Legacy1449 { get; } = new(
        TerrariaMenuProfileKind.Legacy1449,
        "Terraria 1.4.4 legacy menu",
        UsesLegacyCharacterCreationWizard: false,
        UsesLegacyWorldCreationWizard: true,
        SupportsPlayerTemplatePaste: true,
        SupportsJourneyPlayerDifficulty: true,
        SupportsAdvancedSeedMenu: false,
        SupportsSpecialSeedButtons: false,
        SupportsPyramidSeedPreScreen: false,
        SupportsJourneyWorldDifficulty: true) { GameVersion = "1.4.4.9" };

    public static TerrariaMenuProfile FromVersion(string? fileVersion)
    {
        TerrariaFeatureMatch match = TerrariaFeatureCatalog.Resolve(TerrariaFeature.ForegroundWorldCreation, fileVersion);
        if (!match.CanAttempt) throw new InvalidOperationException("Terraria menu automation is unavailable: " + match.Diagnostic);
        TerrariaMenuProfile profile = match.Implementation switch
        {
            TerrariaImplementation.LegacyMenu => Legacy1449,
            TerrariaImplementation.StateDrivenMenu => Modern1458,
            _ => throw new InvalidOperationException("No menu implementation is registered: " + match.Diagnostic)
        };
        return profile with { GameVersion = match.Version.ToString() };
    }

    internal static TerrariaMenuProfile ResolveRunningProcess()
    {
        return FromVersion(TryGetRunningTerrariaFileVersion());
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
            AppLog.ForSource().LogWarning(ex, "Could not read the Terraria menu scale; using the default scale.");
            return false;
        }
    }

    internal static bool IsExclusiveFullscreenConfigured()
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
            JsonElement root = document.RootElement;
            bool fullscreen = root.TryGetProperty("Fullscreen", out JsonElement fullscreenValue) &&
                fullscreenValue.ValueKind == JsonValueKind.True;
            bool borderless = root.TryGetProperty("WindowBorderless", out JsonElement borderlessValue) &&
                borderlessValue.ValueKind == JsonValueKind.True;
            return fullscreen && !borderless;
        }
        catch (Exception ex)
        {
            AppLog.ForSource().LogWarning(ex, "Could not read Terraria fullscreen settings; using window geometry.");
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
