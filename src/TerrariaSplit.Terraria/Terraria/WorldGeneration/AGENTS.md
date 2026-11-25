# WorldGeneration 局部约束

- 负责原生 ABI7 客户端（`JungleSeedJudgeNativeClient`）、线程预算策略（`WorldFilterThreadPolicy`）与统一筛选结果模型，不执行窗口点击、删档或 UI 展示。
- 单世界线程默认 `max(1,min(6,floor(3×可用物理核/4)))`，并发候选固定每世界 1 线程；实际预留预算原样传给 DLL，超时后须等原生调用返回再释放租约。
- 校验响应中的 `terrariaVersion`、`compatibilityId` 与 `generationCompatibilityId`，仅接受 ABI7 且指纹一致的响应。
