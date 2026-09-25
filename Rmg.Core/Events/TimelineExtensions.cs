using System.Collections.Immutable;

namespace Rmg.Core.Events;

public static class TimelineExtensions
{
    public static StateTimelineMap ToStateTimelineMap(this EventTimeline<StateMap> timeline)
    {
        if (timeline.All(x => x.Value.IsDefault))
            return StateTimelineMap.Create(timeline.Duration);

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

    public static T Unroll<T>(this IEnumerable<T> timelines)
        where T : ITimelineLike<T>
    {
        var shiftedTimelines = ImmutableArray.CreateBuilder<T>();
        double shiftPosition = 0;
        foreach (var timeline in timelines)
        {
            // a timeline of no duration takes no time; one with no events still takes its duration
            if (timeline.Duration == 0)
                continue;

            var shiftedTimeline = timeline.Shift(shiftPosition);
            shiftedTimelines.Add(shiftedTimeline);
            shiftPosition += timeline.Duration;
        }

        return T.Merge(shiftedTimelines.ToImmutable());
    }

    public static T Repeat<T>(this T timeline, int count)
        where T : ITimelineLike<T>
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count == 0 || timeline.Duration == 0)
            return T.Merge([]);

        if (count == 1)
            return timeline;

        return Enumerable.Repeat(timeline, count)
            .Unroll();
    }
}
