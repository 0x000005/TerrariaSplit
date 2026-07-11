# MemoryProbe

## 边界
- 独立 `win-x86` / x86 CLRMD 探针，通过标准输出返回结构化 JSON。
- 命令行接口为 `runtime-layout <pid>`；不承担计时、UI、自动化或长期 watcher 状态。

## 约束
- 修改参数、退出码、JSON 字段或位数时，同步 `WinForms/Build/MemoryProbe.targets`、消费方和测试。
- 普通失败返回结构化错误与非零退出码，不把标准输出改成人类日志。
- 发布验证必须确认包含 `TerrariaSplit.MemoryProbe.exe` 且没有 `.pdb`。
