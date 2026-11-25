# WorldGeneration Simulation 局部约束

- 仅保留给诊断工具与历史回归测试，**禁止重新接入生产筛选路径**（生产筛选统一使用原生 ABI7 DLL）。
- 维护时保持官方 RNG、pass 顺序与候选判定，不引入单 seed 特判、数据集专用阈值或破坏顺序的并行写入。
