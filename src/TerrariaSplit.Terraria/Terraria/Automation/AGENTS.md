# Automation

## 边界
- 编排菜单点击、人物/世界创建、进入世界、存档、世界池和筛塔流程。
- 不实现世界生成模拟、内存布局解析或 UI 绘制。

## 约束
- 步骤必须可取消、可记录；坐标和页面判断集中到现有 geometry/reader。
- 金字塔预筛只是创建前预测，创建后 `.wld` 二验必须保留。
- 世界文件顺序读取，不并行扫描；官方生成规则放在 `WorldGeneration/`。
- 修改流程或失败策略时运行 Automation 与 Pyramid seed pre-screen 聚焦测试；真实窗口路径需说明人工验证。
- 预筛维护参考 `pyramid-prescan-maintenance.md`。
