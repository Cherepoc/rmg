using Rmg.Core.Events;
using TUnit.Assertions.AssertConditions.Throws;

namespace Rmg.Tests.TrackEventStateTimelineMaps;

public sealed class TrackEventStateTimelineMapShiftTest
{
    private static readonly StateKind<int> StateKind1 = StateKinds.KeyOffset;
    private static readonly StateKind<double> StateKind2 = StateKinds.QuarterNoteDurationPower;

    [Test]
    public async Task Empty_ResultsIn_Empty()
    {
        var input = TrackEventStateTimelineMap.Empty<int>();
        
        var result = input.Shift(1);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.TrackTimelineMap.AsEnumerable(), assert => assert.IsEmpty())
            .And.Satisfies(x => x.CommonStateTimelineMap.StateTimelines, assert => assert.IsEmpty());
    }

    [Test]
    public async Task OffsetToZeroDuration_ResultsIn_Empty()
    {
        const double inputDuration = 1;
        var trackEventItems = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        var trackEventTimeline = EventTimeline.Create(inputDuration, trackEventItems);
        
        var trackStateItems = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        var trackStateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(inputDuration, StateKind1, trackStateItems)
        };
        var trackStateTimelineMap = trackStateTimelines.ToStateTimelineMap(inputDuration);

        var trackEventStateTimelineMap = EventStateTimelineMap.Create(inputDuration, trackEventTimeline, trackStateTimelineMap);
        var trackTimelineMap = new Dictionary<int, EventStateTimelineMap<int>>
        {
            [1] = trackEventStateTimelineMap
        };
        
        var commonStateItems = new TimelineItem<double>[]
        {
            new(0, 2)
        };
        var commonStateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(inputDuration, StateKind2, commonStateItems)
        };
        var commonStateTimelineMap = commonStateTimelines.ToStateTimelineMap(inputDuration);
        
        var input = TrackEventStateTimelineMap.Create(inputDuration, trackTimelineMap, commonStateTimelineMap);
        
        var result = input.Shift(-1);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => x.TrackTimelineMap.AsEnumerable(), assert => assert.IsEmpty())
            .And.Satisfies(x => x.CommonStateTimelineMap.StateTimelines, assert => assert.IsEmpty());
    }
    
    [Test]
    public async Task OffsetToNegativeDuration_ResultsIn_Exception()
    {
        var input = TrackEventStateTimelineMap.Empty<int>();
        
        await Assert.That(() =>
        {
            input.Shift(-1);
        }).Throws<ArgumentOutOfRangeException>();
    }
    
    [Test]
    public async Task EmptyTrackTimelines_ResultsIn_ShiftedStateTimelines()
    {
        const double inputDuration = 2;
        
        var trackTimelineMap = new Dictionary<int, EventStateTimelineMap<int>>
        {
            [0] = EventStateTimelineMap.Empty<int>(),
            [1] = EventStateTimelineMap.Empty<int>(),
        };
        
        var commonStateItems1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var commonStateItems2 = new TimelineItem<double>[]
        {
            new(0, 2),
            new(1, 3),
        };
        var commonStateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(inputDuration, StateKind2, commonStateItems2),
            StateTimeline.Create(inputDuration, StateKind1, commonStateItems1),
        };
        var commonStateTimelineMap = commonStateTimelines.ToStateTimelineMap(inputDuration);

        var input = TrackEventStateTimelineMap.Create(inputDuration, trackTimelineMap, commonStateTimelineMap);
        
        const double offset = -1;
        var result = input.Shift(offset);
        
        const double expectedDuration = 1;
        var expectedStateTimelines = commonStateTimelines
            .Select(x => x.Shift(offset))
            .ToArray();

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.TrackTimelineMap.AsEnumerable(), assert => assert.IsEmpty())
            .And.Satisfies(x => x.CommonStateTimelineMap.StateTimelines,
                assert => assert.IsEquivalentTo(expectedStateTimelines));
    }

    [Test]
    public async Task EmptyStateTimelines_ResultsIn_ShiftedTrackTimelines()
    {
        const double inputDuration = 2;
        
        var trackEventItems1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var trackEventTimeline1 = EventTimeline.Create(inputDuration, trackEventItems1);
        
        var stateItems11 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var stateItems12 = new TimelineItem<double>[]
        {
            new(0, 2),
            new(1, 3),
        };
        var stateTimelines1 = new IStateTimeline[]
        {
            StateTimeline.Create(inputDuration, StateKind2, stateItems12),
            StateTimeline.Create(inputDuration, StateKind1, stateItems11),
        };
        var stateTimelineMap1 = stateTimelines1.ToStateTimelineMap(inputDuration);
        
        var trackEventStateTimelineMap1 = EventStateTimelineMap.Create(inputDuration, trackEventTimeline1, stateTimelineMap1);
        
        var trackEventItems2 = new TimelineItem<int>[]
        {
            new(0.5, 3),
            new(1.5, 4),
        };
        var trackEventTimeline2 = EventTimeline.Create(inputDuration, trackEventItems2);
        
        var stateItems21 = new TimelineItem<int>[]
        {
            new(0, 2),
            new(1, 3),
        };
        var stateItems22 = new TimelineItem<double>[]
        {
            new(0, 3),
            new(1, 4),
        };
        var stateTimelines2 = new IStateTimeline[]
        {
            StateTimeline.Create(inputDuration, StateKind2, stateItems22),
            StateTimeline.Create(inputDuration, StateKind1, stateItems21),
        };
        var stateTimelineMap2 = stateTimelines2.ToStateTimelineMap(inputDuration);
        
        var trackEventStateTimelineMap2 = EventStateTimelineMap.Create(inputDuration, trackEventTimeline2, stateTimelineMap2);

        var trackTimelineMap = new KeyValuePair<int, EventStateTimelineMap<int>>[]
        {
            new(1, trackEventStateTimelineMap1),
            new(2, trackEventStateTimelineMap2),
        };

        var commonStateTimelineMap = StateTimelineMap.Empty;

        var input = TrackEventStateTimelineMap.Create(inputDuration, trackTimelineMap, commonStateTimelineMap);
        
        const double offset = -1;
        var result = input.Shift(offset);
        
        const double expectedDuration = 1;
        var expectedTrackTimelineMap = trackTimelineMap
            .ToDictionary(x => x.Key, x => x.Value.Shift(offset));

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.TrackTimelineMap.AsEnumerable(),
                assert => assert.IsEquivalentTo(expectedTrackTimelineMap))
            .And.Satisfies(x => x.CommonStateTimelineMap.StateTimelines, assert => assert.IsEmpty());
    }

    [Test]
    [Arguments(-1)]
    [Arguments(-0.5)]
    [Arguments(0)]
    [Arguments(0.5)]
    [Arguments(1)]
    public async Task LessThenDurationOffset_ResultsIn_ShiftedTimelines(double offset)
    {
        const double inputDuration = 2;
        
        var trackEventItems1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var trackEventTimeline1 = EventTimeline.Create(inputDuration, trackEventItems1);
        
        var stateItems11 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var stateItems12 = new TimelineItem<double>[]
        {
            new(0, 2),
            new(1, 3),
        };
        var stateTimelines1 = new IStateTimeline[]
        {
            StateTimeline.Create(inputDuration, StateKind1, stateItems11),
            StateTimeline.Create(inputDuration, StateKind2, stateItems12),
        };
        var stateTimelineMap1 = stateTimelines1.ToStateTimelineMap(inputDuration);
        
        var trackEventStateTimelineMap1 = EventStateTimelineMap.Create(inputDuration, trackEventTimeline1, stateTimelineMap1);
        
        var trackEventItems2 = new TimelineItem<int>[]
        {
            new(0.5, 3),
            new(1.5, 4),
        };
        var trackEventTimeline2 = EventTimeline.Create(inputDuration, trackEventItems2);
        
        var stateItems21 = new TimelineItem<int>[]
        {
            new(0, 2),
            new(1, 3),
        };
        var stateItems22 = new TimelineItem<double>[]
        {
            new(0, 3),
            new(1, 4),
        };
        var stateTimelines2 = new IStateTimeline[]
        {
            StateTimeline.Create(inputDuration, StateKind1, stateItems21),
            StateTimeline.Create(inputDuration, StateKind2, stateItems22),
        };
        var stateTimelineMap2 = stateTimelines2.ToStateTimelineMap(inputDuration);
        
        var trackEventStateTimelineMap2 = EventStateTimelineMap.Create(inputDuration, trackEventTimeline2, stateTimelineMap2);

        var trackTimelineMap = new KeyValuePair<int, EventStateTimelineMap<int>>[]
        {
            new(1, trackEventStateTimelineMap1),
            new(2, trackEventStateTimelineMap2),
        };
        
        var commonStateItems = new TimelineItem<double>[]
        {
            new(0, 2),
            new(1, 3),
        };
        var commonStateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(inputDuration, StateKind2, commonStateItems)
        };
        var commonStateTimelineMap = commonStateTimelines.ToStateTimelineMap(inputDuration);

        var input = TrackEventStateTimelineMap.Create(inputDuration, trackTimelineMap, commonStateTimelineMap);
        
        var result = input.Shift(offset);
        
        var expectedDuration = inputDuration + offset;
        var expectedTrackTimelineMap = new KeyValuePair<int, EventStateTimelineMap<int>>[]
        {
            new(1, trackEventStateTimelineMap1.Shift(offset)),
            new(2, trackEventStateTimelineMap2.Shift(offset)),
        };
        var expectedCommonStateTimelines = commonStateTimelines
            .Select(x => x.Shift(offset))
            .ToArray();

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.TrackTimelineMap.AsEnumerable(),
                assert => assert.IsEquivalentTo(expectedTrackTimelineMap))
            .And.Satisfies(x => x.CommonStateTimelineMap.StateTimelines,
                assert => assert.IsEquivalentTo(expectedCommonStateTimelines));
    }
}