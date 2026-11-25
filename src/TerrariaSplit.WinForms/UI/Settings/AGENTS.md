# Settings UI 局部约束

- `UI/Settings/` 承载与 UI 框架解耦的设置描述符、路由草稿/校验/提交服务、模型变更通知与诊断导出逻辑；默认值与归一化归 `Configuration`，安装/重启/热键/自动化副作用必须回交 Shell/Host。
- 10 页可视化设置界面位于 `UI/Wpf/Settings/`，托管于 `UI/Wpf/Windows/SettingsWindow`；跨页联动统一走 `SettingsModelChange` / `ISettingsModelListener`，禁止直接持有其他页面控件；优先复用 `WpfSettingsUi`、`WpfSettingsPageBase`、`WpfSettingsDialogService` 与 `UI/Wpf/Theme` 控件样式。
- 异步操作必须可取消并在页面/窗体释放时停止；改动绑定时验证打开、编辑、保存与重载闭环。
