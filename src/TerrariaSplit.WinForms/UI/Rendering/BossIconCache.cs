using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;

namespace TerrariaSplit.UI.Rendering;

internal sealed class BossIconCache : IDisposable
{
    private const int FrameDelayPropertyId = 0x5100;

    private readonly ConcurrentDictionary<string, Lazy<IconPair>> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<IconRequestKey, ResolvedIconRequest> resolvedRequests =
        new(IconRequestKeyComparer.Instance);
    private readonly Func<string, bool> fileExists;

    public BossIconCache()
        : this(File.Exists)
    {
    }

    internal BossIconCache(Func<string, bool> fileExists)
    {
        this.fileExists = fileExists;
    }

    public bool AnimatedIconUsedInCurrentFrame { get; private set; }

    public void BeginRenderFrame()
    {
        AnimatedIconUsedInCurrentFrame = false;
    }

    public void TrackRendered(IconPair iconPair)
    {
        AnimatedIconUsedInCurrentFrame |= iconPair.IsAnimated;
    }

    public IconPair Load(SplitDefinition definition, int iconIndex, AppSettings settings)
    {
        if ((uint)iconIndex >= (uint)definition.IconFileNames.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(iconIndex));
        }

        string fileName = definition.IconFileNames[iconIndex];
        string iconKey = GetIconKey(definition, iconIndex);
        var requestKey = new IconRequestKey(definition.Id, fileName, iconKey);
        if (!resolvedRequests.TryGetValue(requestKey, out ResolvedIconRequest resolved))
        {
            string path = ResolveIconPath(fileName, iconKey);
            resolved = resolvedRequests.GetOrAdd(
                requestKey,
                new ResolvedIconRequest($"icon:{path}\u001f{iconKey}", path, iconKey));
        }

        if (cache.TryGetValue(resolved.CacheKey, out Lazy<IconPair>? cachedIcon))
        {
            return GetIconValue(resolved.CacheKey, cachedIcon);
        }

        Lazy<IconPair> lazyIcon = cache.GetOrAdd(
            resolved.CacheKey,
            _ => new Lazy<IconPair>(() =>
            {
                IconFrameSet lit = fileExists(resolved.Path)
                    ? LoadIconFrameSet(resolved.Path, resolved.IconKey)
                    : IconFrameSet.Static(CreatePlaceholderIcon());
                return CreateIconPair(lit, settings);
            }, LazyThreadSafetyMode.ExecutionAndPublication));
        return GetIconValue(resolved.CacheKey, lazyIcon);
    }

    public IconPair LoadEmbedded(string cacheKey, byte[] data, string iconKey, AppSettings settings)
    {
        string normalizedCacheKey = $"embedded:{cacheKey}";
        Lazy<IconPair> lazyIcon = cache.GetOrAdd(
            normalizedCacheKey,
            _ => new Lazy<IconPair>(() =>
            {
                IconFrameSet lit = data.Length > 0
                    ? LoadIconFrameSet(data, iconKey)
                    : IconFrameSet.Static(CreatePlaceholderIcon());
                return CreateIconPair(lit, settings);
            }, LazyThreadSafetyMode.ExecutionAndPublication));
        return GetIconValue(normalizedCacheKey, lazyIcon);
    }

    public void PreloadInitialFrame(
        IReadOnlyList<SplitStatusSnapshot> statuses,
        int currentSplitIndex,
        AppSettings settings)
    {
        StartupDiagnostics.RecordTrace("IconPreloadStarted");
        DateTime nowUtc = DateTime.UtcNow;
        int minimumRowCount = Math.Max(
            SplitDisplayRows.GetRequiredRowCount(settings, statuses, currentSplitIndex),
            SplitCompletionAnimationRenderer.ReservedRowCount);
        IEnumerable<int> statusIndexes = SplitDisplayRows.Build(
                settings,
                statuses,
                currentSplitIndex,
                minimumRowCount)
            .Select(row => row.StatusIndex)
            .Distinct();
        var requests = new List<(int StatusIndex, SplitDefinition Definition, int IconIndex, string FileName, string IconKey)>();
        foreach (int statusIndex in statusIndexes)
        {
            SplitDefinition definition = statuses[statusIndex].Definition;
            for (int iconIndex = 0; iconIndex < definition.IconFileNames.Count; iconIndex++)
            {
                requests.Add((
                    statusIndex,
                    definition,
                    iconIndex,
                    definition.IconFileNames[iconIndex],
                    GetIconKey(definition, iconIndex)));
            }
        }

        foreach ((int StatusIndex, SplitDefinition Definition, int IconIndex, string FileName, string IconKey) request in
                 requests.DistinctBy(
                     request => new IconRequestKey(request.Definition.Id, request.FileName, request.IconKey),
                     IconRequestKeyComparer.Instance))
        {
            try
            {
                IconPair icon = Load(request.Definition, request.IconIndex, settings);
                StartupDiagnostics.RecordTrace($"IconLoaded:{request.FileName}");
                _ = request.StatusIndex == currentSplitIndex
                    ? icon.GetCurrentImage(nowUtc)
                    : icon.GetUndefeatedImage(nowUtc);
                StartupDiagnostics.RecordTrace($"IconPrepared:{request.FileName}");
            }
            catch (Exception ex)
            {
                StaticAppLogger.Instance.Error(ex, $"Failed to preload overlay icon: {request.FileName}");
            }
        }

        StartupDiagnostics.RecordTrace("IconPreloadCompleted");
    }

    private static IconFrameSet LoadIconFrameSet(string path, string iconKey)
    {
        using var source = new Bitmap(path);
        return LoadIconFrameSet(source, iconKey);
    }

    private static IconFrameSet LoadIconFrameSet(byte[] data, string iconKey)
    {
        using var stream = new MemoryStream(data);
        using var source = new Bitmap(stream);
        return LoadIconFrameSet(source, iconKey);
    }

    private static IconFrameSet LoadIconFrameSet(Bitmap source, string iconKey)
    {
        if (TryCreateImageAnimationFrames(source, out IReadOnlyList<Bitmap> frames, out IReadOnlyList<int> delays))
        {
            return new IconFrameSet(frames, delays);
        }

        return TryCreateItemAnimationFrame(source, iconKey, out Bitmap? frame)
            ? IconFrameSet.Static(frame)
            : IconFrameSet.Static(new Bitmap(source));
    }

    private static IconPair CreateIconPair(IconFrameSet lit, AppSettings settings)
    {
        int undefeatedGrayscale = settings.Overlay.UndefeatedIconGrayscalePercent;
        int undefeatedBrightness = settings.Overlay.UndefeatedIconBrightnessPercent;
        int currentGrayscale = Math.Max(
            0,
            undefeatedGrayscale - settings.Overlay.CurrentBossIconGrayscaleWeakenPercent);
        int currentBrightness = Math.Min(
            100,
            undefeatedBrightness + settings.Overlay.CurrentBossIconBrightnessBoostPercent);
        return new IconPair(
            lit,
            () => lit.Map(frame => CreateBossChecklistUndefeatedIcon(
                frame,
                undefeatedGrayscale,
                undefeatedBrightness)),
            () => lit.Map(frame => CreateBossChecklistUndefeatedIcon(
                frame,
                currentGrayscale,
                currentBrightness)));
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
        resolvedRequests.Clear();
        foreach (Lazy<IconPair> lazyIcon in cache.Values)
        {
            if (lazyIcon.IsValueCreated)
            {
                lazyIcon.Value.Dispose();
            }
        }

        cache.Clear();
        AnimatedIconUsedInCurrentFrame = false;
    }

    public void Dispose()
    {
        Clear();
    }

    private IconPair GetIconValue(string cacheKey, Lazy<IconPair> lazyIcon)
    {
        try
        {
            return lazyIcon.Value;
        }
        catch
        {
            cache.TryRemove(cacheKey, out _);
            throw;
        }
    }

    private static string GetIconKey(SplitDefinition definition, int iconIndex)
    {
        return iconIndex < definition.IconKeys.Count
            ? definition.IconKeys[iconIndex]
            : definition.Id;
    }

    private string ResolveIconPath(string fileName, string iconKey)
    {
        if (!string.IsNullOrWhiteSpace(fileName) && fileExists(fileName))
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

    private bool TryResolvePackagedIconPath(string fileName, string iconKey, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        foreach (string directory in GetCandidateIconDirectories(iconKey))
        {
            string candidate = Path.Combine(directory, fileName);
            if (fileExists(candidate))
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

    private readonly record struct IconRequestKey(string DefinitionId, string FileName, string IconKey);

    private readonly record struct ResolvedIconRequest(string CacheKey, string Path, string IconKey);

    private sealed class IconRequestKeyComparer : IEqualityComparer<IconRequestKey>
    {
        public static IconRequestKeyComparer Instance { get; } = new();

        public bool Equals(IconRequestKey x, IconRequestKey y)
        {
            return string.Equals(x.DefinitionId, y.DefinitionId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.FileName, y.FileName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.IconKey, y.IconKey, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(IconRequestKey obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.DefinitionId ?? string.Empty),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.FileName ?? string.Empty),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.IconKey ?? string.Empty));
        }
    }

    private static Bitmap CreateBossChecklistUndefeatedIcon(
        Bitmap source,
        int grayscalePercent,
        int brightnessPercent)
    {
        var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        float grayscale = Math.Clamp(grayscalePercent, 0, 100) / 100f;
        float brightness = Math.Clamp(brightnessPercent, 0, 100) / 100f;
        Rectangle bounds = new(0, 0, source.Width, source.Height);
        Bitmap? convertedSource = null;
        Bitmap readableSource = source;
        if (source.PixelFormat is not PixelFormat.Format32bppArgb and not PixelFormat.Format32bppPArgb)
        {
            convertedSource = source.Clone(bounds, PixelFormat.Format32bppArgb);
            readableSource = convertedSource;
        }

        BitmapData? sourceData = null;
        BitmapData? destinationData = null;
        try
        {
            sourceData = readableSource.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            destinationData = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            int sourceStride = Math.Abs(sourceData.Stride);
            int destinationStride = Math.Abs(destinationData.Stride);
            byte[] sourcePixels = new byte[sourceStride * source.Height];
            byte[] destinationPixels = new byte[destinationStride * source.Height];
            Marshal.Copy(sourceData.Scan0, sourcePixels, 0, sourcePixels.Length);

            for (int y = 0; y < source.Height; y++)
            {
                int sourceRow = sourceData.Stride >= 0
                    ? y * sourceStride
                    : (source.Height - 1 - y) * sourceStride;
                int destinationRow = destinationData.Stride >= 0
                    ? y * destinationStride
                    : (source.Height - 1 - y) * destinationStride;
                for (int x = 0; x < source.Width; x++)
                {
                    int sourceOffset = sourceRow + x * 4;
                    byte alpha = sourcePixels[sourceOffset + 3];
                    if (alpha == 0)
                    {
                        continue;
                    }

                    int blue = sourcePixels[sourceOffset];
                    int green = sourcePixels[sourceOffset + 1];
                    int red = sourcePixels[sourceOffset + 2];
                    int gray = (int)Math.Round(red * 0.299 + green * 0.587 + blue * 0.114);
                    int destinationOffset = destinationRow + x * 4;
                    destinationPixels[destinationOffset] = (byte)Darken(Lerp(blue, gray, grayscale), brightness);
                    destinationPixels[destinationOffset + 1] = (byte)Darken(Lerp(green, gray, grayscale), brightness);
                    destinationPixels[destinationOffset + 2] = (byte)Darken(Lerp(red, gray, grayscale), brightness);
                    destinationPixels[destinationOffset + 3] = alpha;
                }
            }

            Marshal.Copy(destinationPixels, 0, destinationData.Scan0, destinationPixels.Length);
        }
        finally
        {
            if (sourceData is not null)
            {
                readableSource.UnlockBits(sourceData);
            }

            if (destinationData is not null)
            {
                bitmap.UnlockBits(destinationData);
            }

            convertedSource?.Dispose();
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
    private readonly Lazy<IconFrameSet> undefeated;
    private readonly Lazy<IconFrameSet> current;

    public IconPair(
        IconFrameSet lit,
        Func<IconFrameSet> createUndefeated,
        Func<IconFrameSet> createCurrent)
    {
        this.lit = lit;
        undefeated = new Lazy<IconFrameSet>(
            createUndefeated,
            LazyThreadSafetyMode.ExecutionAndPublication);
        current = new Lazy<IconFrameSet>(
            createCurrent,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public Image Lit => lit.First;

    public Image Undefeated => UndefeatedFrames.First;

    public Image Current => CurrentFrames.First;

    public bool IsAnimated => lit.IsAnimated;

    public Image GetLitImage(DateTime nowUtc)
    {
        return lit.GetFrame(nowUtc);
    }

    public Image GetUndefeatedImage(DateTime nowUtc)
    {
        return UndefeatedFrames.GetFrame(nowUtc);
    }

    public Image GetCurrentImage(DateTime nowUtc)
    {
        return CurrentFrames.GetFrame(nowUtc);
    }

    public void Dispose()
    {
        lit.Dispose();
        if (undefeated.IsValueCreated)
        {
            undefeated.Value.Dispose();
        }

        if (current.IsValueCreated)
        {
            current.Value.Dispose();
        }
    }

    private IconFrameSet UndefeatedFrames => undefeated.Value;

    private IconFrameSet CurrentFrames => current.Value;
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
