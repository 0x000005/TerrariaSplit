using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TerrariaSplit.MemoryBridge.Payload
{
    public static partial class EntryPoint
    {
        private static bool TryHandleCreatePlayer(string command, out PayloadCommandResult result)
        {
            string[] parts = (command ?? string.Empty).Split(new[] { '\n' }, StringSplitOptions.None);
            if (parts.Length == 0 || !string.Equals(parts[0], "create-player", StringComparison.Ordinal))
            {
                result = null;
                return false;
            }

            if (parts.Length != 4)
            {
                result = new PayloadCommandResult(40, "The Race player configuration is invalid.", false);
                return true;
            }

            string name;
            string template;
            byte difficulty;
            try
            {
                name = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1])).Trim();
                template = Encoding.UTF8.GetString(Convert.FromBase64String(parts[2])).Trim();
                difficulty = ParsePlayerDifficulty(parts[3]);
            }
            catch (Exception ex)
            {
                result = new PayloadCommandResult(40, "The Race player configuration is invalid: " + ex.Message, false);
                return true;
            }

            if (name.Length == 0 || name.Length > 20 || difficulty > 3 ||
                (template.Length > 0 && (template.IndexOf('{') < 0 || template.LastIndexOf('}') < 0)))
            {
                result = new PayloadCommandResult(40, "The Race player configuration is invalid.", false);
                return true;
            }

            result = CreatePlayerOnMainThread(name, template, difficulty);
            return true;
        }

        private static PayloadCommandResult CreatePlayerOnMainThread(string name, string template, byte difficulty)
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

            if (terraria == null || terraria.GetName().Version != new Version(1, 4, 5, 8) ||
                terraria.ManifestModule.ModuleVersionId != SupportedMvid)
            {
                return new PayloadCommandResult(41, "The Terraria player creator is not compatible with this client.", false);
            }

            Type mainType = terraria.GetType("Terraria.Main", false);
            MethodInfo queue = mainType == null
                ? null
                : mainType.GetMethod(
                    "QueueMainThreadAction",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { typeof(Action) },
                    null);
            if (queue == null)
            {
                return new PayloadCommandResult(42, "The Terraria main-thread player creator is unavailable.", false);
            }

            FieldInfo showSplash = mainType.GetField(
                "showSplash",
                BindingFlags.Static | BindingFlags.Public);
            if (showSplash == null)
            {
                return new PayloadCommandResult(42, "The Terraria player creator readiness state is unavailable.", false);
            }

            if ((bool)showSplash.GetValue(null))
            {
                return PlayerCreationNotReady();
            }

            var request = new MainThreadPlayerCreationRequest();
            Action action = delegate
            {
                if (!request.TryBegin())
                {
                    return;
                }

                try
                {
                    request.Complete(CreateAndSaveLocalPlayer(terraria, mainType, name, template, difficulty));
                }
                catch (Exception ex)
                {
                    request.Complete(new PayloadCommandResult(43, "The Race player could not be created: " + Unwrap(ex).Message, false));
                }
            };

            try
            {
                queue.Invoke(null, new object[] { action });
            }
            catch (Exception ex)
            {
                if (request.TryCancelBeforeStart())
                {
                    request.Dispose();
                    return new PayloadCommandResult(42, "The Race player creation action could not be queued: " + Unwrap(ex).Message, false);
                }

                request.Wait();
            }

            if (!request.Wait(TimeSpan.FromSeconds(10)) && request.TryCancelBeforeStart())
            {
                request.Dispose();
                return new PayloadCommandResult(44, "The Race player creation action timed out before it started.", false);
            }

            // If the action started at the timeout boundary, it already owns the save operation.
            // Waiting for completion keeps the returned path and the on-disk player in one state.
            request.Wait();
            PayloadCommandResult result = request.Result;
            request.Dispose();
            return result ?? new PayloadCommandResult(43, "The Race player creator returned no result.", false);
        }

        private static PayloadCommandResult PlayerCreationNotReady()
        {
            return new PayloadCommandResult(
                45,
                "Terraria is still starting; Race player creation will retry.",
                false);
        }

        private static PayloadCommandResult CreateAndSaveLocalPlayer(
            Assembly terraria,
            Type mainType,
            string name,
            string template,
            byte difficulty)
        {
            Type playerType = RequireType(terraria, "Terraria.Player");
            Type creationType = RequireType(terraria, "Terraria.GameContent.UI.States.UICharacterCreation");
            Type playerFileDataType = RequireType(terraria, "Terraria.IO.PlayerFileData");
            Type fileMetadataType = RequireType(terraria, "Terraria.IO.FileMetadata");
            Type fileType = RequireType(terraria, "Terraria.IO.FileType");

            object player = Activator.CreateInstance(playerType);
            object creation = Activator.CreateInstance(creationType, new[] { player });
            if (template.Length > 0)
            {
                ApplyPlayerTemplate(creationType, creation, template);
            }

            RequireField(playerType, "name").SetValue(player, name);
            RequireField(playerType, "difficulty").SetValue(player, difficulty);
            RequireMethod(creationType, "TryAutoAssigningHair", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(creation, null);
            RequireMethod(creationType, "SetupPlayerStatsAndInventoryBasedOnDifficulty", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(creation, null);
            ClearInitialItemPrefixes(terraria, playerType, player);

            MethodInfo getPath = mainType.GetMethod(
                "GetPlayerPathFromName",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(bool) },
                null);
            if (getPath == null)
            {
                throw new MissingMethodException(mainType.FullName, "GetPlayerPathFromName");
            }

            string path = (string)getPath.Invoke(null, new object[] { name, false });
            object data = Activator.CreateInstance(playerFileDataType, new object[] { path, false });
            object playerFileType = Enum.Parse(fileType, "Player", false);
            object metadata = RequireMethod(fileMetadataType, "FromCurrentSettings", BindingFlags.Static | BindingFlags.Public)
                .Invoke(null, new[] { playerFileType });
            RequireField(playerFileDataType.BaseType, "Metadata").SetValue(data, metadata);
            RequireProperty(playerFileDataType, "Player").SetValue(data, player, null);

            MethodInfo save = playerType.GetMethod(
                "SavePlayer",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { playerFileDataType, typeof(bool) },
                null);
            if (save == null)
            {
                throw new MissingMethodException(playerType.FullName, "SavePlayer");
            }

            save.Invoke(null, new object[] { data, true });
            RequireMethod(mainType, "LoadPlayers", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
            if (!File.Exists(path))
            {
                throw new IOException("Terraria did not create the local player file.");
            }

            return new PayloadCommandResult(0, Path.GetFullPath(path), false);
        }

        private static void ClearInitialItemPrefixes(Assembly terraria, Type playerType, object player)
        {
            Type itemType = RequireType(terraria, "Terraria.Item");
            MethodInfo resetPrefix = RequireMethod(
                itemType,
                "ResetPrefix",
                BindingFlags.Instance | BindingFlags.Public);

            ResetItemPrefixes(
                (Array)RequireField(playerType, "inventory").GetValue(player),
                resetPrefix);
            ResetItemPrefixes(
                (Array)RequireField(playerType, "armor").GetValue(player),
                resetPrefix);
        }

        private static void ResetItemPrefixes(Array items, MethodInfo resetPrefix)
        {
            foreach (object item in items)
            {
                if (item != null)
                {
                    resetPrefix.Invoke(item, null);
                }
            }
        }

        private static void ApplyPlayerTemplate(Type creationType, object creation, string template)
        {
            string previous = string.Empty;
            bool hadText = false;
            try
            {
                previous = Clipboard.GetText(TextDataFormat.UnicodeText);
                hadText = previous.Length > 0;
                Clipboard.SetText(template, TextDataFormat.UnicodeText);
                RequireMethod(creationType, "Click_PastePlayerTemplate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(creation, new object[] { null, null });
            }
            finally
            {
                if (hadText)
                {
                    Clipboard.SetText(previous, TextDataFormat.UnicodeText);
                }
                else
                {
                    Clipboard.Clear();
                }
            }
        }

        private static byte ParsePlayerDifficulty(string value)
        {
            if (string.Equals(value, "Mediumcore", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.Equals(value, "Hardcore", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            return string.Equals(value, "Journey", StringComparison.OrdinalIgnoreCase) ? (byte)3 : (byte)0;
        }

        private static Type RequireType(Assembly assembly, string name)
        {
            Type type = assembly.GetType(name, false);
            if (type == null)
            {
                throw new TypeLoadException(name);
            }

            return type;
        }

        private static FieldInfo RequireField(Type type, string name)
        {
            FieldInfo field = type == null ? null : type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(type == null ? string.Empty : type.FullName, name);
            }

            return field;
        }

        private static PropertyInfo RequireProperty(Type type, string name)
        {
            PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
            {
                throw new MissingMemberException(type.FullName, name);
            }

            return property;
        }

        private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags)
        {
            MethodInfo method = type.GetMethod(name, flags);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, name);
            }

            return method;
        }

        private static Exception Unwrap(Exception exception)
        {
            while (exception is TargetInvocationException && exception.InnerException != null)
            {
                exception = exception.InnerException;
            }

            return exception;
        }

        private sealed class MainThreadPlayerCreationRequest : IDisposable
        {
            private const int Pending = 0;
            private const int Running = 1;
            private const int Completed = 2;
            private const int Canceled = 3;
            private readonly ManualResetEvent completed = new ManualResetEvent(false);
            private int state;

            public PayloadCommandResult Result { get; private set; }

            public bool TryBegin()
            {
                return Interlocked.CompareExchange(ref state, Running, Pending) == Pending;
            }

            public bool TryCancelBeforeStart()
            {
                return Interlocked.CompareExchange(ref state, Canceled, Pending) == Pending;
            }

            public void Complete(PayloadCommandResult result)
            {
                Result = result;
                Interlocked.Exchange(ref state, Completed);
                completed.Set();
            }

            public bool Wait(TimeSpan timeout)
            {
                return completed.WaitOne(timeout);
            }

            public void Wait()
            {
                completed.WaitOne();
            }

            public void Dispose()
            {
                completed.Dispose();
            }
        }
    }
}
