using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Tests.EventStateTimelineMaps;

public sealed class EventStateTimelineMapUnwrapTest
{
    private static readonly StateKind<int> StateKind1 = StateKinds.KeyOffset;
    private static readonly StateKind<double> StateKind2 = StateKinds.QuarterNoteDurationPower;
    private static readonly StateKind<ImmutableArray<int>> StateKind3 = StateKinds.ScaleOffsets;
    
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task Empty_ResultsIn_Empty(int count)
    {
        var items = new TimelineItem<EventStateTimelineMap<int>>[count];
        for (var i = 0; i < count; i++)
            items[i] = new TimelineItem<EventStateTimelineMap<int>>(i, EventStateTimelineMap.Empty<int>());
        
        var input = EventTimeline.Create(count, items);
        
        var result = input.Unwrap();

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.EventTimeline.ToImmutableArray(), assert => assert.IsEmpty())
            .And.Satisfies(x => x.StateTimelineMap.StateTimelines, assert => assert.IsEmpty());
    }
    
    [Test]
    public async Task MultipleTimelines_ResultsIn_SingleTimeline()
    {
        var eventItems1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var eventTimeline1 = EventTimeline.Create(2, eventItems1);
        
        var stateItems11 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var stateItems12 = new TimelineItem<ImmutableArray<int>>[]
        {
            new(0, [0, 1]),
            new(1, [2]),
        };
        var stateTimelines1 = new IStateTimeline[]
        {
            StateTimeline.Create(2, StateKind1, stateItems11),
            StateTimeline.Create(2, StateKind3, stateItems12)
        };
        var stateTimelineMap1 = StateTimelineMap.Create(2, stateTimelines1);
        
        var timeline1 = EventStateTimelineMap.Create(2, eventTimeline1, stateTimelineMap1);
        
        
        var eventItems2 = new TimelineItem<int>[]
        {
            new(0, 3),
            new(1, 4),
        };
        var eventTimeline2 = EventTimeline.Create(2, eventItems2);
        
        var stateItems21 = new TimelineItem<double>[]
        {
            new(0, 2),
            new(1, 3),
        };
        var stateItems22 = new TimelineItem<ImmutableArray<int>>[]
        {
            new(0, [3, 1]),
            new(1, []),
        };
        var stateTimelines2 = new IStateTimeline[]
        {
            StateTimeline.Create(2, StateKind2, stateItems21),
            StateTimeline.Create(2, StateKind3, stateItems22)
        };
        var stateTimelineMap2 = StateTimelineMap.Create(2, stateTimelines2);
        
        var timeline2 = EventStateTimelineMap.Create(2, eventTimeline2, stateTimelineMap2);
        
        var inputItems = new[]
        {
            timeline1.ToTimelineItem(0),
            timeline2.ToTimelineItem(0.5)
        };
        var input = EventTimeline.Create(2, inputItems);

        var result = input.Unwrap();

        const double expectedDuration = 2.5;
        var expectedEventItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(0.5, 3),
            new(1, 2),
            new(1.5, 4),
        };
        var expectedEventTimeline = EventTimeline.Create(expectedDuration, expectedEventItems);
        
        var expectedStateItems1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 0),
        };
        var expectedStateItems2 = new TimelineItem<double>[]
        {
            new(0.5, 2),
            new(1.5, 3),
        };
        var expectedStateItems3 = new TimelineItem<ImmutableArray<int>>[]
        {
            new(0, [0, 1]),
            new(0.5, [0, 1, 3]),
            new(1, [1, 2, 3]),
            new(1.5, [2]),
            new(2, []),
        };
        
        var expectedStateTimeline1 = StateTimeline.Create(4, StateKind1, expectedStateItems1);
        var expectedStateTimeline2 = StateTimeline.Create(4, StateKind2, expectedStateItems2);
        var expectedStateTimeline3 = StateTimeline.Create(4, StateKind3, expectedStateItems3);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.EventTimeline.ToImmutableArray(),
                assert => assert.IsEquivalentTo(expectedEventTimeline))
            .And.Satisfies(x => x.StateTimelineMap.GetStateTimeline(StateKind1).AsEnumerable(),
                assert => assert.IsEquivalentTo(expectedStateTimeline1))
            .And.Satisfies(x => x.StateTimelineMap.GetStateTimeline(StateKind2).AsEnumerable(),
                assert => assert.IsEquivalentTo(expectedStateTimeline2))
            .And.Satisfies(x => x.StateTimelineMap.GetStateTimeline(StateKind3).AsEnumerable(),
                assert => assert.IsEquivalentTo(expectedStateTimeline3));
    }
}