using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Rmg.Core.Events;

[DebuggerDisplay("[StateTimeline[{_items.Length}] {StateKind.Name}]")]
public sealed class StateTimeline<T> : IStateTimeline, IReadOnlyList<TimelineItem<T>>
    where T : notnull
{
    private readonly ImmutableArray<TimelineItem<T>> _items;

    // while a trace runs, the timelines this one was made from, so that a state read from it tells its layers apart;
    // empty otherwise
    private readonly ImmutableArray<StateTimeline<T>> _sources;

    public StateTimeline(double duration, StateKind<T> stateKind, ImmutableArray<TimelineItem<T>> items)
        : this(duration, stateKind, items, null, [])
    {
    }

    private StateTimeline(
        double duration,
        StateKind<T> stateKind,
        ImmutableArray<TimelineItem<T>> items,
        string? layer,
        ImmutableArray<StateTimeline<T>> sources
    )
    {
        Layer = layer;
        _sources = sources;
        Duration = duration;
        StateKind = stateKind;
        _items = items;
        Positions = GeneratePositions(items, stateKind, duration);
    }

    public StateKind<T> StateKind { get; }

    /// <summary>
    ///     The layer the timeline's values come from, such as the bar, which a <see cref="StateTrace" /> records for
    ///     the states read from it. A trimmed or shifted timeline keeps it, and so does a merge of timelines that all
    ///     have it. A merge of timelines of different layers has none, but while a trace runs it keeps the timelines
    ///     it was merged from, and a state read from it is made of what they hold there.
    /// </summary>
    public string? Layer { get; }

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

    /// <summary>
    ///     Merges timelines of one kind. There must be at least one, which gives the kind; to merge any number of
    ///     them, use <see cref="StateKind{T}.MergeTimelines(IEnumerable{StateTimeline{T}})" />.
    /// </summary>
    public static StateTimeline<T> Merge(IEnumerable<StateTimeline<T>> timelines)
    {
        var allTimelines = timelines.ToArray();
        if (allTimelines.Length == 0)
            throw new ArgumentException("There must be at least one timeline, which gives the state kind.", nameof(timelines));

        var timelineArray = allTimelines
            .Where(x => x.Duration > 0)
            .ToArray();

        if (timelineArray.Length == 0)
            return allTimelines[0].StateKind.CreateDefaultTimeline(0);

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

        // timelines of one layer, such as the sections' parts of it put one after another, stay that layer's; while a
        // trace runs, timelines of different layers are kept for the parts of the merged values
        var layer = timelineArray[0].Layer;
        if (timelineArray.All(x => x.Layer == layer) && layer is not null)
            return new StateTimeline<T>(duration, stateKind, [..items], layer, []);

        return new StateTimeline<T>(duration, stateKind, [..items], null, StateTrace.IsRunning ? [..timelineArray] : []);
    }

    public double Duration { get; }

    public StateTimeline<T> Trim(double duration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);

        if (duration == 0)
            return StateKind.CreateDefaultTimeline(0);

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (duration == Duration)
            return this;

        return new StateTimeline<T>(
            duration,
            StateKind,
            _items.TrimState(StateKind, Duration, duration),
            Layer,
            [.._sources.Select(x => x.Trim(duration))]
        );
    }

    public StateTimeline<T> Shift(double offset)
    {
        var newDuration = Duration + offset;
        if (newDuration < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be greater then the duration.");

        if (offset == 0)
            return this;

        if (newDuration == 0)
            return StateKind.CreateDefaultTimeline(0);

        return new StateTimeline<T>(
            newDuration,
            StateKind,
            _items.ShiftState(StateKind, offset),
            Layer,
            [.._sources.Select(x => x.Shift(offset))]
        );
    }

    public static StateTimeline<T> Create(double duration, StateKind<T> stateKind, IEnumerable<TimelineItem<T>> items)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);

        if (duration == 0)
            return stateKind.CreateDefaultTimeline(0);

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
        var value = GetEffectiveValueAt(position);
        if (!StateTrace.IsRunning)
            return new State<T>(StateKind, value);

        if (Layer is not null)
            return new State<T>(StateKind, value, [new StateContribution(Layer, value)]);

        // the parts of a merged value: what each timeline it was merged from holds here, those at their default
        // adding nothing
        var contributions = _sources
            .Select(x => x.GetEffectiveStateAt(position))
            .Where(x => !x.IsDefault)
            .SelectMany(x => x.Contributions.IsEmpty ? [StateContribution.Unlabeled(x.Value)] : x.Contributions);
        return new State<T>(StateKind, value, [..contributions]);
    }

    /// <summary>
    ///     The timeline, its values recorded as made of the given timelines, while a trace runs; such as one whose
    ///     value is several layers' parts.
    /// </summary>
    internal StateTimeline<T> WithSources(ImmutableArray<StateTimeline<T>> sources)
    {
        return new StateTimeline<T>(Duration, StateKind, _items, null, sources);
    }

    /// <summary>The timeline, its values recorded as coming from the given layer.</summary>
    public StateTimeline<T> WithLayer(string layer)
    {
        return new StateTimeline<T>(Duration, StateKind, _items, layer, []);
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

        return timelineArray
            .Where(x => x.Duration > 0)
            .GroupBy(x => x.StateKind)
            .Select(group => group.Key.MergeTimelines(group))
            // a timeline with only the default value adds nothing; the duration is kept by the owner of the timelines
            .Where(x => !x.IsDefault)
            .ToImmutableArray();
    }

    public static StateTimelineMap ToStateTimelineMap(this IEnumerable<IStateTimeline> timelines, double duration)
    {
        return StateTimelineMap.Create(duration, timelines);
    }
}

public interface IStateTimeline
{
    double Duration { get; }

    bool IsDefault { get; }

    IStateKind StateKind { get; }

    ImmutableArray<double> Positions { get; }

    object GetEffectiveValueAt(double position);

    IState GetEffectiveStateAt(double position);

    IStateTimeline Trim(double duration);

    IStateTimeline Shift(double offset);
}
