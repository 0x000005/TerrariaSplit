using System.Drawing;
using System.Drawing.Imaging;

namespace TerrariaSplit.UI.Rendering;

internal sealed class BossIconCache : IDisposable
{
    private const int FrameDelayPropertyId = 0x5100;

    private readonly Dictionary<string, IconPair> cache = new(StringComparer.OrdinalIgnoreCase);

    public bool AnimatedIconUsedInCurrentFrame { get; private set; }

    public void BeginRenderFrame()
    {
        AnimatedIconUsedInCurrentFrame = false;
    }

    public void TrackRendered(IconPair iconPair)
    {
        AnimatedIconUsedInCurrentFrame |= iconPair.IsAnimated;
    }

    public IconPair Load(SplitDefinition definition, string fileName, AppSettings settings)
    {
        string iconKey = GetIconKey(definition, fileName);
        string path = ResolveIconPath(fileName, iconKey);
        string cacheKey = $"icon:{path}";

        if (cache.TryGetValue(cacheKey, out IconPair? iconPair))
        {
            return iconPair;
        }

        IconFrameSet lit = File.Exists(path) ? LoadIconFrameSet(path, iconKey) : IconFrameSet.Static(CreatePlaceholderIcon());
        IconFrameSet undefeated = lit.Map(frame => CreateBossChecklistUndefeatedIcon(
            frame,
            settings.Overlay.UndefeatedIconGrayscalePercent,
            settings.Overlay.UndefeatedIconBrightnessPercent));
        IconFrameSet current = lit.Map(frame => CreateBossChecklistUndefeatedIcon(
            frame,
            Math.Max(0, settings.Overlay.UndefeatedIconGrayscalePercent - settings.Overlay.CurrentBossIconGrayscaleWeakenPercent),
            Math.Min(100, settings.Overlay.UndefeatedIconBrightnessPercent + settings.Overlay.CurrentBossIconBrightnessBoostPercent)));
        iconPair = new IconPair(lit, undefeated, current);
        cache[cacheKey] = iconPair;
        return iconPair;
    }

    private static IconFrameSet LoadIconFrameSet(string path, string iconKey)
    {
        using var source = new Bitmap(path);
        if (TryCreateImageAnimationFrames(source, out IReadOnlyList<Bitmap> frames, out IReadOnlyList<int> delays))
        {
            return new IconFrameSet(frames, delays);
        }

        return TryCreateItemAnimationFrame(source, iconKey, out Bitmap? frame)
            ? IconFrameSet.Static(frame)
            : IconFrameSet.Static(new Bitmap(source));
    }

    private static bool TryCreateImageAnimationFrames(
        Bitmap source,
        out IReadOnlyList<Bitmap> frames,
        out IReadOnlyList<int> delays)
    {
        frames = [];
        delays = [];
        Guid timeDimensionGuid = source.FrameDimensionsList.FirstOrDefault(guid => guid == FrameDimension.Time.Guid);
        if (timeDimensionGuid == Guid.Empty)
        {
            return false;
        }

        var dimension = new FrameDimension(timeDimensionGuid);
        int frameCount = source.GetFrameCount(dimension);
        if (frameCount <= 1)
        {
            return false;
        }

        int[] frameDelays = GetFrameDelays(source, frameCount);
        var animationFrames = new List<Bitmap>(frameCount);
        try
        {
            for (int i = 0; i < frameCount; i++)
            {
                source.SelectActiveFrame(dimension, i);
                animationFrames.Add(CloneCurrentFrame(source));
            }
        }
        catch
        {
            foreach (Bitmap frame in animationFrames)
            {
                frame.Dispose();
            }

            throw;
        }

        frames = animationFrames;
        delays = frameDelays;
        return true;
    }

    private static int[] GetFrameDelays(Image source, int frameCount)
    {
        var delays = Enumerable.Repeat(IconFrameSet.DefaultFrameDelayMs, frameCount).ToArray();
        try
        {
            PropertyItem? item = source.GetPropertyItem(FrameDelayPropertyId);
            byte[]? values = item?.Value;
            if (values is null)
            {
                return delays;
            }

            for (int i = 0; i < frameCount && i * 4 + 3 < values.Length; i++)
            {
                int hundredths = BitConverter.ToInt32(values, i * 4);
                delays[i] = Math.Max(IconFrameSet.MinimumFrameDelayMs, hundredths * 10);
            }
        }
        catch (ArgumentException)
        {
        }

        return delays;
    }

    private static Bitmap CloneCurrentFrame(Image source)
    {
        var frame = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(frame);
        graphics.Clear(Color.Transparent);
        graphics.DrawImage(source, 0, 0, source.Width, source.Height);
        return frame;
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
        AnimatedIconUsedInCurrentFrame = false;
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

internal sealed class IconPair : IDisposable
{
    private readonly IconFrameSet lit;
    private readonly IconFrameSet undefeated;
    private readonly IconFrameSet current;

    public IconPair(IconFrameSet lit, IconFrameSet undefeated, IconFrameSet current)
    {
        this.lit = lit;
        this.undefeated = undefeated;
        this.current = current;
    }

    public Image Lit => lit.First;

    public Image Undefeated => undefeated.First;

    public Image Current => current.First;

    public bool IsAnimated => lit.IsAnimated;

    public Image GetLitImage(DateTime nowUtc)
    {
        return lit.GetFrame(nowUtc);
    }

    public Image GetUndefeatedImage(DateTime nowUtc)
    {
        return undefeated.GetFrame(nowUtc);
    }

    public Image GetCurrentImage(DateTime nowUtc)
    {
        return current.GetFrame(nowUtc);
    }

    public void Dispose()
    {
        lit.Dispose();
        undefeated.Dispose();
        current.Dispose();
    }
}

internal sealed class IconFrameSet : IDisposable
{
    public const int DefaultFrameDelayMs = 100;
    public const int MinimumFrameDelayMs = 20;

    private readonly IReadOnlyList<Bitmap> frames;
    private readonly IReadOnlyList<int> delays;
    private readonly int totalDurationMs;

    public IconFrameSet(IReadOnlyList<Bitmap> frames, IReadOnlyList<int> delays)
    {
        if (frames.Count == 0)
        {
            throw new ArgumentException("Icon frame set must contain at least one frame.", nameof(frames));
        }

        this.frames = frames;
        this.delays = NormalizeDelays(delays, frames.Count);
        totalDurationMs = this.delays.Sum();
    }

    public Image First => frames[0];

    public bool IsAnimated => frames.Count > 1;

    public static IconFrameSet Static(Bitmap frame)
    {
        return new IconFrameSet([frame], [DefaultFrameDelayMs]);
    }

    public IconFrameSet Map(Func<Bitmap, Bitmap> transform)
    {
        return new IconFrameSet(frames.Select(transform).ToArray(), delays.ToArray());
    }

    public Image GetFrame(DateTime nowUtc)
    {
        if (frames.Count == 1)
        {
            return frames[0];
        }

        long elapsedMs = Math.Max(0, (long)(nowUtc.ToUniversalTime() - DateTime.UnixEpoch).TotalMilliseconds);
        int position = (int)(elapsedMs % totalDurationMs);
        int cursor = 0;
        for (int i = 0; i < frames.Count; i++)
        {
            cursor += delays[i];
            if (position < cursor)
            {
                return frames[i];
            }
        }

        return frames[^1];
    }

    public void Dispose()
    {
        foreach (Bitmap frame in frames)
        {
            frame.Dispose();
        }
    }

    private static IReadOnlyList<int> NormalizeDelays(IReadOnlyList<int> source, int count)
    {
        var normalized = new int[count];
        for (int i = 0; i < count; i++)
        {
            int delay = i < source.Count ? source[i] : DefaultFrameDelayMs;
            normalized[i] = Math.Max(MinimumFrameDelayMs, delay);
        }

        return normalized;
    }
}
