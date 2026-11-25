using TerrariaSplit.Compatibility;
using TerrariaSplit.Race.Contracts;
using TerrariaSplit.Terraria.Automation;
using TerrariaSplit.Terraria.Memory;
using TerrariaSplit.Terraria.Processes;

namespace TerrariaSplit.Tests;

internal static class CompatibilityFlowTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("version capabilities classify groups and select one implementation for every feature", TestSuite.Core, MappingJourney);
        yield return TestCase.Sync("version capabilities dispatch foreground and server targets independently without unknown-version fallback", TestSuite.Core, DispatchJourney);
        yield return TestCase.Sync("version capabilities restrict Race to 1461 while preserving ordinary legacy features", TestSuite.Core, StrictFeaturesJourney);
        yield return TestCase.Async("version capabilities isolate unavailable menu and item data from working Split detection", TestSuite.Core, RuntimeIsolationJourney);
    }

    private static void MappingJourney()
    {
        (string? Text, TerrariaVersionGroup Group)[] versions =
        [
            ("1.4.4.9", TerrariaVersionGroup.Version144),
            (" v1.4.4.8 ", TerrariaVersionGroup.Version144),
            ("V1.4.5.7", TerrariaVersionGroup.Version145),
            ("1.4.5.8", TerrariaVersionGroup.Version145),
            ("1.4.6", TerrariaVersionGroup.Version146Plus),
            ("1.4.6.1", TerrariaVersionGroup.Version146Plus),
            ("1.4.7.0", TerrariaVersionGroup.Version146Plus),
            ("1.5.0.0", TerrariaVersionGroup.Version146Plus),
            ("1.4.3.6", TerrariaVersionGroup.Unknown),
            ("not-a-version", TerrariaVersionGroup.Unknown),
            (null, TerrariaVersionGroup.Unknown)
        ];

        foreach ((string? text, TerrariaVersionGroup group) in versions)
        {
            Check.Equal(group, TerrariaFeatureCatalog.GetVersionGroup(text));
            foreach (TerrariaFeature feature in Enum.GetValues<TerrariaFeature>())
            {
                TerrariaFeatureMatch match = TerrariaFeatureCatalog.Resolve(feature, text);
                Check.Equal(group, match.VersionGroup);
                if (group == TerrariaVersionGroup.Unknown)
                {
                    Check.False(match.CanAttempt);
                    Check.Equal(TerrariaImplementation.None, match.Implementation);
                    continue;
                }

                if (TerrariaFeatureCatalog.GetCategory(feature) == TerrariaFeatureCategory.Race || feature == TerrariaFeature.UiScale)
                    continue;
                Check.True(match.CanAttempt);
                Check.True(match.Rule.IsImplemented);
                Check.True((match.Rule.VersionGroups & group) != 0);
                int matchingRules = TerrariaFeatureCatalog.Rules.Count(rule => rule.Feature == feature &&
                    (rule.VersionGroups & group) != 0 && (rule.ExactBuild is null || rule.ExactBuild.Version == match.Version));
                Check.Equal(1, matchingRules);
            }
        }

        foreach (TerrariaFeature feature in Enum.GetValues<TerrariaFeature>())
            Check.True(TerrariaFeatureCatalog.Rules.Any(rule => rule.Feature == feature));
        Check.Equal(TerrariaFeatureSupport.Supported,
            TerrariaFeatureCatalog.Resolve(TerrariaFeature.ForegroundWorldCreation, "1.4.5.8").Support);
        Check.Equal(TerrariaFeatureSupport.Supported,
            TerrariaFeatureCatalog.Resolve(TerrariaFeature.ForegroundWorldCreation, "1.4.6.1").Support);
        Check.Equal(TerrariaFeatureSupport.Supported,
            TerrariaFeatureCatalog.Resolve(TerrariaFeature.SeedFiltering, "1.4.6.1").Support);
        Check.Equal(TerrariaFeatureSupport.AllowedToTry,
            TerrariaFeatureCatalog.Resolve(TerrariaFeature.BackgroundWorldCreation, "1.4.6.1").Support);
        Check.Throws<ArgumentOutOfRangeException>(() => TerrariaFeatureCatalog.Resolve((TerrariaFeature)999, "1.4.5.8"));
    }

    private static void DispatchJourney()
    {
        foreach (string version in new[] { "1.4.4.8", "1.4.4.9", "v1.4.4.90" })
        {
            Check.Equal(TerrariaMenuProfileKind.Legacy1449, TerrariaMenuProfile.FromVersion(version).Kind);
            Check.False(TerrariaMenuObserver.IsSupported(version));
        }
        foreach (string version in new[] { "1.4.5.7", "1.4.5.8", "1.4.6", "1.4.6.1" })
        {
            TerrariaMenuProfile profile = TerrariaMenuProfile.FromVersion(version);
            Check.Equal(TerrariaMenuProfileKind.Modern1458, profile.Kind);
            Check.True(TerrariaMenuObserver.IsSupported(version));
            Check.Equal(TerrariaFeatureCatalog.Resolve(TerrariaFeature.ForegroundWorldCreation, version).Version.ToString(), profile.GameVersion);
        }
        foreach (string? version in new[] { null, "", "unreadable", "1.4.3.6" })
        {
            Check.Throws<InvalidOperationException>(() => TerrariaMenuProfile.FromVersion(version));
            Check.False(TerrariaMenuObserver.IsSupported(version));
        }
        Check.Throws<ArgumentException>(() => TerrariaMenuGeometry.From(new System.Drawing.Size(800, 600), default, false));

        // Background selection belongs to the server executable, not the foreground game.
        TerrariaMenuProfile foreground = TerrariaMenuProfile.FromVersion("1.4.6.1");
        var legacyServer = new TerrariaServerTarget("legacy/TerrariaServer.exe", "1.4.4.9");
        var modernServer = new TerrariaServerTarget("modern/TerrariaServer.exe", "1.4.5.8");
        var unknownServer = new TerrariaServerTarget("unknown/TerrariaServer.exe", null);
        Check.Equal(TerrariaMenuProfileKind.Modern1458, foreground.Kind);
        Check.True(legacyServer.IsLegacy1449);
        Check.Equal(TerrariaImplementation.LegacyServer, legacyServer.GenerationCapability.Implementation);
        Check.Equal(TerrariaImplementation.ModernServer, modernServer.GenerationCapability.Implementation);
        Check.False(unknownServer.GenerationCapability.CanAttempt);
        Check.Equal(TerrariaImplementation.None, unknownServer.GenerationCapability.Implementation);
        Check.Equal(TerrariaImplementation.ResourceJudge1458, legacyServer.FilteringCapability.Implementation);
        Check.Equal(TerrariaImplementation.ResourceJudge1458, modernServer.FilteringCapability.Implementation);
        Check.False(TerrariaFeatureCatalog.Resolve(TerrariaFeature.RacePreparation, legacyServer.FileVersion).CanAttempt);
        Check.False(TerrariaFeatureCatalog.Resolve(TerrariaFeature.RacePreparation, modernServer.FileVersion).CanAttempt);
        Check.True(TerrariaFeatureCatalog.Resolve(TerrariaFeature.RacePreparation, foreground.GameVersion).CanAttempt);
    }

    private static void StrictFeaturesJourney()
    {
        foreach (TerrariaFeature feature in Enum.GetValues<TerrariaFeature>().Where(feature =>
                     TerrariaFeatureCatalog.GetCategory(feature) == TerrariaFeatureCategory.Race))
        {
            Check.True(TerrariaFeatureCatalog.SupportsRaceBuild(feature, new Version(1, 4, 6, 1), TerrariaBuilds.Race1461.ModuleVersionId));
            Check.False(TerrariaFeatureCatalog.SupportsRaceBuild(feature, new Version(1, 4, 6, 1), Guid.Empty));
            foreach (string version in new[] { "1.4.4.9", "1.4.5.7", "1.4.5.8", "1.4.5.9", "1.4.6", "1.4.6.2" })
            {
                Check.False(TerrariaFeatureCatalog.Resolve(feature, version).CanAttempt);
                Check.False(TerrariaFeatureCatalog.SupportsRaceBuild(feature, new Version(version), TerrariaBuilds.Race1461.ModuleVersionId));
            }
        }
        Check.True(RaceTerrariaCompatibility.IsSupportedVersion(" v1.4.6.1 "));
        Check.False(RaceTerrariaCompatibility.IsSupportedVersion("1.4.6"));
        Check.Equal(329, RaceTerrariaCompatibility.WorldFileVersion);
        Check.Equal(TerrariaBuilds.Race1461.CompatibilityId, RaceTerrariaCompatibility.Id);
        Check.False(TerrariaFeatureCatalog.Resolve(TerrariaFeature.UiScale, "1.4.4.9").CanAttempt);
        Check.Equal(TerrariaImplementation.UiScale145, TerrariaFeatureCatalog.Resolve(TerrariaFeature.UiScale, "1.4.5.8").Implementation);
        TerrariaFeatureMatch futurePatch = TerrariaFeatureCatalog.Resolve(TerrariaFeature.UiScale, "1.4.6.1");
        Check.Equal(TerrariaImplementation.UiScale146, futurePatch.Implementation);
        Check.Equal(TerrariaFeatureSupport.Supported, futurePatch.Support);
        Check.True(futurePatch.CanAttempt);
        Check.Equal(TerrariaFeatureSupport.Unsupported, TerrariaFeatureCatalog.Resolve(TerrariaFeature.RacePreparation, "1.4.5.8").Support);
    }

    private static async Task RuntimeIsolationJourney(CancellationToken token)
    {
        var layout = new TerrariaRuntimeMemoryLayout("1.4.6.1", new(new IntPtr(1000), IntPtr.Zero, IntPtr.Zero),
            new(new Dictionary<string, IntPtr> { ["boss:skeletron:defeated"] = new(2000) }),
            null, null, null, null, new(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, -1, -1, -1, -1, -1, -1, -1, -1), 2,
            CapabilityErrors: new Dictionary<string, string> { [nameof(TerrariaFeature.ItemDetection)] = "Missing CLR field Terraria.Player.inventory." });
        int probes = 0;
        var cache = new TerrariaRuntimeLayoutCache(_ => { probes++; return Task.FromResult(layout); });
        var identity = new TerrariaProcessIdentity(42, 100);
        Check.True(ReferenceEquals(layout, await cache.GetAsync(identity, null, token)));
        Check.True(layout.ValidationError(TerrariaFeature.BossAndEventDetection) is null);
        Check.Equal("Missing CLR field Terraria.Player.inventory.", layout.ValidationError(TerrariaFeature.ItemDetection));
        Check.True(layout.ValidationError(TerrariaFeature.ForegroundWorldCreation) is not null);
        Check.False(cache.TryGet(identity, out _, TerrariaFeature.ForegroundWorldCreation));
        Check.True(cache.TryGet(identity, out _, TerrariaFeature.BossAndEventDetection));
        await Check.ThrowsAsync<InvalidOperationException>(async () =>
        {
            _ = await cache.GetAsync(identity, null, token, TerrariaFeature.ForegroundWorldCreation);
        });
        Check.Equal(2, probes);
        Check.True(ReferenceEquals(layout, await cache.GetAsync(identity, null, token)));
        Check.Equal(2, probes);
        Check.False(cache.TryGet(new TerrariaProcessIdentity(42, 101), out _));
    }
}
