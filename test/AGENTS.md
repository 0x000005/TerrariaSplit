# 测试层说明

## 职责
- 本目录是自定义测试工程，入口在 `Code/Program.cs`，通过 `dotnet run --project test\TerrariaSplit.Tests.csproj` 执行。
- 根目录只保留 `TerrariaSplit.Tests.csproj` 和 `AGENTS.md`；测试代码放在 `Code/`，测试过程临时文件放在 `Temp/`，可保留测试结果放在 `Results/`。
- 不要再创建 `test-output/`、`.verify/`、`.codex-*/` 或仓库根目录 `artifacts/verify/`；构建中间产物、测试输出、临时 probe 输出都进 `Temp/`。
- 测试项目编译 `Code/*.cs` 和 `Code/Diagnostics/*.cs`；更深子目录默认不参与编译。
- 测试应帮助维护模块边界、核心行为和外部集成适配的可预测性。
- `Code/Diagnostics/PyramidPreScreenMetrics.cs` 和 `Code/Diagnostics/PyramidPreScreenTrace.cs` 是显式命令触发的诊断/评估工具；`Results/Metrics/` 放已提交的评估 CSV；`Code/OfficialProbe/` 是官方流程对照工具，不属于主程序运行路径。

## 编码原则
- 测试保持 deterministic，优先使用 fake watcher、fake patch applier、显式 timestamp 和小型 fixture。
- 架构边界测试应检查稳定原则，例如单一入口、快照传递、外部失败可恢复，而不是只绑定某次重构的旧名字。
- 失败信息要指向具体行为差异，避免只抛出模糊异常。
- 数据集评估输出可以用于比较 FP/FN/耗时，但不能单独作为筛选规则来源。
- 临时 CSV、trace 和 probe 输出不要提交；只有明确刷新基线或当前指标文件时才保留。
- 读取真实世界文件的数据集评估按一个世界一个世界处理，不要为了提速并行读取 `.wld`。
- `pyramid-metrics` 增加诊断输出时，保持默认测试入口轻量；耗时数据集评估应通过显式命令触发。

## 覆盖重点
- 输入如何变成应用命令。
- 应用层如何产出状态和副作用意图。
- runtime 状态机在明确 snapshot 和 timestamp 下如何推进。
- shell 和 rendering 是否只消费快照和上下文。
- 配置、存储和 Terraria 集成是否能处理缺失、非法或外部失败场景。
- 金字塔预筛要分别覆盖 scope gating、随机种子读取失败、预测结果、二验兜底和物品过滤语义。
