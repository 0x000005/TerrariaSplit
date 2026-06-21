namespace TerrariaSplit.Application;

internal sealed class RunFinalizer
{
    private readonly IRunStatisticsRecorder runStatisticsRecorder;
    private readonly IPersonalBestSnapshotStore personalBestSnapshotStore;

    public RunFinalizer(
        IRunStatisticsRecorder? runStatisticsRecorder = null,
        IPersonalBestSnapshotStore? personalBestSnapshotStore = null)
    {
        this.runStatisticsRecorder = runStatisticsRecorder ?? NullRunStatisticsRecorder.Instance;
        this.personalBestSnapshotStore = personalBestSnapshotStore ?? InMemoryPersonalBestSnapshotStore.Instance;
    }

    public bool Finalize(
        AppSettings settings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        bool runStatsRecorded,
        Func<string, bool> confirmPersonalBestUpdates)
    {
        return Finalize(settings, settings, statuses, runStatsRecorded, confirmPersonalBestUpdates);
    }

    public bool Finalize(
        AppSettings routeSettings,
        AppSettings updateTargetSettings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        bool runStatsRecorded,
        Func<string, bool> confirmPersonalBestUpdates)
    {
        PendingPersonalBestUpdates updates = BuildPendingPersonalBestUpdates(
            routeSettings,
            updateTargetSettings,
            statuses);
        bool settingsUpdated = false;
        if (updates.HasUpdates && updateTargetSettings.Comparison.AutoUpdatePersonalBestData)
        {
            bool shouldUpdate = !updateTargetSettings.Comparison.AskBeforeUpdatingPersonalBestData ||
                confirmPersonalBestUpdates(BuildPersonalBestUpdatePromptText(updates, updateTargetSettings));
            if (shouldUpdate)
            {
                ApplyPendingPersonalBestUpdates(updateTargetSettings, updates);
                settingsUpdated = true;
            }
        }

        if (!runStatsRecorded)
        {
            runStatisticsRecorder.RecordRun(statuses);
        }

        return settingsUpdated;
    }

    private void ApplyPendingPersonalBestUpdates(AppSettings settings, PendingPersonalBestUpdates updates)
    {
        List<PendingPersonalBestSegmentUpdate> segmentUpdates = updates.SegmentUpdates.Values.ToList();
        foreach (PendingPersonalBestSegmentUpdate update in segmentUpdates)
        {
            settings.SetPersonalBestSegmentText(update.GroupKey, update.NewTimeText);
        }

        if (segmentUpdates.Count > 0)
        {
            (string bossName, string previousTimeText, string newTimeText) = BuildSnapshotLabel(segmentUpdates);
            ReferenceSplitSet snapshot = personalBestSnapshotStore.SavePersonalBestSegmentSnapshot(
                settings.Comparison.PersonalBestSegmentTimes,
                bossName,
                previousTimeText,
                newTimeText);
            AddPersonalBestSnapshot(settings.Comparison.PersonalBestSegmentSets, snapshot);
            settings.Comparison.ActivePersonalBestSegmentSet = snapshot.Name;
        }

        if (updates.TimeUpdate is PendingPersonalBestTimeUpdate timeUpdate)
        {
            settings.Comparison.PersonalBestTimes = new Dictionary<string, string>(
                timeUpdate.Splits,
                StringComparer.OrdinalIgnoreCase);
            ReferenceSplitSet snapshot = personalBestSnapshotStore.SavePersonalBestTimeSnapshot(
                settings.Comparison.PersonalBestTimes,
                timeUpdate.BossName,
                timeUpdate.PreviousTimeText,
                timeUpdate.NewTimeText);
            AddPersonalBestSnapshot(settings.Comparison.PersonalBestTimeSets, snapshot);
            settings.Comparison.ActivePersonalBestTimeSet = snapshot.Name;
        }

    }

    private static PendingPersonalBestUpdates BuildPendingPersonalBestUpdates(
        AppSettings routeSettings,
        AppSettings baselineSettings,
        IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        var segmentUpdates = new Dictionary<string, PendingPersonalBestSegmentUpdate>(StringComparer.OrdinalIgnoreCase);
        AddPendingSegmentBestUpdates(routeSettings, baselineSettings, statuses, segmentUpdates);

        PendingPersonalBestTimeUpdate? timeUpdate = BuildPendingTimeBestUpdate(
            routeSettings,
            baselineSettings,
            statuses);
        return new PendingPersonalBestUpdates(segmentUpdates, timeUpdate);
    }

    private static void AddPendingSegmentBestUpdates(
        AppSettings routeSettings,
        AppSettings baselineSettings,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        Dictionary<string, PendingPersonalBestSegmentUpdate> segmentUpdates)
    {
        Dictionary<string, SplitStatusSnapshot> statusById = statuses
            .ToDictionary(status => status.Definition.Id, StringComparer.OrdinalIgnoreCase);
        List<RouteGroup> groups = SplitRouteGroups.Build(routeSettings);
        if (!TryGetSegmentBestCompletionLimit(groups, statusById, out int lastCompletedGroupIndex))
        {
            return;
        }

        TimeSpan previousGroupSplitTime = TimeSpan.Zero;
        for (int groupIndex = 0; groupIndex <= lastCompletedGroupIndex; groupIndex++)
        {
            RouteGroup group = groups[groupIndex];
            if (!TryGetCompleteGroupSplitTime(group, statusById, out TimeSpan groupSplitTime))
            {
                continue;
            }

            TimeSpan segmentTime = groupSplitTime - previousGroupSplitTime;
            previousGroupSplitTime = groupSplitTime;
            if (segmentTime < TimeSpan.Zero)
            {
                continue;
            }

            if (baselineSettings.Comparison.PersonalBestSegmentTimes.TryGetValue(group.Key, out string? existingText) &&
                TimeText.TryParse(existingText, out TimeSpan existingSegment) &&
                existingSegment <= segmentTime)
            {
                continue;
            }

            segmentUpdates[group.Key] = new PendingPersonalBestSegmentUpdate(
                group.Key,
                SplitRouteGroups.GetGroupDisplayName(group, routeSettings),
                existingText ?? string.Empty,
                TimeText.FormatRecord(segmentTime),
                segmentTime);
        }
    }

    private static bool TryGetSegmentBestCompletionLimit(
        IReadOnlyList<RouteGroup> groups,
        Dictionary<string, SplitStatusSnapshot> statusById,
        out int lastCompletedGroupIndex)
    {
        lastCompletedGroupIndex = -1;
        for (int i = 0; i < groups.Count; i++)
        {
            if (TryGetCompleteGroupSplitTime(groups[i], statusById, out _))
            {
                lastCompletedGroupIndex = i;
            }
        }

        if (lastCompletedGroupIndex < 0)
        {
            return false;
        }

        for (int i = 0; i <= lastCompletedGroupIndex; i++)
        {
            RouteGroup group = groups[i];
            foreach (SplitRouteEntry entry in group.Entries)
            {
                if (!statusById.TryGetValue(entry.Id, out SplitStatusSnapshot? status) ||
                    status.IsSkipped ||
                    status.Time is null)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryGetCompleteGroupSplitTime(
        RouteGroup group,
        Dictionary<string, SplitStatusSnapshot> statusById,
        out TimeSpan splitTime)
    {
        splitTime = TimeSpan.Zero;
        bool found = false;
        foreach (SplitRouteEntry entry in group.Entries)
        {
            if (!statusById.TryGetValue(entry.Id, out SplitStatusSnapshot? status) ||
                status.Time is not TimeSpan candidate)
            {
                return false;
            }

            if (!found || candidate > splitTime)
            {
                splitTime = candidate;
                found = true;
            }
        }

        return found;
    }

    private static PendingPersonalBestTimeUpdate? BuildPendingTimeBestUpdate(
        AppSettings routeSettings,
        AppSettings baselineSettings,
        IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        if (!IsCumulativePersonalBestEligible(statuses))
        {
            return null;
        }

        SplitStatusSnapshot? moonLordStatus = statuses.FirstOrDefault(status =>
            !status.Definition.IsAttached &&
            SplitCatalog.IsMoonLordSplit(status.Definition));
        if (moonLordStatus?.Time is not TimeSpan moonLordTime)
        {
            return null;
        }

        bool hasExistingMoonLordTime = SplitConditionDataRows.TryGetSplitTime(
            routeSettings,
            baselineSettings.Comparison.PersonalBestTimes,
            moonLordStatus.Definition,
            out TimeSpan existingMoonLordTime);
        string existingMoonLordText = hasExistingMoonLordTime
            ? TimeText.FormatRecord(existingMoonLordTime)
            : string.Empty;
        if (hasExistingMoonLordTime &&
            existingMoonLordTime <= moonLordTime)
        {
            return null;
        }

        return new PendingPersonalBestTimeUpdate(
            moonLordStatus.Definition.DisplayName,
            existingMoonLordText,
            TimeText.FormatRecord(moonLordTime),
            moonLordTime,
            BuildCompletedSplitValues(routeSettings, statuses));
    }

    private static bool IsCumulativePersonalBestEligible(IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        bool hasMainSplit = false;
        foreach (SplitStatusSnapshot status in statuses)
        {
            if (status.Definition.IsAttached)
            {
                continue;
            }

            hasMainSplit = true;
            if (status.IsSkipped || status.Time is null)
            {
                return false;
            }
        }

        return hasMainSplit;
    }

    private static Dictionary<string, string> BuildCompletedSplitValues(
        AppSettings settings,
        IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (SplitStatusSnapshot status in statuses)
        {
            if (status.Time is not TimeSpan splitTime)
            {
                continue;
            }

            string formatted = TimeText.FormatRecord(splitTime);
            IReadOnlyList<SplitConditionDataRow> rows = SplitConditionDataRows
                .ForSplit(settings, status.Definition.Id)
                .ToList();

            if (status.Definition.IsAttached)
            {
                if (TryGetAttachedCompletedSplitRow(status, rows, out SplitConditionDataRow attachedRow))
                {
                    values[attachedRow.Key] = formatted;
                }

                continue;
            }

            foreach (SplitConditionDataRow row in rows)
            {
                if (status.TryGetFactCompletionTime(row.Condition.FactKey, out TimeSpan factTime))
                {
                    values[row.Key] = TimeText.FormatRecord(factTime);
                    continue;
                }

                if (status.CompletedFactKeys.Count == 0 ||
                    status.CompletedFactKeys.Contains(row.Condition.FactKey, StringComparer.OrdinalIgnoreCase))
                {
                    values[row.Key] = formatted;
                }
            }
        }

        return values;
    }

    private static bool TryGetAttachedCompletedSplitRow(
        SplitStatusSnapshot status,
        IReadOnlyList<SplitConditionDataRow> rows,
        out SplitConditionDataRow row)
    {
        row = default!;
        if (rows.Count == 0)
        {
            return false;
        }

        row = rows[0];
        return true;
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
