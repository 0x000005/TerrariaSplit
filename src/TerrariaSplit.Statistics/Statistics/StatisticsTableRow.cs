namespace TerrariaSplit.Statistics;

public sealed record StatisticsTableRow(
    SplitConditionDataRow ConditionRow,
    string ReferenceTimeText,
    string PersonalTimeText,
    string ReferenceSegmentText,
    string PersonalSegmentText,
    string PersonalBestText,
    string PersonalBestSegmentText,
    int GroupRowCount,
    int GroupOffset)
{
    public string DisplayName => ConditionRow.DisplayName;
}
