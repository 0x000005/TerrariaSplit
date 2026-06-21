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
            AppendSequenceStep(lines, localize, ref step, "Character Clothing Tab", geometry.CharacterClothingCategoryButton());
            AppendSequenceStep(lines, localize, ref step, "Paste Player Template", geometry.CharacterTemplatePasteButton());
        }

        string normalizedPlayerDifficulty = AutoCreatePlayerDifficulty.Normalize(autoCreate.PlayerDifficulty);
        if (!string.Equals(normalizedPlayerDifficulty, AutoCreatePlayerDifficulty.Softcore, StringComparison.OrdinalIgnoreCase))
        {
            AppendSequenceStep(lines, localize, ref step, "Character Info Tab", geometry.CharacterInfoCategoryButton());
            AppendSequenceStep(
                lines,
                localize,
                ref step,
                "Player difficulty",
                geometry.PlayerDifficultyButton(normalizedPlayerDifficulty),
                localize(normalizedPlayerDifficulty));
        }

        AppendSequenceStep(lines, localize, ref step, "Create Player", geometry.CreatePlayerButton());
        AppendSequenceStep(lines, localize, ref step, "Select Created Player", geometry.PlayerPlayButton(favoritePlayers));
        if (usesPooledWorld)
        {
            lines.Add($"{step++}. {localize("Stop at world select")}");
            return string.Join(Environment.NewLine, lines);
        }

        AppendSequenceStep(lines, localize, ref step, "New World", geometry.SelectMenuNewButton());

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
            AppendSequenceStep(lines, localize, ref step, "Secret seeds", geometry.AdvancedSeedTextButton(), secretSeeds);
            AppendSequenceStep(lines, localize, ref step, "Submit World Seed", geometry.VirtualKeyboardSubmitButton());
        }

        AppendSequenceStep(lines, localize, ref step, "Randomize Visible Seed", geometry.AdvancedSeedRandomizeButton());
        AppendSequenceStep(lines, localize, ref step, "Apply visible seed", geometry.WorldAdvancedApplyButton());

        AppendSequenceStep(lines, localize, ref step, "Create World", geometry.CreateWorldButton());

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
            if (autoCreate.ReturnToMainMenuOnFilterFailure)
            {
                lines.Add($"{step++}. {localize("Return to main menu on filter failure")}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool UsesPooledWorldPath(
        AutoCreateWorldSettings autoCreate,
        Func<int> worldPoolCountProvider)
    {
        return autoCreate.EnableWorldPool &&
            worldPoolCountProvider() > 0;
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

    private static string FormatPoint(Point point)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{point.X}, {point.Y}");
    }
}
