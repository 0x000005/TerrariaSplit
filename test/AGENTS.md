# 测试层说明

## 职责
- 本目录是自定义测试工程，入口在 `Program.cs`，通过 `dotnet run --project test\TerrariaSplit.Tests.csproj` 执行。
- 测试应帮助维护模块边界、核心行为和外部集成适配的可预测性。

## 编码原则
- 测试保持 deterministic，优先使用 fake watcher、fake patch applier、显式 timestamp 和小型 fixture。
- 架构边界测试应检查稳定原则，例如单一入口、快照传递、外部失败可恢复，而不是只绑定某次重构的旧名字。
- 失败信息要指向具体行为差异，避免只抛出模糊异常。

## 覆盖重点
- 输入如何变成应用命令。
- 应用层如何产出状态和副作用意图。
- runtime 状态机在明确 snapshot 和 timestamp 下如何推进。
- shell 和 rendering 是否只消费快照和上下文。
- 配置、存储和 Terraria 集成是否能处理缺失、非法或外部失败场景。
