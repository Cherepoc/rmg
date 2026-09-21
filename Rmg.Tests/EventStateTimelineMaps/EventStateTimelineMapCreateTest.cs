using Rmg.Core.Events;

namespace Rmg.Tests.EventStateTimelineMaps;

public sealed class EventStateTimelineMapCreateTest
{
    private static readonly StateKind<int> StateKind1 = StateKinds.KeyOffset;
    private static readonly StateKind<double> StateKind2 = StateKinds.QuarterNoteDurationPower;
    
    [Test]
    public async Task ZeroDuration_ResultsIn_Empty()
    {
        var eventItems = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        var eventTimeline = EventTimeline.Create(1, eventItems);
        
        var stateItems1 = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        var stateItems2 = new TimelineItem<double>[]
        {
            new(0, 2)
        };
        var stateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(1, StateKind1, stateItems1),
            StateTimeline.Create(1, StateKind2, stateItems2)
        };
        var stateTimelineMap = stateTimelines.ToStateTimelineMap(1);
        
        const double duration = 0;
        var result = EventStateTimelineMap.Create(duration, eventTimeline, stateTimelineMap);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => !(x.EventTimeline.ToImmutableArray()).Any(), assert => assert.IsTrue())
            .And.Satisfies(x => !(x.StateTimelineMap.StateTimelines).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task EmptyTimelines_ResultsIn_Empty()
    {
        var eventTimeline = EventTimeline.Empty<int>();
        var stateTimelineMap = StateTimelineMap.Empty;
        
        const double duration = 1;
        var result = EventStateTimelineMap.Create(duration, eventTimeline, stateTimelineMap);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => !(x.EventTimeline.ToImmutableArray()).Any(), assert => assert.IsTrue())
            .And.Satisfies(x => !(x.StateTimelineMap.StateTimelines).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task BeforeFirstItemsDuration_ResultsIn_Empty()
    {
        var eventItems = new TimelineItem<int>[]
        {
            new(1, 1)
        };
        var eventTimeline = EventTimeline.Create(2, eventItems);
        
        var stateItems1 = new TimelineItem<int>[]
        {
            new(1, 1)
        };
        var stateItems2 = new TimelineItem<double>[]
        {
            new(1, 2)
        };
        var stateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(2, StateKind1, stateItems1),
            StateTimeline.Create(2, StateKind2, stateItems2)
        };
        var stateTimelineMap = stateTimelines.ToStateTimelineMap(2);
        
        const double duration = 1;
        var result = EventStateTimelineMap.Create(duration, eventTimeline, stateTimelineMap);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => !(x.EventTimeline.ToImmutableArray()).Any(), assert => assert.IsTrue())
            .And.Satisfies(x => !(x.StateTimelineMap.StateTimelines).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task NegativeDuration_ThrowsException()
    {
        var eventTimeline = EventTimeline.Empty<int>();
        var stateTimelineMap = StateTimelineMap.Empty;
        
        const double duration = -1;

        await Assert.That(() =>
        {
            EventStateTimelineMap.Create(duration, eventTimeline, stateTimelineMap);
        }).Throws<ArgumentOutOfRangeException>();
    }
    
    [Test]
    public async Task EmptyEventTimeline_ResultsIn_StateTimelinesOnly()
    {
        var eventTimeline = EventTimeline.Empty<int>();
        
        var stateItems1 = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        var stateItems2 = new TimelineItem<double>[]
        {
            new(0, 2)
        };
        var stateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(1, StateKind2, stateItems2),
            StateTimeline.Create(1, StateKind1, stateItems1),
        };
        var stateTimelineMap = stateTimelines.ToStateTimelineMap(1);
        
        const double duration = 1;
        var result = EventStateTimelineMap.Create(duration, eventTimeline, stateTimelineMap);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => !(x.EventTimeline.ToImmutableArray()).Any(), assert => assert.IsTrue())
            .And.Satisfies(x => x.StateTimelineMap.StateTimelines, assert => assert.IsEquivalentTo(stateTimelines.OrderBy(t => t.StateKind.Name)));
    }
    
    [Test]
    public async Task EmptyStateTimelines_ResultsIn_EventTimelineOnly()
    {
        var eventItems = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        var eventTimeline = EventTimeline.Create(1, eventItems);

        var stateTimelineMap = StateTimelineMap.Empty;
        
        const double duration = 1;
        var result = EventStateTimelineMap.Create(duration, eventTimeline, stateTimelineMap);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.EventTimeline.ToImmutableArray(), assert => assert.IsEquivalentTo(eventItems))
            .And.Satisfies(x => !(x.StateTimelineMap.StateTimelines).Any(), assert => assert.IsTrue());
    }

    [Test]
    public async Task LesserDuration_ResultsIn_TrimmedTimelines()
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
        
        const double duration = 1;
        var result = EventStateTimelineMap.Create(duration, eventTimeline, stateTimelineMap);
        
        var expectedEventTimeline = eventTimeline.Trim(duration);
        var expectedStateTimelines = stateTimelines
            .Select(x => x.Trim(duration))
            .ToArray();

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.EventTimeline, assert => assert.IsEquivalentTo(expectedEventTimeline))
            .And.Satisfies(x => x.StateTimelineMap.StateTimelines, assert => assert.IsEquivalentTo(expectedStateTimelines.OrderBy(t => t.StateKind.Name)));
    }

    [Test]
    public async Task SameKindStateTimelines_ResultsIn_MergedStateTimelines()
    {
        var eventTimeline = EventTimeline.Empty<int>();
        
        var stateItems1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var stateItems2 = new TimelineItem<int>[]
        {
            new(0, 2),
            new(1, 3),
        };
        var stateItems3 = new TimelineItem<double>[]
        {
            new(0, 2),
            new(1, 3),
        };
        var stateItems4 = new TimelineItem<double>[]
        {
            new(0, 3),
            new(1, 4),
        };
        var stateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(2, StateKind1, stateItems1),
            StateTimeline.Create(2, StateKind1, stateItems2),
            StateTimeline.Create(2, StateKind2, stateItems3),
            StateTimeline.Create(2, StateKind2, stateItems4),
        };
        var stateTimelineMap = stateTimelines.ToStateTimelineMap(2);
        
        const double duration = 2;
        var result = EventStateTimelineMap.Create(duration, eventTimeline, stateTimelineMap);
        
        var expectedStateItems1 = new TimelineItem<int>[]
        {
            new(0, 3),
            new(1, 5),
        };
        var expectedStateItems2 = new TimelineItem<double>[]
        {
            new(0, 5),
            new(1, 7),
        };
        var expectedStateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(2, StateKind2, expectedStateItems2),
            StateTimeline.Create(2, StateKind1, expectedStateItems1),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.EventTimeline, assert => assert.IsEquivalentTo(eventTimeline))
            .And.Satisfies(x => x.StateTimelineMap.StateTimelines, assert => assert.IsEquivalentTo(expectedStateTimelines.OrderBy(t => t.StateKind.Name)));
    }
}