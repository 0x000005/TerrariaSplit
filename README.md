# TerrariaSplit
<p align="center">
  <img src="docs/images/readme-main.png" alt="TerrariaSplit 计时器预览" width="360">
</p>
<p align="center">
  <a href="README.en.md">English</a> ·
  <span>简体中文</span>
</p>

TerrariaSplit 是一款面向 Terraria 的 Windows 分段计时器。它以 Boss 路线计时为核心，也支持物品、NPC、生物群系等条件。程序提供可视化设置、实时 overlay、数据统计以及用于练习和刷图的自动化工具。

当前支持 Terraria `1.4.5.7` 和 `1.4.4.9`。

## 核心功能

### 计时与路线

- 支持各类条件： Boss 击败、物品获取、NPC 到达、进入生物群系。
- 支持“全部完成”“任意完成”“至少完成 N 个”等多目标条件。
- 支持自定义分组，并可以配置主分组、附属分组及其显示条件。

### 显示

- 支持鼠标穿透，以及通过 RTSS 在全屏 Terraria 中显示简洁 OSD。
- 支持配置主界面字体、图标、透明度、颜色、阴影和描边。
- 支持图标点亮、当前阶段高亮、字体颜色渐变以及 BOSS 击败动画，单段高亮等动画效果。


### 数据与统计

- 自动记录运行数据，并可选择自动更新个人最佳。
- 统计页面可对比当前记录、历史记录、个人最佳和参考时间。


### 自动化工具

- 一键创建世界（支持彩蛋、秘密种子）和加载练习存档。
- 支持金字塔筛选和物品过滤，以及 Zenith 世界自动接星。

### 联机

- 联机模式用于让多名玩家使用同一世界、路线和参考时间进行比赛，并实时查看彼此的进度和排名。
- 联机模式需要单独启动 Race Server。可以这样指定端口启动：

Windows：

```powershell
.\TerrariaSplit.Race.Server.exe --urls http://0.0.0.0:5000
```

Linux：

```bash
./TerrariaSplit.Race.Server --urls http://0.0.0.0:5000
```

- 将 `5000` 改为需要的端口号即可。若玩家不在同一内网，服务器需要能通过公网 IP、端口转发或内网穿透服务访问。


## 注意事项

- 本项目主要使用 AI 辅助构建。

## OBS 捕获黑底

如果 OBS 窗口捕获出现黑色背景，可以尝试使用 Windows 10 捕获方式，而不是传统窗口捕获方式。

## 致谢

- 感谢 [LiveSplit](https://github.com/LiveSplit/LiveSplit) 对速通计时器布局和 split 展示方式的启发。
- Boss 点亮设计与图标呈现参考了 [kengho/terraria-boss-checklist](https://github.com/kengho/terraria-boss-checklist)。

## License

本项目使用 MIT License。
