# MemoryBridge 局部约束

- 仅承载 `win-x86` 一次性命令（`runtime-layout`、`random-seed-batch`、`inject`），不常驻、不持有计时或竞速业务状态。
- 线协议定义在 `Protocol/MemoryBridgeProtocol.cs`（源码链接共享）；变更命令、JSON、IPC、退出码或权限时同步 `MemoryBridge.targets`、`Payload`/`Bootstrap`、消费方和测试。
- 探测命令失败返回结构化 JSON 错误与非零退出码，不向 stdout 输出人类日志；注入诊断仅写 stderr。
- 发布目录根目录必须为单文件 `TerrariaSplit.MemoryBridge.exe`，`Runtime/MemoryBridge/` 包含 Payload、Bootstrap 与清单，且无 `.pdb`。
