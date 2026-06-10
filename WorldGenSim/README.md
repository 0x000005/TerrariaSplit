# WorldGenSim

`WorldGenSim` is the isolated workspace for the first-stage Terraria worldgen replica.

Stage 1 is intentionally split into two tracks:

1. Build a verifiable baseline that can run Terraria-compatible generation to the `Pyramids` pass.
2. Build a sample corpus reader from existing `.wld` files, so generated results can be compared against known worlds.

The first correctness target is small-world, crimson, normal non-secret seeds. The pyramid search area is `X=[35%,75%)` and `Y=[15%,35%)` of the world. A seed is only considered matched when the simulator and the reference world agree on:

- world metadata: seed text, size, difficulty, evil, special seed mask
- `Pyramids` outcome: whether a pyramid chest exists in the target scan area, and the generated chest loot
- later: pass-level `RandNext` values where a reference log is available

The reference world corpus currently expected by the tool is:

```text
C:\Users\HZR\Documents\My Games\Terraria\TerrariaSplitDeleted\PyramidWorlds
```

## Commands

```text
dotnet run --project WorldGenSim -- samples
dotnet run --project WorldGenSim -- samples <world-folder>
dotnet run --project WorldGenSim -- compare --limit 3
dotnet run --project WorldGenSim -- compare --backend replica
dotnet run --project WorldGenSim -- compare --backend echo
dotnet run --project WorldGenSim -- pyramid-smoke 540278984
dotnet run --project WorldGenSim -- passes-smoke 540278984 Pyramids
dotnet run --project WorldGenSim -- runner-smoke
```

`samples` scans `.wld` files recursively, reads their metadata, and prints a compact CSV-like table. This is the first verification input for later simulator-vs-world comparisons.

`runner-smoke` exercises the stage-1 pass runner and proves the key scheduling invariant used by Terraria 1.4.5 worldgen: every pass starts with a fresh `UnifiedRandom(seed)`, so two identical pass bodies produce the same `RandNext`.

`compare` is the long-term first-stage verification entry point. It reads reference worlds, asks the current simulator backend to generate the same seed, and compares the target pyramid chest loot while ignoring exact chest coordinates. The default `replica` backend runs the current pass replica. `--backend echo` is a comparer self-test only; it reads the reference sample back as the simulated result and must not be treated as generation.

The current replica backend has the official normal-world pass order wired through `Pyramids`. Most pass bodies needed before `Pyramids` are implemented or explicitly audited as skippable for the stage-1 target scope. The normal small-world crimson branch of `Corruption` is implemented because crimson can mutate sand inside the center target area, and that can change whether the official `Pyramids` pass accepts a dune candidate.

## Skip Policy

Terraria 1.4.5.6 resets the worldgen random stream at the start of each pass. Because of that, skipping an entire pass body does not change later pass RNG, but it can still be wrong if the skipped pass mutates tiles or state read by a later pass.

For stage 1, pass-level skips are limited to audited sky-only, cavern-only, bottom-only, far-edge, wall-only, or post-target generation that has no known pre-`Pyramids` dependency in the target pyramid area.

Horizontal target-area cropping is only applied to independent tile scans that do not consume random numbers. Do not crop an RNG-consuming loop unless the outside-target work is dry-run to preserve the official random stream before any target-area work.

## Current Baseline

The current simulator is scoped to the stage-1 target, not the final fast pyramid screener. On the 26-world reference corpus, the replica currently matches 24 worlds. The two known mismatches are:

- seed `1959861357`: reference has an extra voice item `#5485`; normal pyramid loot matches.
- seed `1883062725`: reference has target pyramid loot, replica currently reports `none`.
