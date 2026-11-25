# TerrariaSplit 开发与维护约束

> 架构全景与模块细节见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)；产品语义、UI 与业务规则见 [docs/DESIGN.md](docs/DESIGN.md)；原生筛选器 ABI7 契约见 `../TerrariaResourceJudge/docs/ABI.md`。

## 常用命令

- **还原**（首次签出或增删依赖后）：
  ```powershell
  dotnet restore TerrariaSplit.slnx -m:1
  ```
- **日常构建与运行客户端**（复用增量缓存）：
  ```powershell
  dotnet build src\TerrariaSplit.WinForms\TerrariaSplit.WinForms.csproj --no-restore
  dotnet run --project src\TerrariaSplit.WinForms\TerrariaSplit.WinForms.csproj --no-build
  ```
- **构建并运行测试**（执行测试前须先构建测试项目或全量解决方案）：
  ```powershell
  dotnet build test\TerrariaSplit.Tests.csproj --no-restore
  dotnet run --project test\TerrariaSplit.Tests.csproj --no-build
  ```
- **提交/发布前确定性全量构建**：
  ```powershell
  dotnet build TerrariaSplit.slnx --no-restore -m:1 -p:UseSharedCompilation=false
  ```
- **发布与清理**（发布仅产出目录，不自动压缩）：
  ```powershell
  pwsh -NoProfile -File eng\Publish-Release.ps1 -Version <X.Y.Z.W>   # 支持 -ClientOnly / -ServerOnly
  pwsh -NoProfile -File eng\Clean-Workspace.ps1 -Execute -IncludeIde
  ```
- **专项选项**：
  - 跳过 MemoryBridge 构建：`-p:TerrariaSplitSkipMemoryBridge=true`
  - 原生集成套件：`$env:TERRARIA_SPLIT_TEST_SUITE='Native'` 后运行测试
  - 聚焦测试：`$env:TERRARIA_SPLIT_TEST_FILTER='<关键字>'` 后运行测试
  - 诊断工具：`dotnet run --project test\TerrariaSplit.Diagnostics.csproj --no-restore -- <命令及参数>`

## 核心工程红线

以下架构边界由 `QualityGateTests` 持续校验：

- **单向依赖**：严格保持 17 个生产项目的声明依赖方向——全图无环、`Terraria` 不依赖 `Storage` 或 `WinForms`、`Race.Client` 不依赖 UI、`Domain` 与 `Application` 保持零平台副作用。
- **单向数据流**：外部输入统一转为 `AppCommand`/`SystemEvent`，经 `ApplicationController` 产出 `ApplicationEffect` 与 `DisplayInvalidation`，由外层 Shell/执行器落地副作用；跨线程一律传递不可变快照。
- **跨位数统一入口**：所有 32 位 CLRMD 探测与注入统一走 `TerrariaSplit.MemoryBridge`（`win-x86`），不在其他项目另建注入器或探测进程；修改协议、参数或位数时同步更新 `MemoryBridge.targets`、`Payload`/`Bootstrap`、消费方与测试。
- **版本与竞速门禁**：普通功能按 `1.4.4`/`1.4.5`/`1.4.6+` 版本组路由；竞速功能必须精确匹配 `TerrariaBuilds.Race1461` 的版本、`ModuleVersionId` 与世界格式 `329`。
- **原生筛选契约**：
  - 生产筛选统一调用 `../TerrariaResourceJudge/out/resourcejudge-release/current/TerrariaSplit.WorldFilter.dll`（60 字节 ABI7，`1.4.4`/`1.4.5` 传 `1458`，`1.4.6+` 传 `1461`），仅接受版本与指纹一致的响应；
  - 单世界自动预算 `max(1,min(6,floor(3×可用物理核/4)))` 原样传给 DLL，多候选并发固定每世界 1 线程；
  - 物理核识别归 `Infrastructure.Windows`，预算策略归 `Terraria.WorldGeneration`。
- **日志与故障规范**：
  - 共享接口为 `Microsoft.Extensions.Logging`（`AppLog`），`Domain` 零日志依赖，`Race.Client` 仅接收 `ILoggerFactory`；
  - 日志消息、事件码、字段名与故障报告统一使用英文，并由 `TERRARIA_SPLIT_LOG_LEVEL` 控制环境变量级别；
  - 级别分工：候选未命中与重试记 `DEBUG`，用户取消记 `INFO`，可恢复降级记 `WARN`，最终操作失败经 `AppLog.Failure` 记一次 `ERROR`（向上传播只补充上下文，不重复记同级 `ERROR`）；禁止逐轮询 tick 输出日志。

## 构建、目录与变更联动

- **构建输出与版本真源**：中间产物由 [Directory.Build.props](Directory.Build.props) 统一托管在 `.build/`，禁止在单个 `.csproj` 中配置 `BaseOutputPath`/`OutputPath` 或使用 `dotnet build -o`；版本号唯一真源为 `TerrariaSplitProductVersion`，发布产物输出至 `publish/<产品名>-v<版本>-<平台>/`。
- **数据隔离**：用户数据（`Settings/`、`Data/`、`Worlds/`、`terrariasplit.log`）严禁写入 `src/` 或打入发布包；测试临时文件只放 `test/Temp/`，保留结果放 `test/Results/`。
- **构建进程异常**：若出现无编译错误但构建失败或 `dotnet` 子进程异常堆积，先停止并仅清理本次残留进程，再以 `-m:1` 重试。
- **设置项变更联动**：新增或修改设置项时同步核对 `settings.default.json`、`AppSettingsCloner`、`SettingsNormalizer`、持久化、当前 run 收尾、UI 绑定与 `WorldPoolSignature`。
- **参考与局部约束**：优先复用现有 helper、主题控件、模型与测试风格；核对游戏原版实现时只读参考 `..\reference\Terraria1458` / `..\reference\Terraria1460`；修改重点子目录前先阅读对应目录下的局部 `AGENTS.md`。
