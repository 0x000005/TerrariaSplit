# TerrariaSplit Refactor Baseline

Date: 2026-06-21
Branch: `codex/refactor-deep-architecture`
Scope: R0 baseline for the deep refactor execution plan.

## Workspace State

The branch was created from `main` with existing uncommitted work in the tree. Those changes are treated as user work and are not reverted by this refactor.

Dirty files observed before R0 edits:

- `TerrariaSplit/Localization/ChineseStrings.cs`
- `TerrariaSplit/UI/MainForm.cs`
- `TerrariaSplit/UI/Settings/SettingsDialogService.cs`
- `TerrariaSplit/UI/Settings/SplitSettingsPage.cs`
- `TerrariaSplit/UI/SettingsDialogHost.cs`
- `TerrariaSplit/UI/SettingsForm.cs`
- `TerrariaSplit/UI/WindowLayerController.cs`
- `test/MainShellRefactorTests.cs`
- `test/Program.cs`
- `TerrariaSplit/UI/Settings/SettingsMessageDialog.cs`
- `TerrariaSplit_Deep_Refactor_Execution_Plan.md`

## Build And Test Baseline

Command:

```powershell
dotnet build TerrariaSplit.slnx -c Debug -p:UseSharedCompilation=false
```

Result: passed, 0 warnings, 0 errors.

Command:

```powershell
dotnet run --project test\TerrariaSplit.Tests.csproj
```

Result: failed in the current dirty workspace.

Observed failing tests:

- `Localizer returns English fallback and Chinese Crimson`
- `Default attached split display matches primary display without bold`
- `AppSettingsStore writes embedded defaults when settings file is invalid`
- `SplitTimeSetStore writes embedded WR when reference files are invalid`
- `AppSettingsStore preserves active external split set names`
- `Settings form saves split icon override`
- `Settings form saves localized split icon override`
- `Settings form warns and rejects invalid split route apply`
- `Main form initializes overlay layout with current split count`
- `Overlay composite layout derives status and timer windows from shared bounds`

The first R0 refactor work does not attempt to resolve these failures. Later phases should decide whether each failure is stale test expectation, existing dirty-work behavior, or a product regression.

## Largest Source Files

| File | Lines |
|---|---:|
| `TerrariaSplit/Domain/ItemCatalog.cs` | 6158 |
| `TerrariaSplit/UI/Settings/SplitSettingsPage.cs` | 2491 |
| `TerrariaSplit/UI/MainForm.cs` | 1772 |
| `TerrariaSplit/UI/Settings/DebugSettingsPage.cs` | 1506 |
| `TerrariaSplit/Terraria/WorldGeneration/Simulation/FullDesertPassReplica.cs` | 1353 |
| `TerrariaSplit/Configuration/EmbeddedDefaults.cs` | 1213 |
| `TerrariaSplit/Terraria/WorldGeneration/Simulation/CrimsonPassReplica.cs` | 1166 |
| `TerrariaSplit/UI/Settings/AnimationSettingsPage.cs` | 891 |
| `TerrariaSplit/UI/Settings/AutomationSettingsPage.cs` | 830 |
| `TerrariaSplit/UI/Rendering/SplitListRenderer.cs` | 800 |
| `TerrariaSplit/Terraria/WorldGeneration/Simulation/WorldGenState.cs` | 787 |
| `TerrariaSplit/Terraria/Memory/TerrariaClrMemoryResolver.cs` | 760 |
| `TerrariaSplit/Terraria/WorldGeneration/Simulation/PyramidsPassReplica.cs` | 735 |
| `TerrariaSplit/UI/Rendering/SplitRenderData.cs` | 714 |
| `TerrariaSplit/Application/TerrariaMonitorCoordinator.cs` | 704 |
| `TerrariaSplit/UI/Settings/ThemedDropDownList.cs` | 698 |
| `TerrariaSplit/Terraria/Automation/CreateWorldWorkflow.cs` | 690 |
| `TerrariaSplit/Configuration/AppSettings.cs` | 690 |
| `TerrariaSplit/UI/Rendering/SplitCompletionAnimationRenderer.cs` | 653 |
| `TerrariaSplit/Terraria/Memory/TerrariaMemoryResolver.cs` | 650 |

## Architecture Baseline

- Main project source files: 256
- Files still using root namespace `TerrariaSplit`: 219
- Files referencing `AppSettingsStore`: 10
- Files referencing `AppLogger`: 32
- Application references to `AppSettingsStore`: 6
- Application references to WinForms keywords: 0

Current static dependency debt to remove later:

- `Application/ApplicationController.cs` references `AppSettingsStore`.
- `Application/WorldPoolFillService.cs` references `AppSettingsStore`.
- `Application/AutomationRunner.cs` references `AppLogger`.
- `Application/TerrariaMonitorCoordinator.cs` references `AppLogger`.
- `Application/WorldPoolFillService.cs` references `AppLogger`.

## Immediate Refactor Risks

- `MainForm` remains the effective composition root and owns many shell side effects.
- `SplitSettingsPage` mixes UI layout, route editing, condition editing, target search, drag/drop, validation, and test access.
- `AppSettingsStore.Save` normalizes, saves external split sets, temporarily mutates collection properties, and writes JSON in one static path.
- `AppSettings` depends on `System.Windows.Forms.Keys`, blocking a pure configuration project split.
- `ApplicationEffect` uses `Kind` plus nullable payload properties, so invalid payload combinations are representable.
- `TerrariaMonitorCoordinator` combines watcher loop, runtime command sequencing, dispatch throttling, diagnostics, and UI scale patch scheduling.
