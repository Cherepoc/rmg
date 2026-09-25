using System.Collections;
using System.Collections.Immutable;

namespace Rmg.Core.Events;

public sealed class EventTimeline<T> : ITimelineLike<EventTimeline<T>>, IReadOnlyList<TimelineItem<T>>
    where T : notnull
{
    private readonly ImmutableArray<TimelineItem<T>> _items;

    private EventTimeline(double duration, ImmutableArray<TimelineItem<T>> items)
    {
        Duration = duration;
        _items = items;
    }

    public IEnumerator<TimelineItem<T>> GetEnumerator()
    {
        return ((IEnumerable<TimelineItem<T>>)_items).GetEnumerator();
    }

    public int Count => _items.Length;

    public TimelineItem<T> this[int index] => _items[index];

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_items).GetEnumerator();
    }

    private static readonly EventTimeline<T> Zero = new(0, []);

    /// <summary>
    ///     A timeline with no events that still takes its duration, such as a bar in which nothing plays.
    /// </summary>
    public static EventTimeline<T> Create(double duration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration, nameof(duration));

        return duration == 0 ? Zero : new EventTimeline<T>(duration, []);
    }

    public static EventTimeline<T> Merge(IEnumerable<EventTimeline<T>> timelines)
    {
        var timelineArray = timelines.AsImmutableArray();

        if (timelineArray.Length == 0)
            return Zero;

        if (timelineArray.Length == 1)
            return timelineArray[0];

        var items = timelineArray
            .SelectMany(x => x.AsEnumerable())
            .OrderBy(x => x.Position)
            .ToImmutableArray();

        var duration = timelineArray.Max(x => x.Duration);
        return duration == 0 ? Zero : new EventTimeline<T>(duration, items);
    }

    public double Duration { get; }

    public EventTimeline<T> Trim(double duration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);

        if (duration == 0)
            return Zero;

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (duration == Duration)
            return this;

        var newItems = duration < Duration
            ? _items.Trim(duration)
            : _items;

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

        if (newDuration == 0)
            return Zero;

        return new EventTimeline<T>(newDuration, _items.Shift(offset));
    }

    public static EventTimeline<T> Create(double duration, IEnumerable<TimelineItem<T>> items)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration, nameof(duration));

        if (duration == 0)
            return Zero;

        var itemArray = items.AsImmutableArray();

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
        }

        return new EventTimeline<T>(duration, processedArray);
    }

    public EventTimeline<T> Stretch(double factor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(factor);

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (factor == 1)
            return this;

        if (factor == 0)
            return Zero;

        return new EventTimeline<T>(Duration * factor, _items.Stretch(factor));
    }

    public EventTimeline<TDest> MapValues<TDest>(Func<T, TDest> mapFunc)
        where TDest : notnull
    {
        if (_items.Length == 0)
            return EventTimeline<TDest>.Create(Duration);

        return new EventTimeline<TDest>(Duration, _items.MapValues(mapFunc));
    }

    public EventTimeline<TDest> MapValues<TDest>(Func<double, T, TDest> mapFunc)
        where TDest : notnull
    {
        if (_items.Length == 0)
            return EventTimeline<TDest>.Create(Duration);

        return new EventTimeline<TDest>(Duration, _items.MapValues(mapFunc));
    }

    public EventTimeline<T> FilterValues(Func<T, bool> filterFunc)
    {
        if (_items.Length == 0)
            return this;

        return new EventTimeline<T>(Duration, _items.FilterValues(filterFunc));
    }

    public EventTimeline<T> PhaseShift(double phase)
    {
        if (_items.Length == 0)
            return this;

        if (phase.Mod(Duration) == 0)
            return this;

        return new EventTimeline<T>(Duration, _items.PhaseShift(phase, Duration));
    }

    public EventStateTimelineMap<T> MergeStateMap(StateMap stateMap)
    {
        return EventStateTimelineMap<T>.Create(Duration, this, stateMap.ToStateTimelineMap(Duration));
    }

    public IEnumerable<TimelineItem<T>> AsEnumerable()
    {
        return this;
    }

    public ImmutableArray<TimelineItem<T>> ToImmutableArray()
    {
        return _items;
    }

    public EventTimeline<WithDuration<T>> WithDurations()
    {
        if (_items.Length == 0)
            return EventTimeline<WithDuration<T>>.Create(Duration);

        var items = ImmutableArray.CreateBuilder<TimelineItem<WithDuration<T>>>(_items.Length);

        for (var i = 0; i < _items.Length; i++)
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

    public static EventTimeline<T> Create<T>(double duration)
        where T : notnull
    {
        return EventTimeline<T>.Create(duration);
    }

    public static EventTimeline<T> Merge<T>(IEnumerable<EventTimeline<T>> timelines)
        where T : notnull
    {
        return EventTimeline<T>.Merge(timelines);
    }
}
