# Terraria 1.4.4.9 Compatibility Analysis

Date: 2026-07-03

Target runtime observed: `D:\Games\Terraria_1.4.4.9\Terraria.exe`, PID `52532`, product/file version `1.4.4.9`, file size `20422144` bytes.

Primary evidence sources:

- Terraria 1.4.4.9 source: `..\reference\Terraria1449`
- Current implementation: this repository
- Comparison source: `..\reference\Terraria1456`
- Running process: current local Terraria 1.4.4.9 process, read-only probes only

Status meanings:

- `Supported`: the feature is version-independent or has direct 1.4.4.9 evidence.
- `Needs 1449 Profile`: the concept is compatible, but the current 1.4.5.x memory signature/offset profile should not be reused.
- `Needs Manual Verification`: the code is likely compatible, but this report did not perform UI clicks, save mutation, or in-game scenario execution.
- `Unsupported / Unknown`: current implementation cannot be considered usable on 1.4.4.9 without a new strategy or more evidence.

## Executive Summary

Terraria 1.4.4.9 support should be implemented as an explicit 1449 compatibility profile, not by reusing the existing `Terraria1456Memory.Profile`.

The strongest positive result is that `publish\TerrariaSplit.MemoryProbe.exe item-layout 52532` successfully resolved the running 1.4.4.9 process. The returned layout includes local player inventory, equipment, banks, mouse item, NPC array, NPC fields, and biome zone bit field offsets. This supports the user's initial observation that item splits are likely fine, and it also gives good evidence that NPC and biome facts can work once the runtime can attach and resolve the process state.

The strongest negative result is that the current 1.4.5.x byte signatures do not match the 1.4.4.9 executable. A file-level scan of the 1.4.4.9 `Terraria.exe` found zero matches for the current `UpdateTime`, `gameMenu` fallback, boss progression fallback, worldgen controller, and worldgen progress signatures. The seed reader `MenuUI.SetState(null)` signature also found zero matches. The UI scale patch's three current byte patterns also found zero matches. These results make a 1449-specific profile mandatory for timer state, boss flags, world creation seed reading, world generation progress, and UI scale patching.

The highest-risk areas are:

- Memory profile: `gameMenu`, `UpdateTime`, boss flag base, hardmode flag, and fallback signatures.
- World generation progress: 1.4.4.9 does not expose the same `Terraria.WorldBuilding.WorldGenerator.CurrentGenerationProgress` / `CurrentController` source shape used by 1.4.5.6.
- World creation seed reader: current signature and object offsets are 1.4.5.x-oriented and did not match the 1.4.4.9 executable.
- UI scale patch: current patch signatures did not match 1.4.4.9.
- Pyramid pre-screen: current local simulation is documented as a 1.4.5.6 normal small-world path; 1.4.4.9 source has pyramid item IDs `857` and `934`, but the pass sequence and RNG parity still need a 1449 audit.

## Evidence Log

### Running 1.4.4.9 Process

`Get-Process -Id 52532` confirmed:

- `ProcessName`: `Terraria`
- `Path`: `D:\Games\Terraria_1.4.4.9\Terraria.exe`
- `ProductVersion`: `1.4.4.9`
- `FileVersion`: `1.4.4.9`
- `StartTime`: `2026/7/3 13:02:32`

### MemoryProbe Result

Read-only probe command:

```powershell
publish\TerrariaSplit.MemoryProbe.exe item-layout 52532
```

Result: `Success=true`.

Important resolved fields:

- Local player statics: `PlayerArrayStaticFieldAddress`, `MyPlayerStaticFieldAddress`, `MouseItemStaticFieldAddress`
- Item containers: armor, dye, misc equips, misc dyes, trash, inventory, bank1, bank2, bank3, bank4
- Item fields: `ItemTypeFieldOffset=160`, `ItemStackFieldOffset=180`
- NPC statics/fields: `NpcArrayStaticFieldAddress`, `NpcTypeFieldOffset=232`, `NpcActiveFieldOffset=32`, `NpcTownNpcFieldOffset=433`, `NpcHomelessFieldOffset=434`
- Biome zone bytes: `zone1=2268`, `zone2=2272`, `zone3=2276`, `zone4=2280`, `zone5=2284`
- Managed layout: `ObjectReferenceSize=4`, `ManagedArrayLengthOffset=4`, `ManagedArrayFirstElementOffset=8`

Conclusion: the CLR layout path used by `TerrariaClrMemoryResolver`, `ItemFactProvider`, `NpcFactProvider`, and `BiomeFactProvider` is viable against the running 1.4.4.9 x86 process.

### Signature Scan Result

File-level scan against `D:\Games\Terraria_1.4.4.9\Terraria.exe`:

```text
UpdateTime: count=0
GameMenuFallback: count=0
BossProgressionFallback: count=0
CurrentController: count=0
CurrentGenerationProgress: count=0
SeedReaderMenuUiSetNull: count=0
```

UI scale patch pattern scan:

```text
mouse slider display range: count=0
mouse slider assignment range: count=0
gamepad slider range: count=0
```

Conclusion: 1.4.4.9 needs its own memory profile/signatures. The current 1.4.5.x profile should be treated as non-compatible for static address resolution and byte patching.

### Source Facts

1.4.4.9 source facts:

- `Main.versionNumber = "v1.4.4.9"` and `versionNumber2 = "v1.4.4.9"` in `..\reference\Terraria1449\Terraria\Main.cs`.
- `Main.MenuUI`, `Main.InGameUI`, `Main.hardMode`, `Main.gameMenu`, `Main.npc`, `Main.mouseItem`, `Main.myPlayer`, and `Main.player` exist in `Main.cs`.
- `Main.PreDrawMenu` still uses the logical 900px menu-height scaling rule and `SettingDontScaleMainMenuUp`.
- `NPC.downedBoss1`, `downedBoss2`, `downedBoss3`, `downedPlantBoss`, `downedGolemBoss`, `downedAncientCultist`, `downedMoonlord`, `downedMechBoss1`, `downedMechBoss2`, and `downedMechBoss3` exist in `..\reference\Terraria1449\Terraria\NPC.cs`.
- `WorldGen.Pyramid` in `..\reference\Terraria1449\Terraria\WorldGen.cs` places pyramid main items `857` and `934`.
- 1.4.4.9 source has `WorldGen._generator` and `GenerationProgress` usage, but not the 1.4.5.6 `Terraria.WorldBuilding.WorldGenerator.CurrentGenerationProgress` / `CurrentController` source shape.
- 1.4.4.9 has special seed globals through `zenithWorld`; 1.4.5.6 additionally has `skyblockWorld`. The current scanner gates skyblock reading behind world file version `>= 302`, which should remain harmless for 1.4.4.9 files.

### Test Results

Full test command, run escalated:

```powershell
dotnet run --project test\TerrariaSplit.Tests.csproj
```

Result: failed with 5 test failures. The failures are not 1.4.4.9 memory/profile-specific:

- `Default UI page settings match tuned overlay layout`
- `SettingsNormalizer clamps auto-create timings`
- `AppSettings falls back from invalid hotkeys`
- `Overlay composite layout derives status and timer windows from shared bounds`
- `TextEffectRenderer scales image effects to 100 percent`

Focused pyramid command, run escalated:

```powershell
$env:TERRARIA_SPLIT_TEST_FILTER='Pyramid seed pre-screen'
dotnet run --project test\TerrariaSplit.Tests.csproj
```

Result: all 11 focused pyramid pre-screen tests passed.

Important caveat: the repository already had unrelated uncommitted changes before this report was written. The full-test failures should be investigated separately before using the current worktree as a clean compatibility baseline.

## Feature Compatibility Matrix

| Feature | Status | Evidence | Required action |
| --- | --- | --- | --- |
| Process discovery | Needs 1449 Profile | Process name/path/version are detected; current `TerrariaProcessFinder` defaults to `Terraria1456Memory.Profile`. | Add version-aware profile selection using file/product version or explicit user setting. |
| Process attach / CLR layout | Supported | MemoryProbe succeeded against PID `52532`; object references are x86-size. | Reuse CLRMD layout path; keep graceful failure behavior. |
| Timer menu/world state | Needs 1449 Profile | `Main.gameMenu` exists in 1449 source, but current `UpdateTime` and fallback signatures matched zero times. | Create 1449 `gameMenu` signature/offset route and fallback route. |
| Boss splits | Needs 1449 Profile | 1449 `NPC` boss flags exist and source order matches the current conceptual mapping; current boss progression signature matched zero times. | Resolve 1449 boss flag base/hardmode addresses and verify offsets for Skeletron, WoF/hardmode, mech trio, Plantera, Golem, Cultist, Moon Lord. |
| Item splits | Supported, after attach | MemoryProbe resolved all item containers and item type/stack offsets; current item reader is CLR-layout-based. | Validate one live in-world item snapshot after 1449 profile attach works. |
| NPC splits | Supported, after attach | MemoryProbe resolved `Main.npc` and town NPC fields. | Validate with one in-world NPC fact snapshot. |
| Biome splits | Supported, after attach | MemoryProbe resolved `zone1` through `zone5` byte offsets. | Validate with one in-world biome snapshot for several common biomes. |
| Route/group logic | Supported | `SplitCatalog`, `SplitRouteGroups`, split conditions, comparison, finalization, storage, and statistics are app-internal. | No Terraria-version change needed. |
| Default route item IDs | Supported | Item catalog contains `857`, `934`, `525`, and `1220`; 1449 pyramid source uses `857`/`934`. | No item ID migration needed for the current route. |
| Overlay rendering | Supported / unrelated test failures | Rendering is app-internal; full test run has one overlay layout failure and one text effect renderer failure in the dirty worktree. | Not a Terraria-version blocker; fix/regress-test separately before release. |
| Hotkeys / input | Supported / unrelated test failure | Hotkey system is app-internal; full test run has one invalid-hotkey fallback failure in the dirty worktree. | Not a 1449 blocker; fix/regress-test separately before release. |
| Settings/profile normalization | Supported / unrelated test failures | Settings are app-internal; full test run has UI default and auto-create timing normalization failures. | Not a 1449 blocker; fix/regress-test separately before release. |
| Storage/statistics | Supported | App storage and statistics use internal split IDs/route groups, not Terraria process data. | No 1449-specific change needed. |
| Menu geometry | Needs Manual Verification | 1449 `PreDrawMenu` still uses logical 900px scaling and `SettingDontScaleMainMenuUp`; no live clicking was performed. | Manually verify menu coordinates at common client sizes before enabling automation. |
| Create player/world automation | Needs Manual Verification | 1449 UI flow still has create-world/create-player source paths; no destructive UI automation was run. | Verify with backup/sandbox saves only after profile and menu geometry are confirmed. |
| Load world automation | Needs Manual Verification | World selection source flow exists; no live click/load test was run. | Verify with non-favorite disposable saves and backup strategy. |
| Save cleaner | Needs Manual Verification | Save family behavior including `.bak` appears consistent; cleaner is file-system side-effectful and was not run. | Test on copied saves before release. |
| World creation seed reader | Needs 1449 Profile | 1449 source has seed set/read flow, but current `MenuUI.SetState(null)` signature matched zero times. | Add 1449 seed reader signature and verify UI object offsets or move to CLR-derived field resolution. |
| Worldgen progress | Unsupported / Unknown | 1449 source lacks 1456 `CurrentGenerationProgress` / `CurrentController`; current worldgen signatures matched zero times. | Implement a 1449-specific worldgen progress strategy or gracefully mark progress unavailable. |
| Zenith star catch automation | Unsupported / Unknown | It consumes worldgen current pass names; those are currently unresolved for 1449. | Disable or degrade until 1449 worldgen progress is implemented. |
| UI scale patch | Unsupported / Unknown | All three current patch byte patterns matched zero times in 1449. | Add 1449 patch patterns or disable the patch for 1449 with a clear user-facing message. |
| World file scanner | Needs Manual Verification | Current scanner has version gates for metadata, special seeds, chest layout, and skyblock; no real 1449 `.wld` sample was scanned in this report. | Scan a known 1449 `.wld` sample for metadata and pyramid chest data. |
| Pyramid post-generation filter | Needs Manual Verification | It depends on world file scanner and item matcher; focused tests pass, but no 1449 world file was scanned. | Verify against generated/copied 1449 worlds before release. |
| Pyramid pre-screen | Needs 1449 Source Parity Audit | Focused tests pass, and 1449 pyramid items match, but `OfficialPassPlan` is explicitly a 1.4.5.6 pass plan. | Create/confirm a 1.4.4.9 pass plan and run metrics against 1449 generated worlds. |
| World pool/headless generation | Needs Manual Verification | Depends on scanner, pyramid filter, and generated world metadata. | Verify only after scanner/pre-screen status is resolved. |

## Required Code Changes For 1.4.4.9 Support

1. Add explicit 1.4.4.9 profile selection.
   - Do not reuse `Terraria1456Memory.Profile`.
   - Choose the profile from the running process file/product version, with a clear unknown-version failure state.
   - Preserve 1.4.5.x behavior unchanged.

2. Create a 1.4.4.9 memory profile.
   - Resolve `gameMenu` from a 1449 `UpdateTime` or equivalent stable anchor.
   - Resolve `NPC` boss flags and `Main.hardMode`.
   - Verify boss offsets with source plus live snapshots.
   - Keep fallback routes, but make them 1449-specific.

3. Keep CLR-derived item/NPC/biome layouts.
   - The MemoryProbe result is strong evidence this path works on 1449.
   - The profile work should not hardcode these CLR offsets.

4. Split seed reader logic by version or move it to CLR metadata.
   - The current seed-reader signature does not match 1449.
   - Verify `UserInterface.CurrentState`, `UIWorldCreation`, `UIWorldCreationAdvanced`, and `UICharacterNameButton.actualContents` offsets on the running 1449 process before enabling pre-screen automation.

5. Rework or disable worldgen progress for 1449.
   - 1449's source shape uses `WorldGen._generator` and passed `GenerationProgress` objects, not the 1456 static `WorldGenerator.CurrentGenerationProgress` / `CurrentController` path.
   - If a stable path cannot be found, expose worldgen status as unavailable and keep post-create `.wld` verification.

6. Gate UI scale patch by version.
   - Current byte patterns do not match 1449.
   - Either add 1449-specific patterns or return an unsupported result for 1449.

7. Audit pyramid pre-screen against 1449 source.
   - 1449 `WorldGen.Pyramid` main items match the current expected `857`/`934`.
   - The pass plan, earlier terrain/desert/crimson mutations, and RNG consumption still need 1449 parity checks before calling pre-screen supported.

## Verification Still Needed

Read-only or low-risk:

- Run a 1449 profile diagnostic that reports resolved `gameMenu`, boss flag base, `hardMode`, and CLR layouts in one snapshot.
- Capture menu and in-world facts without changing the world: item count, one NPC fact, several biome bits, and boss/hardmode booleans.
- Scan one copied 1449 `.wld` file with `TerrariaWorldFilePyramidScanner`.

Manual or side-effectful, requires explicit approval and backup:

- Click through create player/world automation.
- Run pyramid post-generation filter against real 1449 generated worlds.
- Run save cleaner on copied save folders.
- Apply UI scale patch to a disposable 1449 process instance.
- Validate Zenith star catch automation during a real worldgen run only after worldgen progress is implemented.

Automated:

- Re-run `dotnet run --project test\TerrariaSplit.Tests.csproj` after unrelated dirty-worktree failures are fixed.
- Re-run the focused pyramid suite after any 1449 pre-screen changes:

```powershell
$env:TERRARIA_SPLIT_TEST_FILTER='Pyramid seed pre-screen'
dotnet run --project test\TerrariaSplit.Tests.csproj
```

- If pyramid pre-screen behavior changes, run dataset metrics against 1449 world data:

```powershell
dotnet run -c Release --project test\TerrariaSplit.Tests.csproj -- pyramid-metrics <world-folder> --csv test\Results\Metrics\metrics-current-release.csv
```

## Task Breakdown

Must change code:

- Add version-aware memory profile selection.
- Add a 1.4.4.9 memory profile for menu state, boss flags, and hardmode.
- Add or disable 1.4.4.9 seed reader support.
- Add or disable 1.4.4.9 worldgen progress support.
- Gate UI scale patch with 1.4.4.9-specific support status.
- Audit and, if necessary, add a 1.4.4.9 pyramid pre-screen pass plan.

Needs verification only:

- Item split live snapshot.
- NPC split live snapshot.
- Biome split live snapshot.
- Menu geometry at actual client sizes.
- World file scanner on copied 1.4.4.9 `.wld` files.
- Save cleaner on copied save folders.
- Overlay/hotkey/settings behavior after unrelated current test failures are addressed.

No Terraria-version code change expected:

- Split route/group/domain logic.
- Default route item IDs `857`, `934`, `525`, and `1220`.
- Split timing comparison and run finalization.
- Storage/statistics schemas.
- Localization and internal settings serialization, aside from the unrelated test failures already noted.

## Release Readiness Criteria

1. The app selects a 1.4.4.9 profile when attached to `Terraria.exe` version `1.4.4.9`.
2. Timer state changes correctly when moving between menu and world.
3. Boss split facts are verified against known 1449 boss/hardmode states.
4. Item, NPC, and biome facts are verified with live snapshots from 1449.
5. Unsupported 1449 features fail loudly and safely instead of silently using 1.4.5.x offsets.
6. Pyramid pre-screen is either proven against 1449 source/data or disabled/marked unsupported for 1449.
7. Full tests pass on a clean worktree, and the pyramid focused suite passes after any worldgen changes.
