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
        IsDefault = TrackTimelineMap.Count == 0 && CommonStateTimelineMap.IsDefault;
    }

    public ImmutableSortedDictionary<int, EventStateTimelineMap<T>> TrackTimelineMap { get; }

    public StateTimelineMap CommonStateTimelineMap { get; }

    private static readonly TrackEventStateTimelineMap<T> Zero =
        new(0, ImmutableSortedDictionary<int, EventStateTimelineMap<T>>.Empty, StateTimelineMap.Create(0));

    public static TrackEventStateTimelineMap<T> Merge(IEnumerable<TrackEventStateTimelineMap<T>> timelines)
    {
        var timelineArray = timelines
            .Where(x => x.Duration > 0)
            .ToArray();

        if (timelineArray.Length == 0)
            return Zero;

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

    /// <summary>Whether the map has no tracks and only default common state.</summary>
    public bool IsDefault { get; }

    public TrackEventStateTimelineMap<T> Shift(double offset)
    {
        if (offset == 0)
            return this;

        var newDuration = Duration + offset;
        if (newDuration < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "The resulting duration cannot be negative.");

        if (newDuration == 0)
            return Zero;

        var timelines = TrackTimelineMap
            .Select(x => new KeyValuePair<int, EventStateTimelineMap<T>>(x.Key, x.Value.Shift(offset)));
        return Create(newDuration, timelines, CommonStateTimelineMap.Shift(offset));
    }

    public TrackEventStateTimelineMap<T> Trim(double duration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration, nameof(duration));

        if (duration == 0)
            return Zero;

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
            return Zero;

        var timelineDictionary = timelines
            .Where(x => !x.Value.IsDefault)
            .GroupBy(x => x.Key, x => x.Value.Trim(duration))
            .Select(x => new KeyValuePair<int, EventStateTimelineMap<T>>(x.Key, EventStateTimelineMap<T>.Merge(x)))
            .Where(x => !x.Value.IsDefault)
            .ToImmutableSortedDictionary(x => x.Key, x => x.Value);

        var trimmedCommonStateTimelineMap = commonStateTimelineMap.Trim(duration);

        return new TrackEventStateTimelineMap<T>(duration, timelineDictionary, trimmedCommonStateTimelineMap);
    }

    public TrackEventStateTimelineMap<T> MergeStateMap(StateMap stateMap)
    {
        if (stateMap.IsDefault)
            return this;

        return Create(Duration, TrackTimelineMap, CommonStateTimelineMap.MergeStateMap(stateMap));
    }
}

public static class TrackEventStateTimelineMap
{
    /// <summary>A map of the given duration with no tracks and only default common state.</summary>
    public static TrackEventStateTimelineMap<T> Create<T>(double duration)
        where T : notnull
    {
        return TrackEventStateTimelineMap<T>.Create(duration, [], StateTimelineMap.Create(duration));
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
