using Rmg.Core.Events;

namespace Rmg.Tests.TrackEventStateTimelineMaps;

public sealed class TrackEventStateTimelineMapCreateTest
{
    private static readonly StateKind<int> StateKind1 = StateKinds.KeyOffset;
    private static readonly StateKind<double> StateKind2 = StateKinds.QuarterNoteDurationPower;
    
    [Test]
    public async Task ZeroDuration_ResultsIn_ZeroDuration()
    {
        var trackEventItems = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        var trackEventTimeline = EventTimeline.Create(1, trackEventItems);
        
        var trackStateItems = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        var trackStateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(1, StateKind1, trackStateItems)
        };
        var trackStateTimelineMap = trackStateTimelines.ToStateTimelineMap(1);

        var trackEventStateTimelineMap = EventStateTimelineMap.Create(1, trackEventTimeline, trackStateTimelineMap);
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
            StateTimeline.Create(1, StateKind2, commonStateItems)
        };
        var commonStateTimelineMap = commonStateTimelines.ToStateTimelineMap(1);
        
        const double duration = 0;
        var result = TrackEventStateTimelineMap.Create(duration, trackTimelineMap, commonStateTimelineMap);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsDefault, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsZero())
            .And.Satisfies(x => !(x.TrackTimelineMap.AsEnumerable()).Any(), assert => assert.IsTrue())
            .And.Satisfies(x => !(x.CommonStateTimelineMap.StateTimelines).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task ZeroDurationTimelines_ResultsIn_Default(int trackEventStateTimelineMapCount)
    {
        var trackEventStateTimelineMaps = new Dictionary<int, EventStateTimelineMap<int>>();
        for (int i = 0; i < trackEventStateTimelineMapCount; i++)
            trackEventStateTimelineMaps[i] = EventStateTimelineMap.Create<int>(0);

        var commonStateTimelineMap = StateTimelineMap.Create(0);
        
        const double duration = 1;
        var result = TrackEventStateTimelineMap.Create(duration, trackEventStateTimelineMaps, commonStateTimelineMap);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsDefault, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => !(x.TrackTimelineMap.AsEnumerable()).Any(), assert => assert.IsTrue())
            .And.Satisfies(x => !(x.CommonStateTimelineMap.StateTimelines).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task BeforeFirstItemsDuration_ResultsIn_Default()
    {
        var trackEventItems = new TimelineItem<int>[]
        {
            new(1, 1)
        };
        var trackEventTimeline = EventTimeline.Create(2, trackEventItems);
        
        var trackStateItems = new TimelineItem<int>[]
        {
            new(1, 1)
        };
        var trackStateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(2, StateKind1, trackStateItems)
        };
        var trackStateTimelineMap = trackStateTimelines.ToStateTimelineMap(2);

        var trackEventStateTimelineMap = EventStateTimelineMap.Create(2, trackEventTimeline, trackStateTimelineMap);
        var trackTimelineMap = new Dictionary<int, EventStateTimelineMap<int>>
        {
            [1] = trackEventStateTimelineMap
        };
        
        var commonStateItems = new TimelineItem<double>[]
        {
            new(1, 2)
        };
        var commonStateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(2, StateKind2, commonStateItems)
        };
        var commonStateTimelineMap = commonStateTimelines.ToStateTimelineMap(2);
        
        const double duration = 1;
        var result = TrackEventStateTimelineMap.Create(duration, trackTimelineMap, commonStateTimelineMap);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsDefault, assert => assert.IsTrue())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => !(x.TrackTimelineMap.AsEnumerable()).Any(), assert => assert.IsTrue())
            .And.Satisfies(x => !(x.CommonStateTimelineMap.StateTimelines).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task NegativeDuration_ThrowsException()
    {
        // ReSharper disable once CollectionNeverUpdated.Local
        var eventTimelineMap = new Dictionary<int, EventStateTimelineMap<int>>();
        var commonStateTimelineMap = StateTimelineMap.Create(0);
        
        const double duration = -1;

        await Assert.That(() =>
        {
            TrackEventStateTimelineMap.Create(duration, eventTimelineMap, commonStateTimelineMap);
        }).Throws<ArgumentOutOfRangeException>();
    }
    
    [Test]
    public async Task NoTracks_ResultsIn_CommonStateTimelinesOnly()
    {
        // ReSharper disable once CollectionNeverUpdated.Local
        var trackTimelineMap = new Dictionary<int, EventStateTimelineMap<int>>();
        
        var commonStateItems = new TimelineItem<double>[]
        {
            new(0, 2)
        };
        var commonStateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(1, StateKind2, commonStateItems)
        };
        var commonStateTimelineMap = commonStateTimelines.ToStateTimelineMap(1);
        
        const double duration = 1;
        var result = TrackEventStateTimelineMap.Create(duration, trackTimelineMap, commonStateTimelineMap);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsDefault, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => !(x.TrackTimelineMap.AsEnumerable()).Any(), assert => assert.IsTrue())
            .And.Satisfies(x => x.CommonStateTimelineMap.StateTimelines,
                assert => assert.IsEquivalentTo(commonStateTimelines.OrderBy(t => t.StateKind.Name)));
    }
    
    [Test]
    public async Task DefaultCommonState_ResultsIn_TrackTimelineMapOnly()
    {
        var trackEventItems = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        var trackEventTimeline = EventTimeline.Create(1, trackEventItems);
        
        var trackStateItems = new TimelineItem<int>[]
        {
            new(0, 1)
        };
        var trackStateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(1, StateKind1, trackStateItems)
        };
        var trackStateTimelineMap = trackStateTimelines.ToStateTimelineMap(1);

        var trackEventStateTimelineMap = EventStateTimelineMap.Create(1, trackEventTimeline, trackStateTimelineMap);
        var trackTimelineMap = new Dictionary<int, EventStateTimelineMap<int>>
        {
            [1] = trackEventStateTimelineMap
        };
        
        var commonStateTimelineMap = StateTimelineMap.Create(0);
        
        const double duration = 1;
        var result = TrackEventStateTimelineMap.Create(duration, trackTimelineMap, commonStateTimelineMap);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsDefault, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.TrackTimelineMap.AsEnumerable(), assert => assert.IsEquivalentTo(trackTimelineMap))
            .And.Satisfies(x => !(x.CommonStateTimelineMap.StateTimelines).Any(), assert => assert.IsTrue());
    }
    
    [Test]
    public async Task LesserDuration_ResultsIn_TrimmedTimelines()
    {
        var trackEventItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var trackEventTimeline = EventTimeline.Create(2, trackEventItems);
        
        var trackStateItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var trackStateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(2, StateKind1, trackStateItems)
        };
        var trackStateTimelineMap = trackStateTimelines.ToStateTimelineMap(2);

        var trackEventStateTimelineMap = EventStateTimelineMap.Create(2, trackEventTimeline, trackStateTimelineMap);
        var trackTimelineMap = new Dictionary<int, EventStateTimelineMap<int>>
        {
            [1] = trackEventStateTimelineMap
        };
        
        var commonStateItems = new TimelineItem<double>[]
        {
            new(0, 2),
            new(1, 3),
        };
        var commonStateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(2, StateKind2, commonStateItems)
        };
        var commonStateTimelineMap = commonStateTimelines.ToStateTimelineMap(2);
        
        const double duration = 1;
        var result = TrackEventStateTimelineMap.Create(duration, trackTimelineMap, commonStateTimelineMap);

        var expectedTrackTimelineMap = new Dictionary<int, EventStateTimelineMap<int>>
        {
            [1] = trackEventStateTimelineMap.Trim(duration)
        };
        var expectedCommonStateTimelines = commonStateTimelines
            .Select(x => x.Trim(duration))
            .ToArray();

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsDefault, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.TrackTimelineMap.AsEnumerable(),
                assert => assert.IsEquivalentTo(expectedTrackTimelineMap))
            .And.Satisfies(x => x.CommonStateTimelineMap.StateTimelines,
                assert => assert.IsEquivalentTo(expectedCommonStateTimelines.OrderBy(t => t.StateKind.Name)));
    }
    
    [Test]
    public async Task SameKindStateTimelines_ResultsIn_MergedStateTimelines()
    {
        // ReSharper disable once CollectionNeverUpdated.Local
        var trackTimelineMap = new Dictionary<int, EventStateTimelineMap<int>>();
        
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
        var commonStateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(2, StateKind1, stateItems1),
            StateTimeline.Create(2, StateKind1, stateItems2),
            StateTimeline.Create(2, StateKind2, stateItems3),
            StateTimeline.Create(2, StateKind2, stateItems4),
        };
        var commonStateTimelineMap = commonStateTimelines.ToStateTimelineMap(2);
        
        const double duration = 2;
        var result = TrackEventStateTimelineMap.Create(duration, trackTimelineMap, commonStateTimelineMap);
        
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
        var expectedCommonStateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(2, StateKind2, expectedStateItems2),
            StateTimeline.Create(2, StateKind1, expectedStateItems1),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsDefault, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => !(x.TrackTimelineMap.AsEnumerable()).Any(), assert => assert.IsTrue())
            .And.Satisfies(x => x.CommonStateTimelineMap.StateTimelines,
                assert => assert.IsEquivalentTo(expectedCommonStateTimelines.OrderBy(t => t.StateKind.Name)));
    }

    [Test]
    public async Task SameTrackEventStateTimelines_ResultsIn_MergedTrackEventStateTimelines()
    {
        const double duration = 2;
        
        var trackEventItems1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var trackEventTimeline1 = EventTimeline.Create(duration, trackEventItems1);
        
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
            StateTimeline.Create(duration, StateKind1, stateItems11),
            StateTimeline.Create(duration, StateKind2, stateItems12),
        };
        var stateTimelineMap1 = stateTimelines1.ToStateTimelineMap(duration);
        
        var trackEventStateTimelineMap1 = EventStateTimelineMap.Create(duration, trackEventTimeline1, stateTimelineMap1);
        
        var trackEventItems2 = new TimelineItem<int>[]
        {
            new(0.5, 3),
            new(1.5, 4),
        };
        var trackEventTimeline2 = EventTimeline.Create(duration, trackEventItems2);
        
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
            StateTimeline.Create(duration, StateKind1, stateItems21),
            StateTimeline.Create(duration, StateKind2, stateItems22),
        };
        var stateTimelineMap2 = stateTimelines2.ToStateTimelineMap(duration);
        
        var trackEventStateTimelineMap2 = EventStateTimelineMap.Create(duration, trackEventTimeline2, stateTimelineMap2);

        var trackTimelineMap = new KeyValuePair<int, EventStateTimelineMap<int>>[]
        {
            new(1, trackEventStateTimelineMap1),
            new(1, trackEventStateTimelineMap2),
        };

        var commonStateTimelines = StateTimelineMap.Create(0);
        
        var result = TrackEventStateTimelineMap.Create(duration, trackTimelineMap, commonStateTimelines);
        
        var expectedEventItems = new TimelineItem<int>[]
        {
            new(0, 1),
            new(0.5, 3),
            new(1, 2),
            new(1.5, 4),
        };
        var expectedEventTimeline = EventTimeline.Create(duration, expectedEventItems);
        
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
        var expectedCommonStateTimelines = new IStateTimeline[]
        {
            StateTimeline.Create(2, StateKind1, expectedStateItems1),
            StateTimeline.Create(2, StateKind2, expectedStateItems2),
        };
        var expectedCommonStateTimelineMap = expectedCommonStateTimelines.ToStateTimelineMap(duration);
        
        var expectedEventStateTimelineMap =
            EventStateTimelineMap.Create(duration, expectedEventTimeline, expectedCommonStateTimelineMap);
        var expectedTimelineMap = new KeyValuePair<int, EventStateTimelineMap<int>>[]
        {
            new(1, expectedEventStateTimelineMap),
        };

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsDefault, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.TrackTimelineMap.AsEnumerable(), assert => assert.IsEquivalentTo(expectedTimelineMap))
            .And.Satisfies(x => !(x.CommonStateTimelineMap.StateTimelines).Any(), assert => assert.IsTrue());
    }

    [Test]
    public async Task DifferentTrackEventStateTimelines_ResultsIn_DifferentTrackEventStateTimelines()
    {
        const double duration = 2;
        
        var trackEventItems1 = new TimelineItem<int>[]
        {
            new(0, 1),
            new(1, 2),
        };
        var trackEventTimeline1 = EventTimeline.Create(duration, trackEventItems1);
        
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
            StateTimeline.Create(duration, StateKind1, stateItems11),
            StateTimeline.Create(duration, StateKind2, stateItems12),
        };
        var stateTimelineMap1 = stateTimelines1.ToStateTimelineMap(duration);
        
        var trackEventStateTimelineMap1 = EventStateTimelineMap.Create(duration, trackEventTimeline1, stateTimelineMap1);
        
        var trackEventItems2 = new TimelineItem<int>[]
        {
            new(0.5, 3),
            new(1.5, 4),
        };
        var trackEventTimeline2 = EventTimeline.Create(duration, trackEventItems2);
        
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
            StateTimeline.Create(duration, StateKind1, stateItems21),
            StateTimeline.Create(duration, StateKind2, stateItems22),
        };
        var stateTimelineMap2 = stateTimelines2.ToStateTimelineMap(duration);
        
        var trackEventStateTimelineMap2 = EventStateTimelineMap.Create(duration, trackEventTimeline2, stateTimelineMap2);

        var trackTimelineMap = new KeyValuePair<int, EventStateTimelineMap<int>>[]
        {
            new(1, trackEventStateTimelineMap1),
            new(2, trackEventStateTimelineMap2),
        };

        var commonStateTimelineMap = StateTimelineMap.Create(0);

        var result = TrackEventStateTimelineMap.Create(duration, trackTimelineMap, commonStateTimelineMap);

        await Assert.That(result)
            .IsNotNull()
            .And.Satisfies(x => x.IsDefault, assert => assert.IsFalse())
            .And.Satisfies(x => x.Duration, assert => assert.IsEqualTo(duration))
            .And.Satisfies(x => x.TrackTimelineMap.AsEnumerable(), assert => assert.IsEquivalentTo(trackTimelineMap))
            .And.Satisfies(x => !(x.CommonStateTimelineMap.StateTimelines).Any(), assert => assert.IsTrue());
    }
}