# Agent 工作说明

## 项目定位
- 本项目是 C# WinForms / .NET 10 的 Terraria 分段计时器，主工程在 `TerrariaSplit/`，测试工程在 `test/`。
- 开发时先理解现有信息流和模块边界，再改代码；不要为了局部任务引入另一套并行架构。
- 可以参考 Terraria 源码 `..\reference\Terraria1456`，但只把必要事实沉淀成项目内模型或小注释。

## 常用命令
- 运行测试：`dotnet run --project test\TerrariaSplit.Tests.csproj`
- 构建解决方案：`dotnet build TerrariaSplit.slnx`
- 运行主程序：`dotnet run --project TerrariaSplit\TerrariaSplit.csproj`

## 目录职责
- `TerrariaSplit/Application/`：应用编排、命令/effect/snapshot 合约、runtime 协调。
- `TerrariaSplit/Domain/`：计时、boss split、路线和比较等纯领域逻辑。
- `TerrariaSplit/UI/`：WinForms shell、输入适配、窗口/overlay 生命周期和 shell 副作用。
- `TerrariaSplit/UI/Rendering/`：overlay 绘制模型、布局和渲染器。
- `TerrariaSplit/Terraria/`：Terraria 进程、内存、窗口、存档和自动化集成。
- `TerrariaSplit/Configuration/`：设置模型、默认值、序列化、profile 和归一化。
- `TerrariaSplit/Storage/`：运行统计和 split time 持久化。

## 工作原则
- 外部输入进入核心逻辑前应先归一化成项目内命令或明确的数据模型。
- 跨线程、跨模块或给 UI 展示的数据优先使用不可变快照，不共享可变领域对象。
- 平台副作用放在外层适配器或 shell 中执行；应用层表达意图，领域层表达规则。
- 修改横跨多个目录的功能时，按“输入 -> 应用决策 -> 领域/runtime -> 显示/持久化”的路径逐段检查。
- 优先沿用已有 helper、模型和测试风格；只有在减少真实复杂度时才新增抽象。

## 禁止事项
- 不要删除或回滚用户已有改动，除非用户明确要求。
- 不要升级 SDK、目标框架或核心依赖，除非用户明确要求。
- 不要让低层模块依赖更外层的 UI 或平台实现。

## 验证要求
- 修改业务逻辑、架构边界、配置、存储或渲染数据结构后，运行 `dotnet run --project test\TerrariaSplit.Tests.csproj`。
- 修改外部集成或窗口行为后，至少运行相关测试；如果无法自动验证，在最终回复说明人工验证风险。
- 如果发现本文件规则与代码现状不符，优先修正规则或指出过期点，不要盲目服从旧规则。
