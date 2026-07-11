# WorldGeneration Simulation

## 约束
- 保持官方 RNG、pass 顺序和候选判定；裁剪 pass 前确认不会影响后续相关状态。
- 优化优先采用局部区域、稀疏 tile 和提前停止，不引入第二套 tile/RNG 模型。
- 不为单个 seed 增加名单或数据集专用阈值，不随意并行化 RNG 或相邻 tile 写入。
- FP/FN 修复记录错误类别、涉及 pass、修改前后指标和耗时；官方事实只保留短注释或维护文档。
- 修改 pass、tile runner、state grid 或扫描逻辑后先跑聚焦测试；行为或性能变化再跑 Release metrics。
