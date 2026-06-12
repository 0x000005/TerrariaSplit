# Infrastructure 说明

## 职责
- 本目录放跨层基础设施：结构化 JSON 文件读写、日志、高精度调度、性能诊断和 layered window 平台封装。
- 这里提供可复用能力，不承载 run 规则、设置语义、Terraria 业务判断或 UI 页面流程。

## 设计原则
- 平台 API 封装要把失败路径表达清楚；能返回 `null`、`false` 或诊断时，不要让普通环境差异直接崩溃主流程。
- 计时和调度代码要保持可停止、可释放、可观测；线程回调不要直接操作 WinForms 控件。
- 文件 helper 保持通用，具体路径、默认值和迁移策略由 Configuration 或 Storage 决定。
- layered window 和 GDI 相关代码要明确资源所有权，避免把绘制规则写进平台更新器。
- 性能诊断只记录和汇总事实，不反向驱动业务状态。

## 验证
- 修改调度器、waitable timer、timer period 或性能计数后，运行 `HighPrecisionSchedulerTests` 和相关 overlay/runtime 测试。
- 修改 JSON 存储 helper 或日志行为后，运行涉及配置、存储和设置加载的测试。
