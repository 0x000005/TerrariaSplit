using System.Windows.Forms;

namespace TerrariaSplit;

internal sealed class AppSettings
{
    public string PauseResumeKey { get; set; } = Keys.R.ToString();
    public string ResetKey { get; set; } = Keys.T.ToString();
    public Dictionary<string, string> WorldRecordSplits { get; set; } = new();
    public UiColorSettings Colors { get; set; } = new();

    public Keys PauseResumeKeys => ParseKey(PauseResumeKey, Keys.R);
    public Keys ResetKeys => ParseKey(ResetKey, Keys.T);

    public static AppSettings CreateDefault()
    {
        var settings = new AppSettings();
        foreach (BossSplitDefinition definition in BossSplitDefinitions.All)
        {
            settings.WorldRecordSplits.TryAdd(definition.Name.ToString(), string.Empty);
        }

        return settings;
    }

    public bool TryGetWorldRecordSplit(BossSplitName name, out TimeSpan split)
    {
        split = TimeSpan.Zero;
        return WorldRecordSplits.TryGetValue(name.ToString(), out string? value) &&
            TimeText.TryParse(value, out split);
    }

    public string GetWorldRecordText(BossSplitName name)
    {
        return WorldRecordSplits.TryGetValue(name.ToString(), out string? value) ? value : string.Empty;
    }

    public void SetWorldRecordText(BossSplitName name, string value)
    {
        WorldRecordSplits[name.ToString()] = value;
    }

    private static Keys ParseKey(string? value, Keys fallback)
    {
        return Enum.TryParse(value, ignoreCase: true, out Keys key) ? key : fallback;
    }
}
