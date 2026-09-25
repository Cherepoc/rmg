namespace Rmg.Core.Events;

public sealed class EventStateTimelineMap<T> : ITimelineLike<EventStateTimelineMap<T>>
    where T : notnull
{
    private EventStateTimelineMap(double duration, EventTimeline<T> eventTimeline, StateTimelineMap stateTimelineMap)
    {
        Duration = duration;
        EventTimeline = eventTimeline;
        StateTimelineMap = stateTimelineMap;
        IsDefault = eventTimeline.Count == 0 && stateTimelineMap.IsDefault;
    }

    public EventTimeline<T> EventTimeline { get; }

    public StateTimelineMap StateTimelineMap { get; }

    private static readonly EventStateTimelineMap<T> Zero =
        new(0, Events.EventTimeline.Create<T>(0), StateTimelineMap.Create(0));

    public static EventStateTimelineMap<T> Merge(IEnumerable<EventStateTimelineMap<T>> items)
    {
        var itemArray = items.AsImmutableArray();

        if (itemArray.Length == 0)
            return Zero;

        if (itemArray.Length == 1)
            return itemArray[0];

        var eventTimelines = itemArray.Select(x => x.EventTimeline);
        var mergedEventTimeline = EventTimeline<T>.Merge(eventTimelines);

        var timelineMaps = itemArray.Select(x => x.StateTimelineMap);
        var stateTimelineMap = StateTimelineMap.Merge(timelineMaps);

        var duration = itemArray.Max(x => x.Duration);
        return Create(duration, mergedEventTimeline, stateTimelineMap);
    }

    public double Duration { get; }

    /// <summary>Whether the map has no events and only default state.</summary>
    public bool IsDefault { get; }

    public EventStateTimelineMap<T> Shift(double offset)
    {
        if (offset == 0)
            return this;

        var newDuration = Duration + offset;
        if (newDuration < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Duration cannot be negative.");

        if (newDuration == 0)
            return Zero;

        return Create(newDuration, EventTimeline.Shift(offset), StateTimelineMap.Shift(offset));
    }

    public EventStateTimelineMap<T> Trim(double duration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration, nameof(duration));

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (duration == Duration)
            return this;

        return Create(duration, EventTimeline.Trim(duration), StateTimelineMap.Trim(duration));
    }

    public static EventStateTimelineMap<T> Create(
        double duration,
        EventTimeline<T> eventTimeline,
        StateTimelineMap stateTimelineMap
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration, nameof(duration));

        if (duration == 0)
            return Zero;

        var trimmedEventTimeline = eventTimeline.Trim(duration);
        var trimmedStateTimelineMap = stateTimelineMap.Trim(duration);

        return new EventStateTimelineMap<T>(duration, trimmedEventTimeline, trimmedStateTimelineMap);
    }

    public StateMap GetEffectiveStateMapAt(double position)
    {
        return StateTimelineMap.GetEffectiveStateMapAt(position);
    }

    public EventTimeline<TDest> ToMappedEventTimeline<TDest>(Func<T, StateMap, TDest> map)
        where TDest : notnull
    {
        if (EventTimeline.Count == 0)
            return Events.EventTimeline.Create<TDest>(Duration);

        var newEventItems = new TimelineItem<TDest>[EventTimeline.Count];
        for (var i = 0; i < EventTimeline.Count; i++)
        {
            var item = EventTimeline[i];
            var effectiveState = GetEffectiveStateMapAt(item.Position);
            var newValue = map(item.Value, effectiveState);
            newEventItems[i] = new TimelineItem<TDest>(item.Position, newValue);
        }

        return Events.EventTimeline.Create(Duration, newEventItems);
    }

    public EventStateTimelineMap<T> MergeStateMap(StateMap stateMap)
    {
        if (stateMap.IsDefault)
            return this;

        return Create(Duration, EventTimeline, StateTimelineMap.MergeStateMap(stateMap));
    }

    public EventStateTimelineMap<T> MergeStateTimelineMap(StateTimelineMap stateTimelineMap)
    {
        if (stateTimelineMap.IsDefault)
            return this;

        return Create(Duration, EventTimeline, StateTimelineMap.Merge([StateTimelineMap, stateTimelineMap]));
    }
}

public static class EventStateTimelineMap
{
    /// <summary>A map of the given duration with no events and only default state.</summary>
    public static EventStateTimelineMap<T> Create<T>(double duration)
        where T : notnull
    {
        return EventStateTimelineMap<T>.Create(duration, EventTimeline.Create<T>(duration), StateTimelineMap.Create(duration));
    }

    public static EventStateTimelineMap<T> Create<T>(
        double duration,
        EventTimeline<T> eventTimeline,
        StateTimelineMap stateTimelineMap
    )
        where T : notnull
    {
        return EventStateTimelineMap<T>.Create(duration, eventTimeline, stateTimelineMap);
    }

    public static EventStateTimelineMap<T> Create<T>(double duration, StateTimelineMap stateTimelineMap)
        where T : notnull
    {
        return EventStateTimelineMap<T>.Create(duration, EventTimeline.Create<T>(duration), stateTimelineMap);
    }

    public static EventStateTimelineMap<T> ToEventStateTimelineMap<T>(
        this EventTimeline<T> timeline,
        StateTimelineMap stateTimelineMap
    )
        where T : notnull
    {
        return EventStateTimelineMap<T>.Create(timeline.Duration, timeline, stateTimelineMap);
    }

    public static EventStateTimelineMap<T> ToEventStateTimelineMap<T>(this EventTimeline<T> timeline, StateMap stateMap)
        where T : notnull
    {
        var stateTimelineMap = stateMap.ToStateTimelineMap(timeline.Duration);
        return EventStateTimelineMap<T>.Create(timeline.Duration, timeline, stateTimelineMap);
    }
}
