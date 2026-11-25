# Settings UI 局部约束

- 仅负责 10 页设置控件构建、懒加载、生命周期与字段绑定；默认值与归一化归 `Configuration`，安装/重启/热键/自动化副作用必须回交 Shell/Host。
- 跨页联动统一走 `SettingsModelChange` / `ISettingsModelListener`，禁止直接持有其他页面控件；优先复用 `SettingsUiFactory`、自绘 `Themed*` 控件与 `SettingsPageBase`。
- 异步操作必须可取消并在页面/窗体释放时停止；改动绑定时验证打开、编辑、保存与重载闭环。
