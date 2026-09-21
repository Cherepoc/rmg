using Rmg.Core.Events;

namespace Rmg.Tests.EventStateTimelineMaps;

public sealed class EventStateTimelineMapTrimTest
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
        
        var input = EventStateTimelineMap.Create(1, eventTimeline, stateTimelineMap);

        var result = input.Trim(0);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => !(x.EventTimeline.ToImmutableArray()).Any(), assert => assert.IsTrue())
            .And.Satisfies(x => !(x.StateTimelineMap.StateTimelines).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task Empty_ResultsIn_Empty()
    {
        var input = EventStateTimelineMap.Empty<int>();

        var result = input.Trim(1);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => !(x.EventTimeline.ToImmutableArray()).Any(), assert => assert.IsTrue())
            .And.Satisfies(x => !(x.StateTimelineMap.StateTimelines).Any(), assert => assert.IsTrue());
    }

    [Test]
    public async Task NegativeDuration_ResultsIn_ThrownException()
    {
        var input = EventStateTimelineMap.Empty<int>();

        await Assert.That(() =>
        {
            input.Trim(-1);
        }).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments(0.5)]
    [Arguments(1)]
    [Arguments(1.5)]
    [Arguments(2)]
    public async Task PositiveDuration_ResultsIn_TrimmedTimelines(double duration)
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
            new(1, 1),
        };
        var stateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(2, StateKind2, stateItems2),
            StateTimeline.Create(2, StateKind1, stateItems1),
        };
        var stateTimelineMap = stateTimelines.ToStateTimelineMap(2);
        
        var input = EventStateTimelineMap.Create(2, eventTimeline, stateTimelineMap);
        
        var result = input.Trim(duration);

        var expectedEventTimeline = eventTimeline.Trim(duration);
        var expectedStateTimelines = stateTimelines
            .Select(x => x.Trim(duration))
            .ToArray();

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.EventTimeline.ToImmutableArray(),
                assert => assert.IsEquivalentTo(expectedEventTimeline))
            .And.Satisfies(x => x.StateTimelineMap.StateTimelines,
                assert => assert.IsEquivalentTo(expectedStateTimelines.OrderBy(t => t.StateKind.Name)));
    }
}