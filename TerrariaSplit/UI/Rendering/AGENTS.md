# Rendering 层说明

## 职责
- 这里只负责 overlay 的视觉表达：布局计算、文字效果、图标、动画曲线和绘制资源管理。
- 渲染器应从上下文得到全部输入，尽量做到给定状态就能得到可预测输出。

## 设计原则
- 绘制代码不触发业务命令、文件写入、声音播放、自动化或 watcher 操作。
- 渲染输入使用 settings、palette、layout、snapshot 和 animation model；不要读取应用核心的可变状态。
- GDI 资源生命周期要清楚，优先复用现有 cache 和 `OverlayRenderResources`。
- 布局和缩放规则要在主 overlay 与 timer overlay 之间保持一致。
- 动画逻辑应有可测试的曲线或模型，绘制函数只消费当前帧状态。

## 验证
- 修改文字效果、列布局、动画曲线或 render model 后，更新 `test/RenderingTests.cs`。
- 修改尺寸计算时，同时检查 shell/layout 相关测试。
