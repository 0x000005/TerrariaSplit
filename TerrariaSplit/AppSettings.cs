using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class AppSettings
{
    public string PauseResumeKey { get; set; } = Keys.R.ToString();
    public string ResetKey { get; set; } = Keys.T.ToString();
    public bool AlwaysOnTop { get; set; }
    public bool PracticeMode { get; set; }
    public Dictionary<string, string> BossIconPaths { get; set; } = new();
    public List<ReferenceSplitSet> ReferenceSplitSets { get; set; } = new();
    public string ActiveReferenceSplitSet { get; set; } = "WR";
    public UiColorSettings Colors { get; set; } = new();
    public UiColumnLayoutSettings Columns { get; set; } = new();
    public int UndefeatedIconGrayscalePercent { get; set; } = 80;
    public int UndefeatedIconBrightnessPercent { get; set; } = 30;

    public Keys PauseResumeKeys => ParseKey(PauseResumeKey, Keys.R);
    public Keys ResetKeys => ParseKey(ResetKey, Keys.T);

    public static AppSettings CreateDefault()
    {
        var settings = new AppSettings();
        foreach (BossSplitDefinition definition in BossSplitDefinitions.All)
        {
            settings.BossIconPaths.TryAdd(definition.Name.ToString(), string.Empty);
        }

        settings.ReferenceSplitSets.Add(CreateReferenceSet("WR"));
        return settings;
    }

    public bool TryGetReferenceSplit(BossSplitName name, out TimeSpan split)
    {
        split = TimeSpan.Zero;
        return GetActiveReferenceSet().Splits.TryGetValue(name.ToString(), out string? value) &&
            TimeText.TryParse(value, out split);
    }

    public string GetReferenceText(BossSplitName name)
    {
        return GetActiveReferenceSet().Splits.TryGetValue(name.ToString(), out string? value) ? value : string.Empty;
    }

    public string GetBossIconPath(BossSplitName name)
    {
        return BossIconPaths.TryGetValue(name.ToString(), out string? value) ? value : string.Empty;
    }

    public void SetBossIconPath(BossSplitName name, string value)
    {
        BossIconPaths[name.ToString()] = value;
    }

    public void SetReferenceText(BossSplitName name, string value)
    {
        GetActiveReferenceSet().Splits[name.ToString()] = value;
    }

    private static Keys ParseKey(string? value, Keys fallback)
    {
        return Enum.TryParse(value, ignoreCase: true, out Keys key) ? key : fallback;
    }

    public ReferenceSplitSet GetActiveReferenceSet()
    {
        ReferenceSplitSet? activeSet = ReferenceSplitSets.FirstOrDefault(
            set => string.Equals(set.Name, ActiveReferenceSplitSet, StringComparison.OrdinalIgnoreCase));
        if (activeSet is not null)
        {
            return activeSet;
        }

        if (ReferenceSplitSets.Count == 0)
        {
            ReferenceSplitSets.Add(CreateReferenceSet("WR"));
        }

        ActiveReferenceSplitSet = ReferenceSplitSets[0].Name;
        return ReferenceSplitSets[0];
    }

    public static ReferenceSplitSet CreateReferenceSet(string name, Dictionary<string, string>? values = null)
    {
        var set = new ReferenceSplitSet
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Reference" : name.Trim()
        };

        foreach (BossSplitDefinition definition in BossSplitDefinitions.All)
        {
            string key = definition.Name.ToString();
            string value = values is not null && values.TryGetValue(key, out string? existingValue)
                ? existingValue
                : string.Empty;
            set.Splits[key] = value;
        }

        return set;
    }
}
