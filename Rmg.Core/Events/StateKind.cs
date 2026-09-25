using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Rmg.Core.Events;

[DebuggerDisplay("State Kind {Name} ({typeof(T)})")]
public sealed class StateKind<T> : IStateKind
    where T : notnull
{
    private readonly Func<IEnumerable<T>, T> _aggregateFunc;
    private readonly IState _defaultStateObject;

    private readonly object _defaultValueObject;
    private readonly Func<T, T, bool> _equalityFunc;
    private readonly Func<T, int> _hashFunc;

    public StateKind(
        string name,
        T defaultValue,
        Func<T, T, bool> equalityFunc,
        Func<IEnumerable<T>, T> aggregateFunc,
        Func<T, int>? hashFunc = null
    )
    {
        Name = name;
        DefaultValue = defaultValue;
        DefaultState = new State<T>(this, defaultValue);
        _equalityFunc = equalityFunc;
        _aggregateFunc = aggregateFunc;
        _hashFunc = hashFunc ?? (value => EqualityComparer<T>.Default.GetHashCode(value));

        _defaultValueObject = DefaultValue;
        _defaultStateObject = DefaultState;

        // each kind gets its own empty timeline so that it reports the kind's default value, not default(T)
        EmptyTimeline = new StateTimeline<T>(0, this, []);
    }

    public static StateKind<T> Empty { get; } = CreateEmpty();

    public T DefaultValue { get; }

    public State<T> DefaultState { get; }

    public StateTimeline<T> EmptyTimeline { get; }

    public string Name { get; }

    object IStateKind.DefaultValue => _defaultValueObject;

    IState IStateKind.DefaultState => _defaultStateObject;

    IStateTimeline IStateKind.EmptyTimeline => EmptyTimeline;

    public object AggregateValues(IEnumerable values)
    {
        return AggregateValues(values.Cast<T>());
    }

    public bool CheckValuesEqual(object value1, object value2)
    {
        return _equalityFunc((T)value1, (T)value2);
    }

    public bool CheckValueIsDefault(object value)
    {
        return CheckValueIsDefault((T)value);
    }

    public IState AggregateValuesToState(IEnumerable values)
    {
        return AggregateValuesToState(values.Cast<T>());
    }

    public IState AggregateStates(IEnumerable<IState> states)
    {
        return AggregateValuesToState(states.Select(x => (T)x.Value));
    }

    IState IStateKind.CreateState(object value)
    {
        return CreateState((T)value);
    }

    public IStateTimeline MergeTimelines(IEnumerable<IStateTimeline> timelines)
    {
        return MergeTimelines(timelines.Cast<StateTimeline<T>>());
    }

    IStateTimeline IStateKind.CreateTimelineFromValue(double duration, object value)
    {
        return CreateTimelineFromValue(duration, (T)value);
    }

    IStateTimeline IStateKind.ExtractStateTimeline(EventTimeline<StateMap> eventTimeline)
    {
        return ExtractStateTimeline(eventTimeline);
    }

    // The field is looked up by name, which a trimmed or native build cannot follow on its own, so it is kept
    // by name here. Every ImmutableArray<> this reaches is one the program itself constructs, so its
    // instantiation is always compiled in; only the field's metadata could otherwise go missing.
    [DynamicDependency(nameof(ImmutableArray<int>.Empty), typeof(ImmutableArray<>))]
    [UnconditionalSuppressMessage("Trimming", "IL2090", Justification = "ImmutableArray<>.Empty is kept by the DynamicDependency above.")]
    private static StateKind<T> CreateEmpty()
    {
        var type = typeof(T);
        T defaultValue;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
        {
            // default(ImmutableArray<>) is an uninitialized array that throws on access
            var emptyField = type.GetField(nameof(ImmutableArray<int>.Empty))!;
            defaultValue = (T)emptyField.GetValue(null)!;
        }
        else
        {
            defaultValue = default!;
        }

        return new StateKind<T>(
            "Empty",
            defaultValue,
            (_, _) => throw new NotImplementedException(),
            _ => throw new NotImplementedException()
        );
    }

    public T AggregateValues(IEnumerable<T> values)
    {
        return _aggregateFunc(values);
    }

    public bool CheckValuesEqual(T value1, T value2)
    {
        return _equalityFunc(value1, value2);
    }

    public int GetValueHashCode(T value)
    {
        return _hashFunc(value);
    }

    public bool CheckValueIsDefault(T value)
    {
        return CheckValuesEqual(value, DefaultValue);
    }

    public State<T> AggregateValuesToState(IEnumerable<T> values)
    {
        return new State<T>(this, AggregateValues(values));
    }

    public State<T> AggregateStates(IEnumerable<State<T>> states)
    {
        return AggregateValuesToState(states.Select(x => x.Value));
    }

    public State<T> CreateState(T value)
    {
        return new State<T>(this, value);
    }

    public StateTimeline<T> MergeTimelines(IEnumerable<StateTimeline<T>> timelines)
    {
        var timelinesArray = timelines.AsImmutableArray();
        if (timelinesArray.Length == 0 || timelinesArray.All(x => x.IsEmpty))
            return EmptyTimeline;

        if (timelinesArray.Any(x => x.StateKind != this))
            throw new ArgumentException($"Timelines must have the state kind {Name}.", nameof(timelines));

        return StateTimeline<T>.Merge(timelinesArray);
    }

    public StateTimeline<T> CreateTimelineFromValue(double duration, T value)
    {
        if (duration <= 0 || CheckValueIsDefault(value))
            return EmptyTimeline;

        return new StateTimeline<T>(duration, this, [new TimelineItem<T>(0, value)]);
    }

    public StateTimeline<T> CreateStateTimelineFromEventTimeline(EventTimeline<T> eventTimeline)
    {
        if (eventTimeline.IsEmpty)
            return EmptyTimeline;

        return StateTimeline.Create(eventTimeline.Duration, this, eventTimeline.ToImmutableArray());
    }

    public StateTimeline<T> ExtractStateTimeline(EventTimeline<StateMap> eventTimeline)
    {
        if (eventTimeline.IsEmpty)
            return EmptyTimeline;

        var items = eventTimeline.MapValues(x => x.GetStateValue(this));
        return StateTimeline.Create(eventTimeline.Duration, this, items.ToImmutableArray());
    }
}

public static class StateKind
{
    public static StateKind<T> Empty<T>()
        where T : notnull
    {
        return StateKind<T>.Empty;
    }
}

public interface IStateKind
{
    string Name { get; }

    object DefaultValue { get; }

    IState DefaultState { get; }

    IStateTimeline EmptyTimeline { get; }

    object AggregateValues(IEnumerable values);

    bool CheckValuesEqual(object value1, object value2);

    bool CheckValueIsDefault(object value);

    IState AggregateValuesToState(IEnumerable values);

    IState AggregateStates(IEnumerable<IState> values);

    IState CreateState(object value);

    IStateTimeline MergeTimelines(IEnumerable<IStateTimeline> timelines);

    IStateTimeline CreateTimelineFromValue(double duration, object value);

    IStateTimeline ExtractStateTimeline(EventTimeline<StateMap> eventTimeline);
}
