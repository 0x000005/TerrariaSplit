# Tests

## 结构
- 自定义测试入口是 `Suites/Program.cs`；每个顶层测试应覆盖一条完整行为链，而不是一个补丁或字段。
- 临时文件放 `Temp/`，生成结果放 `Results/`，小型且与机器无关的长期基准放 `Baselines/`；不要创建其他测试输出目录。
- metrics/trace 使用独立的 `TerrariaSplit.Diagnostics.csproj`；`Probes/` 也不属于默认测试。
- 依赖原生 WorldFilter 和 Terraria 安装的测试属于 `Native` 套件；通过 `TERRARIA_SPLIT_TEST_SUITE=Native` 显式运行，依赖缺失必须失败，不能静默通过。

## 约束
- 测试保持 deterministic，使用 fake、显式 timestamp 和小 fixture；失败信息指出具体行为差异。
- 优先合并同一状态机或用户旅程的输入矩阵；不要用源码文本、私有字段或临时文件名锁死实现。
- 架构测试约束稳定边界，不绑定临时文件名或重构细节。
- 数据集按世界顺序读取，不并行；metrics/trace 只由显式命令触发。
- 默认先跑聚焦测试，影响跨层行为、发布边界或高风险路径时再跑完整套件。
