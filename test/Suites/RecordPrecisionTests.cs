using System.Globalization;
using TerrariaSplit.Statistics;
using TerrariaSplit.UI.Rendering;
using TerrariaSplit.UI.Wpf;
using TerrariaSplit.UI.Wpf.Windows;

namespace TerrariaSplit.Tests;

internal static class RecordPrecisionTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("record precision parses decimal times exactly and round-trips every centisecond", TestSuite.Core, ExactTimeParsing);
        yield return TestCase.Sync("record precision aligns archived cumulatives segment PBs statistics and completion animations", TestSuite.Flow, RecordPrecisionJourney);
        yield return TestCase.Async("record precision statistics sum the displayed segment bests without losing a centisecond", TestSuite.Windows, StatisticsSumJourney);
    }

    private static void ExactTimeParsing()
    {
        for (int hundredths = 0; hundredths < 6000; hundredths++)
        {
            TimeSpan expected = TimeSpan.FromTicks(hundredths * 100_000L);
            string text = $"0:{hundredths / 100:00}.{hundredths % 100:00}";
            Check.True(TimeText.TryParse(text, out TimeSpan actual));
            Check.Equal(expected, actual);
            Check.Equal(text, TimeText.FormatRecord(actual));
        }

        Check.True(TimeText.TryParse("12:34:56.1234567", out TimeSpan precise));
        Check.Equal(12 * TimeSpan.TicksPerHour + 34 * TimeSpan.TicksPerMinute + 561_234_567L, precise.Ticks);
        Check.Equal("12:34:56.12", TimeText.FormatRecord(precise));
        Check.Equal(1_200_000L, TimeText.ToRecordPrecision(precise).Ticks % TimeSpan.TicksPerSecond);
        Check.True(TimeText.TryParse("0:00.00000019", out TimeSpan subTick));
        Check.Equal(1L, subTick.Ticks);
        Check.True(TimeText.TryParse("256204778:48:05.4775807", out TimeSpan maximum));
        Check.Equal(TimeSpan.MaxValue, maximum);

        foreach (string? invalid in new[]
        {
            null, "", "0:NaN", "0:Infinity", "0:-1", "0:60", "60:00", "1:60:00", "0:1e2",
            "2147483647:59:59", "256204778:48:05.4775808"
        })
        {
            Check.False(TimeText.TryParse(invalid, out TimeSpan rejected));
            Check.Equal(TimeSpan.Zero, rejected);
        }
    }

    private static void RecordPrecisionJourney()
    {
        AppSettings settings = CreateSettings(5, includeAttached: true);
        settings.Route.SplitRoute[0].Condition = SplitCondition.All(
        [
            SplitCondition.Fact("event:precision-first-a"),
            SplitCondition.Fact("event:precision-first-b")
        ]);
        long[] rawTicks = [153_490_000, 154_500_000, 155_740_000, 156_890_000, 220_002_000, 234_801_000];
        SplitStatusSnapshot[] statuses = SplitCatalog.Build(settings).Select((definition, index) =>
        {
            string[] facts = definition.Condition.ToFlatGroup().GetFactConditions().Select(fact => fact.FactKey).ToArray();
            TimeSpan rawTime = TimeSpan.FromTicks(rawTicks[index]);
            return new SplitStatusSnapshot(definition, rawTime, IsSkipped: false, facts,
                facts.ToDictionary(fact => fact, _ => rawTime, StringComparer.OrdinalIgnoreCase));
        }).ToArray();

        var finalizer = new RunFinalizer();
        PersonalBestFinalizationPlan plan = finalizer.CreatePersonalBestPlan(1, settings, settings, statuses)
            ?? throw new InvalidOperationException("Expected an initial personal best plan.");
        Check.True(plan.SegmentSnapshot is not null && plan.TimeSnapshot is not null);
        string[] expectedSegments = ["0:15.34", "0:00.23", "0:00.11", "0:06.32", "0:01.48"];
        string[] expectedCumulatives = ["0:15.34", "0:15.57", "0:15.68", "0:22.00", "0:23.48"];
        SplitStatusSnapshot[] mainStatuses = statuses.Where(status => !status.Definition.IsAttached).ToArray();
        for (int i = 0; i < mainStatuses.Length; i++)
        {
            Check.Equal(expectedSegments[i], plan.SegmentBestValues![mainStatuses[i].Definition.Id]);
            Check.True(SplitConditionDataRows.TryGetSplitTime(settings, plan.PersonalBestTimes!,
                mainStatuses[i].Definition, out TimeSpan cumulative));
            Check.Equal(expectedCumulatives[i], TimeText.FormatRecord(cumulative));
        }

        Check.True(RunFinalizer.TryApplySuccessfulPlan(settings, plan,
            new PersonalBestFinalizationResult(plan.PlanId, Approved: true,
                Snapshot("Segments", plan.SegmentSnapshot!.Splits),
                Snapshot("Cumulative", plan.TimeSnapshot!.Splits), []), out var failures));
        Check.Equal(0, failures.Count);
        var reference = Snapshot("Reference", plan.PersonalBestTimes!);
        settings.Comparison.ReferenceSplitSets = [reference];
        settings.Comparison.ActiveReferenceSplitSet = reference.Name;

        using var dir = new TestDirectory("record-precision");
        var timeSets = new SplitTimeSetRepository(new AppContextRuntimeDataPaths(dir.Path));
        var repository = new RunStatsRepository(timeSets);
        repository.RecordRun(statuses);
        RunStats saved = repository.Load();
        StatisticsTableRow[] rows = StatisticsTableBuilder.Build(settings, reference, saved.LastRunSplits)
            .Where(row => !row.ConditionRow.IsAttached && row.GroupOffset == 0).ToArray();
        Check.Sequence(expectedSegments, rows.Select(row => row.PersonalSegmentText));
        Check.Sequence(expectedSegments, rows.Select(row => row.PersonalBestSegmentText));
        Check.Sequence(expectedSegments, rows.Select(row => row.ReferenceSegmentText));

        TimeSpan sum = TimeSpan.Zero;
        var animations = new OverlayAnimationController();
        for (int i = 0; i < statuses.Length; i++)
        {
            Check.Equal(rawTicks[i], statuses[i].Time!.Value.Ticks);
            if (statuses[i].Definition.IsAttached)
            {
                continue;
            }

            Check.True(SplitComparisonService.TryGetCompletedSegmentTime(settings, statuses, i, out TimeSpan segment));
            Check.Equal(plan.SegmentBestValues![statuses[i].Definition.Id], TimeText.FormatRecord(segment));
            sum += segment;
            SplitCompletionAnimation animation = SplitCompletionAnimationFactory.Create(settings, statuses, i, DateTime.UnixEpoch)!;
            Check.Equal(segment, animation.SegmentTime);
            Check.Equal(TimeText.ToRecordPrecision(statuses[i].Time!.Value), animation.SplitTime);
            Check.Equal(TimeSpan.Zero, animation.PersonalBestSegmentComparison.Delta);
            Check.Equal(TimeSpan.Zero, animation.ReferenceSplitComparison.Delta);
            Check.Equal(TimeSpan.Zero, SplitComparisonService.GetSplitComparison(
                settings, SplitTimerPhase.Paused, statuses[^1].Time!.Value, statuses[i], isCurrent: false).Delta);
            animations.TrackSegmentBestDeltaHighlight(settings, statuses, i);
        }

        Check.Equal("0:23.48", TimeText.FormatRecord(sum));
        Check.Equal(0, animations.SegmentBestDeltaHighlights.Count);
        var timer = new SplitTimer();
        timer.ApplyState(new SplitTimerState(SplitTimerPhase.Paused, statuses[^1].Time!.Value, 0));
        Check.Equal(rawTicks[^1], timer.ElapsedAt(0).Ticks);

        SplitStatusSnapshot[] sameRecord = statuses.Select(status => status with
        {
            Time = status.Time!.Value - TimeSpan.FromTicks(1)
        }).ToArray();
        Check.True(finalizer.CreatePersonalBestPlan(2, settings, settings, sameRecord) is null);
        SplitStatusSnapshot[] faster = statuses.ToArray();
        TimeSpan fasterFinal = TimeSpan.FromTicks(234_799_999);
        faster[^1] = faster[^1] with
        {
            Time = fasterFinal,
            FactCompletionTimes = faster[^1].CompletedFactKeys.ToDictionary(fact => fact, _ => fasterFinal)
        };
        PersonalBestFinalizationPlan improvement = finalizer.CreatePersonalBestPlan(3, settings, settings, faster)!;
        Check.Equal("0:23.47", improvement.TimeSnapshot!.NewTime);
        Check.Equal("0:01.47", improvement.SegmentBestValues![faster[^1].Definition.Id]);
    }

    private static Task StatisticsSumJourney(CancellationToken cancellationToken) => StaTestHost.RunAsync(() =>
    {
        WpfBootstrap.EnsureInitialized();
        AppSettings settings = CreateSettings(7, includeAttached: false);
        string[] segments = ["0:05.49", "0:03.27", "0:06.57", "0:00.22", "0:00.11", "0:06.31", "0:01.47"];
        var values = new Dictionary<string, string>();
        TimeSpan cumulative = TimeSpan.Zero;
        IReadOnlyList<SplitConditionDataRow> rows = SplitConditionDataRows.Build(settings);
        for (int i = 0; i < rows.Count; i++)
        {
            Check.True(TimeText.TryParse(segments[i], out TimeSpan segment));
            cumulative += segment;
            settings.Comparison.PersonalBestSegmentTimes[rows[i].SplitId] = segments[i];
            settings.Comparison.PersonalBestTimes[rows[i].Key] = TimeText.FormatRecord(cumulative);
            values[rows[i].Key] = TimeText.FormatRecord(cumulative);
        }

        var reference = Snapshot("Reference", values);
        settings.Comparison.ReferenceSplitSets = [reference];
        settings.Comparison.ActiveReferenceSplitSet = reference.Name;
        using var dir = new TestDirectory("record-precision-statistics");
        var timeSets = new SplitTimeSetRepository(new AppContextRuntimeDataPaths(dir.Path));
        timeSets.SaveLastRun(values);
        var window = new StatisticsWindow(settings, new RunStatsRepository(timeSets), timeSets);
        Check.Equal("0:23.44", window.SelectedTotalDisplayText);
        Check.Equal("0:23.44", window.PersonalBestDisplayText);
        Check.Equal("0:23.44", window.SumOfBestDisplayText);
        ((System.Windows.Controls.CheckBox)window.FindName("ShowComparisonToggle")).IsChecked = true;
        Check.Equal("+0.00", ((System.Windows.Controls.TextBlock)window.FindName("KpiSobDeltaText")).Text);
        Check.Equal(13d, ((System.Windows.Controls.TextBlock)window.FindName("KpiSobDeltaText")).FontSize);

        settings.Comparison.PersonalBestSegmentTimes[rows[5].SplitId] = "0:06.30";
        reference.Splits[rows[^1].Key] = "1:23.45";
        ((System.Windows.Controls.CheckBox)window.FindName("ShowComparisonToggle")).IsChecked = false;
        ((System.Windows.Controls.CheckBox)window.FindName("ShowComparisonToggle")).IsChecked = true;
        Check.Equal("+0.01", ((System.Windows.Controls.TextBlock)window.FindName("KpiSobDeltaText")).Text);
        Check.Equal("-1:00.01", window.ReferenceTotalDeltaDisplayText);
        window.Close();
    }, cancellationToken);

    private static AppSettings CreateSettings(int mainCount, bool includeAttached)
    {
        AppSettings settings = AppSettingsDefaults.Create();
        settings.General.Language = "English";
        settings.Comparison.UsePersonalBestAsReferenceTime = false;
        settings.Comparison.AutoUpdatePersonalBestData = true;
        settings.Comparison.AskBeforeUpdatingPersonalBestData = false;
        settings.Comparison.PersonalBestTimes.Clear();
        settings.Comparison.PersonalBestSegmentTimes.Clear();
        settings.Route.SplitRoute = Enumerable.Range(0, mainCount).Select(index =>
        {
            string id = "precision-" + index.ToString(CultureInfo.InvariantCulture);
            return new SplitRouteEntry { Id = id, DisplayName = id, Enabled = true, Condition = SplitCondition.Fact("event:" + id) };
        }).ToList();
        if (includeAttached)
        {
            settings.Route.SplitRoute.Insert(1, new SplitRouteEntry
            {
                Id = "precision-attached", DisplayName = "Attached", Enabled = true, IsAttached = true,
                Condition = SplitCondition.Fact("event:precision-attached")
            });
        }

        return settings;
    }

    private static ReferenceSplitSet Snapshot(string name, IReadOnlyDictionary<string, string> values) => new()
    {
        Name = name,
        Splits = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase)
    };
}
