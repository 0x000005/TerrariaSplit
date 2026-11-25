using System;

namespace TerrariaSplit.Compatibility
{
    public sealed class TerrariaRaceBuild
    {
        internal TerrariaRaceBuild(string version, string compatibilityId, Guid moduleVersionId, int worldFileVersion)
        {
            Version = new Version(version);
            CompatibilityId = compatibilityId;
            ModuleVersionId = moduleVersionId;
            WorldFileVersion = worldFileVersion;
        }

        public Version Version { get; }
        public string CompatibilityId { get; }
        public Guid ModuleVersionId { get; }
        public int WorldFileVersion { get; }

        public bool Matches(Version version, Guid moduleVersionId)
        {
            return Version.Equals(TerrariaFeatureCatalog.NormalizeVersion(version)) && ModuleVersionId == moduleVersionId;
        }
    }

    public static class TerrariaBuilds
    {
        public const string Race1461Version = "1.4.6.1";
        public const string Race1461CompatibilityId = "terraria-1.4.6.1-win-x86-mvid-316ae3eded524207a7cf8c70fc902a47";
        public const int Race1461WorldFileVersion = 329;

        public static TerrariaRaceBuild Race1461 { get; } = new TerrariaRaceBuild(
            Race1461Version,
            Race1461CompatibilityId,
            new Guid("316ae3ed-ed52-4207-a7cf-8c70fc902a47"),
            Race1461WorldFileVersion);
    }
}
