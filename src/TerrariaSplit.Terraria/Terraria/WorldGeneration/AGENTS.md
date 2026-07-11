# WorldGeneration

## 边界
- 负责金字塔预筛模型、范围判断、结果整理和模拟入口；独立 `TerrariaSplit.WorldGeneration` 项目只提供外部 façade。
- 不执行窗口点击、文件删除或 UI 展示。

## 约束
- 当前支持范围是小世界、猩红、无特殊种子；其他范围明确返回 unsupported。
- 预测状态、金字塔、物品摘要和耗时由统一结果表达，不让调用方重新推断。
- 筛选规则必须能追溯到官方机制或解析错误；数据集指标只用于验证。
- 逻辑改动先运行 Pyramid seed pre-screen 聚焦测试；影响 FP/FN 或性能时再运行 Release metrics。
