using TerrariaSplit.Configuration;
using TerrariaSplit.Models;
using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.Race.Client;

public static class RaceSplitReportFactory
{
    public static IReadOnlyList<RaceSplitReport> CreateProgressReports(
        string roomCode,
        string nickname,
        IReadOnlyList<SplitStatusSnapshot> statuses,
        DateTimeOffset reportedAtUtc)
    {
        var reports = new List<RaceSplitReport>();
        for (int splitIndex = 0; splitIndex < statuses.Count; splitIndex++)
        {
            SplitStatusSnapshot status = statuses[splitIndex];
            if (status.IsSkipped)
            {
                continue;
            }

            AddConditionReports(
                reports,
                roomCode,
                nickname,
                splitIndex,
                status,
                reportedAtUtc);
        }

        return reports;
    }

    public static string CreateProgressKey(RaceSplitReport report)
    {
        return string.Join(
            "|",
            report.SplitIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            report.ConditionIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            report.FactKey ?? string.Empty);
    }

    private static void AddConditionReports(
        List<RaceSplitReport> reports,
        string roomCode,
        string nickname,
        int splitIndex,
        SplitStatusSnapshot status,
        DateTimeOffset reportedAtUtc)
    {
        if (!IsMultiIconProgressDefinition(status.Definition))
        {
            AddCompletedSplitReport(reports, roomCode, nickname, splitIndex, status, reportedAtUtc);
            return;
        }

        SplitCondition[] facts = status.Definition.Condition
            .ToFlatGroup()
            .GetFactConditions()
            .ToArray();

        for (int conditionIndex = 0; conditionIndex < facts.Length; conditionIndex++)
        {
            SplitCondition fact = facts[conditionIndex];
            if (!TryGetConditionCompletionTime(status, fact.FactKey, out TimeSpan completion))
            {
                continue;
            }

            SplitTargetDefinition? target = SplitCatalog.TryGetTargetByFactKey(fact.FactKey, out SplitTargetDefinition resolved)
                ? resolved
                : null;
            bool isSplitComplete = status.Time.HasValue && status.Time.Value == completion;
            reports.Add(new RaceSplitReport(
                roomCode,
                nickname,
                splitIndex,
                status.Definition.Id,
                (long)Math.Round(completion.TotalMilliseconds),
                ReportedAtUtc: reportedAtUtc,
                ConditionIndex: conditionIndex,
                FactKey: fact.FactKey,
                TargetId: target?.Id,
                IconFileName: ResolveConditionIconFileName(status.Definition, target, conditionIndex) ??
                    ResolveFallbackIconFileName(status),
                IconDisplayName: target?.DisplayName,
                IsSplitComplete: isSplitComplete));
        }

        AddCompletedSplitReport(reports, roomCode, nickname, splitIndex, status, reportedAtUtc);
    }

    private static void AddCompletedSplitReport(
        List<RaceSplitReport> reports,
        string roomCode,
        string nickname,
        int splitIndex,
        SplitStatusSnapshot status,
        DateTimeOffset reportedAtUtc)
    {
        if (reports.Any(report =>
                report.SplitIndex == splitIndex &&
                string.Equals(report.SplitId, status.Definition.Id, StringComparison.OrdinalIgnoreCase) &&
                report.IsSplitComplete) ||
            status.Time is not TimeSpan elapsed)
        {
            return;
        }

        SplitTargetDefinition? fallbackTarget = ResolveCompletedTarget(status);
        reports.Add(new RaceSplitReport(
            roomCode,
            nickname,
            splitIndex,
            status.Definition.Id,
            (long)Math.Round(elapsed.TotalMilliseconds),
            ReportedAtUtc: reportedAtUtc,
            ConditionIndex: 0,
            FactKey: ResolveCompletedFactKey(status),
            TargetId: fallbackTarget?.Id,
            IconFileName: ResolveConditionIconFileName(status.Definition, fallbackTarget, 0) ??
                ResolveFallbackIconFileName(status),
            IconDisplayName: fallbackTarget?.DisplayName,
            IsSplitComplete: true));
    }

    private static bool IsMultiIconProgressDefinition(SplitDefinition definition)
    {
        return definition.IconLightingConditions.Count == 0 &&
            definition.IconFileNames.Count > 1 &&
            definition.IconKeys.Count > 1;
    }

    private static bool TryGetConditionCompletionTime(
        SplitStatusSnapshot status,
        string factKey,
        out TimeSpan completion)
    {
        if (status.TryGetFactCompletionTime(factKey, out completion))
        {
            return true;
        }

        if (status.CompletedFactKeys.Contains(factKey, StringComparer.OrdinalIgnoreCase) &&
            status.Time is TimeSpan splitTime)
        {
            completion = splitTime;
            return true;
        }

        return false;
    }

    private static int ResolveCompletedConditionIndex(SplitStatusSnapshot status)
    {
        string? factKey = ResolveCompletedFactKey(status);
        if (string.IsNullOrWhiteSpace(factKey))
        {
            return 0;
        }

        SplitCondition[] facts = status.Definition.Condition
            .ToFlatGroup()
            .GetFactConditions()
            .ToArray();
        for (int index = 0; index < facts.Length; index++)
        {
            if (string.Equals(facts[index].FactKey, factKey, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return 0;
    }

    private static string? ResolveCompletedFactKey(SplitStatusSnapshot status)
    {
        if (status.FactCompletionTimes?.Count is > 0)
        {
            return status.FactCompletionTimes
                .OrderBy(static item => item.Value)
                .ThenBy(static item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Last()
                .Key;
        }

        return status.CompletedFactKeys.LastOrDefault();
    }

    private static SplitTargetDefinition? ResolveCompletedTarget(SplitStatusSnapshot status)
    {
        string? factKey = ResolveCompletedFactKey(status);
        return !string.IsNullOrWhiteSpace(factKey) &&
            SplitCatalog.TryGetTargetByFactKey(factKey, out SplitTargetDefinition target)
                ? target
                : null;
    }

    private static string? ResolveFallbackIconFileName(SplitStatusSnapshot status)
    {
        return status.Definition.IconFileNames.Count > 0
            ? status.Definition.IconFileNames[0]
            : null;
    }

    private static string? ResolveConditionIconFileName(
        SplitDefinition definition,
        SplitTargetDefinition? target,
        int conditionIndex)
    {
        if (target is not null)
        {
            for (int index = 0; index < definition.IconKeys.Count && index < definition.IconFileNames.Count; index++)
            {
                if (string.Equals(definition.IconKeys[index], target.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return definition.IconFileNames[index];
                }
            }
        }

        if (definition.IconFileNames.Count == 1)
        {
            return definition.IconFileNames[0];
        }

        return conditionIndex >= 0 && conditionIndex < definition.IconFileNames.Count
            ? definition.IconFileNames[conditionIndex]
            : null;
    }
}
