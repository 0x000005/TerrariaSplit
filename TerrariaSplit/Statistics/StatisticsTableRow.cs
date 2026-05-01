namespace TerrariaSplit;

internal sealed record StatisticsTableRow(
    BossUnitDefinition Unit,
    string ReferenceTimeText,
    string PersonalTimeText,
    string ReferenceSegmentText,
    string PersonalSegmentText,
    string PersonalBestText,
    string PersonalBestSegmentText,
    int GroupRowCount,
    int GroupOffset);
