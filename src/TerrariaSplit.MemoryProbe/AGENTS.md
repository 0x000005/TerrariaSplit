# MemoryProbe 说明

## 职责
- 本项目是独立 x86 辅助进程，用 `Microsoft.Diagnostics.Runtime` 探测 Terraria 托管对象布局。
- 它通过标准输出返回 JSON 响应，供主程序消费；不要把标准输出改成面向人的日志格式。
- 主程序通过 `src/TerrariaSplit.WinForms/Build/MemoryProbe.targets` 构建、复制和发布该工具。

## 边界
- 保持 `win-x86` / `PlatformTarget=x86` 语义；Terraria 目标进程和对象引用大小假设依赖这一点。
- 命令行接口当前是 `item-layout <pid>`；修改参数、退出码或 JSON 字段时，同步更新调用方和测试。
- 这里只负责探测布局并返回数据或错误，不负责决定计时、UI 展示、自动化流程或长期 watcher 状态。
- 普通失败应写入结构化响应并返回非零退出码；不要让调用方只能靠崩溃文本推断失败原因。
- 除非确实需要解决位数或 CLRMD 隔离问题，不要把 `TerrariaSplit.Terraria/Terraria/Memory/` 的普通读取逻辑搬进这里。

## 验证
- 单独构建探针：`dotnet build src\TerrariaSplit.MemoryProbe\TerrariaSplit.MemoryProbe.csproj`
- 验证主程序集成：`dotnet build src\TerrariaSplit.WinForms\TerrariaSplit.WinForms.csproj`
- 验证发布包含探针且没有 `.pdb`：`dotnet publish src\TerrariaSplit.WinForms\TerrariaSplit.WinForms.csproj -c Release -o publish`
- 需要真实 Terraria 进程时，手动运行：`dotnet run --project src\TerrariaSplit.MemoryProbe\TerrariaSplit.MemoryProbe.csproj -- item-layout <pid>`
