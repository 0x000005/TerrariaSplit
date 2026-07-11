# Rendering

## 约束
- 只负责 overlay 布局、文字效果、图标、动画和绘制资源；不触发命令、文件、声音、自动化或 watcher。
- 输入使用 settings、palette、layout、snapshot 和 animation model，不读取应用可变状态。
- 明确 GDI 资源所有权并复用现有 cache；主 overlay 与 timer overlay 保持一致的布局和缩放来源。
- 修改效果、布局、动画或 render model 时更新 `test/Code/RenderingTests.cs`；尺寸变化同时检查 shell/layout 测试。
