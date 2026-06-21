# Agent 工作说明

## 项目定位
- 本项目是 C# WinForms / .NET 10 的 Terraria 分段计时器，主工程在 `src/TerrariaSplit.WinForms/`，测试工程在 `test/`，发布输出默认写到 `publish/`。
- 开发时先理解现有信息流和模块边界，再改代码；不要为了局部任务引入另一套并行架构。
- 可以参考 Terraria 源码 `..\reference\Terraria1456`，但只把必要事实沉淀成项目内模型、局部复刻或小注释。
- 金字塔种子预筛已经整合在 `src/TerrariaSplit.Terraria/Terraria/WorldGeneration/` 内；外层独立 `WorldGenSim/` 历史项目已移除，不要重新引入第二套模拟入口。

## 常用命令
- 运行测试：`dotnet run --project test\TerrariaSplit.Tests.csproj`
- 构建解决方案：`dotnet build TerrariaSplit.slnx`
- 运行主程序：`dotnet run --project src\TerrariaSplit.WinForms\TerrariaSplit.WinForms.csproj`
- 发布主程序：`dotnet publish src\TerrariaSplit.WinForms\TerrariaSplit.WinForms.csproj -c Release -o publish`
- 金字塔预筛聚焦测试：`$env:TERRARIA_SPLIT_TEST_FILTER='Pyramid seed pre-screen'; dotnet run --project test\TerrariaSplit.Tests.csproj`
- 金字塔预筛数据集评估：`dotnet run -c Release --project test\TerrariaSplit.Tests.csproj -- pyramid-metrics <world-folder> --csv test\metrics-current-release.csv`

## 目录职责
- `src/TerrariaSplit.Application/Application/`：应用编排、命令/effect/snapshot 合约、runtime 协调。
- `src/TerrariaSplit.Domain/Domain/`：计时、boss split、路线和比较等纯领域逻辑。
- `src/TerrariaSplit.Domain/Models/`：跨层共享的简单记录、定义和 run/boss 数据模型。
- `src/TerrariaSplit.WinForms/UI/`：WinForms shell、输入适配、窗口/overlay 生命周期和 shell 副作用。
- `src/TerrariaSplit.WinForms/UI/Settings/`：设置窗口的分页面 UI、控件工厂、页面生命周期和设置编辑适配。
- `src/TerrariaSplit.WinForms/UI/Rendering/`：overlay 绘制模型、布局和渲染器。
- `src/TerrariaSplit.Terraria/Terraria/`：Terraria 进程、内存、窗口、存档和自动化集成。
- `src/TerrariaSplit.Terraria/Terraria/Process/`：Terraria 客户端/服务器进程定位。
- `src/TerrariaSplit.Terraria/Terraria/WorldGeneration/`：金字塔种子预筛、世界生成局部复刻、金字塔/箱子结果模型。
- `src/TerrariaSplit.Terraria/Terraria/Automation/`：自动建人物/建世界/进世界流程和筛塔自动化编排。
- `src/TerrariaSplit.Terraria/Terraria/Memory/`：Terraria 进程内存读取、签名扫描、世界生成状态和创图种子读取。
- `src/TerrariaSplit.Configuration/Configuration/`：设置模型、默认值、序列化、profile 和归一化。
- `src/TerrariaSplit.Storage/Storage/`：运行统计和 split time 持久化。
- `src/TerrariaSplit.Statistics/Statistics/`：统计表格和展示行构造，不负责持久化。
- `src/TerrariaSplit.Configuration/Localization/`：中英文 UI 字符串、语言名和 Terraria 语言代码映射。
- `src/TerrariaSplit.Infrastructure/Infrastructure/`：JSON 文件 helper、日志、性能诊断和高精度调度。
- `src/TerrariaSplit.Infrastructure.Windows/Infrastructure/`：layered window 等 Windows 平台封装。
- `src/TerrariaSplit.WinForms/Assets/`：随程序复制的图标和 Terraria 世界名资源。
- `docs/`：用户和维护者参考文档；复杂流程变更后同步更新对应说明。

## 工作原则
- 外部输入进入核心逻辑前应先归一化成项目内命令或明确的数据模型。
- 跨线程、跨模块或给 UI 展示的数据优先使用不可变快照，不共享可变领域对象。
- 平台副作用放在外层适配器或 shell 中执行；应用层表达意图，领域层表达规则。
- 修改横跨多个目录的功能时，按“输入 -> 应用决策 -> 领域/runtime -> 显示/持久化”的路径逐段检查。
- 优先沿用已有 helper、模型和测试风格；只有在减少真实复杂度时才新增抽象。
- 进入已有子目录前先检查该目录或父目录的 `AGENTS.md`；内层规则比根规则更贴近具体边界。
- 世界生成预筛的规则来源必须是官方生成机制或可解释的局部模拟差异；数据集指标只能作为验证和诊断证据。
- 临时 metrics、trace、official probe 输出不要随手留下；只有明确更新基线或当前评估结果时才提交 CSV/报告。

## 禁止事项
- 不要删除或回滚用户已有改动，除非用户明确要求。
- 不要升级 SDK、目标框架或核心依赖，除非用户明确要求。
- 不要让低层模块依赖更外层的 UI 或平台实现。
- 不要为修单个种子加入黑名单/白名单、数据集特化阈值或无法追溯到官方机制的规则。

## 验证要求
- 修改业务逻辑、架构边界、配置、存储或渲染数据结构后，运行 `dotnet run --project test\TerrariaSplit.Tests.csproj`。
- 修改外部集成或窗口行为后，至少运行相关测试；如果无法自动验证，在最终回复说明人工验证风险。
- 修改金字塔预筛、世界生成模拟或筛塔自动化后，至少运行聚焦测试；涉及 FP/FN 风险时再运行 `pyramid-metrics` 数据集评估。
- 如果发现本文件规则与代码现状不符，优先修正规则或指出过期点，不要盲目服从旧规则。
