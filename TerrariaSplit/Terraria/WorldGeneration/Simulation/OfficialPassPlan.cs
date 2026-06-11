namespace TerrariaSplit.Terraria.WorldGeneration.Simulation;

internal static class OfficialPassPlan
{
    public const string StopPassName = "Pyramids";

    // Terraria 1.4.5.6 normal small-world path through Pyramids.
    // Conditional secret-seed-only passes are intentionally excluded for the current
    // stage-1 target corpus: small, normal, crimson, non-secret worlds.
    private static readonly (string Name, double Weight)[] PassesToPyramids =
    [
        ("Terrain", 507.352d),
        ("Dunes", 239.7913d),
        ("Ocean Sand", 10.4129d),
        ("Sand Patches", 452.6755d),
        ("Tunnels", 4.3622d),
        ("Mount Caves", 49.9993d),
        ("Dirt Wall Backgrounds", 328.7817d),
        ("Rocks In Dirt", 1537.4661d),
        ("Dirt In Rocks", 1515.2301d),
        ("Clay", 314.8327d),
        ("Small Holes", 2955.9258d),
        ("Dirt Layer Caves", 238.2545d),
        ("Rock Layer Caves", 2708.3958d),
        ("Surface Caves", 42.3857d),
        ("Wavy Caves", 1d),
        ("Generate Ice Biome", 100.005d),
        ("Grass", 29.7885d),
        ("Jungle", 11205.83d),
        ("Mud Caves To Grass", 3319.761d),
        ("Full Desert", 9730.408d),
        ("Mushroom Patches", 743.7686d),
        ("Marble", 5358.8843d),
        ("Granite", 2142.6638d),
        ("Floating Islands", 1364.3461d),
        ("Dirt To Mud", 351.3519d),
        ("Silt", 211.84d),
        ("Shinies", 237.4298d),
        ("Webs", 50.6646d),
        ("Underworld", 8936.494d),
        ("Corruption", 1094.237d),
        ("Lakes", 12.1766d),
        ("Slush", 55.1857d),
        ("Dungeon", 477.1963d),
        ("Mountain Caves", 11.4819d),
        ("Beaches", 7.8287d),
        ("Gems", 895.426d),
        ("Gravitating Sand", 933.5295d),
        ("Create Ocean Caves", 1d),
        ("Shimmer", 1d),
        ("Clean Up Dirt", 697.0276d),
        ("Pyramids", 6.6884d)
    ];

    public static int PassCount => PassesToPyramids.Length;

    public static int ImplementedPassCount => 19;

    public static int ExplicitlySkippedPassCount => 22;

    public static int StubPassCount => PassesToPyramids.Length - ImplementedPassCount - ExplicitlySkippedPassCount;

    public static void AppendToPyramids(WorldGenerator generator)
    {
        for (int i = 0; i < PassesToPyramids.Length; i++)
        {
            (string name, double weight) = PassesToPyramids[i];
            if (string.Equals(name, "Terrain", StringComparison.Ordinal))
            {
                generator.Append(new DelegateGenPass(name, weight, TerrainPassReplica.Apply));
                continue;
            }

            if (string.Equals(name, "Dunes", StringComparison.Ordinal))
            {
                generator.Append(new DelegateGenPass(name, weight, DunesPassReplica.Apply));
                continue;
            }

            if (string.Equals(name, "Ocean Sand", StringComparison.Ordinal))
            {
                generator.Append(new DelegateGenPass(name, weight, OceanSandPassReplica.Apply));
                continue;
            }

            if (string.Equals(name, "Sand Patches", StringComparison.Ordinal))
            {
                AppendCandidateDependentPass(generator, name, weight, SandPatchesPassReplica.Apply);
                continue;
            }

            if (string.Equals(name, "Tunnels", StringComparison.Ordinal))
            {
                AppendCandidateDependentPass(generator, name, weight, EarlyWorldMutationPasses.ApplyTunnels);
                continue;
            }

            if (string.Equals(name, "Mount Caves", StringComparison.Ordinal))
            {
                AppendCandidateDependentPass(generator, name, weight, EarlyWorldMutationPasses.ApplyMountCaves);
                continue;
            }

            if (string.Equals(name, "Dirt Wall Backgrounds", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Rocks In Dirt", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Dirt In Rocks", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Clay", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Small Holes", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Dirt Layer Caves", StringComparison.Ordinal))
            {
                AppendCandidateDependentPass(generator, name, weight, EarlyWorldMutationPasses.ApplyDirtLayerCaves);
                continue;
            }

            if (string.Equals(name, "Rock Layer Caves", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Surface Caves", StringComparison.Ordinal))
            {
                AppendCandidateDependentPass(generator, name, weight, EarlyWorldMutationPasses.ApplySurfaceCaves);
                continue;
            }

            if (string.Equals(name, "Wavy Caves", StringComparison.Ordinal))
            {
                generator.Append(new DelegateGenPass(name, weight, static (_, _) =>
                {
                    // Official normal non-secret worlds skip this pass; it only mutates Don't Starve/remix variants.
                }));
                continue;
            }

            if (string.Equals(name, "Generate Ice Biome", StringComparison.Ordinal))
            {
                AppendCandidateDependentPass(generator, name, weight, IceBiomePassReplica.Apply);
                continue;
            }

            if (string.Equals(name, "Grass", StringComparison.Ordinal))
            {
                AppendCandidateDependentPass(generator, name, weight, GrassPassReplica.Apply);
                continue;
            }

            if (string.Equals(name, "Jungle", StringComparison.Ordinal))
            {
                AppendCandidateDependentPass(generator, name, weight, JunglePassReplica.Apply);
                continue;
            }

            if (string.Equals(name, "Mud Caves To Grass", StringComparison.Ordinal))
            {
                AppendCandidateDependentPass(generator, name, weight, MudCavesToGrassPassReplica.Apply);
                continue;
            }

            if (string.Equals(name, "Full Desert", StringComparison.Ordinal))
            {
                AppendCandidateDependentPass(generator, name, weight, FullDesertPassReplica.Apply);
                continue;
            }

            if (string.Equals(name, "Mushroom Patches", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Marble", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Granite", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Floating Islands", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Dirt To Mud", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Silt", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Shinies", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Webs", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Underworld", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Corruption", StringComparison.Ordinal))
            {
                AppendCandidateDependentPass(generator, name, weight, CrimsonPassReplica.Apply);
                continue;
            }

            if (string.Equals(name, "Lakes", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Slush", StringComparison.Ordinal))
            {
                AppendCandidateDependentPass(generator, name, weight, SnowConversionPasses.ApplySlush);
                continue;
            }

            if (string.Equals(name, "Dungeon", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Mountain Caves", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Beaches", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Gems", StringComparison.Ordinal))
            {
                AppendCandidateDependentPass(generator, name, weight, SandCleanupPasses.ApplyGemsSandSettling);
                continue;
            }

            if (string.Equals(name, "Gravitating Sand", StringComparison.Ordinal))
            {
                AppendCandidateDependentPass(generator, name, weight, SandCleanupPasses.ApplyGravitatingSand);
                continue;
            }

            if (string.Equals(name, "Create Ocean Caves", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Shimmer", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Clean Up Dirt", StringComparison.Ordinal))
            {
                AppendSkippedIsolatedPass(generator, name, weight);
                continue;
            }

            if (string.Equals(name, "Pyramids", StringComparison.Ordinal))
            {
                AppendCandidateDependentPass(generator, name, weight, PyramidsPassReplica.Apply);
                continue;
            }

            generator.Append(new DelegateGenPass(name, weight, static (_, _) =>
            {
            }));
        }
    }

    private static void AppendSkippedIsolatedPass(WorldGenerator generator, string name, double weight)
    {
        generator.Append(new DelegateGenPass(name, weight, static (_, _) =>
        {
            // Stage 1 stops at Pyramids. These audited skips are sky-only, cavern-only,
            // bottom-only, far-edge, wall-only, sand-protected, or post-target work
            // with no known pre-Pyramids dependency in the target pyramid area.
        }));
    }

    private static void AppendCandidateDependentPass(
        WorldGenerator generator,
        string name,
        double weight,
        Action<WorldGenContext, GenerationProgress> apply)
    {
        generator.Append(new DelegateGenPass(name, weight, (context, progress) =>
        {
            WorldGenState? state = context.State;
            if (state is not null && !WorldInterestArea.HasPotentialTargetPyramidCandidate(state))
            {
                progress.Set(1.0);
                return;
            }

            apply(context, progress);
        }));
    }
}
