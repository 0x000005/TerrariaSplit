using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TerrariaSplit.Configuration;
using TerrariaSplit.Models;
using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.Race.Client;

public static class RaceRoutePayloadFactory
{
    private const long MaxEmbeddedIconBytes = 2 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static RaceRoutePayload Create(AppSettings settings)
    {
        string routeJson = JsonSerializer.Serialize(CreateSyncDocument(settings), JsonOptions);
        SplitDefinition[] definitions = SplitCatalog.Build(settings).ToArray();
        RaceSplitDefinition[] splits = definitions
            .Select(static (definition, index) => new RaceSplitDefinition(
                index,
                definition.Id,
                definition.DisplayName,
                definition.IsAttached)
            {
                IconFileNames = definition.IconFileNames.Select(NormalizePayloadFileName).ToArray(),
                IconKeys = definition.IconKeys.ToArray(),
                Conditions = CreateConditionDefinitions(definition)
            })
            .ToArray();
        return new RaceRoutePayload(
            Hash(routeJson),
            CreateSummary(splits),
            routeJson,
            splits)
        {
            Icons = CreateIconPayloads(definitions)
        };
    }

    internal static bool TryDeserializeSyncedSettings(
        RaceRoutePayload payload,
        out RaceRouteSyncSettings settings,
        out string detail)
    {
        settings = new RaceRouteSyncSettings();
        detail = string.Empty;
        try
        {
            RaceRouteSyncDocument? document = JsonSerializer.Deserialize<RaceRouteSyncDocument>(
                payload.SerializedRouteJson,
                JsonOptions);
            if (document is null)
            {
                detail = "Race route payload is empty.";
                return false;
            }

            settings = new RaceRouteSyncSettings
            {
                SplitRoute = document.SplitRoute ?? new List<SplitRouteEntry>(),
                ReferenceSet = CloneReferenceSet(document.ReferenceSet)
            };
            return true;
        }
        catch (JsonException ex)
        {
            detail = ex.Message;
            return false;
        }
    }

    public static bool TryDeserializeRoute(
        RaceRoutePayload payload,
        out RouteSettings route,
        out string detail)
    {
        route = new RouteSettings();
        if (!TryDeserializeSyncedSettings(payload, out RaceRouteSyncSettings settings, out detail))
        {
            return false;
        }

        route.SplitRoute = settings.SplitRoute;
        return true;
    }

    private static RaceRouteSyncDocument CreateSyncDocument(AppSettings settings)
    {
        ReferenceSplitSet activeReference = ReferenceSplitSetService.GetActiveReferenceSet(settings);
        return new RaceRouteSyncDocument
        {
            SplitRoute = settings.Route.SplitRoute.Select(CreateSyncedRouteEntry).ToList(),
            ReferenceSet = CloneReferenceSet(activeReference, "Race Reference")
        };
    }

    private static IReadOnlyList<RaceSplitConditionDefinition> CreateConditionDefinitions(SplitDefinition definition)
    {
        SplitCondition[] facts = definition.Condition
            .ToFlatGroup()
            .GetFactConditions()
            .ToArray();
        if (facts.Length == 0)
        {
            return [];
        }

        return facts
            .Select((fact, index) =>
            {
                SplitTargetDefinition? target = SplitCatalog.TryGetTargetByFactKey(fact.FactKey, out SplitTargetDefinition resolved)
                    ? resolved
                    : null;
                return new RaceSplitConditionDefinition(
                    index,
                    fact.FactKey,
                    target?.Id,
                    target?.DisplayName ?? fact.FactKey,
                    NormalizePayloadFileName(ResolveConditionIconFileName(definition, target)));
            })
            .ToArray();
    }

    private static string? ResolveConditionIconFileName(SplitDefinition definition, SplitTargetDefinition? target)
    {
        if (target is not null)
        {
            for (int index = 0; index < definition.IconKeys.Count && index < definition.IconFileNames.Count; index++)
            {
                if (string.Equals(definition.IconKeys[index], target.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return definition.IconFileNames[index];
                }
            }
        }

        return definition.IconFileNames.Count == 1
            ? definition.IconFileNames[0]
            : target?.IconFileName;
    }

    private static IReadOnlyList<RaceRouteIconPayload> CreateIconPayloads(IReadOnlyList<SplitDefinition> definitions)
    {
        var icons = new Dictionary<string, RaceRouteIconPayload>(StringComparer.OrdinalIgnoreCase);
        foreach (SplitDefinition definition in definitions)
        {
            int count = Math.Min(definition.IconKeys.Count, definition.IconFileNames.Count);
            for (int index = 0; index < count; index++)
            {
                AddIconPayload(icons, definition.IconKeys[index], definition.IconFileNames[index]);
            }

            foreach (SplitCondition fact in definition.Condition.ToFlatGroup().GetFactConditions())
            {
                SplitTargetDefinition? target = SplitCatalog.TryGetTargetByFactKey(fact.FactKey, out SplitTargetDefinition resolved)
                    ? resolved
                    : null;
                AddIconPayload(
                    icons,
                    target?.Id ?? fact.FactKey,
                    ResolveConditionIconFileName(definition, target));
            }
        }

        return icons.Values.ToArray();
    }

    private static void AddIconPayload(
        Dictionary<string, RaceRouteIconPayload> icons,
        string? key,
        string? fileName)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        string normalizedKey = key.Trim();
        string normalizedFileName = NormalizePayloadFileName(fileName);
        if (string.IsNullOrWhiteSpace(normalizedFileName))
        {
            return;
        }

        string payloadKey = normalizedKey + "|" + normalizedFileName;
        if (icons.ContainsKey(payloadKey))
        {
            return;
        }

        icons[payloadKey] = new RaceRouteIconPayload(
            normalizedKey,
            normalizedFileName,
            TryReadIconDataBase64(fileName.Trim(), normalizedKey));
    }

    private static string? TryReadIconDataBase64(string fileName, string key)
    {
        try
        {
            if (!TryResolveIconDataPath(fileName, key, out string path))
            {
                return null;
            }

            var info = new FileInfo(path);
            if (info.Length <= 0 || info.Length > MaxEmbeddedIconBytes)
            {
                return null;
            }

            return Convert.ToBase64String(File.ReadAllBytes(path));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool TryResolveIconDataPath(string fileName, string key, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (File.Exists(fileName))
        {
            path = fileName;
            return true;
        }

        if (SplitCatalog.TryGetReferenceIconFileName(key, out string referenceFileName) &&
            TryResolvePackagedIconPath(referenceFileName, key, out path))
        {
            return true;
        }

        return TryResolvePackagedIconPath(fileName, key, out path);
    }

    private static bool TryResolvePackagedIconPath(string fileName, string key, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        foreach (string directory in GetCandidateIconDirectories(key))
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

    private static IEnumerable<string> GetCandidateIconDirectories(string key)
    {
        string preferred = GetPreferredIconDirectory(key);
        yield return preferred;

        foreach (string directory in GetAllIconDirectories())
        {
            if (!string.Equals(directory, preferred, StringComparison.OrdinalIgnoreCase))
            {
                yield return directory;
            }
        }
    }

    private static string GetPreferredIconDirectory(string key)
    {
        if (SplitCatalog.TryGetBossFact(key, out _))
        {
            return GetIconDirectory("Bosses");
        }

        if (SplitCatalog.TryParseItemTargetId(key, out _))
        {
            return GetIconDirectory("Items");
        }

        if (SplitCatalog.TryParseNpcTargetId(key, out _))
        {
            return GetIconDirectory("NPCs");
        }

        if (SplitCatalog.TryParseBiomeTargetId(key, out _))
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

    private static ReferenceSplitSet? CloneReferenceSet(ReferenceSplitSet? source, string? name = null)
    {
        if (source is null)
        {
            return null;
        }

        return new ReferenceSplitSet
        {
            Name = string.IsNullOrWhiteSpace(name) ? source.Name : name,
            Splits = new Dictionary<string, string>(source.Splits, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static SplitRouteEntry CreateSyncedRouteEntry(SplitRouteEntry source)
    {
        SplitRouteEntry clone = SettingsRouteOverrideService.CloneEntry(source);
        if (SplitIconOverrideSource.Normalize(clone.IconOverride.Source) == SplitIconOverrideSource.CustomFile)
        {
            clone.IconOverride.FilePath = NormalizePayloadFileName(clone.IconOverride.FilePath);
        }

        return clone;
    }

    private static string NormalizePayloadFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFileName(value.Trim());
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }

    internal sealed class RaceRouteSyncSettings
    {
        public List<SplitRouteEntry> SplitRoute { get; init; } = new();

        public ReferenceSplitSet? ReferenceSet { get; init; }
    }

    private sealed class RaceRouteSyncDocument
    {
        public List<SplitRouteEntry>? SplitRoute { get; set; }

        public ReferenceSplitSet? ReferenceSet { get; set; }
    }

    private static string CreateSummary(IReadOnlyList<RaceSplitDefinition> splits)
    {
        if (splits.Count == 0)
        {
            return "empty route";
        }

        string first = splits[0].DisplayName;
        string last = splits[^1].DisplayName;
        return splits.Count == 1
            ? first
            : $"{splits.Count} splits: {first} -> {last}";
    }

    private static string Hash(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
