using System.Media;

namespace TerrariaSplit.UI;

internal sealed class SoundPlayerService
{
    private readonly object syncRoot = new();
    private readonly List<ActiveSound> activeSounds = new();

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

        ActiveSound? activeSound = null;
        try
        {
            activeSound = new ActiveSound(new SoundPlayer(resolvedPath));
            lock (syncRoot)
            {
                activeSounds.Add(activeSound);
            }

            _ = Task.Run(() =>
            {
                try
                {
                    lock (syncRoot)
                    {
                        if (activeSound.StopRequested)
                        {
                            return;
                        }
                    }

                    activeSound.Player.PlaySync();
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, $"Failed to play sound: {resolvedPath}");
                }
                finally
                {
                    lock (syncRoot)
                    {
                        activeSounds.Remove(activeSound);
                    }

                    activeSound.Player.Dispose();
                }
            });
        }
        catch (Exception ex)
        {
            if (activeSound is not null)
            {
                lock (syncRoot)
                {
                    activeSounds.Remove(activeSound);
                }

                activeSound.Player.Dispose();
            }

            AppLogger.Error(ex, $"Failed to play sound: {resolvedPath}");
        }
    }

    public void StopAll()
    {
        ActiveSound[] sounds;
        lock (syncRoot)
        {
            sounds = activeSounds.ToArray();
            foreach (ActiveSound sound in sounds)
            {
                sound.StopRequested = true;
            }

            activeSounds.Clear();
        }

        foreach (ActiveSound sound in sounds)
        {
            try
            {
                sound.Player.Stop();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Failed to stop sound.");
            }
        }
    }

    private sealed class ActiveSound
    {
        public ActiveSound(SoundPlayer player)
        {
            Player = player;
        }

        public SoundPlayer Player { get; }

        public bool StopRequested { get; set; }
    }
}
