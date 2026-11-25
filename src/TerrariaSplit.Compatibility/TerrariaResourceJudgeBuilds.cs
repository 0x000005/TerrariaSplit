using System;

namespace TerrariaSplit.Compatibility
{
    public enum TerrariaResourceJudgeVersion
    {
        Default = 0,
        Version1458 = 1458,
        Version1461 = 1461
    }

    public sealed class TerrariaResourceJudgeBuild
    {
        internal TerrariaResourceJudgeBuild(TerrariaResourceJudgeVersion version, string terrariaVersion, string generationCompatibilityId)
        {
            Version = version;
            TerrariaVersion = terrariaVersion;
            CompatibilityId = "terraria-" + terrariaVersion + "-resource-judge-filter-v7-sizes-pass97";
            GenerationCompatibilityId = generationCompatibilityId;
        }

        public TerrariaResourceJudgeVersion Version { get; }
        public string TerrariaVersion { get; }
        public string CompatibilityId { get; }
        public string GenerationCompatibilityId { get; }
    }

    public static class TerrariaResourceJudgeBuilds
    {
        public static TerrariaResourceJudgeBuild Version1458 { get; } = new TerrariaResourceJudgeBuild(
            TerrariaResourceJudgeVersion.Version1458, "1.4.5.8",
            "terraria-1.4.5.8-win32-d5f79871fb8b07aac28253a2111ff195eb5dd3e63af7836949829119314d4815");

        public static TerrariaResourceJudgeBuild Version1461 { get; } = new TerrariaResourceJudgeBuild(
            TerrariaResourceJudgeVersion.Version1461, "1.4.6.1",
            "terraria-1.4.6.1-win32-dd2ea7e3198cfa11ed7c33654f97a0c8efb67ec36f861f817036e9d10d98f9c0");

        public static TerrariaResourceJudgeBuild ForVersion(TerrariaResourceJudgeVersion version)
        {
            switch (version)
            {
                case TerrariaResourceJudgeVersion.Version1458: return Version1458;
                case TerrariaResourceJudgeVersion.Default:
                case TerrariaResourceJudgeVersion.Version1461: return Version1461;
                default: throw new ArgumentOutOfRangeException(nameof(version));
            }
        }

        public static TerrariaResourceJudgeBuild ForGameVersion(string version)
        {
            TerrariaFeatureMatch match = TerrariaFeatureCatalog.Resolve(TerrariaFeature.SeedFiltering, version);
            if (!match.CanAttempt) throw new InvalidOperationException("Resource filtering is unavailable: " + match.Diagnostic);
            switch (match.Implementation)
            {
                case TerrariaImplementation.ResourceJudge1458: return Version1458;
                case TerrariaImplementation.ResourceJudge1461: return Version1461;
                default: throw new InvalidOperationException("No resource judge is registered: " + match.Diagnostic);
            }
        }
    }
}
