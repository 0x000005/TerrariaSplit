using TerrariaSplit.Statistics;

namespace TerrariaSplit.Tests;

internal static class CoreAndRunTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("timer lifecycle preserves elapsed time across pause, resume, edit and reset", TestSuite.Core, TimerLifecycle);
        yield return TestCase.Sync("route progression handles initial state, attached splits, skipping and practice edits", TestSuite.Core, RouteProgression);
        yield return TestCase.Sync("statistics preserve an unknown segment boundary after a missing cumulative split", TestSuite.Core, StatisticsMissingSplitBoundary);
        yield return TestCase.Sync("fact conditions evaluate boolean, integer, all, any and threshold semantics", TestSuite.Core, FactConditionMatrix);
        yield return TestCase.Sync("timer and tracker state round-trip without sharing live state", TestSuite.Core, StateRoundTrip);
    }

    private static void TimerLifecycle()
    {
        var timer = new SplitTimer();
        long start = 10_000;
        timer.StartAt(start);
        Check.Equal(SplitTimerPhase.Running, timer.Phase);
        Check.Equal(TimeSpan.FromSeconds(3), timer.ElapsedAt(start + TestTiming.Timestamp(TimeSpan.FromSeconds(3))));

        timer.TogglePauseAt(start + TestTiming.Timestamp(TimeSpan.FromSeconds(5)));
        Check.Equal(SplitTimerPhase.Paused, timer.Phase);
        Check.Equal(TimeSpan.FromSeconds(5), timer.ElapsedAt(long.MaxValue));
        timer.TogglePauseAt(start + TestTiming.Timestamp(TimeSpan.FromSeconds(9)));
        timer.StopAt(start + TestTiming.Timestamp(TimeSpan.FromSeconds(11)));
        Check.Equal(TimeSpan.FromSeconds(7), timer.ElapsedAt(long.MaxValue));

        timer.SetPracticeElapsed(TimeSpan.FromSeconds(-1), start);
        Check.Equal(TimeSpan.Zero, timer.ElapsedAt(long.MaxValue));
        timer.Reset();
        Check.Equal(SplitTimerPhase.NotStarted, timer.Phase);
    }

    private static void RouteProgression()
    {
        SplitDefinition[] route =
        [
            Definition("first", "fact:first"),
            Definition("attached", "fact:attached", attached: true),
            Definition("final", "fact:final")
        ];
        var tracker = new SplitTracker();
        tracker.SetDefinitions(route);
        tracker.OnRunStarted(Facts(("fact:first", false), ("fact:attached", false), ("fact:final", false)));

        SplitRecord? first = tracker.Update(Facts(("fact:first", true), ("fact:attached", false), ("fact:final", false)), false, TimeSpan.FromSeconds(1));
        Check.Equal("first", first?.Name);
        SplitRecord? attached = tracker.Update(Facts(("fact:first", true), ("fact:attached", true), ("fact:final", false)), false, TimeSpan.FromSeconds(2));
        Check.Equal("attached", attached?.Name);
        SplitRecord? final = tracker.Update(Facts(("fact:first", true), ("fact:attached", true), ("fact:final", true)), false, TimeSpan.FromSeconds(5));
        Check.Equal("final", final?.Name);
        Check.True(tracker.Statuses[0].IsCompleted);
        Check.True(tracker.Statuses[1].IsCompleted);
        Check.True(tracker.Statuses[2].IsCompleted);

        tracker.SetPracticeTime(2, TimeSpan.FromSeconds(1));
        Check.Equal(TimeSpan.FromSeconds(1), tracker.Statuses[0].Time);
        Check.Equal(TimeSpan.FromSeconds(2), tracker.Statuses[1].Time);
        Check.Equal(TimeSpan.FromSeconds(2), tracker.Statuses[2].Time);

        tracker.SetPracticeTime(0, TimeSpan.FromSeconds(3));
        Check.Equal(TimeSpan.FromSeconds(3), tracker.Statuses[0].Time);
        Check.Equal(TimeSpan.FromSeconds(3), tracker.Statuses[1].Time);
        Check.Equal(TimeSpan.FromSeconds(3), tracker.Statuses[2].Time);

        var forwardRepair = new SplitTracker();
        forwardRepair.SetDefinitions(route);
        forwardRepair.SetPracticeTime(0, TimeSpan.FromSeconds(10));
        forwardRepair.SetPracticeTime(1, TimeSpan.FromSeconds(20));
        forwardRepair.SetPracticeTime(2, TimeSpan.FromSeconds(45));
        forwardRepair.SetPracticeTime(0, TimeSpan.FromSeconds(30));
        Check.Equal(TimeSpan.FromSeconds(30), forwardRepair.Statuses[0].Time);
        Check.Equal(TimeSpan.FromSeconds(30), forwardRepair.Statuses[1].Time);
        Check.Equal(TimeSpan.FromSeconds(45), forwardRepair.Statuses[2].Time);
    }

    private static void StatisticsMissingSplitBoundary()
    {
        AppSettings settings = AppSettingsDefaults.Create();
        settings.Route.SplitRoute =
        [
            DefinitionEntry("first"),
            DefinitionEntry("missing"),
            DefinitionEntry("third"),
            DefinitionEntry("fourth")
        ];
        SettingsNormalizer.Normalize(settings);
        var reference = new ReferenceSplitSet
        {
            Name = "Reference",
            Splits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["first"] = "00:10",
                ["third"] = "00:30",
                ["fourth"] = "00:45"
            }
        };
        var personal = new Dictionary<string, string>(
            reference.Splits,
            StringComparer.OrdinalIgnoreCase);

        List<StatisticsTableRow> rows =
            StatisticsTableBuilder.Build(settings, reference, personal);
        string tenSeconds = TimeText.FormatRecord(TimeSpan.FromSeconds(10));
        string fifteenSeconds = TimeText.FormatRecord(TimeSpan.FromSeconds(15));

        Check.Sequence(
            [tenSeconds, "--", "--", fifteenSeconds],
            rows.Select(row => row.ReferenceSegmentText));
        Check.Sequence(
            [tenSeconds, "--", "--", fifteenSeconds],
            rows.Select(row => row.PersonalSegmentText));
    }

    private static void FactConditionMatrix()
    {
        TerrariaGameFacts facts = Facts(("boss:a", true), ("boss:b", false), ("items", 3));
        Check.Equal(SplitConditionResult.True, SplitCondition.Fact("boss:a").Evaluate(facts));
        Check.Equal(SplitConditionResult.False, SplitCondition.Fact("boss:b").Evaluate(facts));
        Check.Equal(SplitConditionResult.True, SplitCondition.Fact("items", SplitFactComparison.AtLeast, 3).Evaluate(facts));
        Check.Equal(SplitConditionResult.False, SplitCondition.All([SplitCondition.Fact("boss:a"), SplitCondition.Fact("boss:b")]).Evaluate(facts));
        Check.Equal(SplitConditionResult.True, SplitCondition.Any([SplitCondition.Fact("boss:a"), SplitCondition.Fact("boss:b")]).Evaluate(facts));
        Check.Equal(SplitConditionResult.True, SplitCondition.AtLeast([SplitCondition.Fact("boss:a"), SplitCondition.Fact("boss:b")], 1).Evaluate(facts));
        Check.Equal(SplitConditionResult.Unknown, SplitCondition.Fact("missing").Evaluate(facts));
    }

    private static void StateRoundTrip()
    {
        var originalTimer = new SplitTimer();
        originalTimer.ApplyState(new SplitTimerState(SplitTimerPhase.Paused, TimeSpan.FromSeconds(12), 0));
        var restoredTimer = new SplitTimer();
        restoredTimer.ApplyState(originalTimer.CaptureState());
        originalTimer.Reset();
        Check.Equal(TimeSpan.FromSeconds(12), restoredTimer.ElapsedAt(0));

        SplitDefinition[] route = [Definition("one", "fact:one"), Definition("two", "fact:two")];
        var original = new SplitTracker();
        original.SetDefinitions(route);
        original.OnRunStarted(Facts(("fact:one", false), ("fact:two", false)));
        original.Update(Facts(("fact:one", true), ("fact:two", false)), false, TimeSpan.FromSeconds(4));
        SplitTrackerState state = original.CaptureState();
        var restored = new SplitTracker();
        restored.SetDefinitions(route);
        restored.ApplyState(state);
        original.Reset();
        Check.True(restored.Statuses[0].IsCompleted);
        Check.Equal(TimeSpan.FromSeconds(4), restored.Statuses[0].Time);
        Check.Equal(1, restored.CurrentIndex);
    }

    private static SplitDefinition Definition(string name, string fact, bool attached = false) =>
        new(name, name, SplitCondition.Fact(fact), [], [], [], attached);

    private static SplitRouteEntry DefinitionEntry(string id) =>
        new()
        {
            Id = id,
            DisplayName = id,
            Condition = SplitCondition.Fact("fact:" + id)
        };

    internal static TerrariaGameFacts Facts(params (string Key, object? Value)[] values)
    {
        TerrariaGameFacts.Builder builder = TerrariaGameFacts.CreateBuilder();
        foreach ((string key, object? value) in values)
        {
            if (value is bool boolean) builder.SetBoolean(key, boolean);
            else if (value is int integer) builder.SetInteger(key, integer);
            else builder.Set(key, FactValue.Unknown);
        }
        return builder.Build();
    }
}
