using System.Drawing;

namespace TerrariaSplit.Terraria.Automation;

public readonly record struct TerrariaMenuGeometry(
    float Scale,
    float LogicalWidth,
    float LogicalHeight,
    TerrariaMenuProfile Profile)
{
    public static TerrariaMenuGeometry From(Size clientSize)
    {
        return From(clientSize, TerrariaMenuProfile.Modern1456);
    }

    public static TerrariaMenuGeometry From(Size clientSize, TerrariaMenuProfile profile)
    {
        // Terraria's PreDrawMenu scales menu UI up to a logical 900px height unless disabled in config.
        float scale = GetMainMenuScale(clientSize.Height);
        TerrariaMenuProfile selectedProfile = string.IsNullOrWhiteSpace(profile.Name)
            ? TerrariaMenuProfile.Modern1456
            : profile;
        return new TerrariaMenuGeometry(scale, clientSize.Width / scale, clientSize.Height / scale, selectedProfile);
    }

    public Point MainMenuSinglePlayer()
    {
        return ToClient(LogicalWidth / 2f, 245f);
    }

    public Point SelectMenuNewButton()
    {
        float outerWidth = GetSelectListOuterWidth();
        return ToClient(LogicalWidth / 2f + outerWidth / 4f + 5f, LogicalHeight - 70f);
    }

    public Point SelectMenuBackButton()
    {
        float outerWidth = GetSelectListOuterWidth();
        return ToClient(LogicalWidth / 2f - outerWidth / 4f - 5f, LogicalHeight - 70f);
    }

    public Point CreatePlayerButton()
    {
        if (Profile.UsesLegacyCharacterCreationWizard)
        {
            return LegacyMenuButton(top: 220f, spacing: 38f, index: 6, offset: 6f, itemScale: 0.9f);
        }

        return ToClient(LogicalWidth / 2f + 130f, 534f);
    }

    public Point VirtualKeyboardSubmitButton()
    {
        return ToClient(LogicalWidth / 2f, 350f);
    }

    public Point CharacterClothingCategoryButton()
    {
        return ToClient(LogicalWidth / 2f - 176f, 294f);
    }

    public Point CharacterTemplateCategoryButton()
    {
        return Profile.Kind == TerrariaMenuProfileKind.Legacy1449
            ? CharacterGenderCategoryButton1449()
            : CharacterClothingCategoryButton();
    }

    private Point CharacterGenderCategoryButton1449()
    {
        return ToClient(LogicalWidth / 2f - 170f, 294f);
    }

    public Point CharacterInfoCategoryButton()
    {
        return ToClient(LogicalWidth / 2f - 224f, 294f);
    }

    public Point CharacterTemplatePasteButton()
    {
        if (Profile.Kind == TerrariaMenuProfileKind.Legacy1449)
        {
            return ToClient(LogicalWidth / 2f, 461f);
        }

        return ToClient(LogicalWidth / 2f + 110f, 475f);
    }

    public Point PlayerDifficultyButton(string playerDifficulty)
    {
        if (Profile.UsesLegacyCharacterCreationWizard)
        {
            int legacyIndex = AutoCreatePlayerDifficulty.Normalize(playerDifficulty) switch
            {
                AutoCreatePlayerDifficulty.Mediumcore => 2,
                AutoCreatePlayerDifficulty.Hardcore => 3,
                _ => 1
            };
            return LegacyMenuButton(top: 250f, spacing: 50f, index: legacyIndex, offset: 25f, itemScale: 1f);
        }

        float y = AutoCreatePlayerDifficulty.Normalize(playerDifficulty) switch
        {
            AutoCreatePlayerDifficulty.Journey => 403f,
            AutoCreatePlayerDifficulty.Mediumcore => 458f,
            AutoCreatePlayerDifficulty.Hardcore => 485f,
            _ => 430f
        };
        return ToClient(LogicalWidth / 2f - 146f, y);
    }

    public Point PlayerDifficultyMenuButton()
    {
        if (Profile.UsesLegacyCharacterCreationWizard)
        {
            return LegacyMenuButton(top: 220f, spacing: 38f, index: 5, offset: 0f, itemScale: 0.75f);
        }

        return CharacterInfoCategoryButton();
    }

    public Point CreateWorldButton()
    {
        return ToClient(LogicalWidth / 2f + 130f, 534f);
    }

    public Point CreateWorldBackButton()
    {
        return ToClient(LogicalWidth / 2f - 130f, 534f);
    }

    public Point WorldSizeButton(string worldSize)
    {
        float x = AutoCreateWorldSize.Normalize(worldSize) switch
        {
            AutoCreateWorldSize.Small => -164f,
            AutoCreateWorldSize.Large => 164f,
            _ => 0f
        };
        return ToClient(LogicalWidth / 2f + x, 331f);
    }

    public Point WorldDifficultyButton(string worldDifficulty)
    {
        float x = AutoCreateWorldDifficulty.Normalize(worldDifficulty) switch
        {
            AutoCreateWorldDifficulty.Journey => -182f,
            AutoCreateWorldDifficulty.Expert => 61f,
            AutoCreateWorldDifficulty.Master => 182f,
            _ => -61f
        };
        return ToClient(LogicalWidth / 2f + x, 379f);
    }

    public Point WorldEvilButton(string worldEvil)
    {
        float x = AutoCreateWorldEvil.Normalize(worldEvil) switch
        {
            AutoCreateWorldEvil.Corruption => 0f,
            AutoCreateWorldEvil.Crimson => 164f,
            _ => -164f
        };
        return ToClient(LogicalWidth / 2f + x, 427f);
    }

    public Point WorldAdvancedSeedButton()
    {
        return ToClient(LogicalWidth / 2f - 220f, 274f);
    }

    public Point WorldSeedFieldButton()
    {
        return ToClient(LogicalWidth / 2f, 274f);
    }

    public Point AdvancedSeedRandomizeButton()
    {
        return ToClient(LogicalWidth / 2f - 220f, 230f);
    }

    public Point AdvancedSeedTextButton()
    {
        return ToClient(LogicalWidth / 2f, 230f);
    }

    public Point AdvancedSpecialSeedButton(string specialSeed)
    {
        int index = AutoCreateSpecialWorldSeed.MenuIndex(specialSeed);
        int column = index % 6;
        int row = index / 6;
        return ToClient(LogicalWidth / 2f - 186f + 78.4f * column, 287f + 67f * row);
    }

    public Point WorldAdvancedApplyButton()
    {
        return ToClient(LogicalWidth / 2f, 534f);
    }

    public Point PlayerPlayButton(int listIndex)
    {
        return SelectListPlayButton(listIndex);
    }

    public Point WorldPlayButton(int listIndex)
    {
        return SelectListPlayButton(listIndex);
    }

    private Point SelectListPlayButton(int listIndex)
    {
        float outerWidth = GetSelectListOuterWidth();
        float left = LogicalWidth / 2f - outerWidth / 2f;
        float itemTop = 232f + listIndex * 101f;
        return ToClient(left + 33f, itemTop + 79f);
    }

    private Point LegacyMenuButton(float top, float spacing, int index, float offset, float itemScale)
    {
        return ToClient(LogicalWidth / 2f, top + spacing * index + offset + 25f * itemScale);
    }

    private Point ToClient(float logicalX, float logicalY)
    {
        return new Point(
            (int)Math.Round(logicalX * Scale),
            (int)Math.Round(logicalY * Scale));
    }

    private float GetSelectListOuterWidth()
    {
        return Math.Min(LogicalWidth * 0.8f, 650f);
    }

    private static float GetMainMenuScale(int clientHeight)
    {
        if (TerrariaMenuProfile.IsMainMenuUpscaleDisabled())
        {
            return 1f;
        }

        return Math.Max(1f, clientHeight / 900f);
    }
}
