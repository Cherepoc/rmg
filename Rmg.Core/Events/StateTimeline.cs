using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Rmg.Core.Events;

[DebuggerDisplay("[StateTimeline[{_items.Length}] {StateKind.Name}]")]
public sealed class StateTimeline<T> : IStateTimeline, ITimelineLike<StateTimeline<T>>, IReadOnlyList<TimelineItem<T>>
    where T : notnull
{
    private readonly ImmutableArray<TimelineItem<T>> _items;

    public StateTimeline(double duration, StateKind<T> stateKind, ImmutableArray<TimelineItem<T>> items)
    {
        Duration = duration;
        StateKind = stateKind;
        _items = items;
        Positions = GeneratePositions(items, stateKind, duration);
    }

    public StateKind<T> StateKind { get; }

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

    public bool IsEmpty => Duration == 0;

    public bool IsDefault => _items.Length == 0;

    public ImmutableArray<double> Positions { get; }

    IStateKind IStateTimeline.StateKind => StateKind;

    object IStateTimeline.GetEffectiveValueAt(double position)
    {
        return GetEffectiveValueAt(position);
    }

    IState IStateTimeline.GetEffectiveStateAt(double position)
    {
        return GetEffectiveStateAt(position);
    }

    IStateTimeline IStateTimeline.Trim(double duration)
    {
        return Trim(duration);
    }

    IStateTimeline IStateTimeline.Shift(double offset)
    {
        return Shift(offset);
    }

    IStateTimeline IStateTimeline.Stretch(double factor)
    {
        return Stretch(factor);
    }

    IStateTimeline IStateTimeline.PhaseShift(double phase)
    {
        return PhaseShift(phase);
    }

    public static StateTimeline<T> Empty => Events.StateKind.Empty<T>().EmptyTimeline;

    public static StateTimeline<T> Merge(IEnumerable<StateTimeline<T>> timelines)
    {
        var timelineArray = timelines
            .Where(x => !x.IsEmpty)
            .ToArray();

        if (timelineArray.Length == 0)
            return Events.StateKind.Empty<T>().EmptyTimeline;

        if (timelineArray.Length == 1)
            return timelineArray[0];

        var stateKind = timelineArray[0].StateKind;

        if (timelineArray.Any(x => x.StateKind != stateKind))
            throw new ArgumentException(
                $"Expected all timelines to have event kind '{stateKind.Name}'.",
                nameof(timelines)
            );

        var duration = timelineArray.Max(x => x.Duration);

        var positions = timelineArray
            .SelectMany(x => x.Positions)
            .Where(x => x < duration)
            .Distinct()
            .Order()
            .ToArray();

        var items = new List<TimelineItem<T>>(positions.Length);
        var previousValue = stateKind.DefaultValue;
        foreach (var position in positions)
        {
            var values = timelineArray
                .Select(x => x.GetEffectiveValueAt(position))
                .ToArray();
            var aggregatedValue = stateKind.AggregateValues(values);
            if (stateKind.CheckValuesEqual(aggregatedValue, previousValue))
                continue;

            previousValue = aggregatedValue;
            items.Add(new TimelineItem<T>(position, aggregatedValue));
        }

        if (items.Count == 0)
            return stateKind.EmptyTimeline;

        return new StateTimeline<T>(duration, stateKind, [..items]);
    }

    public double Duration { get; }

    public StateTimeline<T> Trim(double duration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);

        if (duration == 0 || IsEmpty)
            return StateKind.EmptyTimeline;

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (duration == Duration)
            return this;

        var newItems = _items.TrimState(StateKind, Duration, duration);
        if (newItems.Length == 0)
            return StateKind.EmptyTimeline;

        return new StateTimeline<T>(duration, StateKind, newItems);
    }

    public StateTimeline<T> Shift(double offset)
    {
        var newDuration = Duration + offset;
        if (newDuration < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be greater then the duration.");

        if (offset == 0)
            return this;

        if (newDuration == 0 || IsEmpty)
            return StateKind.EmptyTimeline;

        var newItems = _items.ShiftState(StateKind, offset);
        if (newItems.Length == 0)
            return StateKind.EmptyTimeline;

        return new StateTimeline<T>(newDuration, StateKind, newItems);
    }

    public static StateTimeline<T> Create(double duration, StateKind<T> stateKind, IEnumerable<TimelineItem<T>> items)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);

        if (duration == 0)
            return stateKind.EmptyTimeline;

        var itemArray = items.AsImmutableArray();

        if (itemArray.Length == 0)
            return new StateTimeline<T>(duration, stateKind, []);

        ImmutableArray<TimelineItem<T>> processedItemArray;
        if (!CheckArrayNeedsPreprocessing(duration, stateKind, itemArray))
        {
            processedItemArray = itemArray;
        }
        else
        {
            var itemsWithDuplicates = itemArray
                .Where(x => x.Position >= 0 && x.Position < duration)
                .GroupBy(x => x.Position)
                .Select(x => new TimelineItem<T>(x.Key, stateKind.AggregateValues(x.Select(y => y.Value))))
                .OrderBy(x => x.Position);
            processedItemArray = RemoveConsecutiveDuplicateValues(itemsWithDuplicates, stateKind).ToImmutableArray();
            if (processedItemArray.Length == 0)
                return new StateTimeline<T>(duration, stateKind, []);
        }

        return new StateTimeline<T>(duration, stateKind, processedItemArray);
    }

    public T GetEffectiveValueAt(double position)
    {
        if (position >= Duration)
            return StateKind.DefaultValue;

        var index = _items.GetIndexAtFloor(position);
        return index >= 0 ? this[index].Value : StateKind.DefaultValue;
    }

    public State<T> GetEffectiveStateAt(double position)
    {
        return new State<T>(StateKind, GetEffectiveValueAt(position));
    }

    public StateTimeline<T> Merge(T value)
    {
        if (Duration == 0 || StateKind.CheckValueIsDefault(value))
            return this;

        var mergeTimeline = new StateTimeline<T>(Duration, StateKind, [new TimelineItem<T>(0, value)]);
        return Merge([this, mergeTimeline]);
    }

    public IStateTimeline Merge(object value)
    {
        return Merge((T)value);
    }

    public StateTimeline<T> Stretch(double factor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(factor);

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (factor == 1)
            return this;

        if (_items.Length == 0 || factor == 0)
            return StateKind.EmptyTimeline;

        return new StateTimeline<T>(Duration * factor, StateKind, _items.Stretch(factor));
    }

    public StateTimeline<T> PhaseShift(double phase)
    {
        if (_items.Length == 0)
            return StateKind.EmptyTimeline;

        if (phase.Mod(Duration) == 0)
            return this;

        var newItems = _items.PhaseShiftState(StateKind, phase, Duration);
        if (newItems.Length == 0)
            return StateKind.EmptyTimeline;

        return new StateTimeline<T>(Duration, StateKind, newItems);
    }

    public IEnumerable<TimelineItem<T>> AsEnumerable()
    {
        return this;
    }

    private static bool CheckArrayNeedsPreprocessing(
        double duration,
        StateKind<T> stateKind,
        ImmutableArray<TimelineItem<T>> array
    )
    {
        if (array.Length == 0)
            return false;

        for (var i = 0; i < array.Length; i++)
        {
            var item = array[i];
            var previousItemCheck = true;
            var startItemCheck = true;
            if (i > 0)
            {
                var previousItem = array[i - 1];
                previousItemCheck =
                    item.Position > previousItem.Position &&
                    !stateKind.CheckValuesEqual(item.Value, previousItem.Value);
            }
            else
            {
                startItemCheck = !stateKind.CheckValueIsDefault(item.Value);
            }

            if (!startItemCheck || !previousItemCheck || item.Position < 0 || item.Position >= duration)
                return true;
        }

        return false;
    }

    private static IEnumerable<TimelineItem<T>> RemoveConsecutiveDuplicateValues(
        IEnumerable<TimelineItem<T>> source,
        StateKind<T> stateKind
    )
    {
        var previousValue = stateKind.DefaultValue;
        foreach (var item in source)
        {
            if (!stateKind.CheckValuesEqual(previousValue, item.Value))
                yield return item;

            previousValue = item.Value;
        }
    }

    private static ImmutableArray<double> GeneratePositions(
        ImmutableArray<TimelineItem<T>> items,
        StateKind<T> stateKind,
        double duration
    )
    {
        var appendZeroPosition = items.IsEmpty || items[0].Position > 0;
        var appendDurationPosition = items.IsEmpty || !stateKind.CheckValueIsDefault(items[^1].Value);
        var length = items.Length + (appendZeroPosition ? 1 : 0) + (appendDurationPosition ? 1 : 0);
        var builder = ImmutableArray.CreateBuilder<double>(length);
        if (appendZeroPosition)
            builder.Add(0);
        foreach (var timelineItem in items)
            builder.Add(timelineItem.Position);
        if (appendDurationPosition)
            builder.Add(duration);
        return builder.ToImmutable();
    }
}

public static class StateTimeline
{
    public static StateTimeline<T> Create<T>(
        double duration,
        StateKind<T> stateKind,
        IEnumerable<TimelineItem<T>> items
    )
        where T : notnull
    {
        return StateTimeline<T>.Create(duration, stateKind, items);
    }

    public static StateTimeline<T> Merge<T>(IEnumerable<StateTimeline<T>> timelines)
        where T : notnull
    {
        return StateTimeline<T>.Merge(timelines);
    }

    public static ImmutableArray<IStateTimeline> Merge(IEnumerable<IStateTimeline> timelines)
    {
        var timelineArray = timelines.AsImmutableArray();

        if (timelineArray.Length == 0 || timelineArray.All(x => x.IsEmpty))
            return ImmutableArray<IStateTimeline>.Empty;

        return timelineArray
            .Where(x => !x.IsEmpty)
            .GroupBy(x => x.StateKind)
            .Select(group => group.Key.MergeTimelines(group))
            .Where(x => !x.IsEmpty)
            .ToImmutableArray();
    }

    public static StateTimelineMap ToStateTimelineMap(this IEnumerable<IStateTimeline> timelines, double duration)
    {
        return StateTimelineMap.Create(duration, timelines);
    }
}

public interface IStateTimeline
{
    bool IsEmpty { get; }

    bool IsDefault { get; }

    IStateKind StateKind { get; }

    ImmutableArray<double> Positions { get; }

    object GetEffectiveValueAt(double position);

    IState GetEffectiveStateAt(double position);

    IStateTimeline Trim(double duration);

    IStateTimeline Shift(double offset);

    IStateTimeline Stretch(double factor);

    IStateTimeline PhaseShift(double phase);
}
