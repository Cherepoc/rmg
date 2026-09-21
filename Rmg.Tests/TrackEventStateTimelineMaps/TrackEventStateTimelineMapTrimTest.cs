using Rmg.Core.Events;

namespace Rmg.Tests.TrackEventStateTimelineMaps;

public sealed class TrackEventStateTimelineMapTrimTest
{
    private static readonly StateKind<int> StateKind1 = StateKinds.KeyOffset;
    private static readonly StateKind<double> StateKind2 = StateKinds.QuarterNoteDurationPower;

    [Test]
    public async Task ZeroDuration_ResultsIn_Empty()
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

        var result = input.Trim(0);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => !(x.TrackTimelineMap.AsEnumerable()).Any(), assert => assert.IsTrue())
            .And.Satisfies(x => !(x.CommonStateTimelineMap.StateTimelines).Any(), assert => assert.IsTrue());
    }

    [Test]
    public async Task Empty_ResultsIn_Empty()
    {
        var input = TrackEventStateTimelineMap.Empty<int>();

        var result = input.Trim(1);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => !(x.TrackTimelineMap.AsEnumerable()).Any(), assert => assert.IsTrue())
            .And.Satisfies(x => !(x.CommonStateTimelineMap.StateTimelines).Any(), assert => assert.IsTrue());
    }

    [Test]
    public async Task NegativeDuration_ResultsIn_ThrownException()
    {
        var input = TrackEventStateTimelineMap.Empty<int>();

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
        
        var result = input.Trim(duration);
        
        var expectedTrackTimelineMap = new KeyValuePair<int, EventStateTimelineMap<int>>[]
        {
            new(1, trackEventStateTimelineMap1.Trim(duration)),
            new(2, trackEventStateTimelineMap2.Trim(duration)),
        };
        var expectedCommonStateTimelines = commonStateTimelines
            .Select(x => x.Trim(duration))
            .ToArray();

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.TrackTimelineMap.AsEnumerable(),
                assert => assert.IsEquivalentTo(expectedTrackTimelineMap))
            .And.Satisfies(x => x.CommonStateTimelineMap.StateTimelines,
                assert => assert.IsEquivalentTo(expectedCommonStateTimelines.OrderBy(t => t.StateKind.Name)));
    }
}