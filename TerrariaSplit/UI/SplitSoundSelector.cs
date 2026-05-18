namespace TerrariaSplit;

internal static class SplitSoundSelector
{
    public static string GetPath(
        UiSoundSettings sounds,
        BossSplitDefinition definition,
        bool cumulativeFasterThanReference,
        bool segmentFasterThanPersonalBest)
    {
        if (BossSplitDefinitions.IsMoonLordSplit(definition))
        {
            string moonLordPath = (cumulativeFasterThanReference, segmentFasterThanPersonalBest) switch
            {
                (false, false) => sounds.MoonLordBehindReferenceBehindSegment,
                (false, true) => sounds.MoonLordBehindReferenceAheadSegment,
                (true, false) => sounds.MoonLordAheadReferenceBehindSegment,
                _ => sounds.MoonLordAheadReferenceAheadSegment
            };
            if (!string.IsNullOrWhiteSpace(moonLordPath))
            {
                return moonLordPath;
            }
        }

        return (cumulativeFasterThanReference, segmentFasterThanPersonalBest) switch
        {
            (false, false) => sounds.SplitBehindReferenceBehindSegment,
            (false, true) => sounds.SplitBehindReferenceAheadSegment,
            (true, false) => sounds.SplitAheadReferenceBehindSegment,
            _ => sounds.SplitAheadReferenceAheadSegment
        };
    }
}
