using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Tests.EventStateTimelineMaps;

public sealed class EventStateTimelineMapMergeTest
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
        var input = new EventStateTimelineMap<int>[count];
        for (var i = 0; i < count; i++)
            input[i] = EventStateTimelineMap.Empty<int>();
        
        var result = EventStateTimelineMap<int>.Merge(input);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => !(x.EventTimeline.ToImmutableArray()).Any(), assert => assert.IsTrue())
            .And.Satisfies(x => !(x.StateTimelineMap.StateTimelines).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task SingleTimeline_ResultsIn_Itself()
    {
        var eventItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var eventTimeline = EventTimeline.Create(2, eventItems);
        
        var stateItems1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var stateItems2 = new TimelineItem<double>[]
        {
            new(0, 2),
            new(1, 3),
        };
        var stateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(2, StateKind2, stateItems2),
            StateTimeline.Create(2, StateKind1, stateItems1),
        };
        var stateTimelineMap = stateTimelines.ToStateTimelineMap(2);
        
        const double duration = 2;
        var timeline = EventStateTimelineMap.Create(duration, eventTimeline, stateTimelineMap);

        var input = new[]
        {
            timeline
        };
        
        var result = EventStateTimelineMap<int>.Merge(input);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.EventTimeline.ToImmutableArray(), assert => assert.IsEquivalentTo(eventItems))
            .And.Satisfies(x => x.StateTimelineMap.StateTimelines, assert => assert.IsEquivalentTo(stateTimelines.OrderBy(t => t.StateKind.Name)));
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
        var stateTimelineMap1 = stateTimelines1.ToStateTimelineMap(2);
        
        var timeline1 = EventStateTimelineMap.Create(2, eventTimeline1, stateTimelineMap1);
        
        
        var eventItems2 = new TimelineItem<int>[]
        {
            new(2, 3),
            new(3, 4),
        };
        var eventTimeline2 = EventTimeline.Create(4, eventItems2);
        
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
            StateTimeline.Create(4, StateKind2, stateItems21),
            StateTimeline.Create(4, StateKind3, stateItems22)
        };
        var stateTimelineMap2 = stateTimelines2.ToStateTimelineMap(4);
        
        var timeline2 = EventStateTimelineMap.Create(4, eventTimeline2, stateTimelineMap2);
        
        var input = new[]
        {
            timeline1,
            timeline2
        };
        
        var result = EventStateTimelineMap<int>.Merge(input);

        const double expectedDuration = 4;
        var expectedEventItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 3),
            new(3, 4),
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
            new(0, 2),
            new(1, 3),
        };
        var expectedStateItems3 = new TimelineItem<ImmutableArray<int>>[]
        {
            new(0, [0, 1, 3]),
            new(1, [2]),
            new(2, []),
        };
        var expectedStateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(4, StateKind2, expectedStateItems2),
            StateTimeline.Create(4, StateKind1, expectedStateItems1),
            StateTimeline.Create(4, StateKind3, expectedStateItems3),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.EventTimeline, assert => assert.IsEquivalentTo(expectedEventTimeline))
            .And.Satisfies(x => x.StateTimelineMap.StateTimelines, assert => assert.IsEquivalentTo(expectedStateTimelines.OrderBy(t => t.StateKind.Name)));
    }
}