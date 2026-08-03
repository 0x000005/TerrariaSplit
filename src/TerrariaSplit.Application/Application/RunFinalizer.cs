using System.Collections.ObjectModel;

namespace TerrariaSplit.Application;

internal sealed class RunFinalizer
{
    public PersonalBestFinalizationPlan? CreatePersonalBestPlan(
        long planId,
        AppSettings routeSettings,
        AppSettings baselineSettings,
        IReadOnlyList<SplitStatusSnapshot> statuses)
    {
        PendingPersonalBestUpdates updates = BuildPendingPersonalBestUpdates(
            routeSettings,
            baselineSettings,
            statuses);
        if (!updates.HasUpdates || !baselineSettings.Comparison.AutoUpdatePersonalBestData)
        {
            return null;
        }

        PersonalBestSnapshotRequest? segmentSnapshot = null;
        IReadOnlyDictionary<string, string>? segmentBestValues = null;
        List<PendingPersonalBestSegmentUpdate> segmentUpdates = updates.SegmentUpdates.Values.ToList();
        if (segmentUpdates.Count > 0)
        {
            var values = new Dictionary<string, string>(
                baselineSettings.Comparison.PersonalBestSegmentTimes,
                StringComparer.OrdinalIgnoreCase);
            foreach (PendingPersonalBestSegmentUpdate update in segmentUpdates)
            {
                values[update.GroupKey] = update.NewTimeText;
            }

            (string bossName, string previousTimeText, string newTimeText) = BuildSnapshotLabel(segmentUpdates);
            segmentBestValues = Freeze(values);
            segmentSnapshot = new PersonalBestSnapshotRequest(
                segmentBestValues,
                bossName,
                previousTimeText,
                newTimeText);
        }

        PersonalBestSnapshotRequest? timeSnapshot = null;
        IReadOnlyDictionary<string, string>? personalBestTimes = null;
        if (updates.TimeUpdate is PendingPersonalBestTimeUpdate timeUpdate)
        {
            personalBestTimes = Freeze(timeUpdate.Splits);
            timeSnapshot = new PersonalBestSnapshotRequest(
                personalBestTimes,
                timeUpdate.BossName,
                timeUpdate.PreviousTimeText,
                timeUpdate.NewTimeText);
        }

        return new PersonalBestFinalizationPlan(
            planId,
            baselineSettings.Comparison.AskBeforeUpdatingPersonalBestData,
            BuildPersonalBestUpdatePromptText(updates, baselineSettings),
            segmentBestValues,
            segmentSnapshot,
            personalBestTimes,
            timeSnapshot);
    }

    public static bool TryApplySuccessfulPlan(
        AppSettings settings,
        PersonalBestFinalizationPlan plan,
        PersonalBestFinalizationResult result,
        out IReadOnlyList<OperationResult> failures)
    {
        if (result.PlanId != plan.PlanId)
        {
            failures =
            [
                OperationResult.Failure(
                    $"Ignored mismatched personal best finalization result {result.PlanId}; expected {plan.PlanId}.")
            ];
            return false;
        }

        if (!result.Approved)
        {
            failures = [];
            return false;
        }

        var validationFailures = new List<OperationResult>(result.Failures);
        if (validationFailures.Count == 0 &&
            plan.SegmentSnapshot is not null &&
            result.SegmentSnapshot is null)
        {
            validationFailures.Add(OperationResult.Failure(
                "Personal best segment snapshot was not returned after persistence."));
        }

        if (validationFailures.Count == 0 &&
            plan.TimeSnapshot is not null &&
            result.TimeSnapshot is null)
        {
            validationFailures.Add(OperationResult.Failure(
                "Personal best time snapshot was not returned after persistence."));
        }

        if (validationFailures.Count > 0)
        {
            failures = validationFailures;
            return false;
        }

        if (plan.SegmentBestValues is not null)
        {
            settings.Comparison.PersonalBestSegmentTimes = new Dictionary<string, string>(
                plan.SegmentBestValues,
                StringComparer.OrdinalIgnoreCase);
            ReferenceSplitSet snapshot = CloneSnapshot(result.SegmentSnapshot!);
            AddPersonalBestSnapshot(settings.Comparison.PersonalBestSegmentSets, snapshot);
            settings.Comparison.ActivePersonalBestSegmentSet = snapshot.Name;
        }

        if (plan.PersonalBestTimes is not null)
        {
            settings.Comparison.PersonalBestTimes = new Dictionary<string, string>(
                plan.PersonalBestTimes,
                StringComparer.OrdinalIgnoreCase);
            ReferenceSplitSet snapshot = CloneSnapshot(result.TimeSnapshot!);
            AddPersonalBestSnapshot(settings.Comparison.PersonalBestTimeSets, snapshot);
            settings.Comparison.ActivePersonalBestTimeSet = snapshot.Name;
        }

        failures = [];
        return true;
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
                TimeText.FormatRecord(segmentTime));
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
                if (TryGetAttachedCompletedSplitRow(rows, out SplitConditionDataRow attachedRow))
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

    private static string BuildPersonalBestUpdatePromptText(
        PendingPersonalBestUpdates updates,
        AppSettings settings)
    {
        var lines = new List<string>();
        if (updates.TimeUpdate is PendingPersonalBestTimeUpdate timeUpdate)
        {
            lines.Add(
                $"{Localizer.Get("Cumulative", settings)}: {timeUpdate.BossName} " +
                FormatPromptChange(timeUpdate.PreviousTimeText, timeUpdate.NewTimeText, settings));
        }

        foreach (PendingPersonalBestSegmentUpdate update in updates.SegmentUpdates.Values)
        {
            lines.Add(
                $"{Localizer.Get("Segment", settings)}: {update.BossName} " +
                FormatPromptChange(update.PreviousTimeText, update.NewTimeText, settings));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatPromptChange(
        string previousTimeText,
        string newTimeText,
        AppSettings settings)
    {
        string oldText = string.IsNullOrWhiteSpace(previousTimeText)
            ? Localizer.Get("None", settings)
            : previousTimeText;
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

    private static IReadOnlyDictionary<string, string> Freeze(
        IReadOnlyDictionary<string, string> values)
    {
        return new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase));
    }

    private static ReferenceSplitSet CloneSnapshot(ReferenceSplitSet source)
    {
        return new ReferenceSplitSet
        {
            Name = source.Name,
            Splits = new Dictionary<string, string>(
                source.Splits ?? [],
                StringComparer.OrdinalIgnoreCase)
        };
    }

    private static void AddPersonalBestSnapshot(
        List<ReferenceSplitSet> sets,
        ReferenceSplitSet snapshot)
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
        Dictionary<string, string> Splits);

    private sealed record PendingPersonalBestSegmentUpdate(
        string GroupKey,
        string BossName,
        string PreviousTimeText,
        string NewTimeText);
}
