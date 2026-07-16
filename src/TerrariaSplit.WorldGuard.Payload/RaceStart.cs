using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TerrariaSplit.WorldGuard.Payload
{
    public static partial class EntryPoint
    {
        private static PayloadCommandResult StartRaceAndEnterWorld(string command)
        {
            WorldLockConfiguration current = configuration;
            if (current == null)
            {
                return new PayloadCommandResult(3, "The Race hook has no active package.", false);
            }

            TimeSpan countdownDuration;
            string countdownFormat;
            if (!TryParseRaceStartCommand(command, out countdownDuration, out countdownFormat))
            {
                return new PayloadCommandResult(2, "The Race start command is invalid.", false);
            }

            try
            {
                Assembly terraria = null;
                foreach (Assembly candidate in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (string.Equals(candidate.GetName().Name, "Terraria", StringComparison.Ordinal))
                    {
                        terraria = candidate;
                        break;
                    }
                }

                if (terraria == null || terraria.GetName().Version != new Version(1, 4, 5, 6) ||
                    terraria.ManifestModule.ModuleVersionId != SupportedMvid)
                {
                    return new PayloadCommandResult(48, "The Terraria Race start helper is not compatible with this client.", false);
                }

                Type mainType = terraria.GetType("Terraria.Main", false);
                Type worldGenType = terraria.GetType("Terraria.WorldGen", false);
                Type playerFileDataType = terraria.GetType("Terraria.IO.PlayerFileData", false);
                Type worldFileDataType = terraria.GetType("Terraria.IO.WorldFileData", false);
                FieldInfo gameMenu = mainType == null ? null : mainType.GetField("gameMenu", BindingFlags.Static | BindingFlags.Public);
                FieldInfo playerList = mainType == null ? null : mainType.GetField("PlayerList", BindingFlags.Static | BindingFlags.Public);
                FieldInfo worldList = mainType == null ? null : mainType.GetField("WorldList", BindingFlags.Static | BindingFlags.Public);
                FieldInfo menuMultiplayer = mainType == null ? null : mainType.GetField("menuMultiplayer", BindingFlags.Static | BindingFlags.Public);
                FieldInfo menuServer = mainType == null ? null : mainType.GetField("menuServer", BindingFlags.Static | BindingFlags.Public);
                FieldInfo menuMode = mainType == null ? null : mainType.GetField("menuMode", BindingFlags.Static | BindingFlags.Public);
                MethodInfo queue = mainType == null
                    ? null
                    : mainType.GetMethod("QueueMainThreadAction", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(Action) }, null);
                MethodInfo loadPlayers = mainType == null
                    ? null
                    : mainType.GetMethod("LoadPlayers", BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
                MethodInfo selectPlayer = mainType == null || playerFileDataType == null
                    ? null
                    : mainType.GetMethod("SelectPlayer", BindingFlags.Static | BindingFlags.Public, null, new[] { playerFileDataType }, null);
                MethodInfo setWorldActive = worldFileDataType == null
                    ? null
                    : worldFileDataType.GetMethod("SetAsActive", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                MethodInfo playWorld = worldGenType == null
                    ? null
                    : worldGenType.GetMethod("playWorld", BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
                MethodInfo saveAndQuit = worldGenType == null
                    ? null
                    : worldGenType.GetMethod("SaveAndQuit", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(Action) }, null);
                FieldInfo statusText = mainType == null ? null : mainType.GetField("statusText", BindingFlags.Static | BindingFlags.Public);
                if (gameMenu == null || playerList == null || worldList == null || menuMultiplayer == null || menuServer == null ||
                    menuMode == null || queue == null || loadPlayers == null || selectPlayer == null || setWorldActive == null ||
                    playWorld == null || saveAndQuit == null || statusText == null)
                {
                    return new PayloadCommandResult(48, "The Terraria Race start helper is unavailable.", false);
                }

                int generation = Interlocked.Increment(ref raceStartGeneration);
                var activation = new TaskCompletionSource<PayloadCommandResult>();
                Action enterWorld = delegate
                {
                    Interlocked.Exchange(ref raceCountdownActive, 0);
                    if (!IsCurrentRaceStart(generation, current))
                    {
                        return;
                    }

                    try
                    {
                        if (!(bool)gameMenu.GetValue(null))
                        {
                            throw new InvalidOperationException("Terraria is not at the main menu.");
                        }

                        menuMultiplayer.SetValue(null, false);
                        menuServer.SetValue(null, false);
                        loadPlayers.Invoke(null, null);
                        object assignedPlayer = FindAssignedPlayer((IEnumerable)playerList.GetValue(null), current);
                        if (assignedPlayer == null)
                        {
                            throw new InvalidOperationException("The assigned Race player is unavailable.");
                        }

                        selectPlayer.Invoke(null, new[] { assignedPlayer });
                        object assignedWorld = FindAssignedWorld((IEnumerable)worldList.GetValue(null), current);
                        if (assignedWorld == null)
                        {
                            throw new InvalidOperationException("The assigned Race world is unavailable.");
                        }

                        current.SetEntryAllowed(true);
                        setWorldActive.Invoke(assignedWorld, null);
                        menuMode.SetValue(null, 10);
                        playWorld.Invoke(null, null);
                        runtimeFailure = null;
                    }
                    catch (Exception ex)
                    {
                        current.SetEntryAllowed(false);
                        runtimeFailure = "Terraria could not enter the assigned Race world: " + Unwrap(ex).Message;
                    }
                };

                Action beginCountdown = delegate
                {
                    if (!IsCurrentRaceStart(generation, current))
                    {
                        activation.TrySetResult(new PayloadCommandResult(
                            50,
                            "The Race start was superseded before its countdown began.",
                            false));
                        return;
                    }

                    try
                    {
                        Interlocked.Exchange(ref raceCountdownActive, 1);
                        menuMode.SetValue(null, 10);
                        SetRaceCountdownText(statusText, countdownFormat, countdownDuration);
                        Task.Factory.StartNew(delegate
                        {
                            try
                            {
                                Stopwatch countdown = Stopwatch.StartNew();
                                int previousSeconds = int.MinValue;
                                while (IsCurrentRaceStart(generation, current))
                                {
                                    TimeSpan remaining = countdownDuration - countdown.Elapsed;
                                    if (remaining <= TimeSpan.Zero)
                                    {
                                        queue.Invoke(null, new object[] { enterWorld });
                                        return;
                                    }

                                    int seconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
                                    if (seconds != previousSeconds)
                                    {
                                        previousSeconds = seconds;
                                        int displayedSeconds = seconds;
                                        Action updateCountdown = delegate
                                        {
                                            if (IsCurrentRaceStart(generation, current))
                                            {
                                                menuMode.SetValue(null, 10);
                                                statusText.SetValue(null, string.Format(
                                                    CultureInfo.CurrentCulture,
                                                    countdownFormat,
                                                    displayedSeconds));
                                            }
                                        };
                                        queue.Invoke(null, new object[] { updateCountdown });
                                    }

                                    Thread.Sleep(25);
                                }
                            }
                            catch (Exception ex)
                            {
                                Interlocked.Exchange(ref raceCountdownActive, 0);
                                current.SetEntryAllowed(false);
                                runtimeFailure = "Terraria could not show the Race countdown: " + Unwrap(ex).Message;
                            }
                        });
                        activation.TrySetResult(new PayloadCommandResult(0, current.PackageDigest, false));
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Exchange(ref raceCountdownActive, 0);
                        current.SetEntryAllowed(false);
                        runtimeFailure = "Terraria could not show the Race countdown: " + Unwrap(ex).Message;
                        activation.TrySetResult(new PayloadCommandResult(51, runtimeFailure, false));
                    }
                };

                Action returnToMenu = delegate
                {
                    try
                    {
                        if ((bool)gameMenu.GetValue(null))
                        {
                            beginCountdown();
                        }
                        else
                        {
                            saveAndQuit.Invoke(null, new object[] { beginCountdown });
                            activation.TrySetResult(new PayloadCommandResult(0, current.PackageDigest, false));
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Exchange(ref raceCountdownActive, 0);
                        current.SetEntryAllowed(false);
                        runtimeFailure = "Terraria could not return to the main menu: " + Unwrap(ex).Message;
                        activation.TrySetResult(new PayloadCommandResult(52, runtimeFailure, false));
                    }
                };

                queue.Invoke(null, new object[] { returnToMenu });
                if (!activation.Task.Wait(TimeSpan.FromSeconds(5)))
                {
                    Interlocked.CompareExchange(ref raceStartGeneration, generation + 1, generation);
                    Interlocked.Exchange(ref raceCountdownActive, 0);
                    current.SetEntryAllowed(false);
                    return new PayloadCommandResult(
                        53,
                        "Terraria did not execute the Race start action on its main thread.",
                        false);
                }

                return activation.Task.Result;
            }
            catch (Exception ex)
            {
                current.SetEntryAllowed(false);
                return new PayloadCommandResult(
                    49,
                    "Terraria could not start the Race: " + Unwrap(ex).Message,
                    false);
            }
        }

        private static bool IsCurrentRaceStart(int generation, WorldLockConfiguration current)
        {
            return generation == Interlocked.CompareExchange(ref raceStartGeneration, 0, 0) &&
                ReferenceEquals(configuration, current);
        }

        private static bool TryParseRaceStartCommand(
            string command,
            out TimeSpan countdownDuration,
            out string countdownFormat)
        {
            countdownDuration = TimeSpan.Zero;
            countdownFormat = string.Empty;
            string[] parts = (command ?? string.Empty).Split(new[] { '\n' }, StringSplitOptions.None);
            long countdownMilliseconds;
            if (parts.Length != 3 ||
                !string.Equals(parts[0], "start-race", StringComparison.Ordinal) ||
                !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out countdownMilliseconds) ||
                countdownMilliseconds <= 0 ||
                countdownMilliseconds > 60000)
            {
                return false;
            }

            try
            {
                countdownFormat = Encoding.UTF8.GetString(Convert.FromBase64String(parts[2]));
                countdownDuration = TimeSpan.FromMilliseconds(countdownMilliseconds);
                if (string.IsNullOrWhiteSpace(countdownFormat) || !countdownFormat.Contains("{0}"))
                {
                    return false;
                }

                string.Format(CultureInfo.InvariantCulture, countdownFormat, 7);
                return true;
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static void SetRaceCountdownText(
            FieldInfo statusText,
            string countdownFormat,
            TimeSpan remaining)
        {
            int seconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
            statusText.SetValue(null, string.Format(CultureInfo.CurrentCulture, countdownFormat, seconds));
        }

        private static object FindAssignedPlayer(IEnumerable players, WorldLockConfiguration current)
        {
            if (players == null)
            {
                return null;
            }

            foreach (object player in players)
            {
                string path = player == null ? string.Empty : (string)playerPathProperty.GetValue(player, null);
                if (current.MatchesPlayer(path))
                {
                    return player;
                }
            }

            return null;
        }

        private static object FindAssignedWorld(IEnumerable worlds, WorldLockConfiguration current)
        {
            if (worlds == null)
            {
                return null;
            }

            foreach (object world in worlds)
            {
                if (world == null)
                {
                    continue;
                }

                string path = (string)worldPathProperty.GetValue(world, null);
                int worldId = (int)worldIdField.GetValue(world);
                Guid uniqueId = (Guid)worldUniqueIdField.GetValue(world);
                if (current.Matches(path, worldId, uniqueId))
                {
                    return world;
                }
            }

            return null;
        }
    }
}
