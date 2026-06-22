namespace TerrariaSplit.Application;

internal static class SplitSoundSelector
{
    public static string GetPath(
        UiSoundSettings sounds,
        bool isFinalGroupCompletion,
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
            isFinalGroupCompletion,
            cumulativeFasterThanReference,
            segmentFasterThanPersonalBest);
    }

    public static string GetPath(
        UiSoundSettings sounds,
        bool isFinalGroupCompletion,
        bool cumulativeFasterThanReference,
        bool segmentFasterThanPersonalBest)
    {
        if (isFinalGroupCompletion)
        {
            string finalGroupPath = (cumulativeFasterThanReference, segmentFasterThanPersonalBest) switch
            {
                (false, false) => sounds.FinalGroupBehindReferenceBehindSegment,
                (false, true) => sounds.FinalGroupBehindReferenceAheadSegment,
                (true, false) => sounds.FinalGroupAheadReferenceBehindSegment,
                _ => sounds.FinalGroupAheadReferenceAheadSegment
            };
            if (!string.IsNullOrWhiteSpace(finalGroupPath))
            {
                return finalGroupPath;
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
