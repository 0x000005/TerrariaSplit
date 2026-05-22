# TerrariaSplit

<p align="center">
  <a href="README.en.md">English</a> ·
  <span>简体中文</span>
</p>




TerrariaSplit 是一款面向 1.4.5 的高度可自定义 Boss 专用计时器，拥有精致的分段动画、灵活的参考数据、统计对比功能，以及完整的可视化设置界面。

## 功能特性

### ⚡ 便捷性

TerrariaSplit 追求“打开就能用”的体验。程序启动很快，主界面保持轻量，不需要复杂准备即可进入计时状态。常用设置都集中在可视化配置界面中完成，包括快捷键、路线、数据、界面、效果、颜色和声音，不需要在 JSON 里反复手改才能完成基础配置。

### 🧩 自由性

你可以按自己的跑法完全定制计时器：

- 自定义 BOSS 顺序、启用状态、分组方式和 BOSS 图标。
- 方便维护多套配置，并在主界面右键快速切换。
- 自定义分段显示器、主计时器等界面组件的位置、大小、字体、透明度、阴影、描边和显示状态。
- 自定义界面颜色，让计时器更适合自己的 overlay 风格。
- 自定义暂停、重置、到达 split 点等状态音效。

### 📊 实用性

TerrariaSplit 会自动记录每一局的数据，并且可以选择自动更新个人最佳数据。统计页面可以方便地对比自己的成绩、个人最佳和参考成绩，帮助你快速判断问题出在哪个 BOSS 或哪个分段。

对于不是从第一个BOSS开始的练习局，程序会自动忽略这些记录，不会把中途练习的数据误计入。

为了减少 Terraria 练习本身的重复劳动，程序还提供了更贴近实际跑图流程的工具：

- 一键自动创建世界，加快重开与练习节奏。
- 提供练习存档槽位，方便在不同训练场景之间快速切换。
- Zenith 世界支持自动接星，建图时还可自动筛掉不含目标金字塔的地图，减少重复刷图成本。

### ✨ 美观性

TerrariaSplit 不只是显示时间，也重视跑步过程中的视觉反馈。项目内置了大量经过设计的效果：

- BOSS 图标会按路线顺序逐步点亮，清晰展示当前进度。
- 可以提前显示与参考时间的差值，不必等到击败下一个 BOSS 才看到节奏变化。
- 主计时器与差值列支持根据当前快慢动态渐变着色，更容易一眼读出节奏。
- BOSS 击败时会播放精美的完成动画，分段时间、总时间以及参考对比都可以直接查看。
- 当分段或总成绩表现优秀时，可使用高亮、霓虹、极光等炫彩字体效果强调结果。
- 当前阶段可以突出显示，让视线自然落到正在进行的分段。

### 🖥️ 显示支持

TerrariaSplit 也考虑了不同显示设备上的可用性：

- 支持更高的采样与刷新频率，在高刷显示器上保持更顺滑的计时显示。
- 提供 Terraria UI 缩放增强，可将游戏内 UI 缩放上限从 200% 提升到 300%，缓解高分辨率屏幕下 UI 过小的问题。

## 预览



### 主界面
<p align="center">
  <img src="docs/images/image-5.png" alt="Main window" width="720">
</p>

<p align="center">
  <img src="docs/images/image-4.png" alt="Main window" width="720">
</p>

### BOSS 击败动画与高亮字体

<p align="center">
  <img src="docs/images/image-3.png" alt="Split completion animation" width="720">
</p>

### 当前阶段突出显示
<p align="center">
  <img src="docs/images/image-6.png" alt="Split completion animation" width="720">
</p>


## 默认快捷键

| 操作 | 默认键 |
| --- | --- |
| 暂停 / 继续 | `F12` |
| 菜单时重置 | `F6` |
| 鼠标穿透 | `F9` |
| 菜单时建图 | `F7` |
| 菜单时加载世界 | `F8` |

## 配置文件

TerrariaSplit 使用程序目录下的 `settings/` 文件夹保存配置。

```text
TerrariaSplit.exe
settings/
  settings.json
  other-profile.json
  active-profile.txt
reference-times/
  WR.json
last-times/
  2026-05-02-...json
```

- `settings/*.json`：主配置文件，可放多个。
- `settings/active-profile.txt`：记录当前使用的配置文件名。
- `reference-times/*.json`：参考时间组。
- `last-times/*.json`：自动记录的上一局和历史局数据。

首次运行时，如果 `settings/` 下没有有效配置，程序会从内置模板生成默认配置。


## 注意事项

- 本项目主要使用 AI 辅助构建。

## 致谢
- 感谢 [LiveSplit](https://github.com/LiveSplit/LiveSplit) 对速通计时器布局和分段展示方式的启发。
- BOSS 点亮设计与图标呈现参考了 [kengho/terraria-boss-checklist](https://github.com/kengho/terraria-boss-checklist)。




## OBS 捕获黑底

如果 OBS 窗口捕获出现黑色背景，可以在 `设置 > 颜色 > 界面颜色 > 捕获背景` 里设置录屏软件能看到的透明色键，例如 `#FF00FF` 或 `#00FF00`，再在 OBS 中抠除该颜色。程序窗口本身仍然保持透明。

## License

本项目使用 MIT License。
