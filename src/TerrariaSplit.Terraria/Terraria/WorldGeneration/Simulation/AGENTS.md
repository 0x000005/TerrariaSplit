# Simulation 说明

## 职责
- 本目录是已整合进主工程的 Terraria 世界生成局部复刻，用于金字塔预筛。
- `StageOneReplicaSimulator` 和 `OfficialPassPlan` 维护 pass 顺序；各 `*PassReplica` 维护对应官方 pass 的必要地形影响。

## 设计原则
- 优先保持官方 RNG 顺序、pass 顺序和候选点判定；跳过或裁剪 pass 时，要确认不会影响后续金字塔相关状态，或明确记录风险。
- 性能优化优先来自局部区域、稀疏/惰性 tile、少建无关图格和提前停止；不要重新引入外层独立模拟器。
- 不为单个 seed 加黑名单/白名单，不加入只能解释当前数据集的 magic threshold。
- 如果 FP/FN 来自官方 pass 缺失，优先补该 pass 的候选列或候选附近局部效果；完整慢模拟只能作为最后手段。
- 不在 pass 内随意并行化 tile 写入或 RNG 消耗；只有证明无 RNG 顺序依赖、无相邻写入依赖或已固定 RNG 序列时才考虑。
- 复用现有 `WorldGenState`、`WorldGenTileRunner`、`UnifiedRandom`、tile id 和 wall id 模型，不新增第二套 tile/RNG 表达。
- 官方源码事实可以写成短注释或维护文档；避免整段源码搬运。

## 验证
- 改动 pass、tile runner、state grid 或扫描逻辑后，运行聚焦测试和至少一组 Release metrics。
- 对 FP/FN 修复要保留诊断依据：错误类别、涉及 pass、修改前后指标和耗时变化。
