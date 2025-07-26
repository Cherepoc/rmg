using System.Collections.Immutable;

namespace Rmg.Core.Events;

public static class TimelineExtensions
{
    public static T Unwrap<T>(this EventTimeline<T> timeline)
        where T : ITimelineLike<T>
    {
        var timelines = timeline
            .ToImmutableArray()
            .Where(x => !x.Value.IsEmpty)
            .Select(x => x.Value.Shift(x.Position));
        return T.Merge(timelines);
    }

    public static StateTimelineMap ToStateTimelineMap(this EventTimeline<StateMap> timeline)
    {
        if (timeline.IsEmpty || timeline.All(x => x.Value.IsDefault))
            return StateTimelineMap.Empty;

        var stateTimelines = timeline
            .Select(x => x.Value)
            .Where(x => !x.IsDefault)
            .SelectMany(x => x.Kinds)
            .Distinct()
            .Select(x => x.ExtractStateTimeline(timeline))
            .ToImmutableArray();

        return StateTimelineMap.Create(timeline.Duration, stateTimelines);
    }

    public static TrackEventStateTimelineMap<T> ToTrackEventStateTimelineMap<T>(
        this StateTimelineMap commonTimelineMap,
        double duration
    )
        where T : notnull
    {
        return TrackEventStateTimelineMap<T>.Create(duration, [], commonTimelineMap);
    }

    public static T Merge<T>(IEnumerable<T> items)
        where T : ITimelineLike<T>
    {
        return T.Merge(items);
    }

    public static T MergeWith<T>(this T timeline1, T timeline2)
        where T : ITimelineLike<T>
    {
        return T.Merge([timeline1, timeline2]);
    }

    public static EventTimeline<StateMap> AbsorbStateTimelineMap(
        this EventTimeline<StateMap> timeline,
        StateTimelineMap stateTimelineMap
    )
    {
        if (timeline.IsEmpty)
            return EventTimeline.Empty<StateMap>();

        if (stateTimelineMap.IsEmpty || stateTimelineMap.StateTimelines.All(x => x.IsDefault))
            return timeline;

        return timeline.MapValues((position, stateMap) => stateTimelineMap
            .GetEffectiveStateMapAt(position)
            .MergeWith(stateMap)
        );
    }

    public static EventTimeline<StateMap> AbsorbStateMap(
        this EventTimeline<StateMap> timeline,
        StateMap newStateMap
    )
    {
        if (timeline.IsEmpty)
            return EventTimeline.Empty<StateMap>();

        if (newStateMap.IsDefault)
            return timeline;

        return timeline.MapValues(newStateMap.MergeWith);
    }

    public static EventTimeline<StateMap> ToAbsorbedStateMapTimeline(this EventStateTimelineMap<StateMap> timeline)
    {
        if (timeline.EventTimeline.IsEmpty)
            return EventTimeline.Empty<StateMap>();
        if (timeline.StateTimelineMap.IsEmpty)
            return timeline.EventTimeline;

        return timeline.ToMappedEventTimeline((stateMap1, stateMap2) => stateMap1.MergeWith(stateMap2));
    }

    public static StateTimelineMap ToAbsorbedStateTimelineMap(this EventStateTimelineMap<StateMap> timeline)
    {
        return timeline.EventTimeline
            .ToStateTimelineMap()
            .MergeWith(timeline.StateTimelineMap);
    }

    public static T Unroll<T>(this IEnumerable<T> timelines)
        where T : ITimelineLike<T>
    {
        var shiftedTimelines = ImmutableArray.CreateBuilder<T>();
        double shiftPosition = 0;
        foreach (var timeline in timelines)
        {
            if (timeline.IsEmpty)
                continue;

            var shiftedTimeline = timeline.Shift(shiftPosition);
            shiftedTimelines.Add(shiftedTimeline);
            shiftPosition += timeline.Duration;
        }

        return T.Merge(shiftedTimelines.ToImmutable());
    }

    public static EventTimeline<T> ToEventTimeline<T>(this IEnumerable<TimelineItem<T>> items, double duration)
        where T : notnull
    {
        return EventTimeline<T>.Create(duration, items);
    }

    public static StateTimelineMap ToStateTimelineMap(this IEnumerable<TimelineItem<StateMap>> items, double duration)
    {
        var eventTimeline = items.ToEventTimeline(duration);
        return eventTimeline.ToStateTimelineMap();
    }

    public static T Repeat<T>(this T timeline, int count)
        where T : ITimelineLike<T>
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count == 0 || timeline.IsEmpty)
            return T.Empty;

        if (count == 1)
            return timeline;

        return Enumerable.Repeat(timeline, count)
            .Unroll();
    }
}
