namespace TerrariaSplit;

internal sealed class ChineseStrings : ILocalizedStringProvider
{
    private static readonly Dictionary<string, string> Values = new(StringComparer.OrdinalIgnoreCase)
    {

        { "TerrariaSplit Settings", "TerrariaSplit 设置" },
        { "OK", "确定" },
        { "Apply", "应用" },
        { "Cancel", "取消" },
        { "General", "常规" },
        { "Route", "路线" },
        { "Too many results", "结果过多" },
        { "Data", "数据" },
        { "UI", "界面" },
        { "Colors", "颜色" },
        { "Sounds", "声音" },
        { "Advanced", "高级" },
        { "Automation", "自动化" },
        { "Auto", "自动" },
        { "Fixed", "固定" },
        { "Terraria UI scale enhancement", "Terraria UI 缩放增强" },
        { "Sampling frequency", "采样频率" },
        { "Control frequency", "控制频率" },
        { "Split timer refresh rate", "分段计时器刷新率" },
        { "Main timer refresh rate", "主计时器刷新率" },
        { "Raises Terraria's in-game UI scale slider limit from 200% to 300%.", "将游戏内 UI 缩放上限从 200% 提高到 300%。" },
        { "If Terraria's options menu was already opened before enabling, restart Terraria for the change to take effect.", "如果启用前已经打开过 Terraria 选项菜单，可能需要重启 Terraria 后生效。" },
        { "This changes the running Terraria process memory; enable with caution.", "该功能会修改正在运行的 Terraria 进程内存，启用需谨慎。" },
        { "Hotkeys", "快捷键" },
        { "General Options", "常规选项" },
        { "Language", "语言" },
        { "Pause / Resume", "暂停 / 继续" },
        { "Reset at Menu", "在菜单时重置" },
        { "Reset (Disabled in world)", "重置（在世界内不生效）" },
        { "Mouse passthrough", "鼠标穿透" },
        { "Create world flow", "自动创建世界" },
        { "Create world (Disabled in world)", "创建世界（在世界内不生效）" },
        { "Quick enter world (Disabled in world)", "快速进入世界（在世界内不生效）" },
        { "Load world (Disabled in world)", "加载世界（在世界内不生效）" },
        { "Hotkey warning", "快捷键警告" },
        { "Some hotkeys could not be registered:", "部分快捷键无法注册：" },
        { "{0}: {1} is duplicated; only the first action using this key is active.", "{0}：{1} 与其他快捷键重复；只有第一个使用该按键的动作会生效。" },
        { "{0}: {1} is not allowed as a hotkey.", "{0}：{1} 不允许作为快捷键。" },
        { "{0}: {1} registration failed. It may be used by another program. ({2})", "{0}：{1} 注册失败，可能已被其他程序占用。（{2}）" },
        { "Always on top", "置顶显示" },
        { "Practice mode", "练习模式" },
        { "Allow manual time editing", "允许手动修改时间" },
        { "Columns", "列" },
        { "Split display", "分段显示器" },
        { "Global scale %", "全局缩放 %" },
        { "Column", "列名" },
        { "Show", "显示" },
        { "Width", "宽度" },
        { "Font", "字体大小" },
        { "Font family", "字体" },
        { "Size", "字号" },
        { "Bold", "粗体" },
        { "Icon", "图标" },
        { "Icon (attached)", "图标（附属）" },
        { "Flags", "标记" },
        { "Expand", "展开" },
        { "Split details", "非附属组" },
        { "Expand multi-condition groups", "展开多条件组" },
        { "Collapse after completion", "完成后折叠" },
        { "Attached groups", "附属组" },
        { "Attached group marker", "附属组" },
        { "Auto hide attached groups", "自动隐藏附属组" },
        { "Attached groups affect main timer comparison", "附属组参与主计时器快慢判定" },
        { "Time", "时间" },
        { "Time (attached)", "时间（附属）" },
        { "Delta", "差值" },
        { "Delta (attached)", "差值（附属）" },
        { "Timer", "计时器" },
        { "Text", "文字" },
        { "Outline", "描边" },
        { "Shadow", "阴影" },
        { "Split", "分段" },
        { "Opacity %", "不透明度 %" },
        { "Shadow %", "阴影 %" },
        { "Outline thickness %", "描边厚度 %" },
        { "Main timer", "主计时器" },
        { "Offset X", "水平偏移" },
        { "Offset Y", "垂直偏移" },
        { "Part", "分段" },
        { "Section", "部分" },
        { "Main time", "累计时间" },
        { "Before decimal", "小数点前" },
        { "Milliseconds", "毫秒" },
        { "After decimal", "小数点后" },
        { "Icon Style", "图标样式" },
        { "Light icons when BOSS defeated", "完成当前阶段时点亮图标" },
        { "Light icons when current stage completed", "完成当前阶段时点亮图标" },
        { "Enable defeated icon lighting", "启用击败点亮图标" },
        { "Current boss grayscale weaken %", "当前阶段图标灰度额外削弱 %" },
        { "Current boss brightness boost %", "当前阶段图标亮度额外增强 %" },
        { "Current stage icon grayscale weaken %", "当前阶段图标灰度额外削弱 %" },
        { "Current stage icon brightness boost %", "当前阶段图标亮度额外增强 %" },
        { "Effects", "效果" },
        { "Current Split Highlight", "当前阶段突出显示" },
        { "Highlight current split", "突出显示当前分段" },
        { "Highlight current stage", "突出显示当前阶段" },
        { "Enable current split highlight", "启用当前阶段突出显示" },
        { "Current split scale %", "当前阶段放大比例 %" },
        { "Scale %", "放大比例 %" },
        { "Depth strength %", "景深强度 %" },
        { "Early delta time", "提前显示差值时间" },
        { "Show when within seconds", "相差秒数内显示" },
        { "BOSS Defeat", "主阶段完成" },
        { "BOSS defeat animation", "主阶段完成动画" },
        { "Main stage completion animation", "主阶段完成动画" },
        { "Enable animation", "启用动画" },
        { "Animation duration seconds", "动画时长（秒）" },
        { "Show comparison", "显示对比" },
        { "Show comparison with reference time", "是否显示与参考时间的对比" },
        { "Rainbow outline", "炫彩描边" },
        { "Outline when faster than reference", "速度快于参考值时的描边" },
        { "Outline %", "描边 %" },
        { "None", "无" },
        { "Neon", "霓虹" },
        { "Rainbow", "炫彩" },
        { "Aurora", "极光" },
        { "Gold", "鎏金" },
        { "Breathe", "呼吸" },
        { "Segment Best Highlight", "单段最佳突出显示" },
        { "Highlight best segment", "突出显示最佳单段" },
        { "Enable highlight", "启用突出显示" },
        { "Effect", "效果" },
        { "Cumulative time", "累积时间" },
        { "Segment comparison", "单段时间比较" },
        { "Rainbow outline %", "炫彩描边 %" },
        { "Unlit grayscale %", "未击败时图标灰度 %" },
        { "Unlit brightness %", "未击败时图标亮度 %" },
        { "Choose icon", "选择图标" },
        { "Browse", "浏览" },
        { "Reference Data", "参考时间" },
        { "Personal Best Data", "个人最佳数据" },
        { "m:ss or h:mm:ss", "m:ss 或 h:mm:ss" },
        { "new group name", "新参考组名" },
        { "Add", "添加" },
        { "Add to selected group", "添加至选中组" },
        { "Copy ID", "复制 ID" },
        { "Copied target ID: {0}", "已复制目标 ID：{0}" },
        { "Add to new group", "添加至新组" },
        { "Create new group", "创建新组" },
        { "Remove selected group", "移除选中组" },
        { "Remove selected condition", "移除选中条件" },
        { "Delete", "删除" },
        { "Remove", "移除" },
        { "Active group", "当前参考组" },
        { "Use PB as reference time", "使用 PB 作为参考时间" },
        { "Text Colors", "界面颜色" },
        { "Text type", "文字类型" },
        { "Reference text", "参考时间文字" },
        { "Reference time (future stage)", "参考时间（未来阶段）" },
        { "Reference time (current stage)", "参考时间（当前阶段）" },
        { "Reference", "参考" },
        { "Reference time", "参考时间" },
        { "Reference run", "参考局" },
        { "Selected run", "选择一局" },
        { "Personal best", "个人最佳累计" },
        { "Personal Cumulative Best", "个人最佳累计" },
        { "Personal segment best", "个人最佳单段" },
        { "Personal Data", "个人最佳更新" },
        { "Auto update personal data", "自动更新个人最佳" },
        { "Ask before updating personal data", "更新前确认" },
        { "Active file", "当前数据文件" },
        { "Update personal data?", "是否更新个人数据？" },
        { "Update", "更新" },
        { "Skip", "跳过" },
        { "UI Colors", "界面颜色" },
        { "Animation Colors", "动画颜色" },
        { "Capture Background", "捕获背景" },
        { "Background", "背景" },
        { "Transparent", "透明" },
        { "Cumulative", "累积" },
        { "No response updates automatically in {0}s.", "{0}秒内无响应将自动更新。" },
        { "Segment time", "单段时间" },
        { "Split time", "累计时间" },
        { "Reference time column", "参考累计时间" },
        { "Selected run time column", "该局累计时间" },
        { "Personal best time column", "个人最佳累计时间" },
        { "Reference segment time column", "参考单段时间" },
        { "Selected run segment time column", "该局单段时间" },
        { "Personal best segment time column", "个人最佳单段时间" },
        { "Reference segment", "参考单段时间" },
        { "Personal segment", "该局单段时间" },
        { "Personal best segment", "个人最佳单段时间" },
        { "Active reference text", "当前目标时间文字" },
        { "Completed split text", "已完成分段文字" },
        { "Cumulative time (completed stage)", "累积时间（已完成阶段）" },
        { "Delta (fast)", "差值（快）" },
        { "Delta (slow)", "差值（慢）" },
        { "Delta ahead text", "差值领先文字" },
        { "Delta behind text", "差值落后文字" },
        { "Timer text", "计时文字" },
        { "Main timer (not timing)", "主计时器（不在计时中）" },
        { "Main timer (fast)", "主计时器（快）" },
        { "Main timer (slow)", "主计时器（慢）" },
        { "Main timer (total fast)", "主计时器（总成绩快）" },
        { "Main timer (total slow)", "主计时器（总成绩慢）" },
        { "Main timer (paused)", "主计时器（暂停）" },
        { "Timer ahead text", "计时领先文字" },
        { "Timer behind text", "计时落后文字" },
        { "Timer record text", "计时破纪录文字" },
        { "Timer no record text", "计时未破纪录文字" },
        { "Timer paused text", "计时暂停文字" },
        { "Animation text", "动画文字" },
        { "Segment time hint text", "单段时间提示文本" },
        { "Cumulative time hint text", "累积时间提示文本" },
        { "Animation main time", "动画累计时间" },
        { "Animation cumulative time", "动画累计时间" },
        { "Pause sound", "暂停声音" },
        { "Resume sound", "继续声音" },
        { "Reset sound", "重置声音" },
        { "Timer start sound", "计时开始声音" },
        { "Split: total behind, segment behind", "Split：累积时间不快于参考，单段时间不快于 PB" },
        { "Split: total behind, segment not behind", "Split：累积时间不快于参考，单段时间快于 PB" },
        { "Split: total not behind, segment behind", "Split：累积时间快于参考，单段时间不快于 PB" },
        { "Split: total not behind, segment not behind", "Split：累积时间快于参考，单段时间快于 PB" },
        { "Split reached: total slower than reference, segment slower than PB", "到达分段点：累积时间不快于参考，单段时间不快于 PB" },
        { "Split reached: total slower than reference, segment not slower than PB", "到达分段点：累积时间不快于参考，单段时间快于 PB" },
        { "Split reached: total not slower than reference, segment slower than PB", "到达分段点：累积时间快于参考，单段时间不快于 PB" },
        { "Split reached: total not slower than reference, segment not slower than PB", "到达分段点：累积时间快于参考，单段时间快于 PB" },
        { "Split: total slower, segment slower", "分段点：累积时间不快于参考，单段时间不快于 PB" },
        { "Split: total slower, segment not slower", "分段点：累积时间不快于参考，单段时间快于 PB" },
        { "Split: total not slower, segment slower", "分段点：累积时间快于参考，单段时间不快于 PB" },
        { "Split: total not slower, segment not slower", "分段点：累积时间快于参考，单段时间快于 PB" },
        { "Stage reached: cumulative not faster, segment not faster", "分段点：累积时间不快于参考，单段时间不快于 PB" },
        { "Stage reached: cumulative not faster, segment faster", "分段点：累积时间不快于参考，单段时间快于 PB" },
        { "Stage reached: cumulative faster, segment not faster", "分段点：累积时间快于参考，单段时间不快于 PB" },
        { "Stage reached: cumulative faster, segment faster", "分段点：累积时间快于参考，单段时间快于 PB" },
        { "Moon Lord: cumulative not faster, segment not faster", "月亮领主：累积时间不快于参考，单段时间不快于 PB" },
        { "Moon Lord: cumulative not faster, segment faster", "月亮领主：累积时间不快于参考，单段时间快于 PB" },
        { "Moon Lord: cumulative faster, segment not faster", "月亮领主：累积时间快于参考，单段时间不快于 PB" },
        { "Moon Lord: cumulative faster, segment faster", "月亮领主：累积时间快于参考，单段时间快于 PB" },
        { "Choose sound", "选择声音" },
        { "Clear", "清空" },
        { "BOSS Groups", "组" },
        { "BOSS route", "BOSS 顺序" },
        { "BOSS", "BOSS" },
        { "BOSS Group", "组" },
        { "Enabled", "启用" },
        { "Attached", "附属" },
        { "Segment", "分段" },
        { "Group", "组" },
        { "Settings...", "设置..." },
        { "Switch config", "切换配置" },
        { "No config files", "没有配置文件" },
        { "Statistics...", "统计信息..." },
        { "Statistics", "统计信息" },
        { "Last run", "上一局" },
        { "Best split", "历史最佳累积时间" },
        { "Fastest segment", "最快单段时间" },
        { "No splits", "没有分段" },
        { "Exit", "退出" },
        { "Edit total time", "编辑总计时间" },
        { "Edit split time", "编辑累积时间" },
        { "Target library", "目标库" },
        { "Candidates", "候选" },
        { "Conditions", "条件" },
        { "Condition", "条件" },
        { "Search boss or item name / id", "搜索 BOSS 或物品名称 / ID" },
        { "Search target name / id", "搜索目标名称 / ID" },
        { "Display name", "显示名称" },
        { "Type", "类型" },
        { "Search", "搜索" },
        { "Match", "匹配" },
        { "Satisfy", "满足" },
        { "Conditions suffix", "条件" },
        { "At least", "至少" },
        { "At least {0}", "至少 {0} 个" },
        { "At least count", "至少数量" },
        { "Quantity", "数量" },
        { "Advanced condition", "高级编辑" },
        { "Condition syntax hint", "使用 ALL(...) 表示全部满足；使用 ATLEAST(N, ...) 表示至少 N 个满足。目标格式为 类型:ID，例如 Boss:skeletron、Item:50、NPC:17、Biome:Jungle；数量写作 Item:50 >= 2。" },
        { "Switch to advanced", "切换至高级模式" },
        { "Switch to basic", "切换至普通模式" },
        { "Invalid advanced condition.", "高级条件格式无效。" },
        { "Advanced condition cannot be converted to basic editor without losing structure.", "当前高级条件无法无损转换为普通编辑模式。" },
        { "Icon file", "图标文件" },
        { "Custom image", "自定义图片" },
        { "Item", "物品" },
        { "NPC", "NPC" },
        { "Biome", "群系" },
        { "All", "全部" },
        { "Any", "任一" },
        { "Select a target first.", "请先选择目标。" },
        { "Select a split first.", "请先选择分段。" },
        { "Route must contain at least one split.", "路线至少需要一个分段。" },
        { "Every split needs an id.", "每个分段都需要内部 ID。" },
        { "Duplicate split id: {0}", "分段内部 ID 重复：{0}" },
        { "Condition group must use AtLeast.", "条件匹配方式必须是至少 N 个。" },
        { "Condition group cannot be empty.", "条件不能为空。" },
        { "Match count must be at least 1.", "匹配数量至少为 1。" },
        { "Match count cannot exceed condition count.", "匹配数量不能超过条件数量。" },
        { "Icon target must be in condition.", "图标目标必须来自当前条件。" },
        { "Custom icon file is required.", "自定义图标需要选择图片文件。" },
        { "Unknown condition group.", "未知条件组。" },
        { "Nested condition groups are not supported.", "不支持嵌套条件组。" },
        { "Unknown fact.", "未知条件。" },
        { "Item quantity must be at least 1.", "物品数量至少为 1。" },
        { "King Slime", "史莱姆王" },
        { "Eye of Cthulhu", "克苏鲁之眼" },
        { "Eater of Worlds", "世界吞噬怪" },
        { "Brain of Cthulhu", "克苏鲁之脑" },
        { "Queen Bee", "蜂王" },
        { "Skeletron", "骷髅王" },
        { "Deerclops", "独眼巨鹿" },
        { "Wall of Flesh", "血肉墙" },
        { "Queen Slime", "史莱姆皇后" },
        { "Destroyer", "毁灭者" },
        { "Skeletron Prime", "机械骷髅王" },
        { "The Twins", "双子魔眼" },
        { "Plantera", "世纪之花" },
        { "Golem", "石巨人" },
        { "Duke Fishron", "猪龙鱼公爵" },
        { "Empress of Light", "光之女皇" },
        { "Lunatic Cultist", "拜月教邪教徒" },
        { "Moon Lord", "月亮领主" },
        { "Mechanical Bosses", "机械三王" }
      , { "Virtual background", "\u865A\u62DF\u80CC\u666F" }
      , { "Common", "\u901A\u7528" }
      , { "Mouse passthrough indicator", "\u7A7F\u900F\u6307\u793A" }
      , { "Special Options", "\u7279\u6B8A\u9009\u9879" }
      , { "Delta gradient", "\u8BEF\u5DEE\u6E10\u53D8" }
      , { "Delta time gradient", "\u5DEE\u503C\u65F6\u95F4\u6E10\u53D8" }
      , { "Historical delta", "\u5386\u6B21\u5DEE\u503C" }
      , { "Dynamic delta time units", "\u5DEE\u503C\u65F6\u95F4\u5355\u4F4D\u52A8\u6001\u8C03\u6574" }
      , { "Enabled (Delta)", "\u542F\u7528\uff08\u5DEE\u503C\uff09" }
      , { "Enabled (Historical delta)", "\u542F\u7528\uff08\u5386\u6B21\u5DEE\u503C\uff09" }
      , { "Enabled (Current delta)", "\u542F\u7528\uff08\u5F53\u524D\u5DEE\u503C\uff09" }
      , { "Enabled (Main timer)", "\u542F\u7528\uff08\u4E3B\u8BA1\u65F6\u5668\uff09" }
      , { "Threshold time", "\u9608\u503C\u65F6\u95F4" }
      , { "Gradient mode", "\u6E10\u53D8\u65B9\u5F0F" }
      , { "Linear", "\u7EBF\u6027" }
      , { "Smooth", "\u5E73\u6ED1" }
      , { "Hard step", "\u786C\u7A81\u53D8" }
      , { "Soft step", "\u67D4\u7A81\u53D8" }
      , { "Auto Create", "\u81EA\u52A8\u521B\u5EFA" }
      , { "Create World", "\u521B\u5EFA\u4E16\u754C" }
      , { "Enter World", "\u8FDB\u56FE" }
      , { "Load World", "\u52A0\u8F7D\u4E16\u754C" }
      , { "World Selector", "\u4E16\u754C\u9009\u62E9" }
      , { "Save Selector", "\u5B58\u6863\u9009\u62E9" }
      , { "Press ESC to exit", "\u6309ESC\u9000\u51FA" }
      , { "Practice world", "\u7EC3\u4E60\u4E16\u754C" }
      , { "Key", "\u6309\u952E" }
      , { "Name", "\u540D\u79F0" }
      , { "Player file", "\u4EBA\u7269\u6587\u4EF6" }
      , { "World file", "\u4E16\u754C\u6587\u4EF6" }
      , { "Choose player file", "\u9009\u62E9\u4EBA\u7269\u6587\u4EF6" }
      , { "Choose world file", "\u9009\u62E9\u4E16\u754C\u6587\u4EF6" }
      , { "Not configured", "\u672A\u914D\u7F6E" }
      , { "Create World creates a world automatically. Load World copies the selected player and world saves, then opens Single Player.", "\u521B\u5EFA\u4E16\u754C\u4F1A\u81EA\u52A8\u521B\u5EFA\u4E16\u754C\uFF1B\u52A0\u8F7D\u4E16\u754C\u4F1A\u590D\u5236\u9009\u4E2D\u7684\u4EBA\u7269\u548C\u4E16\u754C\u5B58\u6863\uFF0C\u7136\u540E\u6253\u5F00\u5355\u4EBA\u6E38\u620F\u3002" }
      , { "Create World creates a world automatically by simulating mouse and keyboard input.", "\u521B\u5EFA\u4E16\u754C\u4F1A\u901A\u8FC7\u6A21\u62DF\u9F20\u6807\u548C\u952E\u76D8\u64CD\u4F5C\u81EA\u52A8\u521B\u5EFA\u4E16\u754C\u3002" }
      , { "Load World copies the selected player and/or world files to Terraria's save folder, then opens Single Player.", "\u52A0\u8F7D\u4E16\u754C\u4F1A\u590D\u5236\u9009\u4E2D\u7684\u4EBA\u7269\u6216\u4E16\u754C\u6587\u4EF6\u5230\u6CF0\u62C9\u745E\u4E9A\u5B58\u6863\u6587\u4EF6\u5939\uFF0C\u7136\u540E\u6253\u5F00\u5355\u4EBA\u6E38\u620F\u3002" }
      , { "Create World deletes all non-favorite players and worlds.", "\u521B\u5EFA\u4E16\u754C\u4F1A\u5220\u9664\u6240\u6709\u975E\u6536\u85CF\u7684\u4EBA\u7269\u548C\u4E16\u754C\u3002" }
      , { "Automatically creates or enters a world by simulating mouse and keyboard input.", "\u901A\u8FC7\u6A21\u62DF\u9F20\u6807\u548C\u952E\u76D8\u64CD\u4F5C\u81EA\u52A8\u521B\u5EFA\u6216\u8FDB\u5165\u4E16\u754C\u3002" }
      , { "Do not choose players or worlds in the default save location.", "\u8BF7\u4E0D\u8981\u9009\u62E9\u5728\u9ED8\u8BA4\u5B58\u6863\u4F4D\u7F6E\u7684\u4EBA\u7269\u6216\u4E16\u754C\u3002" }
      , { "Do not choose favorite players or worlds.", "\u8BF7\u4E0D\u8981\u9009\u62E9\u6536\u85CF\u7684\u4EBA\u7269\u6216\u4E16\u754C\u3002" }
      , { "Deletes all non-favorite players and worlds.", "\u4F1A\u5220\u9664\u6240\u6709\u975E\u6536\u85CF\u7684\u4EBA\u7269\u548C\u4E16\u754C\u3002" }
      , { "The most recent 50 deletions are kept in the backup folder.", "\u6700\u8FD150\u6B21\u5220\u9664\u4F1A\u4FDD\u7559\u5728\u5907\u4EFD\u6587\u4EF6\u5939\u4E2D\u3002" }
      , { "Open folder", "\u6253\u5F00\u6587\u4EF6\u5939" }
      , { "Could not open backup folder.", "\u65E0\u6CD5\u6253\u5F00\u5907\u4EFD\u6587\u4EF6\u5939\u3002" }
      , { "Timing", "\u65F6\u95F4" }
      , { "Delay", "\u5EF6\u8FDF" }
      , { "Character", "\u4EBA\u7269" }
      , { "Player options", "\u4EBA\u7269\u9009\u9879" }
      , { "Player name", "\u4EBA\u7269\u540D\u79F0" }
      , { "Player difficulty", "\u4EBA\u7269\u96BE\u5EA6" }
      , { "Player code", "\u4EBA\u7269\u4EE3\u7801" }
      , { "World", "\u4E16\u754C" }
      , { "World options", "\u4E16\u754C\u9009\u9879" }
      , { "World size", "\u4E16\u754C\u5927\u5C0F" }
      , { "World difficulty", "\u4E16\u754C\u96BE\u5EA6" }
      , { "World evil", "\u90AA\u6076\u7C7B\u578B" }
      , { "World seed / secret seed", "\u4E16\u754C\u79CD\u5B50 / \u79D8\u5BC6\u79CD\u5B50" }
      , { "Special seeds", "\u5F69\u86CB\u79CD\u5B50" }
      , { "Secret seeds", "\u79D8\u5BC6\u79CD\u5B50" }
      , { "Zenith star catch", "\u5929\u9876\u63A5\u661F" }
      , { "Stop after stage", "\u5728\u4EE5\u4E0B\u9636\u6BB5\u540E\u505C\u6B62" }
      , { "Catch speed", "\u63A5\u661F\u901F\u5EA6" }
      , { "Filter pyramid", "\u7B5B\u9009\u91D1\u5B57\u5854" }
      , { "Pyramid filter", "\u7B5B\u9009\u91D1\u5B57\u5854" }
      , { "Quick pyramid filter", "\u7B5B\u5854" }
      , { "Required pyramid items", "\u6307\u5B9A\u7269\u54C1" }
      , { "Return to main menu on filter failure", "\u7B5B\u9009\u5931\u8D25\u8FD4\u56DE\u4E3B\u9875\u91CD\u65B0\u521B\u5EFA" }
      , { "Sandstorm in a Bottle", "\u6C99\u66B4\u74F6" }
      , { "Flying Carpet", "\u98DE\u6BEF" }
      , { "Pharaoh set", "\u6CD5\u8001\u5957" }
      , { "Background world generation", "\u540E\u53F0\u5EFA\u56FE" }
      , { "Background world pool", "\u540E\u53F0\u9884\u5EFA\u4E16\u754C\u6C60" }
      , { "World pool size", "\u4E16\u754C\u6C60\u4E2A\u6570" }
      , { "Install pooled world", "\u5B89\u88C5\u4E16\u754C\u6C60\u4E16\u754C" }
      , { "Stop at world select", "\u505C\u5728\u4E16\u754C\u9009\u62E9\u754C\u9762" }
      , { "Apply visible seed", "\u5E94\u7528\u53EF\u89C1\u79CD\u5B50" }
      , { "Submit World Seed", "\u63D0\u4EA4\u4E16\u754C\u79CD\u5B50" }
      , { "Mouse / key press ms", "\u9F20\u6807 / \u6309\u952E\u6301\u7EED\u6BEB\u79D2" }
      , { "Window activation wait ms", "\u7A97\u53E3\u6FC0\u6D3B\u7B49\u5F85\u6BEB\u79D2" }
      , { "Click focus wait ms", "\u70B9\u51FB\u805A\u7126\u7B49\u5F85\u6BEB\u79D2" }
      , { "Short action delay ms", "\u77ED\u64CD\u4F5C\u5EF6\u8FDF\u6BEB\u79D2" }
      , { "Menu action delay ms", "\u83DC\u5355\u64CD\u4F5C\u5EF6\u8FDF\u6BEB\u79D2" }
      , { "Mouse / key duration ms", "\u9F20\u6807/\u6309\u952E\u6301\u7EED\u65F6\u95F4\uFF08ms\uFF09" }
      , { "Initial wait ms", "\u521D\u59CB\u7B49\u5F85\u65F6\u95F4\uFF08ms\uFF09" }
      , { "Pre-click wait ms", "\u70B9\u51FB\u524D\u7B49\u5F85\u65F6\u95F4\uFF08ms\uFF09" }
      , { "Adjacent operation delay ms", "\u76F8\u90BB\u64CD\u4F5C\u95F4\u7B49\u5F85\u65F6\u95F4\uFF08ms\uFF09" }
      , { "Cross-menu operation delay ms", "\u8DE8\u83DC\u5355\u64CD\u4F5C\u7B49\u5F85\u65F6\u95F4\uFF08ms\uFF09" }
      , { "Pyramid filter post wait ms", "\u7B5B\u5854\u540E\u7B49\u5F85\u65F6\u95F4\uFF08ms\uFF09" }
      , { "Empty = default character", "\u7559\u7A7A = \u9ED8\u8BA4\u4EBA\u7269" }
      , { "Empty = 1", "\u7559\u7A7A = 1" }
      , { "Empty = random visible seed", "\u7559\u7A7A = \u968F\u673A\u53EF\u89C1\u79CD\u5B50" }
      , { "Empty = none", "\u7559\u7A7A=\u65E0" }
      , { "Empty = none; submitted exactly as typed", "\u7559\u7A7A = \u65E0\uFF1B\u81EA\u52A8\u5316\u4F1A\u6309\u8F93\u5165\u5185\u5BB9\u539F\u6837\u63D0\u4EA4" }
      , { "Softcore", "\u8F6F\u6838" }
      , { "Mediumcore", "\u4E2D\u6838" }
      , { "Hardcore", "\u786C\u6838" }
      , { "Journey", "\u65C5\u884C" }
      , { "Small", "\u5C0F" }
      , { "Medium", "\u4E2D" }
      , { "Large", "\u5927" }
      , { "Classic", "\u7ECF\u5178" }
      , { "Expert", "\u4E13\u5BB6" }
      , { "Master", "\u5927\u5E08" }
      , { "Random", "\u968F\u673A" }
      , { "Corruption", "\u8150\u5316" }
      , { "Crimson", "\u7329\u7EA2" }
      , { "Not the Bees", "\u4E0D\u662F\u871C\u8702" }
      , { "Drunk", "\u9189\u9152" }
      , { "Celebration Mk 10", "\u5341\u5468\u5E74\u5E86\u5178" }
      , { "The Constant", "\u6C38\u6052\u9886\u57DF" }
      , { "For the Worthy", "For the Worthy" }
      , { "No Traps", "\u65E0\u9677\u9631" }
      , { "Remix", "Remix" }
      , { "Zenith", "\u5929\u9876" }
      , { "Skyblock", "\u7A7A\u5C9B" }
      , { "Life Crystals", "\u751F\u547D\u6C34\u6676" }
      , { "Statues", "\u96D5\u50CF" }
      , { "Buried Chests", "\u57CB\u85CF\u7BB1" }
      , { "Gem Caves", "\u5B9D\u77F3\u6D1E" }
      , { "Pots", "\u7F50\u5B50" }
      , { "Traps", "\u9677\u9631" }
      , { "Debug", "\u8C03\u8BD5" }
      , { "Copy all information", "\u590D\u5236\u6240\u6709\u4FE1\u606F" }
      , { "Quick Status", "\u5FEB\u901F\u72B6\u6001" }
      , { "Window & Coordinates", "\u7A97\u53E3\u4E0E\u5750\u6807" }
      , { "Auto Create Route", "\u81EA\u52A8\u521B\u56FE\u8DEF\u7EBF" }
      , { "Boss Progress", "BOSS \u8FDB\u5EA6" }
      , { "World Generation", "\u521B\u4E16\u754C\u72B6\u6001" }
      , { "Memory & Signatures", "\u5185\u5B58\u4E0E\u7B7E\u540D" }
      , { "Catch stars", "\u63A5\u661F\u661F" }
      , { "Catch stars through", "\u63A5\u661F\u661F\u76F4\u5230" }
      , { "Performance", "\u6027\u80FD" }
      , { "Window Detection", "\u7A97\u53E3\u68C0\u6D4B" }
      , { "Watcher State", "\u76D1\u6D4B\u72B6\u6001" }
      , { "Last updated", "\u6700\u8FD1\u66F4\u65B0" }
      , { "Terraria process", "\u6CF0\u62C9\u8FDB\u7A0B" }
      , { "PID", "\u8FDB\u7A0B ID" }
      , { "Start time", "\u542F\u52A8\u65F6\u95F4" }
      , { "Window", "\u7A97\u53E3" }
      , { "Window handle", "\u7A97\u53E3\u53E5\u67C4" }
      , { "Window title", "\u7A97\u53E3\u6807\u9898" }
      , { "Responding", "\u54CD\u5E94\u4E2D" }
      , { "Visible", "\u53EF\u89C1" }
      , { "Minimized", "\u5DF2\u6700\u5C0F\u5316" }
      , { "Maximized", "\u5DF2\u6700\u5927\u5316" }
      , { "Foreground", "\u524D\u53F0\u7A97\u53E3" }
      , { "Window bounds", "\u7A97\u53E3\u8303\u56F4" }
      , { "Client size", "\u5BA2\u6237\u533A\u5927\u5C0F" }
      , { "Watcher attached", "\u5DF2\u9644\u52A0\u76D1\u6D4B" }
      , { "Memory ready", "\u5185\u5B58\u5C31\u7EEA" }
      , { "Boss flags ready", "BOSS \u6807\u8BB0\u5C31\u7EEA" }
      , { "Game state", "\u6E38\u620F\u72B6\u6001" }
      , { "UI paint", "UI \u7ED8\u5236\u9891\u7387" }
      , { "configured {0}, actual {1}", "\u914D\u7F6E {0}\uFF0C\u5B9E\u9645 {1}" }
      , { "configured {0}, actual {1}, avg {2}, max {3}", "\u914D\u7F6E {0}\uFF0C\u5B9E\u9645 {1}\uFF0C\u5E73\u5747 {2}\uFF0C\u6700\u5927 {3}" }
      , { "configured {0}, waiting {1}", "\u914D\u7F6E {0}\uFF0C{1}" }
      , { "configured {0}, actual {1}, avg {2}, max {3}, jitter {4}", "\u914D\u7F6E {0}\uFF0C\u5B9E\u9645 {1}\uFF0C\u5E73\u5747 {2}\uFF0C\u6700\u5927 {3}\uFF0C\u6296\u52A8 {4}" }
      , { "Waiting for samples", "\u7B49\u5F85\u91C7\u6837" }
      , { "Waiting for attached memory", "\u7B49\u5F85\u9644\u52A0\u5185\u5B58" }
      , { "Waiting for timer start", "\u7B49\u5F85\u8BA1\u65F6\u5F00\u59CB" }
      , { "Process architecture", "\u8FDB\u7A0B\u67B6\u6784" }
      , { "Process path", "\u8FDB\u7A0B\u8DEF\u5F84" }
      , { "Process version", "\u8FDB\u7A0B\u7248\u672C" }
      , { "Main module base", "\u4E3B\u6A21\u5757\u57FA\u5740" }
      , { "Main module size", "\u4E3B\u6A21\u5757\u5927\u5C0F" }
      , { "Scan attempts", "\u626B\u63CF\u6B21\u6570" }
      , { "Last scan", "\u6700\u8FD1\u626B\u63CF" }
      , { "Scan page stats", "\u5185\u5B58\u9875\u626B\u63CF\u7EDF\u8BA1" }
      , { "Scan failures", "\u626B\u63CF\u5931\u8D25\u7EDF\u8BA1" }
      , { "UpdateTime address", "UpdateTime \u5730\u5740" }
      , { "Boss flags address", "Boss \u6807\u8BB0\u5730\u5740" }
      , { "Hardmode address", "Hardmode \u5730\u5740" }
      , { "Current pass", "\u5F53\u524D\u9636\u6BB5" }
      , { "Progress message", "\u8FDB\u5EA6\u6587\u6848" }
      , { "Current progress", "\u5F53\u524D\u8FDB\u5EA6" }
      , { "Total progress", "\u603B\u8FDB\u5EA6" }
      , { "Generation progress address", "\u521B\u4E16\u754C\u8FDB\u5EA6\u5730\u5740" }
      , { "Generation controller address", "\u521B\u4E16\u754C\u63A7\u5236\u5668\u5730\u5740" }
      , { "Failure stage", "\u5931\u8D25\u9636\u6BB5" }
      , { "Current seed", "\u5F53\u524D\u79CD\u5B50" }
      , { "world generation pointers pending", "\u521B\u4E16\u754C\u6307\u9488\u7B49\u5F85\u4E2D" }
      , { "world generation pointers pending via fallback", "\u901A\u8FC7\u56DE\u9000\u65B9\u5F0F\u7B49\u5F85\u521B\u4E16\u754C\u6307\u9488" }
      , { "timer and boss pointers ready; world generation scan pending", "\u8BA1\u65F6\u5668\u4E0E BOSS \u6307\u9488\u5C31\u7EEA\uFF1B\u521B\u4E16\u754C\u626B\u63CF\u7B49\u5F85\u4E2D" }
      , { "timer and boss pointers ready via fallback; world generation scan pending", "\u901A\u8FC7\u56DE\u9000\u65B9\u5F0F\u5C31\u7EEA\u8BA1\u65F6\u5668\u4E0E BOSS \u6307\u9488\uFF1B\u521B\u4E16\u754C\u626B\u63CF\u7B49\u5F85\u4E2D" }
      , { "Status", "\u72B6\u6001" }
      , { "Window status", "\u7A97\u53E3\u72B6\u6001" }
      , { "Menu scale", "\u83DC\u5355\u7F29\u653E" }
      , { "Logical menu size", "\u903B\u8F91\u83DC\u5355\u5C3A\u5BF8" }
      , { "Player files", "\u4EBA\u7269\u6587\u4EF6\u6570" }
      , { "World files", "\u4E16\u754C\u6587\u4EF6\u6570" }
      , { "Favorite players", "\u6536\u85CF\u4EBA\u7269\u6570" }
      , { "Favorite worlds", "\u6536\u85CF\u4E16\u754C\u6570" }
      , { "Click sequence", "\u70B9\u51FB\u987A\u5E8F" }
      , { "Single Player", "\u5355\u4EBA\u6E38\u620F" }
      , { "New Player", "\u65B0\u5EFA\u4EBA\u7269" }
      , { "Character Clothing Tab", "\u4EBA\u7269\u5916\u89C2\u9875" }
      , { "Paste Player Template", "\u7C98\u8D34\u4EBA\u7269\u6A21\u677F" }
      , { "Character Info Tab", "\u4EBA\u7269\u4FE1\u606F\u9875" }
      , { "Create Player", "\u521B\u5EFA\u4EBA\u7269" }
      , { "Select Created Player", "\u9009\u62E9\u65B0\u5EFA\u4EBA\u7269" }
      , { "New World", "\u65B0\u5EFA\u4E16\u754C" }
      , { "Advanced Seed", "\u9AD8\u7EA7\u79CD\u5B50" }
      , { "Randomize Visible Seed", "\u968F\u673A\u53EF\u89C1\u79CD\u5B50" }
      , { "Unavailable because client size is unknown.", "\u7531\u4E8E\u5BA2\u6237\u533A\u5927\u5C0F\u672A\u77E5\uFF0C\u65E0\u6CD5\u8BA1\u7B97\u3002" }
      , { "Yes", "\u662F" }
      , { "No", "\u5426" }
      , { "Unknown", "\u672A\u77E5" }
      , { "Empty", "\u7A7A" }
      , { "Not on world creation page", "\u4E0D\u5728\u521B\u4E16\u754C\u9875\u9762" }
      , { "World generation idle", "\u5F53\u524D\u672A\u5728\u521B\u4E16\u754C" }
      , { "Ready", "\u5C31\u7EEA" }
      , { "Pending", "\u7B49\u5F85\u4E2D" }
      , { "Missing", "\u672A\u5339\u914D" }
      , { "In menu", "\u83DC\u5355\u4E2D" }
      , { "In world", "\u4E16\u754C\u5185" }
      , { "count {0}, last {1}, avg {2}, max {3}", "\u6B21\u6570 {0}\uFF0C\u6700\u8FD1 {1}\uFF0C\u5E73\u5747 {2}\uFF0C\u6700\u9AD8 {3}" }
      , { "private {0}/{1} scanned, {2} read; image {3}/{4} scanned, {5} read; total {6}; {7}", "\u79C1\u6709\u9875\u5DF2\u626B {0}/{1}\uFF0C\u8BFB\u53D6 {2}\uFF1B\u6620\u50CF\u9875\u5DF2\u626B {3}/{4}\uFF0C\u8BFB\u53D6 {5}\uFF1B\u603B\u8BA1 {6}\uFF1B{7}" }
      , { "read failures {0}, oversized skipped {1}", "\u8BFB\u53D6\u5931\u8D25 {0}\uFF0C\u8DF3\u8FC7\u8FC7\u5927\u9875 {1}" }
      , { "Matched at {0}", "\u5339\u914D\u4E8E {0}" }
      , { "waiting for Terraria.exe", "\u7B49\u5F85 Terraria.exe" }
      , { "Terraria process changed while reading window state: {0}", "\u8BFB\u53D6\u7A97\u53E3\u72B6\u6001\u65F6 Terraria \u8FDB\u7A0B\u5DF2\u53D8\u5316\uFF1A{0}" }
      , { "cannot read Terraria process: {0}", "\u65E0\u6CD5\u8BFB\u53D6 Terraria \u8FDB\u7A0B\uFF1A{0}" }
      , { "cannot attach to Terraria process: {0}", "\u65E0\u6CD5\u9644\u52A0\u5230 Terraria \u8FDB\u7A0B\uFF1A{0}" }
      , { "attached to Terraria PID {0}", "\u5DF2\u9644\u52A0\u5230 Terraria\uFF0CPID {0}" }
      , { "attached to Terraria PID {0}, {1}", "\u5DF2\u9644\u52A0\u5230 Terraria\uFF0CPID {0}\uFF0C{1}" }
      , { "attached to Terraria process", "\u5DF2\u9644\u52A0\u5230 Terraria \u8FDB\u7A0B" }
      , { "attached to Terraria process, {0}", "\u5DF2\u9644\u52A0\u5230 Terraria \u8FDB\u7A0B\uFF0C{0}" }
      , { "process detected, main window not ready", "\u5DF2\u68C0\u6D4B\u5230\u8FDB\u7A0B\uFF0C\u4E3B\u7A97\u53E3\u5C1A\u672A\u5C31\u7EEA" }
      , { "window handle 0x{0}, client rect unavailable", "\u7A97\u53E3\u53E5\u67C4 0x{0}\uFF0C\u5BA2\u6237\u533A\u77E9\u5F62\u4E0D\u53EF\u7528" }
      , { "window handle 0x{0}", "\u7A97\u53E3\u53E5\u67C4 0x{0}" }
      , { "scanning for {0} memory", "\u6B63\u5728\u626B\u63CF {0} \u5185\u5B58" }
      , { "lost menu-state pointer; rescanning", "\u83DC\u5355\u72B6\u6001\u6307\u9488\u5DF2\u4E22\u5931\uFF0C\u6B63\u5728\u91CD\u65B0\u626B\u63CF" }
      , { "menu-state pointer became unreadable", "\u83DC\u5355\u72B6\u6001\u6307\u9488\u53D8\u4E3A\u4E0D\u53EF\u8BFB" }
      , { "found signature but not menu-state pointer", "\u5DF2\u627E\u5230\u7B7E\u540D\uFF0C\u4F46\u672A\u627E\u5230\u83DC\u5355\u72B6\u6001\u6307\u9488" }
      , { "waiting for UpdateTime signature", "\u7B49\u5F85 UpdateTime \u7B7E\u540D" }
      , { "ready via fallback", "\u5DF2\u901A\u8FC7\u56DE\u9000\u65B9\u6848\u5C31\u7EEA" }
      , { "ready via gameMenu fallback", "\u5DF2\u901A\u8FC7\u83DC\u5355\u72B6\u6001\u56DE\u9000\u65B9\u6848\u5C31\u7EEA" }
      , { "ready via boss fallback", "\u5DF2\u901A\u8FC7 Boss \u56DE\u9000\u65B9\u6848\u5C31\u7EEA" }
      , { "timer ready via fallback", "\u8BA1\u65F6\u5668\u5DF2\u901A\u8FC7\u56DE\u9000\u65B9\u6848\u5C31\u7EEA" }
      , { "timer ready via fallback; boss scan pending", "\u8BA1\u65F6\u5668\u5DF2\u901A\u8FC7\u56DE\u9000\u65B9\u6848\u5C31\u7EEA\uFF1BBoss \u626B\u63CF\u5F85\u5B8C\u6210" }
      , { "boss pointers pending", "Boss \u6307\u9488\u5F85\u89E3\u6790" }
      , { "boss scan pending", "Boss \u626B\u63CF\u5F85\u5B8C\u6210" }
      , { "return to menu once to arm timer start", "\u8BF7\u5148\u8FD4\u56DE\u4E00\u6B21\u4E3B\u83DC\u5355\u4EE5\u6FC0\u6D3B\u8BA1\u65F6\u5F00\u59CB" }
      , { "waiting for process", "\u7B49\u5F85\u8FDB\u7A0B" }
      , { "scanning for signature", "\u6B63\u5728\u626B\u63CF\u7B7E\u540D" }
      , { "cannot read process", "\u65E0\u6CD5\u8BFB\u53D6\u8FDB\u7A0B" }
      , { "cannot attach process", "\u65E0\u6CD5\u9644\u52A0\u8FDB\u7A0B" }
      , { "menu state pointer lost", "\u83DC\u5355\u72B6\u6001\u6307\u9488\u5DF2\u4E22\u5931" }
      , { "menu state target unreadable", "\u83DC\u5355\u72B6\u6001\u76EE\u6807\u4E0D\u53EF\u8BFB" }
      , { "menu state pointer unreadable", "\u83DC\u5355\u72B6\u6001\u6307\u9488\u4E0D\u53EF\u8BFB" }
      , { "signature missing", "\u672A\u627E\u5230\u7B7E\u540D" }
      , { "start pending", "\u7B49\u5F85\u5F00\u59CB" }
      , { "UpdateTime x86-style signature with menu-state and boss progression fallbacks", "UpdateTime x86 \u98CE\u683C\u7B7E\u540D\uFF0C\u542B\u83DC\u5355\u72B6\u6001\u4E0E Boss \u8FDB\u5EA6\u56DE\u9000" }
      , { "Private executable pages, then image executable pages", "\u5148\u626B\u63CF\u79C1\u6709\u53EF\u6267\u884C\u9875\uFF0C\u518D\u626B\u63CF\u6620\u50CF\u53EF\u6267\u884C\u9875" }
      , { "Waiting for Terraria process.", "\u6B63\u5728\u7B49\u5F85 Terraria \u8FDB\u7A0B\u3002" }
      , { "Target Terraria process is x64. The current UpdateTime signature was authored from an x86-style function prologue.", "\u76EE\u6807 Terraria \u8FDB\u7A0B\u4E3A x64\u3002\u5F53\u524D UpdateTime \u7B7E\u540D\u662F\u6309 x86 \u98CE\u683C\u51FD\u6570\u524D\u5E8F\u7F16\u5199\u7684\u3002" }
      , { "Watcher first became ready while Terraria was already in a world. The timer starts only on a menu-to-world transition, so return to the main menu once and enter the world again.", "\u76D1\u6D4B\u5668\u9996\u6B21\u5C31\u7EEA\u65F6\uFF0CTerraria \u5DF2\u7ECF\u5904\u4E8E\u4E16\u754C\u5185\u3002\u8BA1\u65F6\u53EA\u4F1A\u5728\u4ECE\u83DC\u5355\u8FDB\u5165\u4E16\u754C\u7684\u5207\u6362\u65F6\u5F00\u59CB\uFF0C\u6240\u4EE5\u8BF7\u5148\u8FD4\u56DE\u4E3B\u83DC\u5355\u4E00\u6B21\uFF0C\u518D\u91CD\u65B0\u8FDB\u5165\u4E16\u754C\u3002" }
      , { "Fallback signatures resolved menu state and boss progression when the primary UpdateTime anchor was unavailable on this runtime.", "\u5F53\u524D\u8FD0\u884C\u65F6\u4E0B\u4E3B UpdateTime \u951A\u70B9\u4E0D\u53EF\u7528\uFF0C\u5DF2\u901A\u8FC7\u56DE\u9000\u7B7E\u540D\u89E3\u6790\u83DC\u5355\u72B6\u6001\u548C Boss \u8FDB\u5EA6\u3002" }
      , { "Fallback menu-state signature resolved a stronger UpdateTime-adjacent gameMenu access pattern when the direct UpdateTime anchor was unavailable on this runtime.", "\u5F53\u524D\u8FD0\u884C\u65F6\u4E0B\u76F4\u63A5 UpdateTime \u951A\u70B9\u4E0D\u53EF\u7528\uFF0C\u5DF2\u901A\u8FC7\u83DC\u5355\u72B6\u6001\u56DE\u9000\u7B7E\u540D\u89E3\u6790\u5230\u66F4\u7A33\u5B9A\u7684\u83DC\u5355\u72B6\u6001\u8BBF\u95EE\u6A21\u5F0F\u3002" }
      , { "Boss progression fallback resolved hardmode and boss flags when the UpdateTime-relative boss pointer offsets were unavailable.", "\u5F53\u76F8\u5BF9 UpdateTime \u7684 Boss \u6307\u9488\u504F\u79FB\u4E0D\u53EF\u7528\u65F6\uFF0C\u5DF2\u901A\u8FC7 Boss \u8FDB\u5EA6\u56DE\u9000\u89E3\u6790 hardmode \u4E0E Boss \u6807\u8BB0\u3002" }
      , { "UpdateTime did not match any scanned private or image executable page.", "UpdateTime \u672A\u5728\u4EFB\u4F55\u5DF2\u626B\u63CF\u7684\u79C1\u6709\u6216\u6620\u50CF\u53EF\u6267\u884C\u9875\u4E2D\u5339\u914D\u5230\u3002" }
      , { "UpdateTime matched, but the expected menu-state pointer offset did not resolve to readable memory.", "UpdateTime \u5DF2\u5339\u914D\uFF0C\u4F46\u9884\u671F\u7684\u83DC\u5355\u72B6\u6001\u6307\u9488\u504F\u79FB\u672A\u80FD\u89E3\u6790\u5230\u53EF\u8BFB\u5185\u5B58\u3002" }
      , { "gameMenu resolved, but boss and hardmode pointers are still pending or unreadable.", "\u83DC\u5355\u72B6\u6001\u5DF2\u89E3\u6790\uFF0C\u4F46 Boss \u4E0E hardmode \u6307\u9488\u4ECD\u5728\u7B49\u5F85\u89E3\u6790\u6216\u4E0D\u53EF\u8BFB\u3002" }
      , { "Watcher resolved all current pointers.", "\u76D1\u6D4B\u5668\u5DF2\u89E3\u6790\u5F53\u524D\u6240\u6709\u6307\u9488\u3002" }
    };

    public bool TryGet(string key, out string value)
    {
        bool found = Values.TryGetValue(key, out string? localizedValue);
        value = localizedValue ?? string.Empty;
        return found;
    }
}
