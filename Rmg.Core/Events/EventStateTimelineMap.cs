namespace Rmg.Core.Events;

public sealed class EventStateTimelineMap<T> : ITimelineLike<EventStateTimelineMap<T>>
    where T : notnull
{
    public static EventStateTimelineMap<T> Empty { get; } =
        new(0, Events.EventTimeline.Empty<T>(), StateTimelineMap.Empty);

    public static EventStateTimelineMap<T> Create(
        double duration,
        EventTimeline<T> eventTimeline,
        StateTimelineMap stateTimelineMap
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration, nameof(duration));

        if (duration == 0)
            return Empty;

        var trimmedEventTimeline = eventTimeline.Trim(duration);
        var trimmedStateTimelineMap = stateTimelineMap.Trim(duration);

        if (trimmedEventTimeline.IsEmpty && trimmedStateTimelineMap.IsEmpty)
            return Empty;

        return new EventStateTimelineMap<T>(duration, trimmedEventTimeline, trimmedStateTimelineMap);
    }

    public static EventStateTimelineMap<T> Merge(IEnumerable<EventStateTimelineMap<T>> items)
    {
        var itemArray = items.AsImmutableArray();

        if (itemArray.Length == 0 || itemArray.All(x => x.IsEmpty))
            return Empty;

        if (itemArray.Length == 1)
            return itemArray[0];

        var eventTimelines = itemArray.Select(x => x.EventTimeline);
        var mergedEventTimeline = EventTimeline<T>.Merge(eventTimelines);
        
        var timelineMaps = itemArray.Select(x => x.StateTimelineMap);
        var stateTimelineMap = StateTimelineMap.Merge(timelineMaps);

        var duration = itemArray.Max(x => x.Duration);
        return Create(duration, mergedEventTimeline, stateTimelineMap);
    }

    private EventStateTimelineMap(double duration, EventTimeline<T> eventTimeline, StateTimelineMap stateTimelineMap)
    {
        Duration = duration;
        EventTimeline = eventTimeline;
        StateTimelineMap = stateTimelineMap;
        IsEmpty = eventTimeline.IsEmpty && stateTimelineMap.IsEmpty;
    }

    public double Duration { get; }

    public bool IsEmpty { get; }

    public EventTimeline<T> EventTimeline { get; }

    public StateTimelineMap StateTimelineMap { get; }

    public StateMap GetEffectiveStateMapAt(double position)
    {
        return StateTimelineMap.GetEffectiveStateMapAt(position);
    }

    public EventStateTimelineMap<T> Shift(double offset)
    {
        if (offset == 0)
            return this;

        var newDuration = Duration + offset;
        if (newDuration < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Duration cannot be negative.");

        if (newDuration == 0 || IsEmpty)
            return Empty;
        
        var shiftedEventTimeline = !EventTimeline.IsEmpty
            ? EventTimeline.Shift(offset)
            : EventTimeline;
        
        var shiftedStateTimelineMap = !StateTimelineMap.IsEmpty
            ? StateTimelineMap.Shift(offset)
            : StateTimelineMap;

        return Create(newDuration, shiftedEventTimeline, shiftedStateTimelineMap);
    }

    public EventStateTimelineMap<T> Trim(double duration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration, nameof(duration));

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (duration == Duration)
            return this;

        if (IsEmpty)
            return Empty;

        return Create(duration, EventTimeline.Trim(duration), StateTimelineMap.Trim(duration));
    }

    public EventStateTimelineMap<TDest> MapEventTimeline<TDest>(Func<EventTimeline<T>, EventTimeline<TDest>> map)
        where TDest : notnull
    {
        return EventStateTimelineMap<TDest>.Create(Duration, map(EventTimeline), StateTimelineMap);
    }

    public EventTimeline<TDest> ToMappedEventTimeline<TDest>(Func<T, StateMap, TDest> map)
        where TDest : notnull
    {
        if (IsEmpty || EventTimeline.IsEmpty)
            return Events.EventTimeline.Empty<TDest>();

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
        if (IsEmpty)
            return Empty;
        
        if (stateMap.IsDefault)
            return this;
        
        return Create(Duration, EventTimeline, StateTimelineMap.MergeStateMap(stateMap));
    }
    
    public EventStateTimelineMap<T> MergeStateTimelineMap(StateTimelineMap stateTimelineMap)
    {
        if (IsEmpty)
            return Empty;

        if (stateTimelineMap.IsEmpty)
            return this;

        return Create(Duration, EventTimeline, StateTimelineMap.Merge([StateTimelineMap, stateTimelineMap]));
    }
}

public static class EventStateTimelineMap
{
    public static EventStateTimelineMap<T> Empty<T>()
        where T : notnull
    {
        return EventStateTimelineMap<T>.Empty;
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
        return EventStateTimelineMap<T>.Create(duration, EventTimeline.Empty<T>(), stateTimelineMap);
    }

    public static EventStateTimelineMap<T> Unwrap<T>(this EventStateTimelineMap<EventStateTimelineMap<T>> timelineMap)
        where T : notnull
    {
        var unwrappedEventTimeline = timelineMap.EventTimeline.Unwrap();
        var stateTimelineMap =
            StateTimelineMap.Merge([timelineMap.StateTimelineMap, unwrappedEventTimeline.StateTimelineMap]);
        return Create(timelineMap.Duration, unwrappedEventTimeline.EventTimeline, stateTimelineMap);
    }

    public static EventStateTimelineMap<T> ToEventStateTimelineMap<T>(
        this EventTimeline<T> timeline,
        StateTimelineMap stateTimelineMap
    ) where T : notnull
    {
        return EventStateTimelineMap<T>.Create(timeline.Duration, timeline, stateTimelineMap);
    }
}