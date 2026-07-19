using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TerrariaSplit.Race.InGame
{
    internal enum RaceInGamePageKind
    {
        Home,
        MemberJoin,
        WorldSource,
        WorldCreation,
        SpecialSeeds,
        WorldFilters,
        Progress,
        Lobby
    }

    internal enum RaceInGameControlKind
    {
        Heading,
        Label,
        TextField,
        Toggle,
        Button,
        Progress
    }

    internal enum RaceInGameActionKind
    {
        Activate,
        TextSubmitted,
        Close
    }

    internal sealed class RaceInGameControl
    {
        public RaceInGameControl(
            string id,
            RaceInGameControlKind kind,
            string label,
            string value,
            bool enabled,
            bool selected,
            int progressValue,
            int maxLength,
            bool allowEmpty,
            string layoutGroup,
            string iconPath = "",
            string description = "")
        {
            Id = id ?? string.Empty;
            Kind = kind;
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            Enabled = enabled;
            Selected = selected;
            ProgressValue = Math.Max(0, Math.Min(100, progressValue));
            MaxLength = Math.Max(0, maxLength);
            AllowEmpty = allowEmpty;
            LayoutGroup = layoutGroup ?? string.Empty;
            IconPath = iconPath ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public string Id { get; private set; }

        public RaceInGameControlKind Kind { get; private set; }

        public string Label { get; private set; }

        public string Value { get; private set; }

        public bool Enabled { get; private set; }

        public bool Selected { get; private set; }

        public int ProgressValue { get; private set; }

        public int MaxLength { get; private set; }

        public bool AllowEmpty { get; private set; }

        public string LayoutGroup { get; private set; }

        public string IconPath { get; private set; }

        public string Description { get; private set; }
    }

    internal sealed class RaceInGameSnapshot
    {
        public RaceInGameSnapshot(
            long revision,
            bool visible,
            RaceInGamePageKind pageKind,
            string title,
            string status,
            string closeLabel,
            IList<RaceInGameControl> controls)
        {
            Revision = revision;
            Visible = visible;
            PageKind = pageKind;
            Title = title ?? string.Empty;
            Status = status ?? string.Empty;
            CloseLabel = closeLabel ?? string.Empty;
            Controls = controls == null
                ? new RaceInGameControl[0]
                : new List<RaceInGameControl>(controls).ToArray();
        }

        public long Revision { get; private set; }

        public bool Visible { get; private set; }

        public RaceInGamePageKind PageKind { get; private set; }

        public string Title { get; private set; }

        public string Status { get; private set; }

        public string CloseLabel { get; private set; }

        public RaceInGameControl[] Controls { get; private set; }
    }

    internal sealed class RaceInGameAction
    {
        public RaceInGameAction(
            long actionId,
            long snapshotRevision,
            string controlId,
            RaceInGameActionKind kind,
            string value)
        {
            ActionId = actionId;
            SnapshotRevision = snapshotRevision;
            ControlId = controlId ?? string.Empty;
            Kind = kind;
            Value = value ?? string.Empty;
        }

        public long ActionId { get; private set; }

        public long SnapshotRevision { get; private set; }

        public string ControlId { get; private set; }

        public RaceInGameActionKind Kind { get; private set; }

        public string Value { get; private set; }
    }

    internal static class RaceInGameProtocol
    {
        private const int ProtocolVersion = 4;
        private const int MaximumPayloadBytes = 96 * 1024;
        private const int MaximumStringBytes = 64 * 1024;
        private const int MaximumControls = 512;
        private const int MaximumActions = 64;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public static string EncodeSnapshot(RaceInGameSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException("snapshot");
            }

            return Encode(delegate(BinaryWriter writer)
            {
                writer.Write(ProtocolVersion);
                writer.Write(snapshot.Revision);
                writer.Write(snapshot.Visible);
                writer.Write((int)snapshot.PageKind);
                WriteString(writer, snapshot.Title);
                WriteString(writer, snapshot.Status);
                WriteString(writer, snapshot.CloseLabel);
                RaceInGameControl[] controls = snapshot.Controls ?? new RaceInGameControl[0];
                if (controls.Length > MaximumControls)
                {
                    throw new InvalidDataException("The Race menu contains too many controls.");
                }

                writer.Write(controls.Length);
                for (int index = 0; index < controls.Length; index++)
                {
                    RaceInGameControl control = controls[index];
                    WriteString(writer, control.Id);
                    writer.Write((int)control.Kind);
                    WriteString(writer, control.Label);
                    WriteString(writer, control.Value);
                    writer.Write(control.Enabled);
                    writer.Write(control.Selected);
                    writer.Write(control.ProgressValue);
                    writer.Write(control.MaxLength);
                    writer.Write(control.AllowEmpty);
                    WriteString(writer, control.LayoutGroup);
                    WriteString(writer, control.IconPath);
                    WriteString(writer, control.Description);
                }
            });
        }

        public static RaceInGameSnapshot DecodeSnapshot(string encoded)
        {
            return Decode(encoded, delegate(BinaryReader reader)
            {
                RequireVersion(reader);
                long revision = reader.ReadInt64();
                bool visible = reader.ReadBoolean();
                RaceInGamePageKind pageKind = ReadEnum<RaceInGamePageKind>(reader);
                string title = ReadString(reader);
                string status = ReadString(reader);
                string closeLabel = ReadString(reader);
                int count = ReadBoundedCount(reader, MaximumControls);
                var controls = new List<RaceInGameControl>(count);
                for (int index = 0; index < count; index++)
                {
                    string id = ReadString(reader);
                    RaceInGameControlKind kind = ReadEnum<RaceInGameControlKind>(reader);
                    string label = ReadString(reader);
                    string value = ReadString(reader);
                    bool enabled = reader.ReadBoolean();
                    bool selected = reader.ReadBoolean();
                    int progress = reader.ReadInt32();
                    int maxLength = reader.ReadInt32();
                    bool allowEmpty = reader.ReadBoolean();
                    string layoutGroup = ReadString(reader);
                    string iconPath = ReadString(reader);
                    string description = ReadString(reader);
                    if (progress < 0 || progress > 100 || maxLength < 0)
                    {
                        throw new InvalidDataException("The Race menu control is invalid.");
                    }

                    controls.Add(new RaceInGameControl(
                        id,
                        kind,
                        label,
                        value,
                        enabled,
                        selected,
                        progress,
                        maxLength,
                        allowEmpty,
                        layoutGroup,
                        iconPath,
                        description));
                }

                return new RaceInGameSnapshot(
                    revision,
                    visible,
                    pageKind,
                    title,
                    status,
                    closeLabel,
                    controls);
            });
        }

        public static string EncodeActions(IList<RaceInGameAction> actions)
        {
            actions = actions ?? new RaceInGameAction[0];
            if (actions.Count > MaximumActions)
            {
                throw new InvalidDataException("The Race menu returned too many actions.");
            }

            return Encode(delegate(BinaryWriter writer)
            {
                writer.Write(ProtocolVersion);
                writer.Write(actions.Count);
                for (int index = 0; index < actions.Count; index++)
                {
                    RaceInGameAction action = actions[index];
                    writer.Write(action.ActionId);
                    writer.Write(action.SnapshotRevision);
                    WriteString(writer, action.ControlId);
                    writer.Write((int)action.Kind);
                    WriteString(writer, action.Value);
                }
            });
        }

        public static RaceInGameAction[] DecodeActions(string encoded)
        {
            return Decode(encoded, delegate(BinaryReader reader)
            {
                RequireVersion(reader);
                int count = ReadBoundedCount(reader, MaximumActions);
                var actions = new RaceInGameAction[count];
                for (int index = 0; index < count; index++)
                {
                    actions[index] = new RaceInGameAction(
                        reader.ReadInt64(),
                        reader.ReadInt64(),
                        ReadString(reader),
                        ReadEnum<RaceInGameActionKind>(reader),
                        ReadString(reader));
                }

                return actions;
            });
        }

        private static string Encode(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Utf8))
            {
                write(writer);
                writer.Flush();
                if (stream.Length > MaximumPayloadBytes)
                {
                    throw new InvalidDataException("The Race menu payload is too large.");
                }

                return Convert.ToBase64String(stream.ToArray());
            }
        }

        private static T Decode<T>(string encoded, Func<BinaryReader, T> read)
        {
            byte[] data;
            try
            {
                data = Convert.FromBase64String(encoded ?? string.Empty);
            }
            catch (FormatException ex)
            {
                throw new InvalidDataException("The Race menu payload is not valid Base64.", ex);
            }

            if (data.Length == 0 || data.Length > MaximumPayloadBytes)
            {
                throw new InvalidDataException("The Race menu payload length is invalid.");
            }

            using (var stream = new MemoryStream(data, false))
            using (var reader = new BinaryReader(stream, Utf8))
            {
                T result = read(reader);
                if (stream.Position != stream.Length)
                {
                    throw new InvalidDataException("The Race menu payload contains trailing data.");
                }

                return result;
            }
        }

        private static void RequireVersion(BinaryReader reader)
        {
            if (reader.ReadInt32() != ProtocolVersion)
            {
                throw new InvalidDataException("The Race menu protocol version is not supported.");
            }
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            byte[] data = Utf8.GetBytes(value ?? string.Empty);
            if (data.Length > MaximumStringBytes)
            {
                throw new InvalidDataException("A Race menu string is too long.");
            }

            writer.Write(data.Length);
            writer.Write(data);
        }

        private static string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > MaximumStringBytes)
            {
                throw new InvalidDataException("A Race menu string length is invalid.");
            }

            byte[] data = reader.ReadBytes(length);
            if (data.Length != length)
            {
                throw new EndOfStreamException();
            }

            return Utf8.GetString(data);
        }

        private static int ReadBoundedCount(BinaryReader reader, int maximum)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > maximum)
            {
                throw new InvalidDataException("The Race menu item count is invalid.");
            }

            return count;
        }

        private static T ReadEnum<T>(BinaryReader reader) where T : struct
        {
            int value = reader.ReadInt32();
            if (!Enum.IsDefined(typeof(T), value))
            {
                throw new InvalidDataException("The Race menu enum value is invalid.");
            }

            return (T)Enum.ToObject(typeof(T), value);
        }
    }
}
