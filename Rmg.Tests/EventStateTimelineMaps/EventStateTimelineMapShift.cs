using Rmg.Core.Events;

namespace Rmg.Tests.EventStateTimelineMaps;

public sealed class EventStateTimelineMapShift
{
    private static readonly StateKind<int> StateKind1 = StateKinds.KeyOffset;
    private static readonly StateKind<double> StateKind2 = StateKinds.QuarterNoteDurationPower;
    
    [Test]
    public async Task ZeroDurationTimeline_ResultsIn_Default()
    {
        var input = EventStateTimelineMap.Create<int>(0);
        
        var result = input.Shift(1);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsDefault, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(1))
            .And.Satisfies(x => !(x.EventTimeline.ToImmutableArray()).Any(), assert => assert.IsTrue())
            .And.Satisfies(x => !(x.StateTimelineMap.StateTimelines).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task OffsetToZeroDuration_ResultsIn_ZeroDuration()
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
        
        var result = input.Shift(-1);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsDefault, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => !(x.EventTimeline.ToImmutableArray()).Any(), assert => assert.IsTrue())
            .And.Satisfies(x => !(x.StateTimelineMap.StateTimelines).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task OffsetToNegativeDuration_ResultsIn_Exception()
    {
        var input = EventStateTimelineMap.Create<int>(0);
        
        await Assert.That(() =>
        {
            input.Shift(-1);
        }).Throws<ArgumentOutOfRangeException>();
    }
    
    [Test]
    public async Task NoEvents_ResultsIn_ShiftedStateTimelines()
    {
        var eventTimeline = EventTimeline.Create<int>(0);
        
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
            StateTimeline.Create(2, StateKind2, stateItems2),
            StateTimeline.Create(2, StateKind1, stateItems1),
        };
        var stateTimelineMap = stateTimelines.ToStateTimelineMap(2);
        
        var input = EventStateTimelineMap.Create(2, eventTimeline, stateTimelineMap);
        
        const double offset = -1;
        var result = input.Shift(offset);
        
        const double expectedDuration = 1;
        var expectedStateTimelines = stateTimelines
            .Select(x => x.Shift(offset))
            .ToArray();

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsDefault, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => !(x.EventTimeline.ToImmutableArray()).Any(), assert => assert.IsTrue())
            .And.Satisfies(x => x.StateTimelineMap.StateTimelines,
                assert => assert.IsEquivalentTo(expectedStateTimelines.OrderBy(t => t.StateKind.Name)));
    }
    
    [Test]
    public async Task DefaultState_ResultsIn_ShiftedEventTimeline()
    {
        var eventItems = new TimelineItem<int>[]
        {
            new(1, 1)
        };
        var eventTimeline = EventTimeline.Create(2, eventItems);

        var stateTimelineMap = StateTimelineMap.Create(0);
        
        var input = EventStateTimelineMap.Create(2, eventTimeline, stateTimelineMap);
        
        const double offset = -1;
        var result = input.Shift(offset);
        
        const double expectedDuration = 1;
        var expectedEventTimeline = eventTimeline.Shift(offset);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsDefault, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.EventTimeline, assert => assert.IsEquivalentTo(expectedEventTimeline))
            .And.Satisfies(x => !(x.StateTimelineMap.StateTimelines).Any(), assert => assert.IsTrue());
    }

    [Test]
    [Arguments(-1)]
    [Arguments(-0.5)]
    [Arguments(0)]
    [Arguments(0.5)]
    [Arguments(1)]
    public async Task LessThenDurationOffset_ResultsIn_ShiftedTimelines(double offset)
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
        
        var input = EventStateTimelineMap.Create(2, eventTimeline, stateTimelineMap);
        
        var result = input.Shift(offset);

        var expectedDuration = 2 + offset;
        var expectedEventTimeline = eventTimeline.Shift(offset);
        var expectedStateTimelines = stateTimelines
            .Select(x => x.Shift(offset))
            .ToArray();

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsDefault, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.EventTimeline.ToImmutableArray(),
                assert => assert.IsEquivalentTo(expectedEventTimeline))
            .And.Satisfies(x => x.StateTimelineMap.StateTimelines,
                assert => assert.IsEquivalentTo(expectedStateTimelines.OrderBy(t => t.StateKind.Name)));
    }
}