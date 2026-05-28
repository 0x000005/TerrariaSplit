# Domain 层说明

## 职责
- 这里放项目的纯规则：计时、boss split、路线分组、时间格式、成绩比较和 split 状态推进。
- Domain 代码应尽量脱离 UI、文件系统、进程 watcher 和线程调度独立理解、独立测试。

## 设计原则
- 领域逻辑优先使用明确输入和返回值，避免隐藏读取当前环境状态。
- 涉及时间的逻辑优先接受显式 timestamp 或 elapsed time，方便测试和重放。
- 状态推进要保持可预测；同一输入序列不应因为 UI 调度或机器环境不同而改变结果。
- 领域对象可以是可变状态机，但跨层传递时应由 Application 转成 snapshot。
- 新增 boss 或路线规则时，用项目内统一模型表达，不把 Terraria 源码细节散落在调用方。

## 验证
- 修改 timer、tracker、route 或 split 判定后，运行 `dotnet run --project test\TerrariaSplit.Tests.csproj`。
- 新增规则时至少覆盖正常完成、跳过/缺失信息、边界时间三类场景中相关的部分。
