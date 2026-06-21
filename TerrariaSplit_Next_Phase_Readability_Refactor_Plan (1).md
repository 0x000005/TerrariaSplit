# TerrariaSplit 下一阶段重构方案

目标：**可读性、可维护性、逻辑复用、可测试性**。本轮不以“拆开文件”为目标；只有当一个改动能让职责归属更清楚、调用路径更短、重复逻辑减少、失败路径更可见，才算有效重构。

当前代码已经完成了第一阶段：多项目骨架、namespace 清理、重复 helper 抽取、AppSettings section 化、ApplicationEffect 强类型化、MainForm/SplitSettingsPage 初步拆分。下一阶段要做的是把这些“骨架”变成真正稳定的架构边界。

---

## 0. 当前基线与本轮验收目标

### 当前静态基线

基于最新压缩包的静态抽查，当前还有这些明确过渡态：

| 项目 | 当前状态 | 本轮目标 |
|---|---:|---:|
| `Application.csproj` | 仍引用 `Storage`、`Terraria` | 移除这两个具体实现依赖 |
| `Configuration.csproj` | `net10.0-windows` + `UseWindowsForms=true` | 回到纯 `net10.0` |
| source layout | `src/*` 项目通过 `Compile Include=..\..\TerrariaSplit\... Link=...` 链接旧目录 | 物理移动到 `src/*`，取消 linked source |
| `AppSettingsStore.` 调用 | 19 处 | 0 处，或只剩 legacy adapter 内部 |
| `RuntimeDataPaths` 调用 | 9 处 | 0 处，或只剩 `AppContextRuntimeDataPaths` 实现内部 |
| `AppLogger.` static 调用 | 99 处 | 0 处，或只剩 `StaticAppLogger` 过渡层内部 |
| `InternalsVisibleTo` | 34 条 | runtime 项目之间尽量为 0，只保留 tests |
| `MainForm` | partial 文件拆开，但状态仍集中 | 状态迁移到 shell，MainForm 只做 WinForms 生命周期和入口转发 |
| `SplitSettingsPage` | partial 文件拆开，但核心状态仍集中 | route / condition / search / icon / commit 各自有 owner |

### 本轮完成后的硬性验收

```text
[ ] dotnet build TerrariaSplit.slnx 通过
[ ] dotnet test 通过
[ ] Application 不引用 TerrariaSplit.Storage
[ ] Application 不引用 TerrariaSplit.Terraria
[ ] Configuration 不引用 WindowsForms / System.Drawing / InstalledFontCollection
[ ] 所有 src 项目不再通过 Link 编译旧目录源码
[ ] AppSettingsStore. 调用点为 0
[ ] RuntimeDataPaths static 调用点为 0，或只在路径实现类内部出现
[ ] AppLogger. static 调用点为 0，或只在 StaticAppLogger 内部出现
[ ] 设置保存失败、PB 保存失败、world pool 保存失败能到达 UI 提示或 diagnostics
[ ] ApplicationShellEffectExecutor 遇到未知 effect 会 fail fast
[ ] Settings JSON 带 SchemaVersion
[ ] SettingsNormalizer 不再调用 SettingsMigrator
[ ] LegacyChinese 不再出现在运行时 IsChinese 判断里
[ ] MainForm 字段数量明显下降，overlay/runtime/hotkey/window 状态由 shell 持有
[ ] SplitSettingsPage 不再持有 routeDragIndex / conditionDragIndex / advancedConditionMode 等 controller 状态
[ ] runtime 项目之间的 InternalsVisibleTo 基本删除，只保留 tests
```

---

## 1. 重构原则

### 1.1 不做“文件拆分式重构”

每个 extraction 必须满足至少一个条件：

1. 新类拥有明确状态，例如 `OverlayShell` 拥有 overlay 状态，而不是 `MainForm.Overlay.cs` 继续操作 MainForm 字段。
2. 新类是可替换接口，例如 `ISettingsRepository`、`IRuntimeDataPaths`、`IWorldAutomation`。
3. 新类消除两个以上调用点的重复逻辑，例如 settings descriptor 同时服务 UI 和 normalizer。
4. 新类可以被无 WinForms / 无 Terraria 进程测试。
5. 新类让上层不再依赖下层具体实现。

如果只是把 500 行拆成两个 partial 文件，但字段、状态、控制流仍然在同一个类里，这一轮不算有效重构。

### 1.2 推荐依赖方向

目标依赖图：

```text
TerrariaSplit.WinForms
  -> TerrariaSplit.Application
  -> TerrariaSplit.Configuration
  -> TerrariaSplit.Storage
  -> TerrariaSplit.Terraria
  -> TerrariaSplit.Statistics
  -> TerrariaSplit.Infrastructure
  -> TerrariaSplit.Infrastructure.Windows

TerrariaSplit.Application
  -> TerrariaSplit.Configuration
  -> TerrariaSplit.Domain
  -> TerrariaSplit.Infrastructure

TerrariaSplit.Terraria
  -> TerrariaSplit.Application      // 实现 Application ports，可接受
  -> TerrariaSplit.Configuration
  -> TerrariaSplit.Domain
  -> TerrariaSplit.Infrastructure
  -> TerrariaSplit.Infrastructure.Windows
  -> TerrariaSplit.Storage          // 若仍直接安装/读取 world pool，短期可接受

TerrariaSplit.Storage
  -> TerrariaSplit.Configuration
  -> TerrariaSplit.Domain
  -> TerrariaSplit.Infrastructure

TerrariaSplit.Configuration
  -> TerrariaSplit.Domain

TerrariaSplit.Domain
  -> 无项目依赖
```

关键点：

```text
Application 不再引用 Terraria / Storage。
Configuration 不再引用 WinForms / System.Drawing。
UI 是 composition root，负责把 Application ports 绑定到 Terraria/Storage 实现。
```

---

## 2. PR-1：加架构门禁和重构安全网

这一 PR 不做大迁移，先把“不要退化”的规则写下来。

### 2.1 添加 `Directory.Build.props`

建议内容：

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AnalysisLevel>latest</AnalysisLevel>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

初期不要直接 `TreatWarningsAsErrors=true`，否则会把架构重构和历史 warning 清理绑在一起。等本轮完成后再逐步提高严格度。

### 2.2 添加架构测试

在你的本地 test 项目中加一组最小规则。先以 csproj 引用为准，后续再加源码扫描。

```csharp
[Fact]
public void ApplicationProject_ShouldNotReference_TerrariaOrStorage()
{
    string project = File.ReadAllText(ProjectPath("src/TerrariaSplit.Application/TerrariaSplit.Application.csproj"));

    Assert.DoesNotContain("TerrariaSplit.Terraria", project);
    Assert.DoesNotContain("TerrariaSplit.Storage", project);
}

[Fact]
public void ConfigurationProject_ShouldNotUse_WindowsForms()
{
    string project = File.ReadAllText(ProjectPath("src/TerrariaSplit.Configuration/TerrariaSplit.Configuration.csproj"));

    Assert.DoesNotContain("UseWindowsForms", project);
    Assert.DoesNotContain("net10.0-windows", project);
}
```

再加源码级规则：

```csharp
[Fact]
public void RuntimeCode_ShouldNotCall_LegacyStaticSettingsStore()
{
    string[] offenders = FindSourceFiles()
        .Where(path => !path.EndsWith("AppSettingsStore.cs", StringComparison.OrdinalIgnoreCase))
        .Where(path => File.ReadAllText(path).Contains("AppSettingsStore.", StringComparison.Ordinal))
        .ToArray();

    Assert.Empty(offenders);
}
```

第一版可以把这些测试标为 skip 或输出 report；从 PR-2 开始逐条启用。

### 2.3 添加仓库卫生规则

`.gitignore`：

```gitignore
bin/
obj/
.vs/
*.suo
*.user
*.csproj.user
```

清理：

```text
TerrariaSplit.MemoryProbe/bin
TerrariaSplit.MemoryProbe/obj
TerrariaSplit/TerrariaSplit.csproj.user
```

### PR-1 验收

```text
[ ] 新增 Directory.Build.props
[ ] 新增架构测试或架构检查脚本
[ ] bin/obj/*.csproj.user 不再出现在仓库
[ ] 测试暂时允许当前已知违规，但能列出违规清单
```

---

## 3. PR-2：净化 Application 依赖方向

这是下一阶段最重要的一步。目标是让 Application 只表达业务流程、命令、状态和 ports，不再知道 `TerrariaWorldWatcher`、`WorldPoolStore`、`CreateWorldWorkflow` 这些实现类。

### 3.1 移动 watcher ports

当前：

```text
Terraria/ITerrariaWorldWatcher.cs
Terraria/ITerrariaUiScalePatchApplier.cs
```

移动到：

```text
Application/Ports/ITerrariaWorldWatcher.cs
Application/Ports/ITerrariaUiScalePatchApplier.cs
```

命名可以保留 Terraria 前缀，因为这个 app 本来就是 TerrariaSplit，但 namespace 应该属于 Application contract：

```csharp
namespace TerrariaSplit.Application.Ports;

internal interface ITerrariaWorldWatcher : IDisposable
{
    WatcherPollCompletion Poll(RuntimeCommandBatch commands);
}

internal interface ITerrariaUiScalePatchApplier
{
    TerrariaUiScalePatchResult TryApply(TerrariaWatchSnapshot snapshot);
    void Reset();
}
```

如果这些接口依赖的模型仍在 `TerrariaSplit.Terraria`，继续移动模型。

### 3.2 移动 watcher contract models

Application 当前间接依赖 Terraria 类型，例如：

```text
TerrariaWatchSnapshot
TerrariaWatcherDiagnostics
TerrariaWorldGenerationState
TerrariaUiScalePatchResult
WatcherPollCompletion
WatcherPollNotification
```

这些类型是 Application 与 Terraria adapter 之间的 contract，不应该放在具体 Terraria 实现项目里。建议移动到：

```text
Application/RuntimeObservation/
Application/Diagnostics/
```

示例：

```text
Application/RuntimeObservation/TerrariaWatchSnapshot.cs
Application/RuntimeObservation/TerrariaWorldGenerationState.cs
Application/RuntimeObservation/WatcherPollCompletion.cs
Application/RuntimeObservation/WatcherPollNotification.cs
Application/Diagnostics/TerrariaWatcherDiagnostics.cs
Application/RuntimeObservation/TerrariaUiScalePatchResult.cs
```

这样：

```text
Application 定义“需要什么观察结果”
Terraria 实现“如何观察 Terraria 进程”
```

### 3.3 把 `TerrariaWorldAutomation` 移出 Application

当前：

```text
Application/TerrariaWorldAutomation.cs
```

它直接持有：

```csharp
CreateWorldWorkflow
EnterWorldWorkflow
WorldPoolStore
```

这不是 Application 逻辑，而是 Terraria automation 实现。建议移动到：

```text
Terraria/Automation/TerrariaWorldAutomation.cs
```

Application 只保留 port：

```csharp
namespace TerrariaSplit.Application.Ports;

internal interface IWorldAutomation : IDisposable
{
    bool IsCreateWorldRunning { get; }
    bool IsEnterWorldRunning { get; }
    Task StartCreateWorldAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task StartEnterWorldAsync(AppSettings settings, PracticeWorldSlot slot, CancellationToken cancellationToken = default);
    bool CancelCreateWorld();
    bool CancelEnterWorld();
}
```

UI composition root 负责：

```csharp
IWorldAutomation worldAutomation = new TerrariaWorldAutomation(worldPoolStore, logger);
var automationShell = new AutomationShell(worldAutomation, ...);
```

### 3.4 移动 `WorldPoolFillService`

当前：

```text
Application/WorldPoolFillService.cs
```

它直接依赖：

```csharp
WorldPoolStore
HeadlessWorldGenerator
TerrariaServerLocator
```

这也不是 Application 逻辑。建议移动到：

```text
Terraria/Automation/WorldPoolFillService.cs
```

或者如果你想让 world pool 属于 Storage/Automation 之间的独立能力，可以建：

```text
Terraria/WorldPool/WorldPoolFillService.cs
```

Application 不需要知道 world pool fill 的存在。UI 在应用设置后调用：

```csharp
worldPoolFillService.UpdateSettings(settings);
```

即可。

### 3.5 把 `AppCommand` 也改成强类型 record

你已经把 `ApplicationEffect` 改成强类型了，建议本轮把 `AppCommand` 也改掉，消除 `Kind + nullable payload`。

当前：

```csharp
internal sealed record AppCommand
{
    public AppCommandKind Kind { get; }
    public TimeSpan? Time { get; private init; }
    public AppSettings? Settings { get; private init; }
    ...
}
```

目标：

```csharp
internal abstract record AppCommand;

internal sealed record TogglePauseCommand : AppCommand;
internal sealed record ResetRunCommand(bool RecordStats, bool PlayResetSound) : AppCommand;
internal sealed record ToggleMouseClickThroughCommand : AppCommand;
internal sealed record TogglePyramidFilterCommand : AppCommand;
internal sealed record QueueMenuActionCommand(MenuActionKind Action, DateTime RequestedAtUtc) : AppCommand;
internal sealed record CancelCreateWorldCommand : AppCommand;
internal sealed record CancelEnterWorldCommand : AppCommand;
internal sealed record EditPracticeSplitTimeCommand(int SplitIndex, TimeSpan? Time) : AppCommand;
internal sealed record EditPracticeTotalTimeCommand(TimeSpan Time) : AppCommand;
internal sealed record ApplySettingsCommand(AppSettings Settings) : AppCommand;
```

`ApplicationController.HandleCommand` 改成模式匹配：

```csharp
public ApplicationUpdate HandleCommand(AppCommand command)
{
    return command switch
    {
        TogglePauseCommand => HandleTogglePause(),
        ResetRunCommand reset => HandleResetRun(reset),
        ApplySettingsCommand apply => HandleApplySettings(apply),
        _ => throw new NotSupportedException($"Unsupported command: {command.GetType().Name}")
    };
}
```

这样新增 command 时不会静默漏处理。

### PR-2 验收

```text
[ ] src/TerrariaSplit.Application.csproj 不引用 Storage
[ ] src/TerrariaSplit.Application.csproj 不引用 Terraria
[ ] Application/GlobalUsings.cs 不再 global using Storage/Terraria
[ ] TerrariaWorldAutomation 不在 Application 项目
[ ] WorldPoolFillService 不在 Application 项目
[ ] watcher ports 和 watcher contract models 在 Application
[ ] AppCommand 不再使用 Kind + nullable payload
[ ] ApplicationController 对未知 command fail fast
```

---

## 4. PR-3：消灭 static settings/path/logger，并让失败可见

这一 PR 的目标是把“隐式全局依赖”替换成 composition root 注入，同时让保存失败、文件失败、自动化失败不再只写日志。

### 4.1 引入 runtime paths 实例

新增：

```csharp
namespace TerrariaSplit.Infrastructure;

internal interface IRuntimeDataPaths
{
    string DataDirectory { get; }
    string SettingsDirectory { get; }
    string ReferenceTimesDirectory { get; }
    string LastRunTimesDirectory { get; }
    string PersonalBestTimesDirectory { get; }
    string PersonalBestSegmentsDirectory { get; }
    string WorldPoolDirectory { get; }
    string WorldPoolScratchDirectory { get; }
    string LogPath { get; }
}
```

实现：

```csharp
internal sealed class AppContextRuntimeDataPaths : IRuntimeDataPaths
{
    private readonly string baseDirectory;

    public AppContextRuntimeDataPaths(string? baseDirectory = null)
    {
        this.baseDirectory = baseDirectory ?? AppContext.BaseDirectory;
    }

    public string SettingsDirectory => Path.Combine(baseDirectory, "Settings");
    ...
}
```

替换：

```text
RuntimeDataPaths.SettingsDirectory
RuntimeDataPaths.WorldPoolDirectory
RuntimeDataPaths.LogPath
```

最终 `RuntimeDataPaths` 删除，或只保留 very short legacy adapter 并不被 runtime 使用。

### 4.2 把 JsonFileStore 改成实例服务

当前：

```csharp
JsonFileStore.Write(...) -> bool
JsonFileStore.Read(...) -> T?
```

建议：

```csharp
internal interface IJsonFileStore
{
    OperationResult<T?> Read<T>(string path, string description);
    OperationResult Write<T>(string path, T value, string description);
    OperationResult WriteText(string path, string value, string description);
}
```

实现中注入 logger：

```csharp
internal sealed class JsonFileStore : IJsonFileStore
{
    private readonly IAppLogger logger;

    public OperationResult Write<T>(string path, T value, string description)
    {
        try { ...; return OperationResult.Success(); }
        catch (Exception ex)
        {
            logger.Error(ex, $"Failed to write {description}: {path}");
            return OperationResult.Failure($"Failed to save {description}.", ex);
        }
    }
}
```

### 4.3 让 settings repository 返回结果

当前：

```csharp
void Save(AppSettings settings)
AppSettings Load()
```

建议：

```csharp
internal interface ISettingsRepository
{
    OperationResult<AppSettings> Load();
    OperationResult<AppSettings> Load(string path);
    OperationResult Save(AppSettings settings);
    AppSettings CreateSnapshot(AppSettings settings);
    void Normalize(AppSettings settings);
}
```

如果一次改动太大，可以先做折中：

```csharp
OperationResult TrySave(AppSettings settings);
AppSettings LoadOrDefault();
```

但最终目标应该是：写失败不能只停留在 log 文件。

### 4.4 更新 effect executor

当前：

```csharp
case SaveSettingsEffect save:
    settings.Save(save.Settings);
    break;
```

目标：

```csharp
case SaveSettingsEffect save:
    OperationResult result = settings.Save(save.Settings);
    if (result.Failed)
    {
        notifications.ShowError("Settings save failed", result.Message);
    }
    break;
```

新增 port：

```csharp
internal interface IShellNotificationPort
{
    void ShowError(string title, string message);
    void ShowInfo(string message);
    void ReportDiagnostic(string source, string message);
}
```

同时给 `ApplicationShellEffectExecutor.Apply` 加 default：

```csharp
default:
    throw new NotSupportedException($"Unsupported application effect: {effect.GetType().Name}");
```

### 4.5 Logger 全面实例化

当前 static 调用点约 99 处。建议按模块迁移：

第一批：Storage

```text
AppSettingsRepository
SettingsSerializer
SettingsProfileStore
SplitTimeSetStore
WorldPoolStore
```

第二批：Terraria automation

```text
CreateWorldWorkflow
HeadlessWorldGenerator
EnterWorldWorkflow
TerrariaAutomationContext
TerrariaWorldFilePyramidScanner
```

第三批：UI

```text
SoundPlayerService
GlobalHotkeyManager
OverlayWindowController
SettingsForm
DebugSettingsPage
```

最终：

```text
AppLogger static 删除，或只保留给 StaticAppLogger 过渡使用。
```

### PR-3 验收

```text
[ ] AppSettingsStore. 调用点为 0
[ ] RuntimeDataPaths 直接调用点为 0
[ ] AppLogger. 直接调用点为 0，或只剩 StaticAppLogger 内部
[ ] SettingsRepository.Save 返回 OperationResult
[ ] JsonFileStore.Write 失败能传到调用方
[ ] SaveSettingsEffect 失败能显示用户可见错误
[ ] WorldPoolStore / SplitTimeSetStore 不再是纯 static store
```

---

## 5. PR-4：配置系统纯化、版本化 migration、DTO 与行为分离

当前配置 section 化已经做得很好。下一步是让 Configuration 成为纯配置层，不承担 UI/font/windows，也不让 runtime 逻辑永远兼容旧字段。

### 5.1 移走 `UiFontSettings` 的 Windows 逻辑

当前 `Configuration/UiFontSettings.cs` 使用：

```csharp
using System.Drawing;
using System.Drawing.Text;
```

这迫使 `Configuration.csproj` 变成 Windows/WinForms 项目。

目标拆分：

```text
Configuration/UiFontDefaults.cs
Infrastructure.Windows/InstalledFontCatalog.cs
UI/Rendering/UiFontFactory.cs
```

Configuration 只保留：

```csharp
internal static class UiFontDefaults
{
    public const string DefaultFamilyName = "Segoe UI";
}
```

Windows 层：

```csharp
internal interface IInstalledFontCatalog
{
    IReadOnlyList<string> GetInstalledFamilyNames();
    string NormalizeInstalledFamilyName(string? familyName);
}
```

UI 层：

```csharp
internal interface IUiFontFactory
{
    Font CreateFont(string? familyName, float size, FontStyle style, GraphicsUnit unit = GraphicsUnit.Point);
}
```

Normalizer 不再检查字体是否安装，只做：

```csharp
column.FontFamily = string.IsNullOrWhiteSpace(column.FontFamily)
    ? defaults.FontFamily
    : column.FontFamily.Trim();
```

实际 fallback 放到 render/font factory。

### 5.2 Settings JSON 加 `SchemaVersion`

目标文件结构：

```json
{
  "SchemaVersion": 2,
  "Settings": {
    "General": {},
    "Hotkeys": {},
    "Route": {},
    "Comparison": {},
    "Overlay": {},
    "Automation": {},
    "PracticeWorlds": {},
    "Advanced": {}
  }
}
```

读取流程：

```text
Read JSON node
  -> Detect schema version
  -> Migrate JSON to current schema
  -> Deserialize SettingsDocument
  -> Normalize current settings
  -> Load external split sets / PB sets
  -> Return AppSettings
```

保存流程：

```text
Clone settings
  -> Normalize current schema
  -> Save external split sets / PB sets
  -> Project persistence DTO
  -> Write SettingsDocument { SchemaVersion, Settings }
```

### 5.3 Migration 与 Normalizer 分离

当前：

```csharp
SettingsNormalizer.Normalize(settings)
{
    SettingsMigrator.Migrate(settings);
    ...
}
```

目标：

```text
SettingsJsonMigrator 只处理旧 JSON -> 当前 JSON
SettingsObjectMigrator 只处理极少数无法 JSON 迁移的对象兼容
SettingsNormalizer 只处理当前 schema 的合法化
```

即：

```csharp
JsonObject current = SettingsJsonMigrator.MigrateToCurrent(root);
AppSettings settings = current.Deserialize<SettingsDocument>().Settings;
SettingsNormalizer.Normalize(settings);
```

`Normalize` 中不再调用 `Migrate`。

### 5.4 处理 `LegacyChinese`

当前 runtime 仍然有：

```csharp
LanguageNames.LegacyChinese = "涓枃";
LanguageNames.IsChinese(...) 接受旧损坏编码
```

目标：

```text
migration 阶段："涓枃" -> "中文"
runtime 阶段：只识别 "中文" / "Chinese" / language code
保存阶段：写回规范值
```

### 5.5 把 AppSettings 从“行为对象”收敛为 DTO

当前 `AppSettings` 里有很多行为：

```text
TryGetReferenceSplit
GetReferenceText
SetReferenceText
GetActiveReferenceSet
CreatePersonalBestReferenceSet
SyncPersonalBestTimesFromActiveSet
SyncActivePersonalBestTimeSetFromDictionary
```

这会让 DTO 变成隐藏业务服务。建议新增：

```text
Configuration/Comparison/ReferenceSplitSetService.cs
Configuration/Comparison/PersonalBestSetService.cs
Application/Timing/SplitTimingLookup.cs
```

示例：

```csharp
internal sealed class ReferenceSplitSetService
{
    public ReferenceSplitSet GetActiveReferenceSet(AppSettings settings, IReadOnlyList<SplitDefinition> definitions);
    public bool TryGetReferenceSplit(AppSettings settings, SplitDefinition definition, out TimeSpan split);
    public void SetReferenceText(AppSettings settings, string splitName, string value);
}
```

过渡期可以保留 AppSettings 方法，但加 TODO 或 Obsolete：

```csharp
[Obsolete("Use ReferenceSplitSetService instead.")]
public bool TryGetReferenceSplit(...)
```

最终 `AppSettings` 只保留 section 属性。

### PR-4 验收

```text
[ ] Configuration.csproj 为 net10.0
[ ] Configuration.csproj 没有 UseWindowsForms
[ ] Configuration 项目无 System.Drawing / InstalledFontCollection
[ ] settings.json 保存时包含 SchemaVersion
[ ] 无 SchemaVersion 的旧 settings 可以迁移
[ ] SettingsNormalizer 不调用 SettingsMigrator
[ ] LanguageNames 不再暴露 LegacyChinese runtime 判断
[ ] AppSettings 行为方法开始迁移到 service，或至少新增 service 并替换主要调用点
```

---

## 6. PR-5：物理移动 source layout，结束 linked-source 过渡态

当前 `src/*` 项目用 linked source 编译旧目录，例如：

```xml
<Compile Include="..\..\TerrariaSplit\Application\**\*.cs" Link="Application\%(RecursiveDir)%(Filename)%(Extension)" />
```

这对第一阶段迁移很方便，但长期可读性差：文件实际位置和项目位置不一致。

### 6.1 目标目录

```text
src/
  TerrariaSplit.Domain/
    Domain/
    Models/
  TerrariaSplit.Configuration/
    Configuration/
    Localization/
    Assets/Defaults/
  TerrariaSplit.Infrastructure/
    Infrastructure/
  TerrariaSplit.Infrastructure.Windows/
    Infrastructure/Windows/
  TerrariaSplit.Storage/
    Storage/
  TerrariaSplit.Statistics/
    Statistics/
  TerrariaSplit.Terraria/
    Terraria/
  TerrariaSplit.WinForms/
    Program.cs
    Properties/
    UI/
    Assets/Icons/
    Assets/BossAnimations/
    Assets/TerrariaWorldNames/
  TerrariaSplit.MemoryProbe/
```

根目录只保留：

```text
TerrariaSplit.slnx
Directory.Build.props
.editorconfig
.gitignore
README.md
build/
docs/
test/
```

### 6.2 更新 csproj

取消：

```xml
<EnableDefaultCompileItems>false</EnableDefaultCompileItems>
<Compile Include="..\..\TerrariaSplit\..." Link="..." />
```

让每个项目自然编译自身目录下的 `.cs`。

### 6.3 更新资源路径

检查这些资源：

```text
Assets/Defaults/*.json       -> Configuration embedded resource
Assets/Icons/*               -> WinForms content/resource
Assets/BossAnimations/*      -> WinForms content/resource
Assets/ReferenceTimes/*      -> Storage embedded/resource, 或迁入 Configuration/Defaults
Assets/TerrariaWorldNames/*  -> Terraria/Automation resource 或 WinForms content
```

资源所有权原则：

```text
默认 settings/reference data -> Configuration/Storage
图标/动画/UI asset -> WinForms
Terraria automation seed/name data -> Terraria
```

### PR-5 验收

```text
[ ] 所有 src 项目不再使用 Link 编译旧目录
[ ] 根 TerrariaSplit/ 旧源码目录删除或改名为 src/TerrariaSplit.WinForms
[ ] solution 项目路径更新
[ ] test 项目引用更新
[ ] build/test 通过
```

---

## 7. PR-6：MainForm 从 partial 拆分升级为 shell ownership

当前 MainForm 已经拆成多个 partial 文件，但状态仍集中在 MainForm 字段里。下一步要把状态迁移到真正的 shell class。

### 7.1 目标结构

```text
UI/Shell/
  MainForm.cs                       // WinForms 生命周期、入口转发
  MainShellCompositionRoot.cs       // 装配
  RuntimeShell.cs                   // watcher、runtime snapshot、command sequence
  OverlayShell.cs                   // overlay windows、bounds、paint suspension、timer overlay settings
  HotkeyShell.cs                    // hotkey manager、warning、registration lifecycle
  WindowShell.cs                    // dragging、topmost、click-through window style
  ModalShell.cs                     // modal windows、input routing
  SettingsShell.cs                  // 已有，继续清理
  AutomationShell.cs                // 接收 IWorldAutomation，不 new 具体实现
```

### 7.2 OverlayShell 应拥有的状态

从 MainForm 迁走：

```text
mouseClickThrough
runtimeOverlayPaintSuspensionCount
overlayWindowsInitialized
overlayWindowInitializationInProgress
statusBoundsFeedbackEnabled
suppressStatusBoundsFeedback
pendingInitialCompositeBounds
timerOverlaySettingsRevision
timerOverlaySettingsSnapshot
statusOverlayContentDirty
lastStatusOverlayDynamicKey
statusOverlayPartialClipBounds
appliedOverlayReservedRowCount
appliedOverlayVisibleRowCount
overlayWindowController
overlayBoundsController
timerOverlayHost
renderResources
overlayAnimations
```

OverlayShell API 示例：

```csharp
internal sealed class OverlayShell : IDisposable
{
    public void Initialize(AppSettings settings, OverlayRenderContext context);
    public void ApplySettings(AppSettings previous, AppSettings current, int splitCount);
    public void ToggleMouseClickThrough();
    public void RefreshTimerOverlaySettings(AppSettings settings);
    public void InvalidateAll();
    public void QueueStatusPaint();
    public void SuspendRuntimePaint();
    public void ResumeRuntimePaint();
}
```

MainForm 不再直接知道 timer overlay settings snapshot、partial clip、overlay initialization flags。

### 7.3 RuntimeShell 应拥有的状态

从 MainForm 迁走：

```text
monitorCoordinator
runtimeDebugSnapshotLock
watcherDiagnostics
snapshot
controlTickDispatchPending
runtimeControlSchedulerSuspended
controlScheduler
statusPaintScheduler 与 runtime tick 相关部分
```

RuntimeShell API 示例：

```csharp
internal sealed class RuntimeShell : IDisposable
{
    public TerrariaWatchSnapshot CurrentSnapshot { get; }
    public TerrariaWatcherDiagnostics Diagnostics { get; }
    public RuntimeDebugSnapshot CreateDebugSnapshot(ApplicationViewState viewState);

    public long Submit(RuntimeCommand command);
    public long ClearPendingMenuActions();
    public void Tick(SplitTimerPhase phase, bool patchEnabled);
    public void ApplyReadyPollInterval(TimeSpan interval);
    public void ResetUiScalePatchState();
}
```

MainForm 只订阅：

```csharp
runtimeShell.WatcherPollCompleted += notification => ApplyApplicationUpdate(...);
```

### 7.4 HotkeyShell 应拥有的状态

迁走：

```text
hotkeyManager
lastHotkeyWarningText
registerGlobalHotkeys
RegisterConfiguredHotkeys
DisposeHotkeys
```

API：

```csharp
internal sealed class HotkeyShell : IDisposable
{
    public void ApplySettings(AppSettings settings);
    public void Register(AppSettings settings);
    public void Unregister();
}
```

### 7.5 WindowShell 应拥有的状态

迁走：

```text
dragging
dragStartCursor
closeFinalizationPending
closeFinalizationComplete
closing
currentWindowText
mainWindowModalInputRouter
```

WindowShell 处理：

```text
drag move
click-through extended style
topmost sync
window title sync
modal activation redirect
closing lifecycle
```

### 7.6 MainForm 验收标准

MainForm 最终只保留：

```text
ApplicationController
ApplicationShellEffectExecutor
RuntimeShell
OverlayShell
SettingsShell
AutomationShell
HotkeyShell
WindowShell
```

字段目标：从当前几十个字段降到 12-18 个以内。

### PR-6 验收

```text
[ ] MainForm 不再持有 overlay/timer overlay 内部状态
[ ] MainForm 不再持有 watcher snapshot/diagnostics lock
[ ] MainForm 不再持有 hotkey warning state
[ ] MainForm partial 文件可以减少，而不是继续增加
[ ] OverlayShell / RuntimeShell / HotkeyShell / WindowShell 有独立单元测试或 fake 测试
```

---

## 8. PR-7：Settings UI 从 partial 拆分升级为 controller + reusable binding

当前 `SplitSettingsPage` 已经拆成多个 partial，但状态仍集中。下一步要把状态分配给 controller，让 page 只负责 layout 和 wiring。

### 8.1 目标结构

```text
UI/Settings/Splits/
  SplitSettingsPage.cs                  // layout + controller wiring
  SplitRouteDraft.cs                    // 已有，继续保留
  SplitRouteListController.cs           // route list 状态、选择、拖拽、增删改
  SplitConditionEditorController.cs     // condition list、advanced mode、drag/drop
  SplitTargetSearchController.cs        // target kind/search/result，复用现有 SplitTargetSearch/TargetListController
  SplitIconOverrideController.cs        // icon override source/target/file
  SplitSettingsCommitService.cs         // Apply / OnDeselected / validation / normalization / model changed
  SplitSettingsRouteValidator.cs        // 已有
```

### 8.2 SplitSettingsPage 不再持有这些状态

迁走：

```text
routeDirty
loadedRouteEntryIndex
routeDragIndex
routeDragStartPoint
conditionDragIndex
conditionDragStartPoint
currentCondition
advancedConditionMode
advancedConditionError
preserveCurrentCondition
updatingConditionSettings
refreshingRouteList
```

Page 可以持有 controls，但业务状态应由 controller 持有。

### 8.3 统一 Apply / OnDeselected

当前 `Apply` 和 `OnDeselected` 的逻辑有重复：

```text
SaveSelectedEntryFromControls
EnsureEntryIds
NormalizeAttachedRouteFlags
SaveExpansionSettings
TryValidateRoute
AppSettingsStore.Normalize
NotifyModelChanged
statusLabel.Text
routeDirty=false
```

目标：

```csharp
internal sealed class SplitSettingsCommitService
{
    public SplitCommitResult CommitTo(AppSettings target, SplitCommitMode mode);
}

internal enum SplitCommitMode
{
    StrictApply,
    LenientDeselection
}
```

结果对象：

```csharp
internal readonly record struct SplitCommitResult(
    bool Succeeded,
    bool RouteChanged,
    bool ExpansionChanged,
    string Message);
```

Page 只做：

```csharp
public override void Apply(AppSettings settings)
{
    SplitCommitResult result = commitService.CommitTo(settings, SplitCommitMode.StrictApply);
    statusLabel.Text = result.Message;

    if (!result.Succeeded)
    {
        throw new SettingsApplyFailedException(result.Message);
    }
}
```

### 8.4 引入 settings field binding，减少页面重复代码

建立简单 binder，不要一开始做过度泛型。

```csharp
internal sealed class SettingsFieldBinder
{
    public void BindNumber(TextBox box, Func<int> get, Action<int> set, int min, int max, int fallback);
    public void BindBool(CheckBox box, Func<bool> get, Action<bool> set);
    public void BindText(TextBox box, Func<string> get, Action<string> set, Func<string, string>? normalize = null);
}
```

Settings pages 可以变成：

```csharp
binder.BindNumber(shortDelayBox,
    () => autoCreate.ShortActionDelayMilliseconds,
    value => autoCreate.ShortActionDelayMilliseconds = value,
    0,
    5000,
    AppSettingsDefaults.Automation.AutoCreate.ShortActionDelayMilliseconds);
```

### 8.5 用 descriptor 复用 TextEffects / Columns 逻辑

当前 `UiTextEffectSettings` 有大量相似字段，normalizer、settings page、renderer 都容易重复处理。

新增 descriptor：

```csharp
internal sealed record TextEffectDescriptor(
    string Key,
    string Label,
    Func<UiTextEffectSettings, int> GetOpacity,
    Action<UiTextEffectSettings, int> SetOpacity,
    Func<UiTextEffectSettings, int> GetShadow,
    Action<UiTextEffectSettings, int> SetShadow,
    Func<UiTextEffectSettings, int> GetOutline,
    Action<UiTextEffectSettings, int> SetOutline);
```

统一定义：

```csharp
internal static class TextEffectDescriptors
{
    public static IReadOnlyList<TextEffectDescriptor> All { get; } = [...];
}
```

使用位置：

```text
SettingsSectionNormalizer.NormalizeTextEffects
AnimationSettingsPage
TextEffectRenderer / OverlayTextStyles
```

同理 column：

```csharp
internal sealed record OverlayColumnDescriptor(
    SplitColumnRole Role,
    string Label,
    Func<UiColumnLayoutSettings, UiColumnSettings> Get);
```

使用位置：

```text
SettingsSectionNormalizer.NormalizeColumnSettings
UiSettingsPage
SplitListRenderer.GetColumnRects
```

### PR-7 验收

```text
[ ] SplitSettingsPage 不再直接持有 route/condition drag 状态
[ ] Apply 和 OnDeselected 共用 CommitService
[ ] Settings pages 不直接调用 AppSettingsStore.Normalize
[ ] TextEffects normalizer 和 UI 至少共用一套 descriptor
[ ] Columns normalizer 和 UI/renderer 至少共用一套 descriptor
[ ] SplitSettingsPage.Condition.cs 行数显著下降，controller 可单测
```

---

## 9. PR-8：渲染与计时逻辑复用优化

目标不是追求极致性能，而是让 renderer 不再临时到处查询 settings/domain state，减少重复计算和隐藏依赖。

### 9.1 新增 OverlayFrameBuilder

当前 renderer 内部会做很多事情：

```text
SplitDisplayRows.Build
current split focus index
reference split comparison
PB segment comparison
visible icon fact keys
column rects
opacity/depth scale
```

建议新增：

```text
UI/Rendering/OverlayFrameBuilder.cs
UI/Rendering/OverlayFrame.cs
UI/Rendering/SplitRowRenderPlan.cs
```

结构：

```csharp
internal sealed record OverlayFrame(
    AppSettings Settings,
    IReadOnlyList<SplitRowRenderPlan> Rows,
    int FocusRowIndex,
    SplitTimerPhase TimerPhase,
    TimeSpan TimerElapsed);

internal sealed record SplitRowRenderPlan(
    SplitDisplayRow DisplayRow,
    SplitStatusSnapshot Status,
    SplitDefinition DisplayDefinition,
    bool IsCurrent,
    SplitComparison SplitComparison,
    SplitComparison SegmentComparison,
    float DepthScale,
    float DepthOpacity);
```

Renderer 变成：

```csharp
SplitListRenderer.Render(Graphics graphics, OverlayFrame frame, OverlayRenderContext context, ...)
```

好处：

```text
渲染层更像纯绘制，不再到处查询业务规则
同一 frame 可以给 partial dirty calculation / full render / debug preview 复用
比较逻辑集中测试
```

### 9.2 把 comparison 逻辑集中到 service

当前比较逻辑分散在：

```text
AppSettings.TryGetReferenceSplit
SplitRenderData.GetSplitComparison
SplitRenderData.GetPersonalBestSegmentComparison
SplitTimingComparisons
RunFinalizer / RunLifecycle
```

新增：

```text
Application/Timing/SplitComparisonService.cs
Application/Timing/PersonalBestUpdateService.cs
Configuration/Comparison/ReferenceSplitSetService.cs
```

目标：

```csharp
internal sealed class SplitComparisonService
{
    public SplitComparison GetSplitComparison(...);
    public SplitComparison GetSegmentComparison(...);
    public bool TryGetCompletedSegmentTime(...);
}
```

Renderer、RunFinalizer、PB update 走同一套入口。

### 9.3 优化 `SplitListRenderer.Render` 的绘制顺序计算

当前 focus row 绘制顺序通过 distance loop + inner loop：

```csharp
for distance = maxDistance; distance >= 0; distance--
    foreach row in rows
        if abs(row.RowIndex - focusIndex) == distance
            RenderRow(...)
```

这对小列表没问题，但可读性一般。可以由 `OverlayFrameBuilder` 预先提供：

```csharp
IReadOnlyList<SplitRowRenderPlan> PaintOrderRows
```

实现可以简单清晰：

```csharp
Rows.OrderByDescending(row => Math.Abs(row.DisplayRow.RowIndex - FocusRowIndex))
    .ThenBy(row => row.DisplayRow.RowIndex)
```

如果担心 LINQ 分配，可写一次 helper：

```csharp
SplitRowPaintOrder.Create(rows, focusIndex)
```

Renderer 不需要理解为什么要远处先画。

### PR-8 验收

```text
[ ] SplitListRenderer 不再直接构建 SplitDisplayRows
[ ] SplitListRenderer 不再直接处理 reference/PB comparison 规则
[ ] OverlayFrameBuilder 有单测覆盖 focus row、attached rows、expanded rows、completed fact icons
[ ] renderer 更接近纯绘制函数
```

---

## 10. PR-9：Terraria automation 工作流复用与结果化

当前 `CreateWorldWorkflow.cs`、`HeadlessWorldGenerator.cs`、memory resolver 仍是大文件。这里不建议只按行数拆，而要按“自动化步骤”和“失败结果”拆。

### 10.1 把 workflow 拆成可复用步骤

目标结构：

```text
Terraria/Automation/
  TerrariaWorldAutomation.cs
  AutomationRunner.cs                         // 如果仍在 Application，可考虑移到 Infrastructure/Application.Common
  CreateWorld/
    CreateWorldWorkflow.cs                    // 高层编排，变短
    WorldCreationMenuDriver.cs                // 点击菜单、输入 seed/name/配置
    WorldCreationStateDetector.cs             // 判断当前在哪个菜单/是否可点击
    SeedClipboardService.cs                   // 剪贴板写入/恢复
    PyramidFilterWorkflow.cs                  // pyramid filter 阶段
    PyramidSeedPreScreenWorkflow.cs           // pre-screen 阶段
    WorldPoolInstallWorkflow.cs               // pooled world 安装/丢弃
    ZenithStarCatchWorkflow.cs                // special seed star catch
  EnterWorld/
    EnterWorldWorkflow.cs
    EnterWorldSaveInstaller.cs
```

`CreateWorldWorkflow` 只保留编排：

```csharp
public async Task<AutomationResult> RunAsync(AppSettings settings, CancellationToken token)
{
    using var session = await context.StartAsync(...);
    await menuDriver.OpenCreateWorldAsync(session, token);
    await menuDriver.ApplyWorldOptionsAsync(session, settings, token);
    AutomationResult filter = await pyramidFilter.TryAcceptAsync(...);
    ...
}
```

### 10.2 统一 AutomationResult

当前自动化很多失败只 log。建议：

```csharp
internal sealed record AutomationResult(
    bool Succeeded,
    bool Cancelled,
    string UserMessage,
    string DiagnosticMessage,
    Exception? Exception = null)
{
    public static AutomationResult Success(string diagnostic = "") => ...;
    public static AutomationResult CancelledByUser() => ...;
    public static AutomationResult Failure(string userMessage, string diagnostic, Exception? exception = null) => ...;
}
```

`AutomationShell` 收到失败：

```csharp
AutomationResult result = await worldAutomation.StartCreateWorldAsync(...);
if (!result.Succeeded && !result.Cancelled)
{
    notifications.ShowError("Create world failed", result.UserMessage);
}
```

### 10.3 复用文件/进程/窗口保护逻辑

已经有 `FileAccessProbe`，继续集中：

```text
ProcessLifecycleGuard
WindowActivationService
ClipboardBackupScope
TemporaryDirectoryScope
```

示例：

```csharp
internal sealed class ClipboardBackupScope : IDisposable
{
    public static OperationResult<ClipboardBackupScope> TrySetText(string text);
    public void Dispose(); // restore
}
```

这样 `CreateWorldWorkflow` 里不再散落 clipboard try/catch。

### PR-9 验收

```text
[ ] CreateWorldWorkflow 行数显著下降，主要保留编排
[ ] 自动化失败返回 AutomationResult，不只写日志
[ ] Clipboard / Window activation / temporary scratch 有 scope/service 复用
[ ] AutomationShell 能显示关键失败，例如找不到 Terraria、无法写剪贴板、无法安装 world
```

---

## 11. PR-10：清理 InternalsVisibleTo 与 public contract

当前大量 runtime 项目互相 friend，短期方便，长期会让项目边界失效。

### 11.1 明确 public API

建议原则：

```text
Domain: public model / value object / domain services
Configuration: public settings DTO / settings services / repository interfaces
Application: public controller / commands / effects / ports / runtime observation models
Infrastructure: public common interfaces and utilities
Storage/Terraria/WinForms: internal implementation为主，只暴露 composition root 需要创建的类型
```

### 11.2 删除 runtime IVT

最终保留：

```csharp
[assembly: InternalsVisibleTo("TerrariaSplit.Tests")]
```

删除 runtime 之间：

```text
Domain -> Configuration/Storage/Statistics/Terraria/Application/WinForms
Configuration -> Storage/Statistics/Terraria/Application/WinForms
Storage -> Terraria/Application/WinForms
Terraria -> Application/WinForms
Infrastructure -> Storage/Terraria/Application/WinForms
```

如果删不掉，说明 API 边界还没设计清楚，不要用 IVT 硬绕。

### PR-10 验收

```text
[ ] runtime 项目之间基本没有 InternalsVisibleTo
[ ] tests 仍可通过 InternalsVisibleTo 访问必要 internal
[ ] 不因为 IVT 删除而把所有类型无脑 public；public 类型应该是 contract
```

---

## 12. 建议执行顺序

推荐拆成 10 个可合并 PR：

```text
PR-1  安全网与架构检查
PR-2  Application 依赖反转，移出 Terraria/Storage 具体实现
PR-3  消灭 static settings/path/logger，保存失败结果化
PR-4  配置系统纯化，版本化 migration，AppSettings 行为外移
PR-5  物理移动 source layout，结束 linked source
PR-6  MainForm shell ownership
PR-7  Settings UI controller 化 + reusable binding/descriptor
PR-8  渲染 frame plan + comparison 逻辑复用
PR-9  Terraria automation workflow 复用与结果化
PR-10 InternalsVisibleTo 收口，最终架构门禁转为强制
```

每个 PR 的原则：

```text
[ ] 可以独立 build/test
[ ] 不把失败路径改成静默
[ ] 不新增 static 依赖
[ ] 不新增 MainForm/SplitSettingsPage 状态字段
[ ] 新增抽象必须有两个以上使用场景，或能明确阻断错误依赖
```

---

## 13. 最值得优先做的代码复用点

### 13.1 Settings descriptor

优先收益最高。它能同时减少：

```text
SettingsSectionNormalizer 重复 clamp
AnimationSettingsPage 重复控件构建
UiSettingsPage 重复列设置
Renderer 中重复字段选择
```

不要一开始做复杂框架，先做静态 descriptor 列表即可。

### 13.2 Comparison service

把 reference/PB/segment comparison 统一，减少：

```text
AppSettings
SplitRenderData
SplitTimingComparisons
RunFinalizer
ApplicationController
```

之间的规则散落。

### 13.3 OverlayFrameBuilder

让 renderer 只绘制 plan，不计算业务状态。这个对可读性非常明显。

### 13.4 Automation scope/service

复用：

```text
ClipboardBackupScope
WindowActivationService
TemporaryDirectoryScope
ProcessLifecycleGuard
```

可以显著缩短 `CreateWorldWorkflow` 和 `HeadlessWorldGenerator`。

### 13.5 Settings commit service

把所有 settings page 的 Apply/OnDeselected 语义统一，尤其是 `SplitSettingsPage`。

---

## 14. 本轮不建议优先做的事

```text
不要继续只增加 MainForm.*.cs partial 文件。
不要为了行数强拆 world generation replica，它们偏复刻/数据逻辑，收益不如 UI/Application/Config。
不要一开始把所有 settings DTO 改成 immutable record，WinForms 编辑会很痛；先隔离 mutation owner。
不要把所有 public/internal 一次性改完，先建立 contract，再删 IVT。
不要把所有错误都弹窗；用户数据保存和自动化关键失败必须可见，普通 debug 事件进 diagnostics。
```

---

## 15. 最终成功状态

这一阶段完成后，代码读起来应该是这样的：

```text
Program/MainForm
  只负责 WinForms 生命周期、事件入口、composition root。

ApplicationController
  只处理 AppCommand -> ApplicationUpdate，不知道 TerrariaWorldWatcher、WorldPoolStore、AppSettingsStore。

Application ports
  描述需要的外部能力：watcher、ui scale patch、automation、settings save notification。

Terraria project
  实现 Terraria 进程观察、memory scan、automation、world pool fill。

Storage project
  实现 settings/split times/world pool 持久化，所有写失败返回 OperationResult。

Configuration project
  是纯 net10.0，负责 settings schema、defaults、migration、normalization，不创建 Font，不扫描系统字体。

UI shell
  OverlayShell / RuntimeShell / HotkeyShell / WindowShell 各自持有自己的状态。

Settings UI
  Page 负责 layout，controller 负责 state，commit service 负责保存语义，descriptor 负责重复字段。

Renderer
  接收 OverlayFrame/RenderPlan，只绘制，不到处推导业务状态。
```

这才是真正服务于可读性和可维护性的重构，而不是简单把大文件拆成小文件。
