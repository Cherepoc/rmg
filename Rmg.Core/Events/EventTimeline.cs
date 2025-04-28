using System.Collections;
using System.Collections.Immutable;

namespace Rmg.Core.Events;

public sealed class EventTimeline<T> : ITimelineLike<EventTimeline<T>>, IReadOnlyList<TimelineItem<T>>
    where T : notnull
{
    public static EventTimeline<T> Empty { get; } = new(0, []);

    public static EventTimeline<T> Create(double duration, IEnumerable<TimelineItem<T>> items)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration, nameof(duration));

        if (duration == 0)
            return Empty;

        var itemArray = items.AsImmutableArray();
        if (itemArray.Length == 0)
            return Empty;

        ImmutableArray<TimelineItem<T>> processedArray;
        if (!CheckArrayNeedsPreprocessing(duration, itemArray))
        {
            processedArray = itemArray;
        }
        else
        {
            processedArray = itemArray
                .Where(x => x.Position >= 0 && x.Position < duration)
                .OrderBy(x => x.Position)
                .ToImmutableArray();
            if (processedArray.Length == 0)
                return Empty;
        }

        return new EventTimeline<T>(duration, processedArray);
    }

    public static EventTimeline<T> Merge(IEnumerable<EventTimeline<T>> timelines)
    {
        var timelineArray = timelines.AsImmutableArray();

        if (timelineArray.Length == 0 || timelineArray.All(x => x.Count == 0))
            return Empty;

        if (timelineArray.Length == 1)
            return timelineArray[0];

        var items = timelineArray
            .SelectMany(x => x.AsEnumerable())
            .OrderBy(x => x.Position)
            .ToImmutableArray();

        var duration = timelineArray.Max(x => x.Duration);
        return new EventTimeline<T>(duration, items);
    }

    public static EventTimeline<T> Unwrap(EventTimeline<EventTimeline<T>> timeline)
    {
        if (timeline.IsEmpty)
            return Empty;

        var timelines = timeline
            .AsEnumerable()
            .Where(x => !x.Value.IsEmpty)
            .Select(x => x.Value.Shift(x.Position));
        return Merge(timelines);
    }

    private readonly ImmutableArray<TimelineItem<T>> _items;

    private EventTimeline(double duration, ImmutableArray<TimelineItem<T>> items)
    {
        Duration = duration;
        _items = items;
    }

    public double Duration { get; }

    public bool IsEmpty => _items.Length == 0;

    public EventTimeline<T> Trim(double duration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);

        if (IsEmpty || duration == 0)
            return Empty;

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (duration == Duration)
            return this;

        var newItems = duration < Duration
            ? _items.Trim(duration)
            : _items;
        if (newItems.Length == 0)
            return Empty;

        return new EventTimeline<T>(duration, newItems);
    }

    public EventTimeline<T> Shift(double offset)
    {
        var newDuration = Duration + offset;
        if (newDuration < 0)
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                "Offset cannot set the timeline to negative duration."
            );

        if (offset == 0)
            return this;

        if (newDuration == 0 || IsEmpty)
            return Empty;

        var newItems = _items.Shift(offset);
        if (newItems.Length == 0)
            return Empty;

        return new EventTimeline<T>(newDuration, newItems);
    }

    public EventTimeline<T> Stretch(double factor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(factor);

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (factor == 1)
            return this;

        if (_items.Length == 0 || factor == 0)
            return Empty;

        return new EventTimeline<T>(Duration * factor, _items.Stretch(factor));
    }

    public EventTimeline<TDest> MapValues<TDest>(Func<T, TDest> mapFunc)
        where TDest : notnull
    {
        if (IsEmpty)
            return EventTimeline<TDest>.Empty;

        return new EventTimeline<TDest>(Duration, _items.MapValues(mapFunc));
    }

    public EventTimeline<TDest> MapValues<TDest>(Func<double, T, TDest> mapFunc)
        where TDest : notnull
    {
        if (IsEmpty)
            return EventTimeline<TDest>.Empty;

        return new EventTimeline<TDest>(Duration, _items.MapValues(mapFunc));
    }

    public EventTimeline<T> FilterValues(Func<T, bool> filterFunc)
    {
        if (IsEmpty)
            return Empty;

        return new EventTimeline<T>(Duration, _items.FilterValues(filterFunc));
    }

    public EventTimeline<T> PhaseShift(double phase)
    {
        if (IsEmpty)
            return Empty;

        if (phase.Mod(Duration) == 0)
            return this;

        return new EventTimeline<T>(Duration, _items.PhaseShift(phase, Duration));
    }

    public EventStateTimelineMap<T> MergeStateMap(StateMap stateMap)
    {
        return EventStateTimelineMap<T>.Create(Duration, this, stateMap.ToStateTimelineMap(Duration));
    }

    public IEnumerator<TimelineItem<T>> GetEnumerator() => ((IEnumerable<TimelineItem<T>>)_items).GetEnumerator();

    public int Count => _items.Length;

    public TimelineItem<T> this[int index] => _items[index];

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_items).GetEnumerator();

    public IEnumerable<TimelineItem<T>> AsEnumerable() => this;

    public ImmutableArray<TimelineItem<T>> ToImmutableArray() => _items;
    
    public EventTimeline<WithDuration<T>> WithDurations()
    {
        if (IsEmpty)
            return EventTimeline<WithDuration<T>>.Empty;

        var items = ImmutableArray.CreateBuilder<TimelineItem<WithDuration<T>>>(_items.Length);
        
        for(int i = 0; i < _items.Length; i++)
        {
            var item = _items[i];
            var nextPosition = i + 1 < _items.Length
                ? _items[i + 1].Position
                : Duration;
            var duration = nextPosition - item.Position;
            items.Add(item.MapValue(x => x.WithDuration(duration)));
        }
        
        return EventTimeline.Create(Duration, items.ToImmutable());
    }

    public StateTimeline<T> ToStateTimeline(StateKind<T> stateKind)
    {
        return stateKind.CreateStateTimelineFromEventTimeline(this);
    }

    private static bool CheckArrayNeedsPreprocessing(double duration, ImmutableArray<TimelineItem<T>> array)
    {
        for (var i = 0; i < array.Length; i++)
        {
            var item = array[i];
            var isOutOfOrder = i > 0 && item.Position <= array[i - 1].Position;
            if (isOutOfOrder || item.Position < 0 || item.Position >= duration)
                return true;
        }

        return false;
    }
}

public static class EventTimeline
{
    public static EventTimeline<T> Create<T>(double duration, IEnumerable<TimelineItem<T>> items)
        where T : notnull
    {
        return EventTimeline<T>.Create(duration, items);
    }
    
    public static EventTimeline<T> ToEventTimeline<T>(this T value, double duration)
        where T : notnull
    {
        return EventTimeline<T>.Create(duration, [value.ToTimelineItem(0)]);
    }

    public static EventTimeline<T> Empty<T>()
        where T : notnull
    {
        return EventTimeline<T>.Empty;
    }

    public static EventTimeline<T> OfOne<T>(double duration, T value)
        where T : notnull
    {
        return EventTimeline<T>.Create(duration, [new TimelineItem<T>(0, value)]);
    }

    public static EventTimeline<T> Merge<T>(IEnumerable<EventTimeline<T>> timelines)
        where T : notnull
    {
        return EventTimeline<T>.Merge(timelines);
    }
}