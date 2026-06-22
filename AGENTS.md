# Agent 工作说明

## 适用范围
- 本文件适用于整个仓库；进入子目录前先检查更近的 `AGENTS.md`，内层规则优先。
- 本项目是 C# / .NET 10 的 Terraria 分段计时器，主程序是 WinForms 桌面应用。
- 仓库按 `src/`、`test/`、`docs/` 分层；`publish/`、`bin/`、`obj/` 和各种 `.codex-*` 目录都是本地产物。
- 开发时先理解现有信息流和项目引用方向，再改代码；不要为局部任务引入第二套并行架构。

## 常用命令
- 构建解决方案：`dotnet build TerrariaSplit.slnx`
- 运行主程序：`dotnet run --project src\TerrariaSplit.WinForms\TerrariaSplit.WinForms.csproj`
- 运行测试：`dotnet run --project test\TerrariaSplit.Tests.csproj`
- 发布主程序：`dotnet publish src\TerrariaSplit.WinForms\TerrariaSplit.WinForms.csproj -c Release -o publish`
- 跳过探针构建：给构建或发布命令追加 `-p:TerrariaSplitSkipMemoryProbe=true`
- 金字塔预筛聚焦测试：`$env:TERRARIA_SPLIT_TEST_FILTER='Pyramid seed pre-screen'; dotnet run --project test\TerrariaSplit.Tests.csproj`
- 金字塔预筛数据集评估：`dotnet run -c Release --project test\TerrariaSplit.Tests.csproj -- pyramid-metrics <world-folder> --csv test\metrics-current-release.csv`

## 当前项目结构
- `src/TerrariaSplit.Domain/`：纯领域规则。计时、boss split、路线、分组、时间格式和比较逻辑应优先放在这里。
- `src/TerrariaSplit.Domain/Models/`：跨层共享的简单记录、定义和 run/boss 数据模型。
- `src/TerrariaSplit.Configuration/`：设置模型、默认值、归一化、序列化、profile、语言资源和配置资产。
- `src/TerrariaSplit.Infrastructure/`：平台无关基础设施，例如 JSON helper、日志、性能诊断和调度工具。
- `src/TerrariaSplit.Infrastructure.Windows/`：Windows 平台封装，例如 layered window、窗口句柄和 Win32 相关能力。
- `src/TerrariaSplit.Application/`：应用编排层。这里定义 command、effect、event、snapshot 和 runtime 协调。
- `src/TerrariaSplit.Storage/`：运行统计和 split time 持久化。
- `src/TerrariaSplit.Statistics/`：统计展示模型和表格行构造，不负责持久化。
- `src/TerrariaSplit.Terraria/`：Terraria 外部集成。包含进程定位、内存读取、窗口/存档处理、自动化和世界生成预筛。
- `src/TerrariaSplit.WinForms/`：桌面入口和 UI shell。`UI/` 负责窗口、输入、overlay、设置页和渲染；`Build/` 放主程序构建集成。
- `src/TerrariaSplit.MemoryProbe/`：独立 x86 探针工具，用 CLRMD 读取 Terraria 托管对象布局；由主程序构建目标复制/发布。
- `test/`：自定义测试工程，入口是 `Program.cs`；测试项目只默认编译 `test/*.cs`。
- `docs/`：用户和维护者文档。复杂流程或可见行为改变后，同步更新对应说明。

## 依赖方向
- `Domain` 是核心规则层，不依赖 UI、文件系统、进程 watcher、线程调度或 Windows 平台实现。
- `Configuration` 可以依赖 `Domain` 的模型；设置语义、默认值和归一化不要散落到 UI 页。
- `Application` 可以编排 `Domain`、`Configuration` 和平台无关基础设施；它表达“应该发生什么”，不直接执行窗口、文件、进程或输入副作用。
- `Storage` 和 `Statistics` 可以使用领域模型，但不要反向拥有计时状态机。
- `Terraria` 把外部进程、内存、窗口、存档和自动化细节转换成项目内稳定模型，不依赖 overlay 或主窗体实现。
- `WinForms` 是组合根和交互层，可以引用其他产品模块并执行 effect；事件处理器应保持薄。
- `MemoryProbe` 是被主程序调用的工具程序，不是可复用业务层；变更它的参数、输出 JSON 或位数时，同步检查 `WinForms/Build/MemoryProbe.targets` 和消费方。

## 数据流原则
- 外部输入先归一化成项目内命令、事件或明确数据模型，再进入核心逻辑。
- 跨线程、跨模块或给 UI 展示的数据优先使用不可变 snapshot/view state，不共享 live 领域对象。
- 应用层产出 effect 或 snapshot；UI、Terraria 集成、Storage 等外层负责执行具体副作用。
- 修改横跨多个目录的功能时，按“输入 -> 应用决策 -> 领域/runtime -> 显示/持久化/外部集成”的路径逐段检查。
- 设置变更要同时考虑默认值、归一化、序列化、当前 run 收尾、UI 刷新和持久化。

## 工作原则
- 优先沿用已有 helper、模型、测试风格和目录级规则；只有在减少真实复杂度时才新增抽象。
- 不要删除或回滚用户已有改动，除非用户明确要求。
- 不要升级 SDK、目标框架或核心依赖，除非用户明确要求。
- 不要让低层模块依赖更外层的 UI、WinForms shell 或平台实现。
- 金字塔种子预筛已经整合在 `src/TerrariaSplit.Terraria/Terraria/WorldGeneration/`；不要重新引入外层独立模拟入口。
- 世界生成预筛规则必须来自官方生成机制或可解释的局部模拟差异；不要为单个种子加入黑名单、白名单或数据集特化阈值。
- 参考 Terraria 源码 `..\reference\Terraria1456` 时，只把必要事实沉淀成项目内模型、局部复刻或小注释。

## 构建与发布约定
- `Directory.Build.props` 和 `Directory.Build.targets` 负责全局构建约定；Release/publish 默认不保留 `.pdb`。
- `src/TerrariaSplit.WinForms/Build/MemoryProbe.targets` 会构建 `src/TerrariaSplit.MemoryProbe/`，构建时复制到主程序输出子目录，发布时复制 `TerrariaSplit.MemoryProbe.exe` 到发布目录。
- 临时 metrics、trace、official probe 输出、publish 结果和构建产物不要提交；只有明确刷新基线或评估报告时才保留。

## 验证要求
- 修改业务逻辑、架构边界、配置、存储、渲染数据结构或项目引用后，运行 `dotnet run --project test\TerrariaSplit.Tests.csproj`。
- 修改 `.csproj`、`.props`、`.targets` 或发布行为后，至少运行 `dotnet build TerrariaSplit.slnx`；涉及发布输出时再运行 publish 命令并检查 `publish/`。
- 修改窗口、输入、设置应用、overlay 生命周期或用户可见布局后，运行相关 shell/rendering 测试；无法自动验证时在最终回复说明人工验证风险。
- 修改内存地址、Terraria 外部集成、菜单几何、存档处理或自动化流程后，运行完整测试，并说明需要真实 Terraria 环境验证的部分。
- 修改金字塔预筛、世界生成模拟或筛塔自动化后，至少运行聚焦测试；涉及 FP/FN 风险时再运行 `pyramid-metrics` 数据集评估。
- 仅修改文档时通常不需要跑测试，但最终回复要说明没有运行测试的原因。
