# Settings UI 说明

## 职责
- 本目录负责设置窗口的分页 UI、控件工厂、页面宿主、页面生命周期和设置编辑适配。
- 这里可以把用户输入写回 `AppSettings` 对象，但不负责设置的默认值、归一化、序列化或持久化策略。
- `SettingsPageHost` 负责页面注册、懒加载和页面间 model change 通知；各 `*SettingsPage` 只处理本页布局和字段绑定。

## 设计原则
- 新增设置页或设置项时，先确认 `Configuration/` 中模型、默认值和归一化已经有清晰语义，再补 UI。
- 页面之间的联动通过 `SettingsModelChange` 和 `SettingsPageHost.NotifyModelChanged`，不要让页面直接持有彼此的控件。
- 控件创建、颜色、间距和滚动行为优先复用 `SettingsUiFactory`、`ThemedScrollPanel`、`ThemedSlider` 和现有 page base。
- 页面提交前只做 UI 层必要解析和友好错误提示；业务含义、范围兜底和兼容读取应留在配置或应用层。
- 涉及自动化、世界池、筛塔或热键的设置，只在这里编辑数据，不直接启动外部流程或注册系统热键。

## 验证
- 修改设置页字段绑定后，检查对应设置能打开、编辑、保存、重新加载。
- 新增或重命名设置项时，同步运行完整测试，并重点查看配置归一化、设置窗口构建和相关自动化/渲染用例。
