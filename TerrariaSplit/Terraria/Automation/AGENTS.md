# Automation 集成说明

## 职责
- 本目录负责编排 Terraria 菜单点击、建人物、建世界、进世界、存档准备、世界池和筛塔自动化。
- 自动化层处理流程和失败策略，但不实现世界生成模拟、内存偏移解析或 UI 绘制。

## 设计原则
- 每个步骤应可取消、可记录，并通过 `TerrariaAutomationContext`、`TerrariaMenuGeometry` 等现有抽象执行副作用。
- 坐标和菜单判断集中在菜单几何或专用 reader 中，不把裸坐标散落到流程代码里。
- 金字塔预筛只作为创建前预测；创建后的世界文件二验必须保留，防止预筛误判。
- 预筛读取种子超时、模拟报错或预测不可用时，按设置决定继续创建或返回主页，不在流程里吞掉原因。
- 自动化流程处理世界文件时按顺序读取，不并行扫描或打开多个 `.wld`。
- 不要把官方世界生成规则写进自动化层；调用 `WorldGeneration/` 的接口或 `IPyramidSeedPreScreenEvaluator`。

## 验证
- 修改自动化步骤、筛塔循环、世界文件二验或失败策略后，运行 `AutomationRunnerTests` 和 `Pyramid seed pre-screen` 聚焦测试。
- 改动依赖真实 Terraria 窗口或鼠标位置时，在最终回复说明需要人工验证的界面路径。
