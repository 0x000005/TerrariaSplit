using System.Drawing;

namespace TerrariaSplit;

internal readonly record struct TerrariaMenuGeometry(float Scale, float LogicalWidth, float LogicalHeight)
{
    public static TerrariaMenuGeometry From(Size clientSize)
    {
        // Terraria's PreDrawMenu scales menu UI up to a logical 900px height unless disabled in config.
        float scale = GetMainMenuScale(clientSize.Height);
        return new TerrariaMenuGeometry(scale, clientSize.Width / scale, clientSize.Height / scale);
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

    public Point CreatePlayerButton()
    {
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

    public Point CharacterInfoCategoryButton()
    {
        return ToClient(LogicalWidth / 2f - 224f, 294f);
    }

    public Point CharacterTemplatePasteButton()
    {
        return ToClient(LogicalWidth / 2f + 110f, 475f);
    }

    public Point PlayerDifficultyButton(string playerDifficulty)
    {
        float y = AutoCreatePlayerDifficulty.Normalize(playerDifficulty) switch
        {
            AutoCreatePlayerDifficulty.Journey => 403f,
            AutoCreatePlayerDifficulty.Mediumcore => 458f,
            AutoCreatePlayerDifficulty.Hardcore => 485f,
            _ => 430f
        };
        return ToClient(LogicalWidth / 2f - 146f, y);
    }

    public Point CreateWorldButton()
    {
        return ToClient(LogicalWidth / 2f + 130f, 534f);
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

    public Point AdvancedSeedRandomizeButton()
    {
        return ToClient(LogicalWidth / 2f - 220f, 230f);
    }

    public Point WorldAdvancedApplyButton()
    {
        return ToClient(LogicalWidth / 2f, 534f);
    }

    public Point PlayerPlayButton(int favoritePlayers)
    {
        float outerWidth = GetSelectListOuterWidth();
        float left = LogicalWidth / 2f - outerWidth / 2f;
        float itemTop = 232f + favoritePlayers * 101f;
        return ToClient(left + 33f, itemTop + 79f);
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
        if (IsMainMenuUpscaleDisabled())
        {
            return 1f;
        }

        return Math.Max(1f, clientHeight / 900f);
    }

    private static bool IsMainMenuUpscaleDisabled()
    {
        try
        {
            string configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Games",
                "Terraria",
                "config.json");
            if (!File.Exists(configPath))
            {
                return false;
            }

            using FileStream stream = File.OpenRead(configPath);
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(stream);
            return document.RootElement.TryGetProperty("SettingDontScaleMainMenuUp", out System.Text.Json.JsonElement value) &&
                value.ValueKind == System.Text.Json.JsonValueKind.True;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Failed to read Terraria main menu scale setting.");
            return false;
        }
    }
}
