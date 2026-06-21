namespace TerrariaSplit.Application;

internal static class SplitSoundSelector
{
    public static string GetPath(
        UiSoundSettings sounds,
        SplitDefinition definition,
        TimeSpan splitTime,
        TimeSpan? referenceSplit,
        TimeSpan? segmentTime,
        TimeSpan? personalBestSegment)
    {
        bool cumulativeFasterThanReference = referenceSplit is not TimeSpan reference ||
            splitTime < reference;
        bool segmentFasterThanPersonalBest = personalBestSegment is not TimeSpan personalBest ||
            segmentTime is TimeSpan segment && segment < personalBest;

        return GetPath(
            sounds,
            definition,
            cumulativeFasterThanReference,
            segmentFasterThanPersonalBest);
    }

    public static string GetPath(
        UiSoundSettings sounds,
        SplitDefinition definition,
        bool cumulativeFasterThanReference,
        bool segmentFasterThanPersonalBest)
    {
        if (SplitCatalog.IsMoonLordSplit(definition))
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
