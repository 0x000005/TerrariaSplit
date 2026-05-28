# Storage 层说明

## 职责
- 这里负责运行统计、split time set 等用户数据的持久化。
- Storage 层应关注数据格式、读写可靠性和兼容读取，不承载 UI 或业务流程决策。

## 设计原则
- 优先使用现有结构化存储 helper，不手写脆弱字符串拼接。
- 写入用户数据时优先保证原子性或可恢复性，避免中途失败造成文件损坏。
- 持久化模型变更必须考虑旧数据读取、缺失字段和默认值。
- Storage 不依赖 WinForms、overlay、watcher、自动化或热键输入。

## 验证
- 修改统计或 split time 存储后，运行 `dotnet run --project test\TerrariaSplit.Tests.csproj`。
- 涉及用户数据写入时，优先添加兼容读取或失败路径测试。
