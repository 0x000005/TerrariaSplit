using System.Drawing;
using System.Globalization;

namespace TerrariaSplit.UI.Settings;

internal static partial class DebugSettingsSnapshotBuilder
{
    private static string BuildAutoCreateSequenceText(
        AutoCreateWorldSettings autoCreate,
        TerrariaMenuGeometry geometry,
        int favoritePlayers,
        Func<int> worldPoolCountProvider,
        Func<string, string> localize)
    {
        var lines = new List<string>();
        int step = 1;
        bool usesPooledWorld = UsesPooledWorldPath(autoCreate, worldPoolCountProvider);

        if (usesPooledWorld)
        {
            lines.Add($"{step++}. {localize("Install pooled world")}");
        }

        AppendSequenceStep(lines, localize, ref step, "Single Player", geometry.MainMenuSinglePlayer());
        AppendSequenceStep(lines, localize, ref step, "New Player", geometry.SelectMenuNewButton());

        if (!string.IsNullOrWhiteSpace(autoCreate.PlayerTemplateCode))
        {
            if (!geometry.Profile.SupportsPlayerTemplatePaste)
            {
                AppendSequenceNote(lines, localize, ref step, "Unsupported for 1449", localize("Paste Player Template"));
            }
            else
            {
                AppendSequenceStep(
                    lines,
                    localize,
                    ref step,
                    geometry.Profile.Kind == TerrariaMenuProfileKind.Legacy1449
                        ? "Character Gender Tab"
                        : "Character Clothing Tab",
                    geometry.CharacterTemplateCategoryButton());
                AppendSequenceStep(lines, localize, ref step, "Paste Player Template", geometry.CharacterTemplatePasteButton());
            }
        }

        string normalizedPlayerDifficulty = AutoCreatePlayerDifficulty.Normalize(autoCreate.PlayerDifficulty);
        if (!string.Equals(normalizedPlayerDifficulty, AutoCreatePlayerDifficulty.Softcore, StringComparison.OrdinalIgnoreCase))
        {
            AppendSequenceStep(
                lines,
                localize,
                ref step,
                geometry.Profile.UsesLegacyCharacterCreationWizard ? "Player difficulty menu" : "Character Info Tab",
                geometry.PlayerDifficultyMenuButton());
            AppendSequenceStep(
                lines,
                localize,
                ref step,
                "Player difficulty",
                geometry.PlayerDifficultyButton(normalizedPlayerDifficulty),
                localize(normalizedPlayerDifficulty));

            if (!geometry.Profile.SupportsJourneyPlayerDifficulty &&
                string.Equals(normalizedPlayerDifficulty, AutoCreatePlayerDifficulty.Journey, StringComparison.OrdinalIgnoreCase))
            {
                AppendSequenceNote(lines, localize, ref step, "Unsupported for 1449", localize(normalizedPlayerDifficulty));
            }
        }

        AppendSequenceStep(lines, localize, ref step, "Create Player", geometry.CreatePlayerButton());
        AppendSequenceStep(lines, localize, ref step, "Select Created Player", geometry.PlayerPlayButton(favoritePlayers));
        if (usesPooledWorld)
        {
            lines.Add($"{step++}. {localize("Stop at world select")}");
            return string.Join(Environment.NewLine, lines);
        }

        AppendSequenceStep(lines, localize, ref step, "New World", geometry.SelectMenuNewButton());

        if (geometry.Profile.UsesLegacyWorldCreationWizard)
        {
            AppendLegacyWorldCreationSequence(lines, localize, ref step, autoCreate, geometry);
            AppendPostWorldCreationSequence(lines, localize, ref step, autoCreate);
            return string.Join(Environment.NewLine, lines);
        }

        string normalizedWorldSize = AutoCreateWorldSize.Normalize(autoCreate.WorldSize);
        AppendSequenceStep(
            lines,
            localize,
            ref step,
            "World size",
            geometry.WorldSizeButton(normalizedWorldSize),
            localize(normalizedWorldSize));

        string normalizedWorldDifficulty = AutoCreateWorldDifficulty.Normalize(autoCreate.WorldDifficulty);
        AppendSequenceStep(
            lines,
            localize,
            ref step,
            "World difficulty",
            geometry.WorldDifficultyButton(normalizedWorldDifficulty),
            localize(normalizedWorldDifficulty));

        string normalizedWorldEvil = AutoCreateWorldEvil.Normalize(autoCreate.WorldEvil);
        AppendSequenceStep(
            lines,
            localize,
            ref step,
            "World evil",
            geometry.WorldEvilButton(normalizedWorldEvil),
            localize(normalizedWorldEvil));

        AppendSequenceStep(lines, localize, ref step, "Advanced Seed", geometry.WorldAdvancedSeedButton());

        foreach (string specialSeed in AutoCreateSpecialWorldSeed.ParseList(autoCreate.SpecialSeeds))
        {
            AppendSequenceStep(
                lines,
                localize,
                ref step,
                "Special seeds",
                geometry.AdvancedSpecialSeedButton(specialSeed),
                localize(specialSeed));
        }

        string secretSeeds = autoCreate.SecretSeeds?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(secretSeeds))
        {
            AppendSequenceStep(lines, localize, ref step, "Secret seed / fixed seed", geometry.AdvancedSeedTextButton(), secretSeeds);
            AppendSequenceStep(lines, localize, ref step, "Submit World Seed", geometry.VirtualKeyboardSubmitButton());
        }

        AppendSequenceStep(lines, localize, ref step, "Randomize Visible Seed", geometry.AdvancedSeedRandomizeButton());
        AppendSequenceStep(lines, localize, ref step, "Apply visible seed", geometry.WorldAdvancedApplyButton());

        AppendSequenceStep(lines, localize, ref step, "Create World", geometry.CreateWorldButton());

        AppendPostWorldCreationSequence(lines, localize, ref step, autoCreate);
        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendLegacyWorldCreationSequence(
        List<string> lines,
        Func<string, string> localize,
        ref int step,
        AutoCreateWorldSettings autoCreate,
        TerrariaMenuGeometry geometry)
    {
        string normalizedWorldSize = AutoCreateWorldSize.Normalize(autoCreate.WorldSize);
        AppendSequenceStep(
            lines,
            localize,
            ref step,
            "World size",
            geometry.WorldSizeButton(normalizedWorldSize),
            localize(normalizedWorldSize));

        string normalizedWorldDifficulty = AutoCreateWorldDifficulty.Normalize(autoCreate.WorldDifficulty);
        AppendSequenceStep(
            lines,
            localize,
            ref step,
            "World difficulty",
            geometry.WorldDifficultyButton(normalizedWorldDifficulty),
            localize(normalizedWorldDifficulty));

        if (!geometry.Profile.SupportsJourneyWorldDifficulty &&
            string.Equals(normalizedWorldDifficulty, AutoCreateWorldDifficulty.Journey, StringComparison.OrdinalIgnoreCase))
        {
            AppendSequenceNote(lines, localize, ref step, "Unsupported for 1449", localize(normalizedWorldDifficulty));
        }

        string normalizedWorldEvil = AutoCreateWorldEvil.Normalize(autoCreate.WorldEvil);
        AppendSequenceStep(
            lines,
            localize,
            ref step,
            "World evil",
            geometry.WorldEvilButton(normalizedWorldEvil),
            localize(normalizedWorldEvil));

        bool seedTextSupported = TerrariaLegacy1449SeedText.TryBuild(autoCreate, out string seedText, out string seedTextDetail);
        if (!string.IsNullOrWhiteSpace(seedText))
        {
            AppendSequenceStep(lines, localize, ref step, "Secret seed / fixed seed", geometry.WorldSeedFieldButton(), seedText);
            AppendSequenceStep(lines, localize, ref step, "Submit World Seed", geometry.VirtualKeyboardSubmitButton());
        }

        if (!seedTextSupported)
        {
            AppendSequenceNote(lines, localize, ref step, "Unsupported for 1449", seedTextDetail);
        }

        AppendSequenceStep(lines, localize, ref step, "Create World", geometry.CreateWorldButton());

        if (IsPyramidPreScreenCandidate(autoCreate))
        {
            AppendSequenceNote(lines, localize, ref step, "Skip pyramid pre-screen", "1449");
        }
    }

    private static void AppendPostWorldCreationSequence(
        List<string> lines,
        Func<string, string> localize,
        ref int step,
        AutoCreateWorldSettings autoCreate)
    {
        if (AutoCreateSpecialWorldSeed.ParseList(autoCreate.SpecialSeeds)
                .Contains(AutoCreateSpecialWorldSeed.Zenith, StringComparer.OrdinalIgnoreCase) &&
            autoCreate.EnableZenithStarCatch)
        {
            lines.Add(
                $"{step++}. {localize("Catch stars through")}: " +
                localize(AutoCreateZenithStarCatchStage.Normalize(autoCreate.ZenithStarCatchStopStage)));
            lines.Add(
                $"{step++}. {localize("Catch speed")}: " +
                AutoCreateZenithStarCatchSpeed.FormatMultiplier(autoCreate.ZenithStarCatchSpeedSliderValue));
        }

        if (autoCreate.EnablePyramidFilter)
        {
            string itemDetail = FormatPyramidFilterItems(autoCreate, localize);
            string itemSuffix = HasPyramidFilterItems(autoCreate)
                ? $" ({localize("Required pyramid items")}: {itemDetail})"
                : string.Empty;
            lines.Add($"{step++}. {localize("Filter pyramid")}{itemSuffix}");
        }

        bool crimsonCorridorEnabled = autoCreate.RequireCrimsonBetweenDungeonAndSpawn &&
            string.Equals(AutoCreateWorldSize.Normalize(autoCreate.WorldSize), AutoCreateWorldSize.Small, StringComparison.Ordinal) &&
            string.Equals(AutoCreateWorldEvil.Normalize(autoCreate.WorldEvil), AutoCreateWorldEvil.Crimson, StringComparison.Ordinal);
        if (crimsonCorridorEnabled)
        {
            lines.Add($"{step++}. {localize("Filter Crimson between dungeon and spawn")}");
        }

        if ((autoCreate.EnablePyramidFilter || crimsonCorridorEnabled) && autoCreate.ReturnToMainMenuOnFilterFailure)
        {
            lines.Add($"{step++}. {localize("Return to main menu on filter failure")}");
        }
    }

    private static bool UsesPooledWorldPath(
        AutoCreateWorldSettings autoCreate,
        Func<int> worldPoolCountProvider)
    {
        return autoCreate.EnableWorldPool &&
            worldPoolCountProvider() > 0;
    }

    private static bool IsPyramidPreScreenCandidate(AutoCreateWorldSettings autoCreate)
    {
        return autoCreate.EnablePyramidFilter &&
            AutoCreateWorldSize.Normalize(autoCreate.WorldSize) == AutoCreateWorldSize.Small &&
            AutoCreateWorldEvil.Normalize(autoCreate.WorldEvil) == AutoCreateWorldEvil.Crimson &&
            !AutoCreateSpecialWorldSeed.ParseList(autoCreate.SpecialSeeds).Any() &&
            !AutoCreateSeedList.Parse(autoCreate.SecretSeeds).Any();
    }

    private static bool HasPyramidFilterItems(AutoCreateWorldSettings autoCreate)
    {
        return AutoCreatePyramidFilterItem.NormalizeMask(autoCreate.PyramidFilterItemMask) != 0;
    }

    private static string FormatPyramidFilterItems(
        AutoCreateWorldSettings autoCreate,
        Func<string, string> localize)
    {
        IReadOnlyList<string> items = AutoCreatePyramidFilterItem.FromMask(autoCreate.PyramidFilterItemMask);
        return items.Count == 0
            ? localize("None")
            : string.Join(", ", items.Select(localize));
    }

    private static void AppendSequenceStep(
        List<string> lines,
        Func<string, string> localize,
        ref int step,
        string label,
        Point point,
        string? detail = null)
    {
        string title = localize(label);
        if (!string.IsNullOrWhiteSpace(detail))
        {
            title += $" ({detail})";
        }

        lines.Add($"{step.ToString(CultureInfo.InvariantCulture)}. {title} -> {FormatPoint(point)}");
        step++;
    }

    private static void AppendSequenceNote(
        List<string> lines,
        Func<string, string> localize,
        ref int step,
        string label,
        string? detail = null)
    {
        string title = localize(label);
        if (!string.IsNullOrWhiteSpace(detail))
        {
            title += $" ({detail})";
        }

        lines.Add($"{step.ToString(CultureInfo.InvariantCulture)}. {title}");
        step++;
    }

    private static string FormatPoint(Point point)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{point.X}, {point.Y}");
    }
}
