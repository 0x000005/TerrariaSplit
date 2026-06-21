using System.Drawing;
using System.Drawing.Imaging;

namespace TerrariaSplit.UI.Rendering;

internal sealed class BossIconCache : IDisposable
{
    private readonly Dictionary<string, IconPair> cache = new(StringComparer.OrdinalIgnoreCase);

    public IconPair Load(SplitDefinition definition, string fileName, AppSettings settings)
    {
        string iconKey = GetIconKey(definition, fileName);
        string path = ResolveIconPath(fileName, iconKey);
        string cacheKey = $"icon:{path}";

        if (cache.TryGetValue(cacheKey, out IconPair? iconPair))
        {
            return iconPair;
        }

        Bitmap lit = File.Exists(path) ? LoadIconBitmap(path, iconKey) : CreatePlaceholderIcon();
        Bitmap undefeated = CreateBossChecklistUndefeatedIcon(
            lit,
            settings.Overlay.UndefeatedIconGrayscalePercent,
            settings.Overlay.UndefeatedIconBrightnessPercent);
        Bitmap current = CreateBossChecklistUndefeatedIcon(
            lit,
            Math.Max(0, settings.Overlay.UndefeatedIconGrayscalePercent - settings.Overlay.CurrentBossIconGrayscaleWeakenPercent),
            Math.Min(100, settings.Overlay.UndefeatedIconBrightnessPercent + settings.Overlay.CurrentBossIconBrightnessBoostPercent));
        iconPair = new IconPair(lit, undefeated, current);
        cache[cacheKey] = iconPair;
        return iconPair;
    }

    private static Bitmap LoadIconBitmap(string path, string iconKey)
    {
        using var source = new Bitmap(path);
        return TryCreateItemAnimationFrame(source, iconKey, out Bitmap? frame)
            ? frame
            : new Bitmap(source);
    }

    private static bool TryCreateItemAnimationFrame(Bitmap source, string iconKey, out Bitmap frame)
    {
        frame = null!;
        if (!SplitCatalog.TryParseItemTargetId(iconKey, out int itemId) ||
            !ItemIconAnimationCatalog.TryGetAnimation(itemId, out ItemIconAnimation animation) ||
            animation.FrameCount <= 1)
        {
            return false;
        }

        int frameHeight = source.Height / animation.FrameCount;
        int trimmedFrameHeight = frameHeight - 2;
        if (frameHeight <= 2 ||
            trimmedFrameHeight <= 0 ||
            source.Height < animation.FrameCount ||
            source.Height % animation.FrameCount != 0)
        {
            return false;
        }

        frame = source.Clone(
            new Rectangle(0, 0, source.Width, trimmedFrameHeight),
            PixelFormat.Format32bppArgb);
        return true;
    }

    public void Clear()
    {
        foreach (IconPair iconPair in cache.Values)
        {
            iconPair.Dispose();
        }

        cache.Clear();
    }

    public void Dispose()
    {
        Clear();
    }

    private static string GetIconKey(SplitDefinition definition, string fileName)
    {
        int index = definition.IconFileNames
            .Select((value, itemIndex) => new { value, itemIndex })
            .FirstOrDefault(item => string.Equals(item.value, fileName, StringComparison.OrdinalIgnoreCase))
            ?.itemIndex ?? -1;
        return index >= 0 && index < definition.IconKeys.Count
            ? definition.IconKeys[index]
            : definition.Id;
    }

    private static string ResolveIconPath(string fileName, string iconKey)
    {
        if (!string.IsNullOrWhiteSpace(fileName) && File.Exists(fileName))
        {
            return fileName;
        }

        if (SplitCatalog.TryGetReferenceIconFileName(iconKey, out string referenceFileName) &&
            TryResolvePackagedIconPath(referenceFileName, iconKey, out string referencePath))
        {
            return referencePath;
        }

        if (TryResolvePackagedIconPath(fileName, iconKey, out string iconPath))
        {
            return iconPath;
        }

        return Path.Combine(GetPreferredIconDirectory(iconKey), fileName);
    }

    private static bool TryResolvePackagedIconPath(string fileName, string iconKey, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        foreach (string directory in GetCandidateIconDirectories(iconKey))
        {
            string candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                path = candidate;
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> GetCandidateIconDirectories(string iconKey)
    {
        string preferred = GetPreferredIconDirectory(iconKey);
        yield return preferred;

        foreach (string directory in GetAllIconDirectories())
        {
            if (!string.Equals(directory, preferred, StringComparison.OrdinalIgnoreCase))
            {
                yield return directory;
            }
        }
    }

    private static string GetPreferredIconDirectory(string iconKey)
    {
        if (SplitCatalog.TryGetBossFact(iconKey, out _))
        {
            return GetIconDirectory("Bosses");
        }

        if (SplitCatalog.TryParseItemTargetId(iconKey, out _))
        {
            return GetIconDirectory("Items");
        }

        if (SplitCatalog.TryParseNpcTargetId(iconKey, out _))
        {
            return GetIconDirectory("NPCs");
        }

        if (SplitCatalog.TryParseBiomeTargetId(iconKey, out _))
        {
            return GetIconDirectory("Biomes");
        }

        return GetIconDirectory("Bosses");
    }

    private static IEnumerable<string> GetAllIconDirectories()
    {
        yield return GetIconDirectory("Bosses");
        yield return GetIconDirectory("Items");
        yield return GetIconDirectory("NPCs");
        yield return GetIconDirectory("Biomes");
    }

    private static string GetIconDirectory(string category)
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", category);
    }

    private static Bitmap CreateBossChecklistUndefeatedIcon(
        Bitmap source,
        int grayscalePercent,
        int brightnessPercent)
    {
        var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        float grayscale = Math.Clamp(grayscalePercent, 0, 100) / 100f;
        float brightness = Math.Clamp(brightnessPercent, 0, 100) / 100f;

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                Color pixel = source.GetPixel(x, y);
                if (pixel.A == 0)
                {
                    continue;
                }

                int gray = (int)Math.Round(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                int red = Darken(Lerp(pixel.R, gray, grayscale), brightness);
                int green = Darken(Lerp(pixel.G, gray, grayscale), brightness);
                int blue = Darken(Lerp(pixel.B, gray, grayscale), brightness);
                bitmap.SetPixel(x, y, Color.FromArgb(pixel.A, red, green, blue));
            }
        }

        return bitmap;
    }

    private static int Lerp(int from, int to, float amount)
    {
        return Math.Clamp((int)Math.Round(from + (to - from) * amount), 0, 255);
    }

    private static int Darken(int value, float amount)
    {
        return Math.Clamp((int)Math.Round(value * amount), 0, 255);
    }

    private static Bitmap CreatePlaceholderIcon()
    {
        var bitmap = new Bitmap(32, 32);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        using var brush = new SolidBrush(Color.FromArgb(100, 100, 100));
        graphics.FillEllipse(brush, 2, 2, 28, 28);
        return bitmap;
    }
}

internal sealed record IconPair(Image Lit, Image Undefeated, Image Current) : IDisposable
{
    public void Dispose()
    {
        Lit.Dispose();
        Undefeated.Dispose();
        Current.Dispose();
    }
}
