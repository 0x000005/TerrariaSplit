namespace TerrariaSplit.UI.Settings;

internal static class SplitTargetListController
{
    public static string FormatTargetListItem(SplitTargetDefinition target, string language)
    {
        return $"{SplitTargetDisplayNames.GetTargetName(target, language)} ({SplitTargetTokenFormatter.Format(target)})";
    }
}

internal sealed record SplitTargetListItem(SplitTargetDefinition Target, string DisplayText)
{
    public override string ToString()
    {
        return DisplayText;
    }
}

internal sealed record SplitTargetKindOption(string Value, string DisplayName)
{
    public override string ToString()
    {
        return DisplayName;
    }
}
