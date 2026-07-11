# Agent 工作说明

## 项目
- 本文件适用于整个仓库；更近的 `AGENTS.md` 只补充目录特有约束。
- 项目是 C# / .NET 10 的 Terraria 分段计时器，主程序为 WinForms。
- 项目处于开发阶段；除非明确要求，不维护旧接口或旧配置兼容，及时删除无用代码。

## PowerShell 与构建
- 所有命令使用 `pwsh`，脚本首行设置 `$ErrorActionPreference = 'Stop'`；文件读写显式使用 `-Encoding UTF8`。
- 使用 PowerShell 语法和对象管道，不混用 Bash、`cmd.exe` 或旧版 `powershell.exe` 规则。
- `restore` 与后续阶段分开；已还原时使用 `--no-restore -m:1 -p:UseSharedCompilation=false`，测试另加 `-p:BuildInParallel=false`。
- 出现“0 错误但失败”或大量 `dotnet` 子进程时立即停止，只清理确认属于本次时间窗的残留进程，再以单节点重试。
- 验证强度按风险决定；临时文件只放 `test/Temp/`，需保留的结果放 `test/Results/`。

## 模块边界
- `Domain`：纯计时、路线、条件、比较规则；不依赖 UI、文件系统、进程或调度。
- `Configuration`：当前设置 schema、默认值、归一化、序列化和语言资源。
- `Application`：command、effect、event、snapshot 与 runtime 编排；不执行平台副作用。
- `Infrastructure` / `Infrastructure.Windows`：通用基础设施与 Windows 平台封装。
- `Storage` / `Statistics`：用户数据持久化与统计展示模型，不拥有计时状态机。
- `Terraria`：进程、内存、窗口、存档、自动化及世界生成模拟；不依赖 UI shell。
- `WorldGeneration`：对外 façade；核心模拟仍位于 `Terraria/Terraria/WorldGeneration/`。
- `Race.Contracts` / `Race.Client` / `Race.Server`：联机协议、客户端会话与服务器状态。
- `WinForms`：组合根、交互、overlay、设置和程序更新；页面事件保持薄，副作用走 shell、host 或专用执行器。
- `MemoryProbe`：独立 x86 CLRMD 探针；参数、JSON 或位数变化要同步消费方和构建目标。
- `test`：自定义测试宿主；`docs`：用户与维护者文档。

## 工作原则
- 外部输入先转换为项目内命令、事件或稳定模型；跨线程和跨层数据使用不可变 snapshot/view state。
- 应用层表达意图，WinForms、Terraria、Storage 等外层执行副作用；不要为局部功能引入第二套架构。
- 设置变更同时检查默认值、归一化、保存、当前 run 收尾和 UI 刷新。
- 优先复用现有 helper、模型和测试风格；必要时参考 `..\reference\Terraria1456`。

## 常用命令
- 还原：`dotnet restore TerrariaSplit.slnx -m:1`
- 构建：`dotnet build TerrariaSplit.slnx --no-restore -m:1 -p:UseSharedCompilation=false`
- 测试：`dotnet run --project test\TerrariaSplit.Tests.csproj --no-restore -p:BuildInParallel=false -p:UseSharedCompilation=false`
- 运行：`dotnet run --project src\TerrariaSplit.WinForms\TerrariaSplit.WinForms.csproj --no-restore -p:BuildInParallel=false -p:UseSharedCompilation=false`
- 发布前先执行 `dotnet restore src\TerrariaSplit.WinForms\TerrariaSplit.WinForms.csproj -r win-x64 -m:1`，再单节点 `publish --no-restore`。
- 跳过探针构建：追加 `-p:TerrariaSplitSkipMemoryProbe=true`。
- 金字塔聚焦测试：设置 `$env:TERRARIA_SPLIT_TEST_FILTER='Pyramid seed pre-screen'` 后运行测试命令。
- metrics/trace：`dotnet run --project test\TerrariaSplit.Diagnostics.csproj -- <命令及参数>`。
