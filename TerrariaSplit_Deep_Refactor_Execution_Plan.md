# TerrariaSplit 深度重构执行计划（充足预算版）

> 决策原则：当存在“轻量方案”和“彻底方案”两种选择时，本计划默认采用更彻底、长期维护成本更低的方案。仍然不建议推倒重写，因为该项目涉及 WinForms、Win32、Terraria 进程、内存读取、存档和自动化流程，最安全的彻底方案是“分阶段绞杀式重构”：每一步保持可编译、可测试、可回滚。

---

## 1. 重构目标

本次重构的目标不是只把大函数拆小，而是建立长期可维护架构：

1. 从单项目/根命名空间为主，升级为多项目分层架构。
2. 从 static 全局依赖，升级为 composition root + 明确接口。
3. 从配置读取后到处兼容，升级为 migration + normalization + repository。
4. 从 `MainForm` 超级协调器，升级为 WinForms shell + application effects + shell ports。
5. 从 `SplitSettingsPage` 巨型页面，升级为 settings page host + draft model + 子 controller。
6. 从“靠人工审查分层”，升级为架构测试/脚本门禁。
7. 从日志散落和静默失败，升级为统一 `OperationResult` / diagnostics / user notification。
8. 从重复 helper 和隐性 copy-paste，升级为可复用基础设施。

---

## 2. 成功标准

### 2.1 架构成功标准

重构完成后应满足：

```text
TerrariaSplit.Domain
  - 不引用 WinForms、文件系统、进程 watcher、UI、Storage、Configuration repository。

TerrariaSplit.Application
  - 不引用 WinForms。
  - 不直接调用 AppSettingsStore。
  - 不直接调用 AppLogger。
  - 输入是 command / notification，输出是 effect / state snapshot。

TerrariaSplit.Configuration
  - 负责 settings DTO、默认值、migration、normalization、repository。
  - 不决定 run state。
  - 不执行 UI 副作用。

TerrariaSplit.Storage
  - 负责用户数据持久化。
  - 不依赖 UI、Application shell、Terraria watcher。

TerrariaSplit.Terraria
  - 负责进程、内存、窗口、存档和自动化适配。
  - 不依赖 MainForm、Overlay 或 Settings page。

TerrariaSplit.Infrastructure
  - 只放通用 IO、日志、调度、诊断、Windows native 封装。

TerrariaSplit.WinForms
  - composition root。
  - WinForms 窗口生命周期。
  - effect executor。
  - shell ports 实现。
```

### 2.2 代码体量成功标准

建议设定硬指标，便于判断是否真的完成：

| 对象 | 当前问题 | 目标 |
|---|---|---|
| `UI/MainForm.cs` | 约 1772 行，承担 composition、overlay、settings、automation、runtime、modal | 降到 500-700 行以内，只保留窗口生命周期和事件入口 |
| `UI/Settings/SplitSettingsPage.cs` | 约 2491 行，承担 route、target、condition、drag/drop、validation | 降到 500-800 行以内，核心逻辑进入 draft/controller/validator |
| `Configuration/SettingsNormalizer.cs` | migration、兜底、当前 schema normalize 混杂 | 拆成 migrator + section normalizer，单文件不超过 400 行 |
| `Application/TerrariaMonitorCoordinator.cs` | watcher loop、dispatch、runtime command、timer period、patch 调度混杂 | 拆 facade + watcher loop + dispatcher + command sequencer |
| static 全局依赖 | `AppSettingsStore`、`RuntimeDataPaths`、`AppLogger` 到处被调用 | 只允许 composition root / adapter 使用 static facade |
| 根命名空间 | 大量 `namespace TerrariaSplit;` | 只保留极少兼容入口，主体全部进入子命名空间 |

### 2.3 行为成功标准

必须保持：

- 旧 settings JSON 可读。
- active profile 可读可写。
- reference splits / PB times / PB segments 保存位置和语义不破坏。
- overlay、timer overlay、hotkey、statistics、settings dialog 行为不退化。
- Terraria 未运行时不崩溃。
- Terraria 运行中 watcher、memory、window probe、UI scale patch 保护逻辑不退化。
- 自动创建世界和练习世界的取消路径、失败路径不退化。

---

## 3. 全局执行规则

### 3.1 分支策略

建立长期集成分支：

```powershell
git checkout main
git checkout -b refactor/deep-architecture
```

每个任务单独分支：

```powershell
git checkout -b refactor/R2-03-split-domain-project
```

每个 PR 只允许一种类型的变化：

- 只移动文件。
- 只改 namespace / using。
- 只抽 helper。
- 只引入接口和 adapter。
- 只替换调用方。
- 只拆 UI 子组件。
- 只做 migration。

不要把“文件移动 + 逻辑改写 + UI 行为调整 + 测试重写”塞进同一个 PR。

### 3.2 本地门禁命令

每个任务完成后执行：

```powershell
dotnet restore TerrariaSplit.slnx
dotnet build TerrariaSplit.slnx -c Debug
dotnet run --project test\TerrariaSplit.Tests.csproj
```

涉及发布配置时执行：

```powershell
dotnet publish TerrariaSplit\TerrariaSplit.csproj -c Release
```

涉及金字塔预筛、世界生成、自动化时执行：

```powershell
$env:TERRARIA_SPLIT_TEST_FILTER='Pyramid seed pre-screen'
dotnet run --project test\TerrariaSplit.Tests.csproj
Remove-Item Env:\TERRARIA_SPLIT_TEST_FILTER
```

### 3.3 人工 smoke 门禁

涉及 UI、配置、Storage、Terraria 集成时，至少手动验证：

- 程序启动。
- 主 overlay 显示。
- timer overlay 显示、拖动、右键菜单正常。
- 设置窗口打开、切页、保存、关闭、重新打开。
- active settings profile 正常切换和记忆。
- pause / resume / reset 正常。
- PB 更新确认弹窗正常。
- statistics 窗口正常。
- Terraria 未运行时 watcher 显示等待，不刷异常。
- Terraria 运行时 watcher attach、窗口探测、菜单状态诊断正常。
- 自动创建世界可以启动和取消。
- 练习世界选择可以打开、取消、进入流程。

---

## 4. 阶段 R0：基线、安全网和架构门禁

### R0.1 建立重构基线文档

新增：

```text
docs/refactor/baseline.md
docs/refactor/manual-smoke-checklist.md
docs/refactor/architecture-rules.md
```

`baseline.md` 记录：

- 当前最大文件列表。
- 当前 static 全局依赖列表。
- 当前 root namespace 文件数量。
- 当前测试结果。
- 当前 warning 数量。
- 当前关键用户路径 smoke 结果。

验收：

- 只新增文档。
- 不改业务代码。
- 测试通过。

### R0.2 新增一键检查脚本

新增：

```text
scripts/check.ps1
scripts/check-architecture.ps1
```

`check.ps1`：

```powershell
$ErrorActionPreference = 'Stop'
dotnet restore TerrariaSplit.slnx
dotnet build TerrariaSplit.slnx -c Debug
dotnet run --project test\TerrariaSplit.Tests.csproj
```

`check-architecture.ps1` 初期先报警，不失败：

```powershell
Write-Host 'Application -> AppSettingsStore references:'
Select-String -Path TerrariaSplit\Application\*.cs -Pattern 'AppSettingsStore'

Write-Host 'Application -> WinForms references:'
Select-String -Path TerrariaSplit\Application\*.cs -Pattern 'System.Windows.Forms|\bForm\b|\bControl\b'

Write-Host 'Root namespace files:'
Get-ChildItem TerrariaSplit -Recurse -Filter *.cs |
  Select-String -Pattern '^namespace TerrariaSplit;$' |
  Select-Object Path
```

验收：

- `scripts/check.ps1` 可一键跑完。
- `scripts/check-architecture.ps1` 能列出当前违规点。

### R0.3 新增架构测试项目或测试分组

如果现有测试项目方便扩展，新增：

```text
test/ArchitectureDependencyTests.cs
```

第一批测试只做 warning 记录或显式 allowlist。后续逐步收紧。

最终应测试：

- Domain 不引用 UI / WinForms / Storage / Terraria。
- Application 不引用 WinForms。
- Terraria 不引用 UI shell。
- UI.Settings 不调用 automation 启动逻辑。
- Application 不直接调用 `AppSettingsStore`。
- Application 不直接调用 `AppLogger`。

---

## 5. 阶段 R1：多项目拆分准备

> 充足预算下，不建议只改 namespace。最终应拆多项目。但直接拆会被现有循环依赖卡住，所以先做“命名空间和文件归位”。

### R1.1 引入目标命名空间

按顺序改：

```text
TerrariaSplit.Domain
TerrariaSplit.Models
TerrariaSplit.Configuration
TerrariaSplit.Storage
TerrariaSplit.Infrastructure
TerrariaSplit.Infrastructure.Windows
TerrariaSplit.Application
TerrariaSplit.Terraria
TerrariaSplit.Terraria.Memory
TerrariaSplit.Terraria.Automation
TerrariaSplit.Terraria.WorldGeneration
TerrariaSplit.UI
TerrariaSplit.UI.Settings
TerrariaSplit.UI.Rendering
```

规则：

- 一个目录一个 PR。
- 只改 namespace / using。
- 不改逻辑。

验收：

- 每个目录修改后测试通过。
- root namespace 数量下降。

### R1.2 根目录类归位

移动建议：

```text
TerrariaWorldWatcher.cs              -> Terraria/TerrariaWorldWatcher.cs
Terraria1456Memory.cs                -> Terraria/Memory/Terraria1456Memory.cs
TerrariaSavePaths.cs                 -> Terraria/TerrariaSavePaths.cs
TerrariaSaveFileCleaner.cs           -> Terraria/TerrariaSaveFileCleaner.cs
TerrariaWindowController.cs          -> Terraria/TerrariaWindowController.cs
TerrariaWindowProbe.cs               -> Terraria/TerrariaWindowProbe.cs
TerrariaWatcherDiagnostics.cs        -> Terraria/TerrariaWatcherDiagnostics.cs
TerrariaWatchSnapshot.cs             -> Terraria/TerrariaWatchSnapshot.cs
TerrariaWorldGenerationState.cs      -> Terraria/TerrariaWorldGenerationState.cs
NativeMethods.cs                     -> Infrastructure/Windows/NativeMethods.cs
RuntimeDebugSnapshot.cs              -> Application/Diagnostics/RuntimeDebugSnapshot.cs
HotkeyKeyValidator.cs                -> UI/Input/HotkeyKeyValidator.cs 或 Application/Input/HotkeyKeyValidator.cs
ColorText.cs                         -> Domain/Text 或 UI/Rendering，按引用决定
TimeText.cs                          -> Domain/Formatting
```

验收：

- 文件位置表达职责。
- 不改变行为。
- 测试通过。

### R1.3 标记 generated/data 文件

改动：

```text
Domain/ItemCatalog.cs -> Domain/ItemCatalog.Generated.cs
Configuration/EmbeddedDefaults.cs -> 暂不移动，只标记为后续资源化目标
```

验收：

- 不改数据内容。
- 不改调用方语义。

---

## 6. 阶段 R2：低风险重复代码清理

本阶段只做确定性抽取，不做架构大改。

### R2.1 删除重复 `HighResolutionTimerPeriod`

涉及：

```text
Application/TerrariaMonitorCoordinator.cs
Infrastructure/HighResolutionTimerPeriod.cs
```

操作：

1. 删除 `TerrariaMonitorCoordinator` 内部重复类。
2. 使用 `Infrastructure.HighResolutionTimerPeriod`。
3. 保持 begin/end/dispose 语义不变。

验收：

- scheduler / watcher 测试通过。
- watcher 启停 smoke 正常。

### R2.2 抽 `SettingsTokenParser`

涉及：

```text
Configuration/AppSettings.cs
Configuration/SettingsTokenParser.cs
```

操作：

1. 抽 alnum token normalize。
2. 抽 seed/item 列表分割。
3. 替换 `AutoCreateSpecialWorldSeed` 和 `AutoCreatePyramidFilterItem` 的重复逻辑。

验收：

- 旧输入得到一致输出。
- 自动创建 / 金字塔筛选设置测试通过。

### R2.3 抽 `SplitTargetTokenFormatter`

涉及：

```text
Domain/SplitConditionText.cs
UI/Settings/SplitSettingsPage.cs
Domain/SplitTargetTokenFormatter.cs
```

操作：

1. 把 item / npc / biome / boss token 格式化集中到 Domain。
2. UI 和 Domain 共用。
3. 增加 formatter 单元测试。

验收：

- UI 搜索结果文本不变。
- condition 文本不变。

### R2.4 抽 `TextEffectGeometry`

涉及：

```text
UI/Rendering/TextEffectRenderer.cs
UI/Settings/AnimationSettingsPage.cs
```

操作：

1. 抽 `CreateColorPositions`。
2. 抽 `InflateBounds`。
3. 渲染和设置预览共用。

验收：

- 动画设置预览和实际 overlay 效果一致。

### R2.5 抽 `LayeredWindowNative`

涉及：

```text
Infrastructure/LayeredWindowRenderTarget.cs
Infrastructure/LayeredWindowUpdater.cs
Infrastructure/Windows/LayeredWindowNative.cs
```

操作：

1. 移动重复 native structs。
2. 保持 `StructLayout`、字段顺序、字段类型完全不变。
3. 两个调用方复用。

验收：

- overlay 透明和 layered rendering 正常。

### R2.6 抽 `FileAccessProbe`

涉及：

```text
Infrastructure/FileAccessProbe.cs
Terraria/Automation/HeadlessWorldGenerator.cs
Terraria/Automation/PyramidFilterAutomation.cs
```

验收：

- `.wld` 写入/读取等待行为不变。
- 金字塔筛选链路通过。

### R2.7 抽 `WorldGenBounds`

涉及：

```text
Terraria/WorldGeneration/Simulation/*
```

操作：

- 只替换完全等价的 `InWorld` / `IsInWorld`。
- 贴近 Terraria 源码且语义不完全相同的方法暂时保留。

验收：

- 世界生成模拟测试和预筛测试通过。

### R2.8 删除死代码

候选：

```text
Domain/SplitTracker.cs: FindPreviousMainIndex
UI/MainForm.cs: FormatTimerPhase / FormatWorldState / FormatBossSummary / FormatFlag
UI/MainForm.cs: ResetRunWithSound
Terraria/WorldGeneration/Simulation/CrimsonPassReplica.cs: IsDungeonTile
```

操作：

1. IDE Find All References。
2. grep 全仓库确认无反射/字符串引用。
3. 一个 dead-code 区域一个 commit。

验收：

- 测试通过。

---

## 7. 阶段 R3：多项目拆分

> 这是充足预算下应采用的彻底方案。不要长期停留在单项目 + namespace 的中间态。

### R3.1 新建项目骨架

建议 solution 目标：

```text
src/TerrariaSplit.Domain/TerrariaSplit.Domain.csproj
src/TerrariaSplit.Configuration/TerrariaSplit.Configuration.csproj
src/TerrariaSplit.Storage/TerrariaSplit.Storage.csproj
src/TerrariaSplit.Infrastructure/TerrariaSplit.Infrastructure.csproj
src/TerrariaSplit.Infrastructure.Windows/TerrariaSplit.Infrastructure.Windows.csproj
src/TerrariaSplit.Application/TerrariaSplit.Application.csproj
src/TerrariaSplit.Terraria/TerrariaSplit.Terraria.csproj
src/TerrariaSplit.WinForms/TerrariaSplit.WinForms.csproj
TerrariaSplit.MemoryProbe/TerrariaSplit.MemoryProbe.csproj
test/TerrariaSplit.Tests.csproj
```

推荐目标框架：

```text
Domain                       net10.0
Configuration                net10.0 或 net10.0-windows，取决于 hotkey 依赖是否已移除
Storage                      net10.0
Infrastructure               net10.0
Infrastructure.Windows       net10.0-windows
Application                  net10.0
Terraria                     net10.0-windows
WinForms                     net10.0-windows
```

注意：`AppSettings` 当前使用 `System.Windows.Forms.Keys` 解析热键。彻底方案应把 hotkey 解析从 configuration model 中移出，避免 Configuration 被迫依赖 WinForms。

### R3.2 Domain 项目先拆

移动：

```text
Domain/*
Models/*
TimeText.cs
可能的纯文本/格式化 helper
```

验收：

- Domain 项目不能引用 WinForms。
- Domain 测试直接引用 Domain。
- 主项目通过 ProjectReference 使用 Domain。

### R3.3 Infrastructure 项目拆分

移动通用设施：

```text
Infrastructure/JsonFileStore.cs
Infrastructure/AppLogger abstractions
Infrastructure/HighPrecisionScheduler.cs
Infrastructure/HighResolutionTimerPeriod.cs
Infrastructure/HighResolutionWaitableTimer.cs
Infrastructure/RuntimePerformanceDiagnostics.cs
```

移动 Windows 专用设施到 `Infrastructure.Windows`：

```text
NativeMethods.cs
LayeredWindowRenderTarget.cs
LayeredWindowUpdater.cs
LayeredWindowNative.cs
```

验收：

- 通用 Infrastructure 不引用 WinForms。
- Windows native API 都在 Windows 项目。

### R3.4 Configuration 项目拆分

移动：

```text
Configuration/*
Localization/* 如果它主要是配置驱动的文本资源，可单独评估
Localizer.cs 可后续迁移到 UI 或 Localization 项目
```

前置改造：

- 移除 `AppSettings` 对 `Keys` 的直接依赖，改为 `HotkeyBinding` 或 string-only model。
- 热键解析放到 UI/Input 或 Application/Input。

验收：

- Configuration 不依赖 WinForms。
- settings 测试直接引用 Configuration。

### R3.5 Storage 项目拆分

移动：

```text
Storage/*
```

要求：

- 路径通过 `IRuntimeDataPaths` 注入。
- 文件 IO 使用 Infrastructure helper。
- 不引用 UI / Application。

### R3.6 Application 项目拆分

移动：

```text
Application/*
```

要求：

- 不引用 WinForms。
- 不直接调用 static `AppSettingsStore`。
- 不直接调用 static `AppLogger`。
- effect 中不要执行副作用，只表达意图。

### R3.7 Terraria 项目拆分

移动：

```text
Terraria/*
Terraria/Memory/*
Terraria/Automation/*
Terraria/WorldGeneration/*
```

要求：

- 不引用 WinForms UI shell。
- Windows API 通过 `Infrastructure.Windows`。
- 对 Application 暴露 watcher/automation contracts。

### R3.8 WinForms 项目收口

原 `TerrariaSplit.csproj` 演进为：

```text
TerrariaSplit.WinForms.csproj
```

它负责：

- `Program.cs`
- `MainForm`
- UI forms/pages/rendering
- composition root
- shell effect executor
- global hotkey adapter
- modal/window controllers

验收：

- solution 通过多项目 build。
- architecture tests 改为强制失败门禁。

---

## 8. 阶段 R4：配置系统彻底重构

### R4.1 建立 repository/migration/normalization 三段式

新增：

```text
Configuration/ISettingsRepository.cs
Configuration/AppSettingsRepository.cs
Configuration/SettingsDocument.cs
Configuration/SettingsSchemaVersion.cs
Configuration/SettingsMigrator.cs
Configuration/SettingsNormalizer.cs
Configuration/SettingsPersistenceProjection.cs
```

目标流程：

```text
Load:
  read JSON
  -> migrate to current schema
  -> normalize current schema
  -> load external split sets / PB sets
  -> return immutable-enough AppSettings snapshot

Save:
  clone/settings snapshot
  -> normalize
  -> save external split sets / PB sets
  -> project to persistence DTO
  -> write JSON atomically
```

验收：

- 旧 settings JSON 可读。
- 新 settings JSON 带 schema version 或能被 migrator 判定。
- 保存不再临时修改调用者的 `AppSettings` 对象。

### R4.2 删除 `AppSettingsStore.Save` 中的对象临时污染

当前保存逻辑会临时清空：

```text
ReferenceSplitSets
PersonalBestTimeSets
PersonalBestSegmentSets
```

彻底方案：

- 不再修改原对象。
- 用 `AppSettingsPersistenceProjection` 创建专门的落盘 DTO。
- 外部 split sets / PB sets 由 repository 协调保存。

验收测试：

- 保存前后原对象深度等价。
- JSON 输出不包含外部集合。
- PB 和 reference sets 仍被正确保存。

### R4.3 把兼容逻辑迁入 migrator

迁移候选：

- `LanguageNames.LegacyChinese` 旧乱码中文值。
- `SplitRouteEntry.ExpandDetails` 到 `AppSettings.ExpandSplitDetails`。
- 旧 route 缺失 id / display name / icon target。
- 旧 PB set 字段缺失。
- 旧 column / animation / advanced 设置缺失。

规则：

- migrator 负责“旧 schema 到新 schema”。
- normalizer 只负责“当前 schema 的合法范围和默认兜底”。
- runtime 不再到处判断 legacy 值。

验收：

- 每个旧配置样本都有 golden test。
- migration 后再次保存，不应继续输出旧字段。

### R4.4 拆 `AppSettings` 为 section

目标结构：

```csharp
internal sealed class AppSettings
{
    public GeneralSettings General { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
    public RouteSettings Route { get; set; } = new();
    public ComparisonSettings Comparison { get; set; } = new();
    public OverlaySettings Overlay { get; set; } = new();
    public AutomationSettings Automation { get; set; } = new();
    public PracticeWorldSettings PracticeWorlds { get; set; } = new();
    public AdvancedSettings Advanced { get; set; } = new();
}
```

迁移策略：

1. 先新增 section，但保留旧属性作为 `[JsonIgnore]` compatibility facade 或 internal facade。
2. migrator 支持旧扁平 JSON。
3. UI 逐页迁移到 section。
4. 测试覆盖旧 JSON 到新 section 的迁移。
5. 删除旧 facade。

验收：

- 设置页按 section 编辑。
- `SettingsNormalizer` 变成多个 section normalizer。
- 新设置项必须落在具体 section。

---

## 9. 阶段 R5：Application 层 effect/contract 重构

### R5.1 删除 Application 对 `AppSettingsStore` 的直接依赖

涉及：

```text
Application/ApplicationController.cs
Application/WorldPoolFillService.cs
```

彻底方案：

- Application 不负责如何 clone / save settings。
- Application 只产生 `ApplicationEffect.SaveSettings(settingsSnapshot)`。
- settings snapshot 的创建由注入的 `ISettingsSnapshotFactory` 或不可变 settings model 保证。

过渡方案只作为中间步骤：

```csharp
internal interface ISettingsSnapshotFactory
{
    AppSettings Clone(AppSettings settings);
}
```

最终目标：

- Application 不知道 `AppSettingsStore`。
- Application tests 可用 fake snapshot factory。

### R5.2 重构 `ApplicationEffect`

当前 effect 承载多种 nullable 字段。彻底方案：改为强类型 effect 派生或 discriminated records。

建议：

```csharp
internal abstract record ApplicationEffect;
internal sealed record SubmitRuntimeCommandEffect(RuntimeCommand Command) : ApplicationEffect;
internal sealed record PlaySoundEffect(string Path) : ApplicationEffect;
internal sealed record SaveSettingsEffect(AppSettings Settings) : ApplicationEffect;
internal sealed record ApplySettingsToShellEffect(AppSettings PreviousSettings, int SplitCount) : ApplicationEffect;
```

验收：

- 消除 `Kind + nullable payload` 组合错误。
- effect executor switch 更安全。
- 测试断言更清晰。

### R5.3 拆 `TerrariaMonitorCoordinator`

目标结构：

```text
TerrariaMonitorCoordinator        // public facade
WatcherLoop                       // poll loop / interval / start stop
WatcherCompletionDispatcher       // completion queue / UI dispatch / throttling
RuntimeCommandSequencer           // command sequence / stale command filtering
UiScalePatchScheduler             // patch retry / in-flight / applied process id
WatcherDiagnosticsAggregator      // diagnostics snapshot
```

执行顺序：

1. 先删除内部重复 timer period。
2. 抽 RuntimeCommandSequencer。
3. 抽 WatcherCompletionDispatcher。
4. 抽 UiScalePatchScheduler。
5. 最后瘦身 facade。

验收：

- watcher notification 顺序测试通过。
- stale command 被过滤。
- UI dispatch 合并/限流行为不变。
- UI scale patch retry 行为不变。

---

## 10. 阶段 R6：WinForms shell 重构

### R6.1 新增 composition root

新增：

```text
WinForms/MainShellCompositionRoot.cs
WinForms/MainShellServices.cs
```

职责：

- new repository。
- new logger。
- new runtime paths。
- new application controller。
- new watcher。
- new automation。
- new shell services。

`MainForm` 构造函数不再直接 new 大量依赖。

验收：

- `MainForm` 字段数量下降。
- 测试可以创建 fake shell services。

### R6.2 把 17 个 delegate 收束为 shell ports

当前 `ApplicationShellEffectExecutor` 构造参数过多。彻底方案：拆 ports。

建议接口：

```csharp
internal interface IRuntimeCommandPort
{
    void Submit(RuntimeCommand command);
}

internal interface ISoundPort
{
    void StopAll();
    void Play(string path);
}

internal interface IOverlayPort
{
    void ToggleMouseClickThrough();
    void ClearOverlayAnimation();
    void ClearSplitCompletionAnimation();
    void TrackSegmentBestDeltaHighlight(int splitIndex);
    void StartSplitCompletionAnimation(int splitIndex);
    void RefreshTimerOverlaySettings();
    void RefreshRuntimeUi();
}

internal interface ISettingsPort
{
    OperationResult Save(AppSettings settings);
    void ApplyToShell(AppSettings previousSettings, int splitCount);
}

internal interface IAutomationPort
{
    void StartCreateWorld();
    void ShowPracticeWorldSelector();
    void CancelCreateWorld();
    void CancelEnterWorld();
}
```

`ApplicationShellEffectExecutor` 接收这些 port，而不是一长串 delegate。

验收：

- effect executor 构造参数降到 4-6 个对象。
- shell 副作用更容易测试。

### R6.3 拆 `MainForm`

目标拆分：

```text
MainForm                           // 生命周期、WinForms event 入口
MainForm.Rendering.cs              // 保留纯绘制入口，继续向 rendering state 迁移
OverlayShell                       // overlay bounds、timer overlay、click-through、topmost
SettingsShell                      // settings dialog、profile 切换、保存失败提示
AutomationShell                    // create world / practice world / cancel
RuntimeShell                       // watcher notification、runtime command、debug snapshot
ModalShell                         // modal registration、activation、input blocking
HotkeyShell                        // hotkey registration、warning、mapping
PerformanceDiagnosticsShell        // performance snapshots、debug page support
```

执行顺序：

1. 抽 `SettingsShell`，因为 settings 保存和 profile 切换边界清晰。
2. 抽 `AutomationShell`，因为自动化入口集中。
3. 抽 `OverlayShell`，因为 overlay 状态字段多但边界相对明确。
4. 抽 `RuntimeShell`，因为它和 Application/TerrariaMonitorCoordinator 耦合，需要等 R5 稳定。
5. 抽 `HotkeyShell`。
6. 抽 diagnostics。

验收：

- `MainForm.cs` 降到目标范围。
- MainForm 只调用 shell service，不直接处理业务细节。
- UI smoke 全部通过。

---

## 11. 阶段 R7：Settings UI 重构

### R7.1 拆 `SplitSettingsPage`

目标结构：

```text
SplitSettingsPage                  // 页面组装和生命周期
SplitRouteDraft                    // draft 数据、dirty、selected index、clone/apply
SplitRouteListController           // list selection、draw、drag/drop
SplitConditionEditorController     // condition list、advanced editor、match mode
SplitTargetSearchController        // target kind、search、result list、format
SplitRouteValidator                // route validation、attached rules、error messages
SplitSettingsTestAccess            // 测试访问入口，替代页面暴露大量控件属性
```

执行顺序：

1. 抽 draft model，不改 UI。
2. 抽 validator。
3. 抽 target search。
4. 抽 condition editor。
5. 抽 route list drag/drop。
6. 清理 ForTests 控件暴露。

验收：

- `SplitSettingsPage` 降到目标范围。
- route 编辑、target 搜索、advanced condition、drag/drop 行为不变。
- 设置保存/取消语义不变。

### R7.2 拆其他大设置页

候选：

```text
UI/Settings/DebugSettingsPage.cs
UI/Settings/AnimationSettingsPage.cs
UI/Settings/AutomationSettingsPage.cs
```

原则：

- Debug page heavy snapshot 逻辑进入 diagnostics service。
- Animation preview 逻辑复用 rendering helper。
- Automation page 只编辑配置，不启动流程。

验收：

- Settings UI AGENTS 规则可由测试/脚本检查。

---

## 12. 阶段 R8：错误处理、日志和诊断统一

### R8.1 引入 `OperationResult`

新增：

```text
Infrastructure/OperationResult.cs
```

建议结构：

```csharp
internal readonly record struct OperationResult(
    bool Succeeded,
    string? UserMessage = null,
    Exception? Exception = null)
{
    public static OperationResult Success() => new(true);
    public static OperationResult Failure(string message, Exception? exception = null) => new(false, message, exception);
}
```

优先改造：

- settings save。
- world pool bank / install。
- split time set save。
- automation start。
- open deleted backup folder。
- sound play failure。

验收：

- 用户数据保存失败必须能到达 UI 提示。
- 非预期异常进入 log + diagnostics。
- 预期失败不靠 exception 控制流程。

### R8.2 引入 `IAppLogger`

新增：

```text
Infrastructure/IAppLogger.cs
Infrastructure/FileAppLogger.cs
Infrastructure/NullAppLogger.cs
```

规则：

- static `AppLogger` 只作为 legacy adapter。
- 新服务构造函数接收 `IAppLogger`。
- Application 不依赖 logger static。
- 日志是否启用在 composition root 决定，不在每次 call 时读环境变量。

验收：

- 测试可以注入 fake logger。
- 日志路径可由 `IRuntimeDataPaths` 提供。

### R8.3 统一 diagnostics snapshot

目标：

```text
RuntimeDiagnosticsSnapshot
WatcherDiagnosticsSnapshot
AutomationDiagnosticsSnapshot
StorageDiagnosticsSnapshot
PerformanceDiagnosticsSnapshot
```

Debug page 只展示 snapshot，不直接深入各服务读取内部状态。

验收：

- Debug page 不直接触碰大量服务内部细节。
- diagnostics 数据是只读快照。

---

## 13. 阶段 R9：Terraria 集成和自动化清理

### R9.1 自动化流程显式步骤化

重点对象：

```text
Terraria/Automation/CreateWorldWorkflow.cs
Terraria/Automation/HeadlessWorldGenerator.cs
Terraria/Automation/PyramidFilterAutomation.cs
Terraria/Automation/ZenithStarCatchAutomation.cs
```

目标：

- 每个 workflow step 有名称。
- 每个 step 支持 cancellation。
- 每个 step 失败返回 `OperationResult` 或 automation result。
- 日志记录 step start / success / failure。
- UI 只启动/取消，不知道内部步骤。

验收：

- 自动创建世界取消路径测试通过。
- 金字塔预筛失败不影响最终 `.wld` 二验兜底。

### R9.2 Memory / Window 保护逻辑保留并集中

不要删除：

- 进程不存在保护。
- 读写内存失败保护。
- UI scale patch rollback 保护。
- window handle 无效保护。

可以做：

- 把重复 Win32 error 转 diagnostics。
- 把 memory read 失败统一成 typed result。
- 把 watcher diagnostic message 结构化。

验收：

- Terraria 未运行和运行中两种 smoke 均通过。

---

## 14. 阶段 R10：资源化、清理和最终收口

### R10.1 `EmbeddedDefaults.cs` 资源化

彻底方案：

```text
Assets/Defaults/settings.default.json
Assets/Defaults/reference-splits.default.json
```

Configuration 通过 embedded resource 加载默认配置。

验收：

- 默认设置 JSON 可单独校验。
- diff 更清楚。
- 默认配置加载测试通过。

### R10.2 删除长期 compatibility facade

当 migration 已覆盖旧配置后，删除：

- runtime legacy language 判断。
- 旧扁平 settings 属性 facade。
- `AppSettingsStore` static facade 的非必要调用。
- root namespace 兼容残留。

验收：

- 架构测试不再需要大面积 allowlist。

### R10.3 `.csproj.user` 和仓库卫生

处理：

```gitignore
*.csproj.user
*.suo
.vs/
bin/
obj/
```

确认 `.csproj.user` 不再作为必要项目文件提交。

---

## 15. 推荐执行顺序总览

严格顺序：

```text
R0 安全网
R1 命名空间和文件归位
R2 重复代码清理
R3 多项目拆分
R4 配置系统重构
R5 Application contract/effect 重构
R6 MainForm / shell 重构
R7 Settings UI 重构
R8 错误、日志、诊断统一
R9 Terraria 集成清理
R10 资源化和最终收口
```

可并行项：

```text
R2 重复代码清理 可以和 R0/R1 后半并行。
R4 配置系统 和 R7 Settings UI 不能完全并行，R4 section 稳定后 R7 再大拆。
R5 Application 和 R6 MainForm 需要串行推进，先 effect/ports，再 shell。
R9 Terraria 清理可以在 R5 contracts 稳定后并行。
```

---

## 16. 最终 CI / 本地门禁

最终 `scripts/check.ps1` 应升级为：

```powershell
$ErrorActionPreference = 'Stop'

dotnet restore TerrariaSplit.slnx
dotnet build TerrariaSplit.slnx -c Debug -warnaserror
dotnet run --project test\TerrariaSplit.Tests.csproj
.
\scripts\check-architecture.ps1
```

`check-architecture.ps1` 最终必须失败阻断：

- Domain 引用 UI / WinForms。
- Application 引用 WinForms。
- Application 引用 `AppSettingsStore`。
- Application 引用 `AppLogger`。
- Terraria 引用 MainForm / SettingsPage / Overlay shell。
- Storage 引用 UI / Application / Terraria。
- UI.Settings 启动 automation 或 watcher。

---

## 17. 最重要的执行建议

1. 先建安全网，再拆项目。
2. 先移动和改 namespace，再改逻辑。
3. 先让旧 static 成为 adapter，再逐步删除调用。
4. 配置先做 repository/projection，再做 section 化。
5. Application 先强类型 effect，再拆 MainForm。
6. Settings UI 先抽 draft/validator，再拆 controller。
7. Terraria 保护逻辑只集中和结构化，不做粗暴删除。
8. 每个 PR 必须可独立回滚。

这条路线成本更高，但收益也最大：完成后，项目会从“一个 WinForms 工具里有若干分层目录”，变成“有明确边界、可测试、可演进的桌面应用架构”。
