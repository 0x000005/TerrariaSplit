# Pyramid Pre-Screen Maintenance Notes

本文记录筛塔预筛当前用到的测试工具、模拟链路、已包含优化和后续优化方向。

```text
TerrariaSplit/Terraria/WorldGeneration/Simulation
```


## 当前目标范围

- Terraria `1.4.5.6` 普通世界生成。
- 小世界：`4200 x 1200`。
- 猩红世界。
- 无特殊/彩蛋种子。
- 预筛只看目标金字塔走廊：`X=[32%,68%)`，`Y=[15%,35%)`。
- 预筛只是第一层过滤；创建世界后的 `.wld` 二验仍然保留，作为最终防线。

最新大集测试口径使用：

```text
D:\OneDrive - huzhaoran\Creative\Terraria\Worlds
```

注意：`itemMismatch` 按误放行处理，等价于 FP 风险。

## 入口链路

预筛入口：

```text
TerrariaSplit/Terraria/WorldGeneration/PyramidSeedPreScreen.cs
```

运行顺序：

1. `PyramidSeedPreScreen.EvaluateSmallCrimson(...)`
2. 解析 seed、大小、难度、猩红、特殊种子 mask。
3. `WorldOptions.FromMetadata(...)` 检查是否在支持范围内。
4. `StageOneReplicaSimulator.Generate(...)`
5. `StageOneReset.Apply(...)` 复刻 reset 阶段的关键 GenVars 和世界参数。
6. `WorldGenerator.RunUntilInclusive("Pyramids")`
7. `OfficialPassPlan.AppendToPyramids(...)` 按官方 pass 顺序跑到 `Pyramids`。
8. `WorldGenState.ScanTargetPyramidChests()` 扫描模拟出来的目标走廊内金字塔箱子。
9. 根据箱内主物品返回 `sandstorm` / `flying` / `other` / `none`。

金字塔一定有箱子。遇到 `simulated-no-chest` 时，不应理解为“建了塔但没箱子”，而应理解为：本地模拟在 `Pyramids` 前或 `Pyramids` 中没有让真实会建塔的候选通过。

## 当前 pass 模拟步骤

官方普通小世界到 `Pyramids` 的 pass 顺序由：

```text
TerrariaSplit/Terraria/WorldGeneration/Simulation/OfficialPassPlan.cs
```

维护。当前 pass 总数为 41，其中：

- `ImplementedPassCount = 20`
- `ExplicitlySkippedPassCount = 21`
- `StubPassCount = 0`

必须精确或结构等价的核心步骤：

- `Reset`
- `Terrain`
- `Dunes`
- `Ocean Sand`
- `Pyramids`

在目标区域做局部模拟或候选相关模拟的 pass：

- `Sand Patches`
- `Tunnels`
- `Mount Caves`
- `Dirt Wall Backgrounds`
- `Dirt Layer Caves`
- `Surface Caves`
- `Generate Ice Biome`
- `Grass`
- `Jungle`
- `Mud Caves To Grass`
- `Full Desert`
- `Corruption`，在当前范围内实际是猩红分支
- `Slush`
- `Gems`
- `Gravitating Sand`

显式跳过的 pass 依赖一个关键事实：Terraria worldgen runner 每个 pass 开头都会用同一个 world seed 重置 `UnifiedRandom`。因此跳过整个 pass body 不会改变后续 pass 的 RNG 起点。但跳过仍然必须满足下面至少一个条件：

- 它不会写入目标金字塔走廊。
- 它只影响天空、深洞穴、地狱、远海边界或墙体等当前目标不读取的区域。
- 它的缺失已由候选风险 gate 覆盖。
- 它只会影响 `.wld` 二验前的低风险细节，不影响是否存在目标金字塔。

## 已包含的主要优化

### 1. 只支持小世界猩红无特殊种子

这让世界尺寸、evil 分支、特殊 pass 都固定，避免为大世界/腐化/秘密种子保留慢路径。

### 2. 候选驱动

很多 pass 通过 `WorldInterestArea.HasPotentialTargetPyramidCandidate(...)` 做早退。若当前 seed 在目标走廊附近没有可能通过 `Pyramids` 的候选，后续候选相关 pass 直接跳过。

### 3. 目标走廊限制

目标区域集中在 `X=[32%,68%)`，远离中心过宽边界和海边。局部模拟时只保留可能影响候选扫描列、候选附近地形、或 spacing 的部分。

### 4. pass 级跳过

由于每个 pass RNG 重置，跳过已审计无关 pass 不会污染后续 pass 的 RNG。当前显式跳过包括矿物、深层洞穴、浮岛、地狱、湖泊、海滩、Shimmer、Clean Up Dirt 等多类 pass；`Dirt Wall Backgrounds` 已实现，因为 `wall == 2` 会影响后续 jungle mud wall 分支。

### 5. tile grid 轻量化

模拟只维护 `TileData` 的必要字段：active、type、wall、liquid、liquidType。不会创建官方 `Tile` 对象矩阵，也不模拟帧、光照、NPC、完整箱子对象等无关内容。

### 6. 局部 Full Desert

`Full Desert` 是重要且慢的 pass。当前只在候选相关区域模拟地下沙漠壳体、蜂巢/入口附近可能影响扫描列的部分，并用风险 gate 覆盖裁剪导致的不确定。

### 7. 局部 Jungle / Mud Caves To Grass

`Jungle` 和 `Mud Caves To Grass` 会把候选扫描列的沙或周边活跃块变成泥/草，影响金字塔是否可建。当前实现保留会影响地表和地下浅层的部分，并通过 `JungleMudCoverageUncertain` gate 处理缺口。

### 8. Crimson risk gate

猩红 pass 会把候选扫描沙变成 `Crimsand` 或改变附近地形。当前 `CrimsonConvertedScanSand` 是 hard risk 的一部分，用于避免明显 FP。之前尝试过更官方化 Crimson 局部细节，但在大集上仍不能达标，已按要求不保留那组行为改动。

### 9. Crimson surface biome bounds 缓存

Crimson range 选择前需要知道 surface 层 `JungleGrass` 和 `Snow/Ice` 的横向范围。旧实现每次 Crimson pass 开头做两次全宽扫描；当前改为 `WorldGenState` 中的 dirty-column surface biome tracking：

- `Generate Ice Biome` 开始时重置 tracking。
- Ice、Mud Caves To Grass、Jungle tunnel、Full Desert 等会影响这些 tile 的局部写入只标记 dirty 列。
- Crimson 读取前只重建 dirty 列的 surface 计数，再计算与旧扫描相同的 padded bounds。
- 若 tracking 未初始化，会退回一次全宽精确重建，保证语义优先于性能。

该优化在 `D:\Worlds` 上不改变 FP/FN/itemMismatch；主要收益是移除 Crimson 内部的全宽双扫结构，当前总耗时仍主要受 Jungle 和 Full Desert 波动支配。

### 10. Pyramids 只模拟到首个箱子所需信息

预筛只需要知道是否有目标金字塔，以及箱内目标物品。无需完整模拟金字塔每一处墙、装饰和后续无关生成。

## Hard Risk Gate

`PyramidCandidateRisk.HardRejectMask` 当前用于拒绝本地模型不够确定的候选，优先降低 FP。

主要风险：

- `CrimsonConvertedScanSand`
- `FullDesertBoundaryUncertain`
- `FullDesertSurfaceUncertain`
- `SkippedDungeonBoundaryUncertain`
- `JungleMudCoverageUncertain`

经验教训：

- 不能直接删除 `CrimsonConvertedScanSand` 或 `JungleMudCoverageUncertain`，会明显增加 FP。
- 要降低 FN，正确方向是把 risk gate 收窄到官方机制证明会影响候选的子区域，而不是整体放宽。

## 测试工具集

### 1. 常规构建

```powershell
dotnet build test\TerrariaSplit.Tests.csproj
dotnet build TerrariaSplit.slnx
```

业务逻辑改动后优先跑测试工程构建。发布前跑解决方案构建。

### 2. 聚焦测试

```powershell
$env:TERRARIA_SPLIT_TEST_FILTER='Pyramid seed pre-screen'
dotnet run --project test\TerrariaSplit.Tests.csproj
```

用于快速验证预筛相关单元测试。

### 3. 数据集指标评估

入口在：

```text
test/PyramidPreScreenMetrics.cs
```

常用命令：

```powershell
dotnet run -c Release --project test\TerrariaSplit.Tests.csproj -- pyramid-metrics "D:\Worlds" --csv test\metrics-worlds-current-release.csv --diagnose-errors --diagnostics-csv test\metrics-worlds-diagnostics-current.csv
```

选项：

- `--csv <path>`：输出每个世界的 TP/FP/TN/FN/itemMismatch 和耗时。
- `--diagnose-errors`：只输出错误样本诊断。
- `--diagnose-all`：输出所有样本诊断，文件会很大。
- `--diagnostics-csv <path>`：输出候选和箱子细节。
- `--diagnose-seed <seed>`：只诊断指定 seed。
- `--pass-timings`：额外打印每个 simulated pass 的耗时摘要，诊断模式使用，不影响正常预筛运行。
- `--pass-timings-csv <path>`：输出每个 seed、每个 pass 的 `durationMs` 和 `randNext`，用于定位新增 pass 或优化后的耗时来源。

指标含义：

- `FP`：预筛认为有目标塔，但 `.wld` 二验无目标塔。
- `FN`：`.wld` 二验有目标塔，但预筛没有放行。
- `itemMismatch`：预筛有塔但物品类别不一致，按 FP 风险处理。
- `p50Ms`：中位数耗时。当前目标要求不超过 `140ms`。

### 4. 本地 pass trace

入口在：

```text
test/PyramidPreScreenTrace.cs
```

命令：

```powershell
dotnet run -c Release --project test\TerrariaSplit.Tests.csproj -- pyramid-trace 220205531
dotnet run -c Release --project test\TerrariaSplit.Tests.csproj -- pyramid-trace 220205531 "Corruption" "Pyramids"
```

默认 stop：

- `Dunes`
- `Ocean Sand`
- `Full Desert`
- `Corruption`
- `Clean Up Dirt`
- `Pyramids`

输出内容：

- 世界表面、地牢方向、地牢位置、地下沙漠矩形。
- Crimson accepted range 和 attempt 诊断。
- Full Desert candidate step 诊断。
- 每个 pyramid candidate 的 scanY、tile type、spacing、fate、risk。
- 模拟箱子位置和 loot summary。

### 5. 官方 pass-stop probe

入口在：

```text
test/OfficialProbe/OfficialPyramidPassProbe.cs
```

说明在：

```text
test/OfficialProbe/README.md
```

它加载真实 Terraria `1.4.5.6` assembly，记录官方 pass-stop CSV。用于证明某个规则是否来自官方机制，而不是数据集特化。

典型运行：

```powershell
test\OfficialProbe\bin\OfficialPyramidPassProbe.exe `
  --deps "D:\OneDrive - huzhaoran\Creative\Terraria\reference\Terraria1456\pyramid-probe\exactgen\bin" `
  --out test\official-pyramid-pass-diagnostics-current.csv `
  220205531 1572599072 546717794
```

它记录：

- `GenVars.PyrX/PyrY/numPyr` 候选。
- `after:Dunes`、`after:Ocean Sand`、`after:Full Desert`、`after:Corruption`、`after:Clean Up Dirt`。
- `before:Pyramids` 官方候选判定。
- `after:Pyramids` 官方目标区域金字塔 tile 和目标箱子摘要。

### 6. `.wld` 二验

指标工具使用：

```text
TerrariaSplit/Terraria/Automation/TerrariaWorldFilePyramidScanner.cs
```

读取真实世界文件的 metadata、chest contents 和目标区域金字塔结果。它是评估真值来源，不是规则来源。

## 当前已知大集状态

在 `D:\OneDrive - huzhaoran\Creative\Terraria\Worlds` 上，按“Crimson 三项行为变更不保留”后的测试结果：

```text
supported=875
tp=204
fp=11
tn=601
fn=59
itemMismatch=2
fpRate=1.80 %
fnRate=22.43 %
avgMs=99.578
p50Ms=134
p95Ms=214
maxMs=692
```

这个结果未达到目标：

- `FP < 0.5%`
- `FN < 20%`
- `itemMismatch` 作为 FP 风险不能忽略
- `p50Ms <= 140ms`

但中位数时间仍在限制内。

## 当前主要错误类型

### FP / itemMismatch

多数 FP 的候选 risk 是 `None`，说明并不是 hard-risk 放宽造成，而是本地模拟在官方不会建塔的位置建出了塔。

常见根因候选：

- Crimson/Corruption pass 局部地形与官方不一致。
- Jungle / Full Desert 局部模拟没有覆盖某些会改变 scan tile 的写入。
- `Pyramids` 前扫描列 tile type 和官方不同。
- 候选 spacing/order 与官方不同。
- 金字塔内 loot RNG 与官方不一致，导致 `itemMismatch`。

### simulated-no-chest

`simulated-no-chest` 表示真实 `.wld` 有塔，但本地没有进入有效建塔/滚箱逻辑。常见表现：

- 本地 scan tile 是 `Crimsand`、`Mud`、`HardenedSand`、`Dirt` 等，官方 before `Pyramids` 仍为 sand。
- 本地候选被 spacing、边界或 risk 拦住。
- 本地 Full Desert / Jungle / Crimson 改写了真实可建候选。

处理原则：

- 先用官方 probe 确认 before `Pyramids` 的 scan tile 和 reject reason。
- 再用 `pyramid-trace` 对比本地同一 stop。
- 只有能归因到官方 pass 差异时才改模拟逻辑。

## 后续优化方向

### 1. FP 优先：对 FP seed 跑官方 pass-stop 归因

对每个 FP/itemMismatch，至少记录：

- Dunes/Ocean Sand 候选。
- `after:Full Desert` scan tile。
- `after:Corruption` scan tile。
- `before:Pyramids` 官方 reject reason。
- `after:Pyramids` 是否有目标塔/箱。

目标是把 FP 分成：

- 本地候选点不同。
- scan tile 不同。
- spacing/order 不同。
- pass-local 地形写入不同。
- loot RNG 不同。

### 2. 收窄 JungleMudCoverageUncertain

当前 `JungleMudCoverageUncertain` 会产生不少 FN。不能直接删除。可行方向：

- `Dirt Wall Backgrounds` 已接入，用于修正 `wall == 2` 对后续泥墙生成的影响；单独接入在 `D:\Worlds` 上不改变 FP/FN，但保持 p50 在目标内。
- `Rocks In Dirt` / `Dirt In Rocks` / `Clay` 目前仍未接入主流程。完整接入并逐 tile 消耗 RNG 的实验会把 16 样本 p50 推到约 230ms 以上，不满足性能目标；后续需要真正的 per-run 快进或更窄的官方可证写入域。
- 对候选列附近做更接近官方的局部 Jungle 主泥团/通道模拟。
- 只在官方机制证明泥团能覆盖 scan column 时 hard reject。
- 用候选的 `sandDepth`、`sandSpan`、`activeDepth` 和离 Jungle 真实范围距离做辅助诊断，但不要引入数据集魔法阈值。

### 3. 修 simulated-no-chest

优先处理 `risk=None` 的 simulated-no-chest，因为它们不涉及 hard-risk 策略：

- 对比本地和官方 `after:Full Desert`、`after:Corruption`、`before:Pyramids`。
- 若官方 scan tile 为 sand、本地不是 sand，补对应 pass 的局部写入。
- 若本地 spacing 不同，检查候选来源顺序和 `GenVars.PyrX/PyrY` 复刻。
- 若本地建塔但未产生目标箱，先怀疑 `Pyramids` 房间/箱子 RNG 顺序，而不是“金字塔没有箱子”。

### 4. Crimson 局部精度

Crimson 是 FP/FN 的共同高风险点。后续不要整体回退或整体放宽，而是做可证明的小修：

- 给官方 probe 增加 Crimson sub-step 诊断，例如 range、surface conversion、CrimStart/CrimVein 后候选列状态。
- 对本地 trace 增加同样 sub-step。
- 只合并能解释多个 seed 且不增加 FP 的官方行为。

### 5. Full Desert 局部模拟

当前 Full Desert 用局部模拟和风险 gate 结合。后续方向：

- 对候选 scan column 及附近列做更完整的 hive/entrance 局部写入。
- 保留整段 Full Desert 跳过，避免 p50/p95 失控。
- 用官方 probe 验证 `after:Full Desert` scan tile，而不是用数据集结果倒推阈值。

### 6. Loot RNG / itemMismatch

`itemMismatch` 必须按 FP 风险处理。后续需要：

- 对 itemMismatch seed 比较官方 `after:Pyramids` 目标箱主物品。
- 检查本地 `PyramidsPassReplica` 中房间首次放置、箱子位置、main item roll、prefix roll 和后续杂物 roll。
- 不要为了匹配某个 seed 改物品权重；必须和官方 chest item generation 顺序一致。

### 7. 性能边界

当前 p50 仍低于 `140ms`，但 p95 较高。后续增加模拟时应遵守：

- 先用 `pyramid-metrics --pass-timings` 或 `--pass-timings-csv` 量化每个 pass 的耗时，再决定优化点。
- 优先候选列和候选附近局部模拟。
- 不引入整段 Full Jungle、Full Desert、全 cave 级模拟。
- 增加诊断可以放在测试入口，不要默认影响预筛运行。
- 每个优化都用 `D:\Worlds` Release metrics 验证 p50、p95 和 max。

## 禁止事项

- 不加 seed 黑名单/白名单。
- 不用“当前数据集刚好成立”的 magic threshold。
- 不为了修一个 FP 而把同类真塔一起 hard reject。
- 不跳过 `.wld` 二验。
- 不把 `simulated-no-chest` 解释为“金字塔可能无箱子”。
- 不恢复外层 `WorldGenSim/` 作为主程序实际行为来源。

## 推荐工作流

1. 跑大集基线：

```powershell
dotnet run -c Release --project test\TerrariaSplit.Tests.csproj -- pyramid-metrics "D:\Worlds" --csv test\metrics-worlds-current-release.csv --diagnose-errors --diagnostics-csv test\metrics-worlds-diagnostics-current.csv
```

2. 按错误类别分组，先看 FP/itemMismatch，再看 `simulated-no-chest`，最后才看 hard-risk FN。

3. 对代表 seed 跑本地 trace：

```powershell
dotnet run -c Release --project test\TerrariaSplit.Tests.csproj -- pyramid-trace <seed> "Full Desert" "Corruption" "Pyramids"
```

4. 对同一 seed 跑官方 probe。

5. 只有归因到官方 pass 差异后才改代码。

6. 改后至少跑：

```powershell
dotnet build test\TerrariaSplit.Tests.csproj
dotnet run -c Release --project test\TerrariaSplit.Tests.csproj -- pyramid-metrics "D:\Worlds" --csv test\metrics-worlds-after-change-release.csv --diagnose-errors --diagnostics-csv test\metrics-worlds-diagnostics-after-change.csv
```

7. 接受标准：

- FP 不增加。
- itemMismatch 不增加。
- FN 下降或错误归因更清晰。
- `p50Ms <= 140`。
- 修改依据来自官方流程，而不是数据集特化。
