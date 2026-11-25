using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Media.Imaging;
using TerrariaSplit.UI.Wpf.Settings;

namespace TerrariaSplit.UI.Wpf.Interop;

internal static class WpfIconHelper
{
    private static readonly ConcurrentDictionary<string, BitmapSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static BitmapSource? TryGetSplitIcon(SplitDefinition definition, int iconIndex = 0)
    {
        if (iconIndex < 0 ||
            iconIndex >= definition.IconFileNames.Count ||
            !BossIconAssetRegistry.Shared.TryResolve(definition, iconIndex, out BossIconAsset asset) ||
            asset.Data is null ||
            asset.Data.Length == 0)
        {
            return null;
        }

        string iconKey = iconIndex < definition.IconKeys.Count
            ? definition.IconKeys[iconIndex]
            : definition.Id;
        string cacheKey = $"{asset.CacheKey}\u001f{iconKey}";
        return Cache.GetOrAdd(cacheKey, _ => DecodeFirstFrame(asset.Data, iconKey));
    }

    public static Dictionary<string, SplitDefinition> BuildDefinitionMap(AppSettings settings)
    {
        var map = new Dictionary<string, SplitDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (SplitDefinition definition in SplitCatalog.Build(settings))
        {
            if (!string.IsNullOrWhiteSpace(definition.Id))
            {
                map.TryAdd(definition.Id, definition);
            }
        }

        return map;
    }

    private static BitmapSource? DecodeFirstFrame(byte[] imageBytes, string iconKey)
    {
        try
        {
            if (IsGifHeader(imageBytes))
            {
                return DecodeGifFirstFrame(imageBytes);
            }

            using var stream = new MemoryStream(imageBytes, writable: false);
            BitmapDecoder decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
            {
                return null;
            }

            BitmapSource frame = decoder.Frames[0];
            frame = TryCropItemAnimationFirstFrame(frame, iconKey);
            if (frame.CanFreeze)
            {
                frame.Freeze();
            }

            return frame;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static BitmapSource? DecodeGifFirstFrame(byte[] gifBytes)
    {
        using var stream = new MemoryStream(gifBytes, writable: false);
        using var source = new Bitmap(stream);
        Guid timeDimensionGuid = source.FrameDimensionsList.FirstOrDefault(guid => guid == FrameDimension.Time.Guid);
        if (timeDimensionGuid != Guid.Empty)
        {
            source.SelectActiveFrame(new FrameDimension(timeDimensionGuid), 0);
        }

        using var firstFrame = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(firstFrame))
        {
            g.Clear(Color.Transparent);
            g.DrawImage(source, 0, 0, source.Width, source.Height);
        }

        return WpfBitmapBridge.FromGdiBitmap(firstFrame);
    }

    private static BitmapSource TryCropItemAnimationFirstFrame(BitmapSource source, string iconKey)
    {
        if (!SplitCatalog.TryParseItemTargetId(iconKey, out int itemId) ||
            !ItemIconAnimationCatalog.TryGetAnimation(itemId, out ItemIconAnimation animation) ||
            animation.FrameCount <= 1)
        {
            return source;
        }

        int frameHeight = source.PixelHeight / animation.FrameCount;
        int trimmedFrameHeight = frameHeight - 2;
        if (frameHeight <= 2 ||
            trimmedFrameHeight <= 0 ||
            source.PixelHeight < animation.FrameCount ||
            source.PixelHeight % animation.FrameCount != 0)
        {
            return source;
        }

        return new CroppedBitmap(source, new Int32Rect(0, 0, source.PixelWidth, trimmedFrameHeight));
    }

    private static bool IsGifHeader(byte[] data)
    {
        return data.Length >= 6 &&
            data[0] == (byte)'G' &&
            data[1] == (byte)'I' &&
            data[2] == (byte)'F' &&
            data[3] == (byte)'8' &&
            (data[4] == (byte)'7' || data[4] == (byte)'9') &&
            data[5] == (byte)'a';
    }
}
