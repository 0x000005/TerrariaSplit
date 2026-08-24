# Pyramid Candidate Dependency Audit

This note is the working contract for the fast pyramid seed pre-screener in the
main TerrariaSplit project. The standalone `WorldGenSim` folder was historical
scratch space and has been removed; the authoritative implementation is this namespace:

```text
TerrariaSplit.Terraria.WorldGeneration.Simulation
```

## Scope

- Terraria 1.4.5.8 normal world generation.
- Small world only: 4200 x 1200.
- Crimson only.
- No special or secret seed flags.
- Pyramid target corridor: `X=[32%,68%)`, `Y=[15%,35%)`.
- The pre-screen is a fast first pass. The created `.wld` file scan remains the
  final guard against false positives.

The current accuracy policy prefers lower false positives. A conservative hard
reject is acceptable in known uncertain zones because it may create false
negatives, but it prevents wasting full world-creation attempts.

## Official Candidate Chain

The relevant official path, stopping at `Pyramids`, is:

1. `Reset`
   Rolls world metadata and persistent generation state such as dungeon side,
   dungeon location, jungle origin, snow origin, ore tiers, and layer heights.
2. `Terrain`
   Builds the surface and rock layer. Pyramid candidates later scan downward
   from their stored `Y` to the first active tile before world surface.
3. `DunesAndPyramidLocations`
   Creates `GenVars.PyrX/PyrY`. Each accepted dune can add a candidate at
   `x = rand(origin.X - 200, origin.X + 200)`, `y = first active + 20`.
4. `Ocean Sand`
   Can add extra pyramid candidates from beach-side sand.
5. Pre-pyramid mutation passes
   These can alter the first active scan tile or move sand near a candidate.
6. `Pyramids`
   Rechecks buildable band, scans down to the first active tile, requires sand,
   requires distance `>= 220` from previous candidates, then builds the pyramid
   and rolls the first pyramid chest loot.

The official dungeon location itself is side-limited: left dungeon is under
`20%` world width, right dungeon is over `80%`. `Pyramids` still rejects another
`15%` world width inward from `generatingDungeonPositionX`, so in the current
target corridor only `32%-35%` and `65%-68%` are dungeon-boundary uncertainty
zones.

## Pass Classification

Must stay exact or structurally equivalent:

- `Reset`: later passes read many rolled values directly.
- `Terrain`: candidate scan surfaces depend on active tile height and type.
- `Dunes`: authoritative source for most pyramid candidates.
- `Ocean Sand`: can add candidate points.
- `Pyramids`: must preserve acceptance order, spacing, and loot RNG.

Local simulation is required in the target corridor:

- `Sand Patches`
- `Tunnels`
- `Mount Caves`
- `Dirt Wall Backgrounds`
- `Dirt Layer Caves`
- `Surface Caves`
- `Generate Ice Biome`
- `Grass`
- `Jungle` main mud body and surface tunnel
- `Mud Caves To Grass`
- `Full Desert`
- `Corruption` as crimson conversion
- `Slush`
- `Gems`
- `Gravitating Sand`

These passes may be skipped only because their mutation is outside the current
target, is shielded by sand/height checks, or is paired with a conservative risk
gate:

- `Rocks In Dirt`
- `Dirt In Rocks`
- `Clay`
- `Small Holes`
- `Rock Layer Caves`
- `Mushroom Patches`
- `Marble`
- `Granite`
- `Floating Islands`
- `Dirt To Mud`
- `Silt`
- `Shinies`
- `Webs`
- `Underworld`
- `Lakes`
- `Dungeon`
- `Mountain Caves`
- `Beaches`
- `Create Ocean Caves`
- `Shimmer`
- `Clean Up Dirt`

Skipping a pass body does not advance later pass RNG incorrectly because the
worldgen runner resets the `UnifiedRandom(seed)` stream at the start of each
pass. That is not enough by itself: skipped tile/state writes must still be
proved irrelevant for the target corridor or handled by a hard risk gate.

## Hard Risk Gates

`PyramidCandidateRisk.HardRejectMask` currently rejects a simulated target chest
when the candidate lies in a known under-modeled zone:

- `CrimsonConvertedScanSand`
  Crimson can convert the scan sand column before `Pyramids`.
- `FullDesertBoundaryUncertain`
  The clipped Full Desert hive simulation can miss mutations in the scan prefix.
- `FullDesertSurfaceUncertain`
  Very narrow upper-desert sand caps are artifact-prone. Official generation can
  expose dirt there even when the fast model still sees sand.
- `SkippedDungeonBoundaryUncertain`
  Only applies in the dungeon-shadow edge bands: `32%-35%` on left-dungeon
  worlds and `65%-68%` on right-dungeon worlds.
- `JungleMudCoverageUncertain`
  Skipped early RNG-consuming bodies can shift jungle mud coverage enough that
  official generation turns an apparent scan sand tile into mud.

Known probe seeds used for these gates:

- `702683177`: official no target tower; fast model would keep scan sand without
  the jungle uncertainty gate.
- `1944096670`: same jungle mud class as `702683177`.
- `349049665`: official no target tower; Full Desert exposes dirt on a narrow
  upper-desert sand cap.
- `540278984`: true target pyramid inside underground desert; jungle uncertainty
  must not reject underground-desert candidates.
- `1092653535`: true target pyramid on broad Full Desert sand; narrow-sand
  surface gate must not reject it.

## Current Direction

The fastest improvements should come from narrowing hard gates with official
evidence, not from re-enabling broad official passes. Re-enabling whole passes
such as full Jungle, full Full Desert cleanup, or all cave bodies is likely to
push the pre-screen away from the sub-500 ms target unless it is localized to
candidate scan columns or replaces a broader conservative false-positive gate.
