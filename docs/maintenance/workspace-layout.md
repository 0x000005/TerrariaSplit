# Workspace layout and build outputs

## Source and retained data

- `src/` contains production projects only.
- `test/Suites/` contains the default behavior test host.
- `test/Diagnostics/` contains explicitly invoked diagnostics and metrics commands.
- `test/Probes/` contains standalone comparison tools that are not part of the default test build.
- `test/Baselines/` contains small, machine-independent retained baselines.
- `test/Results/` contains generated metrics and trace results and is ignored by Git.
- `test/Temp/` contains disposable runtime files created by tests and diagnostics.

Runtime `Settings/`, `Data/`, `Worlds/`, and `terrariasplit.log` files are user data. They must not be stored under `src/` or copied into a release directory.

## Central build output

The repository enables the .NET SDK artifacts layout in `Directory.Build.props`. Restore, build, test, and non-release publish intermediates for every project live below `.build/`:

```text
.build/
  bin/<project>/<pivot>/
  obj/<project>/<pivot>/
  publish/<project>/<pivot>/
```

Do not set `BaseOutputPath`, `BaseIntermediateOutputPath`, `OutputPath`, or `IntermediateOutputPath` in individual projects. Do not use `dotnet build -o`; project references can otherwise record incompatible paths in their intermediate state.

## Build levels

Restore only after a fresh checkout or package/project changes:

```powershell
$ErrorActionPreference = 'Stop'
dotnet restore TerrariaSplit.slnx -m:1
```

For normal development, build only the application project and reuse the incremental cache:

```powershell
$ErrorActionPreference = 'Stop'
dotnet build src/TerrariaSplit.WinForms/TerrariaSplit.WinForms.csproj --no-restore
```

Before a commit or release, use the deterministic single-node verification commands:

```powershell
$ErrorActionPreference = 'Stop'
dotnet build TerrariaSplit.slnx --no-restore -m:1 -p:UseSharedCompilation=false
dotnet run --project test/TerrariaSplit.Tests.csproj --no-build
```

## MemoryBridge

MemoryBridge is the single memory-control component. Its x86 control executable is published at the release root. Its managed Payload, native Bootstrap, compatibility manifest, and injected dependencies are published below `Runtime/MemoryBridge/`.

The Bootstrap target declares its source files as inputs and the DLL as its output, so an unchanged native component is skipped during incremental builds. Pass `-p:TerrariaSplitSkipMemoryBridge=true` only for focused work that does not require a runnable memory-control unit.

## Directory release

The product version has one source of truth: `TerrariaSplitProductVersion` in `Directory.Build.props`. Publishing stops at runnable directories and never creates ZIP files automatically:

```powershell
$ErrorActionPreference = 'Stop'
pwsh -NoProfile -File eng/Publish-Release.ps1
```

For version `1.9.4.0`, the final products are:

```text
publish/
  TerrariaSplit-v1.9.4.0-win-x64/
  TerrariaSplit.Race.Server-v1.9.4.0-win-x64/
  TerrariaSplit.Race.Server-v1.9.4.0-linux-x64/
```

Each publish replaces its own generated destination directory so stale files cannot survive between releases. Client finalization writes `Runtime/terrariasplit-update-manifest.json` and does not copy user data.

Historical `publish-*` directories and archives are not automatic cleanup targets because they may contain retained releases.

## Cleanup

Preview regenerable outputs:

```powershell
$ErrorActionPreference = 'Stop'
pwsh -NoProfile -File eng/Clean-Workspace.ps1 -IncludeIde
```

Delete `.build/`, test temporary files, IDE state when requested, and output directories left by the old per-project layout:

```powershell
$ErrorActionPreference = 'Stop'
pwsh -NoProfile -File eng/Clean-Workspace.ps1 -Execute -IncludeIde
```

The script validates every target and never removes the repository root, `.git`, `src`, `test`, final or historical publish directories, results, baselines, or runtime user data.
