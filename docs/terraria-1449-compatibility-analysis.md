# Terraria 1.4.4.9 Compatibility Analysis

Last updated: 2026-07-03.

## Summary

Terraria 1.4.4.9 support is now based on MemoryProbe `runtime-layout`, not a version-specific byte-signature profile. The old plan to add a `Terraria1456Memory.Profile` fork, `UpdateTime` signature, boss fallback signature, seed signature, or worldgen signature is obsolete.

The current implementation keeps UI scale patching as the only byte-pattern feature. For Terraria `1.4.4.9`, that patch now returns `Unsupported` before scanning or writing process memory.

Create-world automation now has a separate 1449 menu profile. It intentionally avoids runtime modal prompts and keeps keyboard use to clipboard paste operations; submit/menu actions are mouse clicks. 1449 character creation uses the `UICharacterCreation` panel, so Journey players and player template paste are supported by the profile; template paste first switches to the gender/clothing tab, where 1449 exposes the copy/paste/randomize template buttons. 1449 world creation uses the 1.4.4.9 panel layout: size, Journey/Classic/Expert/Master, evil, seed text, and create are clicked on the main world creation page. Special seed selections are converted to seed-field text before submission, except Skyblock, which does not exist in 1.4.4.9 and is treated as unsupported. Unsupported 1449 automation options, such as Skyblock and pyramid pre-screen, fail or downgrade through logs/diagnostics rather than popups.

The current local target observed during this work:

- `Path`: `D:\Games\Terraria_1.4.4.9\Terraria.exe`
- `PID`: `52532`
- `FileVersion`: `1.4.4.9`
- Probe command: `TerrariaSplit.MemoryProbe.exe runtime-layout 52532`
- Probe result: success, `79` resolved fields, `18` boss fact addresses, `5` biome zone bytes
- Layout groups resolved: core menu/status fields, boss facts, item, NPC, biome, seed UI, and a worldgen source

## Current Status Matrix

| Feature | 1449 status | Evidence | Remaining action |
| --- | --- | --- | --- |
| Process discovery | Supported by runtime layout | `TerrariaProcessFinder` now selects the newest `Terraria` process without a version profile. | Live watcher verification only. |
| Process attach / MemoryProbe | Supported by runtime layout | `runtime-layout` succeeded against the running 1.4.4.9 x86 process. | Keep retry/diagnostic behavior. |
| Timer menu/world state | Supported by runtime layout, needs live transition verification | `Main.gameMenu` static field address is resolved by MemoryProbe. | Verify menu-to-world and world-to-menu snapshots in the app. |
| Boss and hardmode facts | Supported by runtime layout, needs live fact verification | MemoryProbe resolved `18` boss/hardmode static fact addresses. | Verify against known boss/hardmode states. |
| Item splits | Supported by runtime layout, needs live item verification | Item/player/chest offsets resolved by MemoryProbe. | Verify one in-world item split snapshot. |
| NPC facts | Supported by runtime layout, needs live NPC verification | `Main.npc` and NPC fields resolved by MemoryProbe. | Verify one in-world NPC fact snapshot. |
| Biome facts | Supported by runtime layout, needs live biome verification | `zone1` through `zone5` byte offsets resolved by MemoryProbe. | Verify several common biome snapshots. |
| Seed UI diagnostics | Supported by runtime layout, needs page verification | `MenuUI`, `UserInterface` current state, and seed UI offsets resolved by MemoryProbe. | Verify on the advanced seed page. |
| Worldgen diagnostics | Partially supported by runtime layout | 1449 exposed a worldgen source and `statusText`; current-controller path may be absent. | Verify live worldgen progress/statusText behavior. |
| Route/group/timer domain logic | Out of scope / app internal | No Terraria process data dependency. | No 1449-specific change. |
| Overlay/hotkeys/settings/storage/statistics | Out of scope / app internal | App-internal behavior; current full test failures are unrelated to 1449 memory support. | Fix existing non-1449 test failures before release. |
| Menu geometry | Supported by versioned menu profile, needs live click verification | `TerrariaMenuProfile.Legacy1449` has 1449-specific character-template coordinates, reuses the modern world option rows where appropriate, and uses the main world creation seed field instead of the advanced seed-page text field. | Manually verify common client sizes before enabling automation broadly. |
| Create/load world automation | Partially supported for 1449, needs live click verification | 1449 create automation now uses the panel flow shown by the running game: switch to the character gender/clothing tab before player template paste, Journey/Classic/Mediumcore/Hardcore player difficulty, world size, Journey/Classic/Expert/Master world difficulty, evil selection, main-page seed field, and create. Special seed buttons are converted to text aliases such as `fortheworthy`, `dontdigup`, and `getfixedboi`; multiple seed texts are joined with `|`. Skyblock is unsupported because 1.4.4.9 has no Skyblock seed. | Verify only with approval and backed-up disposable saves. |
| Save cleaner | Needs live verification | File operation behavior is not Terraria-memory-specific. | Test only on copied save folders. |
| World file scanner | Needs 1449 world-file verification | Current reader gates metadata by world file version. | Scan copied 1449 `.wld` files for seed/evil/chest/pyramid data. |
| Pyramid post-generation filter | Needs 1449 world-file verification | Depends on world file scanner and item matcher. | Verify against copied or disposable 1449 worlds. |
| Pyramid pre-screen | Unsupported for 1449 | `OfficialPassPlan` is documented as a Terraria 1.4.5.6 pass plan. | Automation now avoids prediction on detected 1.4.4.9; add a 1449 pass plan later before enabling. |
| UI scale patch | Unsupported for 1449 | Existing 1.4.5.x byte patterns were not proven for 1449. | Current behavior returns unsupported and writes no bytes. |

## Obsolete 1449 Tasks

These tasks from the earlier compatibility report should not be implemented:

- Add explicit 1.4.4.9 memory profile selection.
- Add a 1449 `UpdateTime` signature or menu-state fallback signature.
- Add a 1449 boss progression fallback signature.
- Add a 1449 seed-reader byte signature.
- Add a 1449 worldgen byte signature.
- Reuse or fork `Terraria1456Memory.Profile`.

The replacement strategy is a single MemoryProbe/CLRMD runtime layout resolver. Static field addresses and field offsets are resolved during attach/retry, then the main app keeps high-frequency reads as direct `ReadProcessMemory` calls using the cached layout.

## Remaining Work

1. Live read-only watcher verification on Terraria 1.4.4.9:
   - Confirm process attach, layout status, and diagnostics in the app.
   - Confirm `gameMenu` changes between menu and world.
   - Confirm item, boss/hardmode, NPC, and biome facts with known states.
   - Confirm seed UI diagnostics on the advanced seed page.
   - Confirm worldgen status/progress fallback during a real world generation run.

2. Manual automation verification, only with approval:
   - Verify 1449 menu geometry at common client sizes.
   - Verify create-player/create-world/load-world flows with disposable saves.
   - Confirm the 1449 flow remains silent during failures; no runtime modal prompt should appear.
   - Confirm keyboard input is limited to text paste operations (`Ctrl+A` / `Ctrl+V`); submits and menu choices should use mouse clicks.
   - Verify save cleaner only on copied save folders.

3. World file and pyramid verification:
   - Scan copied 1449 `.wld` files for metadata, evil, seed, chest layout, and pyramid items.
   - Run pyramid post-generation filter against copied or disposable 1449 worlds.
   - Keep pyramid pre-screen disabled for detected 1449 until a 1449-specific source parity audit and metrics run exist.

4. Release-quality cleanup:
- Keep UI scale patch unsupported for 1449 unless dedicated 1449 byte patterns are added and tested.
- Keep 1449 Skyblock and pyramid pre-screen unsupported until dedicated 1449 support exists; other special seed buttons are submitted as main-page seed-field text.
- Resolve the existing non-1449 full-test failures before release.
- Update user-facing notes if unsupported 1449 features need clearer wording in the UI.

## Verification Commands

Build:

```powershell
dotnet build TerrariaSplit.slnx
```

Full regression:

```powershell
dotnet run --project test\TerrariaSplit.Tests.csproj
```

Focused pyramid pre-screen:

```powershell
$env:TERRARIA_SPLIT_TEST_FILTER='Pyramid seed pre-screen'
dotnet run --project test\TerrariaSplit.Tests.csproj
```

Read-only runtime layout probe:

```powershell
src\TerrariaSplit.MemoryProbe\bin\Debug\net10.0-windows\win-x86\TerrariaSplit.MemoryProbe.exe runtime-layout <pid>
```

## Current Test Baseline

After the MemoryProbe runtime-layout refactor, the memory-related tests for resolver, item, boss, NPC, biome, seed UI, and worldgen fallback pass.

The known remaining full-test failures are not 1449 memory/profile failures:

- `Default UI page settings match tuned overlay layout`
- `SettingsNormalizer clamps auto-create timings`
- `AppSettings falls back from invalid hotkeys`
- `Overlay composite layout derives status and timer windows from shared bounds`
- `TextEffectRenderer scales image effects to 100 percent`

These should be fixed as normal product quality work before release, but they do not require reintroducing Terraria version profiles or byte signatures.
