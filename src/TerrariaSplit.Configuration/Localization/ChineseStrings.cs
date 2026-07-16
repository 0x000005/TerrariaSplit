namespace TerrariaSplit.Localization;

internal sealed class ChineseStrings : ILocalizedStringProvider
{
    private static readonly Dictionary<string, string> Values = new(StringComparer.OrdinalIgnoreCase)
    {

        { "Initializing...", "正在初始化…" },
        { "Startup failed", "启动失败" },
        { "TerrariaSplit could not finish initialization and must close.", "TerrariaSplit 无法完成初始化，程序必须关闭。" },
        { "TerrariaSplit Settings", "TerrariaSplit 设置" },
        { "Settings cannot be saved while in a Race room.", "联机房间内不能保存设置，请先离开房间。" },
        { "OK", "确定" },
        { "Apply", "应用" },
        { "Cancel", "取消" },
        { "Minimize", "最小化" },
        { "Maximize", "最大化" },
        { "Restore", "还原" },
        { "Close", "关闭" },
        { "General", "常规" },
        { "Route", "路线" },
        { "Too many results", "结果过多" },
        { "Data", "数据" },
        { "UI", "界面" },
        { "Colors", "颜色" },
        { "Sounds", "声音" },
        { "Advanced", "高级" },
        { "About", "关于" },
        { "About TerrariaSplit", "关于 TerrariaSplit" },
        { "Application", "程序" },
        { "Version", "版本" },
        { "Updates", "更新" },
        { "Check for updates", "检查更新" },
        { "Click Check for updates to query GitHub.", "点击“检查更新”以查询 GitHub。" },
        { "Checking for updates...", "正在检查更新…" },
        { "TerrariaSplit is up to date.", "TerrariaSplit 已是最新版本。" },
        { "Version {0} is available.", "发现新版本 {0}。" },
        { "TerrariaSplit Update", "TerrariaSplit 更新" },
        { "Current version: {0}\nNew version: {1}\n\nDownload, install, and restart TerrariaSplit now?", "当前版本：{0}\n新版本：{1}\n\n是否立即下载、安装并重启 TerrariaSplit？" },
        { "Downloading update...", "正在下载更新…" },
        { "Downloading update... {0}%", "正在下载更新… {0}%" },
        { "Verifying update...", "正在校验更新…" },
        { "Update verified. Preparing to restart...", "更新已校验，正在准备重启…" },
        { "Update cancelled.", "更新已取消。" },
        { "Update could not be started.", "无法启动更新。" },
        { "Update failed: {0}", "更新失败：{0}" },
        { "Automation", "自动化" },
        { "Auto", "自动" },
        { "Fixed", "固定" },
        { "Terraria UI scale enhancement", "Terraria UI 缩放增强" },
        { "RTSS fullscreen projection", "RTSS 全屏投影" },
        { "Sampling frequency", "采样频率" },
        { "Control frequency", "控制频率" },
        { "Split timer refresh rate", "分段计时器刷新率" },
        { "Main timer refresh rate", "主计时器刷新率" },
        { "Raises Terraria's in-game UI scale slider limit from 200% to 300%.", "将游戏内 UI 缩放上限从 200% 提高到 300%。" },
        { "If Terraria's options menu was already opened before enabling, restart Terraria for the change to take effect.", "如果启用前已经打开过 Terraria 选项菜单，可能需要重启 Terraria 后生效。" },
        { "This changes the running Terraria process memory; enable with caution.", "该功能会修改正在运行的 Terraria 进程内存，启用需谨慎。" },
        { "Writes the timer to RivaTuner Statistics Server so RTSS can draw it over exclusive fullscreen Terraria.", "将计时写入 RivaTuner Statistics Server，让 RTSS 在独占全屏 Terraria 上绘制计时。" },
        { "RTSS executable", "RTSS 程序" },
        { "X position", "X 位置" },
        { "Y position", "Y 位置" },
        { "Zoom", "缩放" },
        { "Choose RTSS.exe explicitly. Empty paths are not auto-detected.", "请手动选择 RTSS.exe。留空不会自动查找。" },
        { "The projection shows only the main timer. Negative X/Y values anchor from the right or bottom edge.", "投影只显示主时间。X/Y 为负数时表示从右边或底边向内偏移。" },
        { "Zoom uses RTSS native text size: 1 = 100%, 8 = 800%. Pixel zoom stays at 1x to avoid a blurry second stretch.", "缩放使用 RTSS 原生文字大小：1 = 100%，8 = 800%。像素缩放固定 1x，避免二次拉伸导致模糊。" },
        { "RTSS must have Show On-Screen Display enabled and Application detection level set to Low or higher for Terraria.exe.", "RTSS 里需要打开 Show On-Screen Display，并且 Terraria.exe 的 Application detection level 要设为 Low 或更高。" },
        { "If RTSS is running as administrator, TerrariaSplit also needs administrator privileges.", "如果 RTSS 以管理员权限运行，TerrariaSplit 也需要管理员权限。" },
        { "Choose RTSS.exe", "选择 RTSS.exe" },
        { "RTSS executable|RTSS.exe|Applications|*.exe|All files|*.*", "RTSS 程序|RTSS.exe|应用程序|*.exe|所有文件|*.*" },
        { "RTSS executable is required when RTSS fullscreen projection is enabled.", "启用 RTSS 全屏投影时必须配置 RTSS 程序。" },
        { "RTSS zoom must be an integer from 1 to 8.", "RTSS 缩放必须填写 1 到 8 之间的整数。" },
        { "RTSS fullscreen projection requires RTSS.exe to be configured in Advanced options.", "RTSS 全屏投影需要先在高级选项里配置 RTSS.exe。" },
        { "Configured RTSS executable was not found. Choose RTSS.exe in Advanced options.", "配置的 RTSS 程序不存在。请在高级选项里重新选择 RTSS.exe。" },
        { "RTSS fullscreen projection cannot write to RTSS. Run TerrariaSplit with the same privileges as RTSS.", "RTSS 全屏投影无法写入 RTSS。请让 TerrariaSplit 使用和 RTSS 相同的权限运行。" },
        { "Hotkeys", "快捷键" },
        { "Hotkeys support a single key, or a Ctrl / Alt / Shift chord. Press Esc in a hotkey box to disable that shortcut.", "快捷键支持单键，或以 Ctrl / Alt / Shift 开始的组合键。在快捷键输入框中按 Esc 可禁用该快捷键。" },
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
        { "Create World hotkey {0} is now active. Please read the Create World notes in the Automation settings tab first. Enabling this blindly may move save files to the backup folder unless existing saves are kept.", "\u521B\u5EFA\u4E16\u754C\u5FEB\u6377\u952E {0} \u5DF2\u542F\u7528\u3002\u8BF7\u52A1\u5FC5\u5148\u9605\u8BFB\u8BBE\u7F6E\u81EA\u52A8\u5316\u9009\u9879\u5361\u4E2D\u5173\u4E8E\u521B\u5EFA\u4E16\u754C\u7684\u6709\u5173\u63D0\u793A\uFF0C\u76F2\u76EE\u542F\u7528\u53EF\u80FD\u4F1A\u628A\u5B58\u6863\u6587\u4EF6\u79FB\u5230\u5907\u4EFD\u6587\u4EF6\u5939\uFF0C\u9664\u975E\u5DF2\u542F\u7528\u4FDD\u7559\u5B58\u6863\u3002" },
        { "Always on top", "置顶显示" },
        { "Practice mode", "练习模式" },
        { "Allow right-click time editing", "允许右键修改时间" },
        { "Columns", "列" },
        { "Split display", "分段计时器" },
        { "Global scale %", "全局缩放 %" },
        { "Column", "列名" },
        { "Spacing", "间距" },
        { "Show", "显示" },
        { "Width", "宽度" },
        { "Alignment", "对齐方式" },
        { "Left aligned", "左对齐" },
        { "Centered", "居中" },
        { "Right aligned", "右对齐" },
        { "Font", "字体大小" },
        { "Font family", "字体" },
        { "Size", "字号" },
        { "Bold", "粗体" },
        { "Italic", "斜体" },
        { "Icon", "图标" },
        { "Icon (attached)", "图标（附属）" },
        { "Name (attached)", "名称（附属）" },
        { "Flags", "标记" },
        { "Expand", "展开" },
        { "Main groups", "主要组" },
        { "Auto expand multi-condition main groups", "自动展开多条件主要组" },
        { "Collapse after completion", "完成后折叠" },
        { "Group count limit", "组数量限制" },
        { "Visible group count", "可见组数量" },
        { "Current group position", "当前组位置" },
        { "Always show final group", "始终显示最终组" },
        { "Remove limit after final group completion", "最终组完成后取消限制" },
        { "Uncollapse attached groups after final group completion", "最终组完成后取消折叠附属组" },
        { "Uncollapse multi-condition main groups after final group completion", "最终组完成后取消折叠多条件主要组" },
        { "Attached groups", "附属组" },
        { "Attached group", "附属组" },
        { "Attached group marker", "附属组" },
        { "Auto hide attached groups", "自动隐藏附属组" },
        { "Time", "时间" },
        { "Time (attached)", "时间（附属）" },
        { "Name (future stage)", "名称（未来阶段）" },
        { "Name (current stage)", "名称（当前阶段）" },
        { "Name (completed stage)", "名称（已完成阶段）" },
        { "Delta", "差值" },
        { "Delta time", "差值时间" },
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
        { "Auto update personal data", "自动更新个人数据" },
        { "Ask before updating personal data", "更新前确认" },
        { "Active file", "当前数据文件" },
        { "Update personal data?", "是否更新个人数据？" },
        { "Update", "更新" },
        { "Skip", "跳过" },
        { "UI Colors", "界面颜色" },
        { "Icon Colors", "图标颜色" },
        { "Icon type", "图标类型" },
        { "Icon outline", "图标描边" },
        { "Icon shadow", "图标阴影" },
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
        { "cumulative not faster, segment not faster", "累积时间不快于参考，单段时间不快于 PB" },
        { "cumulative not faster, segment faster", "累积时间不快于参考，单段时间快于 PB" },
        { "cumulative faster, segment not faster", "累积时间快于参考，单段时间不快于 PB" },
        { "cumulative faster, segment faster", "累积时间快于参考，单段时间快于 PB" },
        { "Final group", "最后一个组" },
        { ": ", "：" },
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
        { "Race...", "联机..." },
        { "Race", "联机" },
        { "Race leaderboard", "联机实时排行榜" },
        { "Leaderboard appearance", "排行榜界面" },
        { "Leaderboard colors", "排行榜颜色" },
        { "Use rank color for main timer", "主计时器使用排名颜色" },
        { "Rank gradient", "排名色带" },
        { "Start", "起点" },
        { "Middle", "中位点" },
        { "End", "终点" },
        { "Leaderboard rank column", "排名" },
        { "Leaderboard player column", "名字" },
        { "Leaderboard player self color", "名字：自己" },
        { "Leaderboard player other color", "名字：其他" },
        { "Leaderboard icon column", "图标" },
        { "Leaderboard time column", "时间" },
        { "Leaderboard display", "排行榜" },
        { "Nickname: self", "昵称：自己" },
        { "Nickname: other", "昵称：其他" },
        { "Apply UI settings", "应用界面设置" },
        { "Apply color settings", "应用颜色设置" },
        { "Apply leaderboard settings", "应用排行榜设置" },
        { "Settings saved.", "设置已保存。" },
        { "Leaderboard", "排行榜" },
        { "Race settings", "联机设置" },
        { "Voice", "语音" },
        { "Voice announcements", "语音播报" },
        { "System default", "系统默认" },
        { "Installed voice", "系统音色" },
        { "Speech speed", "语速" },
        { "Volume", "音量" },
        { "Preview", "试听" },
        { "Apply voice settings", "应用语音设置" },
        { "Connection", "连接" },
        { "Interface", "界面" },
        { "Server", "服务器" },
        { "Nickname", "昵称" },
        { "Role", "身份" },
        { "Host", "房主" },
        { "Member", "成员" },
        { "Select World", "选择世界" },
        { "Room settings", "房间设置" },
        { "Room Info", "房间详情" },
        { "Room code", "房间码" },
        { "Create room", "创建房间" },
        { "Close room", "关闭房间" },
        { "Join room", "加入房间" },
        { "Join room failed.", "加入房间失败。" },
        { "Leave room", "离开房间" },
        { "Seed and World", "种子和世界" },
        { "Seed", "种子" },
        { "Secret seed", "秘密种子" },
        { "Fixed seed", "固定种子" },
        { "Secret/fixed seed", "秘密种子/固定种子" },
        { "Special seed", "彩蛋种子" },
        { "World source", "世界来源" },
        { "Directly use world file", "直接使用世界文件" },
        { "Pyramid Filter", "金字塔" },
        { "All pyramid items", "所有金字塔物品" },
        { "Generate world", "生成世界" },
        { "Generate and upload", "生成世界并上传" },
        { "Generate random world", "随机生成世界" },
        { "Generate custom seed world", "自定义种子生成世界" },
        { "Upload", "上传世界" },
        { "Upload failed.", "上传失败。" },
        { "Journey player difficulty and Journey world difficulty must be selected together.", "人物难度和世界难度必须同时选择旅行模式，或同时不选择旅行模式。" },
        { "Leave", "离开" },
        { "Players", "玩家" },
        { "Rank", "排名" },
        { "Player", "玩家" },
        { "RNG control", "RNG 控制" },
        { "Enable RNG control", "启用 RNG 控制" },
        { "Server connection", "服务器连接" },
        { "Waiting", "等待中" },
        { "Creating", "创建中" },
        { "Downloading", "下载中" },
        { "Failed", "失败" },
        { "Enabling", "启用中" },
        { "Enable failed", "启用失败" },
        { "Not enabled", "未启用" },
        { "Connecting", "连接中" },
        { "Connected", "已连接" },
        { "Reconnecting", "正在重连" },
        { "Disconnected", "已断开" },
        { "Connection failed", "连接失败" },
        { "Completed", "完成数" },
        { "Latest split", "最新分段" },
        { "Race split time", "分段时间" },
        { "Gap", "差距" },
        { "Not in a race room", "未加入联机房间" },
        { "Room {0} / {1} / Host {2}", "房间 {0} / {1} / 房主 {2}" },
        { "Route: {0}", "路线：{0}" },
        { "Route not assigned", "未分配路线" },
        { "Lobby", "房间中" },
        { "Uploaded", "已上传" },
        { "Running", "进行中" },
        { "Closed", "已关闭" },
        { "Joined", "未就绪" },
        { "Not Ready", "未就绪" },
        { "Room created", "已创建房间" },
        { "Room joined", "已加入房间" },
        { "Room closed", "已关闭房间" },
        { "Copy Room Info", "复制房间信息" },
        { "Server: {0}", "服务器：{0}" },
        { "Room code: {0}", "房间号：{0}" },
        { "Room host route override hint", "所有成员都会使用你的路线与参考时间，如需进行调整，请先关闭房间，并于调整后重新开启房间。" },
        { "Room member route override hint", "在本房间内，计时器的路线与参考时间会被临时替换为房主所指定的路线与参考时间。" },
        { "Room operation restrictions hint", "在房间内，暂停、重置、时间编辑、设置、自动创图、加载世界和切换配置均被禁用。" },
        { "Room host restart hint", "只有房主可以重新开始。重新开始会让所有玩家返回主页，并重置人物文件、世界文件、计时进度和 RNG，重新准备一轮完整流程。" },
        { "Race Start", "开始" },
        { "Race Starting in {0}", "将在 {0} 秒后开始" },
        { "Race Starting...", "正在开始…" },
        { "Race Start failed.", "开始失败。" },
        { "Restart", "重新开始" },
        { "Restarting...", "正在重新准备…" },
        { "Restart failed.", "重新开始失败。" },
        { "Kick", "踢出" },
        { "No players", "暂无玩家" },
        { "World generated: {0}", "世界已生成：{0}" },
        { "World generated after {0} verified attempts: {1}", "世界已生成（二验尝试 {0} 次）：{1}" },
        { "Uploaded: {0} ({1})", "已上传：{0}（{1}）" },
        { "Uploaded: {0}", "已上传：{0}" },
        { "Room created and uploaded", "已创建房间并上传" },
        { "Regenerate and upload", "重新生成并上传" },
        { "Reupload", "重新上传" },
        { "Not uploaded", "尚未上传" },
        { "The host has not uploaded yet.", "房主尚未上传。" },
        { "World download failed.", "世界下载失败。" },
        { "A valid world file is required.", "需要选择有效的世界文件。" },
        { "Terraria world files", "泰拉瑞亚世界文件" },
        { "All files", "所有文件" },
        { "World file selection cancelled.", "已取消选择世界文件。" },
        { "World file selected: {0}", "已选择世界文件：{0}" },
        { "Left race room", "已离开联机房间" },
        { "{0}: {1}", "{0}：{1}" },
        { "Seed and world settings are required before generating the world.", "生成世界前需要先获得种子和世界设置。" },
        { "Prepare and upload before creating the room.", "请先准备并上传，然后创建房间。" },
        { "World generation failed.", "世界生成失败。" },
        { "World generation cancelled.", "世界生成已取消。" },
        { "Race server URL is required.", "需要填写联机服务器地址。" },
        { "Invalid race request.", "联机请求无效。" },
        { "Race room was not found.", "找不到联机房间。" },
        { "Race room is closed.", "联机房间已关闭。" },
        { "Nickname already exists in this room.", "该房间内已存在这个昵称。" },
        { "Player is not in this room.", "玩家不在该房间中。" },
        { "Only the room host can perform this action.", "只有房主可以执行此操作。" },
        { "Invalid race split report.", "联机分段上报无效。" },
        { "Join or create a race room before sending race updates.", "请先创建或加入联机房间。" },
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
        { "Name / Id", "名称 / Id" },
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
      , { "Player file", "\u73A9\u5BB6\u6587\u4EF6" }
      , { "World file", "\u4E16\u754C\u6587\u4EF6" }
      , { "Choose player file", "\u9009\u62E9\u4EBA\u7269\u6587\u4EF6" }
      , { "Choose world file", "\u9009\u62E9\u4E16\u754C\u6587\u4EF6" }
      , { "Not configured", "\u672A\u914D\u7F6E" }
      , { "Create World creates a world automatically. Load World copies the selected player and world saves, then opens Single Player.", "\u521B\u5EFA\u4E16\u754C\u4F1A\u81EA\u52A8\u521B\u5EFA\u4E16\u754C\uFF1B\u52A0\u8F7D\u4E16\u754C\u4F1A\u590D\u5236\u9009\u4E2D\u7684\u4EBA\u7269\u548C\u4E16\u754C\u5B58\u6863\uFF0C\u7136\u540E\u6253\u5F00\u5355\u4EBA\u6E38\u620F\u3002" }
      , { "Create World creates a world automatically by simulating mouse and keyboard input.", "\u521B\u5EFA\u4E16\u754C\u4F1A\u901A\u8FC7\u6A21\u62DF\u9F20\u6807\u548C\u952E\u76D8\u64CD\u4F5C\u81EA\u52A8\u521B\u5EFA\u4E16\u754C\u3002" }
      , { "Load World copies the selected player and/or world files to Terraria's save folder, then opens Single Player.", "\u52A0\u8F7D\u4E16\u754C\u4F1A\u590D\u5236\u9009\u4E2D\u7684\u4EBA\u7269\u6216\u4E16\u754C\u6587\u4EF6\u5230\u6CF0\u62C9\u745E\u4E9A\u5B58\u6863\u6587\u4EF6\u5939\uFF0C\u7136\u540E\u6253\u5F00\u5355\u4EBA\u6E38\u620F\u3002" }
      , { "Create World deletes files in the save folders except favorite players and worlds.", "\u4F1A\u5220\u9664\u5B58\u6863\u6587\u4EF6\u5939\u4E2D\u9664\u4E86\u6536\u85CF\u7684\u4EBA\u7269\u548C\u4E16\u754C\u4EE5\u5916\u7684\u6587\u4EF6\u3002" }
      , { "By default, Create World moves non-favorite players and worlds to the backup folder before creating.", "\u9ED8\u8BA4\u60C5\u51B5\u4E0B\uFF0C\u521B\u5EFA\u4E16\u754C\u4F1A\u5728\u521B\u5EFA\u524D\u628A\u975E\u6536\u85CF\u4EBA\u7269\u548C\u4E16\u754C\u79FB\u5230\u5907\u4EFD\u6587\u4EF6\u5939\u3002" }
      , { "When existing saves are not preserved, the most recent 50 cleanup batches are kept in the backup folder.", "\u672A\u542F\u7528\u4FDD\u7559\u5B58\u6863\u65F6\uFF0C\u6700\u8FD1 50 \u6B21\u6E05\u7406\u6279\u6B21\u4F1A\u4FDD\u7559\u5728\u5907\u4EFD\u6587\u4EF6\u5939\u3002" }
      , { "Automatically creates or enters a world by simulating mouse and keyboard input.", "\u901A\u8FC7\u6A21\u62DF\u9F20\u6807\u548C\u952E\u76D8\u64CD\u4F5C\u81EA\u52A8\u521B\u5EFA\u6216\u8FDB\u5165\u4E16\u754C\u3002" }
      , { "Do not choose players or worlds in the default save location.", "\u8BF7\u4E0D\u8981\u9009\u62E9\u5728\u9ED8\u8BA4\u5B58\u6863\u4F4D\u7F6E\u7684\u4EBA\u7269\u6216\u4E16\u754C\u3002" }
      , { "Do not choose favorite players or worlds.", "\u8BF7\u4E0D\u8981\u9009\u62E9\u6536\u85CF\u7684\u4EBA\u7269\u6216\u4E16\u754C\u3002" }
      , { "Deletes all non-favorite players and worlds.", "\u4F1A\u5220\u9664\u6240\u6709\u975E\u6536\u85CF\u7684\u4EBA\u7269\u548C\u4E16\u754C\u3002" }
      , { "The most recent 50 deletions are kept in the backup folder.", "\u6700\u8FD150\u6B21\u5220\u9664\u4F1A\u4FDD\u7559\u5728\u5907\u4EFD\u6587\u4EF6\u5939\u4E2D\u3002" }
      , { "Open folder", "\u6253\u5F00\u6587\u4EF6\u5939" }
      , { "Open save folder", "\u6253\u5F00\u5B58\u6863\u6587\u4EF6\u5939" }
      , { "Open backup folder", "\u6253\u5F00\u5907\u4EFD\u6587\u4EF6\u5939" }
      , { "If clicks are too fast for your computer to respond, adjust the delay settings at the bottom of this page.", "\u5982\u679C\u70B9\u51FB\u901F\u5EA6\u592A\u5FEB\u800C\u4F60\u7684\u7535\u8111\u6765\u4E0D\u53CA\u54CD\u5E94\uFF0C\u4F60\u53EF\u4EE5\u8C03\u6574\u672C\u9875\u5E95\u90E8\u7684\u5EF6\u8FDF\u8BBE\u7F6E\u3002" }
      , { "Force keep all files", "\u5F3A\u5236\u4FDD\u7559\u6240\u6709\u6587\u4EF6" }
      , { "When enabled, world creation will not delete any files. This can leave many worlds and players to clean up manually.", "\u542F\u7528\u540E\uFF0C\u521B\u56FE\u65F6\u4E0D\u4F1A\u5220\u9664\u4EFB\u4F55\u6587\u4EF6\uFF0C\u8FD9\u4F1A\u5BFC\u81F4\u5927\u91CF\u7684\u4E16\u754C\u548C\u4EBA\u7269\u9700\u8981\u624B\u52A8\u6E05\u7406\u3002" }
      , { "Could not open save folder.", "\u65E0\u6CD5\u6253\u5F00\u5B58\u6863\u6587\u4EF6\u5939\u3002" }
      , { "Could not open backup folder.", "\u65E0\u6CD5\u6253\u5F00\u5907\u4EFD\u6587\u4EF6\u5939\u3002" }
      , { "Timing", "\u65F6\u95F4" }
      , { "Delay", "\u5EF6\u8FDF" }
      , { "Character", "\u4EBA\u7269" }
      , { "Player options", "\u4EBA\u7269\u9009\u9879" }
      , { "Player name", "\u4EBA\u7269\u540D\u79F0" }
      , { "Player difficulty", "\u4EBA\u7269\u96BE\u5EA6" }
      , { "Player code", "\u4EBA\u7269\u4EE3\u7801" }
      , { "Initial player", "Race \u521D\u59CB\u4EBA\u7269" }
      , { "World", "\u4E16\u754C" }
      , { "World options", "\u4E16\u754C\u9009\u9879" }
      , { "World size", "\u4E16\u754C\u5927\u5C0F" }
      , { "World difficulty", "\u4E16\u754C\u96BE\u5EA6" }
      , { "World evil", "\u90AA\u6076\u7C7B\u578B" }
      , { "World seed / secret seed", "\u4E16\u754C\u79CD\u5B50 / \u79D8\u5BC6\u79CD\u5B50" }
      , { "Special seeds", "\u5F69\u86CB\u79CD\u5B50" }
      , { "Secret seed / fixed seed", "\u79D8\u5BC6\u79CD\u5B50/\u56FA\u5B9A\u79CD\u5B50" }
      , { "Zenith star catch", "\u5929\u9876\u63A5\u661F" }
      , { "Stop after stage", "\u5728\u4EE5\u4E0B\u9636\u6BB5\u540E\u505C\u6B62" }
      , { "Catch speed", "\u63A5\u661F\u901F\u5EA6" }
      , { "Filter pyramid", "\u7B5B\u5854" }
      , { "Quick pyramid filter", "\u7B5B\u5854" }
      , { "Required pyramid items", "\u6307\u5B9A\u7269\u54C1" }
      , { "Cheats", "\u4F5C\u5F0A" }
      , { "World filters", "\u4E16\u754C\u7B5B\u9009" }
      , { "Pyramid", "\u91D1\u5B57\u5854" }
      , { "Require Crimson between dungeon and spawn", "\u8981\u6C42\u5730\u7262\u4E0E\u51FA\u751F\u70B9\u4E4B\u95F4\u5B58\u5728\u7329\u7EA2" }
      , { "Required items", "\u6307\u5B9A\u7269\u54C1" }
      , { "Boomstick", "\u4E09\u53D1\u730E\u67AA" }
      , { "Feral Claws", "\u731B\u722A\u624B\u5957" }
      , { "Cloud in a Bottle", "\u4E91\u6735\u74F6" }
      , { "Anklet of the Wind", "\u75BE\u98CE\u811A\u956F" }
      , { "Hermes Boots", "\u8D6B\u5C14\u58A8\u65AF\u9774" }
      , { "Crimson filter", "\u7B5B\u7329\u7EA2" }
      , { "Life Crystal", "\u751F\u547D\u6C34\u6676" }
      , { "Hook", "\u94A9\u722A" }
      , { "Spelunker Potion", "\u6D1E\u7A74\u63A2\u9669\u836F\u6C34" }
      , { "Featherfall Potion", "\u7FBD\u843D\u836F\u6C34" }
      , { "Amethyst", "\u7D2B\u6676" }
      , { "Topaz", "\u9EC4\u7389" }
      , { "Sapphire", "\u84DD\u7389" }
      , { "Emerald", "\u7FE1\u7FE0" }
      , { "Ruby", "\u7EA2\u7389" }
      , { "Diamond", "\u94BB\u77F3" }
      , { "Near", "\u8FD1" }
      , { "Far", "\u8FDC" }
      , { "Filter Crimson between dungeon and spawn", "\u4E8C\u9A8C\u5730\u7262\u4E0E\u51FA\u751F\u70B9\u4E4B\u95F4\u7684\u7329\u7EA2" }
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
      , { "Deep", "\u6DF1" }
      , { "Very deep", "\u5F88\u6DF1" }
      , { "Jungle main route", "\u4E1B\u6797\u4E3B\u8DEF" }
      , { "Large", "\u5927" }
      , { "Classic", "\u7ECF\u5178" }
      , { "Expert", "\u4E13\u5BB6" }
      , { "Master", "\u5927\u5E08" }
      , { "Random", "\u968F\u673A" }
      , { "Corruption", "\u8150\u5316" }
      , { "Crimson", "\u7329\u7EA2" }
      , { "Dungeon-side Crimson", "\u5730\u7262\u4FA7\u7329\u7EA2" }
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
      , { "Performance", "\u6027\u80FD" }
      , { "Terraria process", "\u6CF0\u62C9\u8FDB\u7A0B" }
      , { "PID", "\u8FDB\u7A0B ID" }
      , { "Window", "\u7A97\u53E3" }
      , { "Responding", "\u54CD\u5E94\u4E2D" }
      , { "Visible", "\u53EF\u89C1" }
      , { "Minimized", "\u5DF2\u6700\u5C0F\u5316" }
      , { "Maximized", "\u5DF2\u6700\u5927\u5316" }
      , { "Foreground", "\u524D\u53F0\u7A97\u53E3" }
      , { "Status", "\u72B6\u6001" }
      , { "Menu scale", "\u83DC\u5355\u7F29\u653E" }
      , { "Single Player", "\u5355\u4EBA\u6E38\u620F" }
      , { "Yes", "\u662F" }
      , { "No", "\u5426" }
      , { "Unknown", "\u672A\u77E5" }
      , { "Empty", "\u7A7A" }
      , { "Ready", "\u5C31\u7EEA" }
      , { "Pending", "\u7B49\u5F85\u4E2D" }
      , { "Missing", "\u672A\u5339\u914D" }
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
      , { "resolving runtime layout", "\u6B63\u5728\u89E3\u6790\u8FD0\u884C\u65F6\u5E03\u5C40" }
      , { "resolving MemoryBridge runtime layout", "\u6B63\u5728\u901A\u8FC7 MemoryBridge \u89E3\u6790\u8FD0\u884C\u65F6\u5E03\u5C40" }
      , { "waiting for MemoryBridge runtime layout", "\u7B49\u5F85 MemoryBridge \u8FD0\u884C\u65F6\u5E03\u5C40" }
      , { "core layout missing", "\u6838\u5FC3\u5E03\u5C40\u7F3A\u5931" }
      , { "MemoryBridge did not resolve Terraria.Main.gameMenu", "MemoryBridge \u672A\u89E3\u6790 Terraria.Main.gameMenu" }
      , { "MemoryBridge resolved Terraria.Main.gameMenu, but the static field address is unreadable", "MemoryBridge \u5DF2\u89E3\u6790 Terraria.Main.gameMenu\uFF0C\u4F46\u9759\u6001\u5B57\u6BB5\u5730\u5740\u4E0D\u53EF\u8BFB" }
      , { "world generation layout pending", "\u521B\u4E16\u754C\u5E03\u5C40\u7B49\u5F85\u4E2D" }
      , { "boss layout pending", "Boss \u5E03\u5C40\u7B49\u5F85\u4E2D" }
      , { "fact layouts pending", "\u4E8B\u5B9E\u5E03\u5C40\u7B49\u5F85\u4E2D" }
      , { "MemoryBridge returned a layout without Terraria.Main.gameMenu", "MemoryBridge \u8FD4\u56DE\u7684\u5E03\u5C40\u7F3A\u5C11 Terraria.Main.gameMenu" }
      , { "runtime layout ready", "\u8FD0\u884C\u65F6\u5E03\u5C40\u5C31\u7EEA" }
      , { "timer and boss layouts ready; world generation layout unavailable", "\u8BA1\u65F6\u5668\u4E0E Boss \u5E03\u5C40\u5C31\u7EEA\uFF1B\u521B\u4E16\u754C\u5E03\u5C40\u4E0D\u53EF\u7528" }
      , { "timer and world generation layouts ready; boss layout unavailable", "\u8BA1\u65F6\u5668\u4E0E\u521B\u4E16\u754C\u5E03\u5C40\u5C31\u7EEA\uFF1BBoss \u5E03\u5C40\u4E0D\u53EF\u7528" }
      , { "timer layout ready; fact layouts unavailable", "\u8BA1\u65F6\u5668\u5E03\u5C40\u5C31\u7EEA\uFF1B\u4E8B\u5B9E\u5E03\u5C40\u4E0D\u53EF\u7528" }
      , { "lost menu-state pointer; rescanning", "\u83DC\u5355\u72B6\u6001\u6307\u9488\u5DF2\u4E22\u5931\uFF0C\u6B63\u5728\u91CD\u65B0\u626B\u63CF" }
      , { "menu-state pointer became unreadable", "\u83DC\u5355\u72B6\u6001\u6307\u9488\u53D8\u4E3A\u4E0D\u53EF\u8BFB" }
      , { "ready via fallback", "\u5DF2\u901A\u8FC7\u56DE\u9000\u65B9\u6848\u5C31\u7EEA" }
      , { "ready via gameMenu fallback", "\u5DF2\u901A\u8FC7\u83DC\u5355\u72B6\u6001\u56DE\u9000\u65B9\u6848\u5C31\u7EEA" }
      , { "ready via boss fallback", "\u5DF2\u901A\u8FC7 Boss \u56DE\u9000\u65B9\u6848\u5C31\u7EEA" }
      , { "timer ready via fallback", "\u8BA1\u65F6\u5668\u5DF2\u901A\u8FC7\u56DE\u9000\u65B9\u6848\u5C31\u7EEA" }
      , { "timer ready via fallback; boss scan pending", "\u8BA1\u65F6\u5668\u5DF2\u901A\u8FC7\u56DE\u9000\u65B9\u6848\u5C31\u7EEA\uFF1BBoss \u626B\u63CF\u5F85\u5B8C\u6210" }
      , { "boss pointers pending", "Boss \u6307\u9488\u5F85\u89E3\u6790" }
      , { "boss scan pending", "Boss \u626B\u63CF\u5F85\u5B8C\u6210" }
      , { "return to menu once to arm timer start", "\u8BF7\u5148\u8FD4\u56DE\u4E00\u6B21\u4E3B\u83DC\u5355\u4EE5\u6FC0\u6D3B\u8BA1\u65F6\u5F00\u59CB" }
      , { "waiting for process", "\u7B49\u5F85\u8FDB\u7A0B" }
      , { "cannot read process", "\u65E0\u6CD5\u8BFB\u53D6\u8FDB\u7A0B" }
      , { "cannot attach process", "\u65E0\u6CD5\u9644\u52A0\u8FDB\u7A0B" }
      , { "menu state pointer lost", "\u83DC\u5355\u72B6\u6001\u6307\u9488\u5DF2\u4E22\u5931" }
      , { "menu state target unreadable", "\u83DC\u5355\u72B6\u6001\u76EE\u6807\u4E0D\u53EF\u8BFB" }
      , { "menu state pointer unreadable", "\u83DC\u5355\u72B6\u6001\u6307\u9488\u4E0D\u53EF\u8BFB" }
      , { "start pending", "\u7B49\u5F85\u5F00\u59CB" }
      , { "Waiting for Terraria process.", "\u6B63\u5728\u7B49\u5F85 Terraria \u8FDB\u7A0B\u3002" }
      , { "Target Terraria process is x64. The current managed runtime layout resolver is x86-only.", "\u76EE\u6807 Terraria \u8FDB\u7A0B\u4E3A x64\u3002\u5F53\u524D\u6258\u7BA1\u8FD0\u884C\u65F6\u5E03\u5C40\u89E3\u6790\u5668\u4EC5\u652F\u6301 x86\u3002" }
      , { "Watcher first became ready while Terraria was already in a world. The timer starts only on a menu-to-world transition, so return to the main menu once and enter the world again.", "\u76D1\u6D4B\u5668\u9996\u6B21\u5C31\u7EEA\u65F6\uFF0CTerraria \u5DF2\u7ECF\u5904\u4E8E\u4E16\u754C\u5185\u3002\u8BA1\u65F6\u53EA\u4F1A\u5728\u4ECE\u83DC\u5355\u8FDB\u5165\u4E16\u754C\u7684\u5207\u6362\u65F6\u5F00\u59CB\uFF0C\u6240\u4EE5\u8BF7\u5148\u8FD4\u56DE\u4E3B\u83DC\u5355\u4E00\u6B21\uFF0C\u518D\u91CD\u65B0\u8FDB\u5165\u4E16\u754C\u3002" }
      , { "MemoryBridge has not resolved Terraria.Main.gameMenu yet.", "MemoryBridge \u5C1A\u672A\u89E3\u6790 Terraria.Main.gameMenu\u3002" }
      , { "gameMenu resolved, but boss fact static fields are unavailable in the managed layout.", "gameMenu \u5DF2\u89E3\u6790\uFF0C\u4F46\u6258\u7BA1\u5E03\u5C40\u4E2D\u7F3A\u5C11 Boss \u4E8B\u5B9E\u9759\u6001\u5B57\u6BB5\u3002" }
      , { "Watcher resolved timer and boss layouts, but world generation layout is unavailable. Timer and split facts can still work.", "\u76D1\u6D4B\u5668\u5DF2\u89E3\u6790\u8BA1\u65F6\u5668\u4E0E Boss \u5E03\u5C40\uFF0C\u4F46\u521B\u4E16\u754C\u5E03\u5C40\u4E0D\u53EF\u7528\u3002\u8BA1\u65F6\u4E0E\u5206\u6BB5\u4E8B\u5B9E\u4ECD\u53EF\u5DE5\u4F5C\u3002" }
      , { "Watcher resolved timer, boss, and world generation layouts. Seed UI layout is unavailable, so visible seed diagnostics may stay Unknown.", "\u76D1\u6D4B\u5668\u5DF2\u89E3\u6790\u8BA1\u65F6\u5668\u3001Boss \u548C\u521B\u4E16\u754C\u5E03\u5C40\u3002\u79CD\u5B50 UI \u5E03\u5C40\u4E0D\u53EF\u7528\uFF0C\u53EF\u89C1\u79CD\u5B50\u8BCA\u65AD\u53EF\u80FD\u4FDD\u6301\u672A\u77E5\u3002" }
      , { "Watcher resolved the managed runtime layout.", "\u76D1\u6D4B\u5668\u5DF2\u89E3\u6790\u6258\u7BA1\u8FD0\u884C\u65F6\u5E03\u5C40\u3002" }
      , { "gameMenu resolved, but boss and hardmode pointers are still pending or unreadable.", "\u83DC\u5355\u72B6\u6001\u5DF2\u89E3\u6790\uFF0C\u4F46 Boss \u4E0E hardmode \u6307\u9488\u4ECD\u5728\u7B49\u5F85\u89E3\u6790\u6216\u4E0D\u53EF\u8BFB\u3002" }
      , { "Watcher resolved all current pointers.", "\u76D1\u6D4B\u5668\u5DF2\u89E3\u6790\u5F53\u524D\u6240\u6709\u6307\u9488\u3002" }
      , { "Only the assigned Race world can be entered until the run is completed.", "Race \u5B8C\u6210\u524D\u53EA\u80FD\u8FDB\u5165\u6307\u5B9A\u4E16\u754C\u3002" }
      , { "Only the assigned Race world and player can be used until the run is completed.", "Race \u5B8C\u6210\u524D\u53EA\u80FD\u4F7F\u7528\u6307\u5B9A\u4EBA\u7269\u5E76\u8FDB\u5165\u6307\u5B9A\u4E16\u754C\u3002" }
    };

    public bool TryGet(string key, out string value)
    {
        bool found = Values.TryGetValue(key, out string? localizedValue);
        value = localizedValue ?? string.Empty;
        return found;
    }
}
