# Tests 局部约束

- 自定义测试宿主入口为 `Suites/Program.cs`；顶层测试应覆盖完整行为链而非锁死私有字段或源码文本。
- 临时文件仅写 `Temp/`，生成指标写 `Results/`，机器无关基准放 `Baselines/`；`Diagnostics/` 与 `Probes/` 不属于默认测试。
- 依赖原生 DLL 或本机 Terraria 的用例归入 `Native` 套件（`TERRARIA_SPLIT_TEST_SUITE=Native`），缺依赖必须失败，不得静默通过。
- 测试须保持确定性（使用 fake、显式时间与小 fixture）；架构边界测试仅约束稳定模块边界。
