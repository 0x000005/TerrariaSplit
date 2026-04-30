using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class AppSettings
{
    public string PauseResumeKey { get; set; } = Keys.R.ToString();
    public string ResetKey { get; set; } = Keys.T.ToString();
    public string MouseClickThroughKey { get; set; } = Keys.I.ToString();
    public bool AlwaysOnTop { get; set; }
    public bool PracticeMode { get; set; } = true;
    public List<BossRouteEntry> Route { get; set; } = new();
    public Dictionary<string, string> BossIconPaths { get; set; } = new();
    public List<ReferenceSplitSet> ReferenceSplitSets { get; set; } = new();
    public string ActiveReferenceSplitSet { get; set; } = "WR";
    public UiColorSettings Colors { get; set; } = new();
    public UiColumnLayoutSettings Columns { get; set; } = new();
    public int UndefeatedIconGrayscalePercent { get; set; } = 80;
    public int UndefeatedIconBrightnessPercent { get; set; } = 40;

    public Keys PauseResumeKeys => ParseKey(PauseResumeKey, Keys.R);
    public Keys ResetKeys => ParseKey(ResetKey, Keys.T);
    public Keys MouseClickThroughKeys => ParseKey(MouseClickThroughKey, Keys.I);

    public static AppSettings CreateDefault()
    {
        var settings = new AppSettings();
        settings.Route = BossSplitDefinitions.CreateDefaultRoute();
        foreach (BossUnitDefinition unit in BossSplitDefinitions.Units)
        {
            settings.BossIconPaths.TryAdd(unit.Id, string.Empty);
        }

        settings.ReferenceSplitSets.Add(CreateReferenceSet("WR"));
        return settings;
    }

    public bool TryGetReferenceSplit(BossSplitDefinition definition, out TimeSpan split)
    {
        split = TimeSpan.Zero;
        bool anyFound = false;
        TimeSpan maxSplit = TimeSpan.Zero;
        var splits = GetActiveReferenceSet().Splits;

        foreach (string bossId in definition.BossIds)
        {
            if (splits.TryGetValue(bossId, out string? value) && TimeText.TryParse(value, out TimeSpan s))
            {
                if (!anyFound || s > maxSplit)
                {
                    maxSplit = s;
                }
                anyFound = true;
            }
        }

        if (anyFound)
        {
            split = maxSplit;
            return true;
        }

        return false;
    }

    public string GetReferenceText(string name)
    {
        return GetActiveReferenceSet().Splits.TryGetValue(name, out string? value) ? value : string.Empty;
    }

    public string GetBossIconPath(string name)
    {
        return BossIconPaths.TryGetValue(name, out string? value) ? value : string.Empty;
    }

    public void SetBossIconPath(string name, string value)
    {
        BossIconPaths[name] = value;
    }

    public void SetReferenceText(string name, string value)
    {
        GetActiveReferenceSet().Splits[name] = value;
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

        foreach (BossUnitDefinition unit in BossSplitDefinitions.Units)
        {
            string key = unit.Id;
            string value = values is not null && values.TryGetValue(key, out string? existingValue)
                ? existingValue
                : string.Empty;
            set.Splits[key] = value;
        }

        return set;
    }
}
