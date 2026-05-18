namespace TerrariaSplit;

internal sealed class RunFinalizer
{
    public void Finalize(
        AppSettings settings,
        IReadOnlyList<BossSplitStatus> statuses,
        bool runStatsRecorded,
        Func<string, bool> confirmPersonalBestUpdates)
    {
        PendingPersonalBestUpdates updates = BuildPendingPersonalBestUpdates(settings, statuses);
        if (updates.HasUpdates && settings.AutoUpdatePersonalBestData)
        {
            bool shouldUpdate = !settings.AskBeforeUpdatingPersonalBestData ||
                confirmPersonalBestUpdates(BuildPersonalBestUpdatePromptText(updates, settings));
            if (shouldUpdate)
            {
                ApplyPendingPersonalBestUpdates(settings, updates);
            }
        }

        if (!runStatsRecorded)
        {
            RunStatsStore.RecordRun(statuses);
        }
    }

    private static void ApplyPendingPersonalBestUpdates(AppSettings settings, PendingPersonalBestUpdates updates)
    {
        List<PendingPersonalBestSegmentUpdate> segmentUpdates = updates.SegmentUpdates.Values.ToList();
        foreach (PendingPersonalBestSegmentUpdate update in segmentUpdates)
        {
            settings.SetPersonalBestSegmentText(update.GroupKey, update.NewTimeText);
        }

        if (segmentUpdates.Count > 0)
        {
            (string bossName, string previousTimeText, string newTimeText) = BuildSnapshotLabel(segmentUpdates);
            ReferenceSplitSet snapshot = SplitTimeSetStore.SavePersonalBestSegmentSnapshot(
                settings.PersonalBestSegmentTimes,
                bossName,
                previousTimeText,
                newTimeText);
            AddPersonalBestSnapshot(settings.PersonalBestSegmentSets, snapshot);
            settings.ActivePersonalBestSegmentSet = snapshot.Name;
        }

        if (updates.TimeUpdate is PendingPersonalBestTimeUpdate timeUpdate)
        {
            settings.PersonalBestTimes = new Dictionary<string, string>(
                timeUpdate.Splits,
                StringComparer.OrdinalIgnoreCase);
            ReferenceSplitSet snapshot = SplitTimeSetStore.SavePersonalBestTimeSnapshot(
                settings.PersonalBestTimes,
                timeUpdate.BossName,
                timeUpdate.PreviousTimeText,
                timeUpdate.NewTimeText);
            AddPersonalBestSnapshot(settings.PersonalBestTimeSets, snapshot);
            settings.ActivePersonalBestTimeSet = snapshot.Name;
        }

        AppSettingsStore.Save(settings);
    }

    private static PendingPersonalBestUpdates BuildPendingPersonalBestUpdates(
        AppSettings settings,
        IReadOnlyList<BossSplitStatus> statuses)
    {
        var segmentUpdates = new Dictionary<string, PendingPersonalBestSegmentUpdate>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < statuses.Count; i++)
        {
            AddPendingSegmentBestUpdate(settings, statuses, i, segmentUpdates);
        }

        PendingPersonalBestTimeUpdate? timeUpdate = BuildPendingTimeBestUpdate(settings, statuses);
        return new PendingPersonalBestUpdates(segmentUpdates, timeUpdate);
    }

    private static void AddPendingSegmentBestUpdate(
        AppSettings settings,
        IReadOnlyList<BossSplitStatus> statuses,
        int completedIndex,
        Dictionary<string, PendingPersonalBestSegmentUpdate> segmentUpdates)
    {
        if (completedIndex < 0 ||
            completedIndex >= statuses.Count ||
            statuses[completedIndex].Time is not TimeSpan splitTime)
        {
            return;
        }

        if (completedIndex > 0 && (statuses.Count == 0 || statuses[0].Time is null))
        {
            return;
        }

        TimeSpan previousSplitTime = TimeSpan.Zero;
        for (int i = completedIndex - 1; i >= 0; i--)
        {
            if (statuses[i].Time is TimeSpan previousTime)
            {
                previousSplitTime = previousTime;
                break;
            }
        }

        TimeSpan segmentTime = splitTime - previousSplitTime;
        if (segmentTime < TimeSpan.Zero)
        {
            return;
        }

        string groupKey = GetSplitCompletionGroupKey(statuses[completedIndex].Definition);
        if (segmentUpdates.TryGetValue(groupKey, out PendingPersonalBestSegmentUpdate? pendingSegment) &&
            pendingSegment.NewTime <= segmentTime)
        {
            return;
        }

        if (settings.PersonalBestSegmentTimes.TryGetValue(groupKey, out string? existingText) &&
            TimeText.TryParse(existingText, out TimeSpan existingSegment) &&
            existingSegment <= segmentTime)
        {
            return;
        }

        segmentUpdates[groupKey] = new PendingPersonalBestSegmentUpdate(
            groupKey,
            statuses[completedIndex].Definition.DisplayName,
            existingText ?? string.Empty,
            TimeText.FormatRecord(segmentTime),
            segmentTime);
    }

    private static PendingPersonalBestTimeUpdate? BuildPendingTimeBestUpdate(
        AppSettings settings,
        IReadOnlyList<BossSplitStatus> statuses)
    {
        if (statuses.Count == 0 || statuses.Any(status => status.Time is null || status.IsSkipped))
        {
            return null;
        }

        BossSplitStatus? moonLordStatus = statuses.FirstOrDefault(status =>
            BossSplitDefinitions.IsMoonLordSplit(status.Definition));
        if (moonLordStatus?.Time is not TimeSpan moonLordTime)
        {
            return null;
        }

        if (settings.PersonalBestTimes.TryGetValue(BossSplitDefinitions.MoonLord, out string? existingMoonLordText) &&
            TimeText.TryParse(existingMoonLordText, out TimeSpan existingMoonLordTime) &&
            existingMoonLordTime <= moonLordTime)
        {
            return null;
        }

        return new PendingPersonalBestTimeUpdate(
            moonLordStatus.Definition.DisplayName,
            existingMoonLordText ?? string.Empty,
            TimeText.FormatRecord(moonLordTime),
            moonLordTime,
            BuildCompletedSplitValues(statuses));
    }

    private static Dictionary<string, string> BuildCompletedSplitValues(IReadOnlyList<BossSplitStatus> statuses)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (BossSplitStatus status in statuses)
        {
            if (status.Time is not TimeSpan splitTime)
            {
                continue;
            }

            string formatted = TimeText.FormatRecord(splitTime);
            foreach (string bossId in status.Definition.BossIds)
            {
                values[bossId] = formatted;
            }
        }

        return values;
    }

    private static string BuildPersonalBestUpdatePromptText(PendingPersonalBestUpdates updates, AppSettings settings)
    {
        var lines = new List<string>();
        if (updates.TimeUpdate is PendingPersonalBestTimeUpdate timeUpdate)
        {
            lines.Add($"{Localizer.Get("Cumulative", settings)}: {timeUpdate.BossName} {FormatPromptChange(timeUpdate.PreviousTimeText, timeUpdate.NewTimeText, settings)}");
        }

        foreach (PendingPersonalBestSegmentUpdate update in updates.SegmentUpdates.Values)
        {
            lines.Add($"{Localizer.Get("Segment", settings)}: {update.BossName} {FormatPromptChange(update.PreviousTimeText, update.NewTimeText, settings)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatPromptChange(string previousTimeText, string newTimeText, AppSettings settings)
    {
        string oldText = string.IsNullOrWhiteSpace(previousTimeText) ? Localizer.Get("None", settings) : previousTimeText;
        return $"{oldText} -> {newTimeText}";
    }

    private static string GetSplitCompletionGroupKey(BossSplitDefinition definition)
    {
        return string.Join("+", definition.BossIds);
    }

    private static (string BossName, string PreviousTimeText, string NewTimeText) BuildSnapshotLabel(
        List<PendingPersonalBestSegmentUpdate> updates)
    {
        if (updates.Count == 1)
        {
            PendingPersonalBestSegmentUpdate update = updates[0];
            return (update.BossName, update.PreviousTimeText, update.NewTimeText);
        }

        PendingPersonalBestSegmentUpdate lastUpdate = updates[^1];
        return ($"{lastUpdate.BossName}-Segments", "Multiple", "Multiple");
    }

    private static void AddPersonalBestSnapshot(List<ReferenceSplitSet> sets, ReferenceSplitSet snapshot)
    {
        sets.RemoveAll(set => string.Equals(set.Name, snapshot.Name, StringComparison.OrdinalIgnoreCase));
        sets.Insert(0, snapshot);
    }

    private sealed record PendingPersonalBestUpdates(
        Dictionary<string, PendingPersonalBestSegmentUpdate> SegmentUpdates,
        PendingPersonalBestTimeUpdate? TimeUpdate)
    {
        public bool HasUpdates => TimeUpdate is not null || SegmentUpdates.Count > 0;
    }

    private sealed record PendingPersonalBestTimeUpdate(
        string BossName,
        string PreviousTimeText,
        string NewTimeText,
        TimeSpan NewTime,
        Dictionary<string, string> Splits);

    private sealed record PendingPersonalBestSegmentUpdate(
        string GroupKey,
        string BossName,
        string PreviousTimeText,
        string NewTimeText,
        TimeSpan NewTime);
}
