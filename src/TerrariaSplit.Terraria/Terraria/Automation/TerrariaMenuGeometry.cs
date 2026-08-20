using System.Drawing;

namespace TerrariaSplit.Terraria.Automation;

public readonly record struct TerrariaMenuGeometry(
    float Scale,
    float LogicalWidth,
    float LogicalHeight,
    TerrariaMenuProfile Profile)
{
    // These values mirror Terraria 1.4.5.7's UIElement tree. They are deliberately
    // expressed as layout inputs instead of measured click coordinates.
    private const float CharacterOuterTop = 220f;
    private const float CharacterOuterWidth = 500f;
    private const float CharacterPanelTop = 50f;
    private const float CharacterTopPadding = 4f;
    private const float CharacterCategoryLeft = -240f;
    private const float CharacterCategorySpacing = 48f;
    private const float CharacterCategorySize = 44f;
    private const float CharacterMiddleTop = 56f;
    private const float CharacterMiddleHeight = 160f;
    private const float CharacterMiddlePaddingTop = 3f;
    private const float CharacterInfoBottomSectionOffset = 50f;
    private const float WorldOuterTop = 152f;
    private const float WorldPanelTop = 50f;
    private const float WorldContentPaddingTop = 8f;
    private const float WorldInfoHorizontalPadding = 10f;
    private const float WorldOptionHeight = 34f;
    private const float WorldOptionFirstTop = 94f;
    private const float WorldOptionRowSpacing = 48f;

    public static TerrariaMenuGeometry From(Size clientSize)
    {
        return From(clientSize, TerrariaMenuProfile.Modern1457);
    }

    public static TerrariaMenuGeometry From(Size clientSize, TerrariaMenuProfile profile)
    {
        return From(clientSize, profile, TerrariaMenuProfile.IsMainMenuUpscaleDisabled());
    }

    internal static TerrariaMenuGeometry From(
        Size clientSize,
        TerrariaMenuProfile profile,
        bool mainMenuUpscaleDisabled)
    {
        // Terraria's PreDrawMenu scales menu UI up to a logical 900px height unless disabled in config.
        float scale = GetMainMenuScale(clientSize.Height, mainMenuUpscaleDisabled);
        TerrariaMenuProfile selectedProfile = string.IsNullOrWhiteSpace(profile.Name)
            ? TerrariaMenuProfile.Modern1457
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
        return CharacterCategoryButton(categoryId: 1);
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
        return CharacterCategoryButton(categoryId: 0);
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

        int difficultyIndex = AutoCreatePlayerDifficulty.Normalize(playerDifficulty) switch
        {
            AutoCreatePlayerDifficulty.Journey => 0,
            AutoCreatePlayerDifficulty.Mediumcore => 2,
            AutoCreatePlayerDifficulty.Hardcore => 3,
            _ => 1
        };

        float middleInnerTop = CharacterOuterTop + CharacterPanelTop + CharacterMiddleTop + CharacterMiddlePaddingTop;
        float middleInnerHeight = CharacterMiddleHeight - CharacterMiddlePaddingTop;
        float difficultyContainerHeight = middleInnerHeight - CharacterInfoBottomSectionOffset;
        float difficultyTop = middleInnerTop + CharacterInfoBottomSectionOffset;
        float buttonHeight = difficultyContainerHeight / 4f;
        float y = difficultyTop + buttonHeight * (difficultyIndex + 0.5f);
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
        int index = AutoCreateWorldSize.Normalize(worldSize) switch
        {
            AutoCreateWorldSize.Small => 0,
            AutoCreateWorldSize.Large => 2,
            _ => 1
        };
        return WorldOptionButton(index, optionCount: 3, pixelWidthReduction: 8f, row: 0);
    }

    public Point WorldDifficultyButton(string worldDifficulty)
    {
        int index = AutoCreateWorldDifficulty.Normalize(worldDifficulty) switch
        {
            AutoCreateWorldDifficulty.Journey => 0,
            AutoCreateWorldDifficulty.Expert => 2,
            AutoCreateWorldDifficulty.Master => 3,
            _ => 1
        };
        return WorldOptionButton(index, optionCount: 4, pixelWidthReduction: 3f, row: 1);
    }

    public Point WorldEvilButton(string worldEvil)
    {
        int index = AutoCreateWorldEvil.Normalize(worldEvil) switch
        {
            AutoCreateWorldEvil.Corruption => 1,
            AutoCreateWorldEvil.Crimson => 2,
            _ => 0
        };
        return WorldOptionButton(index, optionCount: 3, pixelWidthReduction: 8f, row: 2);
    }

    public Point WorldAdvancedSeedButton()
    {
        return ToClient(LogicalWidth / 2f - 220f, 274f);
    }

    public Point WorldSeedFieldButton()
    {
        // UIWorldCreation: the 40px advanced button is followed by a 348px seed
        // field, while the 84px preview occupies the right side.
        return ToClient(LogicalWidth / 2f - 22f, 274f);
    }

    public Point AdvancedSeedRandomizeButton()
    {
        return ToClient(LogicalWidth / 2f - 220f, 230f);
    }

    public Point AdvancedSeedTextButton()
    {
        // UIWorldCreationAdvanced: the seed field fills the remaining 436px.
        return ToClient(LogicalWidth / 2f + 22f, 230f);
    }

    public Point AdvancedSpecialSeedButton(string specialSeed)
    {
        int index = AutoCreateSpecialWorldSeed.MenuIndex(specialSeed);
        int column = index % 6;
        int row = index / 6;
        // The list is 432px wide. Six 60px buttons are aligned from 0 to 1,
        // leaving 372px between their centers: 372 / 5 = 74.4.
        return ToClient(LogicalWidth / 2f - 186f + 74.4f * column, 287f + 67f * row);
    }

    public Point WorldAdvancedApplyButton()
    {
        float outerHeight = Math.Clamp(LogicalHeight - 200f, 0f, 400f);
        return ToClient(LogicalWidth / 2f, 134f + outerHeight);
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

    private Point WorldOptionButton(int index, int optionCount, float pixelWidthReduction, int row)
    {
        float infoWidth = 500f - WorldInfoHorizontalPadding * 2f;
        float buttonWidth = infoWidth / optionCount - pixelWidthReduction;
        float hAlign = optionCount == 1 ? 0f : (float)index / (optionCount - 1);
        float infoLeft = LogicalWidth / 2f - infoWidth / 2f;
        float buttonLeft = infoLeft + infoWidth * hAlign - buttonWidth * hAlign;
        float x = buttonLeft + buttonWidth / 2f;
        float infoTop = WorldOuterTop + WorldPanelTop + WorldContentPaddingTop;
        float y = infoTop + WorldOptionFirstTop + WorldOptionRowSpacing * row + WorldOptionHeight / 2f;
        return ToClient(x, y);
    }

    private Point CharacterCategoryButton(int categoryId)
    {
        // UICharacterCreation.MakeCategoriesBar sets a 44px UIColoredImageButton's
        // left edge to (-240 + id * 48, 50%) inside the 500px top container.
        float topContainerLeft = LogicalWidth / 2f - CharacterOuterWidth / 2f;
        float buttonLeft = topContainerLeft + CharacterOuterWidth / 2f +
            CharacterCategoryLeft + categoryId * CharacterCategorySpacing;
        float buttonTop = CharacterOuterTop + CharacterPanelTop + CharacterTopPadding;
        return ToClient(
            buttonLeft + CharacterCategorySize / 2f,
            buttonTop + CharacterCategorySize / 2f);
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

    private static float GetMainMenuScale(int clientHeight, bool mainMenuUpscaleDisabled)
    {
        if (mainMenuUpscaleDisabled)
        {
            return 1f;
        }

        return Math.Max(1f, clientHeight / 900f);
    }
}
