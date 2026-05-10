using System.Media;

namespace TerrariaSplit;

internal sealed class SoundPlayerService
{
    public void Play(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string resolvedPath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);
        if (!File.Exists(resolvedPath))
        {
            return;
        }

        try
        {
            _ = Task.Run(() =>
            {
                try
                {
                    using var player = new SoundPlayer(resolvedPath);
                    player.PlaySync();
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, $"Failed to play sound: {resolvedPath}");
                }
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"Failed to play sound: {resolvedPath}");
        }
    }
}
