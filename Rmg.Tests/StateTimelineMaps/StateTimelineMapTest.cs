using Rmg.Core.Events;

namespace Rmg.Tests.StateTimelineMaps;

public sealed class StateTimelineMapTest
{
    private static readonly StateKind<int> KeyOffset = StateKinds.KeyOffset;
    private static readonly StateKind<double> Velocity = StateKinds.Velocity;

    private static StateTimelineMap KeyMap(double duration, params TimelineItem<int>[] items) =>
        StateTimelineMap.Create(duration, [StateTimeline.Create(duration, KeyOffset, items)]);

    [Test]
    public async Task Empty_IsEmpty()
    {
        var result = StateTimelineMap.Empty;

        await Assert.That(result.IsEmpty).IsTrue();
        await Assert.That(result.Duration).IsEqualTo(0);
        await Assert.That(result.StateTimelines.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Create_ZeroDuration_ResultsIn_Empty()
    {
        var result = StateTimelineMap.Create(0, [StateTimeline.Create(1, KeyOffset, [new TimelineItem<int>(0, 1)])]);

        await Assert.That(result).IsSameReferenceAs(StateTimelineMap.Empty);
    }

    [Test]
    public async Task Create_NegativeDuration_ResultsIn_ThrownException()
    {
        await Assert.That(() => { StateTimelineMap.Create(-1, []); }).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Create_NoTimelines_ResultsIn_Empty()
    {
        await Assert.That(StateTimelineMap.Create(2, []).IsEmpty).IsTrue();
    }

    [Test]
    public async Task Create_OnlyEmptyTimelines_ResultsIn_Empty()
    {
        var result = StateTimelineMap.Create(2, [KeyOffset.EmptyTimeline]);

        await Assert.That(result.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Create_SingleTimeline_KeepsDurationAndTimeline()
    {
        var result = KeyMap(2, new TimelineItem<int>(0, 1), new TimelineItem<int>(1, 2));

        await Assert.That(result.IsEmpty).IsFalse();
        await Assert.That(result.Duration).IsEqualTo(2);
        await Assert.That(result.StateTimelines.Length).IsEqualTo(1);
        await Assert.That(result.GetStateTimeline(KeyOffset).AsEnumerable())
            .IsEquivalentTo(new[] { new TimelineItem<int>(0, 1), new TimelineItem<int>(1, 2) });
    }

    [Test]
    public async Task Create_TimelinesLongerThanDuration_AreTrimmed()
    {
        var timeline = StateTimeline.Create(4, KeyOffset, [new TimelineItem<int>(0, 1), new TimelineItem<int>(3, 2)]);

        var result = StateTimelineMap.Create(2, [timeline]);

        await Assert.That(result.Duration).IsEqualTo(2);
        await Assert.That(result.GetStateTimeline(KeyOffset).AsEnumerable())
            .IsEquivalentTo(new[] { new TimelineItem<int>(0, 1) });
    }

    [Test]
    public async Task Create_TimelinesAreOrderedByKindName()
    {
        var result = StateTimelineMap.Create(
            2,
            [
                StateTimeline.Create(2, KeyOffset, [new TimelineItem<int>(0, 1)]),
                StateTimeline.Create(2, Velocity, [new TimelineItem<double>(0, 1)]),
            ]
        );

        var names = result.StateTimelines.Select(x => x.StateKind.Name).ToArray();
        await Assert.That(names).IsEquivalentTo(names.Order().ToArray());
        await Assert.That(names.Length).IsEqualTo(2);
    }

    [Test]
    public async Task Create_SameKindTimelines_AreMerged()
    {
        var result = StateTimelineMap.Create(
            2,
            [
                StateTimeline.Create(2, KeyOffset, [new TimelineItem<int>(0, 1)]),
                StateTimeline.Create(2, KeyOffset, [new TimelineItem<int>(0, 2)]),
            ]
        );

        await Assert.That(result.StateTimelines.Length).IsEqualTo(1);
        await Assert.That(result.GetEffectiveStateMapAt(0).GetStateValue(KeyOffset)).IsEqualTo(3);
    }

    [Test]
    public async Task GetStateTimeline_Missing_ResultsIn_EmptyTimeline()
    {
        var input = KeyMap(2, new TimelineItem<int>(0, 1));

        var result = input.GetStateTimeline(Velocity);

        await Assert.That(result.IsEmpty).IsTrue();
    }

    [Test]
    public async Task GetEffectiveStateMapAt_NegativePosition_ResultsIn_ThrownException()
    {
        var input = KeyMap(2, new TimelineItem<int>(0, 1));

        await Assert.That(() => { input.GetEffectiveStateMapAt(-1); }).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task GetEffectiveStateMapAt_Empty_ResultsIn_Default()
    {
        await Assert.That(StateTimelineMap.Empty.GetEffectiveStateMapAt(0).IsDefault).IsTrue();
    }

    [Test]
    public async Task GetEffectiveStateMapAt_AtOrAfterDuration_ResultsIn_Default()
    {
        var input = KeyMap(2, new TimelineItem<int>(0, 1));

        await Assert.That(input.GetEffectiveStateMapAt(2).IsDefault).IsTrue();
        await Assert.That(input.GetEffectiveStateMapAt(5).IsDefault).IsTrue();
    }

    [Test]
    public async Task GetEffectiveStateMapAt_BeforeFirstItem_ResultsIn_Default()
    {
        var input = KeyMap(3, new TimelineItem<int>(1, 1));

        await Assert.That(input.GetEffectiveStateMapAt(0.5).IsDefault).IsTrue();
    }

    [Test]
    [Arguments(0, 1)]
    [Arguments(0.5, 1)]
    [Arguments(1, 2)]
    [Arguments(1.99, 2)]
    public async Task GetEffectiveStateMapAt_ResultsIn_LastItemAtOrBeforePosition(double position, int expected)
    {
        var input = KeyMap(2, new TimelineItem<int>(0, 1), new TimelineItem<int>(1, 2));

        var result = input.GetEffectiveStateMapAt(position);

        await Assert.That(result.GetStateValue(KeyOffset)).IsEqualTo(expected);
    }

    [Test]
    public async Task GetEffectiveStateMapAt_CombinesAllKinds()
    {
        var input = StateTimelineMap.Create(
            4,
            [
                StateTimeline.Create(4, KeyOffset, [new TimelineItem<int>(0, 1), new TimelineItem<int>(2, 5)]),
                StateTimeline.Create(4, Velocity, [new TimelineItem<double>(1, 3)]),
            ]
        );

        var atZero = input.GetEffectiveStateMapAt(0);
        var atOne = input.GetEffectiveStateMapAt(1);
        var atTwo = input.GetEffectiveStateMapAt(2);

        await Assert.That(atZero).IsEqualTo(StateMap.FromStates([KeyOffset.CreateState(1)]));
        await Assert.That(atOne).IsEqualTo(StateMap.FromStates([KeyOffset.CreateState(1), Velocity.CreateState(3)]));
        await Assert.That(atTwo).IsEqualTo(StateMap.FromStates([KeyOffset.CreateState(5), Velocity.CreateState(3)]));
    }

    [Test]
    public async Task StateMapEventTimeline_HasItemAtEveryChangePosition()
    {
        var input = StateTimelineMap.Create(
            4,
            [
                StateTimeline.Create(4, KeyOffset, [new TimelineItem<int>(0, 1), new TimelineItem<int>(2, 5)]),
                StateTimeline.Create(4, Velocity, [new TimelineItem<double>(1, 3)]),
            ]
        );

        var positions = input.StateMapEventTimeline.AsEnumerable().Select(x => x.Position).ToArray();

        await Assert.That(positions).IsEquivalentTo(new[] { 0d, 1d, 2d });
    }

    [Test]
    public async Task Trim_SameDuration_ResultsIn_SameInstance()
    {
        var input = KeyMap(2, new TimelineItem<int>(0, 1));

        await Assert.That(input.Trim(2)).IsSameReferenceAs(input);
    }

    [Test]
    public async Task Trim_Zero_ResultsIn_Empty()
    {
        var input = KeyMap(2, new TimelineItem<int>(0, 1));

        await Assert.That(input.Trim(0)).IsSameReferenceAs(StateTimelineMap.Empty);
    }

    [Test]
    public async Task Trim_Empty_ResultsIn_Empty()
    {
        await Assert.That(StateTimelineMap.Empty.Trim(3).IsEmpty).IsTrue();
    }

    [Test]
    public async Task Trim_ShorterDuration_DropsLaterItems()
    {
        var input = KeyMap(2, new TimelineItem<int>(0, 1), new TimelineItem<int>(1, 2));

        var result = input.Trim(1);

        await Assert.That(result.Duration).IsEqualTo(1);
        await Assert.That(result.GetStateTimeline(KeyOffset).AsEnumerable())
            .IsEquivalentTo(new[] { new TimelineItem<int>(0, 1) });
    }

    [Test]
    public async Task Trim_LongerDuration_ExtendsDuration()
    {
        var input = KeyMap(1, new TimelineItem<int>(0, 1));

        var result = input.Trim(3);

        await Assert.That(result.Duration).IsEqualTo(3);
        await Assert.That(result.GetEffectiveStateMapAt(0).GetStateValue(KeyOffset)).IsEqualTo(1);
        await Assert.That(result.GetEffectiveStateMapAt(2).IsDefault).IsTrue();
    }

    [Test]
    public async Task Shift_Zero_ResultsIn_SameInstance()
    {
        var input = KeyMap(2, new TimelineItem<int>(0, 1));

        await Assert.That(input.Shift(0)).IsSameReferenceAs(input);
    }

    [Test]
    public async Task Shift_Positive_ExtendsDurationAndMovesItems()
    {
        var input = KeyMap(2, new TimelineItem<int>(0, 1));

        var result = input.Shift(1);

        await Assert.That(result.Duration).IsEqualTo(3);
        await Assert.That(result.GetEffectiveStateMapAt(0.5).IsDefault).IsTrue();
        await Assert.That(result.GetEffectiveStateMapAt(1).GetStateValue(KeyOffset)).IsEqualTo(1);
    }

    [Test]
    public async Task Shift_Negative_ShortensDuration()
    {
        var input = KeyMap(3, new TimelineItem<int>(0, 1), new TimelineItem<int>(2, 2));

        var result = input.Shift(-1);

        await Assert.That(result.Duration).IsEqualTo(2);
        await Assert.That(result.GetEffectiveStateMapAt(1).GetStateValue(KeyOffset)).IsEqualTo(2);
    }

    [Test]
    public async Task Shift_NegativeBeyondDuration_ResultsIn_ThrownException()
    {
        var input = KeyMap(2, new TimelineItem<int>(0, 1));

        await Assert.That(() => { input.Shift(-3); }).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task Merge_OnlyEmpty_ResultsIn_Empty(int count)
    {
        var input = Enumerable.Repeat(StateTimelineMap.Empty, count);

        await Assert.That(StateTimelineMap.Merge(input).IsEmpty).IsTrue();
    }

    [Test]
    public async Task Merge_UsesLongestDuration()
    {
        var short1 = KeyMap(2, new TimelineItem<int>(0, 1));
        var long1 = StateTimelineMap.Create(
            5,
            [StateTimeline.Create(5, Velocity, [new TimelineItem<double>(0, 2)])]
        );

        var result = StateTimelineMap.Merge([short1, long1]);

        await Assert.That(result.Duration).IsEqualTo(5);
        await Assert.That(result.StateTimelines.Length).IsEqualTo(2);
    }

    [Test]
    public async Task Merge_SharedKind_IsAggregated()
    {
        var a = KeyMap(2, new TimelineItem<int>(0, 1), new TimelineItem<int>(1, 2));
        var b = KeyMap(2, new TimelineItem<int>(0, 10));

        var result = StateTimelineMap.Merge([a, b]);

        await Assert.That(result.GetEffectiveStateMapAt(0).GetStateValue(KeyOffset)).IsEqualTo(11);
        await Assert.That(result.GetEffectiveStateMapAt(1).GetStateValue(KeyOffset)).IsEqualTo(12);
    }

    [Test]
    public async Task MergeStateMap_Empty_ResultsIn_Empty()
    {
        var stateMap = StateMap.FromStates([KeyOffset.CreateState(1)]);

        await Assert.That(StateTimelineMap.Empty.MergeStateMap(stateMap).IsEmpty).IsTrue();
    }

    [Test]
    public async Task MergeStateMap_DefaultStateMap_ResultsIn_SameInstance()
    {
        var input = KeyMap(2, new TimelineItem<int>(0, 1));

        await Assert.That(input.MergeStateMap(StateMap.Default)).IsSameReferenceAs(input);
    }

    [Test]
    public async Task MergeStateMap_AppliesStateMapForWholeDuration()
    {
        var input = KeyMap(2, new TimelineItem<int>(0, 1), new TimelineItem<int>(1, 2));
        var stateMap = StateMap.FromStates([KeyOffset.CreateState(10), Velocity.CreateState(0.5)]);

        var result = input.MergeStateMap(stateMap);

        await Assert.That(result.Duration).IsEqualTo(2);
        await Assert.That(result.GetEffectiveStateMapAt(0))
            .IsEqualTo(StateMap.FromStates([KeyOffset.CreateState(11), Velocity.CreateState(0.5)]));
        await Assert.That(result.GetEffectiveStateMapAt(1.5))
            .IsEqualTo(StateMap.FromStates([KeyOffset.CreateState(12), Velocity.CreateState(0.5)]));
    }
}
