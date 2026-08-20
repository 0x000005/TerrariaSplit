# Agent 工作说明

## 构建
- `restore` 与后续阶段分开；日常开发只构建目标项目并复用增量缓存，提交前或发布验证才使用 `-m:1 -p:UseSharedCompilation=false`。
- 出现“0 错误但失败”或大量 `dotnet` 子进程时立即停止，只清理确认属于本次时间窗的残留进程，再以单节点重试。
- SDK 中间输出统一位于仓库根 `.build/`；最终发布目录统一位于 `publish/产品名-v版本-平台/`；测试运行时临时文件只放 `test/Temp/`，需保留的结果放 `test/Results/`。

## 模块边界
- `Domain`：纯计时、路线、条件、比较规则；不依赖 UI、文件系统、进程或调度。
- `Configuration`：当前设置 schema、默认值、归一化、序列化和语言资源。
- `Application`：command、effect、event、snapshot 与 runtime 编排；不执行平台副作用。
- `Infrastructure` / `Infrastructure.Windows`：通用基础设施与 Windows 平台封装。
- `Storage` / `Statistics`：用户数据持久化与统计展示模型，不拥有计时状态机。
- `Terraria`：进程、内存、窗口、存档、自动化及世界生成模拟；不依赖 UI shell。
- `Race.Contracts` / `Race.Client` / `Race.Server`：联机协议、客户端会话与服务器状态；客户端不依赖 UI 或平台日志实现。
- `Race.Determinism`：联机确定性算法与共享协议常量；保持可跨目标框架复用。
- `Race.InGame`：游戏内联机消息与状态模型；不依赖 WinForms shell。
- `WinForms`：组合根、交互、overlay、设置和程序更新；页面事件保持薄，副作用走 shell、host 或专用执行器。
- `MemoryBridge`：统一的 x86 内存控制单元；控制进程负责 CLRMD 探测、受控内存读取与注入启动，内部 Payload/Bootstrap 负责 Terraria 进程内规则。参数、协议、权限、JSON、导出符号或位数变化要同步消费方、测试和构建目标。
- `test`：自定义测试宿主；`docs`：用户与维护者文档。

## 工作原则
- 外部输入先转换为项目内命令、事件或稳定模型；跨线程和跨层数据使用不可变 snapshot/view state。
- 应用层表达意图，WinForms、Terraria、Storage 等外层执行副作用；不要为局部功能引入第二套架构。
- 设置变更同时检查默认值、归一化、保存、当前 run 收尾和 UI 刷新。
- 优先复用现有 helper、模型和测试风格；必要时参考 `..\reference\Terraria1457`。

## 常用命令
- 还原：`dotnet restore TerrariaSplit.slnx -m:1`
- 日常构建：`dotnet build src\TerrariaSplit.WinForms\TerrariaSplit.WinForms.csproj --no-restore`
- 全量构建：`dotnet build TerrariaSplit.slnx --no-restore -m:1 -p:UseSharedCompilation=false`
- 测试：全量构建后运行 `dotnet run --project test\TerrariaSplit.Tests.csproj --no-build`
- 运行：`dotnet run --project src\TerrariaSplit.WinForms\TerrariaSplit.WinForms.csproj --no-build`
- 完整发布：`pwsh -NoProfile -File eng\Publish-Release.ps1`；客户端与 win-x64/linux-x64 Server 会写入带统一版本号的 `publish/` 子目录，只生成目录，不自动压缩。
- 跳过内存控制单元构建：追加 `-p:TerrariaSplitSkipMemoryBridge=true`。
- 金字塔聚焦测试：设置 `$env:TERRARIA_SPLIT_TEST_FILTER='Pyramid seed pre-screen'` 后运行测试命令。
- metrics/trace：`dotnet run --project test\TerrariaSplit.Diagnostics.csproj --no-restore -- <命令及参数>`。
