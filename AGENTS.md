# Agent 工作说明

## 基础说明
- 本文件适用于整个仓库；进入子目录前先检查更近的 `AGENTS.md`，内层规则优先。
- 本项目是 C# / .NET 10 的 Terraria 分段计时器，主程序是 WinForms 桌面应用。
- 该项目处于开发阶段，除非明确要求否则永远不要考虑兼容性，时刻牢记删除无用的代码，并保证现有的代码是结构清晰的。

## 常用命令
- 构建解决方案：`dotnet build TerrariaSplit.slnx`
- 运行主程序：`dotnet run --project src\TerrariaSplit.WinForms\TerrariaSplit.WinForms.csproj`
- 运行测试：`dotnet run --project test\TerrariaSplit.Tests.csproj`
- 发布主程序：`dotnet publish src\TerrariaSplit.WinForms\TerrariaSplit.WinForms.csproj -c Release -o publish`
- 跳过探针构建：给构建或发布命令追加 `-p:TerrariaSplitSkipMemoryProbe=true`
- 金字塔预筛聚焦测试：`$env:TERRARIA_SPLIT_TEST_FILTER='Pyramid seed pre-screen'; dotnet run --project test\TerrariaSplit.Tests.csproj`
- 金字塔预筛数据集评估：`dotnet run -c Release --project test\TerrariaSplit.Tests.csproj -- pyramid-metrics <world-folder> --csv test\Results\Metrics\metrics-current-release.csv`

## 构建与测试纪律
- 验证强度按风险和影响面决定，默认选择能证明本次改动的最小验证，并不需要总是进行验证；只有跨层行为、发布边界或高风险运行路径变化时才升级到完整测试或完整构建。
- 构建/测试失败时先判断是代码问题还是环境问题。若是权限、文件占用、沙箱或正在运行程序导致的失败，不要在相同条件下重复执行；应改用更小范围、临时输出、提权或请用户释放占用。
- 构建或测试被中断、超时、失败后，检查并清理确认属于本次构建/测试的残留进程；不要误杀用户正在运行的程序或其他任务。
- 测试过程临时文件统一放在 `test/Temp/`，需要保留的测试结果统一放在 `test/Results/`，不要在其他地方创建。

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
- `test/`：自定义测试工程，入口是 `test/Code/Program.cs`；根目录只保留 `TerrariaSplit.Tests.csproj` 和 `AGENTS.md`，测试代码放在 `test/Code/`，过程临时文件放在 `test/Temp/`，可保留测试结果放在 `test/Results/`。
- `docs/`：用户和维护者文档。复杂流程或可见行为改变后，同步更新对应说明。

## 依赖方向
- `Domain` 是核心规则层，不依赖 UI、文件系统、进程 watcher、线程调度或 Windows 平台实现。
- `Configuration` 可以依赖 `Domain` 的模型；设置语义、默认值和归一化不要散落到 UI 页。
- `Application` 可以编排 `Domain`、`Configuration` 和平台无关基础设施；它表达“应该发生什么”，不直接执行窗口、文件、进程或输入副作用。
- `Storage` 和 `Statistics` 可以使用领域模型，但不要反向拥有计时状态机。
- `Terraria` 把外部进程、内存、窗口、存档和自动化细节转换成项目内稳定模型，不依赖 overlay 或主窗体实现。
- `WinForms` 是组合根和交互层，可以引用其他产品模块并执行 effect；事件处理器应保持薄。
- `MemoryProbe` 是被主程序调用的工具程序，不是可复用业务层；变更它的参数、输出 JSON 或位数时，同步检查 `WinForms/Build/MemoryProbe.targets` 和消费方。

## 工作原则
- 外部输入先归一化成项目内命令、事件或明确数据模型，再进入核心逻辑。
- 跨线程、跨模块或给 UI 展示的数据优先使用不可变 snapshot/view state，不共享 live 领域对象。
- 应用层产出 effect 或 snapshot；UI、Terraria 集成、Storage 等外层负责执行具体副作用。
- 修改横跨多个目录的功能时，按“输入 -> 应用决策 -> 领域/runtime -> 显示/持久化/外部集成”的路径逐段检查。
- 设置变更要同时考虑默认值、归一化、序列化、当前 run 收尾、UI 刷新和持久化。
- 开发时先理解现有信息流和项目引用方向，再改代码；不要为局部任务引入第二套并行架构。
- 优先沿用已有 helper、模型、测试风格和目录级规则；只有在减少真实复杂度时才新增抽象。
- 必要时参考 Terraria 源码 `..\reference\Terraria1456` 。
