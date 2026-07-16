using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace TerrariaSplit.WorldGuard.Payload
{
    public static partial class EntryPoint
    {
        private static PayloadCommandResult PrepareRestart()
        {
            PayloadCommandResult cancelled = CancelRaceStartCountdownAndRestoreMenu();
            if (cancelled.Code != 0)
            {
                return cancelled;
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
                    return new PayloadCommandResult(45, "The Terraria restart helper is not compatible with this client.", false);
                }

                Type mainType = terraria.GetType("Terraria.Main", false);
                Type worldGenType = terraria.GetType("Terraria.WorldGen", false);
                FieldInfo gameMenu = mainType == null
                    ? null
                    : mainType.GetField("gameMenu", BindingFlags.Static | BindingFlags.Public);
                MethodInfo queue = mainType == null
                    ? null
                    : mainType.GetMethod(
                        "QueueMainThreadAction",
                        BindingFlags.Static | BindingFlags.Public,
                        null,
                        new[] { typeof(Action) },
                        null);
                MethodInfo saveAndQuit = worldGenType == null
                    ? null
                    : worldGenType.GetMethod(
                        "SaveAndQuit",
                        BindingFlags.Static | BindingFlags.Public,
                        null,
                        new[] { typeof(Action) },
                        null);
                if (gameMenu == null || queue == null || saveAndQuit == null)
                {
                    return new PayloadCommandResult(45, "The Terraria restart helper is unavailable.", false);
                }

                if ((bool)gameMenu.GetValue(null))
                {
                    return new PayloadCommandResult(0, string.Empty, false);
                }

                var completion = new TaskCompletionSource<PayloadCommandResult>();
                Action action = delegate
                {
                    try
                    {
                        if ((bool)gameMenu.GetValue(null))
                        {
                            completion.TrySetResult(new PayloadCommandResult(0, string.Empty, false));
                            return;
                        }

                        Action completed = delegate
                        {
                            completion.TrySetResult(new PayloadCommandResult(0, string.Empty, false));
                        };
                        saveAndQuit.Invoke(null, new object[] { completed });
                    }
                    catch (Exception ex)
                    {
                        completion.TrySetResult(new PayloadCommandResult(
                            46,
                            "Terraria could not return to the main menu: " + Unwrap(ex).Message,
                            false));
                    }
                };
                queue.Invoke(null, new object[] { action });
                if (!completion.Task.Wait(TimeSpan.FromSeconds(30)))
                {
                    return new PayloadCommandResult(47, "Timed out while Terraria was returning to the main menu.", false);
                }

                return completion.Task.Result;
            }
            catch (Exception ex)
            {
                return new PayloadCommandResult(
                    46,
                    "Terraria could not prepare the Race restart: " + Unwrap(ex).Message,
                    false);
            }
        }

        private static PayloadCommandResult CancelRaceStartCountdownAndRestoreMenu()
        {
            Interlocked.Increment(ref raceStartGeneration);
            WorldLockConfiguration activeConfiguration = configuration;
            if (activeConfiguration != null)
            {
                activeConfiguration.SetEntryAllowed(false);
            }

            if (Interlocked.Exchange(ref raceCountdownActive, 0) == 0)
            {
                return new PayloadCommandResult(0, string.Empty, false);
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

                Type mainType = terraria == null ? null : terraria.GetType("Terraria.Main", false);
                FieldInfo gameMenu = mainType == null
                    ? null
                    : mainType.GetField("gameMenu", BindingFlags.Static | BindingFlags.Public);
                FieldInfo menuMode = mainType == null
                    ? null
                    : mainType.GetField("menuMode", BindingFlags.Static | BindingFlags.Public);
                FieldInfo statusText = mainType == null
                    ? null
                    : mainType.GetField("statusText", BindingFlags.Static | BindingFlags.Public);
                MethodInfo queue = mainType == null
                    ? null
                    : mainType.GetMethod(
                        "QueueMainThreadAction",
                        BindingFlags.Static | BindingFlags.Public,
                        null,
                        new[] { typeof(Action) },
                        null);
                if (gameMenu == null || menuMode == null || statusText == null || queue == null)
                {
                    return new PayloadCommandResult(54, "The Terraria Race countdown could not be dismissed.", false);
                }

                var completion = new TaskCompletionSource<PayloadCommandResult>();
                Action restoreMenu = delegate
                {
                    try
                    {
                        if ((bool)gameMenu.GetValue(null) && (int)menuMode.GetValue(null) == 10)
                        {
                            statusText.SetValue(null, string.Empty);
                            menuMode.SetValue(null, 0);
                        }

                        completion.TrySetResult(new PayloadCommandResult(0, string.Empty, false));
                    }
                    catch (Exception ex)
                    {
                        completion.TrySetResult(new PayloadCommandResult(
                            54,
                            "Terraria could not dismiss the Race countdown: " + Unwrap(ex).Message,
                            false));
                    }
                };
                queue.Invoke(null, new object[] { restoreMenu });
                if (!completion.Task.Wait(TimeSpan.FromSeconds(5)))
                {
                    return new PayloadCommandResult(55, "Timed out while dismissing the Race countdown.", false);
                }

                return completion.Task.Result;
            }
            catch (Exception ex)
            {
                return new PayloadCommandResult(
                    54,
                    "Terraria could not dismiss the Race countdown: " + Unwrap(ex).Message,
                    false);
            }
        }
    }
}
