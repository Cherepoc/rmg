using System.Collections.Immutable;

namespace Rmg.Core.Events;

public sealed class TrackEventStateTimelineMap<T> : ITimelineLike<TrackEventStateTimelineMap<T>>
    where T : notnull
{
    private TrackEventStateTimelineMap(
        double duration,
        ImmutableSortedDictionary<int, EventStateTimelineMap<T>> timelineMap,
        StateTimelineMap commonStateTimelineMap
    )
    {
        Duration = duration;
        TrackTimelineMap = timelineMap;
        CommonStateTimelineMap = commonStateTimelineMap;
        IsEmpty = TrackTimelineMap.Count == 0 && CommonStateTimelineMap.IsEmpty;
    }

    public ImmutableSortedDictionary<int, EventStateTimelineMap<T>> TrackTimelineMap { get; }

    public StateTimelineMap CommonStateTimelineMap { get; }

    public static TrackEventStateTimelineMap<T> Empty { get; } =
        new(0, ImmutableSortedDictionary<int, EventStateTimelineMap<T>>.Empty, StateTimelineMap.Empty);

    public static TrackEventStateTimelineMap<T> Merge(IEnumerable<TrackEventStateTimelineMap<T>> timelines)
    {
        var timelineArray = timelines
            .Where(x => !x.IsEmpty)
            .ToArray();

        if (timelineArray.Length == 0)
            return Empty;

        if (timelineArray.Length == 1)
            return timelineArray[0];

        var duration = timelineArray.Max(x => x.Duration);
        var mergedCommonStateTimelineMap =
            StateTimelineMap.Merge(timelineArray.Select(x => x.CommonStateTimelineMap.Trim(duration)));
        return Create(
            duration,
            timelineArray.SelectMany(x => x.TrackTimelineMap),
            mergedCommonStateTimelineMap
        );
    }

    public double Duration { get; }

    public bool IsEmpty { get; }

    public TrackEventStateTimelineMap<T> Shift(double offset)
    {
        if (offset == 0)
            return this;

        var newDuration = Duration + offset;
        if (newDuration < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "The resulting duration cannot be negative.");

        if (newDuration == 0 || IsEmpty)
            return Empty;

        var timelines = TrackTimelineMap
            .Select(x => new KeyValuePair<int, EventStateTimelineMap<T>>(x.Key, x.Value.Shift(offset)));
        var shiftedCommonStateTimelineMap = !CommonStateTimelineMap.IsEmpty
            ? CommonStateTimelineMap.Shift(offset)
            : CommonStateTimelineMap;
        return Create(newDuration, timelines, shiftedCommonStateTimelineMap);
    }

    public TrackEventStateTimelineMap<T> Trim(double duration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration, nameof(duration));

        if (IsEmpty || duration == 0)
            return Empty;

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (duration == Duration)
            return this;

        var timelines = TrackTimelineMap
            .Select(x => new KeyValuePair<int, EventStateTimelineMap<T>>(x.Key, x.Value.Trim(duration)));

        return Create(duration, timelines, CommonStateTimelineMap.Trim(duration));
    }

    public static TrackEventStateTimelineMap<T> Create(
        double duration,
        IEnumerable<KeyValuePair<int, EventStateTimelineMap<T>>> timelines,
        StateTimelineMap commonStateTimelineMap
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration, nameof(duration));

        if (duration == 0)
            return Empty;

        var timelineDictionary = timelines
            .Where(x => !x.Value.IsEmpty)
            .GroupBy(x => x.Key, x => x.Value.Trim(duration))
            .Select(x => new KeyValuePair<int, EventStateTimelineMap<T>>(x.Key, EventStateTimelineMap<T>.Merge(x)))
            .Where(x => !x.Value.IsEmpty)
            .ToImmutableSortedDictionary(x => x.Key, x => x.Value);

        var trimmedCommonStateTimelineMap = commonStateTimelineMap.Trim(duration);

        if (timelineDictionary.Count == 0 && trimmedCommonStateTimelineMap.IsEmpty)
            return Empty;

        return new TrackEventStateTimelineMap<T>(duration, timelineDictionary, trimmedCommonStateTimelineMap);
    }

    public TrackEventStateTimelineMap<TDest> MapTrackTimelines<TDest>(Func<EventStateTimelineMap<T>, EventStateTimelineMap<TDest>> map)
        where TDest : notnull
    {
        if (IsEmpty)
            return TrackEventStateTimelineMap<TDest>.Empty;

        var mappedTimelines = TrackTimelineMap
            .Select(x => new KeyValuePair<int, EventStateTimelineMap<TDest>>(x.Key, map(x.Value)))
            .Where(x => !x.Value.IsEmpty)
            .ToImmutableSortedDictionary(x => x.Key, x => x.Value);
        if (mappedTimelines.Count == 0 && CommonStateTimelineMap.IsEmpty)
            return TrackEventStateTimelineMap<TDest>.Empty;

        return new TrackEventStateTimelineMap<TDest>(Duration, mappedTimelines, CommonStateTimelineMap);
    }

    public TrackEventStateTimelineMap<T> MapTrackIndexes(IReadOnlyDictionary<int, int> trackMap)
    {
        if (IsEmpty)
            return Empty;

        if (trackMap.Count == 0)
            return this;

        var newTimelineMap = TrackTimelineMap.Select(x =>
            new KeyValuePair<int, EventStateTimelineMap<T>>(trackMap.GetValueOrDefault(x.Key, x.Key), x.Value)
        );
        return Create(Duration, newTimelineMap, CommonStateTimelineMap);
    }

    public TrackEventStateTimelineMap<T> MergeStateMap(StateMap stateMap)
    {
        if (stateMap.IsDefault)
            return this;

        if (IsEmpty)
            return Empty;

        return Create(Duration, TrackTimelineMap, CommonStateTimelineMap.MergeStateMap(stateMap));
    }
}

public static class TrackEventStateTimelineMap
{
    public static TrackEventStateTimelineMap<T> Empty<T>()
        where T : notnull
    {
        return TrackEventStateTimelineMap<T>.Empty;
    }

    public static TrackEventStateTimelineMap<T> Create<T>(
        double duration,
        IEnumerable<KeyValuePair<int, EventStateTimelineMap<T>>> timelines,
        StateTimelineMap commonStateTimelineMap
    )
        where T : notnull
    {
        return TrackEventStateTimelineMap<T>.Create(duration, timelines, commonStateTimelineMap);
    }

    public static TrackEventStateTimelineMap<T> Merge<T>(IEnumerable<TrackEventStateTimelineMap<T>> timelines)
        where T : notnull
    {
        return TrackEventStateTimelineMap<T>.Merge(timelines);
    }
}
