# OfficialProbe 说明

## 职责
- 本目录放官方流程对照诊断工具，用来解释金字塔预筛 FP/FN 的机制来源。
- Probe 输出是证据和调试材料，不是主程序筛选逻辑。
- 本目录不被 `test/TerrariaSplit.Tests.csproj` 自动编译；构建方式以 `README.md` 为准。

## 使用原则
- 先用 probe 归因候选点差异、scan tile 差异、spacing/order 差异、pass-local 地形修改差异或箱子/物品差异，再决定是否改模拟器。
- 不把某个数据集刚好成立的现象直接写成规则；必须能对应到官方 pass 或项目解析错误。
- Probe 可以输出 CSV/摘要，但临时结果默认不提交；需要长期保留时写入维护文档并标明数据集和日期。
- 不让 probe 代码被主工程引用。

## 验证
- 修改 probe 后运行 `dotnet build test\TerrariaSplit.Tests.csproj`，必要时用少量已知 FP/FN seed 做手动对照。
