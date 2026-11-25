using System;
using System.Collections.ObjectModel;

namespace TerrariaSplit.Compatibility
{
    public sealed class TerrariaFeatureRule
    {
        internal TerrariaFeatureRule(TerrariaFeature feature, TerrariaImplementation implementation,
            TerrariaVersionGroup groups, bool implemented = true, TerrariaRaceBuild exactBuild = null, string[] verifiedVersions = null)
        {
            Feature = feature;
            Implementation = implementation;
            VersionGroups = groups;
            IsImplemented = implemented;
            ExactBuild = exactBuild;
            VerifiedVersions = Array.AsReadOnly(verifiedVersions != null
                ? Array.ConvertAll(verifiedVersions, text => TerrariaFeatureCatalog.NormalizeVersion(new Version(text)))
                : exactBuild == null ? new Version[0] : new[] { exactBuild.Version });
        }

        public TerrariaFeature Feature { get; }
        public TerrariaFeatureCategory Category { get { return TerrariaFeatureCatalog.GetCategory(Feature); } }
        public TerrariaImplementation Implementation { get; }
        public TerrariaVersionGroup VersionGroups { get; }
        public bool IsImplemented { get; }
        public TerrariaRaceBuild ExactBuild { get; }
        public ReadOnlyCollection<Version> VerifiedVersions { get; }

        internal bool IsVerifiedFor(Version version)
        {
            foreach (Version verified in VerifiedVersions) if (verified.Equals(version)) return true;
            return false;
        }
    }

    // A version match selects an implementation, not runtime readiness. Its owner must still
    // validate fields, files, instruction patterns or Hook fingerprints before executing it.
    public sealed class TerrariaFeatureMatch
    {
        internal TerrariaFeatureMatch(TerrariaFeature feature, Version version, TerrariaVersionGroup group,
            TerrariaFeatureRule rule, TerrariaFeatureSupport support)
        {
            Feature = feature;
            Version = version;
            VersionGroup = group;
            Rule = rule;
            Support = support;
        }

        public TerrariaFeature Feature { get; }
        public Version Version { get; }
        public TerrariaVersionGroup VersionGroup { get; }
        public TerrariaFeatureRule Rule { get; }
        public TerrariaFeatureSupport Support { get; }
        public TerrariaImplementation Implementation { get { return Rule == null ? TerrariaImplementation.None : Rule.Implementation; } }
        public bool CanAttempt { get { return Support == TerrariaFeatureSupport.Supported || Support == TerrariaFeatureSupport.AllowedToTry; } }

        public bool AcceptsBuild(Guid moduleVersionId)
        {
            return CanAttempt && (Rule.ExactBuild == null || Rule.ExactBuild.Matches(Version, moduleVersionId));
        }

        public string Diagnostic
        {
            get
            {
                return "feature=" + Feature + "; version=" + (Version == null ? "unknown" : Version.ToString()) +
                    "; group=" + VersionGroup + "; implementation=" + Implementation + "; support=" + Support;
            }
        }
    }

    public static class TerrariaFeatureCatalog
    {
        private const TerrariaVersionGroup ModernGroups = TerrariaVersionGroup.Version145 | TerrariaVersionGroup.Version146Plus;
        private static readonly string[] LegacyVerified = { "1.4.4.9" };
        private static readonly string[] ExistingVerified = { "1.4.4.9", "1.4.5.8" };
        private static readonly string[] ModernVerified = { "1.4.5.8", "1.4.6.1" };
        private static readonly string[] DetectionVerified = { "1.4.4.9", "1.4.5.8", "1.4.6.1" };

        public static ReadOnlyCollection<TerrariaFeatureRule> Rules { get; } = Array.AsReadOnly(new[]
        {
            new TerrariaFeatureRule(TerrariaFeature.ForegroundWorldCreation, TerrariaImplementation.LegacyMenu, TerrariaVersionGroup.Version144, verifiedVersions: LegacyVerified),
            new TerrariaFeatureRule(TerrariaFeature.ForegroundWorldCreation, TerrariaImplementation.StateDrivenMenu, ModernGroups, verifiedVersions: ModernVerified),
            new TerrariaFeatureRule(TerrariaFeature.SeedFiltering, TerrariaImplementation.ResourceJudge1458,
                TerrariaVersionGroup.Version144 | TerrariaVersionGroup.Version145, verifiedVersions: ExistingVerified),
            new TerrariaFeatureRule(TerrariaFeature.SeedFiltering, TerrariaImplementation.ResourceJudge1461,
                TerrariaVersionGroup.Version146Plus, verifiedVersions: new[] { "1.4.6.1" }),
            new TerrariaFeatureRule(TerrariaFeature.BackgroundWorldCreation, TerrariaImplementation.LegacyServer, TerrariaVersionGroup.Version144, verifiedVersions: LegacyVerified),
            new TerrariaFeatureRule(TerrariaFeature.BackgroundWorldCreation, TerrariaImplementation.ModernServer, ModernGroups, verifiedVersions: new[] { "1.4.5.8" }),
            new TerrariaFeatureRule(TerrariaFeature.RacePreparation, TerrariaImplementation.Race1461, TerrariaVersionGroup.Version146Plus, exactBuild: TerrariaBuilds.Race1461),
            new TerrariaFeatureRule(TerrariaFeature.RaceRandomControl, TerrariaImplementation.Race1461, TerrariaVersionGroup.Version146Plus, exactBuild: TerrariaBuilds.Race1461),
            new TerrariaFeatureRule(TerrariaFeature.RaceBossPenalty, TerrariaImplementation.Race1461, TerrariaVersionGroup.Version146Plus, exactBuild: TerrariaBuilds.Race1461),
            new TerrariaFeatureRule(TerrariaFeature.RaceInGameUi, TerrariaImplementation.Race1461, TerrariaVersionGroup.Version146Plus, exactBuild: TerrariaBuilds.Race1461),
            new TerrariaFeatureRule(TerrariaFeature.GameStateDetection, TerrariaImplementation.SharedMemoryReader, TerrariaVersionGroup.All, verifiedVersions: DetectionVerified),
            new TerrariaFeatureRule(TerrariaFeature.BossAndEventDetection, TerrariaImplementation.SharedMemoryReader, TerrariaVersionGroup.All, verifiedVersions: DetectionVerified),
            new TerrariaFeatureRule(TerrariaFeature.ItemDetection, TerrariaImplementation.SharedMemoryReader, TerrariaVersionGroup.All, verifiedVersions: DetectionVerified),
            new TerrariaFeatureRule(TerrariaFeature.BiomeDetection, TerrariaImplementation.SharedMemoryReader, TerrariaVersionGroup.All, verifiedVersions: DetectionVerified),
            new TerrariaFeatureRule(TerrariaFeature.NpcDetection, TerrariaImplementation.SharedMemoryReader, TerrariaVersionGroup.All, verifiedVersions: DetectionVerified),
            new TerrariaFeatureRule(TerrariaFeature.UiScale, TerrariaImplementation.UiScale145, TerrariaVersionGroup.Version145, verifiedVersions: new[] { "1.4.5.8" }),
            new TerrariaFeatureRule(TerrariaFeature.UiScale, TerrariaImplementation.UiScale146, TerrariaVersionGroup.Version146Plus, verifiedVersions: new[] { "1.4.6.1" })
        });

        public static bool TryParseVersion(string text, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string normalized = text.Trim();
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase)) normalized = normalized.Substring(1);
            Version parsed;
            if (!Version.TryParse(normalized, out parsed)) return false;
            version = NormalizeVersion(parsed);
            return true;
        }

        public static Version NormalizeVersion(Version version)
        {
            return version == null ? null : new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
        }

        public static TerrariaVersionGroup GetVersionGroup(string text)
        {
            Version version;
            return TryParseVersion(text, out version) ? GetVersionGroup(version) : TerrariaVersionGroup.Unknown;
        }

        private static TerrariaVersionGroup GetVersionGroup(Version version)
        {
            if (version.Major == 1 && version.Minor == 4 && version.Build == 4) return TerrariaVersionGroup.Version144;
            if (version.Major == 1 && version.Minor == 4 && version.Build == 5) return TerrariaVersionGroup.Version145;
            return version.CompareTo(new Version(1, 4, 6, 0)) >= 0 ? TerrariaVersionGroup.Version146Plus : TerrariaVersionGroup.Unknown;
        }

        public static TerrariaFeatureMatch Resolve(TerrariaFeature feature, string text)
        {
            GetCategory(feature); // Reject invalid feature IDs instead of treating them as unsupported versions.
            Version version;
            if (!TryParseVersion(text, out version))
                return new TerrariaFeatureMatch(feature, null, TerrariaVersionGroup.Unknown, null, TerrariaFeatureSupport.UnknownVersion);
            TerrariaVersionGroup group = GetVersionGroup(version);
            foreach (TerrariaFeatureRule rule in Rules)
            {
                if (rule.Feature != feature || (rule.VersionGroups & group) == 0) continue;
                if (rule.ExactBuild != null && !rule.ExactBuild.Version.Equals(version)) continue;
                TerrariaFeatureSupport support = !rule.IsImplemented ? TerrariaFeatureSupport.PendingAdaptation :
                    rule.IsVerifiedFor(version) ? TerrariaFeatureSupport.Supported : TerrariaFeatureSupport.AllowedToTry;
                return new TerrariaFeatureMatch(feature, version, group, rule, support);
            }
            bool pendingRace = GetCategory(feature) == TerrariaFeatureCategory.Race && group == TerrariaVersionGroup.Version146Plus;
            return new TerrariaFeatureMatch(feature, version, group, null,
                pendingRace ? TerrariaFeatureSupport.PendingAdaptation : TerrariaFeatureSupport.Unsupported);
        }

        public static bool SupportsRaceBuild(TerrariaFeature feature, Version version, Guid moduleVersionId)
        {
            if (GetCategory(feature) != TerrariaFeatureCategory.Race) throw new ArgumentException("A Race feature is required.", nameof(feature));
            return Resolve(feature, version == null ? null : version.ToString()).AcceptsBuild(moduleVersionId);
        }

        public static TerrariaFeatureCategory GetCategory(TerrariaFeature feature)
        {
            switch (feature)
            {
                case TerrariaFeature.ForegroundWorldCreation:
                case TerrariaFeature.SeedFiltering:
                case TerrariaFeature.BackgroundWorldCreation:
                    return TerrariaFeatureCategory.WorldCreation;
                case TerrariaFeature.RacePreparation:
                case TerrariaFeature.RaceRandomControl:
                case TerrariaFeature.RaceBossPenalty:
                case TerrariaFeature.RaceInGameUi:
                    return TerrariaFeatureCategory.Race;
                case TerrariaFeature.GameStateDetection:
                case TerrariaFeature.BossAndEventDetection:
                case TerrariaFeature.ItemDetection:
                case TerrariaFeature.BiomeDetection:
                case TerrariaFeature.NpcDetection:
                    return TerrariaFeatureCategory.SplitDetection;
                case TerrariaFeature.UiScale:
                    return TerrariaFeatureCategory.UiScale;
                default:
                    throw new ArgumentOutOfRangeException(nameof(feature));
            }
        }

    }
}
