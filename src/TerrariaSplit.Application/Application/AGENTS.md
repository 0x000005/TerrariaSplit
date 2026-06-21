# Application 层说明

## 职责
- 这里是应用编排层，负责把外部请求、领域状态、runtime 事件和 UI 展示需求连接起来。
- 这里定义项目内稳定合约，例如 command、effect、event、snapshot 和 view state。
- Application 层可以决定“下一步应该发生什么”，但不应该亲自完成平台副作用。

## 设计原则
- 输入用命令表达，输出用 effect、event 或 snapshot 表达。
- 应用决策要便于测试：给定命令或 watcher 通知，应能断言产生的状态和意图。
- runtime 与 UI 之间通过明确合约传递信息，避免任何一边直接持有另一边的可变对象。
- 异步 watcher 或后台 runtime 回来的状态必须考虑顺序、过期和幂等问题。
- 设置变更要同时考虑当前 run 的收尾、定义重载、持久化和 UI 刷新。

## 修改指引
- 新增跨层功能时，先确定它属于用户输入、应用决策、领域规则、runtime 状态还是 shell 副作用。
- 新增命令或 effect 时，同步检查处理入口、测试覆盖和调用方是否仍只有一个清晰路径。
- 暴露给 UI 的状态优先做成只读 snapshot；不要把领域对象本体当作显示模型传出去。
- runtime tick 行为尽量可重放，测试中优先使用显式 timestamp 和小型 snapshot。

## 验证
- 修改应用命令、effect、runtime event 或 watcher 协调后，补充或更新 `test/HotkeyTests.cs`、`test/MainShellRefactorTests.cs` 等相关测试。
- 修改计时推进逻辑时，增加覆盖开始、暂停、完成、重置和延迟动作时机的用例。
