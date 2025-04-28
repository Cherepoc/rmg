using Rmg.Core.Events;

namespace Rmg.Core.Composition;

public sealed class EventStatePattern<T>
    where T : notnull
{
    public static EventStatePattern<T> Create(
        double duration,
        EventStateTimelineMap<T> timelineMap,
        EventTimeline<WithStateMap<EventStatePattern<T>>> patternTimeline
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);
        
        var mergedStateTimelineMap = patternTimeline
            .MapValues(x => x.Value.MergeStateMap(x.StateMap))
            .Unwrap()
            .MergeWith(timelineMap);
        return new EventStatePattern<T>(duration, timelineMap, patternTimeline, mergedStateTimelineMap);
    }

    public double Duration { get; }

    public EventStateTimelineMap<T> TimelineMap { get; }

    public EventTimeline<WithStateMap<EventStatePattern<T>>> PatternTimeline { get; }

    public EventStateTimelineMap<T> MergedTimelineMap { get; }

    private EventStatePattern(
        double duration,
        EventStateTimelineMap<T> timelineMap,
        EventTimeline<WithStateMap<EventStatePattern<T>>> patternTimeline,
        EventStateTimelineMap<T> mergedTimelineMap
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);
        
        Duration = duration;
        TimelineMap = timelineMap;
        PatternTimeline = patternTimeline;
        MergedTimelineMap = mergedTimelineMap;
    }

    public EventStateTimelineMap<T> MergeStateMap(StateMap stateMap)
    {
        return MergedTimelineMap.MergeStateMap(stateMap);
    }
}

public static class EventStatePattern
{
    public static EventStatePattern<T> Create<T>(
        double duration,
        EventStateTimelineMap<T> timelineMap,
        EventTimeline<WithStateMap<EventStatePattern<T>>> patternTimeline
    ) where T : notnull
    {
        return EventStatePattern<T>.Create(duration, timelineMap, patternTimeline);
    }
}
