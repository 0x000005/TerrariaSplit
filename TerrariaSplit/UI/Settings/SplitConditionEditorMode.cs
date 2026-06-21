namespace TerrariaSplit.UI.Settings;

internal static class SplitConditionEditorMode
{
    public static bool CanUseBasicEditor(SplitCondition condition)
    {
        string kind = SplitConditionKind.Normalize(condition.Kind);
        if (kind == SplitConditionKind.Fact)
        {
            return true;
        }

        if (kind != SplitConditionKind.All &&
            kind != SplitConditionKind.Any &&
            kind != SplitConditionKind.AtLeast)
        {
            return false;
        }

        return condition.Children.All(child => SplitConditionKind.Normalize(child.Kind) == SplitConditionKind.Fact);
    }
}
