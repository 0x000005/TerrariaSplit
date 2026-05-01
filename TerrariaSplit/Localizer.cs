namespace TerrariaSplit;

internal static class Localizer
{
    private static readonly Dictionary<string, string> Zh = new(StringComparer.OrdinalIgnoreCase)
    {
        { "TerrariaSplit Settings", "TerrariaSplit 设置" },
        { "OK", "确定" },
        { "Apply", "应用" },
        { "Cancel", "取消" },
        { "General", "常规" },
        { "Splits", "计次" },
        { "UI", "界面" },
        { "Colors", "颜色" },
        { "Hotkeys", "快捷键" },
        { "General Options", "常规选项" },
        { "Language", "语言" },
        { "Pause / Resume", "暂停 / 继续" },
        { "Reset at Menu", "主菜单重置" },
        { "Mouse passthrough", "鼠标穿透" },
        { "Always on top", "置顶显示" },
        { "Practice mode", "练习模式" },
        { "Columns", "列" },
        { "Column", "列名" },
        { "Show", "显示" },
        { "Width", "宽度" },
        { "Font", "字体大小" },
        { "Bold", "粗体" },
        { "Icon", "图标" },
        { "Time", "时间" },
        { "Delta", "差值" },
        { "Timer", "计时器" },
        { "Part", "部分" },
        { "Main time", "主时间" },
        { "Milliseconds", "毫秒" },
        { "Icon Style", "图标样式" },
        { "Unlit grayscale %", "未击败 灰度 %" },
        { "Unlit brightness %", "未击败 亮度 %" },
        { "Boss Icons", "BOSS图标" },
        { "empty = bundled icon", "留空 = 默认图标" },
        { "Browse", "浏览" },
        { "Reference Data", "参考数据" },
        { "Personal Best Data", "个人最佳数据" },
        { "new group name", "新组名" },
        { "Add", "添加" },
        { "Delete", "删除" },
        { "Active group", "活动组" },
        { "Text Colors", "文字颜色" },
        { "Reference text", "参考时间文字" },
        { "Reference", "参考" },
        { "Reference time", "参考时间" },
        { "Personal best", "个人最佳时间" },
        { "Personal time", "个人时间" },
        { "Active reference text", "当前目标时间文字" },
        { "Completed split text", "完成计次文字" },
        { "Delta ahead text", "差值领先文字" },
        { "Delta behind text", "差值落后文字" },
        { "Delta even text", "差值持平文字" },
        { "Timer text", "计时文字" },
        { "Timer ahead text", "计时领先文字" },
        { "Timer behind text", "计时落后文字" },
        { "Timer record text", "计时破纪录文字" },
        { "Boss Route", "BOSS顺序" },
        { "Boss", "BOSS" },
        { "Enabled", "启用" },
        { "Segment", "阶段" },
        { "Settings...", "设置..." },
        { "Statistics...", "统计信息..." },
        { "Statistics", "统计信息" },
        { "Last run", "个人成绩" },
        { "Best split", "历史分段最佳成绩" },
        { "Fastest segment", "每段最快时间" },
        { "No splits", "没有分段" },
        { "Exit", "退出" },
        { "Edit total time", "编辑总时间" },
        { "Edit split time", "编辑分段时间" },
        { "Skeletron", "骷髅王" },
        { "Wall of Flesh", "血肉墙" },
        { "Destroyer", "毁灭者" },
        { "Skeletron Prime", "机械骷髅王" },
        { "The Twins", "双子魔眼" },
        { "Plantera", "世纪之花" },
        { "Golem", "石巨人" },
        { "Lunatic Cultist", "拜月教邪教徒" },
        { "Celestial Pillars", "天界柱" },
        { "Moon Lord", "月亮领主" },
        { "Mechanical Bosses", "机械三王" }
    };

    private static readonly Dictionary<string, string> ExtraZh = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Reference segment", "参考分段" },
        { "Personal segment", "个人分段" },
        { "Personal best segment", "个人最佳分段" }
    };

    public static string Get(string key, AppSettings settings)
    {
        if (settings.Language == "中文" && ExtraZh.TryGetValue(key, out string? extraValue))
        {
            return extraValue;
        }

        if (settings.Language == "中文" && Zh.TryGetValue(key, out string? value))
        {
            return value;
        }
        return key;
    }
}
