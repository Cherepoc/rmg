using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Tests.TrackEventStateTimelineMaps;

public sealed class TrackEventStateTimelineMapMergeTest
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
        var input = new TrackEventStateTimelineMap<int>[count];
        for (var i = 0; i < count; i++)
            input[i] = TrackEventStateTimelineMap.Empty<int>();
        
        var result = TrackEventStateTimelineMap.Merge(input);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => !(x.TrackTimelineMap.AsEnumerable()).Any(), assert => assert.IsTrue())
            .And.Satisfies(x => !(x.CommonStateTimelineMap.StateTimelines).Any(), assert => assert.IsTrue());
    }

    [Test]
    public async Task SingleTimeline_ResultsIn_Itself()
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

        var trackEventStateTimelineMap =
            EventStateTimelineMap.Create(inputDuration, trackEventTimeline, trackStateTimelineMap);
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
        
        var timelineMap = TrackEventStateTimelineMap.Create(inputDuration, trackTimelineMap, commonStateTimelineMap);

        var input = new[]
        {
            timelineMap
        };
        
        var result = TrackEventStateTimelineMap.Merge(input);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(inputDuration))
            .And.Satisfies(x => x.TrackTimelineMap.AsEnumerable(),
                assert => assert.IsEquivalentTo(trackTimelineMap))
            .And.Satisfies(x => x.CommonStateTimelineMap.StateTimelines,
                assert => assert.IsEquivalentTo(commonStateTimelines.OrderBy(t => t.StateKind.Name)));
    }

    [Test]
    public async Task MultipleTimelines_ResultsIn_SingleTimeline()
    {
        const double inputDuration1 = 2;
        const double inputDuration2 = 4;
        
        // timeline map 1 track 1
        var trackEventItems11 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var trackEventTimeline11 = EventTimeline.Create(inputDuration1, trackEventItems11);
        
        var stateItems111 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var stateItems112 = new TimelineItem<double>[]
        {
            new(0, 2),
            new(1, 3),
        };
        var stateTimelines11 = new IStateTimeline[]
        {
            StateTimeline.Create(inputDuration1, StateKind1, stateItems111),
            StateTimeline.Create(inputDuration1, StateKind2, stateItems112),
        };
        var stateTimelineMap11 = stateTimelines11.ToStateTimelineMap(inputDuration1);

        var trackEventStateTimelineMap11 =
            EventStateTimelineMap.Create(inputDuration1, trackEventTimeline11, stateTimelineMap11);
        
        // timeline map 1 track 3
        var trackEventItems12 = new TimelineItem<int>[]
        {
            new(0.5, 3),
            new(1.5, 4),
        };
        var trackEventTimeline12 = EventTimeline.Create(inputDuration1, trackEventItems12);
        
        var stateItems121 = new TimelineItem<int>[]
        {
            new(0, 2),
            new(1, 3),
        };
        var stateItems122 = new TimelineItem<double>[]
        {
            new(0, 3),
            new(1, 4),
        };
        var stateTimelines12 = new IStateTimeline[]
        {
            StateTimeline.Create(inputDuration1, StateKind1, stateItems121),
            StateTimeline.Create(inputDuration1, StateKind2, stateItems122),
        };
        var stateTimelineMap2 = stateTimelines12.ToStateTimelineMap(inputDuration1);
        
        var trackEventStateTimelineMap12 = EventStateTimelineMap.Create(inputDuration1, trackEventTimeline12, stateTimelineMap2);

        var trackTimelineMap1 = new KeyValuePair<int, EventStateTimelineMap<int>>[]
        {
            new(1, trackEventStateTimelineMap11),
            new(3, trackEventStateTimelineMap12),
        };
        
        // timeline map 1 common state
        var commonStateItems11 = new TimelineItem<int>[]
        {
            new(0, 2),
            new(1, 3),
        };
        var commonStateItems12 = new TimelineItem<ImmutableArray<int>>[]
        {
            new(0, [1]),
            new(1, [2, 3]),
        };
        var commonStateTimelines1 = new IStateTimeline[]
        {
            StateTimeline.Create(inputDuration1, StateKind1, commonStateItems11),
            StateTimeline.Create(inputDuration1, StateKind3, commonStateItems12),
        };
        var commonStateTimelineMap1 = commonStateTimelines1.ToStateTimelineMap(inputDuration1);

        var timelineMap1 = TrackEventStateTimelineMap.Create(inputDuration1, trackTimelineMap1, commonStateTimelineMap1);
        
        // timeline map 2 track 2
        var trackEventItems21 = new TimelineItem<int>[]
        {
            new(2, 3),
            new(3, 4),
        };
        var trackEventTimeline21 = EventTimeline.Create(inputDuration2, trackEventItems21);
        
        var stateItems211 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var stateItems212 = new TimelineItem<double>[]
        {
            new(0, 2),
            new(1, 3),
        };
        var stateTimelines21 = new IStateTimeline[]
        {
            StateTimeline.Create(inputDuration2, StateKind1, stateItems211),
            StateTimeline.Create(inputDuration2, StateKind2, stateItems212),
        };
        var stateTimelineMap21 = stateTimelines21.ToStateTimelineMap(inputDuration2);
        
        var trackEventStateTimelineMap21 = EventStateTimelineMap.Create(inputDuration2, trackEventTimeline21, stateTimelineMap21);
        
        // timeline map 2 track 3
        var trackEventItems22 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var trackEventTimeline22 = EventTimeline.Create(inputDuration2, trackEventItems22);
        
        var stateItems221 = new TimelineItem<int>[]
        {
            new(0, 3),
            new(2, 4),
        };
        var stateItems222 = new TimelineItem<double>[]
        {
            new(0, 3),
            new(3, 4),
        };
        var stateTimelines22 = new IStateTimeline[]
        {
            StateTimeline.Create(inputDuration2, StateKind1, stateItems221),
            StateTimeline.Create(inputDuration2, StateKind2, stateItems222),
        };
        var stateTimelineMap22 = stateTimelines22.ToStateTimelineMap(inputDuration2);
        
        var trackEventStateTimelineMap22 = EventStateTimelineMap.Create(inputDuration2, trackEventTimeline22, stateTimelineMap22);

        var trackTimelineMap2 = new KeyValuePair<int, EventStateTimelineMap<int>>[]
        {
            new(2, trackEventStateTimelineMap21),
            new(3, trackEventStateTimelineMap22),
        };
        
        // timeline map 2 common state
        var commonStateItems21 = new TimelineItem<double>[]
        {
            new(1, 2),
            new(3, 0),
        };
        var commonStateItems22 = new TimelineItem<ImmutableArray<int>>[]
        {
            new(0, [2, 3]),
            new(3, []),
        };
        var commonStateTimelines2 = new IStateTimeline[]
        {
            StateTimeline.Create(inputDuration2, StateKind2, commonStateItems21),
            StateTimeline.Create(inputDuration2, StateKind3, commonStateItems22),
        };
        var commonStateTimelineMap2 = commonStateTimelines2.ToStateTimelineMap(inputDuration2);

        var timelineMap2 = TrackEventStateTimelineMap.Create(inputDuration2, trackTimelineMap2, commonStateTimelineMap2);
        
        var input = new[]
        {
            timelineMap1,
            timelineMap2
        };
        
        var result = TrackEventStateTimelineMap.Merge(input);

        // expected timeline
        var expectedDuration = inputDuration2;
        
        // expected timeline map track 1
        var expectedTrackEventItems1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var expectedTrackEventTimeline1 = EventTimeline.Create(expectedDuration, expectedTrackEventItems1);
        
        var expectedStateItems11 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
            new(2, 0),
        };
        var expectedStateItems12 = new TimelineItem<double>[]
        {
            new(0, 2),
            new(1, 3),
            new(2, 0),
        };
        var expectedStateTimelines1 = new IStateTimeline[]
        {
            StateTimeline.Create(expectedDuration, StateKind1, expectedStateItems11),
            StateTimeline.Create(expectedDuration, StateKind2, expectedStateItems12),
        };
        var expectedStateTimelineMap1 = expectedStateTimelines1.ToStateTimelineMap(expectedDuration);
        
        var trackEventStateTimelineMap1 =
            EventStateTimelineMap.Create(expectedDuration, expectedTrackEventTimeline1, expectedStateTimelineMap1);
        
        // expected timeline map track 2
        var expectedTrackEventItems2 = new TimelineItem<int>[]
        {
            new(2, 3),
            new(3, 4),
        };
        var expectedTrackEventTimeline2 = EventTimeline.Create(expectedDuration, expectedTrackEventItems2);
        
        var expectedStateItems21 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var expectedStateItems22 = new TimelineItem<double>[]
        {
            new(0, 2),
            new(1, 3),
        };
        var expectedStateTimelines2 = new IStateTimeline[]
        {
            StateTimeline.Create(expectedDuration, StateKind2, expectedStateItems22),
            StateTimeline.Create(expectedDuration, StateKind1, expectedStateItems21),
        };
        var expectedStateTimelineMap2 = expectedStateTimelines2.ToStateTimelineMap(expectedDuration);
        
        var trackEventStateTimelineMap2 =
            EventStateTimelineMap.Create(expectedDuration, expectedTrackEventTimeline2, expectedStateTimelineMap2);
        
        // expected timeline map track 3
        var expectedTrackEventItems3 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(0.5, 3),
            new(1, 2),
            new(1.5, 4),
        };
        var expectedTrackEventTimeline3 = EventTimeline.Create(expectedDuration, expectedTrackEventItems3);
        
        var expectedStateItems31 = new TimelineItem<int>[]
        {
            new(0, 5),
            new(1, 6),
            new(2, 4),
        };
        var expectedStateItems32 = new TimelineItem<double>[]
        {
            new(0, 6),
            new(1, 7),
            new(2, 3),
            new(3, 4),
        };
        var expectedStateTimelines3 = new IStateTimeline[]
        {
            StateTimeline.Create(expectedDuration, StateKind2, expectedStateItems32),
            StateTimeline.Create(expectedDuration, StateKind1, expectedStateItems31),
        };
        var expectedStateTimelineMap3 = expectedStateTimelines3.ToStateTimelineMap(expectedDuration);
        
        var trackEventStateTimelineMap3 =
            EventStateTimelineMap.Create(expectedDuration, expectedTrackEventTimeline3, expectedStateTimelineMap3);

        var expectedTrackTimelineMap = new KeyValuePair<int, EventStateTimelineMap<int>>[]
        {
            new(1, trackEventStateTimelineMap1),
            new(2, trackEventStateTimelineMap2),
            new(3, trackEventStateTimelineMap3),
        };
        
        // expected timeline map common state
        var expectedCommonStateItems1 = new TimelineItem<int>[]
        {
            new(0, 2),
            new(1, 3),
            new(2, 0),
        };
        var expectedCommonStateItems2 = new TimelineItem<double>[]
        {
            new(1, 2),
            new(3, 0),
        };
        var expectedCommonStateItems3 = new TimelineItem<ImmutableArray<int>>[]
        {
            new(0, [1, 2, 3]),
            new(1, [2, 3]),
            new(3, []),
        };
        var expectedCommonStateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(expectedDuration, StateKind2, expectedCommonStateItems2),
            StateTimeline.Create(expectedDuration, StateKind1, expectedCommonStateItems1),
            StateTimeline.Create(expectedDuration, StateKind3, expectedCommonStateItems3),
        };
        
        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsEmpty, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(expectedDuration))
            .And.Satisfies(x => x.TrackTimelineMap.AsEnumerable(),
                assert => assert.IsEquivalentTo(expectedTrackTimelineMap))
            .And.Satisfies(x => x.CommonStateTimelineMap.StateTimelines,
                assert => assert.IsEquivalentTo(expectedCommonStateTimelines.OrderBy(t => t.StateKind.Name)));
    }
}