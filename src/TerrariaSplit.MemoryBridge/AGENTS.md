# MemoryBridge 内存控制单元

## 边界
- 统一的 `win-x86` 内存控制单元，集中承载必须与 32 位 Terraria/CLR 同位数的 CLRMD 探测、受控内存读取和 Payload/Bootstrap 注入启动。
- 只读命令包括 `runtime-layout <pid>` 和 `random-seed-batch <pid> <count>`，通过标准输出返回结构化 JSON；线协议统一定义在 `Protocol/MemoryBridgeProtocol.cs`，由消费方链接同一份源码。
- 控制命令包括 `inject <pid> <bootstrap dll> <command>`；它只负责校验参数、建立命名 IPC、注入受信任的随包 bootstrap 并等待结构化结果，不拥有竞速/计时业务决策。
- 所有命令均为一次性调用；长期 watcher、UI 自动化、重试策略和业务状态仍由消费方拥有。

## 约束
- 修改命令、参数、退出码、IPC/JSON 字段、注入权限或位数时，同步 `WinForms/Build/MemoryBridge.targets`、Payload/Bootstrap、Memory 消费方和测试。
- 探测命令的普通失败返回结构化错误与非零退出码，不把标准输出改成人类日志；注入命令的人类诊断只写标准错误。
- 注入目标、bootstrap 路径与命令必须由消费方显式提供并校验；不得在本进程引入通用脚本执行、任意下载或长期驻留能力。
- 发布验证必须确认根目录包含单文件 `TerrariaSplit.MemoryBridge.exe`，`Runtime/MemoryBridge/` 包含 Payload 与 Bootstrap，且发布目录没有 `.pdb`。
