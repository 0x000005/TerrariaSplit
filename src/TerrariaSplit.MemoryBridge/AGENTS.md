# MemoryBridge

## 边界
- 独立 `win-x86` / x86 CLRMD 探针，通过标准输出返回结构化 JSON。
- 命令行接口为 `runtime-layout <pid>`、`visible-seed <pid>`、`random-seed-batch <pid> <count>`；只提供一次性只读探针，不承担计时、UI 自动化或长期 watcher 状态。

## 约束
- 修改参数、退出码、JSON 字段或位数时，同步 `WinForms/Build/MemoryBridge.targets`、消费方和测试。
- 普通失败返回结构化错误与非零退出码，不把标准输出改成人类日志。
- 发布验证必须确认根目录包含单文件 `TerrariaSplit.MemoryBridge.exe` 且没有 `.pdb`。
