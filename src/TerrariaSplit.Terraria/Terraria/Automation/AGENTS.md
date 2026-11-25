# Automation 局部约束

- 仅编排窗口激活、菜单驱动、存档准备、世界池安装与筛种循环；不实现生成模拟、内存布局解析或 UI 绘制。
- 所有步骤必须可取消、可记录；菜单坐标与页面判定集中在现有 `TerrariaMenuGeometry` / `TerrariaMenuObserver`。
- 存档清理/清点必须在世界池安装与建人之前执行；后台世界池仅操作私有 scratch 目录，严禁触碰用户 `Players/`/`Worlds/`。
- 生产筛选统一通过 `WorldSeedFilterEvaluator` 调用原生 ABI7 DLL；改动流程或失败策略后运行自动化与筛选聚焦测试。
