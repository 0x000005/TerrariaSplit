# Rendering 局部约束

- 纯绘制层：仅消费不可变 `AppSettings`、`UiPalette`、`SplitLayout`、快照与动画模型，不读取应用可变状态，不触发命令、文件、音效或自动化。
- 明确 GDI+ 资源所有权并复用 `OverlayRenderResources` / `BossIconCache`；主状态层与 `TimerOverlayForm` 共享同一套布局和缩放来源。
- 改动布局、文字效果、图标或动画模型时同步校验 `OverlayCompositeLayoutCalculator` 与相关测试。
